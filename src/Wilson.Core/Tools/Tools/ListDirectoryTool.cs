namespace Wilson.Core.Tools.Tools
{
    using System;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using Wilson.Core.Models;

    /// <summary>
    /// Lists immediate directory entries.
    /// </summary>
    public sealed class ListDirectoryTool : IToolExecutor
    {
        /// <inheritdoc />
        public string Name => "list_directory";
        /// <inheritdoc />
        public string Description => "Lists immediate child directories and files at a path inside the configured workspace. Directories are listed first, then files, both sorted alphabetically.";
        /// <inheritdoc />
        public object ParametersSchema => new
        {
            type = "object",
            properties = new
            {
                path = new { type = "string", description = "Directory path inside an allowed root." },
                max_entries = new { type = "integer", description = "Maximum entries to return. Defaults to the configured tool result item limit." }
            },
            required = new[] { "path" },
            additionalProperties = false
        };
        /// <inheritdoc />
        public string Category => ToolCategories.Filesystem;
        /// <inheritdoc />
        public bool RequiresApproval => false;
        /// <inheritdoc />
        public bool Dangerous => false;

        /// <inheritdoc />
        public Task<ToolResult> ExecuteAsync(string toolCallId, JsonElement arguments, ToolExecutionContext context, CancellationToken token)
        {
            try
            {
                ToolJson.RejectUnknownProperties(arguments, "path", "max_entries");
                string path = ToolJson.RequiredString(arguments, "path");
                string resolvedPath = WorkingDirectoryGuard.ResolvePath(path, context);
                int maxEntries = ToolJson.OptionalInt(arguments, "max_entries", context.SafetyLimits.MaxToolResultItems, 1, 1000, true);

                if (!Directory.Exists(resolvedPath)) return Task.FromResult(ToolResultFactory.Error(toolCallId, "directory_not_found", "Directory not found."));

                string[] directories = Directory.GetDirectories(resolvedPath)
                    .Select(Path.GetFileName)
                    .Where(name => !String.IsNullOrWhiteSpace(name))
                    .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                    .Cast<string>()
                    .ToArray();
                string[] files = Directory.GetFiles(resolvedPath)
                    .Select(Path.GetFileName)
                    .Where(name => !String.IsNullOrWhiteSpace(name))
                    .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                    .Cast<string>()
                    .ToArray();

                StringBuilder output = new StringBuilder();
                int emitted = 0;
                foreach (string directory in directories)
                {
                    if (emitted >= maxEntries) break;
                    output.AppendLine("[DIR]  " + directory);
                    emitted++;
                }

                foreach (string file in files)
                {
                    if (emitted >= maxEntries) break;
                    output.AppendLine("[FILE] " + file);
                    emitted++;
                }

                int total = directories.Length + files.Length;
                if (emitted < total) output.AppendLine("(output truncated at " + emitted + " of " + total + " entries)");
                ToolResult result = ToolResultFactory.SuccessText(toolCallId, output.ToString(), context);
                result.Truncated = result.Truncated || emitted < total;
                return Task.FromResult(result);
            }
            catch (ToolExecutionException ex)
            {
                return Task.FromResult(ToolResultFactory.Error(toolCallId, ex));
            }
            catch (UnauthorizedAccessException)
            {
                return Task.FromResult(ToolResultFactory.Error(toolCallId, "permission_denied", "Permission denied when listing the directory."));
            }
            catch (Exception ex)
            {
                return Task.FromResult(ToolResultFactory.Error(toolCallId, "list_error", ex.Message));
            }
        }
    }
}
