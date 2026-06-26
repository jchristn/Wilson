namespace Wilson.Sdk.Models
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// Tool descriptor returned by Wilson's effective tool catalog.
    /// </summary>
    public sealed class ToolDescriptor
    {
        /// <summary>Tool name.</summary>
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        /// <summary>Display name.</summary>
        [JsonPropertyName("displayName")]
        public string DisplayName { get; set; } = string.Empty;

        /// <summary>Tool category.</summary>
        [JsonPropertyName("category")]
        public string Category { get; set; } = string.Empty;

        /// <summary>Whether the tool is enabled by policy.</summary>
        [JsonPropertyName("enabledByPolicy")]
        public bool EnabledByPolicy { get; set; }

        /// <summary>Whether the tool is currently available.</summary>
        [JsonPropertyName("available")]
        public bool Available { get; set; }

        /// <summary>Safe reason the tool is unavailable.</summary>
        [JsonPropertyName("unavailableReason")]
        public string? UnavailableReason { get; set; }

        /// <summary>Whether the tool requires approval.</summary>
        [JsonPropertyName("requiresApproval")]
        public bool RequiresApproval { get; set; }

        /// <summary>Whether the tool can perform dangerous work.</summary>
        [JsonPropertyName("dangerous")]
        public bool Dangerous { get; set; }
    }
}
