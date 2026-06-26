namespace Wilson.Sdk.Models
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// Aggregate tool metrics returned on chat responses.
    /// </summary>
    public sealed class ChatToolMetrics
    {
        /// <summary>Whether tools were enabled for the response.</summary>
        [JsonPropertyName("toolsEnabled")]
        public bool ToolsEnabled { get; set; }

        /// <summary>Tool call count.</summary>
        [JsonPropertyName("toolCallCount")]
        public int ToolCallCount { get; set; }

        /// <summary>Tool error count.</summary>
        [JsonPropertyName("errorCount")]
        public int ErrorCount { get; set; }

        /// <summary>Tool loop iteration count.</summary>
        [JsonPropertyName("iterationCount")]
        public int IterationCount { get; set; }

        /// <summary>Total elapsed milliseconds spent in tools.</summary>
        [JsonPropertyName("totalToolElapsedMs")]
        public double TotalToolElapsedMs { get; set; }
    }
}
