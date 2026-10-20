namespace Wilson.Sdk.Models
{
    /// <summary>
    /// One provider API type Wilson understands, with its capabilities and defaults.
    /// </summary>
    public sealed class ApiTypeDescriptor
    {
        [System.Text.Json.Serialization.JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [System.Text.Json.Serialization.JsonPropertyName("displayName")]
        public string DisplayName { get; set; } = string.Empty;

        [System.Text.Json.Serialization.JsonPropertyName("description")]
        public string Description { get; set; } = string.Empty;

        [System.Text.Json.Serialization.JsonPropertyName("defaultEndpoint")]
        public string DefaultEndpoint { get; set; } = string.Empty;

        [System.Text.Json.Serialization.JsonPropertyName("defaultHealthPath")]
        public string DefaultHealthPath { get; set; } = string.Empty;

        [System.Text.Json.Serialization.JsonPropertyName("defaultHealthIntervalMs")]
        public int DefaultHealthIntervalMs { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("defaultHealthTimeoutMs")]
        public int DefaultHealthTimeoutMs { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("supportsChatCompletions")]
        public bool SupportsChatCompletions { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("supportsEmbeddings")]
        public bool SupportsEmbeddings { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("supportsModelListing")]
        public bool SupportsModelListing { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("supportsToolCalls")]
        public bool SupportsToolCalls { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("supportsStreaming")]
        public bool SupportsStreaming { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("defaultToolCallingApiFormat")]
        public string DefaultToolCallingApiFormat { get; set; } = string.Empty;

        [System.Text.Json.Serialization.JsonPropertyName("defaultChatCompletionsPath")]
        public string DefaultChatCompletionsPath { get; set; } = string.Empty;

        [System.Text.Json.Serialization.JsonPropertyName("requiresApiKey")]
        public bool RequiresApiKey { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("requiresDeployment")]
        public bool RequiresDeployment { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("requiresApiVersion")]
        public bool RequiresApiVersion { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("defaultApiVersion")]
        public string DefaultApiVersion { get; set; } = string.Empty;

        [System.Text.Json.Serialization.JsonPropertyName("requiresProjectId")]
        public bool RequiresProjectId { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("requiresRegion")]
        public bool RequiresRegion { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("defaultRegion")]
        public string DefaultRegion { get; set; } = string.Empty;
    }
}
