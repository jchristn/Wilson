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
    /// Applies multiple exact string replacements to a file.
    /// </summary>
    public sealed class MultiEditTool : IToolExecutor
    {
        /// <inheritdoc />
        public string Name => "multi_edit";
        /// <inheritdoc />
        public string Description => "Applies multiple exact string replacements to a file after validating each edit against the sequential working content.";
        /// <inheritdoc />
        public object ParametersSchema => new
        {
            type = "object",
            properties = new
            {
                file_path = new { type = "string", description = "File path inside an allowed root." },
                edits = new
                {
                    type = "array",
                    description = "Sequential exact replacements.",
                    items = new
                    {
                        type = "object",
                        properties = new
                        {
                            old_string = new { type = "string", description = "Existing text to replace. Must match exactly once at this step." },
                            new_string = new { type = "string", description = "Replacement text." }
                        },
                        required = new[] { "old_string", "new_string" },
                        additionalProperties = false
                    }
                }
            },
            required = new[] { "file_path", "edits" },
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
                ToolJson.RejectUnknownProperties(arguments, "file_path", "edits");
                string filePath = ToolJson.RequiredString(arguments, "file_path");
                JsonElement editsElement = ToolJson.RequiredArray(arguments, "edits");
                List<string> oldStrings = new List<string>();
                List<string> newStrings = new List<string>();
                ParseEdits(editsElement, oldStrings, newStrings);
                if (oldStrings.Count == 0) return ToolResultFactory.Error(toolCallId, "invalid_arguments", "At least one edit is required.");

                string resolvedPath = WorkingDirectoryGuard.ResolvePath(filePath, context);
                if (Directory.Exists(resolvedPath)) return ToolResultFactory.Error(toolCallId, "path_is_directory", "The target path is a directory.");
                if (!File.Exists(resolvedPath)) return ToolResultFactory.Error(toolCallId, "file_not_found", "File not found.");

                string original = await File.ReadAllTextAsync(resolvedPath, token).ConfigureAwait(false);
                string working = original;
                for (int i = 0; i < oldStrings.Count; i++)
                {
                    string oldString = oldStrings[i];
                    string newString = newStrings[i];
                    int matches = FileEditHelpers.CountOccurrences(working, oldString);
                    if (matches != 1)
                    {
                        List<int> lines = FileEditHelpers.CandidateLineNumbers(working, oldString);
                        string lineText = lines.Count > 0 ? " Candidate lines: " + String.Join(", ", lines) + "." : String.Empty;
                        return ToolResultFactory.Error(toolCallId, matches == 0 ? "no_match" : "multiple_matches", "Edit " + (i + 1) + " expected exactly one match but found " + matches + "." + lineText);
                    }

                    working = FileEditHelpers.ReplaceOnce(working, oldString, newString);
                }

                await File.WriteAllTextAsync(resolvedPath, working, new UTF8Encoding(false), token).ConfigureAwait(false);
                return ToolResultFactory.SuccessJson(toolCallId, new
                {
                    path = resolvedPath,
                    edits = oldStrings.Count,
                    lineCount = FileEditHelpers.LineCount(working)
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
                return ToolResultFactory.Error(toolCallId, "multi_edit_error", ex.Message);
            }
        }

        private static void ParseEdits(JsonElement editsElement, List<string> oldStrings, List<string> newStrings)
        {
            foreach (JsonElement item in editsElement.EnumerateArray())
            {
                ToolJson.RejectUnknownProperties(item, "old_string", "new_string");
                oldStrings.Add(ToolJson.RequiredString(item, "old_string"));
                newStrings.Add(ToolJson.RequiredStringAllowEmpty(item, "new_string"));
            }
        }
    }
}
