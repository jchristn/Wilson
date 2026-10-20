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

// Health probe authentication schemes, mirroring Wilson.Core.Settings.HealthCheckAuthSchemes.
export const HEALTH_CHECK_AUTH_SCHEMES = {
  bearer: 'Bearer',
  anthropicApiKey: 'AnthropicApiKey',
  googleApiKey: 'GoogleApiKey',
  azureApiKey: 'AzureApiKey',
  none: 'None'
};

export const API_TYPES = [
  {
    name: 'Ollama',
    displayName: 'Ollama',
    description: 'Local Ollama server using the native Ollama API.',
    defaultEndpoint: 'http://localhost:11434',
    defaultHealthPath: '/api/tags',
    supportsHealthChecks: true,
    healthCheckAuthScheme: HEALTH_CHECK_AUTH_SCHEMES.bearer,
    healthCheckVersion: '',
    healthCheckRequiresApiVersion: false,
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
    supportsHealthChecks: true,
    healthCheckAuthScheme: HEALTH_CHECK_AUTH_SCHEMES.bearer,
    healthCheckVersion: '',
    healthCheckRequiresApiVersion: false,
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
    supportsHealthChecks: true,
    healthCheckAuthScheme: HEALTH_CHECK_AUTH_SCHEMES.bearer,
    healthCheckVersion: '',
    healthCheckRequiresApiVersion: false,
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
    supportsHealthChecks: true,
    healthCheckAuthScheme: HEALTH_CHECK_AUTH_SCHEMES.bearer,
    healthCheckVersion: '',
    healthCheckRequiresApiVersion: false,
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
    supportsHealthChecks: true,
    healthCheckAuthScheme: HEALTH_CHECK_AUTH_SCHEMES.azureApiKey,
    healthCheckVersion: '',
    healthCheckRequiresApiVersion: true,
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
    supportsHealthChecks: true,
    healthCheckAuthScheme: HEALTH_CHECK_AUTH_SCHEMES.bearer,
    healthCheckVersion: '',
    healthCheckRequiresApiVersion: false,
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
    supportsHealthChecks: true,
    healthCheckAuthScheme: HEALTH_CHECK_AUTH_SCHEMES.anthropicApiKey,
    healthCheckVersion: '2023-06-01',
    healthCheckRequiresApiVersion: false,
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
    supportsHealthChecks: true,
    healthCheckAuthScheme: HEALTH_CHECK_AUTH_SCHEMES.googleApiKey,
    healthCheckVersion: '',
    healthCheckRequiresApiVersion: false,
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
    supportsHealthChecks: false,
    healthCheckAuthScheme: HEALTH_CHECK_AUTH_SCHEMES.bearer,
    healthCheckVersion: '',
    healthCheckRequiresApiVersion: false,
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
    supportsHealthChecks: false,
    healthCheckAuthScheme: HEALTH_CHECK_AUTH_SCHEMES.none,
    healthCheckVersion: '',
    healthCheckRequiresApiVersion: false,
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
    supportsHealthChecks: true,
    healthCheckAuthScheme: HEALTH_CHECK_AUTH_SCHEMES.bearer,
    healthCheckVersion: '',
    healthCheckRequiresApiVersion: false,
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
    supportsHealthChecks: false,
    healthCheckAuthScheme: HEALTH_CHECK_AUTH_SCHEMES.bearer,
    healthCheckVersion: '',
    healthCheckRequiresApiVersion: false,
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
    supportsHealthChecks: true,
    healthCheckAuthScheme: HEALTH_CHECK_AUTH_SCHEMES.bearer,
    healthCheckVersion: '',
    healthCheckRequiresApiVersion: false,
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

// True when the value is an absolute http or https URL Wilson can actually probe.
export function isAbsoluteHttpUrl(value) {
  const trimmed = String(value || '').trim();
  if (!trimmed) return false;
  try {
    const url = new URL(trimmed);
    return url.protocol === 'http:' || url.protocol === 'https:';
  } catch {
    return false;
  }
}

// Resolve the absolute health probe URL exactly like the server does: an absolute http(s) value
// wins, otherwise the path is appended to the endpoint. Returns '' when nothing absolute can be
// produced, which callers must treat as "do not probe". This is the value stored in the draft.
export function resolveHealthCheckUrl(apiType, endpoint, healthCheckUrl = '') {
  const descriptor = describeApiType(apiType);
  const configured = String(healthCheckUrl || '').trim();
  let candidate;

  if (isAbsoluteHttpUrl(configured)) {
    candidate = configured;
  } else {
    // A non-http(s) absolute URL is a configuration mistake, never concatenated onto the endpoint.
    if (configured) {
      try {
        new URL(configured);
        return '';
      } catch {
        // Not an absolute URL; treated as a path below.
      }
    }

    // Bedrock signs AWS SDK calls, Vertex AI derives a regional resource URL, and VoyageAI has no
    // models route, so Wilson never invents a probe URL for them.
    if (!descriptor.supportsHealthChecks) return '';
    const base = String(endpoint || descriptor.defaultEndpoint || '').trim().replace(/\/+$/, '');
    const path = configured || descriptor.defaultHealthPath || '';
    const normalizedPath = !path || path.startsWith('/') ? path : `/${path}`;
    candidate = `${base}${normalizedPath}`;
  }

  return isAbsoluteHttpUrl(candidate) ? candidate : '';
}

// Build the URL Wilson actually sends: the resolved health URL plus any query parameter the
// provider contract requires (Azure OpenAI's api-version).
export function buildHealthCheckRequestUrl(apiType, endpoint, healthCheckUrl = '', apiVersion = '') {
  const descriptor = describeApiType(apiType);
  let url = resolveHealthCheckUrl(apiType, endpoint, healthCheckUrl);
  if (!url) return '';
  if (descriptor.healthCheckRequiresApiVersion && !/api-version=/i.test(url)) {
    const version = String(apiVersion || descriptor.defaultApiVersion || '').trim();
    if (version) url += `${url.includes('?') ? '&' : '?'}api-version=${encodeURIComponent(version)}`;
  }
  return url;
}

// Build default health probe settings for an API type. Providers without a usable probe route, and
// drafts whose endpoint is not yet an absolute URL, come back with healthCheckEnabled: false so the
// server never polls an empty or relative URL.
export function defaultHealthSettings(apiType, endpoint, apiKey = '') {
  const descriptor = describeApiType(apiType);
  const healthCheckUrl = resolveHealthCheckUrl(apiType, endpoint, '');
  return {
    healthCheckEnabled: Boolean(healthCheckUrl),
    healthCheckUrl,
    healthCheckMethod: 'GET',
    healthCheckIntervalMs: descriptor.healthIntervalMs,
    healthCheckTimeoutMs: descriptor.healthTimeoutMs,
    healthCheckExpectedStatusCode: 200,
    healthyThreshold: 2,
    unhealthyThreshold: 2,
    healthCheckUseAuth: descriptor.healthCheckAuthScheme !== HEALTH_CHECK_AUTH_SCHEMES.none && Boolean(apiKey)
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

// True when a draft value still carries the previous provider's default, meaning it is safe to
// replace when the provider changes. A value the user typed never matches and is preserved.
function stillDefault(value, previousDefault, blankAliases = []) {
  const current = value === undefined || value === null ? '' : String(value).trim();
  const defaults = [String(previousDefault === undefined || previousDefault === null ? '' : previousDefault).trim(), ...blankAliases];
  return defaults.some(item => item === current);
}

// Re-derive provider defaults when the API type changes in a model server editor.
//
// Both dashboard editors (the model server create/edit modal and the inline settings editor) route
// their API type change through this function. Selecting a new provider from a fresh Ollama draft
// therefore yields the new provider's endpoint, health URL, tool format, and capability flags,
// while user-entered custom endpoints, custom health URLs/settings, and disabled controls survive.
export function applyApiTypeDefaults(runner, nextApiType) {
  if (!runner) return runner;
  const previousType = normalizeApiType(runner.apiType || 'Ollama');
  const resolvedType = normalizeApiType(nextApiType || previousType);
  if (resolvedType === previousType) return { ...runner, apiType: resolvedType };

  const previous = describeApiType(previousType);
  const next = describeApiType(resolvedType);
  const nextType = next.name;

  const draft = { ...runner, apiType: nextType };
  const previousHealth = defaultHealthSettings(previousType, runner.endpoint, runner.apiKey);
  const previousTools = defaultToolSettings(previousType);
  const nextTools = defaultToolSettings(nextType);

  // Endpoint: a blank endpoint or the previous provider's default is replaced. A custom endpoint,
  // including a self-hosted or proxied URL, is preserved.
  if (stillDefault(runner.endpoint, previous.defaultEndpoint)) {
    draft.endpoint = next.defaultEndpoint || '';
  }

  // Resource-derived fields keep their previous-provider default until the user edits them.
  if (stillDefault(runner.deployment, '')) draft.deployment = next.requiresDeployment ? runner.deployment || '' : null;
  if (stillDefault(runner.apiVersion, previous.defaultApiVersion)) {
    draft.apiVersion = next.defaultApiVersion || null;
  }
  if (stillDefault(runner.region, previous.defaultRegion)) {
    draft.region = next.defaultRegion || null;
  }
  if (stillDefault(runner.projectId, '')) {
    draft.projectId = next.requiresProjectId ? runner.projectId || null : null;
  }

  // Health defaults are derived from the endpoint and API version the draft now carries.
  const nextHealth = defaultHealthSettings(nextType, draft.endpoint, runner.apiKey);

  // Health settings: replace anything still equal to the previous provider's default. An explicit
  // "disabled" toggle is preserved, and providers with no probe route come back disabled.
  if (runner.healthCheckEnabled !== false) {
    draft.healthCheckEnabled = nextHealth.healthCheckEnabled;
  }
  if (!String(runner.healthCheckUrl || '').trim() || stillDefault(runner.healthCheckUrl, previousHealth.healthCheckUrl)) {
    draft.healthCheckUrl = nextHealth.healthCheckUrl;
  }
  if (stillDefault(runner.healthCheckMethod || 'GET', previousHealth.healthCheckMethod)) draft.healthCheckMethod = nextHealth.healthCheckMethod;
  if (stillDefault(runner.healthCheckIntervalMs, previousHealth.healthCheckIntervalMs)) draft.healthCheckIntervalMs = nextHealth.healthCheckIntervalMs;
  if (stillDefault(runner.healthCheckTimeoutMs, previousHealth.healthCheckTimeoutMs)) draft.healthCheckTimeoutMs = nextHealth.healthCheckTimeoutMs;
  if (stillDefault(runner.healthCheckExpectedStatusCode, previousHealth.healthCheckExpectedStatusCode)) draft.healthCheckExpectedStatusCode = nextHealth.healthCheckExpectedStatusCode;
  if (stillDefault(runner.healthyThreshold, previousHealth.healthyThreshold)) draft.healthyThreshold = nextHealth.healthyThreshold;
  if (stillDefault(runner.unhealthyThreshold, previousHealth.unhealthyThreshold)) draft.unhealthyThreshold = nextHealth.unhealthyThreshold;
  if (Boolean(runner.healthCheckUseAuth) === Boolean(previousHealth.healthCheckUseAuth)) {
    draft.healthCheckUseAuth = nextHealth.healthCheckUseAuth;
  }

  // Tool settings follow the same rule, so switching to an embeddings-only provider turns tools off
  // and switching back to a tool-capable provider turns them on again.
  if (runner.toolsEnabled !== false) draft.toolsEnabled = nextTools.toolsEnabled;
  if (stillDefault(runner.supportsTools, previousTools.supportsTools, previousTools.supportsTools ? [] : ['true'])) draft.supportsTools = nextTools.supportsTools;
  if (stillDefault(runner.toolCallingApiFormat, previousTools.toolCallingApiFormat)) draft.toolCallingApiFormat = nextTools.toolCallingApiFormat;
  if (stillDefault(runner.chatCompletionsPath, previousTools.chatCompletionsPath)) draft.chatCompletionsPath = nextTools.chatCompletionsPath;
  if (stillDefault(runner.supportsParallelToolCalls, previousTools.supportsParallelToolCalls)) draft.supportsParallelToolCalls = nextTools.supportsParallelToolCalls;
  if (stillDefault(runner.supportsStreamingToolCalls, previousTools.supportsStreamingToolCalls)) draft.supportsStreamingToolCalls = nextTools.supportsStreamingToolCalls;

  return draft;
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

  // A draft that explicitly enables health checks must be able to resolve an absolute http(s) URL,
  // so the error surfaces before save instead of the server queueing a probe it cannot send.
  if (runner.healthCheckEnabled === true && !resolveHealthCheckUrl(apiType, endpoint, runner.healthCheckUrl)) {
    errors.push(descriptor.supportsHealthChecks
      ? `${descriptor.displayName} health checks need an absolute http or https health URL; set the endpoint or the health URL, or disable health checks.`
      : `${descriptor.displayName} has no health route Wilson can probe; set an absolute http or https health URL, or disable health checks.`);
  }

  return errors;
}
