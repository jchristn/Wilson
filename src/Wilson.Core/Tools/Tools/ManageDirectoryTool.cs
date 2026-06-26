namespace Wilson.Core.Tools.Tools
{
    using System;
    using System.IO;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using Wilson.Core.Models;

    /// <summary>
    /// Creates, deletes, or renames directories inside allowed roots.
    /// </summary>
    public sealed class ManageDirectoryTool : IToolExecutor
    {
        /// <inheritdoc />
        public string Name => "manage_directory";
        /// <inheritdoc />
        public string Description => "Creates, recursively deletes, or renames a directory inside the configured workspace.";
        /// <inheritdoc />
        public object ParametersSchema => new
        {
            type = "object",
            properties = new
            {
                action = new { type = "string", @enum = new[] { "create", "delete", "rename" }, description = "Directory action: create, delete, or rename." },
                path = new { type = "string", description = "Directory path inside an allowed root." },
                new_path = new { type = "string", description = "Destination directory path for rename." }
            },
            required = new[] { "action", "path" },
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
                ToolJson.RejectUnknownProperties(arguments, "action", "path", "new_path");
                string action = ToolJson.RequiredString(arguments, "action").Trim().ToLowerInvariant();
                string path = ToolJson.RequiredString(arguments, "path");
                string resolvedPath = WorkingDirectoryGuard.ResolvePath(path, context);

                if (String.Equals(action, "create", StringComparison.Ordinal))
                {
                    bool existed = Directory.Exists(resolvedPath);
                    if (File.Exists(resolvedPath)) return Task.FromResult(ToolResultFactory.Error(toolCallId, "path_is_file", "The target path is a file."));
                    Directory.CreateDirectory(resolvedPath);
                    return Task.FromResult(ToolResultFactory.SuccessJson(toolCallId, new { action, path = resolvedPath, created = !existed }, context));
                }

                if (String.Equals(action, "delete", StringComparison.Ordinal))
                {
                    if (!Directory.Exists(resolvedPath)) return Task.FromResult(ToolResultFactory.Error(toolCallId, "directory_not_found", "Directory not found."));
                    Directory.Delete(resolvedPath, true);
                    return Task.FromResult(ToolResultFactory.SuccessJson(toolCallId, new { action, path = resolvedPath, deleted = true }, context));
                }

                if (String.Equals(action, "rename", StringComparison.Ordinal))
                {
                    string newPath = ToolJson.RequiredString(arguments, "new_path");
                    string resolvedNewPath = WorkingDirectoryGuard.ResolvePath(newPath, context);
                    if (!Directory.Exists(resolvedPath)) return Task.FromResult(ToolResultFactory.Error(toolCallId, "directory_not_found", "Directory not found."));
                    if (Directory.Exists(resolvedNewPath) || File.Exists(resolvedNewPath)) return Task.FromResult(ToolResultFactory.Error(toolCallId, "destination_exists", "Destination already exists."));
                    string? parent = Path.GetDirectoryName(resolvedNewPath);
                    if (!String.IsNullOrWhiteSpace(parent) && !Directory.Exists(parent)) Directory.CreateDirectory(parent);
                    Directory.Move(resolvedPath, resolvedNewPath);
                    return Task.FromResult(ToolResultFactory.SuccessJson(toolCallId, new { action, path = resolvedPath, newPath = resolvedNewPath, renamed = true }, context));
                }

                return Task.FromResult(ToolResultFactory.Error(toolCallId, "invalid_action", "Action must be create, delete, or rename."));
            }
            catch (ToolExecutionException ex)
            {
                return Task.FromResult(ToolResultFactory.Error(toolCallId, ex));
            }
            catch (UnauthorizedAccessException)
            {
                return Task.FromResult(ToolResultFactory.Error(toolCallId, "permission_denied", "Permission denied when managing the directory."));
            }
            catch (Exception ex)
            {
                return Task.FromResult(ToolResultFactory.Error(toolCallId, "directory_error", ex.Message));
            }
        }
    }
}
