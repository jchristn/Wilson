namespace Wilson.Sdk.Models
{
    using System;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Redacted persisted tool-call record.
    /// </summary>
    public sealed class ToolExecutionRecord
    {
        /// <summary>Record identifier.</summary>
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        /// <summary>Tenant identifier.</summary>
        [JsonPropertyName("tenantId")]
        public string TenantId { get; set; } = string.Empty;

        /// <summary>User identifier.</summary>
        [JsonPropertyName("userId")]
        public string UserId { get; set; } = string.Empty;

        /// <summary>Conversation identifier.</summary>
        [JsonPropertyName("conversationId")]
        public string ConversationId { get; set; } = string.Empty;

        /// <summary>Tool run identifier.</summary>
        [JsonPropertyName("runId")]
        public string RunId { get; set; } = string.Empty;

        /// <summary>Request-history identifier.</summary>
        [JsonPropertyName("requestHistoryId")]
        public string? RequestHistoryId { get; set; }

        /// <summary>Assistant message identifier.</summary>
        [JsonPropertyName("assistantMessageId")]
        public string? AssistantMessageId { get; set; }

        /// <summary>Wilson tool-call identifier.</summary>
        [JsonPropertyName("toolCallId")]
        public string ToolCallId { get; set; } = string.Empty;

        /// <summary>Tool name.</summary>
        [JsonPropertyName("toolName")]
        public string ToolName { get; set; } = string.Empty;

        /// <summary>Tool-agent iteration.</summary>
        [JsonPropertyName("iteration")]
        public int Iteration { get; set; }

        /// <summary>Sequence number within the chat turn.</summary>
        [JsonPropertyName("sequenceNumber")]
        public int SequenceNumber { get; set; }

        /// <summary>Execution status.</summary>
        [JsonPropertyName("status")]
        public string Status { get; set; } = string.Empty;

        /// <summary>Approval policy used for the call.</summary>
        [JsonPropertyName("approvalPolicy")]
        public string ApprovalPolicy { get; set; } = string.Empty;

        /// <summary>Redacted argument JSON.</summary>
        [JsonPropertyName("argumentsJson")]
        public string ArgumentsJson { get; set; } = "{}";

        /// <summary>Redacted result JSON.</summary>
        [JsonPropertyName("resultJson")]
        public string ResultJson { get; set; } = "{}";

        /// <summary>Redacted compact result summary JSON.</summary>
        [JsonPropertyName("resultSummaryJson")]
        public string ResultSummaryJson { get; set; } = "{}";

        /// <summary>Short safe result preview.</summary>
        [JsonPropertyName("resultPreview")]
        public string ResultPreview { get; set; } = string.Empty;

        /// <summary>Whether execution succeeded.</summary>
        [JsonPropertyName("success")]
        public bool Success { get; set; }

        /// <summary>Whether execution was denied.</summary>
        [JsonPropertyName("denied")]
        public bool Denied { get; set; }

        /// <summary>Whether output was truncated.</summary>
        [JsonPropertyName("truncated")]
        public bool Truncated { get; set; }

        /// <summary>Output character count.</summary>
        [JsonPropertyName("outputCharacters")]
        public int OutputCharacters { get; set; }

        /// <summary>Safe error message.</summary>
        [JsonPropertyName("errorMessage")]
        public string? ErrorMessage { get; set; }

        /// <summary>Model name.</summary>
        [JsonPropertyName("model")]
        public string? Model { get; set; }

        /// <summary>UTC start timestamp.</summary>
        [JsonPropertyName("startedUtc")]
        public DateTime StartedUtc { get; set; }

        /// <summary>UTC completion timestamp.</summary>
        [JsonPropertyName("completedUtc")]
        public DateTime? CompletedUtc { get; set; }

        /// <summary>Elapsed milliseconds.</summary>
        [JsonPropertyName("elapsedMs")]
        public double ElapsedMs { get; set; }
    }
}
