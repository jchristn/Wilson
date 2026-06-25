namespace Test.Shared
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using Wilson.Core.Database;
    using Wilson.Core.Models;
    using Wilson.Core.Services;
    using Wilson.Core.Settings;
    using Wilson.Core.Tools;

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
            ToolSettingsDefaults();
            await ToolServiceFoundationAsync().ConfigureAwait(false);
            ToolCapableInferenceParsing();
            await ToolAgentLoopAsync().ConfigureAwait(false);
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
                Wilson.Core.Helpers.IdGenerator.ToolRun(),
                Wilson.Core.Helpers.IdGenerator.ToolCall(),
                Wilson.Core.Helpers.IdGenerator.ToolExecution(),
                Wilson.Core.Helpers.IdGenerator.Session()
            };

            foreach (string id in ids)
            {
                if (id.Length > 32) throw new InvalidOperationException("Generated ID exceeds 32 characters: " + id);
            }
        }

        private static void ToolSettingsDefaults()
        {
            ToolsSettings tools = new ToolsSettings();
            if (tools.Enabled) throw new InvalidOperationException("Tools should be disabled by default.");
            if (!tools.BuiltInsEnabled) throw new InvalidOperationException("Built-in tools should be enabled for catalog resolution by default.");
            if (!String.Equals(tools.DefaultApprovalPolicy, ToolApprovalPolicies.Ask, StringComparison.Ordinal)) throw new InvalidOperationException("Unexpected default tool approval policy.");
            if (tools.MaxAgentIterations != 25 || tools.MaxToolCallsPerTurn != 12) throw new InvalidOperationException("Unexpected default tool limits.");

            ModelRunnerSettings ollama = new ModelRunnerSettings { ApiType = "Ollama", Endpoint = "http://localhost:11434" };
            ModelRunnerSettings.ApplyToolDefaults(ollama);
            if (!ollama.SupportsTools || !String.Equals(ollama.ToolCallingApiFormat, "OllamaChat", StringComparison.Ordinal)) throw new InvalidOperationException("Unexpected Ollama tool defaults.");

            ModelRunnerSettings openAi = new ModelRunnerSettings { ApiType = "OpenAI", Endpoint = "https://api.openai.com" };
            ModelRunnerSettings.ApplyToolDefaults(openAi);
            if (!openAi.SupportsTools || !String.Equals(openAi.ToolCallingApiFormat, "OpenAIChatCompletions", StringComparison.Ordinal)) throw new InvalidOperationException("Unexpected OpenAI tool defaults.");
            if (!String.Equals(openAi.ChatCompletionsPath, "/v1/chat/completions", StringComparison.Ordinal)) throw new InvalidOperationException("Unexpected OpenAI chat completions path.");

            ModelRunnerSettings unsupported = new ModelRunnerSettings { ApiType = "Custom", Endpoint = "http://localhost:9999" };
            ModelRunnerSettings.ApplyToolDefaults(unsupported);
            if (unsupported.SupportsTools) throw new InvalidOperationException("Unsupported API types should not default to tool support.");
        }

        private static async Task ToolServiceFoundationAsync()
        {
            Settings disabled = new Settings();
            ToolService disabledService = new ToolService(disabled);
            if (disabledService.ListTools(false).Count != 0) throw new InvalidOperationException("Disabled tool service should not expose model tools.");
            if (disabledService.ListTools(true).Count == 0) throw new InvalidOperationException("Disabled tool service should expose diagnostic descriptors.");
            if (disabledService.GetModelToolDefinitions().Count != 0) throw new InvalidOperationException("Foundation tool service should not expose model tool definitions.");

            Settings enabled = new Settings { Tools = new ToolsSettings { Enabled = true } };
            ToolService enabledService = new ToolService(enabled);
            if (enabledService.ListTools(false).Count != 0) throw new InvalidOperationException("Tool service should not expose file tools without a working directory and allowed root.");
            ToolDescriptor? unavailableRead = enabledService.GetTool("read_file");
            if (unavailableRead == null || unavailableRead.Available) throw new InvalidOperationException("Expected read_file to be unavailable until workspace settings are configured.");

            string workspace = Path.Combine(Path.GetTempPath(), "wilson-tools-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(workspace);
            try
            {
                string filename = Path.Combine(workspace, "sample.txt");
                await File.WriteAllTextAsync(filename, "hello" + Environment.NewLine + "world").ConfigureAwait(false);
                await File.WriteAllTextAsync(Path.Combine(workspace, ".env"), "SECRET=value").ConfigureAwait(false);

                Settings configured = new Settings
                {
                    Tools = new ToolsSettings
                    {
                        Enabled = true,
                        WorkingDirectory = workspace,
                        AllowedRoots = new List<string> { workspace }
                    }
                };
                ToolService configuredService = new ToolService(configured);
                if (!configuredService.ListTools(false).Exists(tool => String.Equals(tool.Name, "read_file", StringComparison.OrdinalIgnoreCase))) throw new InvalidOperationException("Expected read_file to be available with workspace settings.");
                if (configuredService.GetModelToolDefinitions().Count == 0) throw new InvalidOperationException("Expected model tool definitions for available tools.");

                using JsonDocument readArgs = JsonDocument.Parse("""{"file_path":"sample.txt","offset":1,"limit":1}""");
                ToolResult readResult = await configuredService.ExecuteAsync(
                    Wilson.Core.Helpers.IdGenerator.ToolCall(),
                    "read_file",
                    readArgs.RootElement,
                    new ToolExecutionContext(),
                    CancellationToken.None).ConfigureAwait(false);
                if (!readResult.Success || !readResult.Content.Contains("hello", StringComparison.Ordinal)) throw new InvalidOperationException("Expected read_file to return file content.");

                using JsonDocument secretArgs = JsonDocument.Parse("""{"file_path":".env"}""");
                ToolResult secretResult = await configuredService.ExecuteAsync(
                    Wilson.Core.Helpers.IdGenerator.ToolCall(),
                    "read_file",
                    secretArgs.RootElement,
                    new ToolExecutionContext(),
                    CancellationToken.None).ConfigureAwait(false);
                if (secretResult.Success || !String.Equals(secretResult.ErrorCode, "secret_path_blocked", StringComparison.Ordinal)) throw new InvalidOperationException("Expected secret path guard to block .env reads.");
            }
            finally
            {
                Directory.Delete(workspace, true);
            }
        }

        private static void ToolCapableInferenceParsing()
        {
            string openAiResponse = """
{
  "choices": [
    {
      "finish_reason": "tool_calls",
      "message": {
        "role": "assistant",
        "content": null,
        "tool_calls": [
          {
            "id": "call_1",
            "type": "function",
            "function": {
              "name": "read_file",
              "arguments": "{\"file_path\":\"sample.txt\"}"
            }
          }
        ]
      }
    }
  ]
}
""";

            ToolCapableInferenceResponse openAi = InferenceService.ParseToolCapableResponse("OpenAIChatCompletions", openAiResponse);
            if (!openAi.Success || openAi.ToolCalls.Count != 1) throw new InvalidOperationException("Expected one OpenAI-compatible tool call.");
            if (!String.Equals(openAi.ToolCalls[0].Id, "call_1", StringComparison.Ordinal)) throw new InvalidOperationException("Expected provider tool-call ID to be preserved.");
            if (!openAi.ToolCalls[0].Function!.Arguments.Contains("sample.txt", StringComparison.Ordinal)) throw new InvalidOperationException("Expected OpenAI tool-call arguments to be preserved.");

            string ollamaResponse = """
{
  "message": {
    "role": "assistant",
    "content": "",
    "tool_calls": [
      {
        "type": "function",
        "function": {
          "name": "read_file",
          "arguments": {
            "file_path": "sample.txt"
          }
        }
      }
    ]
  }
}
""";

            ToolCapableInferenceResponse ollama = InferenceService.ParseToolCapableResponse("OllamaChat", ollamaResponse);
            if (!ollama.Success || ollama.ToolCalls.Count != 1) throw new InvalidOperationException("Expected one Ollama tool call.");
            if (!String.Equals(ollama.FinishReason, "tool_calls", StringComparison.Ordinal)) throw new InvalidOperationException("Expected missing Ollama finish reason to normalize to tool_calls.");
            if (!ollama.ToolCalls[0].Function!.Arguments.Contains("file_path", StringComparison.Ordinal)) throw new InvalidOperationException("Expected raw Ollama object arguments to be preserved.");
        }

        private static async Task ToolAgentLoopAsync()
        {
            string workspace = Path.Combine(Path.GetTempPath(), "wilson-agent-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(workspace);
            try
            {
                await File.WriteAllTextAsync(Path.Combine(workspace, "sample.txt"), "agent loop content").ConfigureAwait(false);
                Settings settings = new Settings
                {
                    Tools = new ToolsSettings
                    {
                        Enabled = true,
                        WorkingDirectory = workspace,
                        AllowedRoots = new List<string> { workspace },
                        MaxToolIterations = 4,
                        MaxToolCallsPerTurn = 4
                    }
                };
                ToolService toolService = new ToolService(settings);
                int modelCalls = 0;
                ToolAgentService agent = new ToolAgentService(
                    toolService,
                    (runner, request, token) =>
                    {
                        modelCalls++;
                        if (modelCalls == 1)
                        {
                            return Task.FromResult(new ToolCapableInferenceResponse
                            {
                                Success = true,
                                FinishReason = "tool_calls",
                                ToolCalls = new List<ModelToolCall>
                                {
                                    new ModelToolCall
                                    {
                                        Id = "call_read",
                                        Function = new ModelToolFunctionCall
                                        {
                                            Name = "read_file",
                                            Arguments = "{\"file_path\":\"sample.txt\"}"
                                        }
                                    }
                                }
                            });
                        }

                        if (!request.Messages.Exists(message => String.Equals(message.Role, "tool", StringComparison.OrdinalIgnoreCase) && (message.Content ?? String.Empty).Contains("agent loop content", StringComparison.Ordinal)))
                            throw new InvalidOperationException("Expected tool result message before final model call.");

                        return Task.FromResult(new ToolCapableInferenceResponse
                        {
                            Success = true,
                            Content = "The file contains agent loop content.",
                            FinishReason = "stop"
                        });
                    });

                ToolAgentResponse response = await agent.RunAsync(
                    new ModelRunnerSettings { ApiType = "OpenAI", Endpoint = "http://localhost", Models = new List<string> { "test-model" } },
                    "test-model",
                    new List<ModelChatMessage> { new ModelChatMessage { Role = "user", Content = "Read sample.txt." } },
                    new CompletionRequestSettings { MaxTokens = 128, Temperature = 0, TopP = 1 },
                    new ToolExecutionContext { Settings = settings },
                    CancellationToken.None).ConfigureAwait(false);

                if (!response.Success) throw new InvalidOperationException("Expected tool agent loop to succeed.");
                if (modelCalls != 2) throw new InvalidOperationException("Expected two model calls in the tool agent loop.");
                if (response.ToolCalls.Count != 1 || !response.ToolCalls[0].Success) throw new InvalidOperationException("Expected one successful tool trace.");
                if (!response.Content.Contains("agent loop content", StringComparison.Ordinal)) throw new InvalidOperationException("Expected final answer from second model turn.");
            }
            finally
            {
                Directory.Delete(workspace, true);
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
