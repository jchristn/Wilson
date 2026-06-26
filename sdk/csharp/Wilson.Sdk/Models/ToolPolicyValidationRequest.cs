namespace Wilson.Sdk.Models
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// Request for validating draft Wilson tool settings without saving them.
    /// </summary>
    public sealed class ToolPolicyValidationRequest
    {
        /// <summary>
        /// Draft tool settings object. When null, Wilson validates the server's current tool settings.
        /// </summary>
        [JsonPropertyName("tools")]
        public object? Tools { get; set; } = null;
    }
}
