namespace Wilson.Core.Tools
{
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using Wilson.Core.Models;

    /// <summary>
    /// Executes one Wilson tool.
    /// </summary>
    public interface IToolExecutor
    {
        /// <summary>Stable model-facing tool name.</summary>
        string Name { get; }
        /// <summary>Human-readable description.</summary>
        string Description { get; }
        /// <summary>JSON-schema-compatible parameter schema.</summary>
        object ParametersSchema { get; }
        /// <summary>Tool category.</summary>
        string Category { get; }
        /// <summary>Whether the tool requires approval.</summary>
        bool RequiresApproval { get; }
        /// <summary>Whether the tool can change external state or run code.</summary>
        bool Dangerous { get; }

        /// <summary>
        /// Execute the tool.
        /// </summary>
        /// <param name="toolCallId">Tool call identifier.</param>
        /// <param name="arguments">Parsed model arguments.</param>
        /// <param name="context">Execution context.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Tool result.</returns>
        Task<ToolResult> ExecuteAsync(string toolCallId, JsonElement arguments, ToolExecutionContext context, CancellationToken token);
    }
}
