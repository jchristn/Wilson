namespace Wilson.Core.Tools
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using Wilson.Core.Models;
    using Wilson.Core.Tools.Tools;

    /// <summary>
    /// Wilson-owned registry of built-in tools.
    /// </summary>
    public sealed class BuiltInToolRegistry
    {
        private readonly Dictionary<string, IToolExecutor> _Tools = new Dictionary<string, IToolExecutor>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Instantiate the registry.
        /// </summary>
        public BuiltInToolRegistry()
        {
            Register(new ReadFileTool());
            Register(new WriteFileTool());
            Register(new EditFileTool());
            Register(new MultiEditTool());
            Register(new DeleteFileTool());
            Register(new FileMetadataTool());
            Register(new ListDirectoryTool());
            Register(new ManageDirectoryTool());
            Register(new GlobTool());
            Register(new GrepTool());
            Register(new RunProcessTool());
        }

        /// <summary>
        /// Registered executors.
        /// </summary>
        public IReadOnlyCollection<IToolExecutor> Executors => _Tools.Values;

        /// <summary>
        /// Get model-facing tool definitions.
        /// </summary>
        /// <returns>Tool definitions.</returns>
        public List<ToolDefinition> GetToolDefinitions()
        {
            return _Tools.Values
                .OrderBy(tool => tool.Name, StringComparer.OrdinalIgnoreCase)
                .Select(tool => new ToolDefinition
                {
                    Name = tool.Name,
                    Description = tool.Description,
                    ParametersSchema = tool.ParametersSchema,
                    Category = tool.Category,
                    BuiltIn = true,
                    RequiresApproval = tool.RequiresApproval,
                    Dangerous = tool.Dangerous,
                    Enabled = true
                })
                .ToList();
        }

        /// <summary>
        /// Return whether a tool exists.
        /// </summary>
        /// <param name="name">Tool name.</param>
        /// <returns>True when registered.</returns>
        public bool HasTool(string name)
        {
            return _Tools.ContainsKey(name);
        }

        /// <summary>
        /// Get one executor.
        /// </summary>
        /// <param name="name">Tool name.</param>
        /// <returns>Executor or null.</returns>
        public IToolExecutor? GetExecutor(string name)
        {
            if (String.IsNullOrWhiteSpace(name)) return null;
            return _Tools.TryGetValue(name, out IToolExecutor? executor) ? executor : null;
        }

        /// <summary>
        /// Execute a registered tool.
        /// </summary>
        /// <param name="toolCallId">Tool call identifier.</param>
        /// <param name="toolName">Tool name.</param>
        /// <param name="arguments">Parsed arguments.</param>
        /// <param name="context">Execution context.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Tool result.</returns>
        public Task<ToolResult> ExecuteAsync(string toolCallId, string toolName, JsonElement arguments, ToolExecutionContext context, CancellationToken token)
        {
            IToolExecutor? executor = GetExecutor(toolName);
            if (executor == null) return Task.FromResult(ToolResultFactory.Error(toolCallId, "unknown_tool", "Tool '" + toolName + "' is not registered."));
            return executor.ExecuteAsync(toolCallId, arguments, context, token);
        }

        private void Register(IToolExecutor executor)
        {
            _Tools[executor.Name] = executor;
        }
    }
}
