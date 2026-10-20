// Provider API type catalog mirroring Wilson.Core.Settings.ApiTypes.
//
// The server exposes the authoritative catalog at GET /v1.0/api/model-runners/api-types;
// this module keeps a local copy so the dashboard can render API type pickers and validate
// runner drafts without an extra round-trip on every keystroke. Keep it in sync with
// src/Wilson.Core/Settings/ApiTypes.cs.

export const TOOL_CALLING_API_FORMATS = {
  ollamaChat: 'OllamaChat',
  openAIChatCompletions: 'OpenAIChatCompletions',
  native: 'PolyPromptNative'
};

export const API_TYPES = [
  {
    name: 'Ollama',
    displayName: 'Ollama',
    description: 'Local Ollama server using the native Ollama API.',
    defaultEndpoint: 'http://localhost:11434',
    defaultHealthPath: '/api/tags',
    healthIntervalMs: 5000,
    healthTimeoutMs: 2000,
    supportsChatCompletions: true,
    supportsEmbeddings: true,
    supportsModelListing: true,
    supportsToolCalls: true,
    toolCallingApiFormat: TOOL_CALLING_API_FORMATS.ollamaChat,
    chatCompletionsPath: '',
    requiresApiKey: false,
    requiresDeployment: false,
    requiresApiVersion: false,
    requiresProjectId: false,
    requiresRegion: false,
    defaultApiVersion: '',
    defaultRegion: ''
  },
  {
    name: 'OllamaCloud',
    displayName: 'Ollama Cloud',
    description: 'Ollama Cloud hosted models using the native Ollama API.',
    defaultEndpoint: 'https://ollama.com',
    defaultHealthPath: '/api/tags',
    healthIntervalMs: 15000,
    healthTimeoutMs: 5000,
    supportsChatCompletions: true,
    supportsEmbeddings: true,
    supportsModelListing: true,
    supportsToolCalls: false,
    toolCallingApiFormat: '',
    chatCompletionsPath: '',
    requiresApiKey: true,
    requiresDeployment: false,
    requiresApiVersion: false,
    requiresProjectId: false,
    requiresRegion: false,
    defaultApiVersion: '',
    defaultRegion: ''
  },
  {
    name: 'OpenAI',
    displayName: 'OpenAI',
    description: 'OpenAI public API using chat completions.',
    defaultEndpoint: 'https://api.openai.com',
    defaultHealthPath: '/v1/models',
    healthIntervalMs: 15000,
    healthTimeoutMs: 5000,
    supportsChatCompletions: true,
    supportsEmbeddings: true,
    supportsModelListing: true,
    supportsToolCalls: true,
    toolCallingApiFormat: TOOL_CALLING_API_FORMATS.openAIChatCompletions,
    chatCompletionsPath: '/v1/chat/completions',
    requiresApiKey: true,
    requiresDeployment: false,
    requiresApiVersion: false,
    requiresProjectId: false,
    requiresRegion: false,
    defaultApiVersion: '',
    defaultRegion: ''
  },
  {
    name: 'OpenAICompatible',
    displayName: 'OpenAI compatible',
    description: 'Any server that implements the OpenAI chat-completions contract.',
    defaultEndpoint: 'http://localhost:8000',
    defaultHealthPath: '/v1/models',
    healthIntervalMs: 15000,
    healthTimeoutMs: 5000,
    supportsChatCompletions: true,
    supportsEmbeddings: true,
    supportsModelListing: true,
    supportsToolCalls: true,
    toolCallingApiFormat: TOOL_CALLING_API_FORMATS.openAIChatCompletions,
    chatCompletionsPath: '/v1/chat/completions',
    requiresApiKey: false,
    requiresDeployment: false,
    requiresApiVersion: false,
    requiresProjectId: false,
    requiresRegion: false,
    defaultApiVersion: '',
    defaultRegion: ''
  },
  {
    name: 'AzureOpenAI',
    displayName: 'Azure OpenAI',
    description: 'Azure OpenAI deployments. Requires a deployment name and API version.',
    defaultEndpoint: 'https://example.openai.azure.com',
    defaultHealthPath: '/openai/models',
    healthIntervalMs: 15000,
    healthTimeoutMs: 5000,
    supportsChatCompletions: true,
    supportsEmbeddings: true,
    supportsModelListing: true,
    supportsToolCalls: true,
    // Azure is deployment-scoped and authenticates with the api-key header, so tool calls go
    // through the provider-native PolyPrompt client rather than the generic chat-completions sender.
    toolCallingApiFormat: TOOL_CALLING_API_FORMATS.native,
    chatCompletionsPath: '',
    requiresApiKey: true,
    requiresDeployment: true,
    requiresApiVersion: true,
    requiresProjectId: false,
    requiresRegion: false,
    defaultApiVersion: '2024-10-21',
    defaultRegion: ''
  },
  {
    name: 'XAI',
    displayName: 'xAI Grok',
    description: 'xAI Grok using the native Responses API, including hosted tools.',
    defaultEndpoint: 'https://api.x.ai',
    defaultHealthPath: '/v1/models',
    healthIntervalMs: 15000,
    healthTimeoutMs: 5000,
    supportsChatCompletions: true,
    supportsEmbeddings: true,
    supportsModelListing: true,
    supportsToolCalls: true,
    toolCallingApiFormat: TOOL_CALLING_API_FORMATS.native,
    chatCompletionsPath: '',
    requiresApiKey: true,
    requiresDeployment: false,
    requiresApiVersion: false,
    requiresProjectId: false,
    requiresRegion: false,
    defaultApiVersion: '',
    defaultRegion: ''
  },
  {
    name: 'Anthropic',
    displayName: 'Anthropic',
    description: 'Anthropic Claude Messages API.',
    defaultEndpoint: 'https://api.anthropic.com',
    defaultHealthPath: '/v1/models',
    healthIntervalMs: 15000,
    healthTimeoutMs: 5000,
    supportsChatCompletions: true,
    supportsEmbeddings: false,
    supportsModelListing: true,
    supportsToolCalls: true,
    toolCallingApiFormat: TOOL_CALLING_API_FORMATS.native,
    chatCompletionsPath: '',
    requiresApiKey: true,
    requiresDeployment: false,
    requiresApiVersion: false,
    requiresProjectId: false,
    requiresRegion: false,
    defaultApiVersion: '',
    defaultRegion: ''
  },
  {
    name: 'Gemini',
    displayName: 'Google Gemini',
    description: 'Google Gemini API using generateContent.',
    defaultEndpoint: 'https://generativelanguage.googleapis.com',
    defaultHealthPath: '/v1beta/models',
    healthIntervalMs: 15000,
    healthTimeoutMs: 5000,
    supportsChatCompletions: true,
    supportsEmbeddings: true,
    supportsModelListing: true,
    supportsToolCalls: true,
    toolCallingApiFormat: TOOL_CALLING_API_FORMATS.native,
    chatCompletionsPath: '',
    requiresApiKey: true,
    requiresDeployment: false,
    requiresApiVersion: false,
    requiresProjectId: false,
    requiresRegion: false,
    defaultApiVersion: '',
    defaultRegion: ''
  },
  {
    name: 'VertexAI',
    displayName: 'Google Vertex AI',
    description: 'Vertex AI Gemini models. Requires a project and region plus a bearer token or ADC.',
    defaultEndpoint: '',
    defaultHealthPath: '',
    healthIntervalMs: 30000,
    healthTimeoutMs: 8000,
    supportsChatCompletions: true,
    supportsEmbeddings: false,
    supportsModelListing: false,
    supportsToolCalls: false,
    toolCallingApiFormat: '',
    chatCompletionsPath: '',
    requiresApiKey: true,
    requiresDeployment: false,
    requiresApiVersion: false,
    requiresProjectId: true,
    requiresRegion: true,
    defaultApiVersion: '',
    defaultRegion: 'us-central1'
  },
  {
    name: 'Bedrock',
    displayName: 'AWS Bedrock',
    description: 'AWS Bedrock model runtime. Requires a region plus static credentials or the AWS environment chain.',
    defaultEndpoint: '',
    defaultHealthPath: '',
    healthIntervalMs: 30000,
    healthTimeoutMs: 8000,
    supportsChatCompletions: true,
    supportsEmbeddings: true,
    supportsModelListing: true,
    supportsToolCalls: true,
    toolCallingApiFormat: TOOL_CALLING_API_FORMATS.native,
    chatCompletionsPath: '',
    requiresApiKey: false,
    requiresDeployment: false,
    requiresApiVersion: false,
    requiresProjectId: false,
    requiresRegion: true,
    defaultApiVersion: '',
    defaultRegion: 'us-east-1'
  },
  {
    name: 'Cohere',
    displayName: 'Cohere',
    description: 'Cohere platform API for chat and embeddings.',
    defaultEndpoint: 'https://api.cohere.com',
    defaultHealthPath: '/v1/models',
    healthIntervalMs: 15000,
    healthTimeoutMs: 5000,
    supportsChatCompletions: true,
    supportsEmbeddings: true,
    supportsModelListing: true,
    supportsToolCalls: true,
    toolCallingApiFormat: TOOL_CALLING_API_FORMATS.native,
    chatCompletionsPath: '',
    requiresApiKey: true,
    requiresDeployment: false,
    requiresApiVersion: false,
    requiresProjectId: false,
    requiresRegion: false,
    defaultApiVersion: '',
    defaultRegion: ''
  },
  {
    name: 'VoyageAI',
    displayName: 'VoyageAI',
    description: 'VoyageAI embedding models.',
    defaultEndpoint: 'https://api.voyageai.com',
    defaultHealthPath: '',
    healthIntervalMs: 30000,
    healthTimeoutMs: 5000,
    supportsChatCompletions: false,
    supportsEmbeddings: true,
    supportsModelListing: false,
    supportsToolCalls: false,
    toolCallingApiFormat: '',
    chatCompletionsPath: '',
    requiresApiKey: true,
    requiresDeployment: false,
    requiresApiVersion: false,
    requiresProjectId: false,
    requiresRegion: false,
    defaultApiVersion: '',
    defaultRegion: ''
  },
  {
    name: 'Tei',
    displayName: 'Text Embeddings Inference',
    description: 'Hugging Face Text Embeddings Inference server.',
    defaultEndpoint: 'http://localhost:8080',
    defaultHealthPath: '/info',
    healthIntervalMs: 5000,
    healthTimeoutMs: 2000,
    supportsChatCompletions: false,
    supportsEmbeddings: true,
    supportsModelListing: true,
    supportsToolCalls: false,
    toolCallingApiFormat: '',
    chatCompletionsPath: '',
    requiresApiKey: false,
    requiresDeployment: false,
    requiresApiVersion: false,
    requiresProjectId: false,
    requiresRegion: false,
    defaultApiVersion: '',
    defaultRegion: ''
  }
];

