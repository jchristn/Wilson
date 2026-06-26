namespace Wilson.Core.Tools
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Wilson.Core.Models;
    using Wilson.Core.Settings;

    /// <summary>
    /// Resolves effective tool availability from settings and prerequisites.
    /// </summary>
    public sealed class ToolPolicyResolver
    {
        /// <summary>
        /// Resolve descriptors for executors.
        /// </summary>
        /// <param name="settings">Wilson settings.</param>
        /// <param name="executors">Tool executors.</param>
        /// <param name="includeUnavailable">Whether to include unavailable descriptors.</param>
        /// <returns>Tool descriptors.</returns>
        public List<ToolDescriptor> Resolve(Settings settings, IEnumerable<IToolExecutor> executors, bool includeUnavailable)
        {
            ToolsSettings tools = settings.Tools ?? new ToolsSettings();
            HashSet<string> enabledNames = new HashSet<string>(tools.EnabledToolNames ?? new List<string>(), StringComparer.OrdinalIgnoreCase);
            HashSet<string> disabledNames = new HashSet<string>(tools.DisabledToolNames ?? new List<string>(), StringComparer.OrdinalIgnoreCase);
            List<ToolDescriptor> descriptors = new List<ToolDescriptor>();

            foreach (IToolExecutor executor in executors.OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase))
            {
                bool enabledByPolicy = tools.Enabled
                    && tools.BuiltInsEnabled
                    && !disabledNames.Contains(executor.Name)
                    && (enabledNames.Count == 0 || enabledNames.Contains(executor.Name));

                string? unavailableReason = null;
                if (!tools.Enabled) unavailableReason = "Tools are disabled.";
                else if (!tools.BuiltInsEnabled) unavailableReason = "Built-in tools are disabled.";
                else if (disabledNames.Contains(executor.Name)) unavailableReason = "Tool is disabled by name.";
                else if (enabledNames.Count > 0 && !enabledNames.Contains(executor.Name)) unavailableReason = "Tool is not in the enabled tool list.";
                else if (RequiresFilesystemContext(executor))
                {
                    unavailableReason = FilesystemUnavailableReason(settings);
                }
                else if (String.Equals(executor.Name, "web_search", StringComparison.OrdinalIgnoreCase))
                {
                    unavailableReason = WebSearchUnavailableReason(tools);
                }

                bool available = enabledByPolicy && String.IsNullOrWhiteSpace(unavailableReason);
                ToolDescriptor descriptor = new ToolDescriptor
                {
                    Name = executor.Name,
                    DisplayName = DisplayName(executor.Name),
                    Category = executor.Category,
                    EnabledByPolicy = enabledByPolicy,
                    Available = available,
                    UnavailableReason = available ? null : unavailableReason,
                    RequiresApproval = executor.Dangerous ? tools.DestructiveToolsRequireApproval : executor.RequiresApproval,
                    Dangerous = executor.Dangerous
                };

                if (includeUnavailable || descriptor.Available) descriptors.Add(descriptor);
            }

            return descriptors;
        }

        private static bool RequiresFilesystemContext(IToolExecutor executor)
        {
            return String.Equals(executor.Category, ToolCategories.Filesystem, StringComparison.OrdinalIgnoreCase)
                || String.Equals(executor.Category, ToolCategories.Process, StringComparison.OrdinalIgnoreCase);
        }

        private static string? FilesystemUnavailableReason(Settings settings)
        {
            ToolsSettings tools = settings.Tools ?? new ToolsSettings();
            ToolExecutionContext context = new ToolExecutionContext
            {
                Settings = settings,
                WorkingDirectory = tools.WorkingDirectory ?? String.Empty,
                AllowedRoots = tools.AllowedRoots ?? new List<string>()
            };

            try
            {
                WorkingDirectoryGuard.ValidateContext(context);
                return null;
            }
            catch (ToolExecutionException ex)
            {
                return ex.Message;
            }
        }

        private static string? WebSearchUnavailableReason(ToolsSettings tools)
        {
            if (tools.WebSearch == null || !tools.WebSearch.Enabled) return "Web search is disabled.";
            if (tools.WebSearch.Providers == null || !tools.WebSearch.Providers.Any(provider => provider != null && provider.Enabled))
                return "No enabled web search provider is configured.";
            return null;
        }

        private static string DisplayName(string name)
        {
            string[] parts = name.Split('_', StringSplitOptions.RemoveEmptyEntries);
            return String.Join(" ", parts.Select(part => Char.ToUpperInvariant(part[0]) + part.Substring(1)));
        }
    }
}
