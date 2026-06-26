namespace Wilson.Core.Tools.Tools
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Text;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using Wilson.Core.Models;

    /// <summary>
    /// Runs a process in an allowed working directory.
    /// </summary>
    public sealed class RunProcessTool : IToolExecutor
    {
        /// <inheritdoc />
        public string Name => "run_process";
        /// <inheritdoc />
        public string Description => "Runs an executable with an argument array in a configured allowed working directory, capturing stdout, stderr, exit code, timeout, and truncation metadata.";
        /// <inheritdoc />
        public object ParametersSchema => new
        {
            type = "object",
            properties = new
            {
                command = new { type = "string", description = "Executable name or path. Shell expansion is not used." },
                args = new { type = "array", items = new { type = "string" }, description = "Arguments passed to the executable." },
                working_directory = new { type = "string", description = "Optional working directory inside an allowed root. Defaults to the configured tool working directory." },
                timeout_ms = new { type = "integer", description = "Optional timeout in milliseconds." }
            },
            required = new[] { "command" },
            additionalProperties = false
        };
        /// <inheritdoc />
        public string Category => ToolCategories.Process;
        /// <inheritdoc />
        public bool RequiresApproval => true;
        /// <inheritdoc />
        public bool Dangerous => true;

        /// <inheritdoc />
        public async Task<ToolResult> ExecuteAsync(string toolCallId, JsonElement arguments, ToolExecutionContext context, CancellationToken token)
        {
            Stopwatch stopwatch = Stopwatch.StartNew();
            try
            {
                ToolJson.RejectUnknownProperties(arguments, "command", "args", "working_directory", "timeout_ms");
                string command = ToolJson.RequiredString(arguments, "command");
                List<string> args = ParseArgs(arguments);
                string workingDirectory = ResolveWorkingDirectory(arguments, context);
                int timeoutMs = ToolJson.OptionalInt(arguments, "timeout_ms", context.SafetyLimits.ProcessTimeoutMs, 1000, context.SafetyLimits.ProcessTimeoutMs, true);

                ProcessStartInfo startInfo = new ProcessStartInfo
                {
                    FileName = ResolveCommand(command, context),
                    WorkingDirectory = workingDirectory,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                foreach (string arg in args)
                {
                    startInfo.ArgumentList.Add(arg);
                }

                StringBuilder stdout = new StringBuilder();
                StringBuilder stderr = new StringBuilder();
                using Process process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };
                process.OutputDataReceived += (sender, eventArgs) => { if (eventArgs.Data != null) stdout.AppendLine(eventArgs.Data); };
                process.ErrorDataReceived += (sender, eventArgs) => { if (eventArgs.Data != null) stderr.AppendLine(eventArgs.Data); };

                if (!process.Start()) return ToolResultFactory.Error(toolCallId, "process_start_failed", "The process could not be started.");
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                bool timedOut = false;
                bool cancelled = false;
                using CancellationTokenSource timeoutSource = new CancellationTokenSource(timeoutMs);
                using CancellationTokenSource linkedSource = CancellationTokenSource.CreateLinkedTokenSource(token, timeoutSource.Token);
                try
                {
                    await process.WaitForExitAsync(linkedSource.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    timedOut = timeoutSource.IsCancellationRequested;
                    cancelled = token.IsCancellationRequested;
                    KillProcessTree(process);
                    await process.WaitForExitAsync(CancellationToken.None).ConfigureAwait(false);
                }

                stopwatch.Stop();
                bool stdoutTruncated;
                bool stderrTruncated;
                string cappedStdout = CapOutput(stdout.ToString(), context.SafetyLimits.MaxToolOutputChars, out stdoutTruncated);
                string cappedStderr = CapOutput(stderr.ToString(), context.SafetyLimits.MaxToolOutputChars, out stderrTruncated);
                return ToolResultFactory.SuccessJson(toolCallId, new
                {
                    command,
                    args,
                    workingDirectory,
                    exitCode = process.HasExited ? process.ExitCode : -1,
                    timedOut,
                    cancelled,
                    elapsedMs = stopwatch.Elapsed.TotalMilliseconds,
                    stdout = cappedStdout,
                    stderr = cappedStderr,
                    stdoutTruncated,
                    stderrTruncated
                }, context);
            }
            catch (ToolExecutionException ex)
            {
                return ToolResultFactory.Error(toolCallId, ex);
            }
            catch (UnauthorizedAccessException)
            {
                return ToolResultFactory.Error(toolCallId, "permission_denied", "Permission denied when running the process.");
            }
            catch (Exception ex)
            {
                return ToolResultFactory.Error(toolCallId, "process_error", ex.Message);
            }
        }

        private static List<string> ParseArgs(JsonElement arguments)
        {
            List<string> args = new List<string>();
            if (!arguments.TryGetProperty("args", out JsonElement argsElement) || argsElement.ValueKind == JsonValueKind.Null) return args;
            if (argsElement.ValueKind != JsonValueKind.Array) throw new ToolExecutionException("invalid_arguments", "Argument 'args' must be an array of strings.");

            foreach (JsonElement item in argsElement.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.String) throw new ToolExecutionException("invalid_arguments", "Argument 'args' must contain only strings.");
                args.Add(item.GetString() ?? String.Empty);
            }

            return args;
        }

        private static string ResolveWorkingDirectory(JsonElement arguments, ToolExecutionContext context)
        {
            string workingDirectory = ToolJson.OptionalString(arguments, "working_directory", ".");
            string resolved = WorkingDirectoryGuard.ResolvePath(workingDirectory, context);
            if (!Directory.Exists(resolved)) throw new ToolExecutionException("working_directory_not_found", "Process working directory does not exist.");
            return resolved;
        }

        private static string ResolveCommand(string command, ToolExecutionContext context)
        {
            if (Path.IsPathRooted(command) || command.Contains(Path.DirectorySeparatorChar) || command.Contains(Path.AltDirectorySeparatorChar))
            {
                string resolved = WorkingDirectoryGuard.ResolvePath(command, context);
                if (!File.Exists(resolved)) throw new ToolExecutionException("command_not_found", "Command path was not found.");
                return resolved;
            }

            return command;
        }

        private static string CapOutput(string content, int maxCharacters, out bool truncated)
        {
            truncated = false;
            if (String.IsNullOrEmpty(content) || content.Length <= maxCharacters) return content ?? String.Empty;
            truncated = true;
            return content.Substring(0, maxCharacters) + Environment.NewLine + "[truncated]";
        }

        private static void KillProcessTree(Process process)
        {
            try
            {
                if (!process.HasExited) process.Kill(true);
            }
            catch
            {
            }
        }

    }
}
