namespace Wilson.Core.Tools.Tools
{
    using System;
    using System.IO;
    using System.Text;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using Wilson.Core.Models;

    /// <summary>
    /// Reads a file with line numbers.
    /// </summary>
    public sealed class ReadFileTool : IToolExecutor
    {
        /// <inheritdoc />
        public string Name => "read_file";
        /// <inheritdoc />
        public string Description => "Reads a file from the configured workspace and returns line-numbered content. Supports optional offset and limit parameters.";
        /// <inheritdoc />
        public object ParametersSchema => new
        {
            type = "object",
            properties = new
            {
                file_path = new { type = "string", description = "Path to the file, relative to the tool working directory or inside an allowed root." },
                offset = new { type = "integer", description = "One-based line number to start reading from. Defaults to 1." },
                limit = new { type = "integer", description = "Maximum number of lines to read. Defaults to the full file." }
            },
            required = new[] { "file_path" },
            additionalProperties = false
        };
        /// <inheritdoc />
        public string Category => ToolCategories.Filesystem;
        /// <inheritdoc />
        public bool RequiresApproval => false;
        /// <inheritdoc />
        public bool Dangerous => false;

        /// <inheritdoc />
        public async Task<ToolResult> ExecuteAsync(string toolCallId, JsonElement arguments, ToolExecutionContext context, CancellationToken token)
        {
            try
            {
                ToolJson.RejectUnknownProperties(arguments, "file_path", "offset", "limit");
                string filePath = ToolJson.RequiredString(arguments, "file_path");
                string resolvedPath = WorkingDirectoryGuard.ResolvePath(filePath, context);
                int offset = ToolJson.OptionalInt(arguments, "offset", 1, 1, Int32.MaxValue, true);
                int limit = ToolJson.OptionalInt(arguments, "limit", -1, -1, Int32.MaxValue, true);

                if (!File.Exists(resolvedPath)) return ToolResultFactory.Error(toolCallId, "file_not_found", "File not found.");
                FileInfo info = new FileInfo(resolvedPath);
                if (info.Length > context.SafetyLimits.MaxReadFileBytes)
                    return ToolResultFactory.Error(toolCallId, "file_too_large", "File exceeds the configured maximum read size.");

                string[] lines = await File.ReadAllLinesAsync(resolvedPath, token).ConfigureAwait(false);
                int start = Math.Min(lines.Length, Math.Max(0, offset - 1));
                int end = limit > 0 ? Math.Min(lines.Length, start + limit) : lines.Length;
                StringBuilder output = new StringBuilder();
                for (int i = start; i < end; i++)
                {
                    output.AppendFormat("{0,6}\t{1}", i + 1, lines[i].Replace("\r", String.Empty));
                    output.AppendLine();
                }

                return ToolResultFactory.SuccessText(toolCallId, output.ToString(), context);
            }
            catch (ToolExecutionException ex)
            {
                return ToolResultFactory.Error(toolCallId, ex);
            }
            catch (UnauthorizedAccessException)
            {
                return ToolResultFactory.Error(toolCallId, "permission_denied", "Permission denied when reading the file.");
            }
            catch (Exception ex)
            {
                return ToolResultFactory.Error(toolCallId, "read_error", ex.Message);
            }
        }
    }
}
