namespace Wilson.Core.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net.Http;
    using System.Net.Http.Headers;
    using System.Text;
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using System.Threading;
    using System.Threading.Tasks;
    using PolyPrompt.Clients;
    using PolyPrompt.Models;
    using PolyPrompt.Options;
    using Wilson.Core.Models;
    using Wilson.Core.Settings;
    using ChatMessage = Wilson.Core.Models.ChatMessage;
    using PpChatMessage = PolyPrompt.Models.ChatMessage;
    using PpToolDefinition = PolyPrompt.Models.ToolDefinition;
    using PpToolCall = PolyPrompt.Models.ToolCall;
    using PpToolChatRequest = PolyPrompt.Models.ToolChatRequest;
    using PpToolChatStreamingChunk = PolyPrompt.Models.ToolChatStreamingChunk;

    /// <summary>
    /// Inference integration service.
    /// </summary>
    public sealed class InferenceService
    {
        private readonly Settings _Settings;
        private static readonly JsonSerializerOptions _ToolJson = new JsonSerializerOptions
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            WriteIndented = false
        };

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
        public async Task<List<ModelRunnerStatus>> GetRunnerStatusesAsync(bool includeLiveStatus = true, CancellationToken token = default)
        {
            List<ModelRunnerStatus> statuses = new List<ModelRunnerStatus>();

            foreach (ModelRunnerSettings runner in _Settings.ModelRunners)
            {
                token.ThrowIfCancellationRequested();
                ModelRunnerSettings runnerDefaults = CopyRunner(runner);
                ModelRunnerSettings.ApplyHealthCheckDefaults(runnerDefaults);
                ModelRunnerStatus status = new ModelRunnerStatus
                {
                    Id = runner.Id,
                    Name = runner.Name,
                    ApiType = runner.ApiType,
                    Endpoint = runner.Endpoint,
                    ConfiguredModels = new List<string>(runner.Models),
                    ContextWindowTokens = runner.ContextWindowTokens,
                    ToolsEnabled = runnerDefaults.ToolsEnabled,
                    SupportsTools = runnerDefaults.SupportsTools,
                    ToolCallingApiFormat = runnerDefaults.ToolCallingApiFormat,
                    SupportsParallelToolCalls = runnerDefaults.SupportsParallelToolCalls,
                    SupportsStreamingToolCalls = runnerDefaults.SupportsStreamingToolCalls,
                    ChatCompletionsPath = runnerDefaults.ChatCompletionsPath,
                    HealthCheckEnabled = runnerDefaults.HealthCheckEnabled,
                    HealthCheckUrl = runnerDefaults.HealthCheckUrl,
                    HealthCheckMethod = runnerDefaults.HealthCheckMethod.ToString(),
                    HealthCheckIntervalMs = runnerDefaults.HealthCheckIntervalMs,
                    HealthCheckTimeoutMs = runnerDefaults.HealthCheckTimeoutMs,
                    HealthCheckExpectedStatusCode = runnerDefaults.HealthCheckExpectedStatusCode,
                    HealthyThreshold = runnerDefaults.HealthyThreshold,
                    UnhealthyThreshold = runnerDefaults.UnhealthyThreshold,
                    HealthCheckUseAuth = runnerDefaults.HealthCheckUseAuth
                };

                if (!includeLiveStatus)
                {
                    status.AvailableModels = new List<string>(runner.Models);
                    ModelCapabilityClassification nameClassification = ClassifyModelsFromNames(status.AvailableModels);
                    status.ChatModels = nameClassification.ChatModels;
                    status.EmbeddingModels = nameClassification.EmbeddingModels;
                    status.ToolModels = nameClassification.ToolModels;
                    if (!String.Equals(runner.ApiType, "Ollama", StringComparison.OrdinalIgnoreCase) && runnerDefaults.SupportsTools) status.ToolModels = new List<string>(status.ChatModels);
                    status.Models = new List<string>(status.ChatModels);
                    status.Online = true;
                    status.StatusMessage = "Live model status not queried.";
                    statuses.Add(status);
                    continue;
                }

                using CancellationTokenSource statusTimeout = CancellationTokenSource.CreateLinkedTokenSource(token);
                int statusTimeoutMs = Math.Max(1000, runnerDefaults.HealthCheckTimeoutMs);
                if (String.Equals(runner.ApiType, "Ollama", StringComparison.OrdinalIgnoreCase) && runnerDefaults.SupportsTools)
                    statusTimeoutMs = Math.Max(statusTimeoutMs, 15000);
                statusTimeout.CancelAfter(statusTimeoutMs);
                CancellationToken statusToken = statusTimeout.Token;

                try
                {
                    if (runner.Models.Count > 0)
                    {
                        status.AvailableModels = new List<string>(runner.Models);
                    }
                    else if (String.Equals(runner.ApiType, "Ollama", StringComparison.OrdinalIgnoreCase))
                    {
                        status.AvailableModels = await ListModelsAsync(runner, statusToken).ConfigureAwait(false);
                    }

                    if (String.Equals(runner.ApiType, "Ollama", StringComparison.OrdinalIgnoreCase))
                    {
                        status.LoadedModels = await ListLoadedOllamaModelsAsync(runner, statusToken).ConfigureAwait(false);
                    }

                    ModelCapabilityClassification classification = await ClassifyModelsAsync(runner, status.AvailableModels, statusToken).ConfigureAwait(false);
                    status.ChatModels = classification.ChatModels;
                    status.EmbeddingModels = classification.EmbeddingModels;
                    status.ToolModels = classification.ToolModels;
                    if (!String.Equals(runner.ApiType, "Ollama", StringComparison.OrdinalIgnoreCase) && runnerDefaults.SupportsTools) status.ToolModels = new List<string>(status.ChatModels);
                    status.Models = new List<string>(status.ChatModels);
                    status.Online = true;
                    status.StatusMessage = "Connected";
                }
                catch (OperationCanceledException) when (!token.IsCancellationRequested)
                {
                    ModelCapabilityClassification classification = ClassifyModelsFromNames(status.AvailableModels);
                    status.ChatModels = classification.ChatModels;
                    status.EmbeddingModels = classification.EmbeddingModels;
                    status.ToolModels = classification.ToolModels;
                    if (!String.Equals(runner.ApiType, "Ollama", StringComparison.OrdinalIgnoreCase) && runnerDefaults.SupportsTools) status.ToolModels = new List<string>(status.ChatModels);
                    status.Models = new List<string>(status.ChatModels);
                    status.Online = !runnerDefaults.HealthCheckEnabled;
                    status.StatusMessage = runnerDefaults.HealthCheckEnabled
                        ? "Live model status timed out after " + runnerDefaults.HealthCheckTimeoutMs + "ms."
                        : "Health checks are disabled; configured model server is treated as available.";
                }
                catch (Exception ex)
                {
                    ModelCapabilityClassification classification = ClassifyModelsFromNames(status.AvailableModels);
                    status.ChatModels = classification.ChatModels;
                    status.EmbeddingModels = classification.EmbeddingModels;
                    status.ToolModels = classification.ToolModels;
                    if (!String.Equals(runner.ApiType, "Ollama", StringComparison.OrdinalIgnoreCase) && runnerDefaults.SupportsTools) status.ToolModels = new List<string>(status.ChatModels);
                    status.Models = new List<string>(status.ChatModels);
                    status.Online = !runnerDefaults.HealthCheckEnabled;
                    status.StatusMessage = runnerDefaults.HealthCheckEnabled
                        ? ex.Message
                        : "Health checks are disabled; configured model server is treated as available.";
                }

                statuses.Add(status);
            }

            return statuses;
        }

        /// <summary>
        /// Determine whether a model is suitable for chat or completion requests.
        /// </summary>
        public async Task<bool> IsChatCapableModelAsync(ModelRunnerSettings runner, string model, CancellationToken token = default)
        {
            if (String.IsNullOrWhiteSpace(model)) return false;
            ModelCapabilityClassification classification = await ClassifyModelsAsync(runner, new List<string> { model }, token).ConfigureAwait(false);
            return classification.ChatModels.Any(item => String.Equals(item, model, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Determine whether a model advertises native tool-call support.
        /// </summary>
        public async Task<bool> IsToolCapableModelAsync(ModelRunnerSettings runner, string model, CancellationToken token = default)
        {
            if (String.IsNullOrWhiteSpace(model)) return false;
            ModelRunnerSettings copy = CopyRunner(runner);
            ModelRunnerSettings.ApplyToolDefaults(copy);
            if (!copy.ToolsEnabled || !copy.SupportsTools) return false;
            if (!String.Equals(copy.ApiType, "Ollama", StringComparison.OrdinalIgnoreCase)) return true;
            ModelCapabilityClassification classification = await ClassifyModelsAsync(copy, new List<string> { model }, token).ConfigureAwait(false);
            return classification.ToolModels.Any(item => String.Equals(item, model, StringComparison.OrdinalIgnoreCase));
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
            ThrowIfOllamaRequestFailed(response, responseBody, "pull");

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
            ThrowIfOllamaRequestFailed(response, responseBody, "load");

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
            return BuildPromptWithMetadata(messages, prompt, contextWindowTokens).Prompt;
        }

        /// <summary>
        /// Build a context prompt with truncation metadata.
        /// </summary>
        public PromptBuildResult BuildPromptWithMetadata(List<ChatMessage> messages, string prompt, int contextWindowTokens)
        {
            int budget = Math.Max(256, contextWindowTokens - 1024);
            List<string> selected = new List<string>();
            int included = 0;
            int total = EstimateTokens(prompt);
            for (int i = messages.Count - 1; i >= 0; i--)
            {
                ChatMessage message = messages[i];
                int tokens = message.TokenEstimate > 0 ? message.TokenEstimate : EstimateTokens(message.Content);
                if (total + tokens > budget) break;
                selected.Insert(0, message.Role + ": " + message.Content);
                total += tokens;
                included++;
            }
            selected.Add("user: " + prompt);
            string builtPrompt = String.Join(Environment.NewLine + Environment.NewLine, selected);
            return new PromptBuildResult
            {
                Prompt = builtPrompt,
                IncludedMessageCount = included,
                OmittedMessageCount = Math.Max(0, messages.Count - included),
                PromptBudgetTokens = budget,
                PromptTokenEstimate = EstimateTokens(builtPrompt),
                ContextWindowTokens = contextWindowTokens
            };
        }

        /// <summary>
        /// Execute a non-streaming chat request.
        /// </summary>
        public async Task<string> ChatAsync(ModelRunnerSettings runner, string model, string prompt, CompletionRequestSettings? settings = null, CancellationToken token = default)
        {
            using (CompletionClientBase client = CreateClient(runner, model))
            {
                ChatResponse response = await client.ChatAsync(prompt, CreateChatOptions(runner, settings), token).ConfigureAwait(false);
                if (!response.Success) throw new InvalidOperationException(response.Error ?? "Inference request failed.");
                // Strip any inline <think> reasoning so callers (e.g. title generation) get clean visible text.
                return ThinkParser.Strip(response.Text ?? String.Empty);
            }
        }

        /// <summary>
        /// Execute a non-streaming chat request and return the visible answer separated from model reasoning.
        /// Reasoning is sourced from the provider's reasoning channel and any inline &lt;think&gt; blocks.
        /// </summary>
        public async Task<InferenceCompletion> ChatWithReasoningAsync(ModelRunnerSettings runner, string model, string prompt, CompletionRequestSettings? settings = null, CancellationToken token = default)
        {
            using (CompletionClientBase client = CreateClient(runner, model))
            {
                ChatResponse response = await client.ChatAsync(prompt, CreateChatOptions(runner, settings), token).ConfigureAwait(false);
                if (!response.Success) throw new InvalidOperationException(response.Error ?? "Inference request failed.");

                ThinkParser parser = new ThinkParser();
                string visible = parser.Feed(response.Text ?? String.Empty) + parser.Finish();
                return new InferenceCompletion
                {
                    Text = visible,
                    Thinking = MergeReasoning(response.Reasoning, parser.Thinking)
                };
            }
        }

        /// <summary>
        /// Combine a provider reasoning channel value with inline-parsed think text into a single reasoning string.
        /// </summary>
        private static string MergeReasoning(string? channel, string? inline)
        {
            string a = (channel ?? String.Empty).Trim();
            string b = (inline ?? String.Empty).Trim();
            if (a.Length == 0) return b;
            if (b.Length == 0) return a;
            return a + "\n\n" + b;
        }

        /// <summary>
        /// Execute a non-streaming tool-capable chat request using a provider-native chat-completions API.
        /// </summary>
        /// <param name="runner">Model runner settings.</param>
        /// <param name="request">Tool-capable request.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Tool-capable response.</returns>
        public async Task<ToolCapableInferenceResponse> ChatWithToolsAsync(ModelRunnerSettings runner, ToolCapableInferenceRequest request, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(runner);
            ArgumentNullException.ThrowIfNull(request);
            if (request.Messages == null || request.Messages.Count == 0) throw new ArgumentException("At least one message is required.", nameof(request));

            ModelRunnerSettings effectiveRunner = CopyRunner(runner);
            ModelRunnerSettings.ApplyToolDefaults(effectiveRunner);
            if (!effectiveRunner.ToolsEnabled || !effectiveRunner.SupportsTools)
            {
                return new ToolCapableInferenceResponse
                {
                    Success = false,
                    ErrorMessage = "Runner is not configured for tool-capable requests."
                };
            }

            string format = effectiveRunner.ToolCallingApiFormat ?? String.Empty;
            if (String.Equals(format, "OpenAIChatCompletions", StringComparison.OrdinalIgnoreCase))
            {
                return await SendOpenAIChatCompletionsWithToolsAsync(effectiveRunner, request, token).ConfigureAwait(false);
            }

            if (String.Equals(format, "OllamaChat", StringComparison.OrdinalIgnoreCase))
            {
                return await SendOllamaChatWithToolsAsync(effectiveRunner, request, token).ConfigureAwait(false);
            }

            return new ToolCapableInferenceResponse
            {
                Success = false,
                ErrorMessage = "Unsupported tool-calling API format '" + format + "'."
            };
        }

        /// <summary>
        /// Execute a streaming tool-capable chat request via PolyPrompt, forwarding visible-text and reasoning
        /// deltas through <paramref name="onToken"/> and returning the accumulated tool-capable response.
        /// </summary>
        /// <param name="runner">Model runner settings.</param>
        /// <param name="request">Tool-capable request.</param>
        /// <param name="onToken">Optional live delta handler (text and reasoning fragments).</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Accumulated tool-capable response.</returns>
        public async Task<ToolCapableInferenceResponse> ChatWithToolsStreamingAsync(ModelRunnerSettings runner, ToolCapableInferenceRequest request, ToolAgentService.ToolTokenHandler? onToken, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(runner);
            ArgumentNullException.ThrowIfNull(request);
            if (request.Messages == null || request.Messages.Count == 0) throw new ArgumentException("At least one message is required.", nameof(request));

            ModelRunnerSettings effectiveRunner = CopyRunner(runner);
            ModelRunnerSettings.ApplyToolDefaults(effectiveRunner);
            if (!effectiveRunner.ToolsEnabled || !effectiveRunner.SupportsTools)
            {
                return new ToolCapableInferenceResponse
                {
                    Success = false,
                    ErrorMessage = "Runner is not configured for tool-capable requests."
                };
            }

            using (CompletionClientBase client = CreateClient(effectiveRunner, request.Model))
            {
                PpToolChatRequest ppRequest = BuildPolyPromptToolRequest(request);
                ToolChatStreamingResponse response = await client.ToolChatStreamingAsync(ppRequest, token).ConfigureAwait(false);
                if (!response.Success)
                {
                    return new ToolCapableInferenceResponse
                    {
                        Success = false,
                        ErrorMessage = response.Error ?? "Streaming tool inference request failed."
                    };
                }

                StringBuilder content = new StringBuilder();
                ThinkParser think = new ThinkParser();
                if (response.Chunks != null)
                {
                    await foreach (PpToolChatStreamingChunk chunk in response.Chunks.WithCancellation(token).ConfigureAwait(false))
                    {
                        string reasoningDelta = chunk.ReasoningText ?? String.Empty;
                        string visible = String.Empty;
                        if (!String.IsNullOrEmpty(chunk.Text))
                        {
                            visible = think.Feed(chunk.Text, out string inlineThink);
                            if (!String.IsNullOrEmpty(inlineThink)) reasoningDelta += inlineThink;
                        }

                        if (visible.Length > 0) content.Append(visible);
                        if (onToken != null && (visible.Length > 0 || reasoningDelta.Length > 0))
                        {
                            await onToken(visible.Length > 0 ? visible : null, reasoningDelta.Length > 0 ? reasoningDelta : null, token).ConfigureAwait(false);
                        }
                    }
                }

                string tail = think.Finish();
                if (!String.IsNullOrEmpty(tail))
                {
                    content.Append(tail);
                    if (onToken != null) await onToken(tail, null, token).ConfigureAwait(false);
                }

                return new ToolCapableInferenceResponse
                {
                    Success = true,
                    Content = content.ToString(),
                    Thinking = MergeReasoning(response.Reasoning, think.Thinking),
                    ToolCalls = MapPolyPromptToolCalls(response.ToolCalls),
                    FinishReason = response.FinishReason
                };
            }
        }

        /// <summary>
        /// Build a PolyPrompt tool-chat request from a Wilson tool-capable request.
        /// </summary>
        private static PpToolChatRequest BuildPolyPromptToolRequest(ToolCapableInferenceRequest request)
        {
            PpToolChatRequest ppRequest = new PpToolChatRequest
            {
                Model = String.IsNullOrWhiteSpace(request.Model) ? null : request.Model,
                Temperature = request.Temperature,
                TopP = request.TopP,
                MaxTokens = request.MaxTokens > 0 ? request.MaxTokens : (int?)null,
                ToolChoice = MapToolChoice(request.ToolChoice)
            };

            foreach (ModelChatMessage message in request.Messages)
            {
                PpChatMessage? mapped = MapMessageToPolyPrompt(message);
                if (mapped != null) ppRequest.Messages.Add(mapped);
            }

            foreach (ModelToolDefinition tool in request.Tools)
            {
                if (tool?.Function == null || String.IsNullOrWhiteSpace(tool.Function.Name)) continue;
                ppRequest.Tools.Add(PpToolDefinition.Function(tool.Function.Name, tool.Function.Description ?? String.Empty, ToParameterDictionary(tool.Function.Parameters)));
            }

            return ppRequest;
        }

        private static string MapToolChoice(string? choice)
        {
            if (String.Equals(choice, ToolChoiceModes.None, StringComparison.OrdinalIgnoreCase)) return "none";
            if (String.Equals(choice, ToolChoiceModes.Required, StringComparison.OrdinalIgnoreCase)) return "required";
            return "auto";
        }

        private static PpChatMessage? MapMessageToPolyPrompt(ModelChatMessage message)
        {
            if (message == null || String.IsNullOrWhiteSpace(message.Role)) return null;
            switch (message.Role.ToLowerInvariant())
            {
                case "system":
                    return PpChatMessage.System(message.Content ?? String.Empty);
                case "tool":
                    return PpChatMessage.ToolResult(message.ToolCallId ?? String.Empty, message.Name ?? String.Empty, message.Content ?? String.Empty);
                case "assistant":
                    if (message.ToolCalls != null && message.ToolCalls.Count > 0)
                        return PpChatMessage.AssistantToolCalls(message.ToolCalls.Where(call => call?.Function != null).Select(MapToolCallToPolyPrompt));
                    return PpChatMessage.Assistant(message.Content ?? String.Empty);
                default:
                    return PpChatMessage.User(message.Content ?? String.Empty);
            }
        }

        private static PpToolCall MapToolCallToPolyPrompt(ModelToolCall call)
        {
            return new PpToolCall
            {
                Id = call.Id,
                Name = call.Function?.Name ?? String.Empty,
                ArgumentsJson = String.IsNullOrWhiteSpace(call.Function?.Arguments) ? "{}" : call.Function!.Arguments
            };
        }

        private static List<ModelToolCall> MapPolyPromptToolCalls(List<PpToolCall>? calls)
        {
            List<ModelToolCall> output = new List<ModelToolCall>();
            if (calls == null) return output;
            foreach (PpToolCall call in calls)
            {
                if (call == null || String.IsNullOrWhiteSpace(call.Name)) continue;
                output.Add(new ModelToolCall
                {
                    Id = call.Id,
                    Type = "function",
                    Function = new ModelToolFunctionCall
                    {
                        Name = call.Name,
                        Arguments = String.IsNullOrWhiteSpace(call.ArgumentsJson) ? "{}" : call.ArgumentsJson
                    }
                });
            }

            return output;
        }

        private static Dictionary<string, object> ToParameterDictionary(object? parameters)
        {
            if (parameters == null) return new Dictionary<string, object>();
            try
            {
                string json = parameters is string raw ? raw : JsonSerializer.Serialize(parameters);
                Dictionary<string, object>? dictionary = JsonSerializer.Deserialize<Dictionary<string, object>>(json);
                return dictionary ?? new Dictionary<string, object>();
            }
            catch (Exception)
            {
                return new Dictionary<string, object>();
            }
        }

        /// <summary>
        /// Execute a streaming chat request.
        /// </summary>
        public async IAsyncEnumerable<InferenceStreamChunk> ChatStreamingAsync(ModelRunnerSettings runner, string model, string prompt, CompletionRequestSettings? settings = null, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken token = default)
        {
            using (CompletionClientBase client = CreateClient(runner, model))
            {
                ChatStreamingResponse response = await client.ChatStreamingAsync(prompt, CreateChatOptions(runner, settings), token).ConfigureAwait(false);
                if (!response.Success) throw new InvalidOperationException(response.Error ?? "Streaming inference request failed.");
                if (response.Chunks == null) yield break;

                ThinkParser think = new ThinkParser();
                await foreach (ChatStreamingChunk chunk in response.Chunks.WithCancellation(token).ConfigureAwait(false))
                {
                    string reasoningDelta = chunk.ReasoningText ?? String.Empty;
                    string visible = String.Empty;
                    if (!String.IsNullOrEmpty(chunk.Text))
                    {
                        visible = think.Feed(chunk.Text, out string inlineThink);
                        if (!String.IsNullOrEmpty(inlineThink)) reasoningDelta += inlineThink;
                    }

                    if (!String.IsNullOrEmpty(visible) || !String.IsNullOrEmpty(reasoningDelta))
                        yield return new InferenceStreamChunk { Text = visible, Reasoning = reasoningDelta };
                }

                string tail = think.Finish();
                if (!String.IsNullOrEmpty(tail)) yield return new InferenceStreamChunk { Text = tail };
            }
        }

        /// <summary>
        /// Parse a provider response from a tool-capable non-streaming chat request.
        /// </summary>
        /// <param name="toolCallingApiFormat">Tool calling API format.</param>
        /// <param name="responseBody">Provider response body.</param>
        /// <returns>Parsed response.</returns>
        public static ToolCapableInferenceResponse ParseToolCapableResponse(string toolCallingApiFormat, string responseBody)
        {
            if (String.IsNullOrWhiteSpace(responseBody))
            {
                return new ToolCapableInferenceResponse
                {
                    Success = false,
                    ErrorMessage = "Provider returned an empty response."
                };
            }

            try
            {
                using JsonDocument document = JsonDocument.Parse(responseBody);
                if (String.Equals(toolCallingApiFormat, "OpenAIChatCompletions", StringComparison.OrdinalIgnoreCase))
                    return ParseOpenAIChatCompletionsResponse(document.RootElement);
                if (String.Equals(toolCallingApiFormat, "OllamaChat", StringComparison.OrdinalIgnoreCase))
                    return ParseOllamaChatResponse(document.RootElement);

                return new ToolCapableInferenceResponse
                {
                    Success = false,
                    ErrorMessage = "Unsupported tool-calling API format '" + toolCallingApiFormat + "'."
                };
            }
            catch (JsonException ex)
            {
                return new ToolCapableInferenceResponse
                {
                    Success = false,
                    ErrorMessage = "Provider returned malformed JSON: " + ex.Message
                };
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

        private async Task<ToolCapableInferenceResponse> SendOpenAIChatCompletionsWithToolsAsync(ModelRunnerSettings runner, ToolCapableInferenceRequest request, CancellationToken token)
        {
            string url = ChatCompletionsUrl(runner);
            Dictionary<string, object> body = new Dictionary<string, object>
            {
                ["model"] = EffectiveToolModel(runner, request),
                ["messages"] = BuildProviderMessages(request.Messages)
            };

            if (request.MaxTokens > 0) body["max_tokens"] = request.MaxTokens;
            body["temperature"] = request.Temperature;
            body["top_p"] = request.TopP <= 0 ? 1 : request.TopP;

            List<ModelToolDefinition> tools = NormalizeToolDefinitions(request.Tools);
            if (tools.Count > 0)
            {
                body["tools"] = tools;
                string toolChoice = NormalizeOpenAIToolChoice(request.ToolChoice);
                if (!String.IsNullOrWhiteSpace(toolChoice)) body["tool_choice"] = toolChoice;
                if (runner.SupportsParallelToolCalls && _Settings.Tools.AllowParallelToolCalls) body["parallel_tool_calls"] = true;
            }

            return await SendToolChatRequestAsync(url, FirstNonEmpty(runner.ApiKey, request.ApiKey), body, "OpenAIChatCompletions", token).ConfigureAwait(false);
        }

        private async Task<ToolCapableInferenceResponse> SendOllamaChatWithToolsAsync(ModelRunnerSettings runner, ToolCapableInferenceRequest request, CancellationToken token)
        {
            string url = runner.Endpoint.TrimEnd('/') + "/api/chat";
            Dictionary<string, object> options = new Dictionary<string, object>
            {
                ["temperature"] = request.Temperature,
                ["top_p"] = request.TopP <= 0 ? 1 : request.TopP
            };
            if (request.MaxTokens > 0) options["num_predict"] = request.MaxTokens;

            Dictionary<string, object> body = new Dictionary<string, object>
            {
                ["model"] = EffectiveToolModel(runner, request),
                ["messages"] = BuildOllamaProviderMessages(request.Messages),
                ["stream"] = false,
                ["options"] = options
            };

            List<ModelToolDefinition> tools = NormalizeToolDefinitions(request.Tools);
            if (tools.Count > 0) body["tools"] = tools;

            return await SendToolChatRequestAsync(url, FirstNonEmpty(runner.ApiKey, request.ApiKey), body, "OllamaChat", token).ConfigureAwait(false);
        }

        private static string EffectiveToolModel(ModelRunnerSettings runner, ToolCapableInferenceRequest request)
        {
            if (!String.IsNullOrWhiteSpace(request.Model)) return request.Model.Trim();
            return runner.Models.FirstOrDefault(model => !String.IsNullOrWhiteSpace(model)) ?? String.Empty;
        }

        private static string? FirstNonEmpty(params string?[] values)
        {
            foreach (string? value in values)
            {
                if (!String.IsNullOrWhiteSpace(value)) return value.Trim();
            }

            return null;
        }

        private static async Task<ToolCapableInferenceResponse> SendToolChatRequestAsync(string url, string? apiKey, Dictionary<string, object> body, string format, CancellationToken token)
        {
            string json = JsonSerializer.Serialize(body, _ToolJson);
            using HttpClient client = new HttpClient();
            using HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, url);
            request.Content = new StringContent(json, Encoding.UTF8, "application/json");
            if (!String.IsNullOrWhiteSpace(apiKey)) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey.Trim());

            using HttpResponseMessage response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token).ConfigureAwait(false);
            string responseBody = await response.Content.ReadAsStringAsync(token).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return new ToolCapableInferenceResponse
                {
                    Success = false,
                    ErrorMessage = "Provider returned HTTP " + (int)response.StatusCode + ".",
                    Telemetry = new Dictionary<string, object>
                    {
                        ["statusCode"] = (int)response.StatusCode,
                        ["format"] = format
                    }
                };
            }

            ToolCapableInferenceResponse parsed = ParseToolCapableResponse(format, responseBody);
            parsed.Telemetry ??= new Dictionary<string, object>
            {
                ["statusCode"] = (int)response.StatusCode,
                ["format"] = format
            };
            return parsed;
        }

        private static string ChatCompletionsUrl(ModelRunnerSettings runner)
        {
            string endpoint = (runner.Endpoint ?? String.Empty).TrimEnd('/');
            if (endpoint.EndsWith("/chat/completions", StringComparison.OrdinalIgnoreCase)) return endpoint;
            string path = String.IsNullOrWhiteSpace(runner.ChatCompletionsPath) ? "/v1/chat/completions" : runner.ChatCompletionsPath.Trim();
            if (!path.StartsWith("/", StringComparison.Ordinal)) path = "/" + path;
            return endpoint + path;
        }

        private static List<object> BuildProviderMessages(List<ModelChatMessage> messages)
        {
            List<object> output = new List<object>();
            foreach (ModelChatMessage message in messages)
            {
                if (message == null || String.IsNullOrWhiteSpace(message.Role)) continue;
                Dictionary<string, object> item = new Dictionary<string, object>
                {
                    ["role"] = message.Role.Trim()
                };
                if (message.Content != null) item["content"] = message.Content;
                if (message.ToolCalls != null && message.ToolCalls.Count > 0) item["tool_calls"] = NormalizeToolCalls(message.ToolCalls);
                if (!String.IsNullOrWhiteSpace(message.ToolCallId)) item["tool_call_id"] = message.ToolCallId.Trim();
                if (!String.IsNullOrWhiteSpace(message.Name)) item["name"] = message.Name.Trim();
                output.Add(item);
            }

            return output;
        }

        private static List<object> BuildOllamaProviderMessages(List<ModelChatMessage> messages)
        {
            List<object> output = new List<object>();
            foreach (ModelChatMessage message in messages)
            {
                if (message == null || String.IsNullOrWhiteSpace(message.Role)) continue;
                Dictionary<string, object> item = new Dictionary<string, object>
                {
                    ["role"] = message.Role.Trim()
                };
                if (message.Content != null) item["content"] = message.Content;
                if (String.Equals(message.Role, "tool", StringComparison.OrdinalIgnoreCase))
                {
                    if (!String.IsNullOrWhiteSpace(message.Name)) item["tool_name"] = message.Name.Trim();
                    output.Add(item);
                    continue;
                }

                if (message.ToolCalls != null && message.ToolCalls.Count > 0) item["tool_calls"] = BuildOllamaToolCalls(message.ToolCalls);
                output.Add(item);
            }

            return output;
        }

        private static List<object> BuildOllamaToolCalls(IEnumerable<ModelToolCall> toolCalls)
        {
            List<object> output = new List<object>();
            int index = 0;
            foreach (ModelToolCall call in NormalizeToolCalls(toolCalls))
            {
                Dictionary<string, object> function = new Dictionary<string, object>
                {
                    ["index"] = index,
                    ["name"] = call.Function!.Name,
                    ["arguments"] = ParseToolArgumentsForProvider(call.Function.Arguments)
                };
                output.Add(new Dictionary<string, object>
                {
                    ["type"] = String.IsNullOrWhiteSpace(call.Type) ? "function" : call.Type.Trim(),
                    ["function"] = function
                });
                index++;
            }

            return output;
        }

        private static object ParseToolArgumentsForProvider(string arguments)
        {
            string json = String.IsNullOrWhiteSpace(arguments) ? "{}" : arguments.Trim();
            try
            {
                using JsonDocument document = JsonDocument.Parse(json);
                return document.RootElement.Clone();
            }
            catch
            {
                return new Dictionary<string, object>();
            }
        }

        private static string NormalizeOpenAIToolChoice(string toolChoice)
        {
            if (String.IsNullOrWhiteSpace(toolChoice)) return ToolChoiceModes.Auto;
            string normalized = toolChoice.Trim().ToLowerInvariant();
            if (normalized == ToolChoiceModes.None || normalized == ToolChoiceModes.Required || normalized == ToolChoiceModes.Auto) return normalized;
            if (normalized == ToolChoiceModes.AllowedOnly) return ToolChoiceModes.Auto;
            return ToolChoiceModes.Auto;
        }

        private static List<ModelToolDefinition> NormalizeToolDefinitions(IEnumerable<ModelToolDefinition>? tools)
        {
            if (tools == null) return new List<ModelToolDefinition>();
            return tools
                .Where(tool => tool != null && tool.Function != null && !String.IsNullOrWhiteSpace(tool.Function.Name))
                .Select(tool => new ModelToolDefinition
                {
                    Type = String.IsNullOrWhiteSpace(tool.Type) ? "function" : tool.Type.Trim(),
                    Function = new ModelToolFunctionDefinition
                    {
                        Name = tool.Function!.Name.Trim(),
                        Description = tool.Function.Description ?? String.Empty,
                        Parameters = tool.Function.Parameters
                    }
                })
                .ToList();
        }

        private static List<ModelToolCall> NormalizeToolCalls(IEnumerable<ModelToolCall>? toolCalls)
        {
            if (toolCalls == null) return new List<ModelToolCall>();
            return toolCalls
                .Where(call => call != null && call.Function != null && !String.IsNullOrWhiteSpace(call.Function.Name))
                .Select(call => new ModelToolCall
                {
                    Id = String.IsNullOrWhiteSpace(call.Id) ? null : call.Id.Trim(),
                    Type = String.IsNullOrWhiteSpace(call.Type) ? "function" : call.Type.Trim(),
                    Function = new ModelToolFunctionCall
                    {
                        Name = call.Function!.Name.Trim(),
                        Arguments = String.IsNullOrWhiteSpace(call.Function.Arguments) ? "{}" : call.Function.Arguments.Trim()
                    }
                })
                .ToList();
        }

        private static ToolCapableInferenceResponse ParseOpenAIChatCompletionsResponse(JsonElement root)
        {
            if (!root.TryGetProperty("choices", out JsonElement choices) || choices.ValueKind != JsonValueKind.Array || choices.GetArrayLength() == 0)
            {
                return new ToolCapableInferenceResponse { Success = false, ErrorMessage = "OpenAI-compatible response contained no choices." };
            }

            JsonElement choice = choices[0];
            JsonElement message = choice.TryGetProperty("message", out JsonElement messageElement) ? messageElement : default;
            List<ModelToolCall> toolCalls = message.ValueKind == JsonValueKind.Object ? ParseToolCalls(message) : new List<ModelToolCall>();
            string? finishReason = choice.TryGetProperty("finish_reason", out JsonElement finishElement) && finishElement.ValueKind == JsonValueKind.String
                ? finishElement.GetString()
                : null;
            if (String.IsNullOrWhiteSpace(finishReason) && toolCalls.Count > 0) finishReason = "tool_calls";

            return new ToolCapableInferenceResponse
            {
                Success = true,
                Content = message.ValueKind == JsonValueKind.Object ? GetOptionalString(message, "content") : null,
                ToolCalls = toolCalls,
                FinishReason = String.IsNullOrWhiteSpace(finishReason) ? "stop" : finishReason
            };
        }

        private static ToolCapableInferenceResponse ParseOllamaChatResponse(JsonElement root)
        {
            if (!root.TryGetProperty("message", out JsonElement message) || message.ValueKind != JsonValueKind.Object)
            {
                return new ToolCapableInferenceResponse { Success = false, ErrorMessage = "Ollama response contained no message." };
            }

            List<ModelToolCall> toolCalls = ParseToolCalls(message);
            string? finishReason = GetOptionalString(root, "done_reason");
            if (String.IsNullOrWhiteSpace(finishReason) && toolCalls.Count > 0) finishReason = "tool_calls";

            return new ToolCapableInferenceResponse
            {
                Success = true,
                Content = GetOptionalString(message, "content"),
                ToolCalls = toolCalls,
                FinishReason = String.IsNullOrWhiteSpace(finishReason) ? "stop" : finishReason
            };
        }

        private static List<ModelToolCall> ParseToolCalls(JsonElement message)
        {
            List<ModelToolCall> calls = new List<ModelToolCall>();
            if (!message.TryGetProperty("tool_calls", out JsonElement toolCalls) || toolCalls.ValueKind != JsonValueKind.Array) return calls;

            foreach (JsonElement callElement in toolCalls.EnumerateArray())
            {
                if (callElement.ValueKind != JsonValueKind.Object) continue;
                if (!callElement.TryGetProperty("function", out JsonElement functionElement) || functionElement.ValueKind != JsonValueKind.Object) continue;
                string? name = GetOptionalString(functionElement, "name");
                if (String.IsNullOrWhiteSpace(name)) continue;

                string arguments = "{}";
                if (functionElement.TryGetProperty("arguments", out JsonElement argumentsElement))
                {
                    arguments = argumentsElement.ValueKind == JsonValueKind.String
                        ? argumentsElement.GetString() ?? "{}"
                        : argumentsElement.GetRawText();
                }

                calls.Add(new ModelToolCall
                {
                    Id = GetOptionalString(callElement, "id"),
                    Type = GetOptionalString(callElement, "type") ?? "function",
                    Function = new ModelToolFunctionCall
                    {
                        Name = name.Trim(),
                        Arguments = String.IsNullOrWhiteSpace(arguments) ? "{}" : arguments.Trim()
                    }
                });
            }

            return calls;
        }

        private static string? GetOptionalString(JsonElement element, string propertyName)
        {
            if (!element.TryGetProperty(propertyName, out JsonElement value)) return null;
            if (value.ValueKind == JsonValueKind.String) return value.GetString();
            return null;
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

        private static async Task<ModelCapabilityClassification> ClassifyModelsAsync(ModelRunnerSettings runner, List<string> models, CancellationToken token)
        {
            ModelCapabilityClassification classification = new ModelCapabilityClassification();
            foreach (string model in models.Where(item => !String.IsNullOrWhiteSpace(item)).Distinct(StringComparer.OrdinalIgnoreCase))
            {
                token.ThrowIfCancellationRequested();
                ModelCapability capability = String.Equals(runner.ApiType, "Ollama", StringComparison.OrdinalIgnoreCase)
                    ? await GetOllamaModelCapabilityAsync(runner, model, token).ConfigureAwait(false)
                    : GetModelCapabilityFromName(model);

                if (capability == ModelCapability.Embedding) classification.EmbeddingModels.Add(model);
                else
                {
                    classification.ChatModels.Add(model);
                    if (capability == ModelCapability.ToolChat) classification.ToolModels.Add(model);
                }
            }

            classification.ChatModels = classification.ChatModels.OrderBy(item => item, StringComparer.OrdinalIgnoreCase).ToList();
            classification.EmbeddingModels = classification.EmbeddingModels.OrderBy(item => item, StringComparer.OrdinalIgnoreCase).ToList();
            classification.ToolModels = classification.ToolModels.OrderBy(item => item, StringComparer.OrdinalIgnoreCase).ToList();
            return classification;
        }

        private static ModelCapabilityClassification ClassifyModelsFromNames(List<string> models)
        {
            ModelCapabilityClassification classification = new ModelCapabilityClassification();
            foreach (string model in models.Where(item => !String.IsNullOrWhiteSpace(item)).Distinct(StringComparer.OrdinalIgnoreCase))
            {
                if (GetModelCapabilityFromName(model) == ModelCapability.Embedding) classification.EmbeddingModels.Add(model);
                else classification.ChatModels.Add(model);
            }

            classification.ChatModels = classification.ChatModels.OrderBy(item => item, StringComparer.OrdinalIgnoreCase).ToList();
            classification.EmbeddingModels = classification.EmbeddingModels.OrderBy(item => item, StringComparer.OrdinalIgnoreCase).ToList();
            classification.ToolModels = classification.ToolModels.OrderBy(item => item, StringComparer.OrdinalIgnoreCase).ToList();
            return classification;
        }

        private static async Task<ModelCapability> GetOllamaModelCapabilityAsync(ModelRunnerSettings runner, string model, CancellationToken token)
        {
            try
            {
                using HttpClient client = new HttpClient();
                if (!String.IsNullOrWhiteSpace(runner.ApiKey)) client.DefaultRequestHeaders.Add("Authorization", "Bearer " + runner.ApiKey);
                string body = JsonSerializer.Serialize(new { model });
                using StringContent content = new StringContent(body, Encoding.UTF8, "application/json");
                using HttpResponseMessage response = await client.PostAsync(EndpointUrl(runner.Endpoint, "/api/show"), content, token).ConfigureAwait(false);
                response.EnsureSuccessStatusCode();
                string json = await response.Content.ReadAsStringAsync(token).ConfigureAwait(false);
                using JsonDocument document = JsonDocument.Parse(json);
                if (HasOllamaCapability(document.RootElement, "tools") && (HasOllamaCapability(document.RootElement, "completion") || HasOllamaCapability(document.RootElement, "chat") || HasOllamaCapability(document.RootElement, "generate"))) return ModelCapability.ToolChat;
                if (HasOllamaCapability(document.RootElement, "completion") || HasOllamaCapability(document.RootElement, "chat") || HasOllamaCapability(document.RootElement, "generate")) return ModelCapability.Chat;
                if (HasOllamaCapability(document.RootElement, "embedding") || HasOllamaCapability(document.RootElement, "embeddings")) return ModelCapability.Embedding;
            }
            catch
            {
                return GetModelCapabilityFromName(model);
            }

            return GetModelCapabilityFromName(model);
        }

        private static bool HasOllamaCapability(JsonElement root, string capability)
        {
            if (!root.TryGetProperty("capabilities", out JsonElement capabilities) || capabilities.ValueKind != JsonValueKind.Array) return false;
            foreach (JsonElement item in capabilities.EnumerateArray())
            {
                string? value = item.GetString();
                if (String.Equals(value, capability, StringComparison.OrdinalIgnoreCase)) return true;
            }

            return false;
        }

        private static ModelCapability GetModelCapabilityFromName(string model)
        {
            return LooksLikeEmbeddingModel(model) ? ModelCapability.Embedding : ModelCapability.Chat;
        }

        private static bool LooksLikeEmbeddingModel(string model)
        {
            if (String.IsNullOrWhiteSpace(model)) return false;
            string normalized = model.ToLowerInvariant();
            return normalized.Contains("embed", StringComparison.Ordinal)
                || normalized.Contains("embedding", StringComparison.Ordinal)
                || normalized.Contains("all-minilm", StringComparison.Ordinal)
                || normalized.Contains("minilm", StringComparison.Ordinal)
                || normalized.Contains("bge-", StringComparison.Ordinal)
                || normalized.StartsWith("bge", StringComparison.Ordinal)
                || normalized.Contains("e5-", StringComparison.Ordinal)
                || normalized.StartsWith("e5", StringComparison.Ordinal)
                || normalized.Contains("gte-", StringComparison.Ordinal)
                || normalized.StartsWith("gte", StringComparison.Ordinal);
        }

        private sealed class ModelCapabilityClassification
        {
            public List<string> ChatModels { get; set; } = new List<string>();
            public List<string> EmbeddingModels { get; set; } = new List<string>();
            public List<string> ToolModels { get; set; } = new List<string>();
        }

        private enum ModelCapability
        {
            Chat,
            ToolChat,
            Embedding
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

        private static void ThrowIfOllamaRequestFailed(HttpResponseMessage response, string responseBody, string action)
        {
            if (response.IsSuccessStatusCode) return;

            int statusCode = (int)response.StatusCode;
            string message = ExtractOllamaError(responseBody);
            if (String.IsNullOrWhiteSpace(message))
            {
                string reason = String.IsNullOrWhiteSpace(response.ReasonPhrase) ? "HTTP " + statusCode.ToString(System.Globalization.CultureInfo.InvariantCulture) : response.ReasonPhrase!;
                message = "Ollama " + action + " request failed with HTTP " + statusCode.ToString(System.Globalization.CultureInfo.InvariantCulture) + " (" + reason + ").";
            }

            throw new ModelServerHttpException(statusCode, message, responseBody);
        }

        private static string ExtractOllamaError(string responseBody)
        {
            if (String.IsNullOrWhiteSpace(responseBody)) return String.Empty;
            string trimmed = responseBody.Trim();

            try
            {
                using JsonDocument document = JsonDocument.Parse(trimmed);
                if (document.RootElement.TryGetProperty("error", out JsonElement errorProperty))
                {
                    string? error = errorProperty.ValueKind == JsonValueKind.String ? errorProperty.GetString() : errorProperty.GetRawText();
                    if (!String.IsNullOrWhiteSpace(error)) return error.Trim();
                }
            }
            catch
            {
            }

            return trimmed;
        }

        private static ChatCompletionOptions CreateChatOptions(ModelRunnerSettings runner, CompletionRequestSettings? settings)
        {
            settings ??= new CompletionRequestSettings();
            ChatCompletionOptions options = String.Equals(runner.ApiType, "Ollama", StringComparison.OrdinalIgnoreCase)
                ? new OllamaChatCompletionOptions
                {
                    ContextLength = runner.ContextWindowTokens,
                    TopK = settings.TopK,
                    MinP = settings.MinP,
                    RepeatPenalty = settings.RepeatPenalty,
                    RepeatLastN = settings.RepeatLastN,
                    Seed = settings.Seed
                }
                : new ChatCompletionOptions();

            options.SystemPrompt = String.IsNullOrWhiteSpace(settings.SystemPrompt) ? CompletionRequestSettings.DefaultSystemPrompt : settings.SystemPrompt;
            options.Temperature = settings.Temperature;
            options.TopP = settings.TopP;
            options.MaxTokens = settings.MaxTokens;
            return options;
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
                ContextWindowTokens = runner.ContextWindowTokens,
                ToolsEnabled = runner.ToolsEnabled,
                SupportsTools = runner.SupportsTools,
                ToolCallingApiFormat = runner.ToolCallingApiFormat,
                SupportsParallelToolCalls = runner.SupportsParallelToolCalls,
                SupportsStreamingToolCalls = runner.SupportsStreamingToolCalls,
                ChatCompletionsPath = runner.ChatCompletionsPath,
                HealthCheckEnabled = runner.HealthCheckEnabled,
                HealthCheckUrl = runner.HealthCheckUrl,
                HealthCheckMethod = runner.HealthCheckMethod,
                HealthCheckIntervalMs = runner.HealthCheckIntervalMs,
                HealthCheckTimeoutMs = runner.HealthCheckTimeoutMs,
                HealthCheckExpectedStatusCode = runner.HealthCheckExpectedStatusCode,
                HealthyThreshold = runner.HealthyThreshold,
                UnhealthyThreshold = runner.UnhealthyThreshold,
                HealthCheckUseAuth = runner.HealthCheckUseAuth
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

    /// <summary>
    /// HTTP error returned by a configured model server.
    /// </summary>
    public sealed class ModelServerHttpException : Exception
    {
        /// <summary>HTTP status code returned by the model server.</summary>
        public int StatusCode { get; }
        /// <summary>Raw response body returned by the model server.</summary>
        public string ResponseBody { get; }

        /// <summary>
        /// Instantiate a model-server HTTP exception.
        /// </summary>
        /// <param name="statusCode">HTTP status code returned by the model server.</param>
        /// <param name="message">Human-readable error message.</param>
        /// <param name="responseBody">Raw response body returned by the model server.</param>
        public ModelServerHttpException(int statusCode, string message, string responseBody) : base(message)
        {
            StatusCode = statusCode;
            ResponseBody = responseBody ?? String.Empty;
        }
    }

    /// <summary>
    /// Prompt build result.
    /// </summary>
    public sealed class PromptBuildResult
    {
        /// <summary>Prompt text.</summary>
        public string Prompt { get; set; } = String.Empty;
        /// <summary>Included history message count.</summary>
        public int IncludedMessageCount { get; set; }
        /// <summary>Omitted history message count.</summary>
        public int OmittedMessageCount { get; set; }
        /// <summary>Prompt budget tokens.</summary>
        public int PromptBudgetTokens { get; set; }
        /// <summary>Estimated prompt tokens.</summary>
        public int PromptTokenEstimate { get; set; }
        /// <summary>Context window tokens.</summary>
        public int ContextWindowTokens { get; set; }
    }
}
