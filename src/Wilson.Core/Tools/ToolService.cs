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
        private readonly McpToolManager? _McpToolManager;
        private readonly ToolPolicyResolver _PolicyResolver = new ToolPolicyResolver();

        /// <summary>
        /// Instantiate the tool service.
        /// </summary>
        /// <param name="settings">Wilson settings.</param>
        /// <param name="mcpToolManager">Optional MCP tool manager.</param>
        public ToolService(Settings settings, McpToolManager? mcpToolManager = null)
        {
            _Settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _Registry = new BuiltInToolRegistry();
            _McpToolManager = mcpToolManager;
        }

        /// <summary>
        /// List effective tool descriptors.
        /// </summary>
        /// <param name="includeDisabled">Whether to include unavailable disabled descriptors.</param>
        /// <returns>Tool descriptors.</returns>
        public List<ToolDescriptor> ListTools(bool includeDisabled = true)
        {
            List<ToolDescriptor> descriptors = _PolicyResolver.Resolve(_Settings, _Registry.Executors, includeDisabled);
            descriptors.AddRange(ResolveMcpDescriptors(includeDisabled));
            return descriptors.OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase).ToList();
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

            List<ModelToolDefinition> definitions = _Registry.GetToolDefinitions()
                .Concat(_McpToolManager?.GetToolDefinitions() ?? new List<ToolDefinition>())
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
            return definitions;
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
            Task<ToolResult> execution = String.Equals(descriptor.Category, ToolCategories.Mcp, StringComparison.OrdinalIgnoreCase)
                ? _McpToolManager!.ExecuteAsync(toolCallId, toolName, arguments, context, linkedSource.Token)
                : _Registry.ExecuteAsync(toolCallId, toolName, arguments, context, linkedSource.Token);
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

        private List<ToolDescriptor> ResolveMcpDescriptors(bool includeDisabled)
        {
            ToolsSettings tools = _Settings.Tools ?? new ToolsSettings();
            List<ToolDefinition> definitions = _McpToolManager?.GetToolDefinitions() ?? new List<ToolDefinition>();
            HashSet<string> enabledNames = new HashSet<string>(tools.EnabledToolNames ?? new List<string>(), StringComparer.OrdinalIgnoreCase);
            HashSet<string> disabledNames = new HashSet<string>(tools.DisabledToolNames ?? new List<string>(), StringComparer.OrdinalIgnoreCase);
            List<ToolDescriptor> descriptors = new List<ToolDescriptor>();

            foreach (ToolDefinition definition in definitions)
            {
                bool enabledByPolicy = tools.Enabled
                    && tools.Mcp != null
                    && tools.Mcp.Enabled
                    && !disabledNames.Contains(definition.Name)
                    && (enabledNames.Count == 0 || enabledNames.Contains(definition.Name));

                string? unavailableReason = null;
                if (!tools.Enabled) unavailableReason = "Tools are disabled.";
                else if (tools.Mcp == null || !tools.Mcp.Enabled) unavailableReason = "MCP tools are disabled.";
                else if (_McpToolManager == null || !_McpToolManager.HasTool(definition.Name)) unavailableReason = "MCP tool is not connected.";
                else if (disabledNames.Contains(definition.Name)) unavailableReason = "Tool is disabled by name.";
                else if (enabledNames.Count > 0 && !enabledNames.Contains(definition.Name)) unavailableReason = "Tool is not in the enabled tool list.";

                bool available = enabledByPolicy && String.IsNullOrWhiteSpace(unavailableReason);
                ToolDescriptor descriptor = new ToolDescriptor
                {
                    Name = definition.Name,
                    DisplayName = DisplayName(definition.Name),
                    Category = ToolCategories.Mcp,
                    EnabledByPolicy = enabledByPolicy,
                    Available = available,
                    UnavailableReason = available ? null : unavailableReason,
                    RequiresApproval = definition.RequiresApproval,
                    Dangerous = definition.Dangerous
                };

                if (includeDisabled || descriptor.Available) descriptors.Add(descriptor);
            }

            return descriptors;
        }

        private static string DisplayName(string name)
        {
            string[] parts = name.Split(new[] { '_', '-' }, StringSplitOptions.RemoveEmptyEntries);
            return String.Join(" ", parts.Select(part => Char.ToUpperInvariant(part[0]) + part.Substring(1)));
        }
    }
}
