namespace Wilson.Core.Tools
{
    using System;
    using System.Collections.Generic;
    using Wilson.Core.Helpers;
    using Wilson.Core.Settings;

    /// <summary>
    /// Context supplied to tool executors.
    /// </summary>
    public class ToolExecutionContext
    {
        /// <summary>Tenant identifier.</summary>
        public string TenantId { get; set; } = String.Empty;
        /// <summary>User identifier.</summary>
        public string UserId { get; set; } = String.Empty;
        /// <summary>Conversation identifier.</summary>
        public string ConversationId { get; set; } = String.Empty;
        /// <summary>Tool run identifier.</summary>
        public string RunId { get; set; } = IdGenerator.ToolRun();
        /// <summary>Working directory for relative paths.</summary>
        public string WorkingDirectory { get; set; } = String.Empty;
        /// <summary>Allowed filesystem roots.</summary>
        public List<string> AllowedRoots { get; set; } = new List<string>();
        /// <summary>Current Wilson settings.</summary>
        public Settings Settings { get; set; } = new Settings();
        /// <summary>Effective safety limits.</summary>
        public ToolSafetyLimits SafetyLimits => ToolSafetyLimits.FromSettings(Settings);
    }
}
