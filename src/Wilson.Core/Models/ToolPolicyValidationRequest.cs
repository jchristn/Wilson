namespace Wilson.Core.Models
{
    using Wilson.Core.Settings;

    /// <summary>
    /// Request for validating draft tool settings without saving them.
    /// </summary>
    public sealed class ToolPolicyValidationRequest
    {
        /// <summary>Draft tool settings. When omitted, current server settings are validated.</summary>
        public ToolsSettings? Tools { get; set; } = null;
    }
}
