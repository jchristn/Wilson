namespace Wilson.Core.Settings
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>
    /// Tool-calling wire formats Wilson can use for tool-enabled chat.
    /// </summary>
    public static class ToolCallingApiFormats
    {
        /// <summary>
        /// Ollama /api/chat tool-calling format.
        /// </summary>
        public const string OllamaChat = "OllamaChat";

        /// <summary>
        /// OpenAI-compatible /v1/chat/completions tool-calling format.
        /// </summary>
        public const string OpenAIChatCompletions = "OpenAIChatCompletions";

        /// <summary>
        /// Provider-native tool calling performed by the PolyPrompt client for the runner API type.
        /// </summary>
        public const string PolyPromptNative = "PolyPromptNative";
    }

    /// <summary>
    /// Describes one model server API type supported through PolyPrompt.
    /// </summary>
    public sealed class ApiTypeDescriptor
    {
        /// <summary>Canonical API type name.</summary>
        public string Name { get; set; } = String.Empty;

        /// <summary>Human-readable display name.</summary>
        public string DisplayName { get; set; } = String.Empty;

        /// <summary>Default endpoint used when the runner endpoint is blank.</summary>
        public string DefaultEndpoint { get; set; } = String.Empty;

        /// <summary>Default health probe path relative to the endpoint.</summary>
        public string DefaultHealthPath { get; set; } = "/v1/models";

        /// <summary>Whether the API key is required rather than optional.</summary>
        public bool RequiresApiKey { get; set; }

        /// <summary>Whether chat completions are supported.</summary>
        public bool SupportsChat { get; set; } = true;

        /// <summary>Whether embeddings are supported.</summary>
        public bool SupportsEmbeddings { get; set; } = true;

        /// <summary>Whether the provider can enumerate models.</summary>
        public bool SupportsModelListing { get; set; } = true;

        /// <summary>Whether native tool calling is supported.</summary>
        public bool SupportsTools { get; set; } = true;

        /// <summary>Whether the runner resolves the endpoint from region/project fields instead of a URL.</summary>
        public bool UsesCloudResourceEndpoint { get; set; }

        /// <summary>
        /// Provider region used when a runner configures no region, in the provider's own identifier style.
        /// Empty for API types that are not region-scoped.
        /// </summary>
        public string DefaultRegion { get; set; } = String.Empty;

        /// <summary>Default tool-calling wire format for this API type.</summary>
        public string DefaultToolCallingApiFormat { get; set; } = ToolCallingApiFormats.OpenAIChatCompletions;

        /// <summary>Whether parallel tool calls default to enabled.</summary>
        public bool SupportsParallelToolCalls { get; set; } = true;

        /// <summary>Whether streaming tool-call deltas default to enabled.</summary>
        public bool SupportsStreamingToolCalls { get; set; } = true;

        /// <summary>Default chat completions path for this API type.</summary>
        public string DefaultChatCompletionsPath { get; set; } = "/v1/chat/completions";

        /// <summary>Default context window used for prompt truncation.</summary>
        public int DefaultContextWindowTokens { get; set; } = 8192;
    }

    /// <summary>
    /// Catalog of the model server API types Wilson supports through PolyPrompt.
    /// </summary>
    public static class ApiTypes
    {
        /// <summary>Ollama API type.</summary>
        public const string Ollama = "Ollama";

        /// <summary>Ollama Cloud API type.</summary>
        public const string OllamaCloud = "OllamaCloud";

        /// <summary>OpenAI API type.</summary>
        public const string OpenAI = "OpenAI";

        /// <summary>OpenAI-compatible API type.</summary>
        public const string OpenAICompatible = "OpenAICompatible";

        /// <summary>Azure OpenAI API type.</summary>
        public const string AzureOpenAI = "AzureOpenAI";

        /// <summary>Anthropic API type.</summary>
        public const string Anthropic = "Anthropic";

        /// <summary>Google Gemini API type.</summary>
        public const string Gemini = "Gemini";

        /// <summary>Google Vertex AI API type.</summary>
        public const string VertexAI = "VertexAI";

        /// <summary>AWS Bedrock API type.</summary>
        public const string Bedrock = "Bedrock";

        /// <summary>Cohere API type.</summary>
        public const string Cohere = "Cohere";

        /// <summary>xAI API type.</summary>
        public const string XAI = "XAI";

        /// <summary>Hugging Face text-embeddings-inference API type.</summary>
        public const string TEI = "TEI";

        /// <summary>VoyageAI API type.</summary>
        public const string VoyageAI = "VoyageAI";

        /// <summary>
        /// AWS region used for Bedrock runners that configure no region and set neither
        /// <c>AWS_REGION</c> nor <c>AWS_DEFAULT_REGION</c>.
        /// </summary>
        public const string DefaultBedrockRegion = "us-east-1";

        /// <summary>
        /// Google Cloud region used for Vertex AI runners that configure no region and set neither
        /// <c>GOOGLE_CLOUD_REGION</c> nor <c>GOOGLE_CLOUD_LOCATION</c>. Google locations use identifiers such as
        /// <c>us-central1</c>; an AWS-style value such as <c>us-east-1</c> would build the invalid endpoint
        /// <c>https://us-east-1-aiplatform.googleapis.com</c>.
        /// </summary>
        public const string DefaultVertexRegion = "us-central1";

        /// <summary>
        /// API type used when a runner does not specify one.
        /// </summary>
        public const string Default = Ollama;

        private static readonly List<ApiTypeDescriptor> _Descriptors = new List<ApiTypeDescriptor>
        {
            new ApiTypeDescriptor
            {
                Name = Ollama,
                DisplayName = "Ollama",
                DefaultEndpoint = "http://localhost:11434",
                DefaultHealthPath = "/api/tags",
                DefaultToolCallingApiFormat = ToolCallingApiFormats.OllamaChat,
                DefaultChatCompletionsPath = String.Empty
            },
            new ApiTypeDescriptor
            {
                Name = OllamaCloud,
                DisplayName = "Ollama Cloud",
                DefaultEndpoint = "https://ollama.com",
                DefaultHealthPath = "/api/tags",
                RequiresApiKey = true,
                DefaultToolCallingApiFormat = ToolCallingApiFormats.OllamaChat,
                DefaultChatCompletionsPath = String.Empty
            },
            new ApiTypeDescriptor
            {
                Name = OpenAI,
                DisplayName = "OpenAI",
                DefaultEndpoint = "https://api.openai.com",
                DefaultHealthPath = "/v1/models"
            },
            new ApiTypeDescriptor
            {
                Name = OpenAICompatible,
                DisplayName = "OpenAI compatible",
                DefaultEndpoint = String.Empty,
                DefaultHealthPath = "/v1/models"
            },
            new ApiTypeDescriptor
            {
                Name = AzureOpenAI,
                DisplayName = "Azure OpenAI",
                DefaultEndpoint = String.Empty,
                DefaultHealthPath = "/openai/models?api-version=2024-10-21",
                RequiresApiKey = true,
                UsesCloudResourceEndpoint = true,
                DefaultToolCallingApiFormat = ToolCallingApiFormats.PolyPromptNative,
                DefaultChatCompletionsPath = String.Empty
            },
            new ApiTypeDescriptor
            {
                Name = Anthropic,
                DisplayName = "Anthropic",
                DefaultEndpoint = "https://api.anthropic.com",
                DefaultHealthPath = "/v1/models",
                RequiresApiKey = true,
                SupportsEmbeddings = false,
                DefaultToolCallingApiFormat = ToolCallingApiFormats.PolyPromptNative,
                DefaultChatCompletionsPath = String.Empty
            },
            new ApiTypeDescriptor
            {
                Name = Gemini,
                DisplayName = "Google Gemini",
                DefaultEndpoint = "https://generativelanguage.googleapis.com",
                DefaultHealthPath = "/v1beta/models",
                RequiresApiKey = true,
                DefaultToolCallingApiFormat = ToolCallingApiFormats.PolyPromptNative,
                DefaultChatCompletionsPath = String.Empty
            },
            new ApiTypeDescriptor
            {
                Name = VertexAI,
                DisplayName = "Google Vertex AI",
                DefaultEndpoint = String.Empty,
                DefaultHealthPath = String.Empty,
                RequiresApiKey = true,
                SupportsModelListing = false,
                UsesCloudResourceEndpoint = true,
                DefaultRegion = DefaultVertexRegion,
                SupportsParallelToolCalls = false,
                DefaultToolCallingApiFormat = ToolCallingApiFormats.PolyPromptNative,
                DefaultChatCompletionsPath = String.Empty
            },
            new ApiTypeDescriptor
            {
                Name = Bedrock,
                DisplayName = "AWS Bedrock",
                DefaultEndpoint = String.Empty,
                DefaultHealthPath = String.Empty,
                RequiresApiKey = true,
                UsesCloudResourceEndpoint = true,
                DefaultRegion = DefaultBedrockRegion,
                SupportsParallelToolCalls = false,
                DefaultToolCallingApiFormat = ToolCallingApiFormats.PolyPromptNative,
                DefaultChatCompletionsPath = String.Empty
            },
            new ApiTypeDescriptor
            {
                Name = Cohere,
                DisplayName = "Cohere",
                DefaultEndpoint = "https://api.cohere.com",
                DefaultHealthPath = "/v1/models",
                RequiresApiKey = true,
                DefaultToolCallingApiFormat = ToolCallingApiFormats.PolyPromptNative,
                DefaultChatCompletionsPath = String.Empty
            },
            new ApiTypeDescriptor
            {
                Name = XAI,
                DisplayName = "xAI",
                DefaultEndpoint = "https://api.x.ai",
                DefaultHealthPath = "/v1/models",
                RequiresApiKey = true,
                DefaultToolCallingApiFormat = ToolCallingApiFormats.PolyPromptNative,
                DefaultChatCompletionsPath = String.Empty
            },
            new ApiTypeDescriptor
            {
                Name = TEI,
                DisplayName = "Text Embeddings Inference",
                DefaultEndpoint = "http://localhost:8080",
                DefaultHealthPath = "/info",
                SupportsChat = false,
                SupportsTools = false,
                SupportsParallelToolCalls = false,
                SupportsStreamingToolCalls = false,
                DefaultToolCallingApiFormat = String.Empty,
                DefaultChatCompletionsPath = String.Empty
            },
            new ApiTypeDescriptor
            {
                Name = VoyageAI,
                DisplayName = "VoyageAI",
                DefaultEndpoint = "https://api.voyageai.com",
                DefaultHealthPath = String.Empty,
                RequiresApiKey = true,
                SupportsChat = false,
                SupportsModelListing = false,
                SupportsTools = false,
                SupportsParallelToolCalls = false,
                SupportsStreamingToolCalls = false,
                DefaultToolCallingApiFormat = String.Empty,
                DefaultChatCompletionsPath = String.Empty
            }
        };

        /// <summary>
        /// All supported API type names in catalog order.
        /// </summary>
        public static IReadOnlyList<string> Supported
        {
            get { return _Descriptors.Select(item => item.Name).ToList(); }
        }

        /// <summary>
        /// All supported API type descriptors in catalog order.
        /// </summary>
        public static IReadOnlyList<ApiTypeDescriptor> DescribeAll()
        {
            return _Descriptors
                .Select(item => new ApiTypeDescriptor
                {
                    Name = item.Name,
                    DisplayName = item.DisplayName,
                    DefaultEndpoint = item.DefaultEndpoint,
                    DefaultHealthPath = item.DefaultHealthPath,
                    RequiresApiKey = item.RequiresApiKey,
                    SupportsChat = item.SupportsChat,
                    SupportsEmbeddings = item.SupportsEmbeddings,
                    SupportsModelListing = item.SupportsModelListing,
                    SupportsTools = item.SupportsTools,
                    UsesCloudResourceEndpoint = item.UsesCloudResourceEndpoint,
                    DefaultRegion = item.DefaultRegion,
                    DefaultToolCallingApiFormat = item.DefaultToolCallingApiFormat,
                    SupportsParallelToolCalls = item.SupportsParallelToolCalls,
                    SupportsStreamingToolCalls = item.SupportsStreamingToolCalls,
                    DefaultChatCompletionsPath = item.DefaultChatCompletionsPath,
                    DefaultContextWindowTokens = item.DefaultContextWindowTokens
                })
                .ToList();
        }

        /// <summary>
        /// Resolve a descriptor for an API type, ignoring case and surrounding whitespace.
        /// </summary>
        /// <param name="apiType">API type value.</param>
        /// <returns>Descriptor, or null when the value is not a supported API type.</returns>
        public static ApiTypeDescriptor? Describe(string? apiType)
        {
            if (String.IsNullOrWhiteSpace(apiType)) return _Descriptors.FirstOrDefault(item => String.Equals(item.Name, Default, StringComparison.OrdinalIgnoreCase));
            string trimmed = apiType.Trim();
            return _Descriptors.FirstOrDefault(item => String.Equals(item.Name, trimmed, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Determine whether an API type is supported.
        /// </summary>
        /// <param name="apiType">API type value.</param>
        /// <returns>True when the API type is cataloged.</returns>
        public static bool IsSupported(string? apiType)
        {
            return Describe(apiType) != null;
        }

        /// <summary>
        /// Return the canonical API type name.
        /// </summary>
        /// <param name="apiType">API type value; blank values resolve to the default API type.</param>
        /// <returns>Canonical API type name.</returns>
        /// <exception cref="ArgumentException">Thrown when the API type is not supported.</exception>
        public static string Normalize(string? apiType)
        {
            ApiTypeDescriptor? descriptor = Describe(apiType);
            if (descriptor == null)
                throw new ArgumentException("Unsupported API type '" + (apiType ?? String.Empty).Trim() + "'. Supported values: " + String.Join(", ", Supported) + ".", nameof(apiType));

            return descriptor.Name;
        }

        /// <summary>
        /// Attempt to canonicalize an API type without throwing.
        /// </summary>
        /// <param name="apiType">API type value.</param>
        /// <param name="normalized">Canonical API type name when supported; otherwise the trimmed input.</param>
        /// <returns>True when the API type is supported.</returns>
        public static bool TryNormalize(string? apiType, out string normalized)
        {
            ApiTypeDescriptor? descriptor = Describe(apiType);
            normalized = descriptor == null ? (apiType ?? String.Empty).Trim() : descriptor.Name;
            return descriptor != null;
        }
    }
}
