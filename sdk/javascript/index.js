export class WilsonClient {
  constructor(baseUrl, token = null) {
    this.baseUrl = String(baseUrl || '').replace(/\/+$/, '');
    this.token = token;
  }

  setToken(token) {
    this.token = token;
  }

  async request(method, path, body = null, query = null) {
    const url = new URL(this.baseUrl + path);
    if (query) {
      Object.entries(query).forEach(([key, value]) => {
        if (value !== undefined && value !== null && value !== '') url.searchParams.set(key, value);
      });
    }

    const headers = { Accept: 'application/json' };
    if (body !== null) headers['Content-Type'] = 'application/json';
    if (this.token) headers.Authorization = `Bearer ${this.token}`;

    const response = await fetch(url.toString(), {
      method,
      headers,
      body: body === null ? undefined : JSON.stringify(body)
    });

    if (!response.ok) {
      const text = await response.text();
      throw new Error(text || `Wilson API request failed with HTTP ${response.status}`);
    }

    if (response.status === 204) return null;
    return response.json();
  }

  async login(accessKey) {
    const result = await this.request('POST', '/v1.0/auth/token', { accessKey });
    this.setToken(result.token);
    return result;
  }

  me() {
    return this.request('GET', '/v1.0/api/me');
  }

  modelRunners(params = {}) {
    return this.request('GET', '/v1.0/api/model-runners', null, params);
  }

  modelRunnerHealth() {
    return this.request('GET', '/v1.0/api/model-runners/health');
  }

  modelRunnerHealthById(runnerId) {
    return this.request('GET', `/v1.0/api/model-runners/${encodeURIComponent(runnerId)}/health`);
  }

  tools() {
    return this.request('GET', '/v1.0/api/tools');
  }

  tool(name) {
    return this.request('GET', `/v1.0/api/tools/${encodeURIComponent(name)}`);
  }

  toolRun(runId, params = {}) {
    return this.request('GET', `/v1.0/api/tool-runs/${encodeURIComponent(runId)}`, null, params);
  }

  conversationToolCalls(conversationId, params = {}) {
    return this.request('GET', `/v1.0/api/conversations/${encodeURIComponent(conversationId)}/tool-calls`, null, params);
  }

  requestHistoryToolCalls(requestHistoryId, params = {}) {
    return this.request('GET', `/v1.0/api/request-history/${encodeURIComponent(requestHistoryId)}/tool-calls`, null, params);
  }
}

export default WilsonClient;
