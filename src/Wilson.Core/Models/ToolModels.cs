namespace Wilson.Core.Models
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json.Serialization;
    using Wilson.Core.Helpers;

    /// <summary>
    /// Model-facing tool definition.
    /// </summary>
    public class ToolDefinition
    {
        /// <summary>Stable tool name.</summary>
        public string Name { get; set; } = String.Empty;
        /// <summary>Tool description.</summary>
        public string Description { get; set; } = String.Empty;
        /// <summary>JSON-schema-compatible parameter schema.</summary>
        public object? ParametersSchema { get; set; } = null;
        /// <summary>Tool category.</summary>
        public string Category { get; set; } = String.Empty;
        /// <summary>Whether the tool is built into Wilson.</summary>
        public bool BuiltIn { get; set; } = false;
        /// <summary>Whether the tool requires approval.</summary>
        public bool RequiresApproval { get; set; } = false;
        /// <summary>Whether the tool can change external state or run code.</summary>
        public bool Dangerous { get; set; } = false;
        /// <summary>Whether the tool is enabled.</summary>
        public bool Enabled { get; set; } = false;
    }

    /// <summary>
    /// Effective tool availability descriptor.
    /// </summary>
    public class ToolDescriptor
    {
        /// <summary>Stable tool name.</summary>
        public string Name { get; set; } = String.Empty;
        /// <summary>Human-readable display name.</summary>
        public string DisplayName { get; set; } = String.Empty;
        /// <summary>Tool category.</summary>
        public string Category { get; set; } = String.Empty;
        /// <summary>Whether policy enables this tool.</summary>
        public bool EnabledByPolicy { get; set; } = false;
        /// <summary>Whether prerequisites are satisfied and the tool can be exposed.</summary>
        public bool Available { get; set; } = false;
        /// <summary>Non-secret unavailable reason.</summary>
        public string? UnavailableReason { get; set; } = null;
        /// <summary>Whether execution requires approval.</summary>
        public bool RequiresApproval { get; set; } = false;
        /// <summary>Whether the tool is dangerous.</summary>
        public bool Dangerous { get; set; } = false;
    }

    /// <summary>
    /// OpenAI-compatible model-facing tool definition.
    /// </summary>
    public class ModelToolDefinition
    {
        /// <summary>Tool type.</summary>
        [JsonPropertyName("type")]
        public string Type { get; set; } = "function";
        /// <summary>Function definition.</summary>
        [JsonPropertyName("function")]
        public ModelToolFunctionDefinition? Function { get; set; } = null;
    }

    /// <summary>
    /// OpenAI-compatible model-facing function definition.
    /// </summary>
    public class ModelToolFunctionDefinition
    {
        /// <summary>Function name.</summary>
        [JsonPropertyName("name")]
        public string Name { get; set; } = String.Empty;
        /// <summary>Function description.</summary>
        [JsonPropertyName("description")]
        public string Description { get; set; } = String.Empty;
        /// <summary>JSON-schema-compatible parameters.</summary>
        [JsonPropertyName("parameters")]
        public object? Parameters { get; set; } = null;
    }

    /// <summary>
    /// Provider-neutral representation of a model-requested tool call.
    /// </summary>
    public class ModelToolCall
    {
        /// <summary>Provider-supplied tool-call identifier.</summary>
        [JsonPropertyName("id")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Id { get; set; } = null;
        /// <summary>Tool call type.</summary>
        [JsonPropertyName("type")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string Type { get; set; } = "function";
        /// <summary>Function call payload.</summary>
        [JsonPropertyName("function")]
        public ModelToolFunctionCall? Function { get; set; } = null;
    }

    /// <summary>
    /// Provider-neutral representation of a model-requested function call.
    /// </summary>
    public class ModelToolFunctionCall
    {
        /// <summary>Function name.</summary>
        [JsonPropertyName("name")]
        public string Name { get; set; } = String.Empty;
        /// <summary>Raw JSON arguments supplied by the model.</summary>
        [JsonPropertyName("arguments")]
        [JsonConverter(typeof(JsonStringOrRawJsonConverter))]
        public string Arguments { get; set; } = "{}";
    }

    /// <summary>
    /// Internal tool call state.
    /// </summary>
    public class ToolCall
    {
        /// <summary>Internal record identifier.</summary>
        public string Id { get; set; } = IdGenerator.ToolCall();
        /// <summary>Tool name.</summary>
        public string Name { get; set; } = String.Empty;
        /// <summary>Parsed argument payload when available.</summary>
        public object? Arguments { get; set; } = null;
        /// <summary>Raw JSON arguments supplied by the model.</summary>
        public string ArgumentsJson { get; set; } = "{}";
        /// <summary>Tool status.</summary>
        public string Status { get; set; } = ToolStatuses.Proposed;
        /// <summary>UTC creation timestamp.</summary>
        public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Tool execution result.
    /// </summary>
    public class ToolResult
    {
        /// <summary>Tool call identifier.</summary>
        public string ToolCallId { get; set; } = String.Empty;
        /// <summary>Whether execution succeeded.</summary>
        public bool Success { get; set; } = false;
        /// <summary>Model-visible content.</summary>
        public string Content { get; set; } = String.Empty;
        /// <summary>Model-visible JSON content.</summary>
        public string ContentJson { get; set; } = "{}";
        /// <summary>Stable error code.</summary>
        public string? ErrorCode { get; set; } = null;
        /// <summary>Safe error message.</summary>
        public string? ErrorMessage { get; set; } = null;
        /// <summary>Whether output was truncated.</summary>
        public bool Truncated { get; set; } = false;
        /// <summary>Output size in bytes.</summary>
        public int OutputBytes { get; set; } = 0;
    }

    /// <summary>
    /// Persistent tool execution record.
    /// </summary>
    public class ToolExecutionRecord
    {
        /// <summary>Record identifier.</summary>
        public string Id { get; set; } = IdGenerator.ToolExecution();
        /// <summary>Tenant identifier.</summary>
        public string TenantId { get; set; } = String.Empty;
        /// <summary>User identifier.</summary>
        public string UserId { get; set; } = String.Empty;
        /// <summary>Conversation identifier.</summary>
        public string ConversationId { get; set; } = String.Empty;
        /// <summary>Tool run identifier.</summary>
        public string RunId { get; set; } = String.Empty;
        /// <summary>Request history identifier.</summary>
        public string? RequestHistoryId { get; set; } = null;
        /// <summary>Trace identifier.</summary>
        public string? TraceId { get; set; } = null;
        /// <summary>Request origin.</summary>
        public string? Origin { get; set; } = null;
        /// <summary>Assistant message identifier.</summary>
        public string? AssistantMessageId { get; set; } = null;
        /// <summary>Provider-supplied tool-call identifier.</summary>
        public string? ProviderToolCallId { get; set; } = null;
        /// <summary>Internal tool-call identifier.</summary>
        public string ToolCallId { get; set; } = String.Empty;
        /// <summary>Tool name.</summary>
        public string ToolName { get; set; } = String.Empty;
        /// <summary>One-based tool-loop iteration.</summary>
        public int Iteration { get; set; } = 0;
        /// <summary>One-based sequence number in the turn.</summary>
        public int SequenceNumber { get; set; } = 0;
        /// <summary>Status.</summary>
        public string Status { get; set; } = ToolStatuses.Proposed;
        /// <summary>Approval policy.</summary>
        public string ApprovalPolicy { get; set; } = ToolApprovalPolicies.Ask;
        /// <summary>User who approved execution.</summary>
        public string? ApprovedByUserId { get; set; } = null;
        /// <summary>Redacted JSON arguments.</summary>
        public string ArgumentsJson { get; set; } = "{}";
        /// <summary>Redacted JSON result.</summary>
        public string ResultJson { get; set; } = "{}";
        /// <summary>Redacted compact result summary JSON.</summary>
        public string ResultSummaryJson { get; set; } = "{}";
        /// <summary>Short result preview.</summary>
        public string ResultPreview { get; set; } = String.Empty;
        /// <summary>Whether execution succeeded.</summary>
        public bool Success { get; set; } = false;
        /// <summary>Whether execution was denied.</summary>
        public bool Denied { get; set; } = false;
        /// <summary>Whether output was truncated.</summary>
        public bool Truncated { get; set; } = false;
        /// <summary>Output character count.</summary>
        public int OutputCharacters { get; set; } = 0;
        /// <summary>Redacted input byte count.</summary>
        public int InputBytes { get; set; } = 0;
        /// <summary>Redacted output byte count.</summary>
        public int OutputBytes { get; set; } = 0;
        /// <summary>Stable error type.</summary>
        public string? ErrorType { get; set; } = null;
        /// <summary>Stable error code.</summary>
        public string? ErrorCode { get; set; } = null;
        /// <summary>Safe error message.</summary>
        public string? ErrorMessage { get; set; } = null;
        /// <summary>Inference provider.</summary>
        public string? Provider { get; set; } = null;
        /// <summary>Model identifier.</summary>
        public string? Model { get; set; } = null;
        /// <summary>UTC start timestamp.</summary>
        public DateTime StartedUtc { get; set; } = DateTime.UtcNow;
        /// <summary>UTC completion timestamp.</summary>
        public DateTime? CompletedUtc { get; set; } = null;
        /// <summary>Elapsed milliseconds.</summary>
        public double ElapsedMs { get; set; } = 0;
        /// <summary>Active flag used by retention and soft-delete flows.</summary>
        public bool Active { get; set; } = true;
        /// <summary>UTC creation timestamp.</summary>
        public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
        /// <summary>UTC update timestamp.</summary>
        public DateTime UpdatedUtc { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Tool run metadata.
    /// </summary>
    public class ToolRun
    {
        /// <summary>Run identifier.</summary>
        public string RunId { get; set; } = IdGenerator.ToolRun();
        /// <summary>Tenant identifier.</summary>
        public string TenantId { get; set; } = String.Empty;
        /// <summary>User identifier.</summary>
        public string UserId { get; set; } = String.Empty;
        /// <summary>Conversation identifier.</summary>
        public string ConversationId { get; set; } = String.Empty;
        /// <summary>Runner identifier.</summary>
        public string RunnerId { get; set; } = String.Empty;
        /// <summary>Model identifier.</summary>
        public string Model { get; set; } = String.Empty;
        /// <summary>Run status.</summary>
        public string Status { get; set; } = ToolStatuses.Proposed;
        /// <summary>UTC start timestamp.</summary>
        public DateTime StartedUtc { get; set; } = DateTime.UtcNow;
        /// <summary>UTC completion timestamp.</summary>
        public DateTime? CompletedUtc { get; set; } = null;
        /// <summary>Elapsed milliseconds.</summary>
        public double ElapsedMs { get; set; } = 0;
        /// <summary>Iterations completed.</summary>
        public int IterationCount { get; set; } = 0;
        /// <summary>Tool calls processed.</summary>
        public int ToolCallCount { get; set; } = 0;
        /// <summary>Errors encountered.</summary>
        public int ErrorCount { get; set; } = 0;
        /// <summary>UTC creation timestamp.</summary>
        public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Safe user-visible tool progress event.
    /// </summary>
    public class ToolProgressEvent
    {
        /// <summary>Stable event type.</summary>
        public string EventType { get; set; } = String.Empty;
        /// <summary>Tool-call identifier.</summary>
        public string? ToolCallId { get; set; } = null;
        /// <summary>Stable tool name.</summary>
        public string? ToolName { get; set; } = null;
        /// <summary>User-facing display label.</summary>
        public string? DisplayLabel { get; set; } = null;
        /// <summary>Stable status code.</summary>
        public string? StatusCode { get; set; } = null;
        /// <summary>One-based iteration.</summary>
        public int Iteration { get; set; } = 0;
        /// <summary>One-based sequence number.</summary>
        public int SequenceNumber { get; set; } = 0;
        /// <summary>UTC start timestamp.</summary>
        public DateTime? StartedUtc { get; set; } = null;
        /// <summary>UTC completion timestamp.</summary>
        public DateTime? CompletedUtc { get; set; } = null;
        /// <summary>Elapsed milliseconds.</summary>
        public double? ElapsedMs { get; set; } = null;
        /// <summary>Safe result count.</summary>
        public int? ResultCount { get; set; } = null;
        /// <summary>Whether output was truncated.</summary>
        public bool? Truncated { get; set; } = null;
        /// <summary>Whether execution was denied.</summary>
        public bool? Denied { get; set; } = null;
        /// <summary>Whether execution succeeded.</summary>
        public bool? Success { get; set; } = null;
        /// <summary>Safe summary.</summary>
        public string? Summary { get; set; } = null;
    }

    /// <summary>
    /// Safe chat response tool trace.
    /// </summary>
    public class ToolTrace
    {
        /// <summary>Tool-call identifier.</summary>
        public string? ToolCallId { get; set; } = null;
        /// <summary>Stable tool name.</summary>
        public string ToolName { get; set; } = String.Empty;
        /// <summary>User-facing display label.</summary>
        public string DisplayLabel { get; set; } = String.Empty;
        /// <summary>One-based iteration.</summary>
        public int Iteration { get; set; } = 0;
        /// <summary>One-based sequence number.</summary>
        public int SequenceNumber { get; set; } = 0;
        /// <summary>Whether execution succeeded.</summary>
        public bool Success { get; set; } = false;
        /// <summary>Whether execution was denied.</summary>
        public bool Denied { get; set; } = false;
        /// <summary>Whether output was truncated.</summary>
        public bool Truncated { get; set; } = false;
        /// <summary>Output character count.</summary>
        public int OutputCharacters { get; set; } = 0;
        /// <summary>Safe result count.</summary>
        public int? ResultCount { get; set; } = null;
        /// <summary>Elapsed milliseconds.</summary>
        public double ElapsedMs { get; set; } = 0;
        /// <summary>Safe summary.</summary>
        public string? Summary { get; set; } = null;
        /// <summary>UTC start timestamp.</summary>
        public DateTime? StartedUtc { get; set; } = null;
        /// <summary>UTC completion timestamp.</summary>
        public DateTime? CompletedUtc { get; set; } = null;
    }

    /// <summary>
    /// Internal tool audit trace used to build redacted persistent audit records.
    /// </summary>
    public class ToolAuditTrace
    {
        /// <summary>Provider-supplied tool-call identifier.</summary>
        public string? ProviderToolCallId { get; set; } = null;
        /// <summary>Stable tool name.</summary>
        public string ToolName { get; set; } = String.Empty;
        /// <summary>User-facing display label.</summary>
        public string DisplayLabel { get; set; } = String.Empty;
        /// <summary>One-based iteration.</summary>
        public int Iteration { get; set; } = 0;
        /// <summary>One-based sequence number.</summary>
        public int SequenceNumber { get; set; } = 0;
        /// <summary>Raw JSON arguments supplied by the model before audit redaction.</summary>
        public string ArgumentsJson { get; set; } = "{}";
        /// <summary>Raw JSON result generated by the tool before audit redaction.</summary>
        public string ResultJson { get; set; } = "{}";
        /// <summary>Whether execution succeeded.</summary>
        public bool Success { get; set; } = false;
        /// <summary>Whether execution was denied.</summary>
        public bool Denied { get; set; } = false;
        /// <summary>Whether output was truncated.</summary>
        public bool Truncated { get; set; } = false;
        /// <summary>Output character count.</summary>
        public int OutputCharacters { get; set; } = 0;
        /// <summary>Safe result count.</summary>
        public int? ResultCount { get; set; } = null;
        /// <summary>Stable error code.</summary>
        public string? ErrorCode { get; set; } = null;
        /// <summary>Safe error message.</summary>
        public string? ErrorMessage { get; set; } = null;
        /// <summary>Elapsed milliseconds.</summary>
        public double ElapsedMs { get; set; } = 0;
        /// <summary>UTC start timestamp.</summary>
        public DateTime? StartedUtc { get; set; } = null;
        /// <summary>UTC completion timestamp.</summary>
        public DateTime? CompletedUtc { get; set; } = null;
    }

    /// <summary>
    /// Provider-neutral tool-capable inference request.
    /// </summary>
    public class ToolCapableInferenceRequest
    {
        /// <summary>Chat messages.</summary>
        public List<ModelChatMessage> Messages { get; set; } = new List<ModelChatMessage>();
        /// <summary>Model identifier.</summary>
        public string Model { get; set; } = String.Empty;
        /// <summary>Maximum completion tokens.</summary>
        public int MaxTokens { get; set; } = 0;
        /// <summary>Sampling temperature.</summary>
        public double Temperature { get; set; } = 0;
        /// <summary>Top-p sampling value.</summary>
        public double TopP { get; set; } = 1;
        /// <summary>Provider name.</summary>
        public string Provider { get; set; } = String.Empty;
        /// <summary>Provider endpoint.</summary>
        public string Endpoint { get; set; } = String.Empty;
        /// <summary>Provider API key.</summary>
        public string? ApiKey { get; set; } = null;
        /// <summary>Tool definitions.</summary>
        public List<ModelToolDefinition> Tools { get; set; } = new List<ModelToolDefinition>();
        /// <summary>Tool choice mode.</summary>
        public string ToolChoice { get; set; } = ToolChoiceModes.Auto;
    }

    /// <summary>
    /// Provider-neutral tool-capable inference response.
    /// </summary>
    public class ToolCapableInferenceResponse
    {
        /// <summary>Whether inference succeeded.</summary>
        public bool Success { get; set; } = false;
        /// <summary>Assistant content.</summary>
        public string? Content { get; set; } = null;
        /// <summary>Model-requested tool calls.</summary>
        public List<ModelToolCall> ToolCalls { get; set; } = new List<ModelToolCall>();
        /// <summary>Provider finish reason.</summary>
        public string? FinishReason { get; set; } = null;
        /// <summary>Error message.</summary>
        public string? ErrorMessage { get; set; } = null;
        /// <summary>Telemetry payload reserved for future provider timing data.</summary>
        public object? Telemetry { get; set; } = null;
    }

    /// <summary>
    /// Non-streaming tool agent loop result.
    /// </summary>
    public class ToolAgentResponse
    {
        /// <summary>Whether the loop produced a final assistant answer.</summary>
        public bool Success { get; set; } = false;
        /// <summary>Final assistant content.</summary>
        public string Content { get; set; } = String.Empty;
        /// <summary>Error message if the loop failed.</summary>
        public string? ErrorMessage { get; set; } = null;
        /// <summary>Final provider finish reason.</summary>
        public string? FinishReason { get; set; } = null;
        /// <summary>Completed model/tool iterations.</summary>
        public int IterationCount { get; set; } = 0;
        /// <summary>Tool calls processed.</summary>
        public int ToolCallCount { get; set; } = 0;
        /// <summary>Errors encountered while processing tools.</summary>
        public int ErrorCount { get; set; } = 0;
        /// <summary>Safe public tool traces.</summary>
        public List<ToolTrace> ToolCalls { get; set; } = new List<ToolTrace>();
        /// <summary>Internal audit tool traces.</summary>
        public List<ToolAuditTrace> AuditToolCalls { get; set; } = new List<ToolAuditTrace>();
        /// <summary>Conversation messages accumulated by the loop.</summary>
        public List<ModelChatMessage> Messages { get; set; } = new List<ModelChatMessage>();
    }

    /// <summary>
    /// Provider-neutral chat message for tool-capable transports.
    /// </summary>
    public class ModelChatMessage
    {
        /// <summary>Message role.</summary>
        [JsonPropertyName("role")]
        public string Role { get; set; } = String.Empty;
        /// <summary>Message content.</summary>
        [JsonPropertyName("content")]
        public string? Content { get; set; } = null;
        /// <summary>Assistant tool calls.</summary>
        [JsonPropertyName("tool_calls")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<ModelToolCall>? ToolCalls { get; set; } = null;
        /// <summary>Tool-call identifier answered by a tool message.</summary>
        [JsonPropertyName("tool_call_id")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? ToolCallId { get; set; } = null;
        /// <summary>Optional tool name for tool messages.</summary>
        [JsonPropertyName("name")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Name { get; set; } = null;
    }

    /// <summary>
    /// Tool status constants.
    /// </summary>
    public static class ToolStatuses
    {
        /// <summary>Proposed by model.</summary>
        public const string Proposed = "proposed";
        /// <summary>Waiting for approval.</summary>
        public const string PendingApproval = "pending_approval";
        /// <summary>Approved.</summary>
        public const string Approved = "approved";
        /// <summary>Running.</summary>
        public const string Running = "running";
        /// <summary>Completed.</summary>
        public const string Completed = "completed";
        /// <summary>Failed.</summary>
        public const string Failed = "failed";
        /// <summary>Denied.</summary>
        public const string Denied = "denied";
        /// <summary>Cancelled.</summary>
        public const string Cancelled = "cancelled";
        /// <summary>Timed out.</summary>
        public const string TimedOut = "timed_out";
    }

    /// <summary>
    /// Tool approval policy constants.
    /// </summary>
    public static class ToolApprovalPolicies
    {
        /// <summary>Deny all execution.</summary>
        public const string Deny = "deny";
        /// <summary>Ask before execution.</summary>
        public const string Ask = "ask";
        /// <summary>Run without interaction when safe.</summary>
        public const string Auto = "auto";
    }

    /// <summary>
    /// Tool category constants.
    /// </summary>
    public static class ToolCategories
    {
        /// <summary>Filesystem tools.</summary>
        public const string Filesystem = "filesystem";
        /// <summary>Process tools.</summary>
        public const string Process = "process";
        /// <summary>Web retrieval tools.</summary>
        public const string Web = "web";
        /// <summary>Search tools.</summary>
        public const string Search = "search";
        /// <summary>MCP tools.</summary>
        public const string Mcp = "mcp";
        /// <summary>Wilson product tools.</summary>
        public const string Wilson = "wilson";
        /// <summary>Custom tools.</summary>
        public const string Custom = "custom";
    }

    /// <summary>
    /// Tool choice mode constants.
    /// </summary>
    public static class ToolChoiceModes
    {
        /// <summary>Let the model decide.</summary>
        public const string Auto = "auto";
        /// <summary>Require a tool call.</summary>
        public const string Required = "required";
        /// <summary>Disable tools for the model request.</summary>
        public const string None = "none";
        /// <summary>Allow only configured tools.</summary>
        public const string AllowedOnly = "allowed_only";
    }

    /// <summary>
    /// Tool progress event type constants.
    /// </summary>
    public static class ToolEventTypes
    {
        /// <summary>Tool iteration started.</summary>
        public const string ToolIterationStarted = "tool_iteration.started";
        /// <summary>Tool iteration stopped.</summary>
        public const string ToolIterationStopped = "tool_iteration.stopped";
        /// <summary>Tool call started.</summary>
        public const string ToolCallStarted = "tool_call.started";
        /// <summary>Tool call heartbeat.</summary>
        public const string ToolCallHeartbeat = "tool_call.heartbeat";
        /// <summary>Tool call completed.</summary>
        public const string ToolCallCompleted = "tool_call.completed";
        /// <summary>Tool call failed.</summary>
        public const string ToolCallFailed = "tool_call.failed";
        /// <summary>Tool call denied.</summary>
        public const string ToolCallDenied = "tool_call.denied";
    }
}
