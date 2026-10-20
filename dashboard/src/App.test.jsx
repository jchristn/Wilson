import { afterEach, describe, expect, it, vi } from 'vitest';
import ApiClient from './utils/api.js';
import {
  API_TYPE_NAMES,
  applyApiTypeDefaults,
  buildHealthCheckRequestUrl,
  describeApiType,
  defaultHealthSettings,
  defaultToolSettings,
  isSupportedApiType,
  normalizeApiType,
  resolveHealthCheckUrl,
  validateRunnerDraft
} from './utils/apiTypes.js';
import { mergeToolProgress, parseSseFrame } from './utils/sseTools.js';

afterEach(() => {
  vi.restoreAllMocks();
});

describe('parseSseFrame', () => {
  it('parses event and multiline data', () => {
    const frame = 'event: tool_call_pending_approval\ndata: {"one":1}\ndata: {"two":2}';

    const parsed = parseSseFrame(frame);

    expect(parsed.event).toBe('tool_call_pending_approval');
    expect(parsed.data).toBe('{"one":1}\n{"two":2}');
  });

  it('ignores comments and preserves empty event/data defaults', () => {
    const parsed = parseSseFrame(': keepalive\nretry: 1000');

    expect(parsed).toEqual({ event: '', data: '' });
  });

  it('trims data prefixes without trimming meaningful JSON whitespace', () => {
    const parsed = parseSseFrame('event: message\ndata:   {"text":" spaced "}');

    expect(parsed.event).toBe('message');
    expect(parsed.data).toBe('{"text":" spaced "}');
  });
});

