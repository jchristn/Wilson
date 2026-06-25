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
        /// API type, Ollama or OpenAI.
        /// </summary>
        public string ApiType { get; set; } = "Ollama";

        /// <summary>
        /// Endpoint URL.
        /// </summary>
        public string Endpoint { get; set; } = "http://localhost:11434";

        /// <summary>
        /// Optional API key.
        /// </summary>
        public string? ApiKey { get; set; } = null;

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
        public bool SupportsParallelToolCalls { get; set; } = false;

        /// <summary>
        /// Whether this runner supports streaming tool-call deltas.
        /// </summary>
        public bool SupportsStreamingToolCalls { get; set; } = false;

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

            if (String.IsNullOrWhiteSpace(runner.HealthCheckUrl))
            {
                string baseUrl = (runner.Endpoint ?? String.Empty).TrimEnd('/');
                runner.HealthCheckUrl = IsOllama(runner) ? baseUrl + "/api/tags" : baseUrl + "/v1/models";
            }

            if (runner.HealthCheckIntervalMs <= 0)
            {
                runner.HealthCheckIntervalMs = IsOllama(runner) ? 5000 : 15000;
            }

            if (runner.HealthCheckTimeoutMs <= 0)
            {
                runner.HealthCheckTimeoutMs = IsOllama(runner) ? 2000 : 5000;
            }

            if (runner.HealthCheckExpectedStatusCode < 100 || runner.HealthCheckExpectedStatusCode > 599)
            {
                runner.HealthCheckExpectedStatusCode = 200;
            }

            if (runner.HealthyThreshold <= 0) runner.HealthyThreshold = 2;
            if (runner.UnhealthyThreshold <= 0) runner.UnhealthyThreshold = 2;

            if (!IsOllama(runner) && !String.IsNullOrWhiteSpace(runner.ApiKey))
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

            string apiType = runner.ApiType ?? String.Empty;
            bool ollama = IsOllama(runner);
            bool openAiLike = String.Equals(apiType, "OpenAI", StringComparison.OrdinalIgnoreCase)
                || String.Equals(apiType, "OpenAICompatible", StringComparison.OrdinalIgnoreCase);

            if (String.IsNullOrWhiteSpace(runner.ToolCallingApiFormat))
            {
                runner.ToolCallingApiFormat = ollama ? "OllamaChat" : openAiLike ? "OpenAIChatCompletions" : String.Empty;
            }

            if (String.IsNullOrWhiteSpace(runner.ChatCompletionsPath) && openAiLike)
            {
                runner.ChatCompletionsPath = "/v1/chat/completions";
            }

            if (!openAiLike && !ollama)
            {
                runner.SupportsTools = false;
            }
        }

        private static bool IsOllama(ModelRunnerSettings runner)
        {
            return String.Equals(runner.ApiType, "Ollama", StringComparison.OrdinalIgnoreCase);
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
