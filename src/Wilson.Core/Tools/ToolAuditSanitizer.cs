namespace Wilson.Core.Tools
{
    using System;
    using System.IO;
    using System.Text;
    using System.Text.Json;
    using System.Text.RegularExpressions;

    /// <summary>
    /// Redacts sensitive values before tool payloads are persisted or returned from audit APIs.
    /// </summary>
    public static class ToolAuditSanitizer
    {
        private static readonly JsonSerializerOptions _Json = new JsonSerializerOptions { WriteIndented = false };
        private static readonly Regex _BearerRegex = new Regex(@"\bBearer\s+[A-Za-z0-9._~+/=-]+", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        private static readonly Regex _AssignmentRegex = new Regex(@"\b(?<key>api[_-]?key|password|passwd|pwd|secret|token|credential|access[_-]?key|connection[_-]?string|client[_-]?secret|private[_-]?key|authorization)\b(?<sep>\s*[:=]\s*)(?<quote>[""']?)[^\s,;""']+", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        /// <summary>
        /// Redact sensitive values from a JSON payload while preserving non-sensitive structure.
        /// </summary>
        /// <param name="json">Raw JSON payload.</param>
        /// <returns>Redacted JSON payload.</returns>
        public static string RedactJson(string? json)
        {
            if (String.IsNullOrWhiteSpace(json)) return "{}";

            try
            {
                using JsonDocument document = JsonDocument.Parse(json);
                using MemoryStream stream = new MemoryStream();
                using (Utf8JsonWriter writer = new Utf8JsonWriter(stream))
                {
                    WriteRedactedElement(writer, document.RootElement, null);
                }

                return Encoding.UTF8.GetString(stream.ToArray());
            }
            catch (JsonException)
            {
                return JsonSerializer.Serialize(new { content = RedactText(json) }, _Json);
            }
        }

        /// <summary>
        /// Redact sensitive values from unstructured text.
        /// </summary>
        /// <param name="value">Raw text.</param>
        /// <returns>Redacted text.</returns>
        public static string RedactText(string? value)
        {
            if (String.IsNullOrEmpty(value)) return String.Empty;
            string output = _BearerRegex.Replace(value, "Bearer [redacted]");
            output = _AssignmentRegex.Replace(output, RedactAssignment);
            return output;
        }

        /// <summary>
        /// Cap text after redaction so secret tails cannot survive truncation boundaries.
        /// </summary>
        /// <param name="value">Raw text.</param>
        /// <param name="maxCharacters">Maximum returned characters.</param>
        /// <returns>Redacted capped text.</returns>
        public static string RedactAndCapText(string? value, int maxCharacters)
        {
            return Cap(RedactText(value), maxCharacters);
        }

        /// <summary>
        /// Cap JSON after redaction so persisted audit payloads stay bounded.
        /// </summary>
        /// <param name="json">Raw JSON payload.</param>
        /// <param name="maxCharacters">Maximum returned characters.</param>
        /// <returns>Redacted capped JSON or a redacted truncation wrapper.</returns>
        public static string RedactAndCapJson(string? json, int maxCharacters)
        {
            string redacted = RedactJson(json);
            if (redacted.Length <= maxCharacters) return redacted;
            string capped = Cap(redacted, maxCharacters);
            return JsonSerializer.Serialize(new
            {
                truncated = true,
                originalCharacters = redacted.Length,
                content = capped
            }, _Json);
        }

        private static string RedactAssignment(Match match)
        {
            return match.Groups["key"].Value + match.Groups["sep"].Value + "[redacted]";
        }

        private static void WriteRedactedElement(Utf8JsonWriter writer, JsonElement element, string? propertyName)
        {
            if (IsSensitiveName(propertyName))
            {
                writer.WriteStringValue("[redacted]");
                return;
            }

            switch (element.ValueKind)
            {
                case JsonValueKind.Object:
                    writer.WriteStartObject();
                    foreach (JsonProperty property in element.EnumerateObject())
                    {
                        writer.WritePropertyName(property.Name);
                        WriteRedactedElement(writer, property.Value, property.Name);
                    }

                    writer.WriteEndObject();
                    break;
                case JsonValueKind.Array:
                    writer.WriteStartArray();
                    foreach (JsonElement item in element.EnumerateArray())
                    {
                        WriteRedactedElement(writer, item, propertyName);
                    }

                    writer.WriteEndArray();
                    break;
                case JsonValueKind.String:
                    writer.WriteStringValue(RedactText(element.GetString()));
                    break;
                case JsonValueKind.Number:
                    element.WriteTo(writer);
                    break;
                case JsonValueKind.True:
                    writer.WriteBooleanValue(true);
                    break;
                case JsonValueKind.False:
                    writer.WriteBooleanValue(false);
                    break;
                case JsonValueKind.Null:
                case JsonValueKind.Undefined:
                default:
                    writer.WriteNullValue();
                    break;
            }
        }

        private static bool IsSensitiveName(string? name)
        {
            if (String.IsNullOrWhiteSpace(name)) return false;
            string normalized = NormalizeName(name);
            return normalized.Contains("apikey", StringComparison.Ordinal)
                || normalized.Contains("password", StringComparison.Ordinal)
                || normalized.Contains("passwd", StringComparison.Ordinal)
                || String.Equals(normalized, "pwd", StringComparison.Ordinal)
                || normalized.Contains("secret", StringComparison.Ordinal)
                || normalized.Contains("token", StringComparison.Ordinal)
                || normalized.Contains("credential", StringComparison.Ordinal)
                || normalized.Contains("accesskey", StringComparison.Ordinal)
                || normalized.Contains("connectionstring", StringComparison.Ordinal)
                || normalized.Contains("clientsecret", StringComparison.Ordinal)
                || normalized.Contains("privatekey", StringComparison.Ordinal)
                || normalized.Contains("authorization", StringComparison.Ordinal);
        }

        private static string NormalizeName(string name)
        {
            StringBuilder builder = new StringBuilder(name.Length);
            foreach (char ch in name)
            {
                if (Char.IsLetterOrDigit(ch)) builder.Append(Char.ToLowerInvariant(ch));
            }

            return builder.ToString();
        }

        private static string Cap(string value, int maxCharacters)
        {
            if (maxCharacters <= 0) return String.Empty;
            if (String.IsNullOrEmpty(value) || value.Length <= maxCharacters) return value ?? String.Empty;
            return value.Substring(0, maxCharacters) + "... [truncated]";
        }
    }
}
