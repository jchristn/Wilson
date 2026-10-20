namespace Wilson.Sdk.Models
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// Describes one model server API type Wilson supports through PolyPrompt.
    /// </summary>
    public sealed class ApiTypeDescriptor
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("displayName")]
        public string DisplayName { get; set; } = string.Empty;

        [JsonPropertyName("defaultEndpoint")]
        public string DefaultEndpoint { get; set; } = string.Empty;

        [JsonPropertyName("defaultHealthPath")]
        public string DefaultHealthPath { get; set; } = string.Empty;

        [JsonPropertyName("requiresApiKey")]
        public bool RequiresApiKey { get; set; }

        [JsonPropertyName("supportsChat")]
        public bool SupportsChat { get; set; }

        [JsonPropertyName("supportsEmbeddings")]
        public bool SupportsEmbeddings { get; set; }

        [JsonPropertyName("supportsModelListing")]
        public bool SupportsModelListing { get; set; }

        [JsonPropertyName("supportsTools")]
        public bool SupportsTools { get; set; }

        [JsonPropertyName("usesCloudResourceEndpoint")]
        public bool UsesCloudResourceEndpoint { get; set; }

        [JsonPropertyName("defaultRegion")]
        public string DefaultRegion { get; set; } = string.Empty;

        [JsonPropertyName("defaultToolCallingApiFormat")]
        public string DefaultToolCallingApiFormat { get; set; } = string.Empty;

        [JsonPropertyName("supportsParallelToolCalls")]
        public bool SupportsParallelToolCalls { get; set; }

        [JsonPropertyName("supportsStreamingToolCalls")]
        public bool SupportsStreamingToolCalls { get; set; }

        [JsonPropertyName("defaultChatCompletionsPath")]
        public string DefaultChatCompletionsPath { get; set; } = string.Empty;

        [JsonPropertyName("defaultContextWindowTokens")]
        public int DefaultContextWindowTokens { get; set; }
    }
}
