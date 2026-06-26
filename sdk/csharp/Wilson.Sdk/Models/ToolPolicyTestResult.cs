namespace Wilson.Sdk.Models
{
    using System.Collections.Generic;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Result of dry-run Wilson tool readiness diagnostics.
    /// </summary>
    public sealed class ToolPolicyTestResult
    {
        /// <summary>
        /// Whether the readiness diagnostics passed.
        /// </summary>
        [JsonPropertyName("success")]
        public bool Success { get; set; }

        /// <summary>
        /// Whether the requested runner exists, or no runner was requested.
        /// </summary>
        [JsonPropertyName("runnerFound")]
        public bool RunnerFound { get; set; }

        /// <summary>
        /// Whether the selected runner enables tools.
        /// </summary>
        [JsonPropertyName("runnerToolsEnabled")]
        public bool RunnerToolsEnabled { get; set; }

        /// <summary>
        /// Whether the selected runner supports model tool calls.
        /// </summary>
        [JsonPropertyName("runnerSupportsTools")]
        public bool RunnerSupportsTools { get; set; }

        /// <summary>
        /// Effective tool-call API format for the selected runner.
        /// </summary>
        [JsonPropertyName("toolCallingApiFormat")]
        public string ToolCallingApiFormat { get; set; } = string.Empty;

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
        /// Non-blocking readiness warnings.
        /// </summary>
        [JsonPropertyName("warnings")]
        public List<string> Warnings { get; set; } = new List<string>();

        /// <summary>
        /// Blocking readiness errors.
        /// </summary>
        [JsonPropertyName("errors")]
        public List<string> Errors { get; set; } = new List<string>();
    }
}
