namespace Wilson.Core.Tools.Tools
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Text.Json;
    using System.Text.RegularExpressions;
    using System.Threading;
    using System.Threading.Tasks;
    using Wilson.Core.Models;

    /// <summary>
    /// Finds files by glob pattern.
    /// </summary>
    public sealed class GlobTool : IToolExecutor
    {
        /// <inheritdoc />
        public string Name => "glob";
        /// <inheritdoc />
        public string Description => "Searches for files matching a glob pattern inside the configured workspace. Supports *, **, and ? wildcards.";
        /// <inheritdoc />
        public object ParametersSchema => new
        {
            type = "object",
            properties = new
            {
                pattern = new { type = "string", description = "Glob pattern such as **/*.cs or src/**/*.json." },
                path = new { type = "string", description = "Directory to search. Defaults to the tool working directory." },
                max_results = new { type = "integer", description = "Maximum matching files to return. Defaults to the configured result item limit." }
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
                ToolJson.RejectUnknownProperties(arguments, "pattern", "path", "max_results");
                string pattern = ToolJson.RequiredString(arguments, "pattern");
                string path = ToolJson.OptionalString(arguments, "path", ".");
                string resolvedPath = WorkingDirectoryGuard.ResolvePath(path, context);
                int maxResults = ToolJson.OptionalInt(arguments, "max_results", context.SafetyLimits.MaxToolResultItems, 1, 1000, true);

                if (!Directory.Exists(resolvedPath)) return Task.FromResult(ToolResultFactory.Error(toolCallId, "directory_not_found", "Directory not found."));

                Regex regex = GlobToRegex(pattern);
                EnumerationOptions options = new EnumerationOptions
                {
                    RecurseSubdirectories = true,
                    IgnoreInaccessible = true,
                    ReturnSpecialDirectories = false
                };

                List<string> matches = new List<string>();
                foreach (string file in Directory.EnumerateFiles(resolvedPath, "*", options))
                {
                    token.ThrowIfCancellationRequested();
                    string relative = Path.GetRelativePath(resolvedPath, file).Replace('\\', '/');
                    if (regex.IsMatch(relative))
                    {
                        matches.Add(relative);
                        if (matches.Count >= maxResults) break;
                    }
                }

                matches.Sort(StringComparer.OrdinalIgnoreCase);
                StringBuilder output = new StringBuilder();
                output.AppendLine("Found " + matches.Count + " matching file(s):");
                foreach (string match in matches) output.AppendLine(match);
                ToolResult result = ToolResultFactory.SuccessText(toolCallId, output.ToString(), context);
                result.Truncated = matches.Count >= maxResults;
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
                return Task.FromResult(ToolResultFactory.Error(toolCallId, "glob_error", ex.Message));
            }
        }

        private static Regex GlobToRegex(string pattern)
        {
            string normalized = pattern.Replace('\\', '/');
            StringBuilder builder = new StringBuilder("^");
            for (int i = 0; i < normalized.Length;)
            {
                char c = normalized[i];
                if (c == '*' && i + 1 < normalized.Length && normalized[i + 1] == '*')
                {
                    if (i + 2 < normalized.Length && normalized[i + 2] == '/')
                    {
                        builder.Append("(.+/)?");
                        i += 3;
                    }
                    else
                    {
                        builder.Append(".*");
                        i += 2;
                    }
                }
                else if (c == '*')
                {
                    builder.Append("[^/]*");
                    i++;
                }
                else if (c == '?')
                {
                    builder.Append("[^/]");
                    i++;
                }
                else
                {
                    builder.Append(Regex.Escape(c.ToString()));
                    i++;
                }
            }

            builder.Append("$");
            return new Regex(builder.ToString(), RegexOptions.IgnoreCase | RegexOptions.Compiled, TimeSpan.FromSeconds(2));
        }
    }
}
