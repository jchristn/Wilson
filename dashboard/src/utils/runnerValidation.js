// Model server API type catalog and runner draft validation shared by the dashboard editors.
// Keep this list in sync with Wilson.Core.Settings.ApiTypes on the server.

export const apiTypeCatalog = [
  { name: 'Ollama', label: 'Ollama', endpoint: 'http://localhost:11434', healthPath: '/api/tags', ollama: true, requiresApiKey: false, supportsTools: true, supportsChat: true, supportsEmbeddings: true, cloudResource: false, endpointRequired: false, requiresDeployment: false, intervalMs: 5000, timeoutMs: 2000 },
  { name: 'OllamaCloud', label: 'Ollama Cloud', endpoint: 'https://ollama.com', healthPath: '/api/tags', ollama: true, requiresApiKey: true, supportsTools: true, supportsChat: true, supportsEmbeddings: true, cloudResource: false, endpointRequired: false, requiresDeployment: false, intervalMs: 5000, timeoutMs: 2000 },
  { name: 'OpenAI', label: 'OpenAI', endpoint: 'https://api.openai.com', healthPath: '/v1/models', ollama: false, requiresApiKey: true, supportsTools: true, supportsChat: true, supportsEmbeddings: true, cloudResource: false, endpointRequired: false, requiresDeployment: false, intervalMs: 15000, timeoutMs: 5000 },
  { name: 'OpenAICompatible', label: 'OpenAI compatible', endpoint: '', healthPath: '/v1/models', ollama: false, requiresApiKey: false, supportsTools: true, supportsChat: true, supportsEmbeddings: true, cloudResource: false, endpointRequired: true, requiresDeployment: false, intervalMs: 15000, timeoutMs: 5000 },
  { name: 'AzureOpenAI', label: 'Azure OpenAI', endpoint: '', healthPath: '/openai/models?api-version=2024-10-21', ollama: false, requiresApiKey: true, supportsTools: true, supportsChat: true, supportsEmbeddings: true, cloudResource: true, endpointRequired: true, requiresDeployment: true, intervalMs: 15000, timeoutMs: 5000 },
  { name: 'Anthropic', label: 'Anthropic', endpoint: 'https://api.anthropic.com', healthPath: '/v1/models', ollama: false, requiresApiKey: true, supportsTools: true, supportsChat: true, supportsEmbeddings: false, cloudResource: false, endpointRequired: false, requiresDeployment: false, intervalMs: 15000, timeoutMs: 5000 },
  { name: 'Gemini', label: 'Google Gemini', endpoint: 'https://generativelanguage.googleapis.com', healthPath: '/v1beta/models', ollama: false, requiresApiKey: true, supportsTools: true, supportsChat: true, supportsEmbeddings: true, cloudResource: false, endpointRequired: false, requiresDeployment: false, intervalMs: 15000, timeoutMs: 5000 },
  { name: 'VertexAI', label: 'Google Vertex AI', endpoint: '', healthPath: '', ollama: false, requiresApiKey: true, supportsTools: true, supportsParallelToolCalls: false, supportsChat: true, supportsEmbeddings: true, cloudResource: true, endpointRequired: false, requiresDeployment: false, defaultRegion: 'us-central1', regionHint: 'Google Cloud location such as us-central1.', intervalMs: 15000, timeoutMs: 5000 },
  { name: 'Bedrock', label: 'AWS Bedrock', endpoint: '', healthPath: '', ollama: false, requiresApiKey: true, supportsTools: true, supportsParallelToolCalls: false, supportsChat: true, supportsEmbeddings: true, cloudResource: true, endpointRequired: false, requiresDeployment: false, defaultRegion: 'us-east-1', regionHint: 'AWS region such as us-east-1.', intervalMs: 15000, timeoutMs: 5000 },
  { name: 'Cohere', label: 'Cohere', endpoint: 'https://api.cohere.com', healthPath: '/v1/models', ollama: false, requiresApiKey: true, supportsTools: true, supportsChat: true, supportsEmbeddings: true, cloudResource: false, endpointRequired: false, requiresDeployment: false, intervalMs: 15000, timeoutMs: 5000 },
  { name: 'XAI', label: 'xAI', endpoint: 'https://api.x.ai', healthPath: '/v1/models', ollama: false, requiresApiKey: true, supportsTools: true, supportsChat: true, supportsEmbeddings: true, cloudResource: false, endpointRequired: false, requiresDeployment: false, intervalMs: 15000, timeoutMs: 5000 },
  { name: 'TEI', label: 'Text Embeddings Inference', endpoint: 'http://localhost:8080', healthPath: '/info', ollama: false, requiresApiKey: false, supportsTools: false, supportsChat: false, supportsEmbeddings: true, cloudResource: false, endpointRequired: false, requiresDeployment: false, intervalMs: 15000, timeoutMs: 5000 },
  { name: 'VoyageAI', label: 'VoyageAI', endpoint: 'https://api.voyageai.com', healthPath: '', ollama: false, requiresApiKey: true, supportsTools: false, supportsChat: false, supportsEmbeddings: true, cloudResource: false, endpointRequired: false, requiresDeployment: false, intervalMs: 15000, timeoutMs: 5000 }
];

export const apiTypeNames = apiTypeCatalog.map(entry => entry.name);

