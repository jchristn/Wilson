namespace Wilson.Core.Models
{
    using System;
    using System.Text.Json;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Reads either a JSON string or raw JSON value into a string.
    /// </summary>
    public sealed class JsonStringOrRawJsonConverter : JsonConverter<string>
    {
        /// <inheritdoc />
        public override string? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.Null) return null;
            if (reader.TokenType == JsonTokenType.String) return reader.GetString();
            using JsonDocument document = JsonDocument.ParseValue(ref reader);
            return document.RootElement.GetRawText();
        }

        /// <inheritdoc />
        public override void Write(Utf8JsonWriter writer, string value, JsonSerializerOptions options)
        {
            if (value == null)
            {
                writer.WriteNullValue();
                return;
            }

            writer.WriteStringValue(value);
        }
    }
}
