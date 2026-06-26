namespace Wilson.Sdk.Models
{
    using System.Collections.Generic;
    using System.Text.Json;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Non-streaming Wilson chat response.
    /// </summary>
    public sealed class ChatResponse
    {
        /// <summary>Conversation JSON object.</summary>
        [JsonPropertyName("conversation")]
        public JsonElement Conversation { get; set; }

        /// <summary>User message JSON object.</summary>
        [JsonPropertyName("userMessage")]
        public JsonElement UserMessage { get; set; }

        /// <summary>Assistant message JSON object.</summary>
        [JsonPropertyName("assistantMessage")]
        public JsonElement AssistantMessage { get; set; }

        /// <summary>Truncation notice JSON object.</summary>
        [JsonPropertyName("truncation")]
        public JsonElement Truncation { get; set; }

        /// <summary>Tool run metadata, when tools were used.</summary>
        [JsonPropertyName("toolRun")]
        public ToolRun? ToolRun { get; set; }

        /// <summary>Safe public tool traces, when tools were used.</summary>
        [JsonPropertyName("toolCalls")]
        public List<ToolTrace> ToolCalls { get; set; } = new List<ToolTrace>();

        /// <summary>Aggregate tool metrics, when tools were used.</summary>
        [JsonPropertyName("toolMetrics")]
        public ChatToolMetrics? ToolMetrics { get; set; }
    }
}