describe('apiTypes catalog', () => {
  it('covers every provider Wilson supports', () => {
    expect(API_TYPE_NAMES).toEqual([
      'Ollama',
      'OllamaCloud',
      'OpenAI',
      'OpenAICompatible',
      'AzureOpenAI',
      'XAI',
      'Anthropic',
      'Gemini',
      'VertexAI',
      'Bedrock',
      'Cohere',
      'VoyageAI',
      'Tei'
    ]);
  });

  it('resolves API types case-insensitively and canonicalizes them', () => {
    expect(describeApiType('xai').name).toBe('XAI');
    expect(describeApiType('  ollamacloud ').name).toBe('OllamaCloud');
    expect(normalizeApiType('azureopenai')).toBe('AzureOpenAI');
    expect(normalizeApiType('gpt-like-custom')).toBe('gpt-like-custom');
  });

  it('falls back to Ollama for unrecognized API types', () => {
    expect(describeApiType('not-a-provider').name).toBe('Ollama');
    expect(isSupportedApiType('not-a-provider')).toBe(false);
    expect(isSupportedApiType('cohere')).toBe(true);
  });

  it('derives provider-specific health and tool defaults', () => {
    expect(defaultHealthSettings('OllamaCloud', 'https://ollama.com', 'token')).toMatchObject({
      healthCheckUrl: 'https://ollama.com/api/tags',
      healthCheckIntervalMs: 15000,
      healthCheckTimeoutMs: 5000,
      healthCheckUseAuth: true
    });
    expect(defaultHealthSettings('OpenAI', 'https://api.openai.com', '')).toMatchObject({
      healthCheckUrl: 'https://api.openai.com/v1/models',
      healthCheckUseAuth: false
    });
    expect(defaultToolSettings('XAI')).toMatchObject({
      supportsTools: true,
      toolCallingApiFormat: 'PolyPromptNative'
    });
    expect(defaultToolSettings('VoyageAI')).toMatchObject({
      supportsTools: false,
      toolCallingApiFormat: ''
    });
  });

  it('accepts a fully configured hosted runner', () => {
    const errors = validateRunnerDraft({
      id: 'grok',
      apiType: 'XAI',
      endpoint: 'https://api.x.ai',
      apiKey: 'key',
      supportsTools: true,
      toolCallingApiFormat: 'PolyPromptNative'
    }, ['grok']);
    expect(errors).toEqual([]);
  });

  it('rejects hosted runners missing credentials', () => {
    const errors = validateRunnerDraft({ id: 'grok', apiType: 'XAI', endpoint: 'https://api.x.ai' });
    expect(errors).toContain('xAI Grok requires an API key.');
  });

  it('rejects Azure OpenAI runners missing deployment and API version', () => {
    const errors = validateRunnerDraft({ id: 'azure', apiType: 'AzureOpenAI', endpoint: 'https://example.openai.azure.com', apiKey: 'key' });
    expect(errors).toContain('Azure OpenAI requires a deployment name.');
    expect(errors).toContain('Azure OpenAI requires an API version.');
  });

  it('rejects Vertex AI and Bedrock runners missing region and project', () => {
    const vertex = validateRunnerDraft({ id: 'vertex', apiType: 'VertexAI', apiKey: 'token' });
    expect(vertex).toContain('Google Vertex AI requires a project identifier.');
    expect(vertex).toContain('Google Vertex AI requires a region.');

    const bedrock = validateRunnerDraft({ id: 'bedrock', apiType: 'Bedrock' });
    expect(bedrock).toContain('AWS Bedrock requires a region.');
    expect(bedrock).not.toContain('AWS Bedrock requires an API key.');
  });

  it('accepts cloud runners whose endpoint is derived instead of supplied', () => {
    // Bedrock reaches AWS through the SDK and Vertex AI builds its regional URL from the project
    // and region, so a blank endpoint is valid for both as long as everything else is present.
    expect(validateRunnerDraft({
      id: 'bedrock',
      apiType: 'Bedrock',
      endpoint: '',
      region: 'us-east-1',
      apiKey: 'access-key',
      apiSecretKey: 'secret-key',
      supportsTools: true,
      toolCallingApiFormat: 'PolyPromptNative'
    })).toEqual([]);

    expect(validateRunnerDraft({
      id: 'vertex',
      apiType: 'VertexAI',
      endpoint: '',
      apiKey: 'bearer-token',
      projectId: 'my-project',
      region: 'us-central1'
    })).toEqual([]);
  });

  it('routes Azure OpenAI tool calls through the provider-native format', () => {
    expect(defaultToolSettings('AzureOpenAI')).toMatchObject({
      supportsTools: true,
      toolCallingApiFormat: 'PolyPromptNative'
    });
    expect(validateRunnerDraft({
      id: 'azure',
      apiType: 'AzureOpenAI',
      endpoint: 'https://example.openai.azure.com',
      apiKey: 'key',
      deployment: 'chat',
      apiVersion: '2024-10-21',
      supportsTools: true,
      toolCallingApiFormat: 'PolyPromptNative'
    })).toEqual([]);
  });

  it('rejects unsupported API types and malformed endpoints', () => {
    expect(validateRunnerDraft({ id: 'x', apiType: 'MadeUp' })[0]).toContain('is not supported');
    expect(validateRunnerDraft({ id: 'x', apiType: 'Ollama', endpoint: 'localhost:11434' }))
      .toContain('Endpoint must be an absolute http or https URL.');
  });

  it('rejects tool configuration on providers without tool support', () => {
    const errors = validateRunnerDraft({
      id: 'tei',
      apiType: 'Tei',
      endpoint: 'http://localhost:8080',
      supportsTools: true
    });
    expect(errors).toContain('Text Embeddings Inference does not support tool calls.');
  });

  it('requires a server id', () => {
    expect(validateRunnerDraft({ id: '  ', apiType: 'Ollama', endpoint: 'http://localhost:11434' }))
      .toContain('Server ID is required.');
  });

  it('derives a usable health default for every advertised provider', () => {
    // Every provider must either resolve an absolute http(s) probe URL or come back disabled. A
    // relative or empty default would make the server poll a URL it cannot send.
    API_TYPE_NAMES.forEach(name => {
      const settings = defaultHealthSettings(name, '');
      const descriptor = describeApiType(name);
      if (descriptor.supportsHealthChecks) {
        expect(settings.healthCheckUrl.startsWith('http'), `${name} health URL`).toBe(true);
        expect(settings.healthCheckEnabled, `${name} enabled`).toBe(true);
      } else {
        expect(settings.healthCheckUrl, `${name} health URL`).toBe('');
        expect(settings.healthCheckEnabled, `${name} enabled`).toBe(false);
      }
      expect(settings.healthCheckIntervalMs).toBeGreaterThan(0);
      expect(settings.healthCheckTimeoutMs).toBeGreaterThan(0);
    });
  });

  it('keeps derived endpoints and the Azure api-version requirement in the probe request URL', () => {
    // Azure only answers its models route with api-version present, so the request URL carries it
    // while the stored health URL stays provider-neutral.
    expect(buildHealthCheckRequestUrl('AzureOpenAI', 'https://example.openai.azure.com', '', '2024-10-21'))
      .toBe('https://example.openai.azure.com/openai/models?api-version=2024-10-21');
    expect(resolveHealthCheckUrl('AzureOpenAI', 'https://example.openai.azure.com', ''))
      .toBe('https://example.openai.azure.com/openai/models');
    // An explicitly configured absolute URL always wins, including for providers with no default.
    expect(resolveHealthCheckUrl('Bedrock', '', 'https://health.example.com/probe', ''))
      .toBe('https://health.example.com/probe');
    // A provider with no probe route and no absolute URL resolves to nothing rather than a relative URL.
    expect(resolveHealthCheckUrl('VertexAI', '', '', '')).toBe('');
    expect(resolveHealthCheckUrl('VoyageAI', 'https://api.voyageai.com', '', '')).toBe('');
    // A relative endpoint is never turned into a probe.
    expect(resolveHealthCheckUrl('Ollama', 'localhost:11434', '', '')).toBe('');
    // A non-http(s) absolute URL is rejected instead of being concatenated onto the endpoint.
    expect(resolveHealthCheckUrl('Ollama', 'http://localhost:11434', 'ftp://example.com/tags')).toBe('');
  });

  it('reports an actionable health error before save when no probe URL can be resolved', () => {
    expect(validateRunnerDraft({ id: 'vertex', apiType: 'VertexAI', endpoint: '', healthCheckEnabled: true, apiKey: 'token', projectId: 'p', region: 'us-central1' }))
      .toContain('Google Vertex AI has no health route Wilson can probe; set an absolute http or https health URL, or disable health checks.');
    expect(validateRunnerDraft({ id: 'rel', apiType: 'Ollama', endpoint: 'localhost:11434', healthCheckEnabled: true }))
      .toContain('Ollama health checks need an absolute http or https health URL; set the endpoint or the health URL, or disable health checks.');
    // Disabled checks need no URL.
    expect(validateRunnerDraft({ id: 'vertex', apiType: 'VertexAI', endpoint: '', healthCheckEnabled: false, apiKey: 'token', projectId: 'p', region: 'us-central1' }))
      .toEqual([]);
  });
});

