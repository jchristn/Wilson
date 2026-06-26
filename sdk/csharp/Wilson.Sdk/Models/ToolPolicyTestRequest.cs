namespace Wilson.Sdk.Models
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// Request for dry-run Wilson tool readiness diagnostics.
    /// </summary>
    public sealed class ToolPolicyTestRequest
    {
        /// <summary>
        /// Draft tool settings object. When null, Wilson tests the server's current tool settings.
        /// </summary>
        [JsonPropertyName("tools")]
        public object? Tools { get; set; } = null;

        /// <summary>
        /// Optional model runner identifier to include in readiness diagnostics.
        /// </summary>
        [JsonPropertyName("runnerId")]
        public string? RunnerId { get; set; } = null;
    }
}
