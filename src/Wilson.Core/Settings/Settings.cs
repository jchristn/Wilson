namespace Wilson.Core.Settings
{
    using System;
    using System.Collections.Generic;

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
        /// API type. One of the values in <see cref="ApiTypes.All"/>.
        /// </summary>
        public string ApiType { get; set; } = ApiTypes.Ollama;

        /// <summary>
        /// Endpoint URL. Empty means "use the API type default" from the provider catalog.
        /// </summary>
        public string Endpoint { get; set; } = String.Empty;

        /// <summary>
        /// Optional API key or bearer token.
        /// </summary>
        public string? ApiKey { get; set; } = null;

        /// <summary>
        /// Deployment name for Azure OpenAI.
        /// </summary>
        public string? Deployment { get; set; } = null;

        /// <summary>
        /// API version for Azure OpenAI.
        /// </summary>
        public string? ApiVersion { get; set; } = null;

        /// <summary>
        /// Cloud region for Vertex AI and AWS Bedrock.
        /// </summary>
        public string? Region { get; set; } = null;

        /// <summary>
        /// Google Cloud project identifier for Vertex AI.
        /// </summary>
        public string? ProjectId { get; set; } = null;

        /// <summary>
        /// AWS secret access key for Bedrock when static credentials are used. The
        /// <see cref="ApiKey"/> property carries the AWS access key identifier.
        /// </summary>
        public string? ApiSecretKey { get; set; } = null;

        /// <summary>
        /// Optional AWS session token for temporary Bedrock credentials.
        /// </summary>
        public string? AwsSessionToken { get; set; } = null;

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
        /// Whether a value is an absolute HTTP or HTTPS URL Wilson can probe.
        /// </summary>
        /// <param name="value">Candidate URL.</param>
        /// <returns>True when the value is an absolute http or https URL.</returns>
        public static bool IsAbsoluteHttpUrl(string? value)
        {
            if (String.IsNullOrWhiteSpace(value)) return false;
            if (!Uri.TryCreate(value.Trim(), UriKind.Absolute, out Uri? uri)) return false;
            return String.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
                || String.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Whether a configured health check value is an explicitly supplied absolute URL with a
        /// non-HTTP scheme, such as <c>file:///tmp/probe</c> or <c>ftp://example.com/tags</c>. Such a
        /// value is a configuration mistake and is rejected rather than concatenated onto the
        /// endpoint. Endpoint-relative paths are NOT rejected: .NET parses <c>/api/tags</c> as an
        /// absolute <c>file</c> URI, so the check has to distinguish a real scheme from a leading
        /// slash path the operator meant to append to the endpoint.
        /// </summary>
        /// <param name="value">Candidate health check value.</param>
        /// <returns>True when the value is an absolute URL with a scheme other than http or https.</returns>
        public static bool IsExplicitNonHttpUrl(string? value)
        {
            if (String.IsNullOrWhiteSpace(value)) return false;
            string trimmed = value.Trim();

            // A leading slash is always an endpoint-relative path, never an explicit scheme, even
            // though Uri.TryCreate reports it as an absolute file URI.
            if (trimmed.StartsWith("/", StringComparison.Ordinal)) return false;
            if (trimmed.StartsWith("\\", StringComparison.Ordinal)) return false;

            if (!Uri.TryCreate(trimmed, UriKind.RelativeOrAbsolute, out Uri? uri)) return false;
            if (!uri.IsAbsoluteUri) return false;
            return !String.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
                && !String.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Resolve the absolute health probe URL for a runner, deriving it from the provider catalog
        /// defaults and endpoint when no absolute URL is configured. Returns an empty string when no
        /// absolute HTTP(S) URL can be produced, which callers must treat as "do not probe".
        /// </summary>
        /// <param name="runner">Model runner settings.</param>
        /// <returns>Absolute HTTP(S) probe URL, or an empty string when none can be built.</returns>
        public static string ResolveHealthCheckUrl(ModelRunnerSettings runner)
        {
            if (runner == null) throw new ArgumentNullException(nameof(runner));

            ApiTypeDescriptor descriptor = ApiTypes.Describe(runner.ApiType);
            string configured = (runner.HealthCheckUrl ?? String.Empty).Trim();
            string candidate;

            if (IsAbsoluteHttpUrl(configured))
            {
                candidate = configured;
            }
            else
            {
                // A configured value that is an explicitly supplied absolute URL with a non-http(s)
                // scheme is a mistake the operator has to fix; concatenating it onto the endpoint
                // would build a nonsense URL. Endpoint-relative paths such as '/api/tags' are NOT
                // rejected here: .NET reports those as absolute 'file' URIs, but they are meant to be
                // appended to the endpoint and are handled as paths below.
                if (IsExplicitNonHttpUrl(configured)) return String.Empty;

                // A provider with no usable health route (AWS Bedrock signs SDK calls, Vertex AI
                // derives a regional resource URL, VoyageAI has no models route) must never get a
                // derived probe, because Wilson would be inventing an endpoint that does not exist.
                // Only an operator-supplied absolute URL is probed for those providers.
                if (!descriptor.SupportsHealthChecks) return String.Empty;

                string baseUrl = (runner.Endpoint ?? String.Empty).Trim().TrimEnd('/');
                string path;
                if (String.IsNullOrWhiteSpace(configured))
                {
                    path = descriptor.DefaultHealthPath;
                }
                else if (configured.StartsWith("/", StringComparison.Ordinal))
                {
                    path = configured;
                }
                else
                {
                    path = "/" + configured.TrimStart('/');
                }

                candidate = String.IsNullOrWhiteSpace(path) ? baseUrl : baseUrl + path;
            }

            if (!IsAbsoluteHttpUrl(candidate)) return String.Empty;
            return candidate;
        }

        /// <summary>
        /// Build the URL Wilson actually sends for a health probe: the resolved health URL plus any
        /// query parameter the provider contract requires (Azure OpenAI's api-version).
        /// </summary>
        /// <param name="runner">Model runner settings.</param>
        /// <returns>Absolute HTTP(S) request URL, or an empty string when none can be built.</returns>
        public static string BuildHealthCheckRequestUrl(ModelRunnerSettings runner)
        {
            if (runner == null) throw new ArgumentNullException(nameof(runner));
            string url = ResolveHealthCheckUrl(runner);
            if (String.IsNullOrWhiteSpace(url)) return String.Empty;
            return AppendRequiredApiVersion(url, runner, ApiTypes.Describe(runner.ApiType));
        }

        /// <summary>
        /// Describe why a runner cannot be probed, for use before an HTTP send.
        /// </summary>
        /// <param name="runner">Model runner settings.</param>
        /// <returns>An actionable error message, or null when the runner has a usable probe URL.</returns>
        public static string? DescribeHealthCheckUrlProblem(ModelRunnerSettings runner)
        {
            if (runner == null) throw new ArgumentNullException(nameof(runner));
            if (!String.IsNullOrWhiteSpace(ResolveHealthCheckUrl(runner))) return null;

            ApiTypeDescriptor descriptor = ApiTypes.Describe(runner.ApiType);
            string configured = (runner.HealthCheckUrl ?? String.Empty).Trim();
            string endpoint = (runner.Endpoint ?? String.Empty).Trim();

            if (IsExplicitNonHttpUrl(configured))
            {
                return "health check URL '" + configured + "' must use the http or https scheme; no probe was sent";
            }

            if (!String.IsNullOrWhiteSpace(endpoint) && !IsAbsoluteHttpUrl(endpoint))
            {
                return "endpoint '" + endpoint + "' is not an absolute http or https URL, so the health check URL cannot be derived; no probe was sent";
            }

            if (!descriptor.SupportsHealthChecks)
            {
                return descriptor.DisplayName + " has no health or model-listing route Wilson can probe, so set the health URL to an absolute http or https URL to enable checks";
            }

            if (String.IsNullOrWhiteSpace(endpoint))
            {
                return "endpoint is empty, so the health check URL cannot be derived; set an absolute http or https endpoint or health check URL";
            }

            return "health check URL cannot be resolved to an absolute http or https URL; no probe was sent";
        }

        private static string AppendRequiredApiVersion(string url, ModelRunnerSettings runner, ApiTypeDescriptor descriptor)
        {
            if (!descriptor.HealthCheckRequiresApiVersion) return url;
            string apiVersion = String.IsNullOrWhiteSpace(runner.ApiVersion) ? descriptor.DefaultApiVersion : runner.ApiVersion!.Trim();
            if (String.IsNullOrWhiteSpace(apiVersion)) return url;
            if (url.IndexOf("api-version=", StringComparison.OrdinalIgnoreCase) >= 0) return url;

            string separator = url.IndexOf('?') >= 0 ? "&" : "?";
            return url + separator + "api-version=" + Uri.EscapeDataString(apiVersion);
        }

        /// <summary>
        /// Apply API-type-aware health check defaults.
        /// </summary>
        /// <param name="runner">Model runner settings.</param>
        public static void ApplyHealthCheckDefaults(ModelRunnerSettings runner)
        {
            if (runner == null) throw new ArgumentNullException(nameof(runner));

            ApiTypeDescriptor descriptor;

            // Provider defaults first so an empty endpoint is filled in before the health URL is derived.
            ApplyProviderDefaults(runner);
            descriptor = ApiTypes.Describe(runner.ApiType);

            // Resolve the probe URL once. A provider without a usable probe route (Bedrock signs AWS
            // SDK calls, Vertex AI derives a regional resource URL, VoyageAI has no models route) or
            // a runner whose endpoint is blank must not leave health checks enabled pointing at an
            // empty or relative URL, so checks default to off until a real URL exists. A runner the
            // operator explicitly disabled stays disabled.
            string resolvedUrl = ResolveHealthCheckUrl(runner);
            if (String.IsNullOrWhiteSpace(resolvedUrl))
            {
                // Keep whatever the operator typed so the failure stays actionable and visible, but
                // never leave checks enabled against a URL Wilson cannot send.
                string configuredUrl = (runner.HealthCheckUrl ?? String.Empty).Trim();
                runner.HealthCheckUrl = String.IsNullOrWhiteSpace(configuredUrl) ? null : configuredUrl;
                runner.HealthCheckEnabled = false;
            }
            else
            {
                runner.HealthCheckUrl = resolvedUrl;
            }

            if (runner.HealthCheckIntervalMs <= 0)
            {
                runner.HealthCheckIntervalMs = descriptor.DefaultHealthIntervalMs;
            }

            if (runner.HealthCheckTimeoutMs <= 0)
            {
                runner.HealthCheckTimeoutMs = descriptor.DefaultHealthTimeoutMs;
            }

            if (runner.HealthCheckExpectedStatusCode < 100 || runner.HealthCheckExpectedStatusCode > 599)
            {
                runner.HealthCheckExpectedStatusCode = 200;
            }

            if (runner.HealthyThreshold <= 0) runner.HealthyThreshold = 2;
            if (runner.UnhealthyThreshold <= 0) runner.UnhealthyThreshold = 2;

            if (descriptor.RequiresApiKey && !String.IsNullOrWhiteSpace(runner.ApiKey))
            {
                runner.HealthCheckUseAuth = true;
            }

            ApplyToolDefaults(runner);
        }

        /// <summary>
        /// Apply provider-specific endpoint and credential defaults for the configured API type.
        /// </summary>
        /// <param name="runner">Model runner settings.</param>
        public static void ApplyProviderDefaults(ModelRunnerSettings runner)
        {
            if (runner == null) throw new ArgumentNullException(nameof(runner));

            ApiTypeDescriptor descriptor = ApiTypes.Describe(runner.ApiType);

            if (String.IsNullOrWhiteSpace(runner.Endpoint) && !String.IsNullOrWhiteSpace(descriptor.DefaultEndpoint))
            {
                runner.Endpoint = descriptor.DefaultEndpoint;
            }

            if (descriptor.RequiresApiVersion && String.IsNullOrWhiteSpace(runner.ApiVersion))
            {
                runner.ApiVersion = descriptor.DefaultApiVersion;
            }

            if (descriptor.RequiresRegion && String.IsNullOrWhiteSpace(runner.Region))
            {
                runner.Region = descriptor.DefaultRegion;
            }

            runner.ApiType = ApiTypes.Normalize(runner.ApiType);
        }

        /// <summary>
        /// Apply API-type-aware tool-call defaults.
        /// </summary>
        /// <param name="runner">Model runner settings.</param>
        public static void ApplyToolDefaults(ModelRunnerSettings runner)
        {
            if (runner == null) throw new ArgumentNullException(nameof(runner));

            // An unrecognized API type is treated as a legacy configuration. It keeps chatting through
            // the Ollama-family clients but never advertises tool support, because Wilson cannot know
            // what tool wire format the endpoint speaks.
            if (!ApiTypes.IsSupported(runner.ApiType))
            {
                runner.ToolCallingApiFormat = String.Empty;
                runner.ChatCompletionsPath = String.Empty;
                runner.SupportsTools = false;
                return;
            }

            ApiTypeDescriptor descriptor = ApiTypes.Describe(runner.ApiType);

            if (String.IsNullOrWhiteSpace(runner.ToolCallingApiFormat))
            {
                runner.ToolCallingApiFormat = descriptor.DefaultToolCallingApiFormat;
            }
            else if (descriptor.RequiresDeployment
                && String.Equals(runner.ToolCallingApiFormat, ToolCallingApiFormats.OpenAIChatCompletions, StringComparison.OrdinalIgnoreCase))
            {
                // Azure OpenAI is deployment-scoped and authenticates with an api-key header, so the
                // generic chat-completions sender cannot build a valid request for it. Route it through
                // the provider-native PolyPrompt client even when an older draft pinned the generic format.
                runner.ToolCallingApiFormat = ToolCallingApiFormats.Native;
            }

            if (String.IsNullOrWhiteSpace(runner.ChatCompletionsPath) && !String.IsNullOrWhiteSpace(descriptor.DefaultChatCompletionsPath))
            {
                runner.ChatCompletionsPath = descriptor.DefaultChatCompletionsPath;
            }

            if (!descriptor.SupportsToolCalls)
            {
                runner.SupportsTools = false;
            }
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
