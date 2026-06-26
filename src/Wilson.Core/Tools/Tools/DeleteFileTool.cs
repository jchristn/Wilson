namespace Wilson.Core.Tools.Tools
{
    using System;
    using System.IO;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using Wilson.Core.Models;

    /// <summary>
    /// Deletes a file inside an allowed root.
    /// </summary>
    public sealed class DeleteFileTool : IToolExecutor
    {
        /// <inheritdoc />
        public string Name => "delete_file";
        /// <inheritdoc />
        public string Description => "Deletes a file inside the configured workspace.";
        /// <inheritdoc />
        public object ParametersSchema => new
        {
            type = "object",
            properties = new
            {
                file_path = new { type = "string", description = "File path inside an allowed root." }
            },
            required = new[] { "file_path" },
            additionalProperties = false
        };
        /// <inheritdoc />
        public string Category => ToolCategories.Filesystem;
        /// <inheritdoc />
        public bool RequiresApproval => true;
        /// <inheritdoc />
        public bool Dangerous => true;

        /// <inheritdoc />
        public Task<ToolResult> ExecuteAsync(string toolCallId, JsonElement arguments, ToolExecutionContext context, CancellationToken token)
        {
            try
            {
                ToolJson.RejectUnknownProperties(arguments, "file_path");
                string filePath = ToolJson.RequiredString(arguments, "file_path");
                string resolvedPath = WorkingDirectoryGuard.ResolvePath(filePath, context);
                if (Directory.Exists(resolvedPath)) return Task.FromResult(ToolResultFactory.Error(toolCallId, "path_is_directory", "The target path is a directory."));
                if (!File.Exists(resolvedPath)) return Task.FromResult(ToolResultFactory.Error(toolCallId, "file_not_found", "File not found."));

                File.Delete(resolvedPath);
                return Task.FromResult(ToolResultFactory.SuccessJson(toolCallId, new
                {
                    path = resolvedPath,
                    deleted = true
                }, context));
            }
            catch (ToolExecutionException ex)
            {
                return Task.FromResult(ToolResultFactory.Error(toolCallId, ex));
            }
            catch (UnauthorizedAccessException)
            {
                return Task.FromResult(ToolResultFactory.Error(toolCallId, "permission_denied", "Permission denied when deleting the file."));
            }
            catch (Exception ex)
            {
                return Task.FromResult(ToolResultFactory.Error(toolCallId, "delete_error", ex.Message));
            }
        }
    }
}
