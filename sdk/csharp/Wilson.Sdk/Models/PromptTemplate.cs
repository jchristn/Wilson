namespace Wilson.Sdk.Models
{
    using System;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Tenant-scoped system or tool prompt template.
    /// </summary>
    public sealed class PromptTemplate
    {
        /// <summary>Prompt template identifier.</summary>
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        /// <summary>Tenant identifier.</summary>
        [JsonPropertyName("tenantId")]
        public string TenantId { get; set; } = string.Empty;

        /// <summary>Prompt template kind.</summary>
        [JsonPropertyName("kind")]
        public PromptTemplateKind Kind { get; set; }

        /// <summary>Prompt display name.</summary>
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        /// <summary>Prompt description.</summary>
        [JsonPropertyName("description")]
        public string Description { get; set; } = string.Empty;

        /// <summary>Prompt content.</summary>
        [JsonPropertyName("content")]
        public string Content { get; set; } = string.Empty;

        /// <summary>Whether this prompt is the default for its kind.</summary>
        [JsonPropertyName("isDefault")]
        public bool IsDefault { get; set; }

        /// <summary>Whether this prompt is protected from deletion.</summary>
        [JsonPropertyName("isProtected")]
        public bool IsProtected { get; set; }

        /// <summary>Whether this prompt can be selected for chat.</summary>
        [JsonPropertyName("active")]
        public bool Active { get; set; } = true;

        /// <summary>User that created the prompt.</summary>
        [JsonPropertyName("createdByUserId")]
        public string CreatedByUserId { get; set; } = string.Empty;

        /// <summary>User that last updated the prompt.</summary>
        [JsonPropertyName("updatedByUserId")]
        public string UpdatedByUserId { get; set; } = string.Empty;

        /// <summary>Creation timestamp.</summary>
        [JsonPropertyName("createdUtc")]
        public DateTime CreatedUtc { get; set; }

        /// <summary>Last update timestamp.</summary>
        [JsonPropertyName("lastUpdateUtc")]
        public DateTime LastUpdateUtc { get; set; }
    }
}
