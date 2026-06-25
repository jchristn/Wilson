namespace Wilson.Server
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Text;
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using System.Threading;
    using System.Threading.Tasks;
    using WatsonWebserver;
    using WatsonWebserver.Core;
    using Wilson.Core.Database;
    using Wilson.Core.Helpers;
    using Wilson.Core.Models;
    using Wilson.Core.Services;
    using Wilson.Core.Settings;

    /// <summary>
    /// Wilson Watson server host.
    /// </summary>
    public sealed class WilsonServer
    {
        private static readonly AsyncLocal<RequestCapture?> _RequestCapture = new AsyncLocal<RequestCapture?>();
        private static readonly JsonSerializerOptions _SseJson = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        private const string _NewConversationTitle = "New Conversation";
        private const int _TitleGenerationCharacterThreshold = 300;
        private readonly JsonSerializerOptions _Json;
        private readonly string _SettingsFile;
        private readonly CancellationTokenSource _TokenSource = new CancellationTokenSource();

        /// <summary>
        /// Settings.
        /// </summary>
        public Settings Settings { get; private set; }

        /// <summary>
        /// Database.
        /// </summary>
        public DatabaseDriver Database { get; }

        /// <summary>
        /// Inference service.
        /// </summary>
        public InferenceService Inference { get; private set; }

        /// <summary>
        /// Model runner health check service.
        /// </summary>
        public ModelRunnerHealthCheckService HealthChecks { get; }

        /// <summary>
        /// Watson webserver.
        /// </summary>
        public Webserver Server { get; }

        private WilsonServer(Settings settings, string settingsFile)
        {
            Settings = settings;
            _SettingsFile = settingsFile;
            _Json = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = true };
            _Json.Converters.Add(new JsonStringEnumConverter());
            Database = new DatabaseDriver(settings.Database);
            Inference = new InferenceService(settings);
            HealthChecks = new ModelRunnerHealthCheckService(settings);
            WebserverSettings webserverSettings = new WebserverSettings(settings.Rest.Hostname, settings.Rest.Port, settings.Rest.Ssl);
            Server = new Webserver(webserverSettings, DefaultRouteAsync);
            ConfigureWatson();
        }

        /// <summary>
        /// Create a server host.
        /// </summary>
        public static async Task<WilsonServer> CreateAsync(string[] args)
        {
            string settingsFile = args != null && args.Length > 0 && !String.IsNullOrWhiteSpace(args[0]) ? args[0] : "wilson.json";
            JsonSerializerOptions json = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = true };
            json.Converters.Add(new JsonStringEnumConverter());
            Settings settings;
            if (!File.Exists(settingsFile))
            {
                settings = DefaultSettings();
                File.WriteAllText(settingsFile, JsonSerializer.Serialize(settings, json));
            }
            else
            {
                settings = JsonSerializer.Deserialize<Settings>(File.ReadAllText(settingsFile), json) ?? DefaultSettings();
            }

            NormalizeSettings(settings);
            WilsonServer server = new WilsonServer(settings, settingsFile);
            await server.Database.InitializeAsync().ConfigureAwait(false);
            await server.Database.SeedAsync(settings.Seed).ConfigureAwait(false);
            return server;
        }

        /// <summary>
        /// Run the server until cancellation.
        /// </summary>
        public async Task RunAsync()
        {
            Console.WriteLine();
            Console.WriteLine(
"""
                  o8o  oooo                                 
                  `"'  `888                                 
oooo oooo    ooo oooo   888   .oooo.o  .ooooo.  ooo. .oo.   
 `88. `88.  .8'  `888   888  d88(  "8 d88' `88b `888P"Y88b  
  `88..]88..8'    888   888  `"Y88b.  888   888  888   888  
   `888'`888'     888   888  o.  )88b 888   888  888   888  
    `8'  `8'     o888o o888o 8""888P' `Y8bod8P' o888o o888o 


""");

            Console.WriteLine("Wilson server listening on " + Settings.Rest.Hostname + ":" + Settings.Rest.Port);
            Console.WriteLine("Default admin bearer token: " + String.Join(", ", Settings.Auth.AdminBearerTokens));
            Console.WriteLine("Default user access key: " + Settings.Seed.AccessKey);
            HealthChecks.Start(_TokenSource.Token);
            _ = Task.Run(() => Server.StartAsync(_TokenSource.Token), _TokenSource.Token);
            Console.CancelKeyPress += (sender, eventArgs) =>
            {
                eventArgs.Cancel = true;
                _TokenSource.Cancel();
            };
            try
            {
                while (!_TokenSource.Token.IsCancellationRequested) await Task.Delay(500, _TokenSource.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
            }
            finally
            {
                await HealthChecks.StopAsync().ConfigureAwait(false);
            }
        }

        private static Settings DefaultSettings()
        {
            Settings settings = new Settings();
            settings.ModelRunners.Add(new ModelRunnerSettings
            {
                Id = "local-ollama",
                Name = "Local Ollama",
                ApiType = "Ollama",
                Endpoint = "http://localhost:11434",
                Models = new List<string>(),
                ContextWindowTokens = 8192
            });
            NormalizeSettings(settings);
            return settings;
        }

        private static void NormalizeSettings(Settings settings)
        {
            settings.ModelRunners ??= new List<ModelRunnerSettings>();
            foreach (ModelRunnerSettings runner in settings.ModelRunners)
            {
                runner.Models ??= new List<string>();
                ModelRunnerSettings.ApplyHealthCheckDefaults(runner);
            }
        }

        private void AttachHealth(List<ModelRunnerStatus> statuses)
        {
            foreach (ModelRunnerStatus status in statuses)
            {
                status.Health = HealthChecks.GetHealthStatus(status.Id);
            }
        }

        private void ConfigureWatson()
        {
            Server.Routes.Preflight = async (ctx) =>
            {
                ApplyCors(ctx.Response);
                ctx.Response.StatusCode = 204;
                await ctx.Response.Send(ctx.Token).ConfigureAwait(false);
            };

            Server.Routes.PostRouting = async (ctx) =>
            {
                ApplyCors(ctx.Response);
                await Task.CompletedTask.ConfigureAwait(false);
            };
        }

        private async Task DefaultRouteAsync(HttpContextBase ctx)
        {
            Stopwatch stopwatch = Stopwatch.StartNew();
            RequestContext? requestContext = null;
            string path = ctx.Request.Url.RawWithoutQuery;
            string method = ctx.Request.Method.ToString().ToUpperInvariant();
            _RequestCapture.Value = new RequestCapture();
            try
            {
                ApplyCors(ctx.Response);
                requestContext = await AuthenticateAsync(ctx, path).ConfigureAwait(false);
                await DispatchAsync(ctx, requestContext, method, path).ConfigureAwait(false);
            }
            catch (UnauthorizedAccessException ex)
            {
                ctx.Response.StatusCode = 401;
                await SendJsonAsync(ctx, new { error = ex.Message }).ConfigureAwait(false);
            }
            catch (KeyNotFoundException ex)
            {
                ctx.Response.StatusCode = 404;
                await SendJsonAsync(ctx, new { error = ex.Message }).ConfigureAwait(false);
            }
            catch (ArgumentException ex)
            {
                ctx.Response.StatusCode = 400;
                await SendJsonAsync(ctx, new { error = ex.Message }).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                ctx.Response.StatusCode = 500;
                await SendJsonAsync(ctx, new { error = ex.Message }).ConfigureAwait(false);
            }
            finally
            {
                stopwatch.Stop();
                if (Settings.RequestHistory.Enabled && !path.Equals("/v1.0/api/request-history", StringComparison.OrdinalIgnoreCase))
                {
                    RequestHistoryEntry entry = new RequestHistoryEntry
                    {
                        TenantId = requestContext?.TenantId,
                        UserId = requestContext?.UserId,
                        Method = method,
                        Path = path,
                        StatusCode = ctx.Response.StatusCode,
                        DurationMs = stopwatch.Elapsed.TotalMilliseconds,
                        RequestHeaders = RequestHeaders(ctx),
                        RequestBody = ctx.Request.DataAsString ?? String.Empty,
                        ResponseHeaders = ResponseHeaders(ctx),
                        ResponseBody = _RequestCapture.Value?.ResponseBody ?? String.Empty,
                        TimeToFirstTokenMs = _RequestCapture.Value?.TimeToFirstTokenMs ?? 0,
                        StreamingTimeMs = _RequestCapture.Value?.StreamingTimeMs ?? 0,
                        TotalTimeMs = _RequestCapture.Value?.TotalTimeMs ?? 0,
                        TokensUsed = _RequestCapture.Value?.TokensUsed ?? 0
                    };
                    _ = Task.Run(async () =>
                    {
                        try { await Database.CreateRequestHistoryAsync(entry).ConfigureAwait(false); }
                        catch { }
                    });
                }
                _RequestCapture.Value = null;
            }
        }

        private async Task DispatchAsync(HttpContextBase ctx, RequestContext? requestContext, string method, string path)
        {
            if (method == "GET" && (path == "/" || path == "/health" || path == "/v1.0/api/health"))
            {
                await SendJsonAsync(ctx, new { status = "healthy", service = "Wilson" }).ConfigureAwait(false);
                return;
            }

            if (method == "GET" && path == "/openapi.json")
            {
                await SendJsonAsync(ctx, OpenApi()).ConfigureAwait(false);
                return;
            }

            if (method == "GET" && path == "/swagger")
            {
                ctx.Response.ContentType = "text/html";
                await ctx.Response.Send(SwaggerHtml(), ctx.Token).ConfigureAwait(false);
                return;
            }

            if (method == "POST" && path == "/v1.0/auth/token")
            {
                Dictionary<string, string> body = Body<Dictionary<string, string>>(ctx);
                string accessKey = body.ContainsKey("accessKey") ? body["accessKey"] : body.ContainsKey("token") ? body["token"] : String.Empty;
                RequestContext auth = await AuthenticateTokenAsync(accessKey).ConfigureAwait(false);
                await SendJsonAsync(ctx, new { token = accessKey, user = auth }).ConfigureAwait(false);
                return;
            }

            RequireAuth(requestContext);

            if (method == "GET" && path == "/v1.0/api/me")
            {
                await SendJsonAsync(ctx, requestContext!).ConfigureAwait(false);
                return;
            }

            if (path == "/v1.0/api/model-runners" && method == "GET")
            {
                bool includeLiveStatus = !String.Equals(Query(ctx, "includeLiveStatus"), "false", StringComparison.OrdinalIgnoreCase);
                List<ModelRunnerStatus> statuses = await Inference.GetRunnerStatusesAsync(includeLiveStatus, ctx.Token).ConfigureAwait(false);
                AttachHealth(statuses);
                await SendJsonAsync(ctx, Enumerate(statuses, Enumeration(ctx))).ConfigureAwait(false);
                return;
            }

            if (path == "/v1.0/api/model-runners/health" && method == "GET")
            {
                await SendJsonAsync(ctx, HealthChecks.GetAllHealthStatuses()).ConfigureAwait(false);
                return;
            }

            if (path.StartsWith("/v1.0/api/model-runners/", StringComparison.OrdinalIgnoreCase) && path.EndsWith("/health", StringComparison.OrdinalIgnoreCase) && method == "GET")
            {
                string id = Segment(path, 3);
                EndpointHealthStatus? health = HealthChecks.GetHealthStatus(id);
                if (health == null) throw new KeyNotFoundException("No health data available for model server '" + id + "'.");
                await SendJsonAsync(ctx, health).ConfigureAwait(false);
                return;
            }

            if (path.StartsWith("/v1.0/api/model-runners/", StringComparison.OrdinalIgnoreCase) && path.EndsWith("/pull", StringComparison.OrdinalIgnoreCase) && method == "POST")
            {
                RequireAdmin(requestContext);
                ModelPullRequest body = Body<ModelPullRequest>(ctx);
                await SendJsonAsync(ctx, await Inference.PullOllamaModelAsync(Segment(path, 3), body.Model, ctx.Token).ConfigureAwait(false)).ConfigureAwait(false);
                return;
            }

            if (path.StartsWith("/v1.0/api/model-runners/", StringComparison.OrdinalIgnoreCase) && path.EndsWith("/load", StringComparison.OrdinalIgnoreCase) && method == "POST")
            {
                ModelPullRequest body = Body<ModelPullRequest>(ctx);
                await SendJsonAsync(ctx, await Inference.LoadOllamaModelAsync(Segment(path, 3), body.Model, ctx.Token).ConfigureAwait(false)).ConfigureAwait(false);
                return;
            }

            if (path == "/v1.0/api/settings" && method == "GET")
            {
                RequireAdmin(requestContext);
                await SendJsonAsync(ctx, Settings).ConfigureAwait(false);
                return;
            }

            if (path == "/v1.0/api/settings" && method == "PUT")
            {
                RequireAdmin(requestContext);
                Settings updated = Body<Settings>(ctx);
                NormalizeSettings(updated);
                Settings = updated;
                Inference = new InferenceService(Settings);
                HealthChecks.UpdateSettings(Settings);
                File.WriteAllText(_SettingsFile, JsonSerializer.Serialize(Settings, _Json));
                await SendJsonAsync(ctx, Settings).ConfigureAwait(false);
                return;
            }

            if (path == "/v1.0/api/tenants")
            {
                RequireAdmin(requestContext);
                if (method == "GET") { await SendJsonAsync(ctx, Enumerate(await Database.GetTenantsAsync(ctx.Token).ConfigureAwait(false), Enumeration(ctx))).ConfigureAwait(false); return; }
                if (method == "POST") { Tenant item = Body<Tenant>(ctx); if (String.IsNullOrWhiteSpace(item.Id)) item.Id = IdGenerator.Tenant(); await Database.CreateTenantAsync(item, ctx.Token).ConfigureAwait(false); ctx.Response.StatusCode = 201; await SendJsonAsync(ctx, item).ConfigureAwait(false); return; }
            }

            if (path.StartsWith("/v1.0/api/tenants/", StringComparison.OrdinalIgnoreCase))
            {
                RequireAdmin(requestContext);
                string id = Segment(path, 3);
                Tenant? existing = await Database.GetTenantAsync(id, ctx.Token).ConfigureAwait(false);
                if (existing == null) throw new KeyNotFoundException("Tenant not found.");
                if (method == "GET") { await SendJsonAsync(ctx, existing).ConfigureAwait(false); return; }
                if (method == "PUT") { Tenant item = Body<Tenant>(ctx); item.Id = id; await Database.UpdateTenantAsync(item, ctx.Token).ConfigureAwait(false); await SendJsonAsync(ctx, item).ConfigureAwait(false); return; }
                if (method == "DELETE") { await Database.DeleteTenantAsync(id, ctx.Token).ConfigureAwait(false); ctx.Response.StatusCode = 204; await ctx.Response.Send(ctx.Token).ConfigureAwait(false); return; }
            }

            if (path == "/v1.0/api/users")
            {
                RequireTenantAdmin(requestContext);
                string? tenantId = requestContext!.IsAdmin ? Query(ctx, "tenantId") : requestContext.TenantId;
                if (method == "GET") { await SendJsonAsync(ctx, Enumerate(await Database.GetUsersAsync(tenantId, ctx.Token).ConfigureAwait(false), Enumeration(ctx))).ConfigureAwait(false); return; }
                if (method == "POST") { User item = Body<User>(ctx); if (!requestContext.IsAdmin) item.TenantId = requestContext.TenantId!; await Database.CreateUserAsync(item, ctx.Token).ConfigureAwait(false); ctx.Response.StatusCode = 201; await SendJsonAsync(ctx, item).ConfigureAwait(false); return; }
            }

            if (path.StartsWith("/v1.0/api/users/", StringComparison.OrdinalIgnoreCase))
            {
                RequireTenantAdmin(requestContext);
                string id = Segment(path, 3);
                string tenantId = requestContext!.IsAdmin ? Query(ctx, "tenantId") ?? String.Empty : requestContext.TenantId!;
                User? existing = await Database.GetUserAsync(tenantId, id, ctx.Token).ConfigureAwait(false);
                if (existing == null) throw new KeyNotFoundException("User not found.");
                if (method == "GET") { await SendJsonAsync(ctx, existing).ConfigureAwait(false); return; }
                if (method == "PUT") { User item = Body<User>(ctx); item.Id = id; item.TenantId = tenantId; await Database.UpdateUserAsync(item, ctx.Token).ConfigureAwait(false); await SendJsonAsync(ctx, item).ConfigureAwait(false); return; }
                if (method == "DELETE") { await Database.DeleteUserAsync(tenantId, id, ctx.Token).ConfigureAwait(false); ctx.Response.StatusCode = 204; await ctx.Response.Send(ctx.Token).ConfigureAwait(false); return; }
            }

            if (path == "/v1.0/api/credentials")
            {
                RequireTenantAdmin(requestContext);
                string? tenantId = requestContext!.IsAdmin ? Query(ctx, "tenantId") : requestContext.TenantId;
                if (method == "GET") { await SendJsonAsync(ctx, Enumerate(await Database.GetCredentialsAsync(tenantId, ctx.Token).ConfigureAwait(false), Enumeration(ctx))).ConfigureAwait(false); return; }
                if (method == "POST") { Credential item = Body<Credential>(ctx); if (!requestContext.IsAdmin) item.TenantId = requestContext.TenantId!; await Database.CreateCredentialAsync(item, ctx.Token).ConfigureAwait(false); ctx.Response.StatusCode = 201; await SendJsonAsync(ctx, item).ConfigureAwait(false); return; }
            }

            if (path.StartsWith("/v1.0/api/credentials/", StringComparison.OrdinalIgnoreCase))
            {
                RequireTenantAdmin(requestContext);
                string id = Segment(path, 3);
                string tenantId = requestContext!.IsAdmin ? Query(ctx, "tenantId") ?? String.Empty : requestContext.TenantId!;
                Credential? existing = await Database.GetCredentialAsync(tenantId, id, ctx.Token).ConfigureAwait(false);
                if (existing == null) throw new KeyNotFoundException("Credential not found.");
                if (method == "GET") { await SendJsonAsync(ctx, existing).ConfigureAwait(false); return; }
                if (method == "PUT") { Credential item = Body<Credential>(ctx); item.Id = id; item.TenantId = tenantId; item.UserId = existing.UserId; item.AccessKey = existing.AccessKey; item.SecretLast4 = existing.SecretLast4; await Database.UpdateCredentialAsync(item, ctx.Token).ConfigureAwait(false); await SendJsonAsync(ctx, item).ConfigureAwait(false); return; }
                if (method == "DELETE") { await Database.DeleteCredentialAsync(tenantId, id, ctx.Token).ConfigureAwait(false); ctx.Response.StatusCode = 204; await ctx.Response.Send(ctx.Token).ConfigureAwait(false); return; }
            }

            if (path == "/v1.0/api/conversations" && method == "GET")
            {
                await SendJsonAsync(ctx, Enumerate(await Database.GetConversationsAsync(requestContext!.TenantId!, requestContext.UserId, requestContext.IsAdmin || requestContext.IsTenantAdmin, ctx.Token).ConfigureAwait(false), Enumeration(ctx))).ConfigureAwait(false);
                return;
            }

            if (path == "/v1.0/api/conversations" && method == "POST")
            {
                Conversation item = Body<Conversation>(ctx);
                item.TenantId = requestContext!.TenantId!;
                item.UserId = requestContext.UserId ?? String.Empty;
                await Database.CreateConversationAsync(item, ctx.Token).ConfigureAwait(false);
                ctx.Response.StatusCode = 201;
                await SendJsonAsync(ctx, item).ConfigureAwait(false);
                return;
            }

            if (path.StartsWith("/v1.0/api/conversations/", StringComparison.OrdinalIgnoreCase) && path.EndsWith("/messages", StringComparison.OrdinalIgnoreCase) && method == "GET")
            {
                string id = Segment(path, 3);
                await SendJsonAsync(ctx, Enumerate(await Database.GetMessagesAsync(requestContext!.TenantId!, id, ctx.Token).ConfigureAwait(false), Enumeration(ctx))).ConfigureAwait(false);
                return;
            }

            if (path.StartsWith("/v1.0/api/conversations/", StringComparison.OrdinalIgnoreCase) && !path.EndsWith("/messages", StringComparison.OrdinalIgnoreCase))
            {
                string id = Segment(path, 3);
                Conversation? existing = await Database.GetConversationAsync(requestContext!.TenantId!, id, ctx.Token).ConfigureAwait(false);
                if (existing == null) throw new KeyNotFoundException("Conversation not found.");
                if (!requestContext.IsAdmin && !requestContext.IsTenantAdmin && !String.Equals(existing.UserId, requestContext.UserId, StringComparison.OrdinalIgnoreCase))
                {
                    throw new UnauthorizedAccessException("Conversation access denied.");
                }

                if (method == "PUT")
                {
                    Conversation item = Body<Conversation>(ctx);
                    existing.Title = String.IsNullOrWhiteSpace(item.Title) ? existing.Title : item.Title.Trim();
                    existing.RunnerId = String.IsNullOrWhiteSpace(item.RunnerId) ? existing.RunnerId : item.RunnerId;
                    existing.Model = String.IsNullOrWhiteSpace(item.Model) ? existing.Model : item.Model;
                    existing.Active = item.Active;
                    await Database.UpdateConversationAsync(existing, ctx.Token).ConfigureAwait(false);
                    await SendJsonAsync(ctx, existing).ConfigureAwait(false);
                    return;
                }

                if (method == "DELETE")
                {
                    await Database.DeleteConversationAsync(requestContext.TenantId!, id, ctx.Token).ConfigureAwait(false);
                    ctx.Response.StatusCode = 204;
                    await ctx.Response.Send(ctx.Token).ConfigureAwait(false);
                    return;
                }
            }

            if (path == "/v1.0/api/chat" && method == "POST")
            {
                await ChatAsync(ctx, requestContext!, false).ConfigureAwait(false);
                return;
            }

            if (path == "/v1.0/api/chat/stream" && method == "POST")
            {
                await ChatAsync(ctx, requestContext!, true).ConfigureAwait(false);
                return;
            }

            if (path == "/v1.0/api/feedback" && method == "GET")
            {
                RequireTenantAdmin(requestContext);
                await SendJsonAsync(ctx, Enumerate(await Database.GetFeedbackAsync(requestContext!.IsAdmin ? Query(ctx, "tenantId") : requestContext.TenantId, ctx.Token).ConfigureAwait(false), Enumeration(ctx))).ConfigureAwait(false);
                return;
            }

            if (path == "/v1.0/api/feedback" && method == "POST")
            {
                Feedback item = Body<Feedback>(ctx);
                item.TenantId = requestContext!.TenantId!;
                item.UserId = requestContext.UserId ?? String.Empty;
                await Database.CreateFeedbackAsync(item, ctx.Token).ConfigureAwait(false);
                ctx.Response.StatusCode = 201;
                await SendJsonAsync(ctx, item).ConfigureAwait(false);
                return;
            }

            if (path == "/v1.0/api/request-history" && method == "GET")
            {
                RequireTenantAdmin(requestContext);
                await SendJsonAsync(ctx, Enumerate(await Database.GetRequestHistoryAsync(requestContext!.IsAdmin ? Query(ctx, "tenantId") : requestContext.TenantId, ctx.Token).ConfigureAwait(false), Enumeration(ctx))).ConfigureAwait(false);
                return;
            }

            if (path == "/v1.0/api/request-history/summary" && method == "GET")
            {
                RequireTenantAdmin(requestContext);
                DateTime from = ParseUtc(Query(ctx, "fromUtc"), DateTime.UtcNow.AddHours(-24));
                DateTime to = ParseUtc(Query(ctx, "toUtc"), DateTime.UtcNow);
                Int32.TryParse(Query(ctx, "bucketMinutes"), out int bucketMinutes);
                if (bucketMinutes < 1) bucketMinutes = 60;
                await SendJsonAsync(ctx, await Database.SummarizeRequestHistoryAsync(requestContext!.IsAdmin ? Query(ctx, "tenantId") : requestContext.TenantId, from, to, bucketMinutes, ctx.Token).ConfigureAwait(false)).ConfigureAwait(false);
                return;
            }

            if (path.StartsWith("/v1.0/api/request-history/", StringComparison.OrdinalIgnoreCase) && method == "DELETE")
            {
                RequireTenantAdmin(requestContext);
                string id = Segment(path, 3);
                await Database.DeleteRequestHistoryAsync(requestContext!.IsAdmin ? Query(ctx, "tenantId") : requestContext.TenantId, id, ctx.Token).ConfigureAwait(false);
                ctx.Response.StatusCode = 204;
                await ctx.Response.Send(ctx.Token).ConfigureAwait(false);
                return;
            }

            ctx.Response.StatusCode = 404;
            await SendJsonAsync(ctx, new { error = "Not found" }).ConfigureAwait(false);
        }

        private async Task ChatAsync(HttpContextBase ctx, RequestContext requestContext, bool streaming)
        {
            ChatRequest body = Body<ChatRequest>(ctx);
            ModelRunnerSettings runner = Inference.GetRunner(body.RunnerId);
            if (!await Inference.IsChatCapableModelAsync(runner, body.Model, ctx.Token).ConfigureAwait(false))
            {
                ctx.Response.StatusCode = 400;
                await SendJsonAsync(ctx, new { error = "The selected model cannot generate chat responses. Choose a chat or completion model instead of an embedding-only model." }).ConfigureAwait(false);
                return;
            }

            Conversation? conversation = String.IsNullOrWhiteSpace(body.ConversationId) ? null : await Database.GetConversationAsync(requestContext.TenantId!, body.ConversationId, ctx.Token).ConfigureAwait(false);
            if (conversation == null)
            {
                conversation = new Conversation { TenantId = requestContext.TenantId!, UserId = requestContext.UserId ?? String.Empty, RunnerId = body.RunnerId, Model = body.Model, Title = _NewConversationTitle };
                await Database.CreateConversationAsync(conversation, ctx.Token).ConfigureAwait(false);
            }
            List<ChatMessage> messages = await Database.GetMessagesAsync(requestContext.TenantId!, conversation.Id, ctx.Token).ConfigureAwait(false);
            ChatMessage userMessage = new ChatMessage { TenantId = requestContext.TenantId!, ConversationId = conversation.Id, Role = "user", Content = body.Prompt, RunnerId = body.RunnerId, Model = body.Model, TokenEstimate = InferenceService.EstimateTokens(body.Prompt) };
            await Database.CreateMessageAsync(userMessage, ctx.Token).ConfigureAwait(false);
            PromptBuildResult promptBuild = Inference.BuildPromptWithMetadata(messages, body.Prompt, runner.ContextWindowTokens);
            string prompt = promptBuild.Prompt;
            ChatTruncationNotice truncation = new ChatTruncationNotice
            {
                ConversationId = conversation.Id,
                Truncated = promptBuild.OmittedMessageCount > 0,
                IncludedMessageCount = promptBuild.IncludedMessageCount,
                OmittedMessageCount = promptBuild.OmittedMessageCount,
                PromptTokenEstimate = promptBuild.PromptTokenEstimate,
                PromptBudgetTokens = promptBuild.PromptBudgetTokens,
                ContextWindowTokens = promptBuild.ContextWindowTokens
            };

            if (!streaming)
            {
                Stopwatch inference = Stopwatch.StartNew();
                string answer = await Inference.ChatAsync(runner, body.Model, prompt, body.Settings, ctx.Token).ConfigureAwait(false);
                inference.Stop();
                int outputTokens = InferenceService.EstimateTokens(answer);
                int inputTokens = InferenceService.EstimateTokens(prompt);
                ChatMessage assistantMessage = new ChatMessage { TenantId = requestContext.TenantId!, ConversationId = conversation.Id, Role = "assistant", Content = answer, RunnerId = body.RunnerId, Model = body.Model, TokenEstimate = outputTokens, TimeToFirstTokenMs = inference.Elapsed.TotalMilliseconds, StreamingTimeMs = 0, TotalTimeMs = inference.Elapsed.TotalMilliseconds, TokensUsed = inputTokens + outputTokens };
                await Database.CreateMessageAsync(assistantMessage, ctx.Token).ConfigureAwait(false);
                conversation = await MaybeGenerateConversationTitleAsync(conversation, runner, body.Model, messages, userMessage, assistantMessage, ctx.Token).ConfigureAwait(false);
                SetRequestCapture(assistantMessage, answer);
                await SendJsonAsync(ctx, new { conversation, userMessage, assistantMessage, truncation }).ConfigureAwait(false);
                return;
            }

            ctx.Response.ContentType = "text/event-stream";
            ctx.Response.Headers.Add("Cache-Control", "no-cache");
            ctx.Response.Headers.Add("Connection", "keep-alive");
            ctx.Response.ChunkedTransfer = true;
            StringBuilder full = new StringBuilder();
            Stopwatch total = Stopwatch.StartNew();
            double firstTokenMs = 0;
            Stopwatch streamingTimer = new Stopwatch();
            await SendSseAsync(ctx, "conversation", conversation, false).ConfigureAwait(false);
            await SendSseAsync(ctx, "truncation", truncation, false).ConfigureAwait(false);
            try
            {
                await foreach (string chunk in Inference.ChatStreamingAsync(runner, body.Model, prompt, body.Settings, ctx.Token).ConfigureAwait(false))
                {
                    if (firstTokenMs <= 0)
                    {
                        firstTokenMs = total.Elapsed.TotalMilliseconds;
                        streamingTimer.Start();
                    }
                    full.Append(chunk);
                    await SendSseAsync(ctx, "chunk", new { text = chunk }, false).ConfigureAwait(false);
                }
                if (streamingTimer.IsRunning) streamingTimer.Stop();
                total.Stop();
                int storedTokens = InferenceService.EstimateTokens(full.ToString());
                int totalTokens = InferenceService.EstimateTokens(prompt) + storedTokens;
                ChatMessage stored = new ChatMessage { TenantId = requestContext.TenantId!, ConversationId = conversation.Id, Role = "assistant", Content = full.ToString(), RunnerId = body.RunnerId, Model = body.Model, TokenEstimate = storedTokens, TimeToFirstTokenMs = firstTokenMs, StreamingTimeMs = streamingTimer.Elapsed.TotalMilliseconds, TotalTimeMs = total.Elapsed.TotalMilliseconds, TokensUsed = totalTokens };
                await Database.CreateMessageAsync(stored, ctx.Token).ConfigureAwait(false);
                conversation = await MaybeGenerateConversationTitleAsync(conversation, runner, body.Model, messages, userMessage, stored, ctx.Token).ConfigureAwait(false);
                SetRequestCapture(stored, full.ToString());
                await SendSseAsync(ctx, "conversation", conversation, false).ConfigureAwait(false);
                await SendSseAsync(ctx, "done", stored, true).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                if (streamingTimer.IsRunning) streamingTimer.Stop();
                total.Stop();
                SetRequestCapture(new ChatMessage { TimeToFirstTokenMs = firstTokenMs, StreamingTimeMs = streamingTimer.Elapsed.TotalMilliseconds, TotalTimeMs = total.Elapsed.TotalMilliseconds, TokensUsed = InferenceService.EstimateTokens(prompt) }, ex.Message);
                await SendSseAsync(ctx, "error", new { error = "The selected model could not generate a chat response. Confirm that it is a chat or completion model, not an embedding-only model.", detail = ex.Message }, true).ConfigureAwait(false);
            }
        }

        private async Task<RequestContext?> AuthenticateAsync(HttpContextBase ctx, string path)
        {
            if (path == "/" || path == "/health" || path == "/v1.0/api/health" || path == "/openapi.json" || path == "/swagger" || path == "/v1.0/auth/token") return null;
            string header = ctx.Request.Headers.Get("Authorization") ?? String.Empty;
            if (String.IsNullOrWhiteSpace(header) || !header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)) throw new UnauthorizedAccessException("Bearer token is required.");
            string token = header.Substring("Bearer ".Length).Trim();
            return await AuthenticateTokenAsync(token).ConfigureAwait(false);
        }

        private async Task<RequestContext> AuthenticateTokenAsync(string token)
        {
            if (String.IsNullOrWhiteSpace(token)) throw new UnauthorizedAccessException("Bearer token is required.");
            if (Settings.Auth.AdminBearerTokens.Contains(token))
            {
                return new RequestContext { IsAuthenticated = true, IsAdmin = true, IsTenantAdmin = true, PrincipalName = "Settings administrator" };
            }
            Credential? credential = await Database.GetCredentialByAccessKeyAsync(token).ConfigureAwait(false);
            if (credential == null || !credential.Active) throw new UnauthorizedAccessException("Invalid bearer token.");
            User? user = await Database.GetUserAsync(credential.TenantId, credential.UserId).ConfigureAwait(false);
            if (user == null || !user.Active) throw new UnauthorizedAccessException("Invalid bearer token.");
            credential.LastUsedUtc = DateTime.UtcNow;
            await Database.UpdateCredentialAsync(credential).ConfigureAwait(false);
            return new RequestContext { IsAuthenticated = true, TenantId = user.TenantId, UserId = user.Id, IsAdmin = user.IsAdmin, IsTenantAdmin = user.IsTenantAdmin, PrincipalName = user.Email };
        }

        private static void RequireAuth(RequestContext? requestContext)
        {
            if (requestContext == null || !requestContext.IsAuthenticated) throw new UnauthorizedAccessException("Authentication required.");
        }

        private static void RequireAdmin(RequestContext? requestContext)
        {
            RequireAuth(requestContext);
            if (!requestContext!.IsAdmin) throw new UnauthorizedAccessException("Administrator access required.");
        }

        private static void RequireTenantAdmin(RequestContext? requestContext)
        {
            RequireAuth(requestContext);
            if (!requestContext!.IsAdmin && !requestContext.IsTenantAdmin) throw new UnauthorizedAccessException("Tenant administrator access required.");
        }

        private T Body<T>(HttpContextBase ctx)
        {
            string body = ctx.Request.DataAsString;
            if (String.IsNullOrWhiteSpace(body)) throw new ArgumentException("Request body is required.");
            T? value = JsonSerializer.Deserialize<T>(body, _Json);
            if (value == null) throw new ArgumentException("Invalid request body.");
            return value;
        }

        private async Task SendJsonAsync(HttpContextBase ctx, object value)
        {
            ctx.Response.ContentType = "application/json";
            await ctx.Response.Send(JsonSerializer.Serialize(value, _Json), ctx.Token).ConfigureAwait(false);
        }

        private void SetRequestCapture(ChatMessage message, string responseBody)
        {
            RequestCapture? capture = _RequestCapture.Value;
            if (capture == null) return;
            capture.TimeToFirstTokenMs = message.TimeToFirstTokenMs;
            capture.StreamingTimeMs = message.StreamingTimeMs;
            capture.TotalTimeMs = message.TotalTimeMs;
            capture.TokensUsed = message.TokensUsed;
            capture.ResponseBody = responseBody;
        }

        private string RequestHeaders(HttpContextBase ctx)
        {
            return JsonSerializer.Serialize(new Dictionary<string, string>
            {
                { "authorization", String.IsNullOrWhiteSpace(ctx.Request.Headers.Get("Authorization")) ? String.Empty : "Bearer [redacted]" },
                { "content-type", ctx.Request.Headers.Get("Content-Type") ?? String.Empty },
                { "accept", ctx.Request.Headers.Get("Accept") ?? String.Empty },
                { "user-agent", ctx.Request.Headers.Get("User-Agent") ?? String.Empty }
            }, _Json);
        }

        private string ResponseHeaders(HttpContextBase ctx)
        {
            return JsonSerializer.Serialize(new Dictionary<string, string>
            {
                { "content-type", ctx.Response.ContentType ?? String.Empty },
                { "status", ctx.Response.StatusCode.ToString() }
            }, _Json);
        }

        private async Task SendSseAsync(HttpContextBase ctx, string eventName, object value, bool done)
        {
            string payload = "event: " + eventName + "\n" + "data: " + JsonSerializer.Serialize(value, _SseJson) + "\n\n";
            byte[] bytes = Encoding.UTF8.GetBytes(payload);
            await ctx.Response.SendChunk(bytes, done, ctx.Token).ConfigureAwait(false);
        }

        private void ApplyCors(HttpResponseBase response)
        {
            if (!Settings.Cors.Enabled) return;
            response.Headers.Add("Access-Control-Allow-Origin", Settings.Cors.AllowedOrigins.Contains("*") ? "*" : Settings.Cors.AllowedOrigins.FirstOrDefault() ?? "*");
            response.Headers.Add("Access-Control-Allow-Methods", String.Join(", ", Settings.Cors.AllowedMethods));
            response.Headers.Add("Access-Control-Allow-Headers", String.Join(", ", Settings.Cors.AllowedHeaders));
        }

        private static string? Query(HttpContextBase ctx, string key)
        {
            return ctx.Request.Query.Elements.Get(key);
        }

        private static EnumerationQuery Enumeration(HttpContextBase ctx)
        {
            Int32.TryParse(Query(ctx, "pageNumber"), out int pageNumber);
            Int32.TryParse(Query(ctx, "pageSize"), out int pageSize);
            return new EnumerationQuery
            {
                PageNumber = pageNumber < 1 ? 1 : pageNumber,
                PageSize = pageSize < 1 ? 25 : Math.Min(pageSize, 500),
                Search = Query(ctx, "search"),
                TenantId = Query(ctx, "tenantId")
            };
        }

        private static EnumerationResult<T> Enumerate<T>(IEnumerable<T> source, EnumerationQuery query)
        {
            List<T> items = source.ToList();
            int total = items.Count;
            int totalPages = Math.Max(1, (int)Math.Ceiling(total / (double)query.PageSize));
            int pageNumber = Math.Min(Math.Max(1, query.PageNumber), totalPages);
            return new EnumerationResult<T>
            {
                Objects = items.Skip((pageNumber - 1) * query.PageSize).Take(query.PageSize).ToList(),
                PageNumber = pageNumber,
                PageSize = query.PageSize,
                TotalRecords = total,
                TotalPages = totalPages
            };
        }

        private static string Segment(string path, int index)
        {
            string[] parts = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
            return parts.Length > index ? parts[index] : String.Empty;
        }

        private async Task<Conversation> MaybeGenerateConversationTitleAsync(Conversation conversation, ModelRunnerSettings runner, string model, List<ChatMessage> previousMessages, ChatMessage userMessage, ChatMessage assistantMessage, CancellationToken token)
        {
            if (!String.Equals(conversation.Title, _NewConversationTitle, StringComparison.Ordinal)) return conversation;

            List<ChatMessage> titleMessages = new List<ChatMessage>(previousMessages) { userMessage, assistantMessage };
            int exchangedCharacters = titleMessages.Sum(message => message.Content?.Length ?? 0);
            if (exchangedCharacters < _TitleGenerationCharacterThreshold) return conversation;

            string transcript = String.Join(Environment.NewLine, titleMessages
                .Select(message => message.Role + ": " + (message.Content ?? String.Empty).Replace("\r", " ").Replace("\n", " ").Trim())
                .Where(line => line.Length > 0));
            if (transcript.Length > 4000) transcript = transcript.Substring(transcript.Length - 4000);

            try
            {
                string title = await Inference.ChatAsync(
                    runner,
                    model,
                    "Generate a short conversation title. Use 2 to 4 words, 32 characters or fewer. Return only the title, with no quotes, punctuation flourish, preamble, or explanation." + Environment.NewLine + Environment.NewLine + transcript,
                    new CompletionRequestSettings
                    {
                        SystemPrompt = "You write very short, clear conversation titles. Return only 2 to 4 words.",
                        Temperature = 0.2,
                        TopP = 0.9,
                        MaxTokens = 12
                    },
                    token).ConfigureAwait(false);

                title = CleanGeneratedTitle(title);
                if (String.IsNullOrWhiteSpace(title)) return conversation;
                conversation.Title = title;
                await Database.UpdateConversationAsync(conversation, token).ConfigureAwait(false);
            }
            catch
            {
            }

            return conversation;
        }

        private static string CleanGeneratedTitle(string title)
        {
            string clean = title.Replace("\r", " ").Replace("\n", " ").Trim().Trim('"', '\'', '`', '.', ':', '-', ' ');
            string[] words = clean.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (words.Length > 4) clean = String.Join(' ', words.Take(4));
            if (clean.Length > 32) clean = clean.Substring(0, 32).Trim();
            return String.IsNullOrWhiteSpace(clean) ? String.Empty : clean;
        }

        private static DateTime ParseUtc(string? value, DateTime defaultValue)
        {
            if (String.IsNullOrWhiteSpace(value)) return defaultValue;
            if (DateTime.TryParse(value, null, System.Globalization.DateTimeStyles.RoundtripKind, out DateTime parsed)) return parsed.ToUniversalTime();
            return defaultValue;
        }

        private static object OpenApi()
        {
            return new
            {
                openapi = "3.0.1",
                info = new
                {
                    title = "Wilson API",
                    version = "1.0.0",
                    description = "Local-first chat, model runner, tenant, request history, feedback, and settings API."
                },
                servers = new[] { new { url = "/" } },
                tags = new[]
                {
                    new { name = "System", description = "Health, OpenAPI, and Swagger UI endpoints." },
                    new { name = "Authentication", description = "Token validation and current principal metadata." },
                    new { name = "Model Runners", description = "Configured model runner inventory, health, and Ollama model actions." },
                    new { name = "Administration", description = "Settings, tenants, users, and credentials." },
                    new { name = "Chat", description = "Conversations, messages, and model chat requests." },
                    new { name = "Feedback", description = "User feedback capture and review." },
                    new { name = "Request History", description = "Captured request metadata, timing, and summaries." }
                },
                paths = new Dictionary<string, object>
                {
                    { "/", Path(Operation("get", "getRootHealth", "System", "Root health check", "Returns basic Wilson service health.", "HealthResponse", false)) },
                    { "/health", Path(Operation("get", "getHealth", "System", "Health check", "Returns basic Wilson service health.", "HealthResponse", false)) },
                    { "/v1.0/api/health", Path(Operation("get", "getVersionedHealth", "System", "Versioned health check", "Returns basic Wilson service health.", "HealthResponse", false)) },
                    { "/openapi.json", Path(Operation("get", "getOpenApiDocument", "System", "OpenAPI document", "Returns the Wilson OpenAPI JSON document.", "OpenApiDocument", false)) },
                    { "/swagger", Path(Operation("get", "getSwaggerUi", "System", "Swagger UI", "Serves the Swagger UI for this Wilson server.", "HtmlDocument", false, responseContentType: "text/html")) },
                    { "/v1.0/auth/token", Path(Operation("post", "authenticateWithAccessKey", "Authentication", "Authenticate with an access key", "Validates a Wilson access key and returns the bearer token plus principal metadata.", "AuthTokenResponse", false, requestSchema: "AuthTokenRequest")) },
                    { "/v1.0/api/me", Path(Operation("get", "getCurrentPrincipal", "Authentication", "Current principal", "Returns the authenticated user or administrator represented by the bearer token.", "RequestContext", true)) },
                    { "/v1.0/api/model-runners", Path(Operation("get", "listModelRunners", "Model Runners", "List model runners", "Lists configured model runners with model inventory and health metadata.", "ModelRunnerStatusEnumeration", true, parameters: WithPagination(QueryParameter("includeLiveStatus", "When false, skips live upstream model inventory refreshes.", BooleanSchema())))) },
                    { "/v1.0/api/model-runners/health", Path(Operation("get", "listModelRunnerHealth", "Model Runners", "List model runner health", "Lists the latest health status for all model runners with health checks enabled.", "EndpointHealthStatusArray", true)) },
                    { "/v1.0/api/model-runners/{id}/health", Path(Operation("get", "getModelRunnerHealth", "Model Runners", "Get model runner health", "Returns the latest health status for one model runner.", "EndpointHealthStatus", true, parameters: Parameters(PathParameter("id", "Model runner identifier.")))) },
                    { "/v1.0/api/model-runners/{id}/pull", Path(Operation("post", "pullOllamaModel", "Model Runners", "Pull an Ollama model", "Starts an Ollama model pull for the selected model runner. Requires a global administrator bearer token.", "ModelPullResult", true, parameters: Parameters(PathParameter("id", "Model runner identifier.")), requestSchema: "ModelPullRequest")) },
                    { "/v1.0/api/model-runners/{id}/load", Path(Operation("post", "loadOllamaModel", "Model Runners", "Load an Ollama model into memory", "Loads an Ollama model for the selected model runner.", "ModelPullResult", true, parameters: Parameters(PathParameter("id", "Model runner identifier.")), requestSchema: "ModelPullRequest")) },
                    { "/v1.0/api/settings", Path(
                        Operation("get", "getSettings", "Administration", "Read settings", "Returns the active Wilson settings. Requires a global administrator bearer token.", "Settings", true),
                        Operation("put", "updateSettings", "Administration", "Update settings", "Replaces the active Wilson settings and persists them to the configured settings file. Requires a global administrator bearer token.", "Settings", true, requestSchema: "Settings")) },
                    { "/v1.0/api/tenants", Path(
                        Operation("get", "listTenants", "Administration", "List tenants", "Lists tenant records. Requires a global administrator bearer token.", "TenantEnumeration", true, parameters: WithPagination()),
                        Operation("post", "createTenant", "Administration", "Create tenant", "Creates a tenant record. Requires a global administrator bearer token.", "Tenant", true, requestSchema: "Tenant", successStatus: "201", successDescription: "Tenant created.")) },
                    { "/v1.0/api/tenants/{id}", Path(
                        Operation("get", "getTenant", "Administration", "Get tenant", "Gets a tenant record by identifier. Requires a global administrator bearer token.", "Tenant", true, parameters: Parameters(PathParameter("id", "Tenant identifier."))),
                        Operation("put", "updateTenant", "Administration", "Update tenant", "Updates a tenant record. Requires a global administrator bearer token.", "Tenant", true, parameters: Parameters(PathParameter("id", "Tenant identifier.")), requestSchema: "Tenant"),
                        Operation("delete", "deleteTenant", "Administration", "Delete tenant", "Deletes a tenant record. Requires a global administrator bearer token.", null, true, parameters: Parameters(PathParameter("id", "Tenant identifier.")), successStatus: "204", successDescription: "Tenant deleted.")) },
                    { "/v1.0/api/users", Path(
                        Operation("get", "listUsers", "Administration", "List users", "Lists users for the authenticated tenant administrator or, for global administrators, an optional tenant scope.", "UserEnumeration", true, parameters: WithPagination(TenantScopeParameter())),
                        Operation("post", "createUser", "Administration", "Create user", "Creates a user. Tenant administrators create users in their own tenant; global administrators can set tenantId in the body.", "User", true, requestSchema: "User", successStatus: "201", successDescription: "User created.")) },
                    { "/v1.0/api/users/{id}", Path(
                        Operation("get", "getUser", "Administration", "Get user", "Gets a user by identifier.", "User", true, parameters: Parameters(PathParameter("id", "User identifier."), TenantScopeParameter())),
                        Operation("put", "updateUser", "Administration", "Update user", "Updates a user by identifier.", "User", true, parameters: Parameters(PathParameter("id", "User identifier."), TenantScopeParameter()), requestSchema: "User"),
                        Operation("delete", "deleteUser", "Administration", "Delete user", "Deletes a user by identifier.", null, true, parameters: Parameters(PathParameter("id", "User identifier."), TenantScopeParameter()), successStatus: "204", successDescription: "User deleted.")) },
                    { "/v1.0/api/credentials", Path(
                        Operation("get", "listCredentials", "Administration", "List credentials", "Lists credentials for the authenticated tenant administrator or, for global administrators, an optional tenant scope.", "CredentialEnumeration", true, parameters: WithPagination(TenantScopeParameter())),
                        Operation("post", "createCredential", "Administration", "Create credential", "Creates a credential. Tenant administrators create credentials in their own tenant; global administrators can set tenantId in the body.", "Credential", true, requestSchema: "Credential", successStatus: "201", successDescription: "Credential created.")) },
                    { "/v1.0/api/credentials/{id}", Path(
                        Operation("get", "getCredential", "Administration", "Get credential", "Gets a credential by identifier.", "Credential", true, parameters: Parameters(PathParameter("id", "Credential identifier."), TenantScopeParameter())),
                        Operation("put", "updateCredential", "Administration", "Update credential", "Updates credential metadata while preserving its access key.", "Credential", true, parameters: Parameters(PathParameter("id", "Credential identifier."), TenantScopeParameter()), requestSchema: "Credential"),
                        Operation("delete", "deleteCredential", "Administration", "Delete credential", "Deletes a credential by identifier.", null, true, parameters: Parameters(PathParameter("id", "Credential identifier."), TenantScopeParameter()), successStatus: "204", successDescription: "Credential deleted.")) },
                    { "/v1.0/api/conversations", Path(
                        Operation("get", "listConversations", "Chat", "List conversations", "Lists conversations visible to the authenticated principal.", "ConversationEnumeration", true, parameters: WithPagination()),
                        Operation("post", "createConversation", "Chat", "Create conversation", "Creates a conversation owned by the authenticated principal.", "Conversation", true, requestSchema: "Conversation", successStatus: "201", successDescription: "Conversation created.")) },
                    { "/v1.0/api/conversations/{id}", Path(
                        Operation("put", "updateConversation", "Chat", "Update conversation", "Updates conversation metadata.", "Conversation", true, parameters: Parameters(PathParameter("id", "Conversation identifier.")), requestSchema: "Conversation"),
                        Operation("delete", "deleteConversation", "Chat", "Delete conversation", "Deletes a conversation and its associated messages and feedback.", null, true, parameters: Parameters(PathParameter("id", "Conversation identifier.")), successStatus: "204", successDescription: "Conversation deleted.")) },
                    { "/v1.0/api/conversations/{id}/messages", Path(Operation("get", "listConversationMessages", "Chat", "List conversation messages", "Lists messages for a conversation visible to the authenticated principal.", "ChatMessageEnumeration", true, parameters: WithPagination(PathParameter("id", "Conversation identifier.")))) },
                    { "/v1.0/api/chat", Path(Operation("post", "createChatCompletion", "Chat", "Non-streaming chat", "Sends a prompt to a chat-capable model and stores the user and assistant messages.", "ChatResponse", true, requestSchema: "ChatRequest")) },
                    { "/v1.0/api/chat/stream", Path(Operation("post", "streamChatCompletion", "Chat", "Streaming chat over SSE", "Streams model output as server-sent events and stores the completed assistant message.", "SseEventStream", true, requestSchema: "ChatRequest", responseContentType: "text/event-stream")) },
                    { "/v1.0/api/feedback", Path(
                        Operation("get", "listFeedback", "Feedback", "List feedback", "Lists feedback for review. Requires tenant administrator or global administrator access.", "FeedbackEnumeration", true, parameters: WithPagination(TenantScopeParameter())),
                        Operation("post", "createFeedback", "Feedback", "Create feedback", "Creates feedback for a conversation message owned by the authenticated principal.", "Feedback", true, requestSchema: "Feedback", successStatus: "201", successDescription: "Feedback created.")) },
                    { "/v1.0/api/request-history", Path(Operation("get", "listRequestHistory", "Request History", "List request history", "Lists captured request history. Requires tenant administrator or global administrator access.", "RequestHistoryEntryEnumeration", true, parameters: WithPagination(TenantScopeParameter()))) },
                    { "/v1.0/api/request-history/summary", Path(Operation("get", "summarizeRequestHistory", "Request History", "Request history summary", "Summarizes captured request history over a UTC time range. Requires tenant administrator or global administrator access.", "RequestHistorySummary", true, parameters: Parameters(
                        TenantScopeParameter(),
                        QueryParameter("fromUtc", "Inclusive UTC start timestamp. Defaults to 24 hours ago.", StringSchema("date-time")),
                        QueryParameter("toUtc", "Inclusive UTC end timestamp. Defaults to now.", StringSchema("date-time")),
                        QueryParameter("bucketMinutes", "Bucket size in minutes. Defaults to 60.", IntegerSchema())))) },
                    { "/v1.0/api/request-history/{id}", Path(Operation("delete", "deleteRequestHistoryEntry", "Request History", "Delete request history entry", "Deletes one captured request history entry. Requires tenant administrator or global administrator access.", null, true, parameters: Parameters(PathParameter("id", "Request history entry identifier."), TenantScopeParameter()), successStatus: "204", successDescription: "Request history entry deleted.")) }
                },
                components = new
                {
                    securitySchemes = new Dictionary<string, object>
                    {
                        {
                            "bearerAuth",
                            new
                            {
                                type = "http",
                                scheme = "bearer",
                                bearerFormat = "Wilson access key or admin token",
                                description = "Paste a Wilson user access key or admin bearer token. Swagger UI sends it as the Authorization bearer token."
                            }
                        }
                    },
                    schemas = OpenApiSchemas()
                }
            };
        }

        private static string SwaggerHtml()
        {
            return """
<!doctype html>
<html>
<head>
  <title>Wilson Swagger</title>
  <link rel="stylesheet" href="https://unpkg.com/swagger-ui-dist@5/swagger-ui.css">
</head>
<body>
  <div id="swagger-ui"></div>
  <script src="https://unpkg.com/swagger-ui-dist@5/swagger-ui-bundle.js"></script>
  <script>
    window.onload = () => SwaggerUIBundle({
      url: '/openapi.json',
      dom_id: '#swagger-ui',
      deepLinking: true,
      displayRequestDuration: true,
      persistAuthorization: true,
      tryItOutEnabled: true
    });
  </script>
</body>
</html>
""";
        }

        private static Dictionary<string, object> Path(params KeyValuePair<string, object>[] operations)
        {
            Dictionary<string, object> path = new Dictionary<string, object>();
            foreach (KeyValuePair<string, object> operation in operations) path[operation.Key] = operation.Value;
            return path;
        }

        private static KeyValuePair<string, object> Operation(
            string method,
            string operationId,
            string tag,
            string summary,
            string description,
            string? responseSchema,
            bool auth,
            object[]? parameters = null,
            string? requestSchema = null,
            string successStatus = "200",
            string successDescription = "OK",
            string responseContentType = "application/json")
        {
            Dictionary<string, object> operation = new Dictionary<string, object>
            {
                { "operationId", operationId },
                { "tags", new[] { tag } },
                { "summary", summary },
                { "description", description },
                { "responses", Responses(responseSchema, successStatus, successDescription, responseContentType, auth) }
            };

            if (parameters != null && parameters.Length > 0) operation["parameters"] = parameters;
            if (!String.IsNullOrWhiteSpace(requestSchema)) operation["requestBody"] = RequestBody(requestSchema);
            if (auth) operation["security"] = new object[] { new Dictionary<string, object> { { "bearerAuth", Array.Empty<string>() } } };

            return new KeyValuePair<string, object>(method, operation);
        }

        private static Dictionary<string, object> Responses(string? responseSchema, string successStatus, string successDescription, string responseContentType, bool auth)
        {
            Dictionary<string, object> responses = new Dictionary<string, object>
            {
                {
                    successStatus,
                    String.IsNullOrWhiteSpace(responseSchema)
                        ? new Dictionary<string, object> { { "description", successDescription } }
                        : Response(successDescription, responseSchema, responseContentType)
                },
                { "400", Response("Invalid request.", "ErrorResponse") },
                { "404", Response("Resource not found.", "ErrorResponse") },
                { "500", Response("Server error.", "ErrorResponse") }
            };

            if (auth) responses["401"] = Response("Authentication or authorization failed.", "ErrorResponse");
            return responses;
        }

        private static Dictionary<string, object> Response(string description, string schemaName, string contentType = "application/json")
        {
            return new Dictionary<string, object>
            {
                { "description", description },
                {
                    "content",
                    new Dictionary<string, object>
                    {
                        { contentType, new Dictionary<string, object> { { "schema", SchemaRef(schemaName) } } }
                    }
                }
            };
        }

        private static Dictionary<string, object> RequestBody(string schemaName)
        {
            return new Dictionary<string, object>
            {
                { "required", true },
                {
                    "content",
                    new Dictionary<string, object>
                    {
                        { "application/json", new Dictionary<string, object> { { "schema", SchemaRef(schemaName) } } }
                    }
                }
            };
        }

        private static object[] Parameters(params object[] parameters)
        {
            return parameters;
        }

        private static object[] WithPagination(params object[] additional)
        {
            return Parameters(
                QueryParameter("pageNumber", "One-based page number. Defaults to 1.", IntegerSchema()),
                QueryParameter("pageSize", "Page size. Defaults to 25 and is capped at 500.", IntegerSchema()))
                .Concat(additional)
                .ToArray();
        }

        private static Dictionary<string, object> PathParameter(string name, string description)
        {
            return Parameter(name, "path", description, StringSchema(), true);
        }

        private static Dictionary<string, object> QueryParameter(string name, string description, object schema)
        {
            return Parameter(name, "query", description, schema, false);
        }

        private static Dictionary<string, object> TenantScopeParameter()
        {
            return QueryParameter("tenantId", "Optional tenant scope for global administrators. Tenant administrators are scoped to their own tenant.", StringSchema());
        }

        private static Dictionary<string, object> Parameter(string name, string location, string description, object schema, bool required)
        {
            return new Dictionary<string, object>
            {
                { "name", name },
                { "in", location },
                { "description", description },
                { "required", required },
                { "schema", schema }
            };
        }

        private static Dictionary<string, object> OpenApiSchemas()
        {
            Dictionary<string, object> schemas = new Dictionary<string, object>
            {
                { "ErrorResponse", ObjectSchema(Property("error", StringSchema())) },
                { "HealthResponse", ObjectSchema(Property("status", StringSchema()), Property("service", StringSchema())) },
                { "OpenApiDocument", new Dictionary<string, object> { { "type", "object" }, { "description", "OpenAPI 3.0 document." } } },
                { "HtmlDocument", new Dictionary<string, object> { { "type", "string" }, { "description", "HTML document." } } },
                { "SseEventStream", new Dictionary<string, object> { { "type", "string" }, { "description", "Server-sent event stream containing conversation, truncation, chunk, done, or error events." } } },
                {
                    "AuthTokenRequest",
                    ObjectSchema(
                        Property("accessKey", StringSchema()),
                        Property("token", StringSchema()))
                },
                {
                    "AuthTokenResponse",
                    ObjectSchema(
                        Property("token", StringSchema()),
                        Property("user", SchemaRef("RequestContext")))
                },
                {
                    "ChatResponse",
                    ObjectSchema(
                        Property("conversation", SchemaRef("Conversation")),
                        Property("userMessage", SchemaRef("ChatMessage")),
                        Property("assistantMessage", SchemaRef("ChatMessage")),
                        Property("truncation", SchemaRef("ChatTruncationNotice")))
                }
            };

            AddComponentSchema(schemas, typeof(RequestContext));
            AddComponentSchema(schemas, typeof(ModelRunnerStatus));
            AddComponentSchema(schemas, typeof(EndpointHealthStatus));
            AddComponentSchema(schemas, typeof(ModelPullResult));
            AddComponentSchema(schemas, typeof(Settings));
            AddComponentSchema(schemas, typeof(Tenant));
            AddComponentSchema(schemas, typeof(User));
            AddComponentSchema(schemas, typeof(Credential));
            AddComponentSchema(schemas, typeof(Conversation));
            AddComponentSchema(schemas, typeof(ChatMessage));
            AddComponentSchema(schemas, typeof(ChatTruncationNotice));
            AddComponentSchema(schemas, typeof(ChatRequest));
            AddComponentSchema(schemas, typeof(ModelPullRequest));
            AddComponentSchema(schemas, typeof(Feedback));
            AddComponentSchema(schemas, typeof(RequestHistoryEntry));
            AddComponentSchema(schemas, typeof(RequestHistorySummary));

            schemas["EndpointHealthStatusArray"] = ArraySchema(SchemaRef("EndpointHealthStatus"));
            schemas["ModelRunnerStatusEnumeration"] = EnumerationSchema("ModelRunnerStatus");
            schemas["TenantEnumeration"] = EnumerationSchema("Tenant");
            schemas["UserEnumeration"] = EnumerationSchema("User");
            schemas["CredentialEnumeration"] = EnumerationSchema("Credential");
            schemas["ConversationEnumeration"] = EnumerationSchema("Conversation");
            schemas["ChatMessageEnumeration"] = EnumerationSchema("ChatMessage");
            schemas["FeedbackEnumeration"] = EnumerationSchema("Feedback");
            schemas["RequestHistoryEntryEnumeration"] = EnumerationSchema("RequestHistoryEntry");

            return schemas;
        }

        private static void AddComponentSchema(Dictionary<string, object> schemas, Type type)
        {
            string name = OpenApiSchemaName(type);
            if (schemas.ContainsKey(name)) return;

            schemas[name] = new Dictionary<string, object> { { "type", "object" } };
            Dictionary<string, object> properties = new Dictionary<string, object>();
            foreach (PropertyInfo property in type.GetProperties(BindingFlags.Instance | BindingFlags.Public))
            {
                if (property.GetMethod == null || property.GetIndexParameters().Length > 0) continue;
                if (property.GetCustomAttribute<JsonIgnoreAttribute>() != null) continue;
                properties[JsonNamingPolicy.CamelCase.ConvertName(property.Name)] = SchemaForType(property.PropertyType, schemas);
            }

            schemas[name] = new Dictionary<string, object>
            {
                { "type", "object" },
                { "properties", properties }
            };
        }

        private static object SchemaForType(Type type, Dictionary<string, object> schemas)
        {
            Type? nullable = Nullable.GetUnderlyingType(type);
            if (nullable != null) return SchemaForType(nullable, schemas);
            if (type == typeof(string)) return StringSchema();
            if (type == typeof(bool)) return BooleanSchema();
            if (type == typeof(int)) return IntegerSchema();
            if (type == typeof(long)) return IntegerSchema("int64");
            if (type == typeof(double) || type == typeof(float) || type == typeof(decimal)) return NumberSchema();
            if (type == typeof(DateTime) || type == typeof(DateTimeOffset)) return StringSchema("date-time");
            if (type.IsEnum) return EnumSchema(type);
            if (type.IsArray) return ArraySchema(SchemaForType(type.GetElementType()!, schemas));
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>)) return ArraySchema(SchemaForType(type.GetGenericArguments()[0], schemas));

            AddComponentSchema(schemas, type);
            return SchemaRef(OpenApiSchemaName(type));
        }

        private static Dictionary<string, object> EnumerationSchema(string itemSchemaName)
        {
            return ObjectSchema(
                Property("objects", ArraySchema(SchemaRef(itemSchemaName))),
                Property("pageNumber", IntegerSchema()),
                Property("pageSize", IntegerSchema()),
                Property("totalRecords", IntegerSchema()),
                Property("totalPages", IntegerSchema()));
        }

        private static Dictionary<string, object> ObjectSchema(params KeyValuePair<string, object>[] properties)
        {
            Dictionary<string, object> propertyMap = new Dictionary<string, object>();
            foreach (KeyValuePair<string, object> property in properties) propertyMap[property.Key] = property.Value;
            return new Dictionary<string, object>
            {
                { "type", "object" },
                { "properties", propertyMap }
            };
        }

        private static KeyValuePair<string, object> Property(string name, object schema)
        {
            return new KeyValuePair<string, object>(name, schema);
        }

        private static Dictionary<string, object> SchemaRef(string schemaName)
        {
            return new Dictionary<string, object> { { "$ref", "#/components/schemas/" + schemaName } };
        }

        private static Dictionary<string, object> ArraySchema(object itemSchema)
        {
            return new Dictionary<string, object>
            {
                { "type", "array" },
                { "items", itemSchema }
            };
        }

        private static Dictionary<string, object> StringSchema(string? format = null)
        {
            Dictionary<string, object> schema = new Dictionary<string, object> { { "type", "string" } };
            if (!String.IsNullOrWhiteSpace(format)) schema["format"] = format;
            return schema;
        }

        private static Dictionary<string, object> BooleanSchema()
        {
            return new Dictionary<string, object> { { "type", "boolean" } };
        }

        private static Dictionary<string, object> IntegerSchema(string format = "int32")
        {
            return new Dictionary<string, object>
            {
                { "type", "integer" },
                { "format", format }
            };
        }

        private static Dictionary<string, object> NumberSchema()
        {
            return new Dictionary<string, object>
            {
                { "type", "number" },
                { "format", "double" }
            };
        }

        private static Dictionary<string, object> EnumSchema(Type type)
        {
            return new Dictionary<string, object>
            {
                { "type", "string" },
                { "enum", Enum.GetNames(type) }
            };
        }

        private static string OpenApiSchemaName(Type type)
        {
            return type.IsGenericType ? type.Name.Split('`')[0] : type.Name;
        }
    }

    internal sealed class RequestCapture
    {
        public string ResponseBody { get; set; } = String.Empty;
        public double TimeToFirstTokenMs { get; set; }
        public double StreamingTimeMs { get; set; }
        public double TotalTimeMs { get; set; }
        public int TokensUsed { get; set; }
    }

    /// <summary>
    /// Chat request.
    /// </summary>
    public sealed class ChatRequest
    {
        /// <summary>Conversation identifier.</summary>
        public string? ConversationId { get; set; }
        /// <summary>Runner identifier.</summary>
        public string RunnerId { get; set; } = String.Empty;
        /// <summary>Model name.</summary>
        public string Model { get; set; } = String.Empty;
        /// <summary>User prompt.</summary>
        public string Prompt { get; set; } = String.Empty;
        /// <summary>Completion settings.</summary>
        public CompletionRequestSettings Settings { get; set; } = new CompletionRequestSettings();
    }

    /// <summary>
    /// Model pull request.
    /// </summary>
    public sealed class ModelPullRequest
    {
        /// <summary>Model name to pull.</summary>
        public string Model { get; set; } = String.Empty;
    }
}