describe('applyApiTypeDefaults', () => {
  function freshOllamaDraft() {
    return {
      id: 'server-1',
      name: 'Server 1',
      apiType: 'Ollama',
      endpoint: 'http://localhost:11434',
      apiKey: 'ollama-token',
      models: ['llama3'],
      ...defaultToolSettings('Ollama'),
      ...defaultHealthSettings('Ollama', 'http://localhost:11434', 'ollama-token')
    };
  }

  it('gives a fresh Ollama draft valid defaults for every other provider', () => {
    API_TYPE_NAMES.filter(name => name !== 'Ollama').forEach(name => {
      const next = applyApiTypeDefaults(freshOllamaDraft(), name);
      const descriptor = describeApiType(name);
      const health = defaultHealthSettings(name, next.endpoint, next.apiKey);

      expect(next.apiType, `${name} apiType`).toBe(name);
      expect(next.endpoint, `${name} endpoint`).toBe(descriptor.defaultEndpoint);
      expect(next.toolCallingApiFormat, `${name} tool format`).toBe(descriptor.toolCallingApiFormat);
      expect(next.supportsTools, `${name} tools`).toBe(descriptor.supportsToolCalls);
      expect(next.healthCheckUrl, `${name} health URL`).toBe(health.healthCheckUrl);
      expect(next.healthCheckEnabled, `${name} health enabled`).toBe(health.healthCheckEnabled);
      expect(next.healthCheckIntervalMs, `${name} interval`).toBe(descriptor.healthIntervalMs);
      expect(next.healthCheckTimeoutMs, `${name} timeout`).toBe(descriptor.healthTimeoutMs);
      if (descriptor.requiresDeployment) expect(next.deployment, `${name} deployment`).toBe('');
      if (descriptor.requiresApiVersion) expect(next.apiVersion, `${name} apiVersion`).toBe(descriptor.defaultApiVersion);
      if (descriptor.requiresRegion) expect(next.region, `${name} region`).toBe(descriptor.defaultRegion);
      if (!descriptor.requiresProjectId) expect(next.projectId, `${name} projectId`).toBeNull();
    });
  });

  it('handles embeddings-only and resource-derived providers', () => {
    expect(applyApiTypeDefaults(freshOllamaDraft(), 'VoyageAI')).toMatchObject({
      apiType: 'VoyageAI',
      endpoint: 'https://api.voyageai.com',
      supportsTools: false,
      toolCallingApiFormat: '',
      healthCheckEnabled: false,
      healthCheckUrl: ''
    });

    expect(applyApiTypeDefaults(freshOllamaDraft(), 'Bedrock')).toMatchObject({
      apiType: 'Bedrock',
      endpoint: '',
      region: 'us-east-1',
      healthCheckEnabled: false,
      healthCheckUrl: ''
    });

    expect(applyApiTypeDefaults(freshOllamaDraft(), 'VertexAI')).toMatchObject({
      apiType: 'VertexAI',
      endpoint: '',
      projectId: null,
      region: 'us-central1',
      healthCheckEnabled: false,
      healthCheckUrl: ''
    });

    expect(applyApiTypeDefaults(freshOllamaDraft(), 'AzureOpenAI')).toMatchObject({
      apiType: 'AzureOpenAI',
      endpoint: 'https://example.openai.azure.com',
      apiVersion: '2024-10-21',
      healthCheckUrl: 'https://example.openai.azure.com/openai/models'
    });
  });

  it('preserves custom endpoints, custom health URLs, and disabled controls', () => {
    const custom = {
      ...freshOllamaDraft(),
      endpoint: 'http://ollama.internal:11434',
      healthCheckUrl: 'http://health.internal:9090/status',
      healthCheckIntervalMs: 1234,
      healthCheckTimeoutMs: 4321,
      healthCheckMethod: 'HEAD',
      healthCheckExpectedStatusCode: 204,
      healthCheckEnabled: false,
      toolsEnabled: false,
      supportsTools: true,
      supportsParallelToolCalls: false,
      supportsStreamingToolCalls: false
    };

    const next = applyApiTypeDefaults(custom, 'OpenAI');
    expect(next.apiType).toBe('OpenAI');
    expect(next.endpoint).toBe('http://ollama.internal:11434');
    expect(next.healthCheckUrl).toBe('http://health.internal:9090/status');
    expect(next.healthCheckIntervalMs).toBe(1234);
    expect(next.healthCheckTimeoutMs).toBe(4321);
    expect(next.healthCheckMethod).toBe('HEAD');
    expect(next.healthCheckExpectedStatusCode).toBe(204);
    expect(next.healthCheckEnabled).toBe(false);
    expect(next.toolsEnabled).toBe(false);
    expect(next.supportsParallelToolCalls).toBe(false);
    expect(next.supportsStreamingToolCalls).toBe(false);
  });

  it('round-trips back to the original provider defaults', () => {
    const openAi = applyApiTypeDefaults(freshOllamaDraft(), 'OpenAI');
    expect(openAi.endpoint).toBe('https://api.openai.com');
    expect(openAi.healthCheckUrl).toBe('https://api.openai.com/v1/models');

    const backToOllama = applyApiTypeDefaults(openAi, 'Ollama');
    expect(backToOllama.endpoint).toBe('http://localhost:11434');
    expect(backToOllama.healthCheckUrl).toBe('http://localhost:11434/api/tags');
    expect(backToOllama.toolCallingApiFormat).toBe('OllamaChat');
    expect(backToOllama.healthCheckIntervalMs).toBe(5000);
    expect(backToOllama.healthCheckTimeoutMs).toBe(2000);
  });

  it('leaves the draft untouched when the API type does not change', () => {
    const draft = freshOllamaDraft();
    const next = applyApiTypeDefaults(draft, 'ollama');
    expect(next.apiType).toBe('Ollama');
    expect(next).toEqual({ ...draft, apiType: 'Ollama' });
  });
});

