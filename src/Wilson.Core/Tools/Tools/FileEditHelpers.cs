namespace Wilson.Core.Tools.Tools
{
    using System;
    using System.Collections.Generic;
    using System.Text.RegularExpressions;

    internal static class FileEditHelpers
    {
        public static string DetectLineEnding(string content)
        {
            int crlf = Count(content, "\r\n");
            string withoutCrlf = content.Replace("\r\n", String.Empty);
            int lf = Count(withoutCrlf, "\n");
            int cr = Count(withoutCrlf, "\r");
            if (crlf >= lf && crlf >= cr && crlf > 0) return "\r\n";
            if (lf >= cr && lf > 0) return "\n";
            if (cr > 0) return "\r";
            return Environment.NewLine;
        }

        public static string NormalizeLineEndings(string content, string lineEnding)
        {
            string normalized = Regex.Replace(content ?? String.Empty, "\r\n|\n|\r", "\n");
            return lineEnding == "\n" ? normalized : normalized.Replace("\n", lineEnding);
        }

        public static int CountOccurrences(string content, string oldString)
        {
            if (String.IsNullOrEmpty(oldString)) return 0;
            int count = 0;
            int index = 0;
            while (true)
            {
                index = content.IndexOf(oldString, index, StringComparison.Ordinal);
                if (index < 0) return count;
                count++;
                index += oldString.Length;
            }
        }

        public static List<int> CandidateLineNumbers(string content, string oldString)
        {
            List<int> lines = new List<int>();
            if (String.IsNullOrEmpty(oldString)) return lines;
            int index = 0;
            while (true)
            {
                index = content.IndexOf(oldString, index, StringComparison.Ordinal);
                if (index < 0) return lines;
                lines.Add(LineNumberAt(content, index));
                index += oldString.Length;
            }
        }

        public static int LineCount(string content)
        {
            if (String.IsNullOrEmpty(content)) return 0;
            int count = 1;
            for (int i = 0; i < content.Length; i++)
            {
                if (content[i] == '\n') count++;
            }

            return count;
        }

        public static string ReplaceOnce(string content, string oldString, string newString)
        {
            int index = content.IndexOf(oldString, StringComparison.Ordinal);
            if (index < 0) return content;
            return content.Substring(0, index) + newString + content.Substring(index + oldString.Length);
        }

        private static int Count(string content, string value)
        {
            int count = 0;
            int index = 0;
            while (true)
            {
                index = content.IndexOf(value, index, StringComparison.Ordinal);
                if (index < 0) return count;
                count++;
                index += value.Length;
            }
        }

        private static int LineNumberAt(string content, int index)
        {
            int line = 1;
            int end = Math.Min(index, content.Length);
            for (int i = 0; i < end; i++)
            {
                if (content[i] == '\n') line++;
            }

            return line;
        }
    }
}