export const API_TYPE_NAMES = API_TYPES.map(item => item.name);

const OLLAMA_FALLBACK = API_TYPES[0];

// Resolve an API type descriptor by name, case- and whitespace-insensitively.
// Unrecognized values fall back to the Ollama descriptor so legacy configuration
// keeps rendering instead of breaking the runner editor.
export function describeApiType(apiType) {
  const normalized = String(apiType || '').trim().toLowerCase();
  if (!normalized) return OLLAMA_FALLBACK;
  return API_TYPES.find(item => item.name.toLowerCase() === normalized) || OLLAMA_FALLBACK;
}

// Canonicalize an API type name. Unrecognized values are returned trimmed but otherwise
// unchanged, matching the server's ApiTypes.Normalize behavior.
export function normalizeApiType(apiType) {
  const normalized = String(apiType || '').trim().toLowerCase();
  const match = API_TYPES.find(item => item.name.toLowerCase() === normalized);
  return match ? match.name : String(apiType || '').trim();
}

export function isSupportedApiType(apiType) {
  const normalized = String(apiType || '').trim().toLowerCase();
  return API_TYPES.some(item => item.name.toLowerCase() === normalized);
}

// Build the default endpoint for an API type when the endpoint field is blank.
export function defaultEndpointFor(apiType) {
  return describeApiType(apiType).defaultEndpoint;
}

