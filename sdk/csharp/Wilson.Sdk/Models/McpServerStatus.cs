namespace Wilson.Sdk.Models
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Redacted MCP server status.
    /// </summary>
    public sealed class McpServerStatus
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = String.Empty;
        [JsonPropertyName("transport")]
        public string Transport { get; set; } = String.Empty;
        [JsonPropertyName("enabled")]
        public bool Enabled { get; set; }
        [JsonPropertyName("connected")]
        public bool Connected { get; set; }
        [JsonPropertyName("toolCount")]
        public int ToolCount { get; set; }
        [JsonPropertyName("tools")]
        public List<string> Tools { get; set; } = new List<string>();
        [JsonPropertyName("error")]
        public string? Error { get; set; }
        [JsonPropertyName("lastAttemptUtc")]
        public DateTime? LastAttemptUtc { get; set; }
    }
}
