namespace Wilson.Core.Tools
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using Wilson.Core.Models;
    using Wilson.Core.Settings;

    /// <summary>
    /// Resolves effective tool availability.
    /// </summary>
    public sealed class ToolService
    {
        private readonly Settings _Settings;
        private readonly BuiltInToolRegistry _Registry;
        private readonly ToolPolicyResolver _PolicyResolver = new ToolPolicyResolver();

        /// <summary>
        /// Instantiate the tool service.
        /// </summary>
        /// <param name="settings">Wilson settings.</param>
        public ToolService(Settings settings)
        {
            _Settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _Registry = new BuiltInToolRegistry();
        }

        /// <summary>
        /// List effective tool descriptors.
        /// </summary>
        /// <param name="includeDisabled">Whether to include unavailable disabled descriptors.</param>
        /// <returns>Tool descriptors.</returns>
        public List<ToolDescriptor> ListTools(bool includeDisabled = true)
        {
            return _PolicyResolver.Resolve(_Settings, _Registry.Executors, includeDisabled);
        }

        /// <summary>
        /// Get one effective tool descriptor.
        /// </summary>
        /// <param name="name">Tool name.</param>
        /// <returns>Tool descriptor or null.</returns>
        public ToolDescriptor? GetTool(string name)
        {
            if (String.IsNullOrWhiteSpace(name)) return null;
            return ListTools(true).FirstOrDefault(item => String.Equals(item.Name, name, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// List model-facing tool definitions.
        /// </summary>
        /// <returns>Model tool definitions.</returns>
        public List<ModelToolDefinition> GetModelToolDefinitions()
        {
            HashSet<string> availableNames = new HashSet<string>(
                ListTools(false).Select(item => item.Name),
                StringComparer.OrdinalIgnoreCase);

            return _Registry.GetToolDefinitions()
                .Where(definition => availableNames.Contains(definition.Name))
                .Select(definition => new ModelToolDefinition
                {
                    Type = "function",
                    Function = new ModelToolFunctionDefinition
                    {
                        Name = definition.Name,
                        Description = definition.Description,
                        Parameters = definition.ParametersSchema
                    }
                })
                .ToList();
        }

        /// <summary>
        /// Execute a tool by name after checking effective policy.
        /// </summary>
        /// <param name="toolCallId">Tool call identifier.</param>
        /// <param name="toolName">Tool name.</param>
        /// <param name="arguments">Parsed arguments.</param>
        /// <param name="context">Execution context.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Tool result.</returns>
        public async Task<ToolResult> ExecuteAsync(string toolCallId, string toolName, JsonElement arguments, ToolExecutionContext context, CancellationToken token)
        {
            ToolDescriptor? descriptor = GetTool(toolName);
            if (descriptor == null) return ToolResultFactory.Error(toolCallId, "unknown_tool", "Tool '" + toolName + "' is not registered.");
            if (!descriptor.Available) return ToolResultFactory.Error(toolCallId, "tool_unavailable", descriptor.UnavailableReason ?? "Tool is unavailable.");

            context.Settings = _Settings;
            int timeoutMs = context.SafetyLimits.ToolTimeoutMs;
            using CancellationTokenSource timeoutSource = new CancellationTokenSource(timeoutMs);
            using CancellationTokenSource linkedSource = CancellationTokenSource.CreateLinkedTokenSource(token, timeoutSource.Token);
            Task<ToolResult> execution = _Registry.ExecuteAsync(toolCallId, toolName, arguments, context, linkedSource.Token);
            Task timeout = Task.Delay(Timeout.InfiniteTimeSpan, linkedSource.Token);
            Task completed = await Task.WhenAny(execution, timeout).ConfigureAwait(false);
            if (completed == execution) return await execution.ConfigureAwait(false);
            if (token.IsCancellationRequested)
            {
                Task cleanupTimeout = Task.Delay(TimeSpan.FromMilliseconds(5000), CancellationToken.None);
                Task cleanupCompleted = await Task.WhenAny(execution, cleanupTimeout).ConfigureAwait(false);
                if (cleanupCompleted == execution) return await execution.ConfigureAwait(false);
                token.ThrowIfCancellationRequested();
            }

            return ToolResultFactory.Error(toolCallId, "tool_timed_out", "Tool execution exceeded the configured timeout.");
        }
    }
}