// Build default health probe settings for an API type.
export function defaultHealthSettings(apiType, endpoint, apiKey = '') {
  const descriptor = describeApiType(apiType);
  const base = String(endpoint || descriptor.defaultEndpoint || '').replace(/\/+$/, '');
  const path = descriptor.defaultHealthPath || '';
  return {
    healthCheckEnabled: true,
    healthCheckUrl: path ? `${base}${path}` : base,
    healthCheckMethod: 'GET',
    healthCheckIntervalMs: descriptor.healthIntervalMs,
    healthCheckTimeoutMs: descriptor.healthTimeoutMs,
    healthCheckExpectedStatusCode: 200,
    healthyThreshold: 2,
    unhealthyThreshold: 2,
    healthCheckUseAuth: descriptor.requiresApiKey && Boolean(apiKey)
  };
}

// Build default tool-calling settings for an API type.
export function defaultToolSettings(apiType) {
  const descriptor = describeApiType(apiType);
  return {
    toolsEnabled: true,
    supportsTools: descriptor.supportsToolCalls,
    toolCallingApiFormat: descriptor.toolCallingApiFormat,
    supportsParallelToolCalls: descriptor.supportsToolCalls,
    supportsStreamingToolCalls: descriptor.supportsToolCalls,
    chatCompletionsPath: descriptor.chatCompletionsPath
  };
}

