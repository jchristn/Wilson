namespace Wilson.Core.Settings
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Tool execution settings.
    /// </summary>
    public class ToolsSettings
    {
        /// <summary>Enable model-directed tools globally.</summary>
        public bool Enabled { get; set; } = true;
        /// <summary>Enable Wilson built-in tools.</summary>
        public bool BuiltInsEnabled { get; set; } = true;
        /// <summary>Default approval policy.</summary>
        public string DefaultApprovalPolicy { get; set; } = "ask";
        /// <summary>Require approval for destructive tools.</summary>
        public bool DestructiveToolsRequireApproval { get; set; } = true;
        /// <summary>Block known secret-bearing paths.</summary>
        public bool BlockSecretPaths { get; set; } = true;
        /// <summary>Default working directory for file and process tools.</summary>
        public string WorkingDirectory { get; set; } = String.Empty;
        /// <summary>Allowed filesystem roots.</summary>
        public List<string> AllowedRoots { get; set; } = new List<string>();
        /// <summary>Maximum agent loop iterations.</summary>
        public int MaxAgentIterations { get; set; } = 25;
        /// <summary>Maximum tool loop iterations.</summary>
        public int MaxToolIterations { get; set; } = 20;
        /// <summary>Maximum tool calls per chat turn.</summary>
        public int MaxToolCallsPerTurn { get; set; } = 12;
        /// <summary>Tool choice mode.</summary>
        public string ToolChoiceMode { get; set; } = "auto";
        /// <summary>Allow parallel tool execution.</summary>
        public bool AllowParallelToolCalls { get; set; } = false;
        /// <summary>Maximum parallel tool calls.</summary>
        public int MaxParallelToolCalls { get; set; } = 1;
        /// <summary>Emit safe progress events.</summary>
        public bool EmitProgressEvents { get; set; } = true;
        /// <summary>Expose safe tool traces to chat users.</summary>
        public bool ExposeToolTracesToUsers { get; set; } = true;
        /// <summary>Per-tool timeout in milliseconds.</summary>
        public int ToolTimeoutMs { get; set; } = 30000;
        /// <summary>Process tool timeout in milliseconds.</summary>
        public int ProcessTimeoutMs { get; set; } = 120000;
        /// <summary>Maximum bytes read by read_file.</summary>
        public int MaxReadFileBytes { get; set; } = 1048576;
        /// <summary>Maximum model-visible result bytes.</summary>
        public int MaxToolResultBytes { get; set; } = 102400;
        /// <summary>Persist tool result records.</summary>
        public bool StoreToolResults { get; set; } = true;
        /// <summary>Persist full tool results instead of summaries/previews.</summary>
        public bool StoreFullToolResults { get; set; } = false;
        /// <summary>Persist redacted tool arguments.</summary>
        public bool StoreToolArguments { get; set; } = true;
        /// <summary>Maximum model-visible characters from one tool call.</summary>
        public int MaxToolOutputChars { get; set; } = 12000;
        /// <summary>Maximum aggregate model-visible tool output characters per turn.</summary>
        public int MaxToolOutputCharsPerTurn { get; set; } = 50000;
        /// <summary>Maximum result items per call when supported.</summary>
        public int MaxToolResultItems { get; set; } = 20;
        /// <summary>Enabled tool names. Empty means all eligible tools.</summary>
        public List<string> EnabledToolNames { get; set; } = new List<string>();
        /// <summary>Disabled tool names.</summary>
        public List<string> DisabledToolNames { get; set; } = new List<string>();
        /// <summary>Web search settings.</summary>
        public WebSearchToolSettings WebSearch { get; set; } = new WebSearchToolSettings();
        /// <summary>MCP settings.</summary>
        public McpToolSettings Mcp { get; set; } = new McpToolSettings();
    }

    /// <summary>
    /// Web search tool settings.
    /// </summary>
    public class WebSearchToolSettings
    {
        /// <summary>Enable web_search.</summary>
        public bool Enabled { get; set; } = false;
        /// <summary>Allow provider fallback.</summary>
        public bool AllowFallback { get; set; } = true;
        /// <summary>Search providers.</summary>
        public List<WebSearchProviderSettings> Providers { get; set; } = new List<WebSearchProviderSettings>();
    }

    /// <summary>
    /// Web search provider settings.
    /// </summary>
    public class WebSearchProviderSettings
    {
        /// <summary>Provider name.</summary>
        public string Name { get; set; } = String.Empty;
        /// <summary>Provider type.</summary>
        public string ProviderType { get; set; } = String.Empty;
        /// <summary>Provider endpoint.</summary>
        public string Endpoint { get; set; } = String.Empty;
        /// <summary>Provider API key or environment reference.</summary>
        public string ApiKey { get; set; } = String.Empty;
        /// <summary>Enable provider.</summary>
        public bool Enabled { get; set; } = false;
        /// <summary>Whether this is the default provider.</summary>
        public bool IsDefault { get; set; } = false;
        /// <summary>Provider timeout in milliseconds.</summary>
        public int TimeoutMs { get; set; } = 30000;
    }

    /// <summary>
    /// MCP tool settings.
    /// </summary>
    public class McpToolSettings
    {
        /// <summary>Enable MCP tools.</summary>
        public bool Enabled { get; set; } = false;
        /// <summary>Configured MCP servers.</summary>
        public List<McpServerSettings> Servers { get; set; } = new List<McpServerSettings>();
    }

    /// <summary>
    /// MCP server settings.
    /// </summary>
    public class McpServerSettings
    {
        /// <summary>Server name.</summary>
        public string Name { get; set; } = String.Empty;
        /// <summary>Transport, such as stdio or http.</summary>
        public string Transport { get; set; } = "stdio";
        /// <summary>Command for stdio servers.</summary>
        public string Command { get; set; } = String.Empty;
        /// <summary>Command arguments.</summary>
        public List<string> Args { get; set; } = new List<string>();
        /// <summary>Environment variables.</summary>
        public Dictionary<string, string> Env { get; set; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        /// <summary>HTTP server URL.</summary>
        public string Url { get; set; } = String.Empty;
        /// <summary>MCP HTTP path.</summary>
        public string McpPath { get; set; } = String.Empty;
        /// <summary>Enable this server.</summary>
        public bool Enabled { get; set; } = false;
    }
}
