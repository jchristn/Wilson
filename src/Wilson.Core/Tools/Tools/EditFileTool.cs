namespace Wilson.Core.Tools.Tools
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Text;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using Wilson.Core.Models;

    /// <summary>
    /// Applies one exact string replacement to a file.
    /// </summary>
    public sealed class EditFileTool : IToolExecutor
    {
        /// <inheritdoc />
        public string Name => "edit_file";
        /// <inheritdoc />
        public string Description => "Applies one exact string replacement to a file. The edit fails when the old string has zero or multiple matches.";
        /// <inheritdoc />
        public object ParametersSchema => new
        {
            type = "object",
            properties = new
            {
                file_path = new { type = "string", description = "File path inside an allowed root." },
                old_string = new { type = "string", description = "Existing text to replace. Must match exactly once." },
                new_string = new { type = "string", description = "Replacement text." }
            },
            required = new[] { "file_path", "old_string", "new_string" },
            additionalProperties = false
        };
        /// <inheritdoc />
        public string Category => ToolCategories.Filesystem;
        /// <inheritdoc />
        public bool RequiresApproval => true;
        /// <inheritdoc />
        public bool Dangerous => true;

        /// <inheritdoc />
        public async Task<ToolResult> ExecuteAsync(string toolCallId, JsonElement arguments, ToolExecutionContext context, CancellationToken token)
        {
            try
            {
                ToolJson.RejectUnknownProperties(arguments, "file_path", "old_string", "new_string");
                string filePath = ToolJson.RequiredString(arguments, "file_path");
                string oldString = ToolJson.RequiredString(arguments, "old_string");
                string newString = ToolJson.RequiredStringAllowEmpty(arguments, "new_string");
                string resolvedPath = WorkingDirectoryGuard.ResolvePath(filePath, context);
                if (Directory.Exists(resolvedPath)) return ToolResultFactory.Error(toolCallId, "path_is_directory", "The target path is a directory.");
                if (!File.Exists(resolvedPath)) return ToolResultFactory.Error(toolCallId, "file_not_found", "File not found.");

                string content = await File.ReadAllTextAsync(resolvedPath, token).ConfigureAwait(false);
                int matches = FileEditHelpers.CountOccurrences(content, oldString);
                if (matches != 1)
                {
                    List<int> lines = FileEditHelpers.CandidateLineNumbers(content, oldString);
                    string lineText = lines.Count > 0 ? " Candidate lines: " + String.Join(", ", lines) + "." : String.Empty;
                    return ToolResultFactory.Error(toolCallId, matches == 0 ? "no_match" : "multiple_matches", "Expected exactly one match but found " + matches + "." + lineText);
                }

                string updated = FileEditHelpers.ReplaceOnce(content, oldString, newString);
                await File.WriteAllTextAsync(resolvedPath, updated, new UTF8Encoding(false), token).ConfigureAwait(false);
                return ToolResultFactory.SuccessJson(toolCallId, new
                {
                    path = resolvedPath,
                    replacements = 1,
                    lineCount = FileEditHelpers.LineCount(updated)
                }, context);
            }
            catch (ToolExecutionException ex)
            {
                return ToolResultFactory.Error(toolCallId, ex);
            }
            catch (UnauthorizedAccessException)
            {
                return ToolResultFactory.Error(toolCallId, "permission_denied", "Permission denied when editing the file.");
            }
            catch (Exception ex)
            {
                return ToolResultFactory.Error(toolCallId, "edit_error", ex.Message);
            }
        }
    }
}
