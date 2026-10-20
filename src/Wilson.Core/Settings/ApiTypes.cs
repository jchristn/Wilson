namespace Wilson.Core.Settings
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>
    /// Canonical API type names understood by Wilson. Each value maps to a PolyPrompt client family.
    /// </summary>
    public static class ApiTypes
    {
        /// <summary>Local Ollama server (native Ollama API).</summary>
        public const string Ollama = "Ollama";

        /// <summary>Ollama Cloud hosted service (native Ollama API over TLS).</summary>
        public const string OllamaCloud = "OllamaCloud";

        /// <summary>OpenAI public API.</summary>
        public const string OpenAI = "OpenAI";

        /// <summary>Any OpenAI-compatible chat-completions endpoint.</summary>
        public const string OpenAICompatible = "OpenAICompatible";

        /// <summary>Azure OpenAI service.</summary>
        public const string AzureOpenAI = "AzureOpenAI";

        /// <summary>xAI Grok (native Responses API).</summary>
        public const string XAI = "XAI";

        /// <summary>Anthropic Claude Messages API.</summary>
        public const string Anthropic = "Anthropic";

        /// <summary>Google Gemini API.</summary>
        public const string Gemini = "Gemini";

        /// <summary>Google Vertex AI Gemini models.</summary>
        public const string VertexAI = "VertexAI";

        /// <summary>AWS Bedrock model runtime.</summary>
        public const string Bedrock = "Bedrock";

        /// <summary>Cohere platform API.</summary>
        public const string Cohere = "Cohere";

        /// <summary>VoyageAI embeddings API.</summary>
        public const string VoyageAI = "VoyageAI";

        /// <summary>Hugging Face Text Embeddings Inference (TEI) server.</summary>
        public const string Tei = "Tei";

        /// <summary>
        /// All supported API types in display order.
        /// </summary>
        public static readonly IReadOnlyList<string> All = new List<string>
        {
            Ollama,
            OllamaCloud,
            OpenAI,
            OpenAICompatible,
            AzureOpenAI,
            XAI,
            Anthropic,
            Gemini,
            VertexAI,
            Bedrock,
            Cohere,
            VoyageAI,
            Tei
        };

        /// <summary>
        /// Describe every supported API type.
        /// </summary>
        /// <returns>Descriptors in display order.</returns>
        public static List<ApiTypeDescriptor> Describe()
        {
            return All.Select(Describe).ToList();
        }

        /// <summary>
        /// Describe a single API type, falling back to the Ollama descriptor for unrecognized values.
        /// </summary>
        /// <param name="apiType">API type name.</param>
        /// <returns>Descriptor for the API type.</returns>
        public static ApiTypeDescriptor Describe(string? apiType)
        {
            string normalized = Normalize(apiType);
            switch (normalized)
            {
                case OllamaCloud:
                    return new ApiTypeDescriptor
                    {
                        Name = OllamaCloud,
                        DisplayName = "Ollama Cloud",
                        Description = "Ollama Cloud hosted models using the native Ollama API.",
                        DefaultEndpoint = "https://ollama.com",
                        DefaultHealthPath = "/api/tags",
                        DefaultHealthIntervalMs = 15000,
                        DefaultHealthTimeoutMs = 5000,
                        SupportsChatCompletions = true,
                        SupportsEmbeddings = true,
                        SupportsModelListing = true,
                        SupportsToolCalls = false,
                        SupportsStreaming = true,
                        DefaultToolCallingApiFormat = String.Empty,
                        RequiresApiKey = true
                    };
                case OpenAI:
                    return new ApiTypeDescriptor
                    {
                        Name = OpenAI,
                        DisplayName = "OpenAI",
                        Description = "OpenAI public API using chat completions.",
                        DefaultEndpoint = "https://api.openai.com",
                        DefaultHealthPath = "/v1/models",
                        DefaultHealthIntervalMs = 15000,
                        DefaultHealthTimeoutMs = 5000,
                        SupportsChatCompletions = true,
                        SupportsEmbeddings = true,
                        SupportsModelListing = true,
                        SupportsToolCalls = true,
                        SupportsStreaming = true,
                        DefaultToolCallingApiFormat = ToolCallingApiFormats.OpenAIChatCompletions,
                        DefaultChatCompletionsPath = "/v1/chat/completions",
                        RequiresApiKey = true
                    };
                case OpenAICompatible:
                    return new ApiTypeDescriptor
                    {
                        Name = OpenAICompatible,
                        DisplayName = "OpenAI compatible",
                        Description = "Any server that implements the OpenAI chat-completions contract.",
                        DefaultEndpoint = "http://localhost:8000",
                        DefaultHealthPath = "/v1/models",
                        DefaultHealthIntervalMs = 15000,
                        DefaultHealthTimeoutMs = 5000,
                        SupportsChatCompletions = true,
                        SupportsEmbeddings = true,
                        SupportsModelListing = true,
                        SupportsToolCalls = true,
                        SupportsStreaming = true,
                        DefaultToolCallingApiFormat = ToolCallingApiFormats.OpenAIChatCompletions,
                        DefaultChatCompletionsPath = "/v1/chat/completions",
                        RequiresApiKey = false
                    };
                case AzureOpenAI:
                    return new ApiTypeDescriptor
                    {
                        Name = AzureOpenAI,
                        DisplayName = "Azure OpenAI",
                        Description = "Azure OpenAI deployments. Requires a deployment name and API version.",
                        DefaultEndpoint = "https://example.openai.azure.com",
                        DefaultHealthPath = "/openai/models",
                        DefaultHealthIntervalMs = 15000,
                        DefaultHealthTimeoutMs = 5000,
                        SupportsChatCompletions = true,
                        SupportsEmbeddings = true,
                        SupportsModelListing = true,
                        SupportsToolCalls = true,
                        SupportsStreaming = true,
                        // Azure routes tools through its own PolyPrompt client, which builds the
                        // /openai/deployments/{deployment}/... URL with an api-version query and
                        // authenticates with the api-key header. The generic OpenAI-compatible
                        // sender would post to /v1/chat/completions with a bearer token instead.
                        DefaultToolCallingApiFormat = ToolCallingApiFormats.Native,
                        RequiresApiKey = true,
                        RequiresDeployment = true,
                        RequiresApiVersion = true,
                        DefaultApiVersion = "2024-10-21"
                    };
                case XAI:
                    return new ApiTypeDescriptor
                    {
                        Name = XAI,
                        DisplayName = "xAI Grok",
                        Description = "xAI Grok using the native Responses API, including hosted tools.",
                        DefaultEndpoint = "https://api.x.ai",
                        DefaultHealthPath = "/v1/models",
                        DefaultHealthIntervalMs = 15000,
                        DefaultHealthTimeoutMs = 5000,
                        SupportsChatCompletions = true,
                        SupportsEmbeddings = true,
                        SupportsModelListing = true,
                        SupportsToolCalls = true,
                        SupportsStreaming = true,
                        DefaultToolCallingApiFormat = ToolCallingApiFormats.Native,
                        RequiresApiKey = true
                    };
                case Anthropic:
                    return new ApiTypeDescriptor
                    {
                        Name = Anthropic,
                        DisplayName = "Anthropic",
                        Description = "Anthropic Claude Messages API.",
                        DefaultEndpoint = "https://api.anthropic.com",
                        DefaultHealthPath = "/v1/models",
                        DefaultHealthIntervalMs = 15000,
                        DefaultHealthTimeoutMs = 5000,
                        SupportsChatCompletions = true,
                        SupportsEmbeddings = false,
                        SupportsModelListing = true,
                        SupportsToolCalls = true,
                        SupportsStreaming = true,
                        DefaultToolCallingApiFormat = ToolCallingApiFormats.Native,
                        RequiresApiKey = true
                    };
                case Gemini:
                    return new ApiTypeDescriptor
                    {
                        Name = Gemini,
                        DisplayName = "Google Gemini",
                        Description = "Google Gemini API using generateContent.",
                        DefaultEndpoint = "https://generativelanguage.googleapis.com",
                        DefaultHealthPath = "/v1beta/models",
                        DefaultHealthIntervalMs = 15000,
                        DefaultHealthTimeoutMs = 5000,
                        SupportsChatCompletions = true,
                        SupportsEmbeddings = true,
                        SupportsModelListing = true,
                        SupportsToolCalls = true,
                        SupportsStreaming = true,
                        DefaultToolCallingApiFormat = ToolCallingApiFormats.Native,
                        RequiresApiKey = true
                    };
                case VertexAI:
                    return new ApiTypeDescriptor
                    {
                        Name = VertexAI,
                        DisplayName = "Google Vertex AI",
                        Description = "Vertex AI Gemini models. Requires a project and region plus a bearer token or ADC.",
                        DefaultEndpoint = String.Empty,
                        DefaultHealthPath = String.Empty,
                        DefaultHealthIntervalMs = 30000,
                        DefaultHealthTimeoutMs = 8000,
                        SupportsChatCompletions = true,
                        SupportsEmbeddings = false,
                        SupportsModelListing = false,
                        SupportsToolCalls = false,
                        SupportsStreaming = true,
                        DefaultToolCallingApiFormat = String.Empty,
                        RequiresApiKey = true,
                        RequiresProjectId = true,
                        RequiresRegion = true,
                        DefaultRegion = "us-central1"
                    };
                case Bedrock:
                    return new ApiTypeDescriptor
                    {
                        Name = Bedrock,
                        DisplayName = "AWS Bedrock",
                        Description = "AWS Bedrock model runtime. Requires a region plus static credentials or the AWS environment chain.",
                        DefaultEndpoint = String.Empty,
                        DefaultHealthPath = String.Empty,
                        DefaultHealthIntervalMs = 30000,
                        DefaultHealthTimeoutMs = 8000,
                        SupportsChatCompletions = true,
                        SupportsEmbeddings = true,
                        SupportsModelListing = true,
                        SupportsToolCalls = true,
                        SupportsStreaming = true,
                        DefaultToolCallingApiFormat = ToolCallingApiFormats.Native,
                        RequiresApiKey = false,
                        RequiresRegion = true,
                        DefaultRegion = "us-east-1"
                    };
                case Cohere:
                    return new ApiTypeDescriptor
                    {
                        Name = Cohere,
                        DisplayName = "Cohere",
                        Description = "Cohere platform API for chat and embeddings.",
                        DefaultEndpoint = "https://api.cohere.com",
                        DefaultHealthPath = "/v1/models",
                        DefaultHealthIntervalMs = 15000,
                        DefaultHealthTimeoutMs = 5000,
                        SupportsChatCompletions = true,
                        SupportsEmbeddings = true,
                        SupportsModelListing = true,
                        SupportsToolCalls = true,
                        SupportsStreaming = true,
                        DefaultToolCallingApiFormat = ToolCallingApiFormats.Native,
                        RequiresApiKey = true
                    };
                case VoyageAI:
                    return new ApiTypeDescriptor
                    {
                        Name = VoyageAI,
                        DisplayName = "VoyageAI",
                        Description = "VoyageAI embedding models.",
                        DefaultEndpoint = "https://api.voyageai.com",
                        DefaultHealthPath = String.Empty,
                        DefaultHealthIntervalMs = 30000,
                        DefaultHealthTimeoutMs = 5000,
                        SupportsChatCompletions = false,
                        SupportsEmbeddings = true,
                        SupportsModelListing = false,
                        SupportsToolCalls = false,
                        SupportsStreaming = false,
                        DefaultToolCallingApiFormat = String.Empty,
                        RequiresApiKey = true
                    };
                case Tei:
                    return new ApiTypeDescriptor
                    {
                        Name = Tei,
                        DisplayName = "Text Embeddings Inference",
                        Description = "Hugging Face Text Embeddings Inference server.",
                        DefaultEndpoint = "http://localhost:8080",
                        DefaultHealthPath = "/info",
                        DefaultHealthIntervalMs = 5000,
                        DefaultHealthTimeoutMs = 2000,
                        SupportsChatCompletions = false,
                        SupportsEmbeddings = true,
                        SupportsModelListing = true,
                        SupportsToolCalls = false,
                        SupportsStreaming = false,
                        DefaultToolCallingApiFormat = String.Empty,
                        RequiresApiKey = false
                    };
                default:
                    return new ApiTypeDescriptor
                    {
                        Name = Ollama,
                        DisplayName = "Ollama",
                        Description = "Local Ollama server using the native Ollama API.",
                        DefaultEndpoint = "http://localhost:11434",
                        DefaultHealthPath = "/api/tags",
                        DefaultHealthIntervalMs = 5000,
                        DefaultHealthTimeoutMs = 2000,
                        SupportsChatCompletions = true,
                        SupportsEmbeddings = true,
                        SupportsModelListing = true,
                        SupportsToolCalls = true,
                        SupportsStreaming = true,
                        DefaultToolCallingApiFormat = ToolCallingApiFormats.OllamaChat,
                        RequiresApiKey = false
                    };
            }
        }

        /// <summary>
        /// Describe one API type by name, or null when the name is not in the catalog.
        /// </summary>
        /// <param name="apiType">API type name.</param>
        /// <returns>Descriptor, or null when unrecognized.</returns>
        public static ApiTypeDescriptor? TryDescribe(string? apiType)
        {
            if (!TryNormalize(apiType, out string normalized)) return null;
            return Describe(normalized);
        }

        /// <summary>
        /// Determine whether a value is a cataloged API type.
        /// </summary>
        /// <param name="apiType">API type name.</param>
        /// <returns>True when the value is cataloged.</returns>
        public static bool IsSupported(string? apiType)
        {
            return TryNormalize(apiType, out _);
        }

        /// <summary>
        /// Normalize an API type to its canonical casing. Unrecognized values are returned trimmed but
        /// otherwise unchanged so existing configuration is preserved verbatim.
        /// </summary>
        /// <param name="apiType">API type name.</param>
        /// <returns>Canonical API type name, or the trimmed input.</returns>
        public static string Normalize(string? apiType)
        {
            if (TryNormalize(apiType, out string normalized)) return normalized;
            return (apiType ?? String.Empty).Trim();
        }

        /// <summary>
        /// Attempt to normalize an API type to its canonical casing.
        /// </summary>
        /// <param name="apiType">API type name.</param>
        /// <param name="normalized">Canonical name when recognized.</param>
        /// <returns>True when the value matched a cataloged API type.</returns>
        public static bool TryNormalize(string? apiType, out string normalized)
        {
            normalized = String.Empty;
            if (String.IsNullOrWhiteSpace(apiType)) return false;
            string trimmed = apiType.Trim();
            foreach (string candidate in All)
            {
                if (!String.Equals(candidate, trimmed, StringComparison.OrdinalIgnoreCase)) continue;
                normalized = candidate;
                return true;
            }

            return false;
        }
    }

    /// <summary>
    /// Tool-calling wire formats used by model runners.
    /// </summary>
    public static class ToolCallingApiFormats
    {
        /// <summary>Provider-native tool calling through PolyPrompt tool-chat clients.</summary>
        public const string Native = "PolyPromptNative";

        /// <summary>OpenAI chat-completions tool calling.</summary>
        public const string OpenAIChatCompletions = "OpenAIChatCompletions";

        /// <summary>Ollama native /api/chat tool calling.</summary>
        public const string OllamaChat = "OllamaChat";

        /// <summary>All supported tool-calling formats.</summary>
        public static readonly IReadOnlyList<string> All = new List<string>
        {
            OllamaChat,
            OpenAIChatCompletions,
            Native
        };
    }

    /// <summary>
    /// Capabilities and defaults for one API type.
    /// </summary>
    public class ApiTypeDescriptor
    {
        /// <summary>Canonical API type name.</summary>
        public string Name { get; set; } = String.Empty;

        /// <summary>Human-readable display name.</summary>
        public string DisplayName { get; set; } = String.Empty;

        /// <summary>Short description of the provider and its transport.</summary>
        public string Description { get; set; } = String.Empty;

        /// <summary>Default endpoint URL. Empty when the endpoint is derived from other fields.</summary>
        public string DefaultEndpoint { get; set; } = String.Empty;

        /// <summary>Default health probe path. Empty when no probe path is available.</summary>
        public string DefaultHealthPath { get; set; } = String.Empty;

        /// <summary>Default health probe interval in milliseconds.</summary>
        public int DefaultHealthIntervalMs { get; set; } = 15000;

        /// <summary>Default health probe timeout in milliseconds.</summary>
        public int DefaultHealthTimeoutMs { get; set; } = 5000;

        /// <summary>Whether the provider supports chat and completion requests.</summary>
        public bool SupportsChatCompletions { get; set; }

        /// <summary>Whether the provider supports embedding requests.</summary>
        public bool SupportsEmbeddings { get; set; }

        /// <summary>Whether the provider supports model listing.</summary>
        public bool SupportsModelListing { get; set; }

        /// <summary>Whether the provider supports native tool calls.</summary>
        public bool SupportsToolCalls { get; set; }

        /// <summary>Whether the provider supports streaming responses.</summary>
        public bool SupportsStreaming { get; set; }

        /// <summary>Default tool-calling wire format. Empty when tool calls are unsupported.</summary>
        public string DefaultToolCallingApiFormat { get; set; } = String.Empty;

        /// <summary>Default chat-completions path for OpenAI-compatible transports.</summary>
        public string DefaultChatCompletionsPath { get; set; } = String.Empty;

        /// <summary>Whether an API key (or bearer token) is required.</summary>
        public bool RequiresApiKey { get; set; }

        /// <summary>Whether a deployment name is required (Azure OpenAI).</summary>
        public bool RequiresDeployment { get; set; }

        /// <summary>Whether an API version is required (Azure OpenAI).</summary>
        public bool RequiresApiVersion { get; set; }

        /// <summary>Default API version when the provider requires one.</summary>
        public string DefaultApiVersion { get; set; } = String.Empty;

        /// <summary>Whether a project identifier is required (Vertex AI).</summary>
        public bool RequiresProjectId { get; set; }

        /// <summary>Whether a region is required (Vertex AI, Bedrock).</summary>
        public bool RequiresRegion { get; set; }

        /// <summary>Default region when the provider requires one.</summary>
        public string DefaultRegion { get; set; } = String.Empty;
    }
}