// Validate a model runner draft against the provider catalog. Returns an array of
// error messages; an empty array means the draft is acceptable.
export function validateRunnerDraft(runner, existingIds = []) {
  const errors = [];
  if (!runner) return ['Model server configuration is missing.'];

  const id = String(runner.id || '').trim();
  if (!id) errors.push('Server ID is required.');

  const duplicate = existingIds.filter(existing => String(existing || '').toLowerCase() === id.toLowerCase());
  if (id && duplicate.length > 1) errors.push(`Server ID "${id}" is used by more than one model server.`);

  const apiType = String(runner.apiType || '').trim();
  if (!apiType) {
    errors.push('API type is required.');
    return errors;
  }

  if (!isSupportedApiType(apiType)) {
    errors.push(`API type "${apiType}" is not supported. Choose one of: ${API_TYPE_NAMES.join(', ')}.`);
    return errors;
  }

  const descriptor = describeApiType(apiType);
  const endpoint = String(runner.endpoint || '').trim();
  // Every cataloged provider either has a default endpoint or derives its routing from other
  // fields (AWS Bedrock uses the AWS SDK, Vertex AI builds a regional resource URL from the
  // project and region), so a blank endpoint is never an error. A supplied endpoint must still
  // be an absolute http or https URL.
  if (endpoint && !/^https?:\/\//i.test(endpoint)) errors.push('Endpoint must be an absolute http or https URL.');

  if (descriptor.requiresApiKey && !String(runner.apiKey || '').trim()) {
    errors.push(`${descriptor.displayName} requires an API key.`);
  }

  if (descriptor.requiresDeployment && !String(runner.deployment || '').trim()) {
    errors.push(`${descriptor.displayName} requires a deployment name.`);
  }

  if (descriptor.requiresApiVersion && !String(runner.apiVersion || '').trim()) {
    errors.push(`${descriptor.displayName} requires an API version.`);
  }

  if (descriptor.requiresProjectId && !String(runner.projectId || '').trim()) {
    errors.push(`${descriptor.displayName} requires a project identifier.`);
  }

  if (descriptor.requiresRegion && !String(runner.region || '').trim()) {
    errors.push(`${descriptor.displayName} requires a region.`);
  }

  const format = String(runner.toolCallingApiFormat || '').trim();
  if (runner.supportsTools && format && !Object.values(TOOL_CALLING_API_FORMATS).includes(format)) {
    errors.push(`Tool API format "${format}" is not supported.`);
  }

  if (runner.supportsTools && !descriptor.supportsToolCalls) {
    errors.push(`${descriptor.displayName} does not support tool calls.`);
  }

  return errors;
}
