namespace Wilson.Sdk.Models
{
    using System.Collections.Generic;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Result of validating draft Wilson tool settings.
    /// </summary>
    public sealed class ToolPolicyValidationResult
    {
        /// <summary>
        /// Whether the draft policy passed validation.
        /// </summary>
        [JsonPropertyName("success")]
        public bool Success { get; set; }

        /// <summary>
        /// Whether tools are enabled globally in the evaluated policy.
        /// </summary>
        [JsonPropertyName("toolsEnabled")]
        public bool ToolsEnabled { get; set; }

        /// <summary>
        /// Effective default approval policy.
        /// </summary>
        [JsonPropertyName("approvalPolicy")]
        public string ApprovalPolicy { get; set; } = string.Empty;

        /// <summary>
        /// Count of executable tools available under the evaluated policy.
        /// </summary>
        [JsonPropertyName("availableToolCount")]
        public int AvailableToolCount { get; set; }

        /// <summary>
        /// Effective tool descriptors, including unavailable tools and safe unavailable reasons.
        /// </summary>
        [JsonPropertyName("tools")]
        public List<ToolDescriptor> Tools { get; set; } = new List<ToolDescriptor>();

        /// <summary>
        /// Non-blocking validation warnings.
        /// </summary>
        [JsonPropertyName("warnings")]
        public List<string> Warnings { get; set; } = new List<string>();

        /// <summary>
        /// Blocking validation errors.
        /// </summary>
        [JsonPropertyName("errors")]
        public List<string> Errors { get; set; } = new List<string>();
    }
}
