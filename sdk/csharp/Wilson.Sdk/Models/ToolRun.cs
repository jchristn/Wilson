namespace Wilson.Sdk.Models
{
    using System;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Tool run metadata.
    /// </summary>
    public sealed class ToolRun
    {
        /// <summary>Tool run identifier.</summary>
        [JsonPropertyName("runId")]
        public string RunId { get; set; } = string.Empty;

        /// <summary>Tenant identifier.</summary>
        [JsonPropertyName("tenantId")]
        public string TenantId { get; set; } = string.Empty;

        /// <summary>User identifier.</summary>
        [JsonPropertyName("userId")]
        public string UserId { get; set; } = string.Empty;

        /// <summary>Conversation identifier.</summary>
        [JsonPropertyName("conversationId")]
        public string ConversationId { get; set; } = string.Empty;

        /// <summary>Model runner identifier.</summary>
        [JsonPropertyName("runnerId")]
        public string RunnerId { get; set; } = string.Empty;

        /// <summary>Model name.</summary>
        [JsonPropertyName("model")]
        public string Model { get; set; } = string.Empty;

        /// <summary>Run status.</summary>
        [JsonPropertyName("status")]
        public string Status { get; set; } = string.Empty;

        /// <summary>UTC start timestamp.</summary>
        [JsonPropertyName("startedUtc")]
        public DateTime StartedUtc { get; set; }

        /// <summary>UTC completion timestamp.</summary>
        [JsonPropertyName("completedUtc")]
        public DateTime? CompletedUtc { get; set; }

        /// <summary>Elapsed milliseconds.</summary>
        [JsonPropertyName("elapsedMs")]
        public double ElapsedMs { get; set; }

        /// <summary>Tool-agent iteration count.</summary>
        [JsonPropertyName("iterationCount")]
        public int IterationCount { get; set; }

        /// <summary>Tool call count.</summary>
        [JsonPropertyName("toolCallCount")]
        public int ToolCallCount { get; set; }

        /// <summary>Error count.</summary>
        [JsonPropertyName("errorCount")]
        public int ErrorCount { get; set; }

        /// <summary>UTC creation timestamp.</summary>
        [JsonPropertyName("createdUtc")]
        public DateTime CreatedUtc { get; set; }
    }
}
