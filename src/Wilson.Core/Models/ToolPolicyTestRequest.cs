namespace Wilson.Core.Models
{
    using Wilson.Core.Settings;

    /// <summary>
    /// Request for testing tool readiness against a runner.
    /// </summary>
    public sealed class ToolPolicyTestRequest
    {
        /// <summary>Draft tool settings. When omitted, current server settings are tested.</summary>
        public ToolsSettings? Tools { get; set; } = null;
        /// <summary>Optional model runner identifier to test.</summary>
        public string? RunnerId { get; set; } = null;
    }
}
