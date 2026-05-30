namespace Wilson.Server
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
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
            _ = Task.Run(() => Server.StartAsync(_TokenSource.Token), _TokenSource.Token);
            Console.CancelKeyPress += (sender, eventArgs) =>
            {
                eventArgs.Cancel = true;
                _TokenSource.Cancel();
            };
            while (!_TokenSource.Token.IsCancellationRequested) await Task.Delay(500, _TokenSource.Token).ConfigureAwait(false);
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
            return settings;
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
                await SendJsonAsync(ctx, Enumerate(await Inference.GetRunnerStatusesAsync(ctx.Token).ConfigureAwait(false), Enumeration(ctx))).ConfigureAwait(false);
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
                Settings = updated;
                Inference = new InferenceService(Settings);
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
                info = new { title = "Wilson API", version = "1.0.0" },
                paths = new Dictionary<string, object>
                {
                    { "/v1.0/auth/token", new { post = new { summary = "Authenticate with an access key" } } },
                    { "/swagger", new { get = new { summary = "Swagger UI" } } },
                    { "/v1.0/api/me", new { get = new { summary = "Current principal" } } },
                    { "/v1.0/api/model-runners", new { get = new { summary = "List model servers" } } },
                    { "/v1.0/api/model-runners/{id}/pull", new { post = new { summary = "Pull an Ollama model" } } },
                    { "/v1.0/api/model-runners/{id}/load", new { post = new { summary = "Load an Ollama model into memory" } } },
                    { "/v1.0/api/chat", new { post = new { summary = "Non-streaming chat" } } },
                    { "/v1.0/api/chat/stream", new { post = new { summary = "Streaming chat over SSE" } } },
                    { "/v1.0/api/conversations", new { get = new { summary = "List conversations" }, post = new { summary = "Create conversation" } } },
                    { "/v1.0/api/conversations/{id}", new { put = new { summary = "Update conversation" }, delete = new { summary = "Delete conversation" } } },
                    { "/v1.0/api/tenants", new { get = new { summary = "List tenants" }, post = new { summary = "Create tenant" } } },
                    { "/v1.0/api/users", new { get = new { summary = "List users" }, post = new { summary = "Create user" } } },
                    { "/v1.0/api/credentials", new { get = new { summary = "List credentials" }, post = new { summary = "Create credential" } } },
                    { "/v1.0/api/feedback", new { get = new { summary = "List feedback" }, post = new { summary = "Create feedback" } } },
                    { "/v1.0/api/request-history", new { get = new { summary = "List request history" } } },
                    { "/v1.0/api/request-history/summary", new { get = new { summary = "Request history summary" } } },
                    { "/v1.0/api/settings", new { get = new { summary = "Read settings" }, put = new { summary = "Update settings" } } }
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
  <script>window.onload = () => SwaggerUIBundle({ url: '/openapi.json', dom_id: '#swagger-ui' });</script>
</body>
</html>
""";
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
