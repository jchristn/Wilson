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
