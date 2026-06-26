namespace Wilson.Sdk.Models
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// Tool approval response.
    /// </summary>
    public sealed class ToolApprovalResponse
    {
        /// <summary>Updated tool-call audit record.</summary>
        [JsonPropertyName("toolCall")]
        public ToolExecutionRecord? ToolCall { get; set; }
    }
}
