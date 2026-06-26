namespace Wilson.Core.Models
{
    using System.Collections.Generic;

    /// <summary>
    /// Result of testing tool readiness against settings and an optional runner.
    /// </summary>
    public sealed class ToolPolicyTestResult
    {
        /// <summary>Whether the tool readiness test passed.</summary>
        public bool Success { get; set; } = false;
        /// <summary>Whether the requested runner exists or no runner was requested.</summary>
        public bool RunnerFound { get; set; } = false;
        /// <summary>Whether the runner enables tools.</summary>
        public bool RunnerToolsEnabled { get; set; } = false;
        /// <summary>Whether the runner supports tool calls.</summary>
        public bool RunnerSupportsTools { get; set; } = false;
        /// <summary>Effective tool-call API format.</summary>
        public string ToolCallingApiFormat { get; set; } = string.Empty;
        /// <summary>Available executable tool count.</summary>
        public int AvailableToolCount { get; set; } = 0;
        /// <summary>Effective tool descriptors.</summary>
        public List<ToolDescriptor> Tools { get; set; } = new List<ToolDescriptor>();
        /// <summary>Non-blocking readiness warnings.</summary>
        public List<string> Warnings { get; set; } = new List<string>();
        /// <summary>Blocking readiness errors.</summary>
        public List<string> Errors { get; set; } = new List<string>();
    }
}
