namespace Wilson.Core.Tools.Tools
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Text;
    using System.Text.Json;
    using System.Text.RegularExpressions;
    using System.Threading;
    using System.Threading.Tasks;
    using Wilson.Core.Models;

    /// <summary>
    /// Searches files for regex matches.
    /// </summary>
    public sealed class GrepTool : IToolExecutor
    {
        /// <inheritdoc />
        public string Name => "grep";
        /// <inheritdoc />
        public string Description => "Searches files recursively for lines matching a regular expression and returns file path, line number, and content.";
        /// <inheritdoc />
        public object ParametersSchema => new
        {
            type = "object",
            properties = new
            {
                pattern = new { type = "string", description = "Regular expression pattern to search for." },
                path = new { type = "string", description = "Directory to search. Defaults to the tool working directory." },
                include = new { type = "string", description = "File glob pattern to include, such as *.cs or *.json. Defaults to *." },
                max_matches = new { type = "integer", description = "Maximum matches to return. Defaults to the configured result item limit." }
            },
            required = new[] { "pattern" },
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
                ToolJson.RejectUnknownProperties(arguments, "pattern", "path", "include", "max_matches");
                string pattern = ToolJson.RequiredString(arguments, "pattern");
                string path = ToolJson.OptionalString(arguments, "path", ".");
                string include = ToolJson.OptionalString(arguments, "include", "*");
                string resolvedPath = WorkingDirectoryGuard.ResolvePath(path, context);
                int maxMatches = ToolJson.OptionalInt(arguments, "max_matches", context.SafetyLimits.MaxToolResultItems, 1, 1000, true);

                if (!Directory.Exists(resolvedPath)) return Task.FromResult(ToolResultFactory.Error(toolCallId, "directory_not_found", "Directory not found."));

                Regex regex;
                try
                {
                    regex = new Regex(pattern, RegexOptions.Compiled, TimeSpan.FromSeconds(2));
                }
                catch (ArgumentException ex)
                {
                    return Task.FromResult(ToolResultFactory.Error(toolCallId, "invalid_regex", "Invalid regular expression: " + ex.Message));
                }

                EnumerationOptions options = new EnumerationOptions
                {
                    RecurseSubdirectories = true,
                    IgnoreInaccessible = true,
                    ReturnSpecialDirectories = false
                };

                StringBuilder output = new StringBuilder();
                int matchCount = 0;
                foreach (string file in Directory.EnumerateFiles(resolvedPath, include, options))
                {
                    token.ThrowIfCancellationRequested();
                    if (matchCount >= maxMatches) break;
                    string relative = Path.GetRelativePath(resolvedPath, file).Replace('\\', '/');
                    int lineNumber = 0;
                    foreach (string line in File.ReadLines(file))
                    {
                        token.ThrowIfCancellationRequested();
                        lineNumber++;
                        if (!regex.IsMatch(line)) continue;
                        output.AppendLine(relative + ":" + lineNumber + ": " + line);
                        matchCount++;
                        if (matchCount >= maxMatches) break;
                    }
                }

                if (matchCount == 0) output.AppendLine("No matches found.");
                else if (matchCount >= maxMatches) output.AppendLine("(output truncated at " + maxMatches + " matches)");
                ToolResult result = ToolResultFactory.SuccessText(toolCallId, output.ToString(), context);
                result.Truncated = result.Truncated || matchCount >= maxMatches;
                return Task.FromResult(result);
            }
            catch (ToolExecutionException ex)
            {
                return Task.FromResult(ToolResultFactory.Error(toolCallId, ex));
            }
            catch (OperationCanceledException)
            {
                return Task.FromResult(ToolResultFactory.Error(toolCallId, "cancelled", "Tool execution was cancelled."));
            }
            catch (Exception ex)
            {
                return Task.FromResult(ToolResultFactory.Error(toolCallId, "grep_error", ex.Message));
            }
        }
    }
}
