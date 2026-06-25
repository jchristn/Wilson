namespace Wilson.Core.Tools.Tools
{
    using System;
    using System.IO;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using Wilson.Core.Models;

    /// <summary>
    /// Reads file or directory metadata.
    /// </summary>
    public sealed class FileMetadataTool : IToolExecutor
    {
        /// <inheritdoc />
        public string Name => "file_metadata";
        /// <inheritdoc />
        public string Description => "Returns metadata about a file or directory, including type, size, timestamps, attributes, and shallow child counts for directories.";
        /// <inheritdoc />
        public object ParametersSchema => new
        {
            type = "object",
            properties = new
            {
                path = new { type = "string", description = "Path to a file or directory inside an allowed root." }
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
                ToolJson.RejectUnknownProperties(arguments, "path");
                string path = ToolJson.RequiredString(arguments, "path");
                string resolvedPath = WorkingDirectoryGuard.ResolvePath(path, context);

                if (File.Exists(resolvedPath))
                {
                    FileInfo info = new FileInfo(resolvedPath);
                    return Task.FromResult(ToolResultFactory.SuccessJson(toolCallId, new
                    {
                        success = true,
                        path = resolvedPath,
                        type = "file",
                        size_bytes = info.Length,
                        created_utc = info.CreationTimeUtc.ToString("o"),
                        modified_utc = info.LastWriteTimeUtc.ToString("o"),
                        accessed_utc = info.LastAccessTimeUtc.ToString("o"),
                        attributes = info.Attributes.ToString(),
                        extension = info.Extension
                    }, context));
                }

                if (Directory.Exists(resolvedPath))
                {
                    DirectoryInfo info = new DirectoryInfo(resolvedPath);
                    int fileCount = 0;
                    int directoryCount = 0;
                    try
                    {
                        fileCount = info.GetFiles().Length;
                        directoryCount = info.GetDirectories().Length;
                    }
                    catch
                    {
                    }

                    return Task.FromResult(ToolResultFactory.SuccessJson(toolCallId, new
                    {
                        success = true,
                        path = resolvedPath,
                        type = "directory",
                        created_utc = info.CreationTimeUtc.ToString("o"),
                        modified_utc = info.LastWriteTimeUtc.ToString("o"),
                        accessed_utc = info.LastAccessTimeUtc.ToString("o"),
                        attributes = info.Attributes.ToString(),
                        file_count = fileCount,
                        directory_count = directoryCount
                    }, context));
                }

                return Task.FromResult(ToolResultFactory.Error(toolCallId, "not_found", "Path not found."));
            }
            catch (ToolExecutionException ex)
            {
                return Task.FromResult(ToolResultFactory.Error(toolCallId, ex));
            }
            catch (UnauthorizedAccessException)
            {
                return Task.FromResult(ToolResultFactory.Error(toolCallId, "permission_denied", "Permission denied when reading metadata."));
            }
            catch (Exception ex)
            {
                return Task.FromResult(ToolResultFactory.Error(toolCallId, "metadata_error", ex.Message));
            }
        }
    }
}
