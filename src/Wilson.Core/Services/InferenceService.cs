namespace Wilson.Core.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net.Http;
    using System.Text;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using PolyPrompt.Clients;
    using PolyPrompt.Models;
    using Wilson.Core.Models;
    using Wilson.Core.Settings;

    /// <summary>
    /// Inference integration service.
    /// </summary>
    public sealed class InferenceService
    {
        private readonly Settings _Settings;

        /// <summary>
        /// Instantiate the inference service.
        /// </summary>
        public InferenceService(Settings settings)
        {
            ArgumentNullException.ThrowIfNull(settings);
            _Settings = settings;
        }

        /// <summary>
        /// Get a runner by identifier.
        /// </summary>
        public ModelRunnerSettings GetRunner(string runnerId)
        {
            ModelRunnerSettings? runner = _Settings.ModelRunners.Find(item => String.Equals(item.Id, runnerId, StringComparison.OrdinalIgnoreCase));
            if (runner == null) throw new KeyNotFoundException("Unknown runner '" + runnerId + "'.");
            return runner;
        }

        /// <summary>
        /// Resolve model runners, retrieving Ollama models from the server when no models are configured.
        /// </summary>
        public async Task<List<ModelRunnerSettings>> GetResolvedRunnersAsync(CancellationToken token = default)
        {
            List<ModelRunnerSettings> resolved = new List<ModelRunnerSettings>();

            foreach (ModelRunnerSettings runner in _Settings.ModelRunners)
            {
                token.ThrowIfCancellationRequested();
                ModelRunnerSettings copy = CopyRunner(runner);

                if (copy.Models.Count < 1 && String.Equals(copy.ApiType, "Ollama", StringComparison.OrdinalIgnoreCase))
                {
                    try
                    {
                        copy.Models = await ListModelsAsync(copy, token).ConfigureAwait(false);
                    }
                    catch
                    {
                        copy.Models = new List<string>();
                    }
                }

                resolved.Add(copy);
            }

            return resolved;
        }

        /// <summary>
        /// Resolve model runner status, including available and loaded models where supported.
        /// </summary>
        public async Task<List<ModelRunnerStatus>> GetRunnerStatusesAsync(CancellationToken token = default)
        {
            List<ModelRunnerStatus> statuses = new List<ModelRunnerStatus>();

            foreach (ModelRunnerSettings runner in _Settings.ModelRunners)
            {
                token.ThrowIfCancellationRequested();
                ModelRunnerStatus status = new ModelRunnerStatus
                {
                    Id = runner.Id,
                    Name = runner.Name,
                    ApiType = runner.ApiType,
                    Endpoint = runner.Endpoint,
                    ConfiguredModels = new List<string>(runner.Models),
                    ContextWindowTokens = runner.ContextWindowTokens
                };

                try
                {
                    if (runner.Models.Count > 0)
                    {
                        status.AvailableModels = new List<string>(runner.Models);
                    }
                    else if (String.Equals(runner.ApiType, "Ollama", StringComparison.OrdinalIgnoreCase))
                    {
                        status.AvailableModels = await ListModelsAsync(runner, token).ConfigureAwait(false);
                    }

                    if (String.Equals(runner.ApiType, "Ollama", StringComparison.OrdinalIgnoreCase))
                    {
                        status.LoadedModels = await ListLoadedOllamaModelsAsync(runner, token).ConfigureAwait(false);
                    }

                    status.Models = new List<string>(status.AvailableModels);
                    status.Online = true;
                    status.StatusMessage = "Connected";
                }
                catch (Exception ex)
                {
                    status.Models = new List<string>(status.AvailableModels);
                    status.Online = false;
                    status.StatusMessage = ex.Message;
                }

                statuses.Add(status);
            }

            return statuses;
        }

        /// <summary>
        /// Request that an Ollama runner pull a model.
        /// </summary>
        public async Task<ModelPullResult> PullOllamaModelAsync(string runnerId, string model, CancellationToken token = default)
        {
            if (String.IsNullOrWhiteSpace(model)) throw new ArgumentException("Model name is required.", nameof(model));

            ModelRunnerSettings runner = GetRunner(runnerId);
            if (!String.Equals(runner.ApiType, "Ollama", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Model pulls are only supported for Ollama model servers.");
            }

            using HttpClient client = new HttpClient { Timeout = TimeSpan.FromMinutes(30) };
            if (!String.IsNullOrWhiteSpace(runner.ApiKey)) client.DefaultRequestHeaders.Add("Authorization", "Bearer " + runner.ApiKey);

            string requestedModel = model.Trim();
            string body = JsonSerializer.Serialize(new { model = requestedModel, stream = false });
            using StringContent content = new StringContent(body, Encoding.UTF8, "application/json");
            using HttpResponseMessage response = await client.PostAsync(EndpointUrl(runner.Endpoint, "/api/pull"), content, token).ConfigureAwait(false);
            string responseBody = await response.Content.ReadAsStringAsync(token).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            return new ModelPullResult
            {
                RunnerId = runner.Id,
                Model = requestedModel,
                Status = ExtractOllamaStatus(responseBody)
            };
        }

        /// <summary>
        /// Request that an Ollama runner load a model into memory.
        /// </summary>
        public async Task<ModelPullResult> LoadOllamaModelAsync(string runnerId, string model, CancellationToken token = default)
        {
            if (String.IsNullOrWhiteSpace(model)) throw new ArgumentException("Model name is required.", nameof(model));

            ModelRunnerSettings runner = GetRunner(runnerId);
            if (!String.Equals(runner.ApiType, "Ollama", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Model loading is only supported for Ollama model servers.");
            }

            using HttpClient client = new HttpClient { Timeout = TimeSpan.FromMinutes(30) };
            if (!String.IsNullOrWhiteSpace(runner.ApiKey)) client.DefaultRequestHeaders.Add("Authorization", "Bearer " + runner.ApiKey);

            string requestedModel = model.Trim();
            string body = JsonSerializer.Serialize(new { model = requestedModel, prompt = String.Empty, stream = false });
            using StringContent content = new StringContent(body, Encoding.UTF8, "application/json");
            using HttpResponseMessage response = await client.PostAsync(EndpointUrl(runner.Endpoint, "/api/generate"), content, token).ConfigureAwait(false);
            string responseBody = await response.Content.ReadAsStringAsync(token).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            return new ModelPullResult
            {
                RunnerId = runner.Id,
                Model = requestedModel,
                Status = ExtractOllamaStatus(responseBody)
            };
        }

        /// <summary>
        /// Build a context prompt with truncation.
        /// </summary>
        public string BuildPrompt(List<ChatMessage> messages, string prompt, int contextWindowTokens)
        {
            int budget = Math.Max(256, contextWindowTokens - 1024);
            List<string> selected = new List<string>();
            int total = EstimateTokens(prompt);
            for (int i = messages.Count - 1; i >= 0; i--)
            {
                ChatMessage message = messages[i];
                int tokens = message.TokenEstimate > 0 ? message.TokenEstimate : EstimateTokens(message.Content);
                if (total + tokens > budget) break;
                selected.Insert(0, message.Role + ": " + message.Content);
                total += tokens;
            }
            selected.Add("user: " + prompt);
            selected.Insert(0, "system: Use prior turns only as context. Respond only to the latest user message, and do not replay or quote earlier assistant responses unless the user explicitly asks for them.");
            return String.Join(Environment.NewLine + Environment.NewLine, selected);
        }

        /// <summary>
        /// Execute a non-streaming chat request.
        /// </summary>
        public async Task<string> ChatAsync(ModelRunnerSettings runner, string model, string prompt, CancellationToken token = default)
        {
            using (CompletionClientBase client = CreateClient(runner, model))
            {
                ChatResponse response = await client.ChatAsync(prompt, null, token).ConfigureAwait(false);
                if (!response.Success) throw new InvalidOperationException(response.Error ?? "Inference request failed.");
                return response.Text ?? String.Empty;
            }
        }

        /// <summary>
        /// Execute a streaming chat request.
        /// </summary>
        public async IAsyncEnumerable<string> ChatStreamingAsync(ModelRunnerSettings runner, string model, string prompt, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken token = default)
        {
            using (CompletionClientBase client = CreateClient(runner, model))
            {
                ChatStreamingResponse response = await client.ChatStreamingAsync(prompt, null, token).ConfigureAwait(false);
                if (!response.Success) throw new InvalidOperationException(response.Error ?? "Streaming inference request failed.");
                if (response.Chunks == null) yield break;
                await foreach (ChatStreamingChunk chunk in response.Chunks.WithCancellation(token).ConfigureAwait(false))
                {
                    if (!String.IsNullOrEmpty(chunk.Text)) yield return chunk.Text;
                }
            }
        }

        /// <summary>
        /// Estimate token count for truncation.
        /// </summary>
        public static int EstimateTokens(string value)
        {
            if (String.IsNullOrWhiteSpace(value)) return 0;
            return Math.Max(1, value.Length / 4);
        }

        private async Task<List<string>> ListModelsAsync(ModelRunnerSettings runner, CancellationToken token)
        {
            List<string> models = new List<string>();
            using (CompletionClientBase client = CreateClient(runner, String.Empty))
            {
                await foreach (ModelInformation model in client.ListModelsAsync(token).ConfigureAwait(false))
                {
                    if (!String.IsNullOrWhiteSpace(model.Name)) models.Add(model.Name);
                }
            }

            return models
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(item => item, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static async Task<List<string>> ListLoadedOllamaModelsAsync(ModelRunnerSettings runner, CancellationToken token)
        {
            using HttpClient client = new HttpClient();
            if (!String.IsNullOrWhiteSpace(runner.ApiKey)) client.DefaultRequestHeaders.Add("Authorization", "Bearer " + runner.ApiKey);
            using HttpResponseMessage response = await client.GetAsync(EndpointUrl(runner.Endpoint, "/api/ps"), token).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            string json = await response.Content.ReadAsStringAsync(token).ConfigureAwait(false);
            using JsonDocument document = JsonDocument.Parse(json);
            List<string> models = new List<string>();
            if (document.RootElement.TryGetProperty("models", out JsonElement modelArray) && modelArray.ValueKind == JsonValueKind.Array)
            {
                foreach (JsonElement item in modelArray.EnumerateArray())
                {
                    string? name = null;
                    if (item.TryGetProperty("model", out JsonElement modelProperty)) name = modelProperty.GetString();
                    if (String.IsNullOrWhiteSpace(name) && item.TryGetProperty("name", out JsonElement nameProperty)) name = nameProperty.GetString();
                    if (!String.IsNullOrWhiteSpace(name)) models.Add(name);
                }
            }

            return models
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(item => item, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static string EndpointUrl(string endpoint, string path)
        {
            return endpoint.TrimEnd('/') + path;
        }

        private static string ExtractOllamaStatus(string responseBody)
        {
            if (String.IsNullOrWhiteSpace(responseBody)) return "Pull request completed.";

            try
            {
                using JsonDocument document = JsonDocument.Parse(responseBody);
                if (document.RootElement.TryGetProperty("status", out JsonElement statusProperty))
                {
                    string? status = statusProperty.GetString();
                    if (!String.IsNullOrWhiteSpace(status)) return status;
                }
            }
            catch
            {
                return "Pull request completed.";
            }

            return "Pull request completed.";
        }

        private static ModelRunnerSettings CopyRunner(ModelRunnerSettings runner)
        {
            return new ModelRunnerSettings
            {
                Id = runner.Id,
                Name = runner.Name,
                ApiType = runner.ApiType,
                Endpoint = runner.Endpoint,
                ApiKey = runner.ApiKey,
                Models = new List<string>(runner.Models),
                ContextWindowTokens = runner.ContextWindowTokens
            };
        }

        private CompletionClientBase CreateClient(ModelRunnerSettings runner, string model)
        {
            CompletionClientBase client;
            if (String.Equals(runner.ApiType, "OpenAI", StringComparison.OrdinalIgnoreCase) || String.Equals(runner.ApiType, "OpenAICompatible", StringComparison.OrdinalIgnoreCase))
            {
                client = new OpenAiClient(runner.Endpoint, runner.ApiKey);
            }
            else
            {
                client = new OllamaClient(runner.Endpoint, runner.ApiKey);
            }
            if (!String.IsNullOrWhiteSpace(model)) client.Model = model;
            return client;
        }
    }
}
