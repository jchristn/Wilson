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
    /// Writes full file content inside an allowed root.
    /// </summary>
    public sealed class WriteFileTool : IToolExecutor
    {
        /// <inheritdoc />
        public string Name => "write_file";
        /// <inheritdoc />
        public string Description => "Writes full file content inside the configured workspace. Existing line endings are preserved when overwriting a file.";
        /// <inheritdoc />
        public object ParametersSchema => new
        {
            type = "object",
            properties = new
            {
                file_path = new { type = "string", description = "Path to write, relative to the tool working directory or inside an allowed root." },
                content = new { type = "string", description = "Full file content to write." }
            },
            required = new[] { "file_path", "content" },
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
                ToolJson.RejectUnknownProperties(arguments, "file_path", "content");
                string filePath = ToolJson.RequiredString(arguments, "file_path");
                string content = ToolJson.RequiredStringAllowEmpty(arguments, "content");
                string resolvedPath = WorkingDirectoryGuard.ResolvePath(filePath, context);
                if (Directory.Exists(resolvedPath)) return ToolResultFactory.Error(toolCallId, "path_is_directory", "The target path is a directory.");

                bool existed = File.Exists(resolvedPath);
                bool createdParent = false;
                if (existed)
                {
                    string existing = await File.ReadAllTextAsync(resolvedPath, token).ConfigureAwait(false);
                    content = FileEditHelpers.NormalizeLineEndings(content, FileEditHelpers.DetectLineEnding(existing));
                }

                string? parent = Path.GetDirectoryName(resolvedPath);
                if (!String.IsNullOrWhiteSpace(parent) && !Directory.Exists(parent))
                {
                    Directory.CreateDirectory(parent);
                    createdParent = true;
                }

                await File.WriteAllTextAsync(resolvedPath, content, new UTF8Encoding(false), token).ConfigureAwait(false);
                FileInfo info = new FileInfo(resolvedPath);
                return ToolResultFactory.SuccessJson(toolCallId, new
                {
                    path = resolvedPath,
                    created = !existed,
                    parentCreated = createdParent,
                    bytes = info.Length,
                    lineCount = FileEditHelpers.LineCount(content)
                }, context);
            }
            catch (ToolExecutionException ex)
            {
                return ToolResultFactory.Error(toolCallId, ex);
            }
            catch (UnauthorizedAccessException)
            {
                return ToolResultFactory.Error(toolCallId, "permission_denied", "Permission denied when writing the file.");
            }
            catch (Exception ex)
            {
                return ToolResultFactory.Error(toolCallId, "write_error", ex.Message);
            }
        }
    }
}
