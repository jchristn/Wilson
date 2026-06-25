namespace Wilson.Core.Tools
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Text.Json;

    internal static class ToolJson
    {
        public static void RequireObject(JsonElement arguments)
        {
            if (arguments.ValueKind != JsonValueKind.Object)
                throw new ToolExecutionException("invalid_arguments", "Tool arguments must be a JSON object.");
        }

        public static void RejectUnknownProperties(JsonElement arguments, params string[] allowedProperties)
        {
            RequireObject(arguments);
            HashSet<string> allowed = new HashSet<string>(allowedProperties, StringComparer.OrdinalIgnoreCase);
            foreach (JsonProperty property in arguments.EnumerateObject())
            {
                if (!allowed.Contains(property.Name))
                    throw new ToolExecutionException("invalid_arguments", "Unknown argument '" + property.Name + "'.");
            }
        }

        public static string RequiredString(JsonElement arguments, string propertyName)
        {
            if (arguments.TryGetProperty(propertyName, out JsonElement value) && value.ValueKind == JsonValueKind.String)
            {
                string? text = value.GetString();
                if (!String.IsNullOrWhiteSpace(text)) return text;
            }

            throw new ToolExecutionException("invalid_arguments", "Required argument '" + propertyName + "' is missing or not a non-empty string.");
        }

        public static string OptionalString(JsonElement arguments, string propertyName, string defaultValue)
        {
            if (arguments.TryGetProperty(propertyName, out JsonElement value))
            {
                if (value.ValueKind == JsonValueKind.String) return value.GetString() ?? defaultValue;
                if (value.ValueKind != JsonValueKind.Null) throw new ToolExecutionException("invalid_arguments", "Argument '" + propertyName + "' must be a string.");
            }

            return defaultValue;
        }

        public static int OptionalInt(JsonElement arguments, string propertyName, int defaultValue, int minValue, int maxValue, bool allowString = false)
        {
            if (!arguments.TryGetProperty(propertyName, out JsonElement value) || value.ValueKind == JsonValueKind.Null) return defaultValue;

            int parsed;
            if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out parsed))
            {
                return Math.Clamp(parsed, minValue, maxValue);
            }

            if (allowString && value.ValueKind == JsonValueKind.String && Int32.TryParse(value.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out parsed))
            {
                return Math.Clamp(parsed, minValue, maxValue);
            }

            throw new ToolExecutionException("invalid_arguments", "Argument '" + propertyName + "' must be an integer.");
        }
    }
}
