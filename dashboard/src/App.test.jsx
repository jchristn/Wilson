import { afterEach, describe, expect, it, vi } from 'vitest';
import ApiClient from './utils/api.js';
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
