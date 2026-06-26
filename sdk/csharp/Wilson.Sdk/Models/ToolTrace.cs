namespace Wilson.Sdk.Models
{
    using System;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Safe public tool trace returned on chat responses.
    /// </summary>
    public sealed class ToolTrace
    {
        /// <summary>Wilson-generated tool-call identifier.</summary>
        [JsonPropertyName("toolCallId")]
        public string? ToolCallId { get; set; }

        /// <summary>Stable tool name.</summary>
        [JsonPropertyName("toolName")]
        public string ToolName { get; set; } = string.Empty;

        /// <summary>User-facing display label.</summary>
        [JsonPropertyName("displayLabel")]
        public string DisplayLabel { get; set; } = string.Empty;

        /// <summary>One-based iteration.</summary>
        [JsonPropertyName("iteration")]
        public int Iteration { get; set; }

        /// <summary>One-based sequence number.</summary>
        [JsonPropertyName("sequenceNumber")]
        public int SequenceNumber { get; set; }

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

        /// <summary>Safe result count.</summary>
        [JsonPropertyName("resultCount")]
        public int? ResultCount { get; set; }

        /// <summary>Elapsed milliseconds.</summary>
        [JsonPropertyName("elapsedMs")]
        public double ElapsedMs { get; set; }

        /// <summary>Safe summary.</summary>
        [JsonPropertyName("summary")]
        public string? Summary { get; set; }

        /// <summary>UTC start timestamp.</summary>
        [JsonPropertyName("startedUtc")]
        public DateTime? StartedUtc { get; set; }

        /// <summary>UTC completion timestamp.</summary>
        [JsonPropertyName("completedUtc")]
        public DateTime? CompletedUtc { get; set; }
    }
}
