namespace Wilson.Core.Services
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using Wilson.Core.Helpers;
    using Wilson.Core.Models;
    using Wilson.Core.Settings;
    using Wilson.Core.Tools;

    /// <summary>
    /// Non-streaming core loop for model-directed tool calls.
    /// </summary>
    public sealed class ToolAgentService
    {
        /// <summary>
        /// Tool-capable inference delegate.
        /// </summary>
        public delegate Task<ToolCapableInferenceResponse> ToolInferenceHandler(ModelRunnerSettings runner, ToolCapableInferenceRequest request, CancellationToken token);

        private readonly ToolService _ToolService;
        private readonly ToolInferenceHandler _Inference;

        /// <summary>
        /// Instantiate using Wilson's inference service.
        /// </summary>
        /// <param name="toolService">Tool service.</param>
        /// <param name="inferenceService">Inference service.</param>
        public ToolAgentService(ToolService toolService, InferenceService inferenceService)
            : this(toolService, inferenceService.ChatWithToolsAsync)
        {
        }

        /// <summary>
        /// Instantiate with a caller-supplied inference delegate.
        /// </summary>
        /// <param name="toolService">Tool service.</param>
        /// <param name="inference">Inference delegate.</param>
        public ToolAgentService(ToolService toolService, ToolInferenceHandler inference)
        {
            _ToolService = toolService ?? throw new ArgumentNullException(nameof(toolService));
            _Inference = inference ?? throw new ArgumentNullException(nameof(inference));
        }

        /// <summary>
        /// Run the non-streaming tool loop.
        /// </summary>
        /// <param name="runner">Model runner.</param>
        /// <param name="model">Model identifier.</param>
        /// <param name="messages">Initial messages.</param>
        /// <param name="completionSettings">Completion settings.</param>
        /// <param name="executionContext">Tool execution context.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Agent result.</returns>
        public async Task<ToolAgentResponse> RunAsync(
            ModelRunnerSettings runner,
            string model,
            List<ModelChatMessage> messages,
            CompletionRequestSettings? completionSettings,
            ToolExecutionContext executionContext,
            CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(runner);
            ArgumentNullException.ThrowIfNull(messages);
            ArgumentNullException.ThrowIfNull(executionContext);

            completionSettings ??= new CompletionRequestSettings();
            List<ModelChatMessage> conversation = BuildInitialConversation(messages, completionSettings);
            List<ToolTrace> traces = new List<ToolTrace>();
            List<ModelToolDefinition> tools = _ToolService.GetModelToolDefinitions();
            int maxIterations = Math.Clamp(executionContext.Settings.Tools.MaxToolIterations, 1, 20);
            int maxToolCalls = Math.Clamp(executionContext.Settings.Tools.MaxToolCallsPerTurn, 1, 50);
            int sequence = 0;
            int errors = 0;

            for (int iteration = 1; iteration <= maxIterations; iteration++)
            {
                token.ThrowIfCancellationRequested();
                ToolCapableInferenceRequest request = new ToolCapableInferenceRequest
                {
                    Messages = conversation,
                    Model = model,
                    MaxTokens = completionSettings.MaxTokens ?? 2048,
                    Temperature = completionSettings.Temperature ?? 0.7,
                    TopP = completionSettings.TopP ?? 0.9,
                    Provider = runner.ApiType,
                    Endpoint = runner.Endpoint,
                    ApiKey = runner.ApiKey,
                    Tools = tools,
                    ToolChoice = tools.Count > 0 ? executionContext.Settings.Tools.ToolChoiceMode : ToolChoiceModes.None
                };

                ToolCapableInferenceResponse response = await _Inference(runner, request, token).ConfigureAwait(false);
                if (!response.Success)
                {
                    return new ToolAgentResponse
                    {
                        Success = false,
                        Content = String.Empty,
                        ErrorMessage = response.ErrorMessage,
                        FinishReason = response.FinishReason,
                        IterationCount = iteration,
                        ToolCallCount = sequence,
                        ErrorCount = errors + 1,
                        ToolCalls = traces,
                        Messages = conversation
                    };
                }

                List<ModelToolCall> toolCalls = NormalizeAgentToolCalls(response.ToolCalls);
                if (toolCalls.Count == 0)
                {
                    if (!String.IsNullOrWhiteSpace(response.Content))
                    {
                        conversation.Add(new ModelChatMessage { Role = "assistant", Content = response.Content });
                    }

                    return new ToolAgentResponse
                    {
                        Success = true,
                        Content = response.Content ?? String.Empty,
                        FinishReason = response.FinishReason,
                        IterationCount = iteration,
                        ToolCallCount = sequence,
                        ErrorCount = errors,
                        ToolCalls = traces,
                        Messages = conversation
                    };
                }

                conversation.Add(new ModelChatMessage
                {
                    Role = "assistant",
                    Content = response.Content,
                    ToolCalls = toolCalls
                });

                foreach (ModelToolCall call in toolCalls)
                {
                    token.ThrowIfCancellationRequested();
                    sequence++;
                    if (sequence > maxToolCalls)
                    {
                        ToolResult limit = ToolResultFactory.Error(call.Id ?? IdGenerator.ToolCall(), "tool_call_limit_reached", "Tool call limit reached for this turn.");
                        conversation.Add(new ModelChatMessage { Role = "tool", ToolCallId = call.Id, Name = call.Function?.Name, Content = limit.Content });
                        traces.Add(BuildTrace(call, iteration, sequence, limit, DateTime.UtcNow, DateTime.UtcNow, 0));
                        errors++;
                        continue;
                    }

                    DateTime started = DateTime.UtcNow;
                    Stopwatch sw = Stopwatch.StartNew();
                    ToolResult result = await ExecuteToolCallAsync(call, executionContext, token).ConfigureAwait(false);
                    sw.Stop();
                    DateTime completed = DateTime.UtcNow;
                    if (!result.Success) errors++;

                    traces.Add(BuildTrace(call, iteration, sequence, result, started, completed, sw.Elapsed.TotalMilliseconds));
                    conversation.Add(new ModelChatMessage
                    {
                        Role = "tool",
                        ToolCallId = call.Id,
                        Name = call.Function?.Name,
                        Content = result.Content
                    });
                }
            }

            return new ToolAgentResponse
            {
                Success = false,
                Content = String.Empty,
                ErrorMessage = "Tool iteration limit reached before a final assistant response.",
                FinishReason = "tool_iteration_limit",
                IterationCount = maxIterations,
                ToolCallCount = sequence,
                ErrorCount = errors + 1,
                ToolCalls = traces,
                Messages = conversation
            };
        }

        private async Task<ToolResult> ExecuteToolCallAsync(ModelToolCall call, ToolExecutionContext context, CancellationToken token)
        {
            string toolCallId = String.IsNullOrWhiteSpace(call.Id) ? IdGenerator.ToolCall() : call.Id!;
            string toolName = call.Function?.Name ?? String.Empty;
            string argumentsJson = call.Function?.Arguments ?? "{}";

            try
            {
                using JsonDocument document = JsonDocument.Parse(String.IsNullOrWhiteSpace(argumentsJson) ? "{}" : argumentsJson);
                return await _ToolService.ExecuteAsync(toolCallId, toolName, document.RootElement, context, token).ConfigureAwait(false);
            }
            catch (JsonException ex)
            {
                return ToolResultFactory.Error(toolCallId, "invalid_arguments", "Tool arguments were not valid JSON: " + ex.Message);
            }
        }

        private static List<ModelChatMessage> BuildInitialConversation(List<ModelChatMessage> messages, CompletionRequestSettings settings)
        {
            List<ModelChatMessage> conversation = new List<ModelChatMessage>();
            if (!String.IsNullOrWhiteSpace(settings.SystemPrompt) && !messages.Any(message => String.Equals(message.Role, "system", StringComparison.OrdinalIgnoreCase)))
            {
                conversation.Add(new ModelChatMessage { Role = "system", Content = settings.SystemPrompt });
            }

            foreach (ModelChatMessage message in messages)
            {
                if (message == null || String.IsNullOrWhiteSpace(message.Role)) continue;
                conversation.Add(message);
            }

            return conversation;
        }

        private static List<ModelToolCall> NormalizeAgentToolCalls(IEnumerable<ModelToolCall>? calls)
        {
            List<ModelToolCall> output = new List<ModelToolCall>();
            if (calls == null) return output;

            foreach (ModelToolCall call in calls)
            {
                if (call?.Function == null || String.IsNullOrWhiteSpace(call.Function.Name)) continue;
                output.Add(new ModelToolCall
                {
                    Id = String.IsNullOrWhiteSpace(call.Id) ? IdGenerator.ToolCall() : call.Id,
                    Type = String.IsNullOrWhiteSpace(call.Type) ? "function" : call.Type,
                    Function = new ModelToolFunctionCall
                    {
                        Name = call.Function.Name.Trim(),
                        Arguments = String.IsNullOrWhiteSpace(call.Function.Arguments) ? "{}" : call.Function.Arguments.Trim()
                    }
                });
            }

            return output;
        }

        private static ToolTrace BuildTrace(ModelToolCall call, int iteration, int sequenceNumber, ToolResult result, DateTime started, DateTime completed, double elapsedMs)
        {
            string name = call.Function?.Name ?? String.Empty;
            return new ToolTrace
            {
                ToolCallId = call.Id,
                ToolName = name,
                DisplayLabel = DisplayName(name),
                Iteration = iteration,
                SequenceNumber = sequenceNumber,
                Success = result.Success,
                Denied = String.Equals(result.ErrorCode, "tool_call_limit_reached", StringComparison.OrdinalIgnoreCase),
                Truncated = result.Truncated,
                OutputCharacters = result.Content?.Length ?? 0,
                ElapsedMs = elapsedMs,
                Summary = result.Success ? "Completed." : result.ErrorMessage,
                StartedUtc = started,
                CompletedUtc = completed
            };
        }

        private static string DisplayName(string name)
        {
            if (String.IsNullOrWhiteSpace(name)) return String.Empty;
            string[] parts = name.Split('_', StringSplitOptions.RemoveEmptyEntries);
            return String.Join(" ", parts.Select(part => Char.ToUpperInvariant(part[0]) + part.Substring(1)));
        }
    }
}
