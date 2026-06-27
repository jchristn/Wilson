namespace Wilson.Sdk.Models
{
    using System.Collections.Generic;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Non-streaming Wilson chat request.
    /// </summary>
    public sealed class ChatRequest
    {
        /// <summary>Conversation identifier. Leave empty to create a conversation.</summary>
        [JsonPropertyName("conversationId")]
        public string? ConversationId { get; set; }

        /// <summary>Model runner identifier.</summary>
        [JsonPropertyName("runnerId")]
        public string RunnerId { get; set; } = string.Empty;

        /// <summary>Model name.</summary>
        [JsonPropertyName("model")]
        public string Model { get; set; } = string.Empty;

        /// <summary>User prompt.</summary>
        [JsonPropertyName("prompt")]
        public string Prompt { get; set; } = string.Empty;

        /// <summary>Selected system prompt template identifier. Leave empty to use the tenant default.</summary>
        [JsonPropertyName("systemPromptId")]
        public string? SystemPromptId { get; set; }

        /// <summary>Selected tool prompt template identifier. Leave empty to use the tenant default when tools are enabled.</summary>
        [JsonPropertyName("toolPromptId")]
        public string? ToolPromptId { get; set; }

        /// <summary>Completion settings object.</summary>
        [JsonPropertyName("settings")]
        public object? Settings { get; set; }

        /// <summary>Override whether tools are enabled for this request.</summary>
        [JsonPropertyName("toolsEnabled")]
        public bool? ToolsEnabled { get; set; }

        /// <summary>Override approval policy for this request. Use deny, ask, or auto.</summary>
        [JsonPropertyName("approvalPolicy")]
        public string? ApprovalPolicy { get; set; }

        /// <summary>Optional tool allow-list for this request.</summary>
        [JsonPropertyName("toolNames")]
        public List<string> ToolNames { get; set; } = new List<string>();

        /// <summary>Optional working directory override. Requires administrator access.</summary>
        [JsonPropertyName("workingDirectory")]
        public string? WorkingDirectory { get; set; }
    }
}