describe('mergeToolProgress', () => {
  it('ignores progress events that cannot be associated with a tool call', () => {
    const existing = [{ toolCallId: 'call-1', toolName: 'read_file' }];

    expect(mergeToolProgress(existing, null)).toBe(existing);
    expect(mergeToolProgress(existing, { summary: 'missing id' })).toBe(existing);
  });

  it('adds pending approval metadata without raw payloads', () => {
    const calls = mergeToolProgress([], {
      runId: 'run-1',
      toolCallId: 'call-1',
      toolName: 'write_file',
      displayLabel: 'Write File',
      iteration: 1,
      sequenceNumber: 1,
      statusCode: 'pending_approval',
      approvalEndpoint: '/v1.0/api/tool-runs/run-1/tool-calls/call-1/approval',
      approvalExpiresUtc: '2026-06-26T20:00:00Z',
      summary: 'Waiting for approval.',
      argumentsJson: '{"secret":"nope"}',
      resultJson: '{"secret":"nope"}'
    });

    expect(calls).toHaveLength(1);
    expect(calls[0]).toMatchObject({
      runId: 'run-1',
      toolCallId: 'call-1',
      toolName: 'write_file',
      statusCode: 'pending_approval',
      approvalEndpoint: '/v1.0/api/tool-runs/run-1/tool-calls/call-1/approval'
    });
    expect(calls[0].argumentsJson).toBeUndefined();
    expect(calls[0].resultJson).toBeUndefined();
  });

  it('updates an existing tool call row in place', () => {
    const original = [
      { toolCallId: 'call-1', toolName: 'read_file', statusCode: 'pending_approval', summary: 'Waiting.' }
    ];
    const calls = mergeToolProgress(original, {
      toolCallId: 'call-1',
      toolName: 'read_file',
      statusCode: 'completed',
      success: true,
      summary: 'Completed.'
    });

    expect(calls).toHaveLength(1);
    expect(calls[0].statusCode).toBe('completed');
    expect(calls[0].success).toBe(true);
    expect(calls[0].summary).toBe('Completed.');
    expect(original[0].statusCode).toBe('pending_approval');
  });

  it('normalizes optional values for rendering-safe tool progress rows', () => {
    const calls = mergeToolProgress([], {
      toolCallId: 'call-2',
      statusCode: 'running'
    });

    expect(calls[0]).toMatchObject({
      toolCallId: 'call-2',
      runId: '',
      toolName: '',
      displayLabel: 'Tool',
      iteration: 0,
      sequenceNumber: 0,
      outputCharacters: 0,
      elapsedMs: 0,
      summary: '',
      startedUtc: '',
      completedUtc: '',
      approvalEndpoint: '',
      approvalExpiresUtc: ''
    });
  });
});

