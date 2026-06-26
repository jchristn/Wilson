namespace Wilson.Core.Models
{
    using System.Collections.Generic;

    /// <summary>
    /// Result of validating draft tool settings.
    /// </summary>
    public sealed class ToolPolicyValidationResult
    {
        /// <summary>Whether the draft policy is usable.</summary>
        public bool Success { get; set; } = false;
        /// <summary>Whether tools are enabled globally in the draft.</summary>
        public bool ToolsEnabled { get; set; } = false;
        /// <summary>Effective approval policy.</summary>
        public string ApprovalPolicy { get; set; } = string.Empty;
        /// <summary>Available executable tool count.</summary>
        public int AvailableToolCount { get; set; } = 0;
        /// <summary>Effective tool descriptors.</summary>
        public List<ToolDescriptor> Tools { get; set; } = new List<ToolDescriptor>();
        /// <summary>Non-blocking validation warnings.</summary>
        public List<string> Warnings { get; set; } = new List<string>();
        /// <summary>Blocking validation errors.</summary>
        public List<string> Errors { get; set; } = new List<string>();
    }
}
