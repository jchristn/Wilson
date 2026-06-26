namespace Wilson.Sdk.Models
{
    using System.Collections.Generic;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Tool run detail response.
    /// </summary>
    public sealed class ToolRunResponse
    {
        /// <summary>Tool run metadata.</summary>
        [JsonPropertyName("toolRun")]
        public ToolRun? ToolRun { get; set; }

        /// <summary>Redacted tool-call records associated with the run.</summary>
        [JsonPropertyName("toolCalls")]
        public List<ToolExecutionRecord> ToolCalls { get; set; } = new List<ToolExecutionRecord>();
    }
}
