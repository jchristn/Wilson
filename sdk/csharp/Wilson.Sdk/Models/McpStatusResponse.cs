namespace Wilson.Sdk.Models
{
    using System.Collections.Generic;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Redacted MCP status response.
    /// </summary>
    public sealed class McpStatusResponse
    {
        [JsonPropertyName("enabled")]
        public bool Enabled { get; set; }
        [JsonPropertyName("configuredServerCount")]
        public int ConfiguredServerCount { get; set; }
        [JsonPropertyName("connectedServerCount")]
        public int ConnectedServerCount { get; set; }
        [JsonPropertyName("toolCount")]
        public int ToolCount { get; set; }
        [JsonPropertyName("servers")]
        public List<McpServerStatus> Servers { get; set; } = new List<McpServerStatus>();
    }
}
