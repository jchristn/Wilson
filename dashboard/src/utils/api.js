class ApiClient {
  constructor(baseUrl, token) {
    this.baseUrl = (baseUrl || '').replace(/\/$/, '');
    this.token = token;
  }

  headers(extra = {}) {
    const headers = { ...extra };
    if (!(extra instanceof FormData)) headers['Content-Type'] = 'application/json';
    if (this.token) headers.Authorization = `Bearer ${this.token}`;
    return headers;
  }

  async request(method, path, body = null, query = null, options = {}) {
    const url = new URL(this.baseUrl + path);
    if (query) {
      Object.entries(query).forEach(([key, value]) => {
        if (value !== undefined && value !== null && value !== '') url.searchParams.set(key, value);
      });
    }
    const response = await fetch(url.toString(), {
      method,
      headers: this.headers(),
      body: body === null ? undefined : JSON.stringify(body),
      signal: options.signal
    });
    if (!response.ok) throw new Error(await response.text());
    if (response.status === 204) return null;
    return response.json();
  }

  async raw(method, path, body = null, query = null, options = {}) {
    const url = new URL(this.baseUrl + path);
    if (query) Object.entries(query).forEach(([key, value]) => value && url.searchParams.set(key, value));
    return fetch(url.toString(), {
      method,
      headers: this.headers(),
      body: body === null || body === '' ? undefined : body,
      signal: options.signal
    });
  }

  login(accessKey) { return this.request('POST', '/v1.0/auth/token', { accessKey }); }
  me() { return this.request('GET', '/v1.0/api/me'); }
  runners(params = {}) { return this.request('GET', '/v1.0/api/model-runners', null, params); }
  runnerHealth() { return this.request('GET', '/v1.0/api/model-runners/health'); }
  runnerHealthById(runnerId) { return this.request('GET', `/v1.0/api/model-runners/${encodeURIComponent(runnerId)}/health`); }
  pullModel(runnerId, model) { return this.request('POST', `/v1.0/api/model-runners/${encodeURIComponent(runnerId)}/pull`, { model }); }
  loadModel(runnerId, model) { return this.request('POST', `/v1.0/api/model-runners/${encodeURIComponent(runnerId)}/load`, { model }); }
  conversations(params = {}) { return this.request('GET', '/v1.0/api/conversations', null, params); }
  updateConversation(id, payload) { return this.request('PUT', `/v1.0/api/conversations/${id}`, payload); }
  deleteConversation(id) { return this.request('DELETE', `/v1.0/api/conversations/${id}`); }
  messages(id, params = {}) { return this.request('GET', `/v1.0/api/conversations/${id}/messages`, null, params); }
  conversationToolCalls(id, params = {}) { return this.request('GET', `/v1.0/api/conversations/${id}/tool-calls`, null, params); }
  chat(payload, options = {}) { return this.request('POST', '/v1.0/api/chat', payload, null, options); }
  tools() { return this.request('GET', '/v1.0/api/tools'); }
  validateTools(payload = {}) { return this.request('POST', '/v1.0/api/tools/validate', payload); }
  testTools(payload = {}) { return this.request('POST', '/v1.0/api/tools/test', payload); }
  tool(name) { return this.request('GET', `/v1.0/api/tools/${encodeURIComponent(name)}`); }
  toolRun(id, params = {}) { return this.request('GET', `/v1.0/api/tool-runs/${encodeURIComponent(id)}`, null, params); }
  tenants(params = {}) { return this.request('GET', '/v1.0/api/tenants', null, params); }
  createTenant(payload) { return this.request('POST', '/v1.0/api/tenants', payload); }
  updateTenant(id, payload) { return this.request('PUT', `/v1.0/api/tenants/${id}`, payload); }
  deleteTenant(id) { return this.request('DELETE', `/v1.0/api/tenants/${id}`); }
  users(tenantId = '', params = {}) { return this.request('GET', '/v1.0/api/users', null, { ...params, tenantId }); }
  createUser(payload) { return this.request('POST', '/v1.0/api/users', payload); }
  updateUser(id, payload) { return this.request('PUT', `/v1.0/api/users/${id}`, payload, { tenantId: payload.tenantId }); }
  deleteUser(id, tenantId) { return this.request('DELETE', `/v1.0/api/users/${id}`, null, { tenantId }); }
  credentials(tenantId = '', params = {}) { return this.request('GET', '/v1.0/api/credentials', null, { ...params, tenantId }); }
  createCredential(payload) { return this.request('POST', '/v1.0/api/credentials', payload); }
  updateCredential(id, payload) { return this.request('PUT', `/v1.0/api/credentials/${id}`, payload, { tenantId: payload.tenantId }); }
  deleteCredential(id, tenantId) { return this.request('DELETE', `/v1.0/api/credentials/${id}`, null, { tenantId }); }
  feedback(payload = null, params = {}) { return payload ? this.request('POST', '/v1.0/api/feedback', payload) : this.request('GET', '/v1.0/api/feedback', null, params); }
  requestHistory(params = {}) { return this.request('GET', '/v1.0/api/request-history', null, params); }
  requestSummary(params = {}) { return this.request('GET', '/v1.0/api/request-history/summary', null, params); }
  requestHistoryToolCalls(id, params = {}) { return this.request('GET', `/v1.0/api/request-history/${encodeURIComponent(id)}/tool-calls`, null, params); }
  deleteRequestHistory(id) { return this.request('DELETE', `/v1.0/api/request-history/${id}`); }
  settings(payload = null) { return payload ? this.request('PUT', '/v1.0/api/settings', payload) : this.request('GET', '/v1.0/api/settings'); }
  openApi() { return this.request('GET', '/openapi.json'); }
}

export default ApiClient;
