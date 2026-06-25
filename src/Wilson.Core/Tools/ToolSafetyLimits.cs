namespace Wilson.Core.Tools
{
    using System;
    using Wilson.Core.Settings;

    /// <summary>
    /// Immutable tool safety limits derived from settings.
    /// </summary>
    public sealed class ToolSafetyLimits
    {
        /// <summary>Maximum bytes read by read_file.</summary>
        public int MaxReadFileBytes { get; init; } = 1048576;
        /// <summary>Maximum model-visible tool result bytes.</summary>
        public int MaxToolResultBytes { get; init; } = 102400;
        /// <summary>Maximum model-visible characters from one tool call.</summary>
        public int MaxToolOutputChars { get; init; } = 12000;
        /// <summary>Maximum returned result items.</summary>
        public int MaxToolResultItems { get; init; } = 20;
        /// <summary>Default tool timeout.</summary>
        public int ToolTimeoutMs { get; init; } = 30000;
        /// <summary>Process timeout.</summary>
        public int ProcessTimeoutMs { get; init; } = 120000;

        /// <summary>
        /// Build limits from Wilson settings.
        /// </summary>
        /// <param name="settings">Wilson settings.</param>
        /// <returns>Effective limits.</returns>
        public static ToolSafetyLimits FromSettings(Settings? settings)
        {
            ToolsSettings tools = settings?.Tools ?? new ToolsSettings();
            return new ToolSafetyLimits
            {
                MaxReadFileBytes = Math.Clamp(tools.MaxReadFileBytes, 1, 104857600),
                MaxToolResultBytes = Math.Clamp(tools.MaxToolResultBytes, 1024, 10485760),
                MaxToolOutputChars = Math.Clamp(tools.MaxToolOutputChars, 1024, 200000),
                MaxToolResultItems = Math.Clamp(tools.MaxToolResultItems, 1, 1000),
                ToolTimeoutMs = Math.Clamp(tools.ToolTimeoutMs, 1000, 300000),
                ProcessTimeoutMs = Math.Clamp(tools.ProcessTimeoutMs, 1000, 600000)
            };
        }
    }
}
