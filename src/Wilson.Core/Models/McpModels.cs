namespace Wilson.Core.Models
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Redacted MCP server connection status.
    /// </summary>
    public sealed class McpServerStatus
    {
        /// <summary>Configured server name.</summary>
        public string Name { get; set; } = String.Empty;
        /// <summary>Transport name.</summary>
        public string Transport { get; set; } = String.Empty;
        /// <summary>Whether this server is enabled in settings.</summary>
        public bool Enabled { get; set; } = false;
        /// <summary>Whether Wilson is currently connected.</summary>
        public bool Connected { get; set; } = false;
        /// <summary>Number of discovered tools.</summary>
        public int ToolCount { get; set; } = 0;
        /// <summary>Discovered model-facing tool names.</summary>
        public List<string> Tools { get; set; } = new List<string>();
        /// <summary>Safe connection or discovery error.</summary>
        public string? Error { get; set; } = null;
        /// <summary>UTC timestamp for the latest connection attempt.</summary>
        public DateTime? LastAttemptUtc { get; set; } = null;
    }

    /// <summary>
    /// Redacted MCP status response.
    /// </summary>
    public sealed class McpStatusResponse
    {
        /// <summary>Whether MCP is enabled globally.</summary>
        public bool Enabled { get; set; } = false;
        /// <summary>Configured server count.</summary>
        public int ConfiguredServerCount { get; set; } = 0;
        /// <summary>Connected server count.</summary>
        public int ConnectedServerCount { get; set; } = 0;
        /// <summary>Discovered tool count.</summary>
        public int ToolCount { get; set; } = 0;
        /// <summary>Per-server redacted status.</summary>
        public List<McpServerStatus> Servers { get; set; } = new List<McpServerStatus>();
    }
}
