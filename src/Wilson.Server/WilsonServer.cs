namespace Wilson.Server
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Security.Cryptography;
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
    using Wilson.Core.Tools;

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
        private readonly ConcurrentDictionary<string, string> _ApprovedToolRuns = new ConcurrentDictionary<string, string>(StringComparer.OrdinalIgnoreCase);

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
        /// Tool service.
        /// </summary>
        public ToolService ToolService { get; private set; }

        /// <summary>
        /// MCP tool manager.
        /// </summary>
        public McpToolManager McpTools { get; private set; }

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
            McpTools = new McpToolManager();
            ToolService = new ToolService(settings, McpTools);
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
            await server.Database.EnsureDefaultPromptTemplatesAsync().ConfigureAwait(false);
            await server.ReloadMcpAsync(CancellationToken.None).ConfigureAwait(false);
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
            settings.Tools ??= new ToolsSettings();
            NormalizeTools(settings.Tools, true);
            settings.ModelRunners ??= new List<ModelRunnerSettings>();
            foreach (ModelRunnerSettings runner in settings.ModelRunners)
            {
                runner.Models ??= new List<string>();
                ModelRunnerSettings.ApplyHealthCheckDefaults(runner);
            }
        }

        private static void NormalizeTools(ToolsSettings tools, bool applyWorkspaceDefaults = false)
        {
            tools.DefaultApprovalPolicy = NormalizeValue(tools.DefaultApprovalPolicy, "auto", "deny", "ask", "auto");
            tools.ToolChoiceMode = NormalizeValue(tools.ToolChoiceMode, "auto", "auto", "required", "none", "allowed_only");
            tools.WorkingDirectory = tools.WorkingDirectory?.Trim() ?? String.Empty;
            tools.AllowedRoots = NormalizeList(tools.AllowedRoots);
            if (applyWorkspaceDefaults) ApplyToolWorkspaceDefaults(tools);
            tools.EnabledToolNames = NormalizeToolNameList(tools.EnabledToolNames);
            tools.DisabledToolNames = NormalizeToolNameList(tools.DisabledToolNames);
            tools.MaxAgentIterations = Math.Clamp(tools.MaxAgentIterations, 1, 100);
            tools.MaxToolIterations = Math.Clamp(tools.MaxToolIterations, 1, 20);
            tools.MaxToolCallsPerTurn = Math.Clamp(tools.MaxToolCallsPerTurn, 1, 50);
            tools.MaxParallelToolCalls = tools.AllowParallelToolCalls ? Math.Clamp(tools.MaxParallelToolCalls, 1, 16) : 1;
            tools.ToolTimeoutMs = Math.Clamp(tools.ToolTimeoutMs, 1000, 300000);
            tools.ApprovalTimeoutMs = Math.Clamp(tools.ApprovalTimeoutMs, 10000, 3600000);
            tools.ProcessTimeoutMs = Math.Clamp(tools.ProcessTimeoutMs, 1000, 600000);
            tools.MaxReadFileBytes = Math.Clamp(tools.MaxReadFileBytes, 1, 104857600);
            tools.MaxToolResultBytes = Math.Clamp(tools.MaxToolResultBytes, 1024, 10485760);
            tools.MaxToolOutputChars = Math.Clamp(tools.MaxToolOutputChars, 1024, 200000);
            tools.MaxToolOutputCharsPerTurn = Math.Clamp(tools.MaxToolOutputCharsPerTurn, tools.MaxToolOutputChars, 500000);
            tools.MaxToolResultItems = Math.Clamp(tools.MaxToolResultItems, 1, 1000);

            tools.WebSearch ??= new WebSearchToolSettings();
            bool legacyUnconfiguredWebSearch = !tools.WebSearch.Enabled
                && (tools.WebSearch.Providers == null || tools.WebSearch.Providers.Count == 0);
            if (legacyUnconfiguredWebSearch)
            {
                tools.WebSearch.Enabled = true;
            }

            tools.WebSearch.Providers ??= new List<WebSearchProviderSettings>();
            if (tools.WebSearch.Enabled && tools.WebSearch.Providers.Count == 0)
            {
                tools.WebSearch.Providers = WebSearchToolSettings.DefaultProviders();
            }

            tools.WebSearch.Providers = tools.WebSearch.Providers
                .Where(provider => provider != null)
                .Select(provider =>
                {
                    provider.Name = provider.Name?.Trim() ?? String.Empty;
                    provider.ProviderType = provider.ProviderType?.Trim() ?? String.Empty;
                    provider.Endpoint = provider.Endpoint?.Trim() ?? String.Empty;
                    provider.ApiKey = provider.ApiKey?.Trim() ?? String.Empty;
                    provider.TimeoutMs = Math.Clamp(provider.TimeoutMs, 1000, 300000);
                    return provider;
                })
                .ToList();

            tools.Mcp ??= new McpToolSettings();
            tools.Mcp.Servers ??= new List<McpServerSettings>();
            tools.Mcp.Servers = tools.Mcp.Servers
                .Where(server => server != null)
                .Select(server =>
                {
                    server.Name = server.Name?.Trim() ?? String.Empty;
                    server.Transport = NormalizeValue(server.Transport, "stdio", "stdio", "http");
                    server.Command = server.Command?.Trim() ?? String.Empty;
                    server.Args = NormalizeList(server.Args);
                    server.Env ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    server.Url = server.Url?.Trim() ?? String.Empty;
                    server.McpPath = server.McpPath?.Trim() ?? String.Empty;
                    return server;
                })
                .ToList();
        }

        private static void ApplyToolWorkspaceDefaults(ToolsSettings tools)
        {
            if (String.IsNullOrWhiteSpace(tools.WorkingDirectory))
            {
                tools.WorkingDirectory = tools.AllowedRoots.Count > 0
                    ? tools.AllowedRoots[0]
                    : Directory.GetCurrentDirectory();
            }

            if (tools.AllowedRoots.Count == 0)
            {
                tools.AllowedRoots.Add(tools.WorkingDirectory);
            }
        }

        private static string NormalizeValue(string? value, string fallback, params string[] allowed)
        {
            if (String.IsNullOrWhiteSpace(value)) return fallback;
            string trimmed = value.Trim();
            foreach (string candidate in allowed)
            {
                if (String.Equals(trimmed, candidate, StringComparison.OrdinalIgnoreCase))
                    return candidate;
            }
            return fallback;
        }

        private static List<string> NormalizeList(List<string>? values)
        {
            return (values ?? new List<string>())
                .Where(value => !String.IsNullOrWhiteSpace(value))
                .Select(value => value.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static List<string> NormalizeToolNameList(List<string>? values)
        {
            return NormalizeList(values)
                .Select(value => value.ToLowerInvariant())
                .ToList();
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
            catch (ModelServerHttpException ex)
            {
                ctx.Response.StatusCode = ex.StatusCode >= 400 && ex.StatusCode <= 599 ? ex.StatusCode : 502;
                await SendJsonAsync(ctx, new
                {
                    error = ex.Message,
                    upstreamStatusCode = ex.StatusCode,
                    upstreamResponseBody = ex.ResponseBody
                }).ConfigureAwait(false);
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
                    RequestCapture? capture = _RequestCapture.Value;
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
                        ResponseBody = capture?.ResponseBody ?? String.Empty,
                        TimeToFirstTokenMs = capture?.TimeToFirstTokenMs ?? 0,
                        StreamingTimeMs = capture?.StreamingTimeMs ?? 0,
                        TotalTimeMs = capture?.TotalTimeMs ?? 0,
                        TokensUsed = capture?.TokensUsed ?? 0,
                        ToolRunId = capture?.ToolRunId ?? String.Empty,
                        ToolCallCount = capture?.ToolCallCount ?? 0,
                        ToolElapsedMs = capture?.ToolElapsedMs ?? 0,
                        AgentIterations = capture?.AgentIterations ?? 0,
                        SystemPromptId = capture?.SystemPromptId ?? String.Empty,
                        SystemPromptName = capture?.SystemPromptName ?? String.Empty,
                        SystemPromptDefault = capture?.SystemPromptDefault ?? false,
                        SystemPromptHash = capture?.SystemPromptHash ?? String.Empty,
                        ToolPromptId = capture?.ToolPromptId ?? String.Empty,
                        ToolPromptName = capture?.ToolPromptName ?? String.Empty,
                        ToolPromptDefault = capture?.ToolPromptDefault ?? false,
                        ToolPromptHash = capture?.ToolPromptHash ?? String.Empty
                    };
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            await Database.CreateRequestHistoryAsync(entry).ConfigureAwait(false);
                            if (!String.IsNullOrWhiteSpace(entry.TenantId) && !String.IsNullOrWhiteSpace(entry.ToolRunId))
                            {
                                await Database.AttachToolCallsToRequestHistoryByRunIdAsync(entry.TenantId, entry.ToolRunId, entry.Id).ConfigureAwait(false);
                            }
                        }
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
                await ReloadMcpAsync(ctx.Token).ConfigureAwait(false);
                HealthChecks.UpdateSettings(Settings);
                File.WriteAllText(_SettingsFile, JsonSerializer.Serialize(Settings, _Json));
                await SendJsonAsync(ctx, Settings).ConfigureAwait(false);
                return;
            }

            if (path == "/v1.0/api/mcp" && method == "GET")
            {
                RequireAdmin(requestContext);
                await SendJsonAsync(ctx, McpTools.GetStatus(Settings)).ConfigureAwait(false);
                return;
            }

            if (path == "/v1.0/api/mcp/reload" && method == "POST")
            {
                RequireAdmin(requestContext);
                await ReloadMcpAsync(ctx.Token).ConfigureAwait(false);
                await SendJsonAsync(ctx, McpTools.GetStatus(Settings)).ConfigureAwait(false);
                return;
            }

            if (path == "/v1.0/api/tools" && method == "GET")
            {
                await SendJsonAsync(ctx, ToolService.ListTools(true)).ConfigureAwait(false);
                return;
            }

            if (path == "/v1.0/api/tools/instructions" && method == "GET")
            {
                await SendJsonAsync(ctx, new ToolInstructionsResponse { SystemPrompt = ToolAgentService.BuildToolSystemInstruction(ToolService) }).ConfigureAwait(false);
                return;
            }

            if (path == "/v1.0/api/tools/validate" && method == "POST")
            {
                RequireAdmin(requestContext);
                ToolPolicyValidationRequest body = Body<ToolPolicyValidationRequest>(ctx);
                await SendJsonAsync(ctx, ValidateToolPolicy(body.Tools)).ConfigureAwait(false);
                return;
            }

            if (path == "/v1.0/api/tools/test" && method == "POST")
            {
                RequireAdmin(requestContext);
                ToolPolicyTestRequest body = Body<ToolPolicyTestRequest>(ctx);
                await SendJsonAsync(ctx, TestToolPolicy(body)).ConfigureAwait(false);
                return;
            }

            if (path.StartsWith("/v1.0/api/tools/", StringComparison.OrdinalIgnoreCase) && method == "GET")
            {
                string name = Segment(path, 3);
                ToolDescriptor? descriptor = ToolService.GetTool(name);
                if (descriptor == null) throw new KeyNotFoundException("Unknown tool '" + name + "'.");
                await SendJsonAsync(ctx, descriptor).ConfigureAwait(false);
                return;
            }

            if (path.StartsWith("/v1.0/api/tool-runs/", StringComparison.OrdinalIgnoreCase) && method == "GET")
            {
                string id = Segment(path, 3);
                string tenantId = requestContext!.IsAdmin ? Query(ctx, "tenantId") ?? requestContext.TenantId ?? String.Empty : requestContext.TenantId!;
                if (String.IsNullOrWhiteSpace(tenantId)) throw new ArgumentException("tenantId is required for global administrators when reading a tool run.");
                ToolRun? run = await Database.GetToolRunAsync(tenantId, id, ctx.Token).ConfigureAwait(false);
                if (run == null) throw new KeyNotFoundException("Tool run not found.");
                Conversation? conversation = await Database.GetConversationAsync(tenantId, run.ConversationId, ctx.Token).ConfigureAwait(false);
                if (conversation == null) throw new KeyNotFoundException("Conversation not found.");
                EnsureConversationAccess(requestContext, conversation);
                List<ToolExecutionRecord> calls = await Database.GetToolCallsForConversationAsync(tenantId, run.ConversationId, ctx.Token).ConfigureAwait(false);
                await SendJsonAsync(ctx, new ToolRunResponse { ToolRun = run, ToolCalls = calls.Where(call => String.Equals(call.RunId, run.RunId, StringComparison.OrdinalIgnoreCase)).ToList() }).ConfigureAwait(false);
                return;
            }

            if (path.StartsWith("/v1.0/api/tool-runs/", StringComparison.OrdinalIgnoreCase)
                && path.Contains("/tool-calls/", StringComparison.OrdinalIgnoreCase)
                && path.EndsWith("/approval", StringComparison.OrdinalIgnoreCase)
                && method == "POST")
            {
                await HandleToolApprovalAsync(ctx, requestContext!).ConfigureAwait(false);
                return;
            }

            if (path == "/v1.0/api/tenants")
            {
                RequireAdmin(requestContext);
                if (method == "GET") { await SendJsonAsync(ctx, Enumerate(await Database.GetTenantsAsync(ctx.Token).ConfigureAwait(false), Enumeration(ctx))).ConfigureAwait(false); return; }
                if (method == "POST") { Tenant item = Body<Tenant>(ctx); if (String.IsNullOrWhiteSpace(item.Id)) item.Id = IdGenerator.Tenant(); await Database.CreateTenantAsync(item, ctx.Token).ConfigureAwait(false); await Database.EnsureDefaultPromptTemplatesAsync(item.Id, ctx.Token).ConfigureAwait(false); ctx.Response.StatusCode = 201; await SendJsonAsync(ctx, item).ConfigureAwait(false); return; }
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

            if (path == "/v1.0/api/prompts")
            {
                RequireAuth(requestContext);
                string? tenantId = requestContext!.IsAdmin ? Query(ctx, "tenantId") : requestContext.TenantId;
                PromptTemplateKind? kind = ParsePromptKind(Query(ctx, "kind"), false);
                bool includeInactive = String.Equals(Query(ctx, "includeInactive"), "true", StringComparison.OrdinalIgnoreCase) && (requestContext.IsAdmin || requestContext.IsTenantAdmin);
                if (method == "GET")
                {
                    await SendJsonAsync(ctx, Enumerate(await Database.GetPromptTemplatesAsync(tenantId, kind, includeInactive, ctx.Token).ConfigureAwait(false), Enumeration(ctx))).ConfigureAwait(false);
                    return;
                }

                if (method == "POST")
                {
                    RequireTenantAdmin(requestContext);
                    PromptTemplate item = Body<PromptTemplate>(ctx);
                    if (String.IsNullOrWhiteSpace(item.Id)) item.Id = IdGenerator.PromptTemplate();
                    item.TenantId = requestContext.IsAdmin ? item.TenantId : requestContext.TenantId!;
                    if (String.IsNullOrWhiteSpace(item.TenantId)) throw new ArgumentException("tenantId is required.");
                    item.CreatedByUserId = requestContext.UserId ?? String.Empty;
                    item.UpdatedByUserId = requestContext.UserId ?? String.Empty;
                    await ValidatePromptNameAvailableAsync(item, ctx.Token).ConfigureAwait(false);
                    await Database.CreatePromptTemplateAsync(item, ctx.Token).ConfigureAwait(false);
                    ctx.Response.StatusCode = 201;
                    await SendJsonAsync(ctx, item).ConfigureAwait(false);
                    return;
                }
            }

            if (path.StartsWith("/v1.0/api/prompts/", StringComparison.OrdinalIgnoreCase) && path.EndsWith("/default", StringComparison.OrdinalIgnoreCase) && method == "POST")
            {
                RequireTenantAdmin(requestContext);
                string id = Segment(path, 3);
                PromptTemplate existing = await GetPromptTemplateForRequestAsync(requestContext!, id, ctx.Token).ConfigureAwait(false);
                await Database.SetDefaultPromptTemplateAsync(existing.TenantId, existing.Id, existing.Kind, requestContext!.UserId ?? String.Empty, ctx.Token).ConfigureAwait(false);
                PromptTemplate updated = await Database.GetPromptTemplateAsync(existing.TenantId, existing.Id, ctx.Token).ConfigureAwait(false) ?? existing;
                await SendJsonAsync(ctx, updated).ConfigureAwait(false);
                return;
            }

            if (path.StartsWith("/v1.0/api/prompts/", StringComparison.OrdinalIgnoreCase))
            {
                RequireAuth(requestContext);
                string id = Segment(path, 3);
                PromptTemplate existing = await GetPromptTemplateForRequestAsync(requestContext!, id, ctx.Token).ConfigureAwait(false);
                if (method == "GET") { await SendJsonAsync(ctx, existing).ConfigureAwait(false); return; }
                if (method == "PUT")
                {
                    RequireTenantAdmin(requestContext);
                    PromptTemplate item = Body<PromptTemplate>(ctx);
                    item.Id = existing.Id;
                    item.TenantId = existing.TenantId;
                    item.CreatedByUserId = existing.CreatedByUserId;
                    item.CreatedUtc = existing.CreatedUtc;
                    item.UpdatedByUserId = requestContext!.UserId ?? String.Empty;
                    if (existing.IsProtected) item.IsProtected = true;
                    await ValidatePromptNameAvailableAsync(item, ctx.Token).ConfigureAwait(false);
                    await Database.UpdatePromptTemplateAsync(item, ctx.Token).ConfigureAwait(false);
                    await SendJsonAsync(ctx, item).ConfigureAwait(false);
                    return;
                }

                if (method == "DELETE")
                {
                    RequireTenantAdmin(requestContext);
                    if (existing.IsProtected || existing.IsDefault) throw new ArgumentException("Default or protected prompt templates cannot be deleted.");
                    await Database.DeletePromptTemplateAsync(existing.TenantId, existing.Id, ctx.Token).ConfigureAwait(false);
                    ctx.Response.StatusCode = 204;
                    await ctx.Response.Send(ctx.Token).ConfigureAwait(false);
                    return;
                }
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

            if (path.StartsWith("/v1.0/api/conversations/", StringComparison.OrdinalIgnoreCase) && path.EndsWith("/tool-calls", StringComparison.OrdinalIgnoreCase) && method == "GET")
            {
                string id = Segment(path, 3);
                Conversation? existing = await Database.GetConversationAsync(requestContext!.TenantId!, id, ctx.Token).ConfigureAwait(false);
                if (existing == null) throw new KeyNotFoundException("Conversation not found.");
                EnsureConversationAccess(requestContext, existing);
                await SendJsonAsync(ctx, Enumerate(await Database.GetToolCallsForConversationAsync(requestContext.TenantId!, id, ctx.Token).ConfigureAwait(false), Enumeration(ctx))).ConfigureAwait(false);
                return;
            }

            if (path.StartsWith("/v1.0/api/conversations/", StringComparison.OrdinalIgnoreCase) && path.EndsWith("/messages", StringComparison.OrdinalIgnoreCase) && method == "GET")
            {
                string id = Segment(path, 3);
                await SendJsonAsync(ctx, Enumerate(await Database.GetMessagesAsync(requestContext!.TenantId!, id, ctx.Token).ConfigureAwait(false), Enumeration(ctx))).ConfigureAwait(false);
                return;
            }

            if (path.StartsWith("/v1.0/api/conversations/", StringComparison.OrdinalIgnoreCase) && !path.EndsWith("/messages", StringComparison.OrdinalIgnoreCase) && !path.EndsWith("/tool-calls", StringComparison.OrdinalIgnoreCase))
            {
                string id = Segment(path, 3);
                Conversation? existing = await Database.GetConversationAsync(requestContext!.TenantId!, id, ctx.Token).ConfigureAwait(false);
                if (existing == null) throw new KeyNotFoundException("Conversation not found.");
                EnsureConversationAccess(requestContext, existing);

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

            if (path.StartsWith("/v1.0/api/request-history/", StringComparison.OrdinalIgnoreCase) && path.EndsWith("/tool-calls", StringComparison.OrdinalIgnoreCase) && method == "GET")
            {
                RequireTenantAdmin(requestContext);
                string id = Segment(path, 3);
                string? tenantId = requestContext!.IsAdmin ? Query(ctx, "tenantId") : requestContext.TenantId;
                await SendJsonAsync(ctx, Enumerate(await Database.GetToolCallsForRequestHistoryAsync(tenantId, id, ctx.Token).ConfigureAwait(false), Enumeration(ctx))).ConfigureAwait(false);
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

            bool requestedTools = body.ToolsEnabled ?? Settings.Tools.Enabled;
            if (requestedTools && !await Inference.IsToolCapableModelAsync(runner, body.Model, ctx.Token).ConfigureAwait(false))
            {
                body.ToolsEnabled = false;
            }

            ChatToolPlan toolPlan = ResolveChatToolPlan(body, requestContext, runner, streaming);
            PromptSelection promptSelection = await ResolvePromptSelectionAsync(body, requestContext, toolPlan, ctx.Token).ConfigureAwait(false);
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
                if (toolPlan.Enabled)
                {
                    await ChatWithToolsAsync(ctx, requestContext, body, runner, conversation, messages, userMessage, truncation, toolPlan, promptSelection).ConfigureAwait(false);
                    return;
                }

                Stopwatch inference = Stopwatch.StartNew();
                string answer = await Inference.ChatAsync(runner, body.Model, prompt, body.Settings, ctx.Token).ConfigureAwait(false);
                inference.Stop();
                int outputTokens = InferenceService.EstimateTokens(answer);
                int inputTokens = InferenceService.EstimateTokens(prompt);
                ChatMessage assistantMessage = new ChatMessage { TenantId = requestContext.TenantId!, ConversationId = conversation.Id, Role = "assistant", Content = answer, RunnerId = body.RunnerId, Model = body.Model, TokenEstimate = outputTokens, TimeToFirstTokenMs = inference.Elapsed.TotalMilliseconds, StreamingTimeMs = 0, TotalTimeMs = inference.Elapsed.TotalMilliseconds, TokensUsed = inputTokens + outputTokens };
                await Database.CreateMessageAsync(assistantMessage, ctx.Token).ConfigureAwait(false);
                conversation = await MaybeGenerateConversationTitleAsync(conversation, runner, body.Model, messages, userMessage, assistantMessage, ctx.Token).ConfigureAwait(false);
                SetRequestCapture(assistantMessage, answer, promptSelection: promptSelection);
                await SendJsonAsync(ctx, new ChatResponse { Conversation = conversation, UserMessage = userMessage, AssistantMessage = assistantMessage, Truncation = truncation }).ConfigureAwait(false);
                return;
            }

            ctx.Response.ContentType = "text/event-stream";
            ctx.Response.Headers.Add("Cache-Control", "no-cache");
            ctx.Response.Headers.Add("Connection", "keep-alive");
            ctx.Response.ChunkedTransfer = true;
            if (toolPlan.Enabled)
            {
                await ChatWithToolsStreamingAsync(ctx, requestContext, body, runner, conversation, messages, userMessage, truncation, toolPlan, promptSelection).ConfigureAwait(false);
                return;
            }

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
                SetRequestCapture(stored, full.ToString(), promptSelection: promptSelection);
                await SendSseAsync(ctx, "conversation", conversation, false).ConfigureAwait(false);
                await SendSseAsync(ctx, "done", stored, true).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                if (streamingTimer.IsRunning) streamingTimer.Stop();
                total.Stop();
                SetRequestCapture(new ChatMessage { TimeToFirstTokenMs = firstTokenMs, StreamingTimeMs = streamingTimer.Elapsed.TotalMilliseconds, TotalTimeMs = total.Elapsed.TotalMilliseconds, TokensUsed = InferenceService.EstimateTokens(prompt) }, ex.Message, promptSelection: promptSelection);
                await SendSseAsync(ctx, "error", new { error = "The selected model could not generate a chat response. Confirm that it is a chat or completion model, not an embedding-only model.", detail = ex.Message }, true).ConfigureAwait(false);
            }
        }

        private async Task ChatWithToolsAsync(
            HttpContextBase ctx,
            RequestContext requestContext,
            ChatRequest body,
            ModelRunnerSettings runner,
            Conversation conversation,
            List<ChatMessage> previousMessages,
            ChatMessage userMessage,
            ChatTruncationNotice truncation,
            ChatToolPlan toolPlan,
            PromptSelection promptSelection)
        {
            ChatResponse response = await CompleteToolChatAsync(ctx, requestContext, body, runner, conversation, previousMessages, userMessage, truncation, toolPlan, promptSelection, null).ConfigureAwait(false);
            await SendJsonAsync(ctx, response).ConfigureAwait(false);
        }

        private async Task ChatWithToolsStreamingAsync(
            HttpContextBase ctx,
            RequestContext requestContext,
            ChatRequest body,
            ModelRunnerSettings runner,
            Conversation conversation,
            List<ChatMessage> previousMessages,
            ChatMessage userMessage,
            ChatTruncationNotice truncation,
            ChatToolPlan toolPlan,
            PromptSelection promptSelection)
        {
            await SendSseAsync(ctx, "conversation", conversation, false).ConfigureAwait(false);
            await SendSseAsync(ctx, "truncation", truncation, false).ConfigureAwait(false);
            await SendSseAsync(ctx, "run_started", new { conversationId = conversation.Id }, false).ConfigureAwait(false);

            try
            {
                ChatResponse response = await CompleteToolChatAsync(
                    ctx,
                    requestContext,
                    body,
                    runner,
                    conversation,
                    previousMessages,
                    userMessage,
                    truncation,
                    toolPlan,
                    promptSelection,
                    async (progress, token) =>
                    {
                        if (toolPlan.Settings.Tools.EmitProgressEvents)
                        {
                            await SendSseAsync(ctx, SseEventName(progress.EventType), progress, false).ConfigureAwait(false);
                        }
                    }).ConfigureAwait(false);

                await SendSseAsync(ctx, "conversation", response.Conversation!, false).ConfigureAwait(false);
                await SendSseAsync(ctx, "run_completed", new { toolRun = response.ToolRun, toolMetrics = response.ToolMetrics }, false).ConfigureAwait(false);
                await SendSseAsync(ctx, "done", new
                {
                    response.AssistantMessage!.Id,
                    response.AssistantMessage.TenantId,
                    response.AssistantMessage.ConversationId,
                    response.AssistantMessage.Role,
                    response.AssistantMessage.Content,
                    response.AssistantMessage.RunnerId,
                    response.AssistantMessage.Model,
                    response.AssistantMessage.TokenEstimate,
                    response.AssistantMessage.TimeToFirstTokenMs,
                    response.AssistantMessage.StreamingTimeMs,
                    response.AssistantMessage.TotalTimeMs,
                    response.AssistantMessage.TokensUsed,
                    response.AssistantMessage.RunId,
                    response.AssistantMessage.ToolCallsJson,
                    response.AssistantMessage.ToolCallId,
                    response.AssistantMessage.MetadataJson,
                    response.AssistantMessage.CreatedUtc,
                    toolRun = response.ToolRun,
                    toolCalls = response.ToolCalls,
                    toolMetrics = response.ToolMetrics
                }, true).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                await SendSseAsync(ctx, "error", new { error = "Tool-enabled chat failed before producing a final assistant response.", detail = ex.Message }, true).ConfigureAwait(false);
            }
        }

        private async Task<ChatResponse> CompleteToolChatAsync(
            HttpContextBase ctx,
            RequestContext requestContext,
            ChatRequest body,
            ModelRunnerSettings runner,
            Conversation conversation,
            List<ChatMessage> previousMessages,
            ChatMessage userMessage,
            ChatTruncationNotice truncation,
            ChatToolPlan toolPlan,
            PromptSelection promptSelection,
            ToolAgentService.ToolProgressHandler? progressHandler)
        {
            Stopwatch total = Stopwatch.StartNew();
            ToolExecutionContext executionContext = new ToolExecutionContext
            {
                TenantId = requestContext.TenantId ?? String.Empty,
                UserId = requestContext.UserId ?? String.Empty,
                ConversationId = conversation.Id,
                RunId = IdGenerator.ToolRun(),
                WorkingDirectory = toolPlan.Settings.Tools.WorkingDirectory,
                AllowedRoots = new List<string>(toolPlan.Settings.Tools.AllowedRoots),
                Settings = toolPlan.Settings
            };

            ToolRun toolRun = new ToolRun
            {
                RunId = executionContext.RunId,
                TenantId = requestContext.TenantId ?? String.Empty,
                UserId = requestContext.UserId ?? String.Empty,
                ConversationId = conversation.Id,
                RunnerId = body.RunnerId,
                Model = body.Model,
                Status = ToolStatuses.Running,
                StartedUtc = DateTime.UtcNow,
                CreatedUtc = DateTime.UtcNow
            };
            await Database.CreateToolRunAsync(toolRun, ctx.Token).ConfigureAwait(false);

            ToolAgentService agent = new ToolAgentService(
                toolPlan.ToolService,
                Inference.ChatWithToolsAsync,
                progressHandler == null
                    ? null
                    : async (call, approvalContext, iteration, sequenceNumber, startedUtc, token) =>
                    {
                        return await WaitForToolApprovalAsync(call, approvalContext, toolRun, iteration, sequenceNumber, startedUtc, token).ConfigureAwait(false);
                    });
            List<ModelChatMessage> modelMessages = previousMessages
                .Select(message => new ModelChatMessage { Role = message.Role, Content = message.Content })
                .ToList();
            modelMessages.Add(new ModelChatMessage { Role = "user", Content = body.Prompt });

            ToolAgentResponse agentResponse = await agent.RunAsync(runner, body.Model, modelMessages, body.Settings, executionContext, ctx.Token, progressHandler).ConfigureAwait(false);
            total.Stop();

            if (!agentResponse.Success)
            {
                throw new InvalidOperationException(agentResponse.ErrorMessage ?? "Tool-enabled chat failed before producing a final assistant response.");
            }

            string answer = agentResponse.Content ?? String.Empty;
            int outputTokens = InferenceService.EstimateTokens(answer);
            int inputTokens = modelMessages.Sum(message => InferenceService.EstimateTokens(message.Content ?? String.Empty));
            toolRun.Status = ToolStatuses.Completed;
            toolRun.CompletedUtc = DateTime.UtcNow;
            toolRun.ElapsedMs = total.Elapsed.TotalMilliseconds;
            toolRun.IterationCount = agentResponse.IterationCount;
            toolRun.ToolCallCount = agentResponse.ToolCallCount;
            toolRun.ErrorCount = agentResponse.ErrorCount;
            ChatToolMetrics metrics = new ChatToolMetrics
            {
                ToolsEnabled = true,
                ToolCallCount = agentResponse.ToolCallCount,
                ErrorCount = agentResponse.ErrorCount,
                IterationCount = agentResponse.IterationCount,
                TotalToolElapsedMs = agentResponse.ToolCalls.Sum(trace => trace.ElapsedMs)
            };
            ChatMessage assistantMessage = new ChatMessage
            {
                TenantId = requestContext.TenantId!,
                ConversationId = conversation.Id,
                Role = "assistant",
                Content = answer,
                RunnerId = body.RunnerId,
                Model = body.Model,
                TokenEstimate = outputTokens,
                TimeToFirstTokenMs = total.Elapsed.TotalMilliseconds,
                StreamingTimeMs = 0,
                TotalTimeMs = total.Elapsed.TotalMilliseconds,
                TokensUsed = inputTokens + outputTokens,
                RunId = toolRun.RunId
            };
            List<ToolExecutionRecord> toolRecords = ToolAuditWriter.BuildExecutionRecords(toolRun, agentResponse.AuditToolCalls, agentResponse.ToolCalls, body.ApprovalPolicy ?? toolPlan.Settings.Tools.DefaultApprovalPolicy, assistantMessage.Id, toolPlan.Settings.Tools);
            List<ToolTrace> safeToolCalls = BuildSafeToolTraces(agentResponse.ToolCalls, toolRecords);
            assistantMessage.ToolCallsJson = JsonSerializer.Serialize(safeToolCalls, _Json);
            assistantMessage.MetadataJson = JsonSerializer.Serialize(metrics, _Json);

            await Database.CreateMessageAsync(assistantMessage, ctx.Token).ConfigureAwait(false);
            await Database.UpdateToolRunAsync(toolRun, ctx.Token).ConfigureAwait(false);
            await UpsertToolRecordsAsync(toolRun, toolRecords, ctx.Token).ConfigureAwait(false);

            conversation = await MaybeGenerateConversationTitleAsync(conversation, runner, body.Model, previousMessages, userMessage, assistantMessage, ctx.Token).ConfigureAwait(false);
            SetRequestCapture(assistantMessage, answer, toolRun, metrics, promptSelection);

            return new ChatResponse
            {
                Conversation = conversation,
                UserMessage = userMessage,
                AssistantMessage = assistantMessage,
                Truncation = truncation,
                ToolRun = toolRun,
                ToolCalls = safeToolCalls,
                ToolMetrics = metrics
            };
        }

        private async Task<ToolApprovalDecision> WaitForToolApprovalAsync(ModelToolCall call, ToolExecutionContext executionContext, ToolRun run, int iteration, int sequenceNumber, DateTime startedUtc, CancellationToken token)
        {
            if (_ApprovedToolRuns.TryGetValue(ToolRunApprovalKey(executionContext.TenantId, executionContext.RunId), out string? approvedByUserId))
            {
                return new ToolApprovalDecision { Approved = true, UserId = approvedByUserId ?? String.Empty };
            }

            string toolCallId = String.IsNullOrWhiteSpace(call.Id) ? IdGenerator.ToolCall() : call.Id!;
            string toolName = call.Function?.Name ?? String.Empty;
            int maxPayloadCharacters = Math.Clamp(executionContext.Settings.Tools.MaxToolResultBytes, 1024, 200000);
            string argumentsJson = executionContext.Settings.Tools.StoreToolArguments
                ? ToolAuditSanitizer.RedactAndCapJson(call.Function?.Arguments ?? "{}", maxPayloadCharacters)
                : "{}";
            DateTime expiresUtc = DateTime.UtcNow.AddMilliseconds(executionContext.Settings.Tools.ApprovalTimeoutMs);
            ToolExecutionRecord record = new ToolExecutionRecord
            {
                TenantId = executionContext.TenantId,
                UserId = executionContext.UserId,
                ConversationId = executionContext.ConversationId,
                RunId = executionContext.RunId,
                TraceId = executionContext.RunId + ":" + sequenceNumber.ToString(System.Globalization.CultureInfo.InvariantCulture),
                Origin = "chat",
                ProviderToolCallId = call.Id,
                ToolCallId = toolCallId,
                ToolName = toolName,
                Iteration = iteration,
                SequenceNumber = sequenceNumber,
                Status = ToolStatuses.PendingApproval,
                ApprovalPolicy = executionContext.Settings.Tools.DefaultApprovalPolicy,
                ArgumentsJson = argumentsJson,
                ResultJson = "{}",
                ResultSummaryJson = JsonSerializer.Serialize(new { status = ToolStatuses.PendingApproval, summary = "Waiting for approval." }, _Json),
                ResultPreview = "Waiting for approval.",
                Success = false,
                Denied = false,
                InputBytes = Encoding.UTF8.GetByteCount(argumentsJson),
                Model = run.Model,
                StartedUtc = startedUtc,
                CreatedUtc = startedUtc,
                UpdatedUtc = DateTime.UtcNow
            };
            await Database.CreateToolCallAsync(record, token).ConfigureAwait(false);

            while (DateTime.UtcNow < expiresUtc)
            {
                token.ThrowIfCancellationRequested();
                await Task.Delay(500, token).ConfigureAwait(false);
                ToolExecutionRecord? latest = await Database.GetToolCallAsync(executionContext.TenantId, record.Id, token).ConfigureAwait(false);
                if (latest == null) continue;
                if (String.Equals(latest.Status, ToolStatuses.Approved, StringComparison.OrdinalIgnoreCase))
                {
                    return new ToolApprovalDecision { Approved = true, UserId = latest.ApprovedByUserId };
                }

                if (String.Equals(latest.Status, ToolStatuses.Denied, StringComparison.OrdinalIgnoreCase))
                {
                    return new ToolApprovalDecision { Approved = false, Reason = latest.ErrorMessage ?? "Tool execution was denied.", UserId = latest.ApprovedByUserId };
                }
            }

            record.Status = ToolStatuses.TimedOut;
            record.Denied = true;
            record.ErrorCode = "tool_call_approval_timeout";
            record.ErrorMessage = "Tool execution was denied because approval timed out.";
            record.CompletedUtc = DateTime.UtcNow;
            record.ElapsedMs = (record.CompletedUtc.Value - startedUtc).TotalMilliseconds;
            await Database.UpdateToolCallAsync(record, CancellationToken.None).ConfigureAwait(false);
            return new ToolApprovalDecision { Approved = false, Reason = record.ErrorMessage };
        }

        private async Task UpsertToolRecordsAsync(ToolRun run, List<ToolExecutionRecord> records, CancellationToken token)
        {
            List<ToolExecutionRecord> existing = await Database.GetToolCallsForConversationAsync(run.TenantId, run.ConversationId, token).ConfigureAwait(false);
            foreach (ToolExecutionRecord record in records)
            {
                ToolExecutionRecord? current = existing.FirstOrDefault(item =>
                    String.Equals(item.RunId, run.RunId, StringComparison.OrdinalIgnoreCase)
                    && (String.Equals(item.ToolCallId, record.ToolCallId, StringComparison.OrdinalIgnoreCase)
                        || item.SequenceNumber == record.SequenceNumber));
                if (current == null)
                {
                    await Database.CreateToolCallAsync(record, token).ConfigureAwait(false);
                    continue;
                }

                record.Id = current.Id;
                record.ApprovedByUserId = current.ApprovedByUserId;
                await Database.UpdateToolCallAsync(record, token).ConfigureAwait(false);
            }

            _ApprovedToolRuns.TryRemove(ToolRunApprovalKey(run.TenantId, run.RunId), out _);
        }

        private async Task HandleToolApprovalAsync(HttpContextBase ctx, RequestContext requestContext)
        {
            string runId = Segment(ctx.Request.Url.RawWithoutQuery, 3);
            string toolCallId = Segment(ctx.Request.Url.RawWithoutQuery, 5);
            ToolApprovalRequest body = Body<ToolApprovalRequest>(ctx);
            string tenantId = requestContext.IsAdmin ? Query(ctx, "tenantId") ?? requestContext.TenantId ?? String.Empty : requestContext.TenantId ?? String.Empty;
            if (String.IsNullOrWhiteSpace(tenantId)) throw new ArgumentException("tenantId is required for global administrators when approving a tool call.");

            ToolRun? run = await Database.GetToolRunAsync(tenantId, runId, ctx.Token).ConfigureAwait(false);
            if (run == null) throw new KeyNotFoundException("Tool run not found.");

            Conversation? conversation = await Database.GetConversationAsync(tenantId, run.ConversationId, ctx.Token).ConfigureAwait(false);
            if (conversation == null) throw new KeyNotFoundException("Conversation not found.");
            EnsureConversationAccess(requestContext, conversation);

            if (!requestContext.IsAdmin && !requestContext.IsTenantAdmin && !String.Equals(run.UserId, requestContext.UserId, StringComparison.OrdinalIgnoreCase))
                throw new UnauthorizedAccessException("Only the user who started the tool run may approve this tool call.");

            List<ToolExecutionRecord> records = await Database.GetToolCallsForConversationAsync(tenantId, run.ConversationId, ctx.Token).ConfigureAwait(false);
            ToolExecutionRecord? record = records.FirstOrDefault(item =>
                String.Equals(item.RunId, run.RunId, StringComparison.OrdinalIgnoreCase)
                && (String.Equals(item.ToolCallId, toolCallId, StringComparison.OrdinalIgnoreCase) || String.Equals(item.Id, toolCallId, StringComparison.OrdinalIgnoreCase)));
            if (record == null) throw new KeyNotFoundException("Tool call not found.");
            if (!String.Equals(record.Status, ToolStatuses.PendingApproval, StringComparison.OrdinalIgnoreCase)
                && !String.Equals(record.Status, ToolStatuses.Proposed, StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("Only proposed or pending tool calls can be approved or denied.");

            record.ApprovedByUserId = requestContext.UserId;
            record.Status = body.Approved ? ToolStatuses.Approved : ToolStatuses.Denied;
            record.Denied = !body.Approved;
            record.Success = false;
            record.ErrorCode = body.Approved ? record.ErrorCode : "tool_call_denied";
            record.ErrorMessage = body.Approved ? record.ErrorMessage : String.IsNullOrWhiteSpace(body.Reason) ? "Tool execution was denied by the user." : body.Reason;
            await Database.UpdateToolCallAsync(record, ctx.Token).ConfigureAwait(false);
            if (body.Approved && body.AlwaysForRun)
            {
                _ApprovedToolRuns[ToolRunApprovalKey(tenantId, run.RunId)] = requestContext.UserId ?? String.Empty;
            }

            await SendJsonAsync(ctx, new ToolApprovalResponse { ToolCall = record }).ConfigureAwait(false);
        }

        private static string ToolRunApprovalKey(string tenantId, string runId)
        {
            return (tenantId ?? String.Empty) + ":" + (runId ?? String.Empty);
        }

        private async Task<PromptSelection> ResolvePromptSelectionAsync(ChatRequest body, RequestContext requestContext, ChatToolPlan toolPlan, CancellationToken token)
        {
            string tenantId = requestContext.TenantId ?? String.Empty;
            if (String.IsNullOrWhiteSpace(tenantId)) throw new ArgumentException("Chat requests require a tenant-scoped user.");

            PromptTemplate systemTemplate = await ResolvePromptTemplateAsync(tenantId, body.SystemPromptId, PromptTemplateKind.System, token).ConfigureAwait(false);
            string systemPrompt = String.IsNullOrWhiteSpace(body.Settings.SystemPrompt) || String.Equals(body.Settings.SystemPrompt, CompletionRequestSettings.DefaultSystemPrompt, StringComparison.Ordinal)
                ? systemTemplate.Content
                : body.Settings.SystemPrompt;
            body.SystemPromptId = systemTemplate.Id;
            body.Settings.SystemPrompt = systemPrompt;

            PromptSelection selection = new PromptSelection
            {
                SystemPromptId = systemTemplate.Id,
                SystemPromptName = systemTemplate.Name,
                SystemPromptDefault = systemTemplate.IsDefault,
                SystemPromptHash = PromptContentHash(systemPrompt)
            };

            if (!toolPlan.Enabled)
            {
                body.ToolPromptId = String.Empty;
                body.Settings.ToolSystemPrompt = String.Empty;
                return selection;
            }

            PromptTemplate toolTemplate = await ResolvePromptTemplateAsync(tenantId, body.ToolPromptId, PromptTemplateKind.Tool, token).ConfigureAwait(false);
            string toolPrompt = String.IsNullOrWhiteSpace(body.Settings.ToolSystemPrompt)
                ? toolTemplate.Content
                : body.Settings.ToolSystemPrompt;
            string toolInstructions = ToolAgentService.BuildToolSystemInstruction(toolPlan.ToolService);
            toolPrompt = RenderToolPrompt(toolPrompt, toolInstructions);
            body.ToolPromptId = toolTemplate.Id;
            body.Settings.ToolSystemPrompt = toolPrompt;
            selection.ToolPromptId = toolTemplate.Id;
            selection.ToolPromptName = toolTemplate.Name;
            selection.ToolPromptDefault = toolTemplate.IsDefault;
            selection.ToolPromptHash = PromptContentHash(toolPrompt);
            return selection;
        }

        private async Task<PromptTemplate> ResolvePromptTemplateAsync(string tenantId, string? promptId, PromptTemplateKind kind, CancellationToken token)
        {
            PromptTemplate? prompt;
            if (String.IsNullOrWhiteSpace(promptId))
            {
                prompt = await Database.GetDefaultPromptTemplateAsync(tenantId, kind, token).ConfigureAwait(false);
                if (prompt == null)
                {
                    await Database.EnsureDefaultPromptTemplatesAsync(tenantId, token).ConfigureAwait(false);
                    prompt = await Database.GetDefaultPromptTemplateAsync(tenantId, kind, token).ConfigureAwait(false);
                }
            }
            else
            {
                prompt = await Database.GetPromptTemplateAsync(tenantId, promptId, token).ConfigureAwait(false);
            }

            if (prompt == null) throw new KeyNotFoundException(kind + " prompt template not found.");
            if (prompt.Kind != kind) throw new ArgumentException("Selected prompt template is not a " + kind.ToString().ToLowerInvariant() + " prompt.");
            if (!prompt.Active) throw new ArgumentException("Selected prompt template is inactive.");
            return prompt;
        }

        private static string RenderToolPrompt(string prompt, string toolInstructions)
        {
            string value = prompt ?? String.Empty;
            if (value.Contains("{{tool_catalog}}", StringComparison.OrdinalIgnoreCase))
                value = value.Replace("{{tool_catalog}}", toolInstructions ?? String.Empty, StringComparison.OrdinalIgnoreCase);
            return value;
        }

        private static string PromptContentHash(string prompt)
        {
            byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(prompt ?? String.Empty));
            return Convert.ToHexString(hash).ToLowerInvariant();
        }

        private ChatToolPlan ResolveChatToolPlan(ChatRequest body, RequestContext requestContext, ModelRunnerSettings runner, bool streaming)
        {
            bool requestedToolsEnabled = body.ToolsEnabled ?? Settings.Tools.Enabled;
            if (!requestedToolsEnabled) return ChatToolPlan.Disabled();

            ModelRunnerSettings runnerDefaults = new ModelRunnerSettings
            {
                Id = runner.Id,
                Name = runner.Name,
                ApiType = runner.ApiType,
                Endpoint = runner.Endpoint,
                ApiKey = runner.ApiKey,
                Models = new List<string>(runner.Models),
                ContextWindowTokens = runner.ContextWindowTokens,
                ToolsEnabled = runner.ToolsEnabled,
                SupportsTools = runner.SupportsTools,
                ToolCallingApiFormat = runner.ToolCallingApiFormat,
                SupportsParallelToolCalls = runner.SupportsParallelToolCalls,
                SupportsStreamingToolCalls = runner.SupportsStreamingToolCalls,
                ChatCompletionsPath = runner.ChatCompletionsPath
            };
            ModelRunnerSettings.ApplyToolDefaults(runnerDefaults);
            if (body.ToolsEnabled == true)
            {
                runnerDefaults.ToolsEnabled = true;
            }

            if (!runnerDefaults.ToolsEnabled || !runnerDefaults.SupportsTools || String.IsNullOrWhiteSpace(runnerDefaults.ToolCallingApiFormat))
            {
                if (body.ToolsEnabled == true) throw new ArgumentException("The selected runner is not configured for tool-capable requests.");
                return ChatToolPlan.Disabled();
            }

            ToolsSettings tools = CloneTools(Settings.Tools);
            tools.Enabled = requestedToolsEnabled;

            if (!String.IsNullOrWhiteSpace(body.ApprovalPolicy))
            {
                string approvalPolicy = NormalizeApprovalPolicy(body.ApprovalPolicy!);
                if (String.Equals(approvalPolicy, ToolApprovalPolicies.Auto, StringComparison.OrdinalIgnoreCase) && !requestContext.IsAdmin)
                    throw new UnauthorizedAccessException("Only administrators may request automatic tool approval.");
                tools.DefaultApprovalPolicy = approvalPolicy;
            }

            if (!String.IsNullOrWhiteSpace(body.WorkingDirectory))
            {
                RequireAdmin(requestContext);
                tools.WorkingDirectory = body.WorkingDirectory!;
            }

            if (body.ToolNames != null && body.ToolNames.Count > 0)
            {
                List<string> requested = NormalizeToolNameList(body.ToolNames);
                tools.EnabledToolNames = tools.EnabledToolNames.Count > 0
                    ? tools.EnabledToolNames.Intersect(requested, StringComparer.OrdinalIgnoreCase).ToList()
                    : requested;
            }

            NormalizeTools(tools, true);
            if (!streaming && String.Equals(tools.DefaultApprovalPolicy, ToolApprovalPolicies.Ask, StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("Non-streaming tool approval is not implemented. Set approvalPolicy to 'auto' for safe tools, or disable tools for this request.");

            Settings effectiveSettings = new Settings { Tools = tools };
            ToolService effectiveToolService = new ToolService(effectiveSettings, McpTools);
            if (effectiveToolService.GetModelToolDefinitions().Count == 0)
            {
                if (body.ToolsEnabled == true) throw new ArgumentException("No executable tools are available for this request. Check tool working directory, allowed roots, and enabled tool names.");
                return ChatToolPlan.Disabled();
            }

            return new ChatToolPlan
            {
                Enabled = true,
                Settings = effectiveSettings,
                ToolService = effectiveToolService
            };
        }

        private ToolPolicyValidationResult ValidateToolPolicy(ToolsSettings? draftTools)
        {
            ToolsSettings tools = draftTools == null ? CloneTools(Settings.Tools) : CloneTools(draftTools);
            NormalizeTools(tools);
            Settings validationSettings = new Settings { Tools = tools };
            ToolService service = new ToolService(validationSettings, McpTools);
            List<ToolDescriptor> descriptors = service.ListTools(true);
            List<ToolDescriptor> available = descriptors.Where(tool => tool.Available).ToList();
            HashSet<string> registeredNames = new HashSet<string>(descriptors.Select(tool => tool.Name), StringComparer.OrdinalIgnoreCase);

            ToolPolicyValidationResult result = new ToolPolicyValidationResult
            {
                ToolsEnabled = tools.Enabled,
                ApprovalPolicy = tools.DefaultApprovalPolicy,
                AvailableToolCount = available.Count,
                Tools = descriptors
            };

            if (!tools.Enabled)
            {
                result.Warnings.Add("Tools are disabled globally.");
            }

            if (tools.Enabled && !tools.BuiltInsEnabled)
            {
                result.Warnings.Add("Built-in tools are disabled.");
            }

            if (tools.Enabled && String.IsNullOrWhiteSpace(tools.WorkingDirectory))
            {
                result.Errors.Add("A working directory is required for filesystem and process tools.");
            }

            if (tools.Enabled && tools.AllowedRoots.Count == 0)
            {
                result.Errors.Add("At least one allowed root is required for filesystem and process tools.");
            }

            foreach (string name in tools.EnabledToolNames.Where(name => !registeredNames.Contains(name)))
            {
                result.Errors.Add("Enabled tool name is not registered: " + name + ".");
            }

            foreach (string name in tools.DisabledToolNames.Where(name => !registeredNames.Contains(name)))
            {
                result.Warnings.Add("Disabled tool name is not registered: " + name + ".");
            }

            if (tools.Enabled && available.Count == 0)
            {
                result.Errors.Add("No executable tools are available. Check working directory, allowed roots, built-in enablement, enabled names, and disabled names.");
            }

            if (String.Equals(tools.DefaultApprovalPolicy, ToolApprovalPolicies.Ask, StringComparison.OrdinalIgnoreCase))
            {
                result.Warnings.Add("Interactive approval requires streaming chat; non-streaming tool chat rejects ask approval.");
            }

            if (tools.WebSearch.Enabled)
            {
                if (!tools.WebSearch.Providers.Any(provider => provider.Enabled))
                {
                    result.Warnings.Add("Web search is enabled, but no enabled search provider is configured.");
                }
            }

            if (tools.Mcp.Enabled)
            {
                if (!tools.Mcp.Servers.Any(server => server.Enabled))
                {
                    result.Warnings.Add("MCP is enabled, but no enabled MCP server is configured.");
                }
            }

            result.Success = result.Errors.Count == 0;
            return result;
        }

        private ToolPolicyTestResult TestToolPolicy(ToolPolicyTestRequest request)
        {
            ToolPolicyValidationResult validation = ValidateToolPolicy(request.Tools);
            ToolPolicyTestResult result = new ToolPolicyTestResult
            {
                AvailableToolCount = validation.AvailableToolCount,
                Tools = validation.Tools,
                Warnings = new List<string>(validation.Warnings),
                Errors = new List<string>(validation.Errors)
            };

            if (!validation.ToolsEnabled)
            {
                result.Errors.Add("Tools are disabled globally.");
            }

            if (String.IsNullOrWhiteSpace(request.RunnerId))
            {
                result.RunnerFound = true;
                result.Warnings.Add("No runnerId was supplied, so only tool policy and workspace prerequisites were tested.");
                result.Success = result.Errors.Count == 0;
                return result;
            }

            try
            {
                ModelRunnerSettings runner = Inference.GetRunner(request.RunnerId);
                ModelRunnerSettings runnerDefaults = CloneRunner(runner);
                ModelRunnerSettings.ApplyToolDefaults(runnerDefaults);
                result.RunnerFound = true;
                result.RunnerToolsEnabled = runnerDefaults.ToolsEnabled;
                result.RunnerSupportsTools = runnerDefaults.SupportsTools;
                result.ToolCallingApiFormat = runnerDefaults.ToolCallingApiFormat;

                if (!runnerDefaults.ToolsEnabled)
                {
                    result.Errors.Add("The selected runner has tools disabled.");
                }

                if (!runnerDefaults.SupportsTools)
                {
                    result.Errors.Add("The selected runner does not support tool calls.");
                }

                if (String.IsNullOrWhiteSpace(runnerDefaults.ToolCallingApiFormat))
                {
                    result.Errors.Add("The selected runner has no tool-calling API format.");
                }
            }
            catch (KeyNotFoundException)
            {
                result.RunnerFound = false;
                result.Errors.Add("The selected runner was not found.");
            }

            result.Success = result.Errors.Count == 0;
            return result;
        }

        private async Task ReloadMcpAsync(CancellationToken token)
        {
            await McpTools.InitializeAsync(Settings, token).ConfigureAwait(false);
            ToolService = new ToolService(Settings, McpTools);
        }

        private static ModelRunnerSettings CloneRunner(ModelRunnerSettings source)
        {
            return new ModelRunnerSettings
            {
                Id = source.Id,
                Name = source.Name,
                ApiType = source.ApiType,
                Endpoint = source.Endpoint,
                ApiKey = source.ApiKey,
                Models = new List<string>(source.Models),
                ContextWindowTokens = source.ContextWindowTokens,
                ToolsEnabled = source.ToolsEnabled,
                SupportsTools = source.SupportsTools,
                ToolCallingApiFormat = source.ToolCallingApiFormat,
                SupportsParallelToolCalls = source.SupportsParallelToolCalls,
                SupportsStreamingToolCalls = source.SupportsStreamingToolCalls,
                ChatCompletionsPath = source.ChatCompletionsPath,
                HealthCheckEnabled = source.HealthCheckEnabled,
                HealthCheckUrl = source.HealthCheckUrl,
                HealthCheckMethod = source.HealthCheckMethod,
                HealthCheckIntervalMs = source.HealthCheckIntervalMs,
                HealthCheckTimeoutMs = source.HealthCheckTimeoutMs,
                HealthCheckExpectedStatusCode = source.HealthCheckExpectedStatusCode,
                HealthyThreshold = source.HealthyThreshold,
                UnhealthyThreshold = source.UnhealthyThreshold,
                HealthCheckUseAuth = source.HealthCheckUseAuth
            };
        }

        private static ToolsSettings CloneTools(ToolsSettings source)
        {
            WebSearchToolSettings sourceWebSearch = source.WebSearch ?? new WebSearchToolSettings();
            McpToolSettings sourceMcp = source.Mcp ?? new McpToolSettings();
            return new ToolsSettings
            {
                Enabled = source.Enabled,
                BuiltInsEnabled = source.BuiltInsEnabled,
                DefaultApprovalPolicy = source.DefaultApprovalPolicy,
                DestructiveToolsRequireApproval = source.DestructiveToolsRequireApproval,
                BlockSecretPaths = source.BlockSecretPaths,
                WorkingDirectory = source.WorkingDirectory,
                AllowedRoots = new List<string>(source.AllowedRoots ?? new List<string>()),
                MaxAgentIterations = source.MaxAgentIterations,
                MaxToolIterations = source.MaxToolIterations,
                MaxToolCallsPerTurn = source.MaxToolCallsPerTurn,
                ToolChoiceMode = source.ToolChoiceMode,
                AllowParallelToolCalls = source.AllowParallelToolCalls,
                MaxParallelToolCalls = source.MaxParallelToolCalls,
                EmitProgressEvents = source.EmitProgressEvents,
                ExposeToolTracesToUsers = source.ExposeToolTracesToUsers,
                ToolTimeoutMs = source.ToolTimeoutMs,
                ApprovalTimeoutMs = source.ApprovalTimeoutMs,
                ProcessTimeoutMs = source.ProcessTimeoutMs,
                MaxReadFileBytes = source.MaxReadFileBytes,
                MaxToolResultBytes = source.MaxToolResultBytes,
                StoreToolResults = source.StoreToolResults,
                StoreFullToolResults = source.StoreFullToolResults,
                StoreToolArguments = source.StoreToolArguments,
                MaxToolOutputChars = source.MaxToolOutputChars,
                MaxToolOutputCharsPerTurn = source.MaxToolOutputCharsPerTurn,
                MaxToolResultItems = source.MaxToolResultItems,
                EnabledToolNames = new List<string>(source.EnabledToolNames ?? new List<string>()),
                DisabledToolNames = new List<string>(source.DisabledToolNames ?? new List<string>()),
                WebSearch = new WebSearchToolSettings
                {
                    Enabled = sourceWebSearch.Enabled,
                    AllowFallback = sourceWebSearch.AllowFallback,
                    Providers = (sourceWebSearch.Providers ?? new List<WebSearchProviderSettings>())
                    .Where(provider => provider != null)
                    .Select(provider => new WebSearchProviderSettings
                    {
                        Name = provider.Name,
                        ProviderType = provider.ProviderType,
                        Endpoint = provider.Endpoint,
                        ApiKey = provider.ApiKey,
                        Enabled = provider.Enabled,
                        IsDefault = provider.IsDefault,
                        TimeoutMs = provider.TimeoutMs
                    }).ToList()
                },
                Mcp = new McpToolSettings
                {
                    Enabled = sourceMcp.Enabled,
                    Servers = (sourceMcp.Servers ?? new List<McpServerSettings>())
                    .Where(server => server != null)
                    .Select(server => new McpServerSettings
                    {
                        Name = server.Name,
                        Transport = server.Transport,
                        Command = server.Command,
                        Args = new List<string>(server.Args ?? new List<string>()),
                        Env = new Dictionary<string, string>(server.Env ?? new Dictionary<string, string>(), StringComparer.OrdinalIgnoreCase),
                        Url = server.Url,
                        McpPath = server.McpPath,
                        Enabled = server.Enabled
                    }).ToList()
                }
            };
        }

        private static string NormalizeApprovalPolicy(string approvalPolicy)
        {
            string normalized = NormalizeValue(approvalPolicy, ToolApprovalPolicies.Ask, ToolApprovalPolicies.Deny, ToolApprovalPolicies.Ask, ToolApprovalPolicies.Auto);
            if (!String.Equals(normalized, approvalPolicy.Trim(), StringComparison.OrdinalIgnoreCase)
                && !String.IsNullOrWhiteSpace(approvalPolicy))
                throw new ArgumentException("Invalid approvalPolicy value. Use deny, ask, or auto.");
            return normalized;
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

        private static void EnsureConversationAccess(RequestContext requestContext, Conversation conversation)
        {
            if (!requestContext.IsAdmin && !requestContext.IsTenantAdmin && !String.Equals(conversation.UserId, requestContext.UserId, StringComparison.OrdinalIgnoreCase))
            {
                throw new UnauthorizedAccessException("Conversation access denied.");
            }
        }

        private async Task<PromptTemplate> GetPromptTemplateForRequestAsync(RequestContext requestContext, string id, CancellationToken token)
        {
            PromptTemplate? existing = null;
            if (requestContext.IsAdmin && String.IsNullOrWhiteSpace(requestContext.TenantId))
            {
                existing = (await Database.GetPromptTemplatesAsync(null, null, true, token).ConfigureAwait(false)).FirstOrDefault(item => String.Equals(item.Id, id, StringComparison.OrdinalIgnoreCase));
            }
            else
            {
                existing = await Database.GetPromptTemplateAsync(requestContext.TenantId ?? String.Empty, id, token).ConfigureAwait(false);
            }

            if (existing == null) throw new KeyNotFoundException("Prompt template not found.");
            if (!requestContext.IsAdmin && !String.Equals(existing.TenantId, requestContext.TenantId, StringComparison.OrdinalIgnoreCase))
                throw new UnauthorizedAccessException("Prompt template access denied.");
            if (!existing.Active && !requestContext.IsAdmin && !requestContext.IsTenantAdmin)
                throw new KeyNotFoundException("Prompt template not found.");
            return existing;
        }

        private async Task ValidatePromptNameAvailableAsync(PromptTemplate item, CancellationToken token)
        {
            if (String.IsNullOrWhiteSpace(item.Name)) throw new ArgumentException("Prompt template name is required.");
            if (String.IsNullOrWhiteSpace(item.Content)) throw new ArgumentException("Prompt template content is required.");
            List<PromptTemplate> existing = await Database.GetPromptTemplatesAsync(item.TenantId, item.Kind, true, token).ConfigureAwait(false);
            if (existing.Any(prompt => !String.Equals(prompt.Id, item.Id, StringComparison.OrdinalIgnoreCase) && String.Equals(prompt.Name, item.Name, StringComparison.OrdinalIgnoreCase)))
                throw new ArgumentException("A prompt template with this name already exists for the tenant and kind.");
        }

        private static PromptTemplateKind? ParsePromptKind(string? value, bool required)
        {
            if (String.IsNullOrWhiteSpace(value))
            {
                if (required) throw new ArgumentException("Prompt kind is required.");
                return null;
            }

            if (Enum.TryParse(value, true, out PromptTemplateKind kind)) return kind;
            throw new ArgumentException("Prompt kind must be system or tool.");
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

        private void SetRequestCapture(ChatMessage message, string responseBody, ToolRun? toolRun = null, ChatToolMetrics? metrics = null, PromptSelection? promptSelection = null)
        {
            RequestCapture? capture = _RequestCapture.Value;
            if (capture == null) return;
            capture.TimeToFirstTokenMs = message.TimeToFirstTokenMs;
            capture.StreamingTimeMs = message.StreamingTimeMs;
            capture.TotalTimeMs = message.TotalTimeMs;
            capture.TokensUsed = message.TokensUsed;
            capture.ResponseBody = responseBody;
            capture.ToolRunId = toolRun?.RunId ?? String.Empty;
            capture.ToolCallCount = metrics?.ToolCallCount ?? 0;
            capture.ToolElapsedMs = metrics?.TotalToolElapsedMs ?? 0;
            capture.AgentIterations = metrics?.IterationCount ?? 0;
            if (promptSelection != null)
            {
                capture.SystemPromptId = promptSelection.SystemPromptId;
                capture.SystemPromptName = promptSelection.SystemPromptName;
                capture.SystemPromptDefault = promptSelection.SystemPromptDefault;
                capture.SystemPromptHash = promptSelection.SystemPromptHash;
                capture.ToolPromptId = promptSelection.ToolPromptId;
                capture.ToolPromptName = promptSelection.ToolPromptName;
                capture.ToolPromptDefault = promptSelection.ToolPromptDefault;
                capture.ToolPromptHash = promptSelection.ToolPromptHash;
            }
        }

        private static List<ToolTrace> BuildSafeToolTraces(List<ToolTrace> traces, List<ToolExecutionRecord> records)
        {
            List<ToolTrace> safe = new List<ToolTrace>();
            for (int i = 0; i < traces.Count; i++)
            {
                ToolTrace trace = traces[i];
                ToolExecutionRecord? record = i < records.Count ? records[i] : null;
                safe.Add(new ToolTrace
                {
                    ToolCallId = record?.ToolCallId ?? IdGenerator.ToolCall(),
                    ToolName = trace.ToolName,
                    DisplayLabel = trace.DisplayLabel,
                    Iteration = trace.Iteration,
                    SequenceNumber = trace.SequenceNumber,
                    Success = trace.Success,
                    Denied = trace.Denied,
                    Truncated = trace.Truncated,
                    OutputCharacters = trace.OutputCharacters,
                    ResultCount = trace.ResultCount,
                    ElapsedMs = trace.ElapsedMs,
                    Summary = SafeToolSummary(trace),
                    StartedUtc = trace.StartedUtc,
                    CompletedUtc = trace.CompletedUtc
                });
            }

            return safe;
        }

        private static string SafeToolSummary(ToolTrace trace)
        {
            string summary = String.IsNullOrWhiteSpace(trace.Summary) ? (trace.Success ? "Completed." : "Tool call failed.") : trace.Summary!;
            return ToolAuditSanitizer.RedactAndCapText(summary, 4000);
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

        private static string SseEventName(string? eventType)
        {
            if (String.Equals(eventType, ToolEventTypes.ToolIterationStarted, StringComparison.OrdinalIgnoreCase)
                || String.Equals(eventType, ToolEventTypes.ToolIterationStopped, StringComparison.OrdinalIgnoreCase))
                return "tool_iteration";
            if (String.Equals(eventType, ToolEventTypes.ToolCallPendingApproval, StringComparison.OrdinalIgnoreCase)) return "tool_call_pending_approval";
            if (String.Equals(eventType, ToolEventTypes.ToolCallApproved, StringComparison.OrdinalIgnoreCase)) return "tool_call_approved";
            if (String.Equals(eventType, ToolEventTypes.ToolCallStarted, StringComparison.OrdinalIgnoreCase)) return "tool_call_running";
            if (String.Equals(eventType, ToolEventTypes.ToolCallHeartbeat, StringComparison.OrdinalIgnoreCase)) return "tool_call_heartbeat";
            if (String.Equals(eventType, ToolEventTypes.ToolCallCompleted, StringComparison.OrdinalIgnoreCase)) return "tool_call_completed";
            if (String.Equals(eventType, ToolEventTypes.ToolCallFailed, StringComparison.OrdinalIgnoreCase)) return "tool_call_failed";
            if (String.Equals(eventType, ToolEventTypes.ToolCallDenied, StringComparison.OrdinalIgnoreCase)) return "tool_call_denied";
            return "tool_iteration";
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
                    new { name = "Tools", description = "Tool catalog, diagnostics, and run history." },
                    new { name = "Prompts", description = "Tenant-scoped system and tool prompt templates." },
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
                    { "/v1.0/api/tools", Path(Operation("get", "listTools", "Tools", "List effective tools", "Lists effective tool descriptors for the authenticated principal, including unavailable diagnostics when tools or prerequisites are disabled.", "ToolDescriptorArray", true)) },
                    { "/v1.0/api/tools/instructions", Path(Operation("get", "getToolInstructions", "Tools", "Get default tool system instructions", "Returns the generated Wilson tool instruction block that users can review, edit, or restore in tool-enabled chat.", "ToolInstructionsResponse", true)) },
                    { "/v1.0/api/mcp", Path(Operation("get", "getMcpStatus", "Tools", "Get MCP status", "Returns redacted MCP server connection status and discovered tool counts. Requires a global administrator bearer token.", "McpStatusResponse", true)) },
                    { "/v1.0/api/mcp/reload", Path(Operation("post", "reloadMcp", "Tools", "Reload MCP servers", "Reconnects configured MCP servers and rediscovers MCP tools. Requires a global administrator bearer token.", "McpStatusResponse", true)) },
                    { "/v1.0/api/tools/validate", Path(Operation("post", "validateTools", "Tools", "Validate draft tool policy", "Validates draft tool settings without saving them. Requires a global administrator bearer token.", "ToolPolicyValidationResult", true, requestSchema: "ToolPolicyValidationRequest")) },
                    { "/v1.0/api/tools/test", Path(Operation("post", "testTools", "Tools", "Test tool readiness", "Runs dry-run tool readiness diagnostics against draft tool settings and an optional model runner. Requires a global administrator bearer token.", "ToolPolicyTestResult", true, requestSchema: "ToolPolicyTestRequest")) },
                    { "/v1.0/api/tools/{name}", Path(Operation("get", "getTool", "Tools", "Get tool descriptor", "Returns one effective tool descriptor by name.", "ToolDescriptor", true, parameters: Parameters(PathParameter("name", "Tool name.")))) },
                    { "/v1.0/api/tool-runs/{id}", Path(Operation("get", "getToolRun", "Tools", "Get tool run", "Returns one persisted tool run and its redacted tool-call records.", "ToolRunResponse", true, parameters: Parameters(PathParameter("id", "Tool run identifier."), TenantScopeParameter()))) },
                    { "/v1.0/api/tool-runs/{runId}/tool-calls/{toolCallId}/approval", Path(Operation("post", "approveToolCall", "Tools", "Approve or deny a tool call", "Approves or denies a proposed or pending tool call for a conversation visible to the authenticated principal.", "ToolApprovalResponse", true, parameters: Parameters(PathParameter("runId", "Tool run identifier."), PathParameter("toolCallId", "Tool call identifier."), TenantScopeParameter()), requestSchema: "ToolApprovalRequest")) },
                    { "/v1.0/api/prompts", Path(
                        Operation("get", "listPrompts", "Prompts", "List prompt templates", "Lists active prompt templates visible to the authenticated principal. Tenant administrators can include inactive templates.", "PromptTemplateEnumeration", true, parameters: WithPagination(TenantScopeParameter(), QueryParameter("kind", "Optional prompt kind: system or tool.", StringSchema()), QueryParameter("includeInactive", "When true, tenant administrators can include inactive prompt templates.", BooleanSchema()))),
                        Operation("post", "createPrompt", "Prompts", "Create prompt template", "Creates a tenant-scoped system or tool prompt template. Requires tenant administrator access.", "PromptTemplate", true, requestSchema: "PromptTemplate", successStatus: "201", successDescription: "Prompt template created.")) },
                    { "/v1.0/api/prompts/{id}", Path(
                        Operation("get", "getPrompt", "Prompts", "Get prompt template", "Returns one prompt template visible to the authenticated principal.", "PromptTemplate", true, parameters: Parameters(PathParameter("id", "Prompt template identifier."), TenantScopeParameter())),
                        Operation("put", "updatePrompt", "Prompts", "Update prompt template", "Updates a prompt template. Requires tenant administrator access.", "PromptTemplate", true, parameters: Parameters(PathParameter("id", "Prompt template identifier."), TenantScopeParameter()), requestSchema: "PromptTemplate"),
                        Operation("delete", "deletePrompt", "Prompts", "Delete prompt template", "Deletes a non-default, non-protected prompt template. Requires tenant administrator access.", null, true, parameters: Parameters(PathParameter("id", "Prompt template identifier."), TenantScopeParameter()), successStatus: "204", successDescription: "Prompt template deleted.")) },
                    { "/v1.0/api/prompts/{id}/default", Path(Operation("post", "setDefaultPrompt", "Prompts", "Set default prompt template", "Makes one active prompt template the tenant default for its kind. Requires tenant administrator access.", "PromptTemplate", true, parameters: Parameters(PathParameter("id", "Prompt template identifier."), TenantScopeParameter()))) },
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
                    { "/v1.0/api/conversations/{id}/tool-calls", Path(Operation("get", "listConversationToolCalls", "Chat", "List conversation tool calls", "Lists persisted redacted tool-call records for a conversation visible to the authenticated principal.", "ToolExecutionRecordEnumeration", true, parameters: WithPagination(PathParameter("id", "Conversation identifier.")))) },
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
                    { "/v1.0/api/request-history/{id}", Path(Operation("delete", "deleteRequestHistoryEntry", "Request History", "Delete request history entry", "Deletes one captured request history entry. Requires tenant administrator or global administrator access.", null, true, parameters: Parameters(PathParameter("id", "Request history entry identifier."), TenantScopeParameter()), successStatus: "204", successDescription: "Request history entry deleted.")) },
                    { "/v1.0/api/request-history/{id}/tool-calls", Path(Operation("get", "listRequestHistoryToolCalls", "Request History", "List request-history tool calls", "Lists persisted redacted tool-call records linked to one captured request-history entry. Requires tenant administrator or global administrator access.", "ToolExecutionRecordEnumeration", true, parameters: WithPagination(PathParameter("id", "Request history entry identifier."), TenantScopeParameter()))) }
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
                { "SseEventStream", new Dictionary<string, object> { { "type", "string" }, { "description", "Server-sent event stream containing conversation, truncation, chunk, run_started, tool_iteration, tool_call_running, tool_call_pending_approval, tool_call_approved, tool_call_completed, tool_call_failed, tool_call_denied, run_completed, done, or error events." } } },
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
                        Property("truncation", SchemaRef("ChatTruncationNotice")),
                        Property("toolRun", SchemaRef("ToolRun")),
                        Property("toolCalls", ArraySchema(SchemaRef("ToolTrace"))),
                        Property("toolMetrics", SchemaRef("ChatToolMetrics")))
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
            AddComponentSchema(schemas, typeof(PromptTemplate));
            AddComponentSchema(schemas, typeof(Conversation));
            AddComponentSchema(schemas, typeof(ChatMessage));
            AddComponentSchema(schemas, typeof(ChatTruncationNotice));
            AddComponentSchema(schemas, typeof(ChatRequest));
            AddComponentSchema(schemas, typeof(ChatToolMetrics));
            AddComponentSchema(schemas, typeof(ModelPullRequest));
            AddComponentSchema(schemas, typeof(Feedback));
            AddComponentSchema(schemas, typeof(RequestHistoryEntry));
            AddComponentSchema(schemas, typeof(RequestHistorySummary));
            AddComponentSchema(schemas, typeof(ToolDefinition));
            AddComponentSchema(schemas, typeof(ToolDescriptor));
            AddComponentSchema(schemas, typeof(ToolPolicyValidationRequest));
            AddComponentSchema(schemas, typeof(ToolPolicyValidationResult));
            AddComponentSchema(schemas, typeof(ToolPolicyTestRequest));
            AddComponentSchema(schemas, typeof(ToolPolicyTestResult));
            AddComponentSchema(schemas, typeof(ToolInstructionsResponse));
            AddComponentSchema(schemas, typeof(McpServerStatus));
            AddComponentSchema(schemas, typeof(McpStatusResponse));
            AddComponentSchema(schemas, typeof(ModelToolDefinition));
            AddComponentSchema(schemas, typeof(ModelToolFunctionDefinition));
            AddComponentSchema(schemas, typeof(ModelToolCall));
            AddComponentSchema(schemas, typeof(ModelToolFunctionCall));
            AddComponentSchema(schemas, typeof(ToolCall));
            AddComponentSchema(schemas, typeof(ToolResult));
            AddComponentSchema(schemas, typeof(ToolExecutionRecord));
            AddComponentSchema(schemas, typeof(ToolRun));
            AddComponentSchema(schemas, typeof(ToolProgressEvent));
            AddComponentSchema(schemas, typeof(ToolTrace));
            AddComponentSchema(schemas, typeof(ToolCapableInferenceRequest));
            AddComponentSchema(schemas, typeof(ToolCapableInferenceResponse));
            AddComponentSchema(schemas, typeof(ModelChatMessage));
            AddComponentSchema(schemas, typeof(ToolRunResponse));
            AddComponentSchema(schemas, typeof(ToolApprovalRequest));
            AddComponentSchema(schemas, typeof(ToolApprovalResponse));

            schemas["EndpointHealthStatusArray"] = ArraySchema(SchemaRef("EndpointHealthStatus"));
            schemas["ToolDescriptorArray"] = ArraySchema(SchemaRef("ToolDescriptor"));
            schemas["ModelRunnerStatusEnumeration"] = EnumerationSchema("ModelRunnerStatus");
            schemas["TenantEnumeration"] = EnumerationSchema("Tenant");
            schemas["UserEnumeration"] = EnumerationSchema("User");
            schemas["CredentialEnumeration"] = EnumerationSchema("Credential");
            schemas["PromptTemplateEnumeration"] = EnumerationSchema("PromptTemplate");
            schemas["ConversationEnumeration"] = EnumerationSchema("Conversation");
            schemas["ChatMessageEnumeration"] = EnumerationSchema("ChatMessage");
            schemas["FeedbackEnumeration"] = EnumerationSchema("Feedback");
            schemas["RequestHistoryEntryEnumeration"] = EnumerationSchema("RequestHistoryEntry");
            schemas["ToolExecutionRecordEnumeration"] = EnumerationSchema("ToolExecutionRecord");

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
            if (type == typeof(object)) return new Dictionary<string, object> { { "type", "object" } };
            if (type == typeof(string)) return StringSchema();
            if (type == typeof(bool)) return BooleanSchema();
            if (type == typeof(int)) return IntegerSchema();
            if (type == typeof(long)) return IntegerSchema("int64");
            if (type == typeof(double) || type == typeof(float) || type == typeof(decimal)) return NumberSchema();
            if (type == typeof(DateTime) || type == typeof(DateTimeOffset)) return StringSchema("date-time");
            if (type.IsEnum) return EnumSchema(type);
            if (type.IsArray) return ArraySchema(SchemaForType(type.GetElementType()!, schemas));
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>)) return ArraySchema(SchemaForType(type.GetGenericArguments()[0], schemas));
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Dictionary<,>))
            {
                return new Dictionary<string, object>
                {
                    { "type", "object" },
                    { "additionalProperties", SchemaForType(type.GetGenericArguments()[1], schemas) }
                };
            }

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
        public string ToolRunId { get; set; } = String.Empty;
        public int ToolCallCount { get; set; }
        public double ToolElapsedMs { get; set; }
        public int AgentIterations { get; set; }
        public string SystemPromptId { get; set; } = String.Empty;
        public string SystemPromptName { get; set; } = String.Empty;
        public bool SystemPromptDefault { get; set; }
        public string SystemPromptHash { get; set; } = String.Empty;
        public string ToolPromptId { get; set; } = String.Empty;
        public string ToolPromptName { get; set; } = String.Empty;
        public bool ToolPromptDefault { get; set; }
        public string ToolPromptHash { get; set; } = String.Empty;
    }

    internal sealed class PromptSelection
    {
        public string SystemPromptId { get; set; } = String.Empty;
        public string SystemPromptName { get; set; } = String.Empty;
        public bool SystemPromptDefault { get; set; }
        public string SystemPromptHash { get; set; } = String.Empty;
        public string ToolPromptId { get; set; } = String.Empty;
        public string ToolPromptName { get; set; } = String.Empty;
        public bool ToolPromptDefault { get; set; }
        public string ToolPromptHash { get; set; } = String.Empty;
    }

    internal sealed class ChatToolPlan
    {
        public bool Enabled { get; set; } = false;
        public Settings Settings { get; set; } = new Settings();
        public ToolService ToolService { get; set; } = new ToolService(new Settings());

        public static ChatToolPlan Disabled()
        {
            return new ChatToolPlan();
        }
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
        /// <summary>Selected system prompt template identifier. Leave blank to use the tenant default.</summary>
        public string? SystemPromptId { get; set; } = null;
        /// <summary>Selected tool prompt template identifier. Leave blank to use the tenant default when tools are enabled.</summary>
        public string? ToolPromptId { get; set; } = null;
        /// <summary>Override whether tools are enabled for this request. Null uses server settings.</summary>
        public bool? ToolsEnabled { get; set; } = null;
        /// <summary>Override approval policy for this request. Accepted values: deny, ask, auto.</summary>
        public string? ApprovalPolicy { get; set; } = null;
        /// <summary>Optional tool allow-list for this request.</summary>
        public List<string> ToolNames { get; set; } = new List<string>();
        /// <summary>Optional working directory override. Requires administrator access.</summary>
        public string? WorkingDirectory { get; set; } = null;
    }

    /// <summary>
    /// Chat response.
    /// </summary>
    public sealed class ChatResponse
    {
        /// <summary>Conversation.</summary>
        public Conversation? Conversation { get; set; } = null;
        /// <summary>User message.</summary>
        public ChatMessage? UserMessage { get; set; } = null;
        /// <summary>Assistant message.</summary>
        public ChatMessage? AssistantMessage { get; set; } = null;
        /// <summary>Truncation notice.</summary>
        public ChatTruncationNotice? Truncation { get; set; } = null;
        /// <summary>Tool run metadata, when tools were used.</summary>
        public ToolRun? ToolRun { get; set; } = null;
        /// <summary>Safe tool trace metadata, when tools were used.</summary>
        public List<ToolTrace> ToolCalls { get; set; } = new List<ToolTrace>();
        /// <summary>Aggregate tool metrics, when tools were used.</summary>
        public ChatToolMetrics? ToolMetrics { get; set; } = null;
    }

    /// <summary>
    /// Aggregate chat tool metrics.
    /// </summary>
    public sealed class ChatToolMetrics
    {
        /// <summary>Whether tools were enabled for the response.</summary>
        public bool ToolsEnabled { get; set; } = false;
        /// <summary>Tool call count.</summary>
        public int ToolCallCount { get; set; } = 0;
        /// <summary>Tool error count.</summary>
        public int ErrorCount { get; set; } = 0;
        /// <summary>Tool loop iteration count.</summary>
        public int IterationCount { get; set; } = 0;
        /// <summary>Total elapsed milliseconds spent in tools.</summary>
        public double TotalToolElapsedMs { get; set; } = 0;
    }

    /// <summary>
    /// Tool run detail response.
    /// </summary>
    public sealed class ToolRunResponse
    {
        /// <summary>Tool run metadata.</summary>
        public ToolRun? ToolRun { get; set; } = null;
        /// <summary>Tool-call audit records associated with the run.</summary>
        public List<ToolExecutionRecord> ToolCalls { get; set; } = new List<ToolExecutionRecord>();
    }

    /// <summary>
    /// Tool approval request.
    /// </summary>
    public sealed class ToolApprovalRequest
    {
        /// <summary>Whether to approve execution.</summary>
        public bool Approved { get; set; } = false;
        /// <summary>Optional user-visible reason for denial.</summary>
        public string Reason { get; set; } = String.Empty;
        /// <summary>Approve later ask-gated tool calls in this same run.</summary>
        public bool AlwaysForRun { get; set; } = false;
    }

    /// <summary>
    /// Tool approval response.
    /// </summary>
    public sealed class ToolApprovalResponse
    {
        /// <summary>Updated tool-call audit record.</summary>
        public ToolExecutionRecord? ToolCall { get; set; } = null;
    }

    /// <summary>
    /// Generated Wilson tool system instructions.
    /// </summary>
    public sealed class ToolInstructionsResponse
    {
        /// <summary>Generated default tool instruction block.</summary>
        public string SystemPrompt { get; set; } = String.Empty;
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
