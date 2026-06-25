namespace Wilson.Core.Tools
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using Wilson.Core.Settings;

    /// <summary>
    /// Resolves and validates file paths for filesystem and process tools.
    /// </summary>
    public static class WorkingDirectoryGuard
    {
        private static readonly HashSet<string> _SecretSegments = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".ssh", ".aws", ".azure", ".gcp", ".kube", ".docker", ".gnupg", "secrets", "credentials"
        };

        private static readonly HashSet<string> _SecretFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".npmrc", ".pypirc", "nuget.config", "web.config", "app.config", "connectionstrings.json", "credentials.json", "service-account.json"
        };

        /// <summary>
        /// Resolve a path against the configured working directory and verify it remains inside allowed roots.
        /// </summary>
        /// <param name="path">Relative or absolute path.</param>
        /// <param name="context">Tool context.</param>
        /// <returns>Resolved absolute path.</returns>
        public static string ResolvePath(string path, ToolExecutionContext context)
        {
            if (String.IsNullOrWhiteSpace(path))
                throw new ToolExecutionException("invalid_path", "Path is required.");

            string workingDirectory = EffectiveWorkingDirectory(context);
            List<string> roots = EffectiveAllowedRoots(context);

            if (!Directory.Exists(workingDirectory))
                throw new ToolExecutionException("working_directory_not_found", "Configured tool working directory does not exist.");

            if (!IsInsideAnyRoot(workingDirectory, roots))
                throw new ToolExecutionException("working_directory_outside_allowed_roots", "Configured tool working directory is outside the allowed roots.");

            string resolved = Path.IsPathRooted(path)
                ? Path.GetFullPath(path)
                : Path.GetFullPath(Path.Combine(workingDirectory, path));

            if (!IsInsideAnyRoot(resolved, roots))
                throw new ToolExecutionException("path_outside_allowed_roots", "Resolved path is outside the allowed roots.");

            if (ShouldBlockSecretPaths(context) && IsSecretPath(resolved, roots))
                throw new ToolExecutionException("secret_path_blocked", "Resolved path is blocked because it appears to contain secrets.");

            return resolved;
        }

        /// <summary>
        /// Validate working directory and allowed root settings.
        /// </summary>
        /// <param name="context">Tool context.</param>
        public static void ValidateContext(ToolExecutionContext context)
        {
            string workingDirectory = EffectiveWorkingDirectory(context);
            List<string> roots = EffectiveAllowedRoots(context);
            if (!Directory.Exists(workingDirectory))
                throw new ToolExecutionException("working_directory_not_found", "Configured tool working directory does not exist.");
            if (!IsInsideAnyRoot(workingDirectory, roots))
                throw new ToolExecutionException("working_directory_outside_allowed_roots", "Configured tool working directory is outside the allowed roots.");
        }

        private static string EffectiveWorkingDirectory(ToolExecutionContext context)
        {
            string? value = !String.IsNullOrWhiteSpace(context.WorkingDirectory)
                ? context.WorkingDirectory
                : context.Settings.Tools?.WorkingDirectory;
            if (String.IsNullOrWhiteSpace(value))
                throw new ToolExecutionException("missing_working_directory", "Tools require a configured working directory.");
            return Path.GetFullPath(value);
        }

        private static List<string> EffectiveAllowedRoots(ToolExecutionContext context)
        {
            List<string> configured = context.AllowedRoots.Count > 0
                ? context.AllowedRoots
                : context.Settings.Tools?.AllowedRoots ?? new List<string>();
            List<string> roots = configured
                .Where(root => !String.IsNullOrWhiteSpace(root))
                .Select(root => Path.GetFullPath(root))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            if (roots.Count == 0)
                throw new ToolExecutionException("missing_allowed_roots", "Tools require at least one configured allowed root.");
            return roots;
        }

        private static bool ShouldBlockSecretPaths(ToolExecutionContext context)
        {
            ToolsSettings tools = context.Settings.Tools ?? new ToolsSettings();
            return tools.BlockSecretPaths;
        }

        private static bool IsInsideAnyRoot(string path, List<string> roots)
        {
            return roots.Any(root => IsInsideRoot(path, root));
        }

        private static bool IsInsideRoot(string path, string root)
        {
            string fullPath = Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string fullRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            StringComparison comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
            return String.Equals(fullPath, fullRoot, comparison)
                || fullPath.StartsWith(fullRoot + Path.DirectorySeparatorChar, comparison)
                || fullPath.StartsWith(fullRoot + Path.AltDirectorySeparatorChar, comparison);
        }

        private static bool IsSecretPath(string path, List<string> roots)
        {
            string? matchingRoot = roots.FirstOrDefault(root => IsInsideRoot(path, root));
            string relative = matchingRoot == null ? path : Path.GetRelativePath(matchingRoot, path);
            string[] segments = relative.Split(new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar }, StringSplitOptions.RemoveEmptyEntries);
            if (segments.Any(segment => _SecretSegments.Contains(segment))) return true;
            string fileName = Path.GetFileName(path);
            string lower = fileName.ToLowerInvariant();
            if (_SecretFiles.Contains(fileName)) return true;
            if (lower.StartsWith(".env", StringComparison.Ordinal)) return true;
            if (lower.EndsWith(".pem", StringComparison.Ordinal) || lower.EndsWith(".pfx", StringComparison.Ordinal) || lower.EndsWith(".p12", StringComparison.Ordinal) || lower.EndsWith(".key", StringComparison.Ordinal)) return true;
            if (lower.StartsWith("appsettings", StringComparison.Ordinal) && lower.EndsWith(".json", StringComparison.Ordinal)) return true;
            return false;
        }
    }
}
