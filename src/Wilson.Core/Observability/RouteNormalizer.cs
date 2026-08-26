namespace Wilson.Core.Observability
{
    using System;
    using System.Text;

    /// <summary>
    /// Collapses variable path segments (numeric ids, GUIDs, and PrettyId-style identifiers) to <c>{id}</c> so
    /// HTTP metric route labels stay bounded in cardinality.
    /// </summary>
    public static class RouteNormalizer
    {
        /// <summary>
        /// Normalize a request path to a low-cardinality route template.
        /// </summary>
        /// <param name="path">Raw request path.</param>
        /// <returns>Normalized route template.</returns>
        public static string Normalize(string? path)
        {
            if (String.IsNullOrWhiteSpace(path)) return "/";

            string trimmed = path!.Trim();
            int query = trimmed.IndexOf('?');
            if (query >= 0) trimmed = trimmed.Substring(0, query);
            if (trimmed.Length == 0) return "/";

            string[] segments = trimmed.Split('/');
            StringBuilder builder = new StringBuilder();
            foreach (string segment in segments)
            {
                if (segment.Length == 0) continue;
                builder.Append('/');
                builder.Append(IsVariable(segment) ? "{id}" : segment);
            }

            return builder.Length == 0 ? "/" : builder.ToString();
        }

        private static bool IsVariable(string segment)
        {
            // PrettyId identifiers look like "prefix_random"; treat any underscore-bearing alphanumeric token as an id.
            if (segment.IndexOf('_') >= 0) return true;

            bool hasDigit = false;
            bool hasLetter = false;
            foreach (char c in segment)
            {
                if (Char.IsDigit(c)) hasDigit = true;
                else if (Char.IsLetter(c)) hasLetter = true;
                else if (c != '-') return false; // punctuation other than '-' -> treat as a literal segment
            }

            if (!hasDigit) return false;              // pure words are literal route segments
            if (hasLetter && segment.Length < 8) return false; // short mixed tokens like "v1" stay literal
            return true;                              // numeric ids, GUIDs, long hex/mixed tokens
        }
    }
}
