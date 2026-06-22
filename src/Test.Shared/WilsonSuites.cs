namespace Test.Shared
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Threading.Tasks;
    using Wilson.Core.Database;
    using Wilson.Core.Models;
    using Wilson.Core.Services;
    using Wilson.Core.Settings;

    /// <summary>
    /// Automated Wilson test suites.
    /// </summary>
    public static class WilsonSuites
    {
        /// <summary>
        /// Run all tests.
        /// </summary>
        public static async Task RunAllAsync()
        {
            await DatabaseRoundTripAsync().ConfigureAwait(false);
            IdLength();
            ContextTruncationAsync();
            HealthCheckDefaults();
            HealthStatusSnapshot();
        }

        private static async Task DatabaseRoundTripAsync()
        {
            string filename = Path.Combine(Path.GetTempPath(), "wilson-" + Guid.NewGuid().ToString("N") + ".db");
            DatabaseDriver database = new DatabaseDriver(new DatabaseSettings { Type = "Sqlite", Filename = filename });
            await database.InitializeAsync().ConfigureAwait(false);
            await database.SeedAsync(new SeedSettings { AccessKey = "test-token", UserEmail = "test@example.com" }).ConfigureAwait(false);
            List<Tenant> tenants = await database.GetTenantsAsync().ConfigureAwait(false);
            if (tenants.Count != 1) throw new InvalidOperationException("Expected one seeded tenant.");
            Credential? credential = await database.GetCredentialByAccessKeyAsync("test-token").ConfigureAwait(false);
            if (credential == null) throw new InvalidOperationException("Expected seeded credential.");
        }

        private static void ContextTruncationAsync()
        {
            InferenceService service = new InferenceService(new Settings());
            List<ChatMessage> messages = new List<ChatMessage>();
            for (int i = 0; i < 20; i++)
            {
                messages.Add(new ChatMessage { Role = "user", Content = new String('x', 400), TokenEstimate = 100 });
            }
            string prompt = service.BuildPrompt(messages, "hello", 512);
            if (!prompt.EndsWith("user: hello", StringComparison.Ordinal)) throw new InvalidOperationException("Prompt did not include the latest user message.");
            if (InferenceService.EstimateTokens(prompt) > 512) throw new InvalidOperationException("Prompt exceeded expected context budget.");
        }

        private static void IdLength()
        {
            List<string> ids = new List<string>
            {
                Wilson.Core.Helpers.IdGenerator.Tenant(),
                Wilson.Core.Helpers.IdGenerator.User(),
                Wilson.Core.Helpers.IdGenerator.Credential(),
                Wilson.Core.Helpers.IdGenerator.Conversation(),
                Wilson.Core.Helpers.IdGenerator.Message(),
                Wilson.Core.Helpers.IdGenerator.Feedback(),
                Wilson.Core.Helpers.IdGenerator.Request(),
                Wilson.Core.Helpers.IdGenerator.Session()
            };

            foreach (string id in ids)
            {
                if (id.Length > 32) throw new InvalidOperationException("Generated ID exceeds 32 characters: " + id);
            }
        }

        private static void HealthCheckDefaults()
        {
            ModelRunnerSettings ollama = new ModelRunnerSettings { ApiType = "Ollama", Endpoint = "http://localhost:11434" };
            ModelRunnerSettings.ApplyHealthCheckDefaults(ollama);
            if (!String.Equals(ollama.HealthCheckUrl, "http://localhost:11434/api/tags", StringComparison.Ordinal)) throw new InvalidOperationException("Unexpected Ollama health check URL.");
            if (ollama.HealthCheckIntervalMs != 5000 || ollama.HealthCheckTimeoutMs != 2000) throw new InvalidOperationException("Unexpected Ollama health check timing defaults.");

            ModelRunnerSettings openAi = new ModelRunnerSettings { ApiType = "OpenAI", Endpoint = "https://api.openai.com", ApiKey = "test-key" };
            ModelRunnerSettings.ApplyHealthCheckDefaults(openAi);
            if (!String.Equals(openAi.HealthCheckUrl, "https://api.openai.com/v1/models", StringComparison.Ordinal)) throw new InvalidOperationException("Unexpected OpenAI health check URL.");
            if (!openAi.HealthCheckUseAuth) throw new InvalidOperationException("Expected OpenAI health checks to use auth when an API key is configured.");
        }

        private static void HealthStatusSnapshot()
        {
            DateTime lastUnhealthy = DateTime.UtcNow.AddSeconds(-3);
            EndpointHealthState state = new EndpointHealthState
            {
                EndpointId = "runner-1",
                EndpointName = "Runner 1",
                IsHealthy = true,
                FirstCheckUtc = DateTime.UtcNow.AddSeconds(-2),
                LastUnhealthyUtc = lastUnhealthy,
                LastStateChangeUtc = DateTime.UtcNow.AddSeconds(-1),
                ConsecutiveSuccesses = 2
            };
            lock (state.HistoryLock)
            {
                state.CheckHistory.Add(new HealthCheckRecord { TimestampUtc = DateTime.UtcNow, Success = true });
            }

            EndpointHealthStatus status = EndpointHealthStatus.FromState(state);
            if (!status.IsHealthy) throw new InvalidOperationException("Expected health status to be healthy.");
            if (status.LastUnhealthyUtc != lastUnhealthy) throw new InvalidOperationException("Expected last unhealthy timestamp to be preserved.");
            if (status.UptimePercentage <= 0) throw new InvalidOperationException("Expected positive uptime percentage.");
            if (status.History.Count != 1) throw new InvalidOperationException("Expected health history snapshot.");
        }
    }
}
