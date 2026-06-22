namespace Wilson.Sdk.Models
{
    using System.Collections.Generic;
    using System.Text.Json.Serialization;

    public sealed class ModelRunnerStatus
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("apiType")]
        public string ApiType { get; set; } = string.Empty;

        [JsonPropertyName("endpoint")]
        public string Endpoint { get; set; } = string.Empty;

        [JsonPropertyName("availableModels")]
        public List<string> AvailableModels { get; set; } = new List<string>();

        [JsonPropertyName("chatModels")]
        public List<string> ChatModels { get; set; } = new List<string>();

        [JsonPropertyName("embeddingModels")]
        public List<string> EmbeddingModels { get; set; } = new List<string>();

        [JsonPropertyName("loadedModels")]
        public List<string> LoadedModels { get; set; } = new List<string>();

        [JsonPropertyName("healthCheckEnabled")]
        public bool HealthCheckEnabled { get; set; }

        [JsonPropertyName("healthCheckUrl")]
        public string? HealthCheckUrl { get; set; }

        [JsonPropertyName("healthCheckMethod")]
        public string HealthCheckMethod { get; set; } = "GET";

        [JsonPropertyName("health")]
        public EndpointHealthStatus? Health { get; set; }
    }
}
