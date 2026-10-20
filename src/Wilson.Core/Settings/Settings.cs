namespace Wilson.Core.Settings
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>
    /// Wilson server settings.
    /// </summary>
    public class Settings
    {
        /// <summary>
        /// UTC creation timestamp.
        /// </summary>
        public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// REST listener settings.
        /// </summary>
        public RestSettings Rest { get; set; } = new RestSettings();

        /// <summary>
        /// Database settings.
        /// </summary>
        public DatabaseSettings Database { get; set; } = new DatabaseSettings();

        /// <summary>
        /// CORS settings.
        /// </summary>
        public CorsSettings Cors { get; set; } = new CorsSettings();

        /// <summary>
        /// Authentication settings.
        /// </summary>
        public AuthSettings Auth { get; set; } = new AuthSettings();

        /// <summary>
        /// Request history settings.
        /// </summary>
        public RequestHistorySettings RequestHistory { get; set; } = new RequestHistorySettings();

        /// <summary>
        /// Tool execution settings.
        /// </summary>
        public ToolsSettings Tools { get; set; } = new ToolsSettings();

        /// <summary>
        /// Model runner definitions.
        /// </summary>
        public List<ModelRunnerSettings> ModelRunners { get; set; } = new List<ModelRunnerSettings>();

        /// <summary>
        /// First-run seed records.
        /// </summary>
        public SeedSettings Seed { get; set; } = new SeedSettings();

        /// <summary>
        /// Observability (metrics, traces, logs) settings.
        /// </summary>
        public TelemetrySettings Telemetry { get; set; } = new TelemetrySettings();
    }

    /// <summary>
    /// Observability settings controlling the Radiant/OpenTelemetry pipeline.
    /// </summary>
    public class TelemetrySettings
    {
        /// <summary>
        /// Master switch. When false the telemetry pipeline is inert and every emit is a no-op.
        /// </summary>
        public bool Enabled { get; set; } = false;

        /// <summary>
        /// Service name reported as <c>service.name</c> and used as the default meter/activity-source name.
        /// </summary>
        public string ServiceName { get; set; } = "wilson-server";

        /// <summary>
        /// OTLP endpoint. Point at the OpenTelemetry Collector (e.g. http://otel-collector:4317 in Docker).
        /// </summary>
        public string OtlpEndpoint { get; set; } = "http://localhost:4317";

        /// <summary>
        /// OTLP wire protocol, either "grpc" (default) or "httpprotobuf".
        /// </summary>
        public string OtlpProtocol { get; set; } = "grpc";

        /// <summary>
        /// Head trace sampling ratio in the range 0..1. 1.0 samples everything.
        /// </summary>
        public double TracesSamplingRatio { get; set; } = 1.0;

        /// <summary>
        /// Emit metrics.
        /// </summary>
        public bool MetricsEnabled { get; set; } = true;

        /// <summary>
        /// Emit traces.
        /// </summary>
        public bool TracesEnabled { get; set; } = true;

        /// <summary>
        /// Emit logs.
        /// </summary>
        public bool LogsEnabled { get; set; } = true;

        /// <summary>
        /// Minimum log severity to emit, e.g. Trace, Debug, Information, Warning, Error, Critical, None.
        /// </summary>
        public string LogMinimumSeverity { get; set; } = "Information";

        /// <summary>
        /// Include process metrics (memory, uptime, threads).
        /// </summary>
        public bool IncludeProcessMetrics { get; set; } = true;

        /// <summary>
        /// Include .NET runtime metrics (GC, thread pool, JIT).
        /// </summary>
        public bool IncludeRuntimeMetrics { get; set; } = true;

        /// <summary>
        /// Enable an in-process Prometheus scrape endpoint (for local debugging without a collector).
        /// </summary>
        public bool PrometheusScrapeEnabled { get; set; } = false;

        /// <summary>
        /// Port for the in-process Prometheus scrape endpoint when enabled.
        /// </summary>
        public int PrometheusScrapePort { get; set; } = 9464;

        /// <summary>
        /// Enforce strict metric label policy (throws on undeclared labels in Debug).
        /// </summary>
        public bool MetricCatalogStrict { get; set; } = false;
    }

    /// <summary>
    /// REST listener settings.
    /// </summary>
    public class RestSettings
    {
        /// <summary>
        /// Hostname. Default is 127.0.0.1.
        /// </summary>
        public string Hostname { get; set; } = "127.0.0.1";

        /// <summary>
        /// Port. Default is 9400.
        /// </summary>
        public int Port { get; set; } = 9400;

        /// <summary>
        /// Enable TLS.
        /// </summary>
        public bool Ssl { get; set; } = false;
    }

    /// <summary>
    /// Database settings.
    /// </summary>
    public class DatabaseSettings
    {
        /// <summary>
        /// Database type, either Sqlite or Postgres.
        /// </summary>
        public string Type { get; set; } = "Sqlite";

        /// <summary>
        /// SQLite filename.
        /// </summary>
        public string Filename { get; set; } = "data/wilson.db";

        /// <summary>
        /// PostgreSQL connection string.
        /// </summary>
        public string ConnectionString { get; set; } = "Host=localhost;Port=5432;Database=wilson;Username=wilson;Password=wilson";
    }

    /// <summary>
    /// CORS settings.
    /// </summary>
    public class CorsSettings
    {
        /// <summary>
        /// Enable CORS.
        /// </summary>
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// Allowed origins.
        /// </summary>
        public List<string> AllowedOrigins { get; set; } = new List<string> { "*" };

        /// <summary>
        /// Allowed methods.
        /// </summary>
        public List<string> AllowedMethods { get; set; } = new List<string> { "GET", "POST", "PUT", "DELETE", "OPTIONS", "HEAD" };

        /// <summary>
        /// Allowed headers.
        /// </summary>
        public List<string> AllowedHeaders { get; set; } = new List<string> { "Content-Type", "Authorization", "X-Api-Key", "X-Tenant-Guid" };
    }

    /// <summary>
    /// Authentication settings.
    /// </summary>
    public class AuthSettings
    {
        /// <summary>
        /// Global administrator bearer tokens.
        /// </summary>
        public List<string> AdminBearerTokens { get; set; } = new List<string> { "wilson-admin-dev-token" };

        /// <summary>
        /// Session lifetime in hours. Default 24.
        /// </summary>
        public int SessionHours { get; set; } = 24;
    }

    /// <summary>
    /// Request history capture settings.
    /// </summary>
    public class RequestHistorySettings
    {
        /// <summary>
        /// Enable request history.
        /// </summary>
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// Retention in days.
        /// </summary>
        public int RetentionDays { get; set; } = 30;
    }

    /// <summary>
    /// Health check HTTP method.
    /// </summary>
    public enum HealthCheckMethodEnum
    {
        /// <summary>
        /// HTTP GET method.
        /// </summary>
        GET = 0,

        /// <summary>
        /// HTTP HEAD method.
        /// </summary>
        HEAD = 1
    }

    /// <summary>
    /// Model runner settings.
    /// </summary>
    public class ModelRunnerSettings
    {
        /// <summary>
        /// Runner identifier.
        /// </summary>
        public string Id { get; set; } = String.Empty;

        /// <summary>
        /// Display name.
        /// </summary>
        public string Name { get; set; } = String.Empty;

        /// <summary>
        /// API type. See <see cref="ApiTypes"/> for supported values; unsupported values preserve
        /// legacy behavior with tools disabled.
        /// </summary>
        public string ApiType { get; set; } = ApiTypes.Default;

        /// <summary>
        /// Endpoint URL. Blank values fall back to the API type default endpoint.
        /// </summary>
        public string Endpoint { get; set; } = "http://localhost:11434";

        /// <summary>
        /// Optional API key.
        /// </summary>
        public string? ApiKey { get; set; } = null;

        /// <summary>
        /// Azure OpenAI deployment name. Used as the model when a request does not specify one.
        /// </summary>
        public string? Deployment { get; set; } = null;

        /// <summary>
        /// Provider API version for versioned endpoints such as Azure OpenAI.
        /// </summary>
        public string? ApiVersion { get; set; } = null;

        /// <summary>
        /// Cloud region for region-scoped providers such as Vertex AI and AWS Bedrock, in the provider's own
        /// identifier style. When blank, Vertex AI uses <c>GOOGLE_CLOUD_REGION</c>/<c>GOOGLE_CLOUD_LOCATION</c> and
        /// then <see cref="ApiTypes.DefaultVertexRegion"/>, while AWS Bedrock uses
        /// <c>AWS_REGION</c>/<c>AWS_DEFAULT_REGION</c> and then <see cref="ApiTypes.DefaultBedrockRegion"/>.
        /// </summary>
        public string? Region { get; set; } = null;

        /// <summary>
        /// Google Cloud project identifier for Vertex AI.
        /// </summary>
        public string? ProjectId { get; set; } = null;

        /// <summary>
        /// Optional known model list.
        /// </summary>
        public List<string> Models { get; set; } = new List<string>();

        /// <summary>
        /// Context window for truncation.
        /// </summary>
        public int ContextWindowTokens { get; set; } = 8192;

        /// <summary>
        /// Whether this runner can be used with tool-enabled requests when global tools are enabled.
        /// </summary>
        public bool ToolsEnabled { get; set; } = true;

        /// <summary>
        /// Whether this runner supports model tool calls.
        /// </summary>
        public bool SupportsTools { get; set; } = true;

        /// <summary>
        /// Tool-calling API format, such as OpenAIChatCompletions or OllamaChat.
        /// </summary>
        public string ToolCallingApiFormat { get; set; } = String.Empty;

        /// <summary>
        /// Whether this runner supports parallel tool calls.
        /// </summary>
        public bool SupportsParallelToolCalls { get; set; } = true;

        /// <summary>
        /// Whether this runner supports streaming tool-call deltas.
        /// </summary>
        public bool SupportsStreamingToolCalls { get; set; } = true;

        /// <summary>
        /// Chat-completions path for OpenAI-compatible tool-capable transports.
        /// </summary>
        public string ChatCompletionsPath { get; set; } = String.Empty;

        /// <summary>
        /// Enable periodic health checks for this model server.
        /// </summary>
        public bool HealthCheckEnabled { get; set; } = true;

        /// <summary>
        /// Absolute URL or path to probe for model server health. Empty uses API-type defaults.
        /// </summary>
        public string? HealthCheckUrl { get; set; } = null;

        /// <summary>
        /// HTTP method used for health checks.
        /// </summary>
        public HealthCheckMethodEnum HealthCheckMethod { get; set; } = HealthCheckMethodEnum.GET;

        /// <summary>
        /// Milliseconds between health checks.
        /// </summary>
        public int HealthCheckIntervalMs { get; set; } = 0;

        /// <summary>
        /// Per-check timeout in milliseconds.
        /// </summary>
        public int HealthCheckTimeoutMs { get; set; } = 0;

        /// <summary>
        /// Expected HTTP status code for a healthy response.
        /// </summary>
        public int HealthCheckExpectedStatusCode { get; set; } = 200;

        /// <summary>
        /// Consecutive successes required to transition to healthy.
        /// </summary>
        public int HealthyThreshold { get; set; } = 2;

        /// <summary>
        /// Consecutive failures required to transition to unhealthy.
        /// </summary>
        public int UnhealthyThreshold { get; set; } = 2;

        /// <summary>
        /// Send the configured API key with health check requests.
        /// </summary>
        public bool HealthCheckUseAuth { get; set; } = false;

        /// <summary>
        /// Apply API-type-aware health check defaults.
        /// </summary>
        /// <param name="runner">Model runner settings.</param>
        public static void ApplyHealthCheckDefaults(ModelRunnerSettings runner)
        {
            if (runner == null) throw new ArgumentNullException(nameof(runner));

            ApiTypeDescriptor? descriptor = ApiTypes.Describe(runner.ApiType);
            bool ollamaLike = IsOllamaApiType(runner);

            // Ollama Cloud cannot be served from a loopback endpoint. Switching apiType without
            // editing the endpoint should still produce a working configuration.
            if (String.Equals(runner.ApiType, ApiTypes.OllamaCloud, StringComparison.OrdinalIgnoreCase) && IsLoopbackEndpoint(runner.Endpoint) && descriptor != null)
            {
                runner.Endpoint = descriptor.DefaultEndpoint;
            }

            if (String.IsNullOrWhiteSpace(runner.Endpoint) && descriptor != null && !String.IsNullOrWhiteSpace(descriptor.DefaultEndpoint))
            {
                runner.Endpoint = descriptor.DefaultEndpoint;
            }

            if (String.IsNullOrWhiteSpace(runner.HealthCheckUrl) && !String.IsNullOrWhiteSpace(runner.Endpoint))
            {
                string baseUrl = (runner.Endpoint ?? String.Empty).TrimEnd('/');
                string path = descriptor == null ? "/v1/models" : descriptor.DefaultHealthPath;
                if (!String.IsNullOrWhiteSpace(path)) runner.HealthCheckUrl = baseUrl + path;
            }

            if (String.IsNullOrWhiteSpace(runner.HealthCheckUrl)) runner.HealthCheckEnabled = false;

            // Resource-derived cloud endpoints have no generic models URL to probe.
            // Preserve an explicitly configured absolute HTTP(S) probe.
            if (descriptor?.UsesCloudResourceEndpoint == true && String.IsNullOrWhiteSpace(runner.Endpoint)
                && (!Uri.TryCreate(runner.HealthCheckUrl, UriKind.Absolute, out Uri? probe)
                    || (probe.Scheme != Uri.UriSchemeHttp && probe.Scheme != Uri.UriSchemeHttps)))
                runner.HealthCheckEnabled = false;

            if (runner.HealthCheckIntervalMs <= 0)
            {
                runner.HealthCheckIntervalMs = ollamaLike ? 5000 : 15000;
            }

            if (runner.HealthCheckTimeoutMs <= 0)
            {
                runner.HealthCheckTimeoutMs = ollamaLike ? 2000 : 5000;
            }

            if (runner.HealthCheckExpectedStatusCode < 100 || runner.HealthCheckExpectedStatusCode > 599)
            {
                runner.HealthCheckExpectedStatusCode = 200;
            }

            if (runner.HealthyThreshold <= 0) runner.HealthyThreshold = 2;
            if (runner.UnhealthyThreshold <= 0) runner.UnhealthyThreshold = 2;

            if ((!ollamaLike || descriptor?.RequiresApiKey == true) && !String.IsNullOrWhiteSpace(runner.ApiKey))
            {
                runner.HealthCheckUseAuth = true;
            }

            ApplyToolDefaults(runner);
        }

        /// <summary>
        /// Apply API-type-aware tool-call defaults.
        /// </summary>
        /// <param name="runner">Model runner settings.</param>
        public static void ApplyToolDefaults(ModelRunnerSettings runner)
        {
            if (runner == null) throw new ArgumentNullException(nameof(runner));

            ApiTypeDescriptor? descriptor = ApiTypes.Describe(runner.ApiType);

            if (descriptor == null)
            {
                // Legacy behavior: an unrecognized API type keeps chat available but is not
                // assumed to speak any tool-call wire format.
                runner.SupportsTools = false;
                return;
            }

            if (String.IsNullOrWhiteSpace(runner.ToolCallingApiFormat))
            {
                runner.ToolCallingApiFormat = descriptor.DefaultToolCallingApiFormat;
            }

            if (String.IsNullOrWhiteSpace(runner.ChatCompletionsPath) && !String.IsNullOrWhiteSpace(descriptor.DefaultChatCompletionsPath))
            {
                runner.ChatCompletionsPath = descriptor.DefaultChatCompletionsPath;
            }

            if (!descriptor.SupportsTools)
            {
                runner.SupportsTools = false;
            }

            if (!descriptor.SupportsParallelToolCalls)
            {
                runner.SupportsParallelToolCalls = false;
            }

            if (!descriptor.SupportsStreamingToolCalls)
            {
                runner.SupportsStreamingToolCalls = false;
            }

            // Providers that only expose a provider-native tool protocol also need a model
            // inventory for chat; keep the configured list when it is present.
            if (String.IsNullOrWhiteSpace(runner.Deployment) && String.Equals(descriptor.Name, ApiTypes.AzureOpenAI, StringComparison.OrdinalIgnoreCase))
            {
                runner.Deployment = runner.Models.FirstOrDefault() ?? String.Empty;
            }
        }

        /// <summary>
        /// Determine whether a runner uses an Ollama-family API.
        /// </summary>
        /// <param name="runner">Model runner settings.</param>
        /// <returns>True for Ollama and Ollama Cloud runners.</returns>
        public static bool IsOllamaApiType(ModelRunnerSettings runner)
        {
            if (runner == null) throw new ArgumentNullException(nameof(runner));
            return String.Equals(runner.ApiType, ApiTypes.Ollama, StringComparison.OrdinalIgnoreCase)
                || String.Equals(runner.ApiType, ApiTypes.OllamaCloud, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Determine whether a runner uses an OpenAI-family chat-completions API.
        /// </summary>
        /// <param name="runner">Model runner settings.</param>
        /// <returns>True for OpenAI and OpenAI-compatible runners.</returns>
        public static bool IsOpenAiCompatibleApiType(ModelRunnerSettings runner)
        {
            if (runner == null) throw new ArgumentNullException(nameof(runner));
            return String.Equals(runner.ApiType, ApiTypes.OpenAI, StringComparison.OrdinalIgnoreCase)
                || String.Equals(runner.ApiType, ApiTypes.OpenAICompatible, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Resolve the effective endpoint for a runner, applying API type defaults.
        /// </summary>
        /// <param name="runner">Model runner settings.</param>
        /// <returns>Endpoint URL, or an empty string when the runner has none.</returns>
        public static string ResolveEndpoint(ModelRunnerSettings runner)
        {
            if (runner == null) throw new ArgumentNullException(nameof(runner));
            string endpoint = (runner.Endpoint ?? String.Empty).Trim();
            if (!String.IsNullOrWhiteSpace(endpoint)) return endpoint;

            ApiTypeDescriptor? descriptor = ApiTypes.Describe(runner.ApiType);
            return descriptor?.DefaultEndpoint ?? String.Empty;
        }

        private static bool IsLoopbackEndpoint(string? endpoint)
        {
            if (String.IsNullOrWhiteSpace(endpoint)) return true;
            if (!Uri.TryCreate(endpoint.Trim(), UriKind.Absolute, out Uri? uri)) return false;
            return uri.IsLoopback || String.Equals(uri.Host, "localhost", StringComparison.OrdinalIgnoreCase) || String.Equals(uri.Host, "host.docker.internal", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsOllama(ModelRunnerSettings runner)
        {
            return IsOllamaApiType(runner);
        }
    }

    /// <summary>
    /// Seed settings.
    /// </summary>
    public class SeedSettings
    {
        /// <summary>
        /// Default tenant name.
        /// </summary>
        public string TenantName { get; set; } = "Default Tenant";

        /// <summary>
        /// Default user email.
        /// </summary>
        public string UserEmail { get; set; } = "admin@wilson.local";

        /// <summary>
        /// Default user first name.
        /// </summary>
        public string FirstName { get; set; } = "Admin";

        /// <summary>
        /// Default user last name.
        /// </summary>
        public string LastName { get; set; } = "User";

        /// <summary>
        /// Default access key.
        /// </summary>
        public string AccessKey { get; set; } = "wilsonadmin";
    }
}