export function apiTypeInfo(apiType) {
  const normalized = String(apiType || '').toLowerCase();
  return apiTypeCatalog.find(entry => String(entry.name).toLowerCase() === normalized) || apiTypeCatalog[0];
}

/**
 * Describe the region a runner will use when its `region` field is blank.
 * Region identifiers are provider-specific: Google locations look like `us-central1`, AWS
 * regions look like `us-east-1`, and AWS values are never valid Google locations.
 * Returns an empty string for providers that are not region-scoped.
 */
export function regionTooltip(apiType) {
  const info = apiTypeInfo(apiType);
  if (!info.defaultRegion) return 'Region is not used by this provider.';
  return `Cloud region for ${info.label}. Leave blank to use ${info.defaultRegion}.`
    + (info.regionHint ? ` ${info.regionHint}` : '');
}

/**
 * Validate a model server draft against the provider's configuration requirements.
 * Returns a user-facing error message, or an empty string when the draft can be saved.
 *
 * Providers that resolve their endpoint from region, project, or deployment (Vertex AI,
 * AWS Bedrock) and providers with a catalog default endpoint accept a blank endpoint.
 * Providers that cannot derive one (OpenAI compatible, Azure OpenAI) still require it.
 */
export function runnerValidationError(draft) {
  const runner = draft || {};
  const info = apiTypeInfo(runner.apiType);
  const label = info.label || info.name;

  if (!String(runner.id || '').trim() || !String(runner.name || '').trim()) {
    return 'ID and name are required.';
  }

  const endpoint = String(runner.endpoint || '').trim();
  if (!endpoint && info.endpointRequired) {
    return `Endpoint is required for ${label} model servers.`;
  }

  if (info.requiresDeployment) {
    const deployment = String(runner.deployment || '').trim();
    const models = Array.isArray(runner.models) ? runner.models.filter(item => String(item || '').trim()) : [];
    if (!deployment && models.length < 1) {
      return `${label} model servers need a deployment name or at least one configured model.`;
    }
  }

  // Negative: reject an AWS-style region on a Google location, which would build an invalid
  // https://{region}-aiplatform.googleapis.com endpoint. Google locations look like us-central1,
  // us-east1, or the multi-region us/eu, none of which match the AWS area-direction-number form.
  const region = String(runner.region || '').trim();
  if (region && info.defaultRegion === 'us-central1' && /^[a-z]+-[a-z]+-[0-9]+$/.test(region)) {
    return `Region for ${label} must be a Google Cloud location such as us-central1, not an AWS region.`;
  }

  return '';
}

export function defaultHealthCheckSettings(apiType = 'Ollama', endpoint = '', apiKey = '') {
  const info = apiTypeInfo(apiType);
  const base = (String(endpoint || '').trim() || String(info.endpoint || '').trim()).replace(/\/+$/, '');
  return {
    healthCheckEnabled: Boolean(base && info.healthPath),
    healthCheckUrl: base && info.healthPath ? `${base}${info.healthPath}` : '',
    healthCheckMethod: 'GET',
    healthCheckIntervalMs: info.intervalMs,
    healthCheckTimeoutMs: info.timeoutMs,
    healthCheckExpectedStatusCode: 200,
    healthyThreshold: 2,
    unhealthyThreshold: 2,
    healthCheckUseAuth: (!info.ollama || info.requiresApiKey) && Boolean(apiKey)
  };
}

export function defaultRunnerToolSettings(apiType = 'Ollama') {
  const info = apiTypeInfo(apiType);
  return {
    toolsEnabled: true,
    supportsTools: info.supportsTools,
    toolCallingApiFormat: !info.supportsTools ? '' : (info.ollama ? 'OllamaChat' : (info.name === 'OpenAI' || info.name === 'OpenAICompatible' ? 'OpenAIChatCompletions' : 'PolyPromptNative')),
    supportsParallelToolCalls: info.supportsTools && info.supportsParallelToolCalls !== false,
    supportsStreamingToolCalls: info.supportsTools && info.supportsStreamingToolCalls !== false,
    chatCompletionsPath: (info.name === 'OpenAI' || info.name === 'OpenAICompatible') ? '/v1/chat/completions' : ''
  };
}


// Replace protocol defaults while retaining custom URLs and intentional overrides.
export function applyRunnerApiTypeChange(runner, nextType) {
  const oldInfo = apiTypeInfo(runner.apiType);
  const nextInfo = apiTypeInfo(nextType);
  if (String(runner.apiType || 'Ollama').toLowerCase() === String(nextType).toLowerCase()) return { ...runner, apiType: nextType };
  const endpoint = String(runner.endpoint || '').trim();
  const isDefault = !endpoint || endpoint.replace(/\/+$/, '') === String(oldInfo.endpoint || '').replace(/\/+$/, '');
  const nextEndpoint = isDefault ? nextInfo.endpoint || '' : endpoint;
  const previousHealth = defaultHealthCheckSettings(runner.apiType, endpoint, runner.apiKey);
  const health = defaultHealthCheckSettings(nextType, nextEndpoint, runner.apiKey);
  for (const key of Object.keys(health)) {
    const current = runner[key];
    if (current != null && current !== '' && current !== previousHealth[key]) health[key] = current;
  }
  return { ...runner, ...defaultRunnerToolSettings(nextType), ...health,
    apiType: nextType, endpoint: nextEndpoint, toolsEnabled: runner.toolsEnabled !== false };
}