describe('ApiClient', () => {
  it('builds JSON requests with authorization and filtered query values', async () => {
    const fetchMock = vi.spyOn(globalThis, 'fetch').mockResolvedValue(new Response(JSON.stringify({ ok: true }), {
      status: 200,
      headers: { 'Content-Type': 'application/json' }
    }));
    const api = new ApiClient('http://localhost:9400/', 'token-123');

    const result = await api.request('POST', '/v1.0/api/test', { one: 1 }, {
      keepFalse: false,
      keepZero: 0,
      keepText: 'yes',
      skipEmpty: '',
      skipNull: null,
      skipUndefined: undefined
    });

    expect(result).toEqual({ ok: true });
    expect(fetchMock).toHaveBeenCalledTimes(1);
    const [url, options] = fetchMock.mock.calls[0];
    expect(url).toBe('http://localhost:9400/v1.0/api/test?keepFalse=false&keepZero=0&keepText=yes');
    expect(options).toMatchObject({
      method: 'POST',
      body: JSON.stringify({ one: 1 })
    });
    expect(options.headers.Authorization).toBe('Bearer token-123');
    expect(options.headers['Content-Type']).toBe('application/json');
  });

  it('returns null for empty 204 responses', async () => {
    vi.spyOn(globalThis, 'fetch').mockResolvedValue(new Response(null, { status: 204 }));
    const api = new ApiClient('http://localhost:9400', '');

    await expect(api.request('DELETE', '/v1.0/api/test')).resolves.toBeNull();
  });

  it('throws the response body for unsuccessful JSON requests', async () => {
    vi.spyOn(globalThis, 'fetch').mockResolvedValue(new Response('bad request', { status: 400 }));
    const api = new ApiClient('http://localhost:9400', '');

    await expect(api.request('GET', '/v1.0/api/test')).rejects.toThrow('bad request');
  });

  it('encodes path parameters for tool approval endpoints', async () => {
    const fetchMock = vi.spyOn(globalThis, 'fetch').mockResolvedValue(new Response(JSON.stringify({ approved: true }), {
      status: 200,
      headers: { 'Content-Type': 'application/json' }
    }));
    const api = new ApiClient('http://localhost:9400', 'token');

    await api.approveToolCall('run/one', 'call two', true, 'ok', { includeTrace: true }, true);

    const [url, options] = fetchMock.mock.calls[0];
    expect(url).toBe('http://localhost:9400/v1.0/api/tool-runs/run%2Fone/tool-calls/call%20two/approval?includeTrace=true');
    expect(JSON.parse(options.body)).toEqual({
      approved: true,
      reason: 'ok',
      alwaysForRun: true
    });
  });

  it('builds prompt template CRUD requests', async () => {
    const fetchMock = vi.spyOn(globalThis, 'fetch').mockImplementation(() => Promise.resolve(new Response(JSON.stringify({ id: 'prm_123' }), {
      status: 200,
      headers: { 'Content-Type': 'application/json' }
    })));
    const api = new ApiClient('http://localhost:9400', 'token');

    await api.prompts({ tenantId: 'tenant/one', kind: 'Tool', includeInactive: true, pageNumber: 2 });
    await api.prompt('prm/one', { tenantId: 'tenant/one' });
    await api.createPrompt({ kind: 'System', name: 'A', content: 'Prompt' });
    await api.updatePrompt('prm two', { kind: 'Tool', name: 'B', content: '{{tool_catalog}}' });
    await api.setDefaultPrompt('prm three');
    fetchMock.mockResolvedValueOnce(new Response(null, { status: 204 }));
    await api.deletePrompt('prm four');

    expect(fetchMock.mock.calls[0][0]).toBe('http://localhost:9400/v1.0/api/prompts?tenantId=tenant%2Fone&kind=Tool&includeInactive=true&pageNumber=2');
    expect(fetchMock.mock.calls[1][0]).toBe('http://localhost:9400/v1.0/api/prompts/prm%2Fone?tenantId=tenant%2Fone');
    expect(fetchMock.mock.calls[2][1]).toMatchObject({ method: 'POST', body: JSON.stringify({ kind: 'System', name: 'A', content: 'Prompt' }) });
    expect(fetchMock.mock.calls[3][0]).toBe('http://localhost:9400/v1.0/api/prompts/prm%20two');
    expect(fetchMock.mock.calls[3][1]).toMatchObject({ method: 'PUT', body: JSON.stringify({ kind: 'Tool', name: 'B', content: '{{tool_catalog}}' }) });
    expect(fetchMock.mock.calls[4][0]).toBe('http://localhost:9400/v1.0/api/prompts/prm%20three/default');
    expect(fetchMock.mock.calls[5][0]).toBe('http://localhost:9400/v1.0/api/prompts/prm%20four');
    expect(fetchMock.mock.calls[5][1].method).toBe('DELETE');
  });

  it('sends selected prompt IDs and visible prompt text in chat requests', async () => {
    const fetchMock = vi.spyOn(globalThis, 'fetch').mockResolvedValue(new Response(JSON.stringify({ answer: 'ok' }), {
      status: 200,
      headers: { 'Content-Type': 'application/json' }
    }));
    const api = new ApiClient('http://localhost:9400', 'token');

    await api.chat({
      runnerId: 'local-ollama',
      model: 'gpt-oss:20b',
      prompt: 'What tools do you have?',
      systemPromptId: 'prm_system',
      toolPromptId: 'prm_tool',
      settings: {
        systemPrompt: 'Visible system prompt',
        toolSystemPrompt: 'Visible tool prompt with catalog'
      },
      toolsEnabled: true
    });

    const [url, options] = fetchMock.mock.calls[0];
    expect(url).toBe('http://localhost:9400/v1.0/api/chat');
    const body = JSON.parse(options.body);
    expect(body.systemPromptId).toBe('prm_system');
    expect(body.toolPromptId).toBe('prm_tool');
    expect(body.settings.systemPrompt).toBe('Visible system prompt');
    expect(body.settings.toolSystemPrompt).toBe('Visible tool prompt with catalog');
  });

  it('sends raw bodies without JSON stringifying them', async () => {
    const fetchMock = vi.spyOn(globalThis, 'fetch').mockResolvedValue(new Response('ok', { status: 200 }));
    const api = new ApiClient('http://localhost:9400', 'token');

    const response = await api.raw('POST', '/stream', 'event: ping', { empty: '', keep: 'yes' });

    expect(response.status).toBe(200);
    const [url, options] = fetchMock.mock.calls[0];
    expect(url).toBe('http://localhost:9400/stream?keep=yes');
    expect(options.body).toBe('event: ping');
  });
});
