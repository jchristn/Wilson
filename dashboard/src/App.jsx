/* eslint-disable react-hooks/set-state-in-effect, react-hooks/exhaustive-deps */
import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import {
  Activity, AlertCircle, Bot, Check, ChevronDown, ChevronLeft, ChevronRight, Clock, Code, Copy, Download, Eye,
  Gauge, History, Info, KeyRound, LayoutDashboard, ListFilter, LogOut,
  MessageSquarePlus, Moon, MoreVertical, Pencil, Play, Plus, RefreshCw, Save,
  Search, Send, Server, Settings, Square, Sun, Trash2, ThumbsDown, ThumbsUp, Users, X
} from 'lucide-react';
import ReactMarkdown from 'react-markdown';
import remarkGfm from 'remark-gfm';
import ApiClient from './utils/api';
import './index.css';

const text = {
  appName: 'Wilson',
  serverUrl: 'Server URL',
  accessKey: 'Access key',
  connect: 'Connect',
  chat: 'Chat',
  history: 'Conversations',
  requests: 'Request History',
  explorer: 'API Explorer',
  settings: 'Settings',
  tenants: 'Tenants',
  users: 'Users',
  credentials: 'Credentials',
  feedback: 'Feedback',
  newChat: 'New chat',
  prompt: 'Message Wilson',
  send: 'Send',
  runner: 'Server',
  model: 'Model',
  noModels: 'No models available',
  streaming: 'Streaming',
  logout: 'Logout',
  view: 'View',
  viewJson: 'View JSON',
  edit: 'Edit',
  delete: 'Delete',
  refresh: 'Refresh'
};

const loginTaglines = [
  "You're stranded on an island. At least Wilson talks back.",
  'No internet. No distractions. Just you and Wilson.',
  "The island is empty. The conversation doesn't have to be.",
  "When you're alone on an island, every conversation matters.",
  'A thousand miles from shore. One message away from company.',
  'No rescue ships. No search parties. Just Wilson.',
  "The island is quiet. Wilson isn't.",
  "You're not completely alone out here.",
  'Days at sea. Endless conversation.',
  'Talk to Wilson. The island can wait.',
  'Just you, the ocean, and a conversation.',
  'Alone on the island. Not alone in thought.',
  'The waves never answer. Wilson does.',
  'Lost at sea. Found in conversation.',
  'No maps. No compass. Just dialogue.',
  'Every castaway needs someone to talk to.',
  "Welcome to the island. Wilson's been expecting you.",
  'Out here, conversation is survival.',
  'The world is far away. Wilson is right here.',
  "The island doesn't judge. Wilson doesn't either.",
  'Shipwrecked, but not speechless.',
  'Sometimes all you need is someone to listen.',
  "The horizon is empty. Your chat window isn't.",
  'One island. One companion. Infinite conversations.',
  'No signal. No noise. Just Wilson.',
  'The tide comes and goes. Wilson stays.',
  "When nobody's around, Wilson is still here.",
  'Alone with your thoughts? Wilson can help with that.',
  'The island is yours. The conversation is ours.',
  "Somewhere between solitude and conversation, you'll find Wilson.",
  'Stranded together.',
  'Your only contact on the island.',
  'The smartest thing to wash ashore.',
  'A conversation companion for the long haul.',
  'Still better than talking to a volleyball.',
  'Like a volleyball, but with opinions.',
  'At least this Wilson can answer back.',
  'The best listener on the island.',
  'Company for the castaway.',
  'Built for conversations beyond the horizon.',
  'A quiet shore, a local model, and one familiar name.',
  'For when the coconut radios stop working.',
  'Washed ashore with answers.',
  'The companion you do not have to draw a face on.',
  'Local conversations for remote moments.'
];

const defaultSystemPrompt = 'Use prior turns only as context. Respond only to the latest user message, and do not replay or quote earlier assistant responses unless the user explicitly asks for them.';

const defaultCompletionSettings = {
  temperature: 0.7,
  topP: 0.9,
  maxTokens: 2048,
  topK: 40,
  minP: 0,
  repeatPenalty: 1.1,
  repeatLastN: 64,
  seed: ''
};

const lastConversationStorageKey = 'wilson.chat.lastConversationId';

const fieldMeta = {
  id: ['ID', 'Unique record identifier. Click the copy button to copy it.'],
  tenantId: ['Tenant ID', 'Tenant that owns this record.'],
  userId: ['User ID', 'User that owns this record.'],
  conversationId: ['Conversation ID', 'Conversation associated with this record.'],
  messageId: ['Message ID', 'Message associated with this record.'],
  runnerId: ['Server ID', 'Model server identifier used by the conversation.'],
  apiType: ['API type', 'Protocol used by this model server.'],
  endpoint: ['Endpoint', 'Base URL for this model server.'],
  contextWindowTokens: ['Context window', 'Maximum token window used for prompt truncation.'],
  healthCheckEnabled: ['Health checks', 'Enables periodic model server health probes.'],
  healthCheckUrl: ['Health URL', 'Absolute URL or endpoint path Wilson probes for health.'],
  healthCheckMethod: ['Health method', 'HTTP method used for health probes.'],
  healthCheckIntervalMs: ['Health interval', 'Milliseconds between health probes.'],
  healthCheckTimeoutMs: ['Health timeout', 'Milliseconds Wilson waits before marking a probe failed.'],
  healthCheckExpectedStatusCode: ['Expected status', 'HTTP status code required for a healthy probe.'],
  healthyThreshold: ['Healthy threshold', 'Consecutive successful checks required to mark healthy.'],
  unhealthyThreshold: ['Unhealthy threshold', 'Consecutive failed checks required to mark unhealthy.'],
  healthCheckUseAuth: ['Use auth', 'Send this model server API key with health probes.'],
  title: ['Title', 'Human-readable conversation title.'],
  name: ['Name', 'Human-readable name.'],
  email: ['Email', 'User email address.'],
  firstName: ['First name', 'User first name.'],
  lastName: ['Last name', 'User last name.'],
  isAdmin: ['Global admin', 'Allows global administration.'],
  isTenantAdmin: ['Tenant admin', 'Allows tenant-scoped administration.'],
  active: ['Active', 'Controls whether this record is enabled.'],
  isProtected: ['Protected', 'Protected seed records should be changed carefully.'],
  accessKey: ['Access key', 'Credential bearer token.'],
  secretLast4: ['Secret last 4', 'Last four characters of the credential secret.'],
  lastUsedUtc: ['Last used', 'Last time this credential was used.'],
  createdUtc: ['Created', 'UTC timestamp when the record was created.'],
  lastUpdateUtc: ['Updated', 'UTC timestamp when the record was last updated.'],
  method: ['Method', 'HTTP method for the request.'],
  path: ['Path', 'HTTP path that was requested.'],
  statusCode: ['Status', 'HTTP response status code.'],
  durationMs: ['Latency', 'Request duration in milliseconds.'],
  timeToFirstTokenMs: ['Time to first token (ms)', 'Milliseconds from request start until the first generated token was received.'],
  streamingTimeMs: ['Streaming time (ms)', 'Milliseconds spent receiving generated tokens.'],
  totalTimeMs: ['Total time (ms)', 'Total model inference time in milliseconds.'],
  tokensUsed: ['Tokens used', 'Estimated prompt and response tokens used by this message or request.'],
  tokenEstimate: ['Token estimate', 'Estimated token count for this message.'],
  requestHeaders: ['Request headers', 'HTTP request headers captured for this request.'],
  requestBody: ['Request body', 'HTTP request body captured for this request.'],
  responseHeaders: ['Response headers', 'HTTP response headers captured for this request.'],
  responseBody: ['Response body', 'HTTP response body captured for this request.'],
  rating: ['Rating', 'User feedback rating.'],
  comment: ['Comment', 'Optional feedback comment.'],
  model: ['Model', 'Model used for the conversation.']
};

const idFields = new Set(['id', 'tenantId', 'userId', 'conversationId', 'messageId', 'runnerId']);

function App() {
  const [session, setSession] = useState(() => JSON.parse(localStorage.getItem('wilson.session') || 'null'));
  const [theme, setTheme] = useState(() => localStorage.getItem('wilson.theme') || 'light');
  const [view, setView] = useState('chat');
  const api = useMemo(() => session ? new ApiClient(session.serverUrl, session.token) : null, [session]);

  useEffect(() => {
    document.documentElement.setAttribute('data-theme', theme);
    localStorage.setItem('wilson.theme', theme);
  }, [theme]);

  const login = useCallback(async (serverUrl, accessKey) => {
    const client = new ApiClient(serverUrl, null);
    const result = await client.login(accessKey);
    const next = { serverUrl, token: result.token, user: result.user };
    localStorage.setItem('wilson.session', JSON.stringify(next));
    setSession(next);
  }, []);

  const logout = useCallback(() => {
    localStorage.removeItem('wilson.session');
    setSession(null);
  }, []);

  if (!session) return <Login onLogin={login} theme={theme} onToggleTheme={() => setTheme(theme === 'light' ? 'dark' : 'light')} />;

  const nav = [
    ['CHAT', [
      ['chat', text.chat, Bot]
    ]],
    ['MANAGE', [
      ['models', 'Model Servers', Server],
      ['history', text.history, History],
      ['feedback', text.feedback, ThumbsUp],
      ['requests', text.requests, ListFilter],
      ['explorer', text.explorer, Play]
    ]],
    ['ADMINISTRATION', [
      ['tenants', text.tenants, LayoutDashboard],
      ['users', text.users, Users],
      ['credentials', text.credentials, KeyRound],
      ['settings', text.settings, Settings]
    ]]
  ];

  return (
    <div className="shell">
      <aside className="sidebar">
        <div className="brand" title="Wilson dashboard"><img src="/logo.png" alt="" /> <span>{text.appName}</span></div>
        <nav>
          {nav.map(([section, items]) => (
            <div key={section} className="nav-section">
              <div className="nav-section-title">{section}</div>
              {items.map(([id, label, Icon]) => (
                <button key={id} className={view === id ? 'nav active' : 'nav'} onClick={() => setView(id)} title={`Open ${label}`}>
                  <Icon size={18} /><span>{label}</span>
                </button>
              ))}
            </div>
          ))}
        </nav>
      </aside>
      <main className="workspace">
        <Topbar session={session} theme={theme} onToggleTheme={() => setTheme(theme === 'light' ? 'dark' : 'light')} onLogout={logout} />
        <div className="workspace-body">
          {view === 'chat' && <Chat api={api} />}
          {view === 'models' && <ModelServersView api={api} />}
          {view === 'history' && <HistoryView api={api} />}
          {view === 'requests' && <RequestHistory api={api} />}
          {view === 'explorer' && <ApiExplorer api={api} />}
          {view === 'tenants' && <TenantAdmin api={api} />}
          {view === 'users' && <UserAdmin api={api} />}
          {view === 'credentials' && <CredentialAdmin api={api} />}
          {view === 'feedback' && <FeedbackAdmin api={api} />}
          {view === 'settings' && <SettingsAdmin api={api} />}
        </div>
      </main>
    </div>
  );
}

function Topbar({ session, theme, onToggleTheme, onLogout }) {
  const email = session?.user?.principalName || session?.user?.email || 'Authenticated user';
  return (
    <header className="topbar">
      <div className="topbar-connection">
        <span title={`Connected server URL: ${session.serverUrl}`}>Server: <strong>{session.serverUrl}</strong></span>
        <span title={`Logged in user: ${email}`}>User: <strong>{email}</strong></span>
      </div>
      <div className="topbar-actions">
        <a className="icon-button" href="https://github.com/jchristn/Wilson" target="_blank" rel="noreferrer" title="Open Wilson on GitHub" aria-label="Open Wilson on GitHub"><GitHubMark /></a>
        <button className="icon-button" onClick={onToggleTheme} title={`Switch dashboard to ${theme === 'light' ? 'dark' : 'light'} mode`} aria-label="Toggle dashboard theme">{theme === 'light' ? <Moon size={18} /> : <Sun size={18} />}</button>
        <button className="icon-button" onClick={onLogout} title="Log out of the dashboard" aria-label={text.logout}><LogOut size={18} /></button>
      </div>
    </header>
  );
}

function Login({ onLogin, theme, onToggleTheme }) {
  const defaultServerUrl = window.__WILSON_CONFIG__?.serverUrl || 'http://127.0.0.1:9400';
  const [serverUrl, setServerUrl] = useState(localStorage.getItem('wilson.serverUrl') || defaultServerUrl);
  const [accessKey, setAccessKey] = useState('');
  const [error, setError] = useState('');
  const [tagline] = useState(() => loginTaglines[Math.floor(Math.random() * loginTaglines.length)]);
  async function submit(event) {
    event.preventDefault();
    setError('');
    try {
      localStorage.setItem('wilson.serverUrl', serverUrl);
      await onLogin(serverUrl, accessKey);
    } catch (err) {
      setError(String(err.message || err));
    }
  }
  return (
    <div className="login">
      <form onSubmit={submit} className="login-panel">
        <button type="button" className="icon-button login-theme" onClick={onToggleTheme} title={`Switch dashboard to ${theme === 'light' ? 'dark' : 'light'} mode`} aria-label="Toggle dashboard theme">{theme === 'light' ? <Moon size={18} /> : <Sun size={18} />}</button>
        <div className="login-logo"><img src="/logo.png" alt="" /></div>
        <div className="brand large"><span>{text.appName}</span></div>
        <p className="login-tagline" title="Wilson login tagline">{tagline}</p>
        <label title="Base URL for the Wilson REST API">{text.serverUrl}<input title="Enter the Wilson REST API base URL" value={serverUrl} onChange={e => setServerUrl(e.target.value)} /></label>
        <label title="Access key or administrator bearer token">{text.accessKey}<input title="Enter your Wilson access key or administrator bearer token" value={accessKey} onChange={e => setAccessKey(e.target.value)} type="password" autoFocus /></label>
        {error && <div className="error" title="Login error">{error}</div>}
        <button className="primary" title="Connect to Wilson using the supplied server URL and access key"><Check size={18} />{text.connect}</button>
      </form>
    </div>
  );
}

function GitHubMark() {
  return (
    <svg viewBox="0 0 16 16" width="18" height="18" aria-hidden="true" fill="currentColor">
      <path d="M8 0C3.58 0 0 3.67 0 8.2c0 3.63 2.29 6.7 5.47 7.79.4.08.55-.18.55-.4 0-.2-.01-.84-.01-1.52-2.01.38-2.53-.5-2.69-.96-.09-.24-.48-.96-.82-1.16-.28-.16-.68-.56-.01-.57.63-.01 1.08.59 1.23.84.72 1.24 1.87.89 2.33.68.07-.53.28-.89.51-1.09-1.78-.21-3.64-.91-3.64-4.04 0-.89.31-1.62.82-2.19-.08-.21-.36-1.04.08-2.16 0 0 .67-.22 2.2.84A7.43 7.43 0 0 1 8 3.98c.68 0 1.36.09 2 .28 1.53-1.06 2.2-.84 2.2-.84.44 1.12.16 1.95.08 2.16.51.57.82 1.3.82 2.19 0 3.14-1.87 3.83-3.65 4.04.29.25.54.75.54 1.52 0 1.09-.01 1.97-.01 2.24 0 .22.15.48.55.4A8.13 8.13 0 0 0 16 8.2C16 3.67 12.42 0 8 0Z" />
    </svg>
  );
}

function modelKey(runnerId, model) {
  return `${runnerId || ''}::${String(model || '').toLowerCase()}`;
}

function enumerationObjects(result) {
  return Array.isArray(result) ? result : result?.objects || [];
}

function sameModelName(left, right) {
  return String(left || '').toLowerCase() === String(right || '').toLowerCase();
}

function chatModelsForRunner(runner) {
  if (!runner) return [];
  if (Array.isArray(runner.chatModels)) return runner.chatModels;
  return runner.models || [];
}

function parseSseFrame(frame) {
  let event = '';
  const data = [];
  frame.split(/\r?\n/).forEach(line => {
    if (line.startsWith('event:')) event = line.slice(6).trim();
    if (line.startsWith('data:')) data.push(line.slice(5).trimStart());
  });
  return { event, data: data.join('\n') };
}

function Chat({ api }) {
  const [runners, setRunners] = useState([]);
  const [conversations, setConversations] = useState([]);
  const [conversation, setConversation] = useState(null);
  const [messages, setMessages] = useState([]);
  const [runnerId, setRunnerId] = useState('');
  const [model, setModel] = useState('');
  const [prompt, setPrompt] = useState('');
  const [streaming, setStreaming] = useState(true);
  const [systemPrompt, setSystemPrompt] = useState(() => localStorage.getItem('wilson.chat.systemPrompt') || defaultSystemPrompt);
  const [completionSettings, setCompletionSettings] = useState(() => {
    try { return { ...defaultCompletionSettings, ...JSON.parse(localStorage.getItem('wilson.chat.completionSettings') || '{}') }; }
    catch { return defaultCompletionSettings; }
  });
  const [systemPromptOpen, setSystemPromptOpen] = useState(false);
  const [completionSettingsOpen, setCompletionSettingsOpen] = useState(false);
  const [busy, setBusy] = useState(false);
  const [modelError, setModelError] = useState('');
  const [loadingModels, setLoadingModels] = useState(false);
  const [loadedKeys, setLoadedKeys] = useState(new Set());
  const [loadingModelKey, setLoadingModelKey] = useState('');
  const [renameConversation, setRenameConversation] = useState(null);
  const [deleteConversation, setDeleteConversation] = useState(null);
  const [conversationError, setConversationError] = useState('');
  const [truncationNotice, setTruncationNotice] = useState(null);
  const bottom = useRef(null);
  const promptInput = useRef(null);
  const generationAbort = useRef(null);
  const restoredConversation = useRef(false);
  const desiredSelection = useRef({ runnerId: '', model: '' });

  const loadConversations = useCallback(async () => {
    const items = enumerationObjects(await api.conversations({ pageNumber: 1, pageSize: 500 }));
    setConversations(items);
    if (!restoredConversation.current) {
      restoredConversation.current = true;
      const lastConversationId = localStorage.getItem(lastConversationStorageKey);
      const lastConversation = items.find(item => item.id === lastConversationId);
      if (lastConversation) await loadConversation(lastConversation, { remember: false });
    }
  }, [api]);

  const loadRunners = useCallback(async (selection = {}) => {
    setLoadingModels(true);
    setModelError('');
    try {
      const items = enumerationObjects(await api.runners({ pageNumber: 1, pageSize: 500 }));
      const latestDesired = desiredSelection.current;
      const preferredRunnerId = selection.runnerId ?? latestDesired.runnerId ?? runnerId;
      const preferredModel = selection.model ?? latestDesired.model ?? model;
      setRunners(items);
      setLoadedKeys(new Set(items.flatMap(item => (item.loadedModels || []).map(loaded => modelKey(item.id, loaded)))));
      const selected = items.find(item => item.id === preferredRunnerId) || items[0];
      if (selected) {
        const models = chatModelsForRunner(selected);
        setRunnerId(selected.id);
        setModel(models.includes(preferredModel) ? preferredModel : models[0] || '');
        setModelError(models.length ? '' : 'No chat-capable models were returned. Embedding-only models are hidden from Chat. Use Model Servers to review all available models.');
      }
    } catch (err) {
      setModelError(String(err.message || err));
    } finally {
      setLoadingModels(false);
    }
  }, [api, runnerId, model]);

  useEffect(() => { loadRunners(); loadConversations(); }, [api]);
  useEffect(() => { bottom.current?.scrollIntoView({ behavior: 'smooth' }); }, [messages]);
  useEffect(() => {
    const area = promptInput.current;
    if (!area) return;
    area.style.height = 'auto';
    area.style.height = `${area.scrollHeight}px`;
  }, [prompt]);
  useEffect(() => { localStorage.setItem('wilson.chat.systemPrompt', systemPrompt); }, [systemPrompt]);
  useEffect(() => { localStorage.setItem('wilson.chat.completionSettings', JSON.stringify(completionSettings)); }, [completionSettings]);

  useEffect(() => {
    const selected = runners.find(item => item.id === runnerId);
    if (!selected) return;
    const models = chatModelsForRunner(selected);
    if (models.length) {
      if (!models.includes(model)) setModel(models[0]);
      setModelError('');
    } else {
      setModel('');
      setModelError('No chat-capable models available. Use Model Servers to verify this runner has a completion model.');
    }
  }, [runnerId, runners, model]);

  async function loadConversation(item, options = {}) {
    desiredSelection.current = { runnerId: item.runnerId, model: item.model };
    setConversation(item);
    setRunnerId(item.runnerId);
    setModel(item.model);
    setTruncationNotice(null);
    if (options.remember !== false) localStorage.setItem(lastConversationStorageKey, item.id);
    setMessages(enumerationObjects(await api.messages(item.id, { pageNumber: 1, pageSize: 500 })));
  }

  async function loadSelectedModel() {
    if (!runnerId || !model || loadingModelKey) return;
    const key = modelKey(runnerId, model);
    desiredSelection.current = { runnerId, model };
    setLoadingModelKey(key);
    setModelError('');
    try {
      await api.loadModel(runnerId, model);
      await loadRunners({ runnerId, model });
      setLoadedKeys(prev => new Set([...prev, key]));
    } catch (err) {
      setModelError(String(err.message || err));
    } finally {
      setLoadingModelKey('');
    }
  }

  async function send() {
    if (!prompt.trim() || !model || busy) return;
    const controller = new AbortController();
    generationAbort.current = controller;
    const body = { conversationId: conversation?.id, runnerId, model, prompt, settings: normalizeCompletionSettings(systemPrompt, completionSettings) };
    const user = { id: `local-${Date.now()}`, role: 'user', content: prompt };
    setMessages(prev => [...prev, user]);
    setPrompt('');
    setBusy(true);
    try {
      if (!streaming) {
        const result = await api.chat(body, { signal: controller.signal });
        desiredSelection.current = { runnerId: result.conversation.runnerId, model: result.conversation.model };
        setConversation(result.conversation);
        handleTruncationNotice(result.truncation);
        localStorage.setItem(lastConversationStorageKey, result.conversation.id);
        setMessages(prev => [...prev, result.assistantMessage]);
      } else {
        const response = await api.raw('POST', '/v1.0/api/chat/stream', JSON.stringify(body), null, { signal: controller.signal });
        if (!response.ok) throw new Error(await response.text());
        const reader = response.body.getReader();
        const decoder = new TextDecoder();
        const assistantId = `stream-${Date.now()}`;
        let assistant = { id: assistantId, role: 'assistant', content: '' };
        let buffer = '';
        setMessages(prev => [...prev, assistant]);
        while (true) {
          const { value, done } = await reader.read();
          if (done) break;
          buffer += decoder.decode(value, { stream: true });
          const frames = buffer.split('\n\n');
          buffer = frames.pop() || '';
          frames.forEach(frame => {
            const { event, data } = parseSseFrame(frame);
            if (!event || !data) return;
            const parsed = JSON.parse(data);
            if (event === 'conversation') {
              desiredSelection.current = { runnerId: parsed.runnerId, model: parsed.model };
              setConversation(parsed);
              localStorage.setItem(lastConversationStorageKey, parsed.id);
            }
            if (event === 'truncation') {
              handleTruncationNotice(parsed);
            }
            if (event === 'chunk') {
              assistant = { ...assistant, content: assistant.content + (parsed.text || '') };
              setMessages(prev => prev.map(item => item.id === assistant.id ? assistant : item));
            }
            if (event === 'error') {
              const message = parsed.error || parsed.detail || 'The selected model could not generate a response.';
              assistant = { ...assistant, content: message, error: true };
              setModelError(message);
              setMessages(prev => prev.map(item => item.id === assistantId ? assistant : item));
            }
            if (event === 'done') {
              assistant = parsed;
              setMessages(prev => prev.map(item => item.id === assistantId ? assistant : item));
            }
          });
        }
      }
      setConversations(enumerationObjects(await api.conversations({ pageNumber: 1, pageSize: 500 })));
    } catch (err) {
      if (controller.signal.aborted || err?.name === 'AbortError') return;
      const message = String(err.message || err);
      setModelError(message.includes('does not support') ? 'The selected model could not generate a chat response. Choose a chat or completion model instead of an embedding-only model.' : message);
      setMessages(prev => [...prev, { id: `error-${Date.now()}`, role: 'assistant', content: message, error: true }]);
    } finally {
      if (generationAbort.current === controller) generationAbort.current = null;
      setBusy(false);
    }
  }

  function stopGeneration() {
    generationAbort.current?.abort();
  }

  async function renameSelectedConversation(item, title) {
    const updated = await api.updateConversation(item.id, { ...item, title });
    setConversation(current => current?.id === updated.id ? updated : current);
    if (conversation?.id === updated.id) localStorage.setItem(lastConversationStorageKey, updated.id);
    await loadConversations();
    setRenameConversation(null);
  }

  async function deleteSelectedConversation(item) {
    await api.deleteConversation(item.id);
    if (conversation?.id === item.id) {
      desiredSelection.current = { runnerId: '', model: '' };
      setConversation(null);
      setTruncationNotice(null);
      setMessages([]);
    }
    if (localStorage.getItem(lastConversationStorageKey) === item.id) localStorage.removeItem(lastConversationStorageKey);
    await loadConversations();
    setDeleteConversation(null);
  }

  function handleTruncationNotice(notice) {
    if (!notice?.truncated) {
      setTruncationNotice(null);
      return;
    }
    setTruncationNotice(notice);
    const retainCount = Math.max(2, Number(notice.includedMessageCount || 0) + 3);
    setMessages(prev => prev.length > retainCount ? prev.slice(prev.length - retainCount) : prev);
  }

  const selectedRunner = runners.find(item => item.id === runnerId);
  const modelOptions = useMemo(() => {
    return [...chatModelsForRunner(selectedRunner)];
  }, [selectedRunner]);
  const selectedModelKey = modelKey(runnerId, model);
  const modelLoaded = loadedKeys.has(selectedModelKey) || (selectedRunner?.loadedModels || []).some(item => sameModelName(item, model));
  const canLoadModel = selectedRunner?.apiType === 'Ollama' && model && !modelLoaded;
  const modelLoading = loadingModelKey === selectedModelKey;
  return (
    <div className="chat-layout">
      <aside className="conversation-list">
        <button className="new-chat" title="Start a new conversation" onClick={() => { desiredSelection.current = { runnerId: '', model: '' }; localStorage.removeItem(lastConversationStorageKey); setConversation(null); setTruncationNotice(null); setMessages([]); }}><MessageSquarePlus size={18} />{text.newChat}</button>
        {conversationError && <div className="conversation-error" title="Conversation action error">{conversationError}</div>}
        <div className="conversation-scroll">
          {conversations.map(item => (
            <div key={item.id} className={conversation?.id === item.id ? 'conversation-row active' : 'conversation-row'} title={`Conversation: ${item.title}`}>
              <button title={`Load conversation ${item.title}`} className="conversation" onClick={() => loadConversation(item)}>{item.title}</button>
              <ActionMenu title="Open conversation actions" items={[
                { label: 'Rename', tooltip: `Rename conversation ${item.title}`, icon: Pencil, onClick: () => { setConversationError(''); setRenameConversation(item); } },
                { label: text.delete, tooltip: `Delete conversation ${item.title}`, icon: Trash2, danger: true, onClick: () => { setConversationError(''); setDeleteConversation(item); } }
              ]} />
            </div>
          ))}
        </div>
        {renameConversation && (
          <RenameConversationModal
            conversation={renameConversation}
            onClose={() => setRenameConversation(null)}
            onSave={async title => {
              try {
                setConversationError('');
                await renameSelectedConversation(renameConversation, title);
              } catch (err) {
                setConversationError(String(err.message || err));
                setRenameConversation(null);
              }
            }}
          />
        )}
        {deleteConversation && (
          <ConfirmModal
            title="Delete Conversation"
            message={`Delete "${deleteConversation.title}" and its message history? This cannot be undone.`}
            onCancel={() => setDeleteConversation(null)}
            onConfirm={async () => {
              try {
                setConversationError('');
                await deleteSelectedConversation(deleteConversation);
              } catch (err) {
                setConversationError(String(err.message || err));
                setDeleteConversation(null);
              }
            }}
          />
        )}
      </aside>
      <section className="chat-main">
        <PageIntro title="Chat" description="Send prompts to a selected model server, stream responses, review prior conversations, and load Ollama models into memory before use." />
        <div className="chat-toolbar">
          <label title="Select the model server used for chat requests">{text.runner}<select title="Select the model server used for chat requests" value={runnerId} onChange={e => setRunnerId(e.target.value)}>{runners.map(item => <option key={item.id} value={item.id}>{item.name}</option>)}</select></label>
          <label title="Select any model returned by the configured server">{text.model}<select title="Select the model to use for chat requests" value={model} onChange={e => setModel(e.target.value)} disabled={modelOptions.length < 1}>{modelOptions.length < 1 ? <option value="">{loadingModels ? 'Loading models' : text.noModels}</option> : modelOptions.map(item => <option key={item}>{item}</option>)}</select></label>
          <button className="icon-button" title="Refresh model servers and query Ollama model lists" onClick={loadRunners}><RefreshCw size={16} /></button>
          {canLoadModel && <button className="secondary toolbar-button" title={`Load ${model} into Ollama memory`} onClick={loadSelectedModel} disabled={modelLoading}>{modelLoading ? <RefreshCw size={16} className="spin" /> : <Download size={16} />}Load Model</button>}
          {modelLoading && <span className="model-loading-note" title="Model loading may take several minutes depending on model size">Model loading may take several minutes depending on model size</span>}
          <button className="secondary toolbar-button" title="Edit the system prompt used for chat completion requests" onClick={() => setSystemPromptOpen(true)}><Pencil size={16} />System Prompt</button>
          <button className="secondary toolbar-button" title="Edit generation settings used for chat completion requests" onClick={() => setCompletionSettingsOpen(true)}><Settings size={16} />Settings</button>
          <label className="toggle" title="Enable streaming responses"><input title="Toggle streaming responses" type="checkbox" checked={streaming} onChange={e => setStreaming(e.target.checked)} />{text.streaming}</label>
        </div>
        {modelError && <div className="model-error" title="Model loading status">{modelError}</div>}
        <div className="messages">
          {truncationNotice && (
            <div className="context-truncation-notice" title="Older conversation messages were omitted from the model prompt to stay within the configured context window">
              Older context was omitted for this request. {truncationNotice.omittedMessageCount} prior message{truncationNotice.omittedMessageCount === 1 ? '' : 's'} were excluded; {truncationNotice.includedMessageCount} were kept.
            </div>
          )}
          {messages.map(item => <Message key={item.id} api={api} message={item} conversation={conversation} />)}
          <div ref={bottom} />
        </div>
        <div className="composer">
          <div className="composer-input">
            <textarea ref={promptInput} rows={1} title="Type a message to send to Wilson" value={prompt} onChange={e => setPrompt(e.target.value)} onKeyDown={e => { if (e.key === 'Enter' && !e.shiftKey) { e.preventDefault(); send(); } }} placeholder={text.prompt} />
            <div className="ai-disclaimer" title="Reminder to verify generated content">AI can make mistakes, verify all results.</div>
          </div>
          <button className={busy ? 'send stop' : 'send'} onClick={busy ? stopGeneration : send} disabled={!busy && (!prompt.trim() || !model)} title={busy ? 'Stop the current model response' : 'Send this message to Wilson'}>{busy ? <Square size={18} /> : <Send size={20} />}</button>
        </div>
      </section>
      {systemPromptOpen && <SystemPromptModal value={systemPrompt} onChange={setSystemPrompt} onClose={() => setSystemPromptOpen(false)} />}
      {completionSettingsOpen && <CompletionSettingsModal settings={completionSettings} runner={selectedRunner} onChange={setCompletionSettings} onClose={() => setCompletionSettingsOpen(false)} />}
    </div>
  );
}

function Message({ api, message, conversation }) {
  const [rated, setRated] = useState(false);
  const [feedbackDraft, setFeedbackDraft] = useState(null);
  const [infoOpen, setInfoOpen] = useState(false);
  async function rate(value, comment) {
    if (!conversation) return;
    await api.feedback({ conversationId: conversation.id, messageId: message.id, rating: value, comment: comment || '' });
    setRated(true);
    setFeedbackDraft(null);
  }
  return (
    <div className={`message ${message.role}`}>
      <div className="bubble">{message.content ? <MarkdownMessage content={message.content} /> : (message.role === 'assistant' ? <ThinkingIndicator /> : '')}</div>
      {message.role === 'assistant' && <div className="rating">
        <button onClick={() => setFeedbackDraft({ rating: 1, comment: '' })} disabled={rated} title="Mark this assistant response as helpful and optionally explain why"><ThumbsUp size={15} /></button>
        <button onClick={() => setFeedbackDraft({ rating: -1, comment: '' })} disabled={rated} title="Mark this assistant response as not helpful and optionally explain why"><ThumbsDown size={15} /></button>
        <button onClick={() => setInfoOpen(true)} title="Show timing and token details for this assistant response"><Info size={15} /></button>
      </div>}
      {infoOpen && <MessageInfoModal message={message} onClose={() => setInfoOpen(false)} />}
      {feedbackDraft && (
        <FeedbackCommentModal
          draft={feedbackDraft}
          onChange={setFeedbackDraft}
          onClose={() => setFeedbackDraft(null)}
          onSave={() => rate(feedbackDraft.rating, feedbackDraft.comment)}
        />
      )}
    </div>
  );
}

function MarkdownMessage({ content }) {
  return <ReactMarkdown remarkPlugins={[remarkGfm]}>{content}</ReactMarkdown>;
}

function ThinkingIndicator() {
  return <div className="thinking-indicator" title="Waiting for model response"><span /><span /><span /></div>;
}

function MessageInfoModal({ message, onClose }) {
  return (
    <Modal title="Response Details" onClose={onClose}>
      <KeyValueTable rows={[
        ['timeToFirstTokenMs', message.timeToFirstTokenMs || 0],
        ['streamingTimeMs', message.streamingTimeMs || 0],
        ['totalTimeMs', message.totalTimeMs || 0],
        ['tokensUsed', message.tokensUsed || message.tokenEstimate || 0]
      ]} />
    </Modal>
  );
}

function PageIntro({ title, description, actions = null }) {
  return (
    <div className="page-header">
      <div>
        <h1 title={title}>{title}</h1>
        <p title={description}>{description}</p>
      </div>
      {actions && <div className="page-header-actions">{actions}</div>}
    </div>
  );
}

function normalizeCompletionSettings(systemPrompt, settings) {
  const numberOrNull = (value) => value === '' || value === null || value === undefined ? null : Number(value);
  return {
    systemPrompt: systemPrompt || defaultSystemPrompt,
    temperature: numberOrNull(settings.temperature),
    topP: numberOrNull(settings.topP),
    maxTokens: numberOrNull(settings.maxTokens),
    topK: numberOrNull(settings.topK),
    minP: numberOrNull(settings.minP),
    repeatPenalty: numberOrNull(settings.repeatPenalty),
    repeatLastN: numberOrNull(settings.repeatLastN),
    seed: numberOrNull(settings.seed)
  };
}

function SystemPromptModal({ value, onChange, onClose }) {
  const [draft, setDraft] = useState(value || defaultSystemPrompt);
  return (
    <Modal title="System Prompt" onClose={onClose} wide>
      <div className="chat-settings-form">
        <label title="System prompt sent with each chat completion request">
          System prompt
          <textarea className="system-prompt-textarea" title="Instructions that define how the selected model should behave" value={draft} onChange={e => setDraft(e.target.value)} />
        </label>
      </div>
      <div className="modal-actions">
        <button className="secondary" title="Restore the default Wilson system prompt" onClick={() => setDraft(defaultSystemPrompt)}>Reset Default</button>
        <button className="secondary" title="Close without saving system prompt changes" onClick={onClose}>Cancel</button>
        <button className="primary" title="Save this system prompt for future chat requests" onClick={() => { onChange(draft.trim() || defaultSystemPrompt); onClose(); }}><Save size={16} />Save</button>
      </div>
    </Modal>
  );
}

function CompletionSettingsModal({ settings, runner, onChange, onClose }) {
  const [draft, setDraft] = useState({ ...defaultCompletionSettings, ...settings });
  const set = (key, value) => setDraft(prev => ({ ...prev, [key]: value }));
  const isOllama = runner?.apiType === 'Ollama';
  return (
    <Modal title="Completion Settings" onClose={onClose} wide>
      <div className="chat-settings-form">
        <div className="form-grid">
          <FormInput label="Temperature" tooltip="Sampling temperature. Lower values are more deterministic; higher values are more creative." type="number" value={draft.temperature} onChange={v => set('temperature', v)} />
          <FormInput label="Top P" tooltip="Nucleus sampling threshold from 0 to 1. Lower values narrow token selection." type="number" value={draft.topP} onChange={v => set('topP', v)} />
          <FormInput label="Max tokens" tooltip="Maximum number of tokens the model should generate." type="number" value={draft.maxTokens} onChange={v => set('maxTokens', v)} />
          <FormInput label="Seed" tooltip="Optional random seed for reproducible completions where supported. Leave blank for random." type="number" value={draft.seed} onChange={v => set('seed', v)} />
        </div>
        <div className={isOllama ? 'form-grid' : 'form-grid muted-settings'} title={isOllama ? 'Ollama-specific completion settings' : 'These settings apply only to Ollama model servers'}>
          <FormInput label="Top K" tooltip="Ollama top-K sampling. Lower values restrict candidate tokens." type="number" value={draft.topK} onChange={v => set('topK', v)} />
          <FormInput label="Min P" tooltip="Ollama minimum probability threshold." type="number" value={draft.minP} onChange={v => set('minP', v)} />
          <FormInput label="Repeat penalty" tooltip="Ollama repeat penalty. Higher values discourage repetition." type="number" value={draft.repeatPenalty} onChange={v => set('repeatPenalty', v)} />
          <FormInput label="Repeat last N" tooltip="Ollama token lookback window used for repeat penalty." type="number" value={draft.repeatLastN} onChange={v => set('repeatLastN', v)} />
        </div>
        {!isOllama && <p className="settings-note" title="Ollama-specific settings are ignored for this runner type">Top K, Min P, repeat penalty, and repeat lookback are only sent to Ollama runners.</p>}
      </div>
      <div className="modal-actions">
        <button className="secondary" title="Restore sensible default completion settings" onClick={() => setDraft(defaultCompletionSettings)}>Reset Defaults</button>
        <button className="secondary" title="Close without saving completion setting changes" onClick={onClose}>Cancel</button>
        <button className="primary" title="Save these completion settings for future chat requests" onClick={() => { onChange(draft); onClose(); }}><Save size={16} />Save</button>
      </div>
    </Modal>
  );
}

function RenameConversationModal({ conversation, onClose, onSave }) {
  const [title, setTitle] = useState(conversation.title || '');
  const [saving, setSaving] = useState(false);
  const trimmed = title.trim();

  async function save() {
    if (!trimmed || saving) return;
    setSaving(true);
    try {
      await onSave(trimmed);
    } finally {
      setSaving(false);
    }
  }

  return (
    <Modal title="Rename Conversation" onClose={onClose}>
      <div className="form-grid">
        <FormInput label="Conversation title" tooltip="Human-readable name shown in the chat conversation history list" value={title} onChange={setTitle} />
      </div>
      <div className="modal-actions">
        <button className="secondary" title="Close without renaming this conversation" onClick={onClose}>Cancel</button>
        <button className="primary" title="Save the new conversation title" onClick={save} disabled={!trimmed || saving}><Save size={16} />{saving ? 'Saving' : 'Save'}</button>
      </div>
    </Modal>
  );
}

function defaultHealthCheckSettings(apiType = 'Ollama', endpoint = 'http://localhost:11434', apiKey = '') {
  const isOllama = String(apiType || '').toLowerCase() === 'ollama';
  const base = String(endpoint || '').replace(/\/+$/, '');
  return {
    healthCheckEnabled: true,
    healthCheckUrl: `${base}${isOllama ? '/api/tags' : '/v1/models'}`,
    healthCheckMethod: 'GET',
    healthCheckIntervalMs: isOllama ? 5000 : 15000,
    healthCheckTimeoutMs: isOllama ? 2000 : 5000,
    healthCheckExpectedStatusCode: 200,
    healthyThreshold: 2,
    unhealthyThreshold: 2,
    healthCheckUseAuth: !isOllama && Boolean(apiKey)
  };
}

function newModelRunner() {
  return {
    id: `runner-${Date.now()}`,
    name: '',
    apiType: 'Ollama',
    endpoint: 'http://localhost:11434',
    apiKey: '',
    models: [],
    contextWindowTokens: 8192,
    ...defaultHealthCheckSettings('Ollama', 'http://localhost:11434')
  };
}

function normalizeRunnerForSave(runner) {
  const defaults = defaultHealthCheckSettings(runner.apiType, runner.endpoint, runner.apiKey);
  return {
    ...runner,
    models: runner.models || [],
    contextWindowTokens: Number(runner.contextWindowTokens || 8192),
    apiKey: runner.apiKey || null,
    healthCheckEnabled: runner.healthCheckEnabled !== false,
    healthCheckUrl: runner.healthCheckUrl || defaults.healthCheckUrl,
    healthCheckMethod: runner.healthCheckMethod || defaults.healthCheckMethod,
    healthCheckIntervalMs: Number(runner.healthCheckIntervalMs || defaults.healthCheckIntervalMs),
    healthCheckTimeoutMs: Number(runner.healthCheckTimeoutMs || defaults.healthCheckTimeoutMs),
    healthCheckExpectedStatusCode: Number(runner.healthCheckExpectedStatusCode || defaults.healthCheckExpectedStatusCode),
    healthyThreshold: Number(runner.healthyThreshold || defaults.healthyThreshold),
    unhealthyThreshold: Number(runner.unhealthyThreshold || defaults.unhealthyThreshold),
    healthCheckUseAuth: Boolean(runner.healthCheckUseAuth)
  };
}

function healthField(health, key) {
  if (!health) return undefined;
  const pascal = key.charAt(0).toUpperCase() + key.slice(1);
  return health[key] ?? health[pascal];
}

function healthHistory(health) {
  return healthField(health, 'history') || [];
}

function healthMapFromList(items) {
  const map = {};
  (Array.isArray(items) ? items : enumerationObjects(items)).forEach(item => {
    const id = healthField(item, 'endpointId');
    if (id) map[id] = item;
  });
  return map;
}

function modelServerHealthPresentation(server) {
  if (server.healthCheckEnabled === false) return { label: 'Disabled', tone: 'status-redirect', title: 'Health checks are disabled for this model server.' };
  const health = server.health;
  if (!health || !healthField(health, 'lastCheckUtc')) return { label: 'Awaiting Check', tone: 'status-warn', title: 'Awaiting the first background health check.' };
  if (healthField(health, 'isHealthy')) return { label: 'Healthy', tone: 'status-ok', title: `Healthy since ${formatDate(healthField(health, 'lastHealthyUtc')) || 'the latest passing checks'}.` };
  return { label: 'Unhealthy', tone: 'status-error', title: healthField(health, 'lastError') || 'The latest health checks are failing.' };
}

function ModelServersView({ api }) {
  const [servers, setServers] = useState([]);
  const [settings, setSettings] = useState(null);
  const [error, setError] = useState('');
  const [loading, setLoading] = useState(false);
  const [liveLoading, setLiveLoading] = useState(false);
  const [editServer, setEditServer] = useState(null);
  const [deleteServer, setDeleteServer] = useState(null);
  const refreshLiveStatus = useCallback(async () => {
    setLiveLoading(true);
    try {
      const [runnerItems, healthItems] = await Promise.all([
        api.runners({ pageNumber: 1, pageSize: 500 }),
        api.runnerHealth().catch(() => [])
      ]);
      const liveItems = enumerationObjects(runnerItems);
      const liveMap = Object.fromEntries(liveItems.map(item => [item.id, item]));
      const healthMap = healthMapFromList(healthItems);
      setServers(current => {
        const currentIds = new Set(current.map(item => item.id));
        const merged = current.map(server => {
          const live = liveMap[server.id];
          return live ? { ...server, ...live, health: live.health || healthMap[server.id] || server.health } : { ...server, health: healthMap[server.id] || server.health };
        });
        liveItems.forEach(item => {
          if (!currentIds.has(item.id)) merged.push({ ...item, health: item.health || healthMap[item.id] });
        });
        return merged;
      });
    } catch (err) {
      setError(String(err.message || err));
    } finally {
      setLiveLoading(false);
    }
  }, [api]);
  const load = useCallback(async () => {
    setLoading(true);
    try {
      setError('');
      const [runnerItems, settingItems, healthItems] = await Promise.all([
        api.runners({ pageNumber: 1, pageSize: 500, includeLiveStatus: false }),
        api.settings(),
        api.runnerHealth().catch(() => [])
      ]);
      const healthMap = healthMapFromList(healthItems);
      setServers(enumerationObjects(runnerItems).map(item => ({ ...item, health: item.health || healthMap[item.id] })));
      setSettings(settingItems);
    } catch (err) {
      setError(String(err.message || err));
    } finally {
      setLoading(false);
    }
    refreshLiveStatus();
  }, [api, refreshLiveStatus]);
  useEffect(() => { load(); }, [load]);

  const totals = useMemo(() => {
    const available = servers.reduce((sum, server) => sum + (server.availableModels || server.models || []).length, 0);
    const loaded = servers.reduce((sum, server) => sum + (server.loadedModels || []).length, 0);
    const monitored = servers.filter(server => server.healthCheckEnabled !== false);
    const healthy = monitored.filter(server => healthField(server.health, 'isHealthy') === true && healthField(server.health, 'lastCheckUtc')).length;
    const unhealthy = monitored.filter(server => healthField(server.health, 'isHealthy') === false && healthField(server.health, 'lastCheckUtc')).length;
    const pending = monitored.length - healthy - unhealthy;
    return { available, loaded, monitored: monitored.length, healthy, unhealthy, pending };
  }, [servers]);

  async function saveServer(server) {
    const next = structuredCloneSafe(settings);
    const runners = next.modelRunners || [];
    const normalized = normalizeRunnerForSave(server);
    const index = runners.findIndex(item => item.id === server.id);
    if (index >= 0) runners[index] = normalized;
    else runners.push(normalized);
    next.modelRunners = runners;
    const updated = await api.settings(next);
    setSettings(updated);
    setServers(current => mergeRunnerSettings(current, updated.modelRunners || []));
    setEditServer(null);
    await load();
  }

  async function removeServer(server) {
    const next = structuredCloneSafe(settings);
    next.modelRunners = (next.modelRunners || []).filter(item => item.id !== server.id);
    await api.settings(next);
    setDeleteServer(null);
    await load();
  }

  return (
    <div className="page model-servers-page">
      <PageIntro title="Model Servers" description="Review configured model runners, see available and loaded models, pull Ollama models, and manage model server settings." actions={
        <div className="page-actions">
          <button className="primary" title="Add a configured model server to Wilson settings" onClick={() => setEditServer(newModelRunner())}><Plus size={16} />Add</button>
          <button className="icon-button" title={loading ? 'Reloading configured model servers and cached health' : 'Reload model servers, cached health, and refresh live model details in the background'} onClick={load} disabled={loading}><RefreshCw size={16} className={loading ? 'spin' : ''} /></button>
        </div>
      } />
      {error && <PermissionPanel message={error} />}
      {liveLoading && <div className="model-live-refresh" title="Wilson is refreshing available and loaded model details without blocking the table"><RefreshCw size={14} className="spin" />Refreshing live model details...</div>}
      <div className="request-summary-grid">
        <MetricCard icon={Server} label="Servers" value={servers.length} />
        <MetricCard icon={Activity} label="Healthy" value={totals.healthy} tone={totals.healthy > 0 ? 'success' : ''} />
        <MetricCard icon={AlertCircle} label="Unhealthy" value={totals.unhealthy} tone={totals.unhealthy > 0 ? 'danger' : ''} />
        <MetricCard icon={Clock} label="Awaiting" value={totals.pending} />
        <MetricCard icon={ListFilter} label="Available models" value={totals.available} />
        <MetricCard icon={Play} label="Loaded models" value={totals.loaded} tone={totals.loaded > 0 ? 'success' : ''} />
      </div>
      <div className="model-server-grid">
        {servers.map(server => <ModelServerCard key={server.id} server={withRunnerSettings(server, settings)} api={api} onPulled={load} onEdit={() => setEditServer(toRunnerSettings(server, settings))} onDelete={() => setDeleteServer(server)} />)}
        {servers.length < 1 && !loading && <div className="empty-card" title="No configured model servers">No model servers are configured.</div>}
      </div>
      {editServer && <ModelServerEditModal server={editServer} onClose={() => setEditServer(null)} onSave={saveServer} />}
      {deleteServer && <ConfirmModal title="Delete Model Server" message={`Delete ${deleteServer.name || deleteServer.id} from Wilson settings?`} onCancel={() => setDeleteServer(null)} onConfirm={() => removeServer(deleteServer)} />}
    </div>
  );
}

function mergeRunnerSettings(servers, runners) {
  return servers.map(server => withRunnerSettings(server, { modelRunners: runners }));
}

function withRunnerSettings(server, settings) {
  const configured = settings?.modelRunners?.find(item => item.id === server.id);
  if (!configured) return server;
  return {
    ...server,
    name: configured.name || server.name,
    apiType: configured.apiType || server.apiType,
    endpoint: configured.endpoint || server.endpoint,
    health: server.health,
    configuredModels: configured.models || server.configuredModels || [],
    contextWindowTokens: configured.contextWindowTokens ?? server.contextWindowTokens,
    healthCheckEnabled: configured.healthCheckEnabled ?? server.healthCheckEnabled,
    healthCheckUrl: configured.healthCheckUrl || server.healthCheckUrl,
    healthCheckMethod: configured.healthCheckMethod || server.healthCheckMethod,
    healthCheckIntervalMs: configured.healthCheckIntervalMs ?? server.healthCheckIntervalMs,
    healthCheckTimeoutMs: configured.healthCheckTimeoutMs ?? server.healthCheckTimeoutMs,
    healthCheckExpectedStatusCode: configured.healthCheckExpectedStatusCode ?? server.healthCheckExpectedStatusCode,
    healthyThreshold: configured.healthyThreshold ?? server.healthyThreshold,
    unhealthyThreshold: configured.unhealthyThreshold ?? server.unhealthyThreshold,
    healthCheckUseAuth: configured.healthCheckUseAuth ?? server.healthCheckUseAuth
  };
}

function toRunnerSettings(server, settings) {
  const configured = settings?.modelRunners?.find(item => item.id === server.id);
  return structuredCloneSafe(configured || normalizeRunnerForSave({ id: server.id, name: server.name, apiType: server.apiType, endpoint: server.endpoint, apiKey: '', models: server.configuredModels || [], contextWindowTokens: server.contextWindowTokens || 8192, ...defaultHealthCheckSettings(server.apiType, server.endpoint) }));
}

function ModelServerCard({ server, api, onPulled, onEdit, onDelete }) {
  const available = server.availableModels || server.models || [];
  const chatModels = Array.isArray(server.chatModels) ? server.chatModels : server.models || [];
  const embeddingModels = server.embeddingModels || [];
  const configured = server.configuredModels || [];
  const loaded = server.loadedModels || [];
  const healthPresentation = modelServerHealthPresentation(server);
  const history = healthHistory(server.health);
  const [pullOpen, setPullOpen] = useState(false);
  const [healthOpen, setHealthOpen] = useState(false);
  return (
    <section className="model-server-card" title={`${server.name || server.id} model server`}>
      <header>
        <div>
          <h2 title="Model server display name">{server.name || server.id}</h2>
          <CopyableId value={server.id} label="Model server ID" />
        </div>
        <button className={`health-status-button ${healthPresentation.tone}`} title={healthPresentation.title} onClick={() => setHealthOpen(true)}><Activity size={14} />{healthPresentation.label}</button>
      </header>
      {server.apiType === 'Ollama' && (
        <div className="model-server-actions">
          <button className="secondary" title={`Request that ${server.name || server.id} pull an Ollama model by name`} onClick={() => setPullOpen(true)}><Download size={16} />Pull model</button>
          <button className="secondary" title={`Update model server ${server.name || server.id}`} onClick={onEdit}><Pencil size={16} />Update</button>
          <button className="danger-button" title={`Delete model server ${server.name || server.id}`} onClick={onDelete}><Trash2 size={16} />Delete</button>
        </div>
      )}
      {server.apiType !== 'Ollama' && (
        <div className="model-server-actions">
          <button className="secondary" title={`Update model server ${server.name || server.id}`} onClick={onEdit}><Pencil size={16} />Update</button>
          <button className="danger-button" title={`Delete model server ${server.name || server.id}`} onClick={onDelete}><Trash2 size={16} />Delete</button>
        </div>
      )}
      <div className="server-facts">
        <DetailField name="apiType" value={server.apiType} />
        <DetailField name="endpoint" value={server.endpoint} />
        <DetailField name="contextWindowTokens" value={server.contextWindowTokens} />
      </div>
      <div className="server-health-line" title="Recent background health checks for this model server">
        <div>
          <label>Health history</label>
          <span>{history.length ? `${history.length} recent check${history.length === 1 ? '' : 's'}` : (server.healthCheckEnabled === false ? 'Disabled' : 'Awaiting check')}</span>
        </div>
        <HealthHistogram history={history} width={150} height={20} />
        <button className="secondary compact-button" title={`Open health details for ${server.name || server.id}`} onClick={() => setHealthOpen(true)}><Activity size={15} />Details</button>
      </div>
      <ModelList title="Configured models" tooltip="Model names explicitly configured in Wilson JSON for this server. Empty means Wilson resolves available models from Ollama when possible." models={configured} empty="No models configured" />
      <ModelList title="Available models" tooltip="Models available from this server. For Ollama, Wilson queries the Ollama API when no models are configured." models={available} empty="No available models reported" />
      <ModelList title="Chat-capable models" tooltip="Models Wilson will show in the Chat model dropdown because they can handle chat or completion requests." models={chatModels} empty="No chat-capable models reported" />
      <ModelList title="Embedding-only models" tooltip="Models hidden from Chat because they appear to support embedding requests only." models={embeddingModels} empty="No embedding-only models detected" />
      <ModelList title="Loaded / running models" tooltip="Models currently loaded or running according to the model server. Ollama reports this from /api/ps." models={loaded} empty={server.apiType === 'Ollama' ? 'No models currently loaded' : 'Loaded model status is not supported for this API type'} loaded />
      {server.statusMessage && server.online === false && <div className="model-server-error" title="Model server status error">{server.statusMessage}</div>}
      {pullOpen && <ModelPullModal server={server} api={api} suggestions={[...available, ...configured]} onClose={() => setPullOpen(false)} onPulled={onPulled} />}
      {healthOpen && <ModelServerHealthModal server={server} api={api} onClose={() => setHealthOpen(false)} />}
    </section>
  );
}

function HealthHistogram({ history, width = 120, height = 24, fill = false }) {
  const records = (history || []).slice(fill ? -96 : -32);
  if (!records.length) return <div className={fill ? 'health-histogram fill empty' : 'health-histogram empty'} style={{ width: fill ? undefined : width, height }} title="No health check history yet" />;
  return (
    <div className={fill ? 'health-histogram fill' : 'health-histogram'} style={{ width: fill ? undefined : width, height }} title="Recent health check history">
      {records.map((record, index) => {
        const success = healthField(record, 'success') === true;
        const timestamp = healthField(record, 'timestampUtc');
        return <span key={`${timestamp || index}-${index}`} className={success ? 'ok' : 'fail'} title={`${success ? 'Healthy' : 'Unhealthy'}${timestamp ? ` at ${formatDate(timestamp)}` : ''}`} />;
      })}
    </div>
  );
}

function ModelServerHealthModal({ server, api, onClose }) {
  const [health, setHealth] = useState(server.health || null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState('');
  const displayServer = { ...server, health };
  const presentation = modelServerHealthPresentation(displayServer);
  const history = healthHistory(health);

  useEffect(() => {
    let active = true;
    async function load() {
      if (server.healthCheckEnabled === false) return;
      setLoading(true);
      setError('');
      try {
        const latest = await api.runnerHealthById(server.id);
        if (active) setHealth(latest);
      } catch (err) {
        if (active) setError(String(err.message || err));
      } finally {
        if (active) setLoading(false);
      }
    }
    load();
    return () => { active = false; };
  }, [api, server.id, server.healthCheckEnabled]);

  return (
    <Modal title={`Health: ${server.name || server.id}`} onClose={onClose} wide>
      <div className="health-modal">
        <div className="health-stats-row">
          <div className="health-stat-card" title={presentation.title}>
            <div className="health-stat-label">Status</div>
            <div className="health-stat-value"><span className={`status-pill ${presentation.tone}`}>{presentation.label}</span></div>
          </div>
          <div className="health-stat-card" title="Uptime percentage since health monitoring started">
            <div className="health-stat-label">Uptime</div>
            <div className="health-stat-value">{health ? `${Number(healthField(health, 'uptimePercentage') || 0).toFixed(2)}%` : '-'}</div>
          </div>
          <div className="health-stat-card" title="Consecutive successful health checks">
            <div className="health-stat-label">Consecutive OK</div>
            <div className="health-stat-value health-stat-success">{healthField(health, 'consecutiveSuccesses') ?? '-'}</div>
          </div>
          <div className="health-stat-card" title="Consecutive failed health checks">
            <div className="health-stat-label">Consecutive Fail</div>
            <div className="health-stat-value health-stat-danger">{healthField(health, 'consecutiveFailures') ?? '-'}</div>
          </div>
        </div>

        {healthField(health, 'lastError') && (
          <div className="health-error-box" title="Most recent health check error">
            <div className="health-error-label">Last Error</div>
            <div className="health-error-message">{healthField(health, 'lastError')}</div>
          </div>
        )}

        <div className="health-histogram-section">
          <div className="health-section-label">Health History</div>
          <HealthHistogram history={history} height={34} fill />
        </div>

        <div className="health-timestamps">
          <div className="health-timestamp-item" title="When monitoring began for this model server"><span className="health-timestamp-label">First check</span><span className="health-timestamp-value">{formatDate(healthField(health, 'firstCheckUtc')) || '-'}</span></div>
          <div className="health-timestamp-item" title="Most recent health probe time"><span className="health-timestamp-label">Last check</span><span className="health-timestamp-value">{formatDate(healthField(health, 'lastCheckUtc')) || '-'}</span></div>
          <div className="health-timestamp-item" title="Most recent healthy transition"><span className="health-timestamp-label">Last healthy</span><span className="health-timestamp-value">{formatDate(healthField(health, 'lastHealthyUtc')) || '-'}</span></div>
          <div className="health-timestamp-item" title="Most recent unhealthy transition"><span className="health-timestamp-label">Last unhealthy</span><span className="health-timestamp-value">{formatDate(healthField(health, 'lastUnhealthyUtc')) || '-'}</span></div>
        </div>

        <div className="health-config-table">
          <KeyValueTable rows={[
            ['healthCheckEnabled', server.healthCheckEnabled !== false],
            ['healthCheckUrl', server.healthCheckUrl],
            ['healthCheckMethod', server.healthCheckMethod],
            ['healthCheckIntervalMs', server.healthCheckIntervalMs],
            ['healthCheckTimeoutMs', server.healthCheckTimeoutMs],
            ['healthCheckExpectedStatusCode', server.healthCheckExpectedStatusCode],
            ['healthyThreshold', server.healthyThreshold],
            ['unhealthyThreshold', server.unhealthyThreshold],
            ['healthCheckUseAuth', server.healthCheckUseAuth]
          ]} />
        </div>

        {loading && <p className="model-empty" title="Health details loading">Loading health details...</p>}
        {error && <PermissionPanel message={error} />}
      </div>
    </Modal>
  );
}

function ModelPullModal({ server, api, suggestions, onClose, onPulled }) {
  const uniqueSuggestions = useMemo(() => [...new Set((suggestions || []).filter(Boolean))].sort((a, b) => a.localeCompare(b)), [suggestions]);
  const [model, setModel] = useState('');
  const [error, setError] = useState('');
  const [result, setResult] = useState(null);
  const [pulling, setPulling] = useState(false);

  async function pull() {
    if (!model.trim()) {
      setError('Model name is required.');
      return;
    }
    setPulling(true);
    setError('');
    setResult(null);
    try {
      const response = await api.pullModel(server.id, model.trim());
      setResult(response);
      await onPulled();
    } catch (err) {
      setError(String(err.message || err));
    } finally {
      setPulling(false);
    }
  }

  return (
    <Modal title={`Pull Ollama Model: ${server.name || server.id}`} onClose={onClose}>
      <div className="model-pull-form">
        <p title="Ollama pulls may take several minutes for large models">Request a model pull from this Ollama server. Large models can take several minutes to download.</p>
        <FormInput label="Model name" tooltip="Ollama model tag to pull, for example llama3.2 or mistral:7b." value={model} onChange={setModel} />
        {uniqueSuggestions.length > 0 && (
          <div className="model-suggestions" title="Known model names from this server and Wilson configuration">
            <label>Known models</label>
            <div className="model-chip-list">
              {uniqueSuggestions.map(item => <button key={item} className="model-chip model-chip-button" title={`Use ${item} as the model name to pull`} onClick={() => setModel(item)}>{item}</button>)}
            </div>
          </div>
        )}
        {result && <div className="success-panel" title="Model pull request completed"><strong>{result.model}</strong>: {result.status || 'Pull request completed.'}</div>}
        {(pulling || result) && <div className={result ? 'progress-bar complete' : 'progress-bar active'} title={result ? 'Model pull completed' : 'Model pull in progress'}><span /></div>}
        {error && <PermissionPanel message={error} />}
      </div>
      <div className="modal-actions">
        <button className="secondary" title="Close without issuing another model pull request" onClick={onClose}>Cancel</button>
        <button className="primary" title={`Ask ${server.name || server.id} to pull the specified Ollama model`} onClick={pull} disabled={pulling || !model.trim()}><Download size={16} />{pulling ? 'Pulling' : 'Pull Model'}</button>
      </div>
    </Modal>
  );
}

function ModelServerEditModal({ server, onClose, onSave }) {
  const [draft, setDraft] = useState(normalizeRunnerForSave(structuredCloneSafe(server)));
  const [error, setError] = useState('');
  const set = (key, value) => setDraft(prev => ({ ...prev, [key]: value }));
  async function save() {
    if (!draft.id.trim() || !draft.name.trim() || !draft.endpoint.trim()) {
      setError('ID, name, and endpoint are required.');
      return;
    }
    try {
      setError('');
      await onSave(normalizeRunnerForSave(draft));
    } catch (err) {
      setError(String(err.message || err));
    }
  }
  return (
    <Modal title="Model Server" onClose={onClose} wide>
      <div className="form-grid model-server-editor">
        <FormInput label="Server ID" tooltip="Unique model server identifier used by chat requests and settings" value={draft.id} onChange={v => set('id', v)} />
        <FormInput label="Display name" tooltip="Human-readable model server name shown in the dashboard" value={draft.name} onChange={v => set('name', v)} />
        <FormSelect label="API type" tooltip="Model server API compatibility" value={draft.apiType || 'Ollama'} options={['Ollama', 'OpenAI', 'OpenAICompatible']} onChange={v => set('apiType', v)} />
        <FormInput label="Endpoint" tooltip="Base URL for this model server" value={draft.endpoint} onChange={v => set('endpoint', v)} />
        <FormInput label="API key" tooltip="Optional API key sent to this model server" value={draft.apiKey || ''} onChange={v => set('apiKey', v)} />
        <FormInput label="Context window tokens" tooltip="Maximum context window used for prompt truncation" type="number" value={draft.contextWindowTokens || 8192} onChange={v => set('contextWindowTokens', v)} />
        <div className="health-editor-block">
          <div className="runner-editor-header"><strong>Health Checks</strong></div>
          <FormCheck label="Enabled" tooltip="Run periodic background health probes for this model server" checked={draft.healthCheckEnabled !== false} onChange={v => set('healthCheckEnabled', v)} />
          <FormInput label="Health URL" tooltip="Absolute URL or path to probe. Ollama defaults to /api/tags; OpenAI-compatible defaults to /v1/models." value={draft.healthCheckUrl || ''} onChange={v => set('healthCheckUrl', v)} />
          <FormSelect label="Method" tooltip="HTTP method used for health probes" value={draft.healthCheckMethod || 'GET'} options={['GET', 'HEAD']} onChange={v => set('healthCheckMethod', v)} />
          <FormInput label="Expected status" tooltip="HTTP status code that marks the probe as successful" type="number" value={draft.healthCheckExpectedStatusCode ?? 200} onChange={v => set('healthCheckExpectedStatusCode', v)} />
          <FormInput label="Interval (ms)" tooltip="Milliseconds between health probes" type="number" value={draft.healthCheckIntervalMs ?? 5000} onChange={v => set('healthCheckIntervalMs', v)} />
          <FormInput label="Timeout (ms)" tooltip="Milliseconds to wait before a probe fails" type="number" value={draft.healthCheckTimeoutMs ?? 2000} onChange={v => set('healthCheckTimeoutMs', v)} />
          <FormInput label="Healthy threshold" tooltip="Consecutive successful probes required to mark healthy" type="number" value={draft.healthyThreshold ?? 2} onChange={v => set('healthyThreshold', v)} />
          <FormInput label="Unhealthy threshold" tooltip="Consecutive failed probes required to mark unhealthy" type="number" value={draft.unhealthyThreshold ?? 2} onChange={v => set('unhealthyThreshold', v)} />
          <FormCheck label="Use API key" tooltip="Send this model server API key with health probes" checked={!!draft.healthCheckUseAuth} onChange={v => set('healthCheckUseAuth', v)} />
        </div>
        <StringListRows label="Configured models" tooltip="Known model names, one per row. Leave empty for Ollama to query available models." value={draft.models || []} onChange={v => set('models', v)} />
      </div>
      {error && <div className="error" title="Model server validation error">{error}</div>}
      <div className="modal-actions">
        <button className="secondary" title="Close without saving this model server" onClick={onClose}>Cancel</button>
        <button className="primary" title="Save this model server to Wilson settings" onClick={save}><Save size={16} />Save</button>
      </div>
    </Modal>
  );
}

function ModelList({ title, tooltip, models, empty, loaded = false }) {
  return (
    <div className="model-list-block" title={tooltip}>
      <h3>{title}</h3>
      <div className="model-chip-list">
        {models.map(model => <span key={model} className={loaded ? 'model-chip loaded' : 'model-chip'} title={`${title}: ${model}`}>{model}</span>)}
        {models.length < 1 && <span className="model-empty" title={empty}>{empty}</span>}
      </div>
    </div>
  );
}

function FeedbackCommentModal({ draft, onChange, onClose, onSave }) {
  const positive = draft.rating > 0;
  return (
    <Modal title={positive ? 'Helpful Response Feedback' : 'Not Helpful Response Feedback'} onClose={onClose}>
      <div className="feedback-comment-form">
        <div className={positive ? 'feedback-tone positive' : 'feedback-tone negative'} title={positive ? 'This feedback will be saved as helpful' : 'This feedback will be saved as not helpful'}>
          {positive ? <ThumbsUp size={18} /> : <ThumbsDown size={18} />}
          <span>{positive ? 'Helpful' : 'Not helpful'}</span>
        </div>
        <label title="Optional free-form feedback about this assistant response">
          Your opinion
          <textarea
            title="Optionally describe what was useful, missing, wrong, or should be improved"
            value={draft.comment}
            onChange={e => onChange({ ...draft, comment: e.target.value })}
            placeholder="Tell us what worked, what was wrong, or what should change."
            autoFocus
          />
        </label>
      </div>
      <div className="modal-actions">
        <button className="secondary" title="Close without saving this feedback" onClick={onClose}>Cancel</button>
        <button className="primary" title="Save this rating and optional written feedback" onClick={onSave}><Save size={16} />Save Feedback</button>
      </div>
    </Modal>
  );
}

function HistoryView({ api }) {
  const [items, setItems] = useState([]);
  const [enumeration, setEnumeration] = useState(null);
  const [page, setPage] = useState(0);
  const [pageSize, setPageSize] = useState(25);
  const [modal, setModal] = useState(null);
  const load = useCallback(async () => { const result = await api.conversations({ pageNumber: page + 1, pageSize }); setEnumeration(result); setItems(enumerationObjects(result)); }, [api, page, pageSize]);
  useEffect(() => { load(); }, [load]);
  return (
    <div className="page">
      <PageIntro title={text.history} description="Review saved chat conversations, inspect conversation metadata, rename conversations, and remove old message history." />
      <DataTable rows={items} enumeration={enumeration} page={page} pageSize={pageSize} setPage={setPage} setPageSize={setPageSize} columns={['id', 'title', 'runnerId', 'model', 'lastUpdateUtc']} onRefresh={load} onRowClick={(row) => setModal({ type: 'view', row })} actions={(row) => [
        { label: text.view, tooltip: 'View structured conversation details', icon: Eye, onClick: () => setModal({ type: 'view', row }) },
        { label: text.viewJson, tooltip: 'View raw conversation JSON', icon: Code, onClick: () => setModal({ type: 'json', row }) },
        { label: text.edit, tooltip: 'Rename this conversation', icon: Pencil, onClick: () => setModal({ type: 'edit', row }) },
        { label: text.delete, tooltip: 'Delete this conversation and its messages', icon: Trash2, danger: true, onClick: () => setModal({ type: 'delete', row }) }
      ]} />
      {modal?.type === 'view' && <RecordModal title="Conversation" row={modal.row} onClose={() => setModal(null)} />}
      {modal?.type === 'json' && <JsonModal title="Conversation JSON" row={modal.row} onClose={() => setModal(null)} />}
      {modal?.type === 'edit' && <RenameConversationModal conversation={modal.row} onClose={() => setModal(null)} onSave={async title => { await api.updateConversation(modal.row.id, { ...modal.row, title }); setModal(null); load(); }} />}
      {modal?.type === 'delete' && <ConfirmModal title="Delete Conversation" message={`Delete "${modal.row.title}" and its message history?`} onCancel={() => setModal(null)} onConfirm={async () => { await api.deleteConversation(modal.row.id); setModal(null); load(); }} />}
    </div>
  );
}

function RequestHistory({ api }) {
  const [rows, setRows] = useState([]);
  const [enumeration, setEnumeration] = useState(null);
  const [summary, setSummary] = useState(null);
  const [range, setRange] = useState('day');
  const [method, setMethod] = useState('all');
  const [outcome, setOutcome] = useState('all');
  const [query, setQuery] = useState('');
  const [page, setPage] = useState(0);
  const [pageSize, setPageSize] = useState(25);
  const [loading, setLoading] = useState(false);
  const [modal, setModal] = useState(null);
  const ranges = {
    hour: { label: 'Last Hour', ms: 60 * 60 * 1000, bucketMinutes: 1, samples: 60 },
    day: { label: 'Last Day', ms: 24 * 60 * 60 * 1000, bucketMinutes: 15, samples: 96 },
    week: { label: 'Last Week', ms: 7 * 24 * 60 * 60 * 1000, bucketMinutes: 120, samples: 84 },
    month: { label: 'Last Month', ms: 30 * 24 * 60 * 60 * 1000, bucketMinutes: 480, samples: 90 }
  };
  const load = useCallback(async () => {
    setLoading(true);
    const now = new Date();
    const selected = ranges[range];
    const from = new Date(now.getTime() - selected.ms);
    try {
      const result = await api.requestHistory({ pageNumber: page + 1, pageSize });
      setEnumeration(result);
      setRows(enumerationObjects(result));
      const nextSummary = await api.requestSummary({ fromUtc: from.toISOString(), toUtc: now.toISOString(), bucketMinutes: selected.bucketMinutes });
      setSummary({ ...nextSummary, rangeStartUtc: from.toISOString(), rangeEndUtc: now.toISOString(), expectedSamples: selected.samples });
    } finally {
      setLoading(false);
    }
  }, [api, range, page, pageSize]);
  useEffect(() => { load(); }, [load]);

  const filteredRows = useMemo(() => {
    const needle = query.trim().toLowerCase();
    return rows.filter(row => {
      if (method !== 'all' && row.method !== method) return false;
      if (outcome === 'success' && row.statusCode >= 400) return false;
      if (outcome === 'error' && row.statusCode < 400) return false;
      if (needle && !`${row.method} ${row.path} ${row.statusCode} ${row.id}`.toLowerCase().includes(needle)) return false;
      return true;
    });
  }, [rows, method, outcome, query]);

  const stats = useMemo(() => {
    const total = filteredRows.length;
    const failures = filteredRows.filter(row => row.statusCode >= 400).length;
    const avg = total ? filteredRows.reduce((sum, row) => sum + Number(row.durationMs || 0), 0) / total : 0;
    return { total, successes: total - failures, failures, avg, slowest: filteredRows.reduce((max, row) => Math.max(max, Number(row.durationMs || 0)), 0) };
  }, [filteredRows]);

  const methods = ['all', ...Array.from(new Set(rows.map(row => row.method).filter(Boolean))).sort()];

  return (
    <div className="page request-history-page">
      <PageIntro title={text.requests} description="Inspect recent API activity, latency, status trends, request and response payloads, and captured model timing metadata." />
      <div className="request-summary-grid">
        <MetricCard icon={ListFilter} label="Requests" value={stats.total} />
        <MetricCard icon={Check} label="Success" value={stats.successes} tone="success" />
        <MetricCard icon={AlertCircle} label="Errors" value={stats.failures} tone="danger" />
        <MetricCard icon={Gauge} label="Avg latency" value={formatDuration(stats.avg)} />
        <MetricCard icon={Clock} label="Slowest" value={formatDuration(stats.slowest)} />
      </div>
      <div className="history-panel">
        <div className="history-toolbar">
          <Segmented value={range} options={ranges} onChange={setRange} />
          <label className="toolbar-field" title="Filter request history by HTTP method">Method<select title="Filter by HTTP method" value={method} onChange={e => setMethod(e.target.value)}>{methods.map(item => <option key={item} value={item}>{item === 'all' ? 'All methods' : item}</option>)}</select></label>
          <label className="toolbar-field" title="Filter request history by status outcome">Status<select title="Filter by success or error status" value={outcome} onChange={e => setOutcome(e.target.value)}><option value="all">All statuses</option><option value="success">Success</option><option value="error">Errors</option></select></label>
          <label className="toolbar-search" title="Search request history by path, status, or ID"><Search size={16} /><input title="Search request history by path, status, or ID" value={query} onChange={e => setQuery(e.target.value)} placeholder="Search path, status, or ID" /></label>
        </div>
        {summary && <ActivityChart summary={summary} range={range} />}
      </div>
      <RequestHistoryTable rows={filteredRows} enumeration={enumeration} page={page} pageSize={pageSize} setPage={setPage} setPageSize={setPageSize} onRefresh={load} loading={loading} onView={(row) => setModal({ type: 'view', row })} onJson={(row) => setModal({ type: 'json', row })} onDelete={(row) => setModal({ type: 'delete', row })} />
      {modal?.type === 'view' && <RequestHistoryDetailModal row={modal.row} onClose={() => setModal(null)} />}
      {modal?.type === 'json' && <JsonModal title="Request History Entry JSON" row={modal.row} onClose={() => setModal(null)} />}
      {modal?.type === 'delete' && <ConfirmModal title="Delete request history entry" message="Delete this request history entry?" onCancel={() => setModal(null)} onConfirm={async () => { await api.deleteRequestHistory(modal.row.id); setModal(null); load(); }} />}
    </div>
  );
}

function ActivityChart({ summary, range }) {
  const buckets = normalizeChartBuckets(summary);
  const [tooltip, setTooltip] = useState(null);
  const max = Math.max(1, ...buckets.map(b => b.successCount + b.failureCount));
  const yTicks = [3, 2, 1, 0].map(index => Math.round((max / 3) * index));
  const xTicks = chartTicks(buckets, range);
  const bucketTooltip = (bucket) => ({
    timestamp: formatDate(bucket.bucketStartUtc),
    successes: bucket.successCount || 0,
    failures: bucket.failureCount || 0
  });
  function showTooltip(event, bucket) {
    const rect = event.currentTarget.getBoundingClientRect();
    setTooltip({ ...bucketTooltip(bucket), left: rect.left + rect.width / 2, top: rect.top });
  }
  return (
    <div className="chart-shell" title="Time-bucketed request volume. Green is success; red is failure.">
      <div className="chart-y-axis">
        {yTicks.map((tick, index) => <span key={`${tick}-${index}`}>{tick}</span>)}
      </div>
      <div className="chart-area">
        <div className="chart" onMouseLeave={() => setTooltip(null)}>
          {buckets.map((b, i) => {
            const tip = bucketTooltip(b);
            const label = `Timestamp: ${tip.timestamp}. Successful requests: ${tip.successes}. Failed requests: ${tip.failures}.`;
            return (
              <div
                key={i}
                className="bar"
                title={label}
                aria-label={label}
                tabIndex="0"
                onMouseEnter={event => showTooltip(event, b)}
                onMouseMove={event => showTooltip(event, b)}
                onFocus={event => showTooltip(event, b)}
                onBlur={() => setTooltip(null)}
              >
                <span className="ok" style={{ height: `${(b.successCount / max) * 100}%` }} />
                <span className="fail" style={{ height: `${(b.failureCount / max) * 100}%` }} />
              </div>
            );
          })}
          {buckets.length < 1 && <div className="empty-chart">No activity in this range</div>}
        </div>
        {tooltip && (
          <div className="chart-tooltip" style={{ left: tooltip.left, top: tooltip.top }} role="tooltip">
            <div><strong>Timestamp</strong><span>{tooltip.timestamp}</span></div>
            <div><strong>Successful requests</strong><span>{tooltip.successes}</span></div>
            <div><strong>Failed requests</strong><span>{tooltip.failures}</span></div>
          </div>
        )}
        <div className="chart-x-axis">
          {xTicks.map((tick, i) => <span key={`${tick.value}-${i}`}>{tick.label}</span>)}
        </div>
      </div>
    </div>
  );
}

function normalizeChartBuckets(summary) {
  const buckets = summary?.buckets || [];
  const expectedSamples = Number(summary?.expectedSamples || 0);
  const start = new Date(summary?.rangeStartUtc || buckets[0]?.bucketStartUtc || '');
  const end = new Date(summary?.rangeEndUtc || buckets[buckets.length - 1]?.bucketEndUtc || '');
  if (!expectedSamples || Number.isNaN(start.getTime()) || Number.isNaN(end.getTime()) || end <= start) return buckets;

  const stepMs = (end.getTime() - start.getTime()) / expectedSamples;
  return Array.from({ length: expectedSamples }, (_, index) => {
    const bucketStart = new Date(start.getTime() + stepMs * index);
    const bucketEnd = new Date(start.getTime() + stepMs * (index + 1));
    const source = buckets.find(item => {
      const sourceStart = new Date(item.bucketStartUtc);
      return !Number.isNaN(sourceStart.getTime()) && sourceStart >= bucketStart && sourceStart < bucketEnd;
    });
    return source || {
      bucketStartUtc: bucketStart.toISOString(),
      bucketEndUtc: bucketEnd.toISOString(),
      successCount: 0,
      failureCount: 0,
      averageDurationMs: 0
    };
  });
}

function chartTicks(buckets, range) {
  if (!buckets.length) return [];
  const start = new Date(buckets[0].bucketStartUtc);
  const end = new Date(buckets[buckets.length - 1].bucketEndUtc || buckets[buckets.length - 1].bucketStartUtc);
  if (Number.isNaN(start.getTime()) || Number.isNaN(end.getTime()) || end <= start) return [];
  const count = Math.min(8, buckets.length + 1);
  return Array.from({ length: count }, (_, index) => {
    const time = new Date(start.getTime() + ((end.getTime() - start.getTime()) * index) / (count - 1));
    return { value: time.toISOString(), label: formatChartTick(time, range) };
  });
}

function formatChartTick(date, range) {
  if (range === 'week') return date.toLocaleString([], { month: 'numeric', day: 'numeric', hour: 'numeric' });
  if (range === 'month') return date.toLocaleDateString([], { month: 'numeric', day: 'numeric' });
  return date.toLocaleTimeString([], { hour: 'numeric', minute: '2-digit' });
}

function MetricCard({ icon: Icon, label, value, tone = '' }) {
  return <div className={`metric-card ${tone}`} title={`${label}: ${value}`}><Icon size={18} /><span>{label}</span><strong>{value}</strong></div>;
}

function RequestHistoryTable({ rows, enumeration, page, pageSize, setPage, setPageSize, onRefresh, loading, onView, onJson, onDelete }) {
  const total = enumeration?.totalRecords ?? rows.length;
  const totalPages = Math.max(1, enumeration?.totalPages || Math.ceil(rows.length / pageSize));
  const safePage = Math.min(page, totalPages - 1);
  const pageRows = rows;
  useEffect(() => { if (page !== safePage) setPage(safePage); }, [page, safePage]);
  return (
    <div className="operator-table request-table">
      <Pagination total={total} totalPagesOverride={totalPages} page={safePage} pageSize={pageSize} setPage={setPage} setPageSize={setPageSize} onRefresh={onRefresh} refreshing={loading} refreshTitle={loading ? 'Reloading request history and chart data' : 'Reload request history and chart data'} />
      <div className="table-wrap">
        <table>
          <thead><tr>{['method', 'path', 'statusCode', 'durationMs', 'createdUtc'].map(c => <HeaderCell key={c} column={c} />)}<th className="actions-col" title="Available row actions">Actions</th></tr></thead>
          <tbody>
            {pageRows.map(row => (
              <tr key={row.id} className="clickable" onClick={() => onView(row)} title="Open request history detail">
                <td><span className={`method-pill method-${String(row.method).toLowerCase()}`}>{row.method}</span></td>
                <td><div className="path-cell">{row.path}</div><CopyableId value={row.id} label="Request ID" /></td>
                <td><span className={`status-pill ${statusClass(row.statusCode)}`}>{row.statusCode}</span></td>
                <td><LatencyCell value={row.durationMs} rows={rows} /></td>
                <td>{formatDate(row.createdUtc)}</td>
                <td className="actions-col" onClick={(event) => event.stopPropagation()}><ActionMenu items={[{ label: text.view, tooltip: 'Open request detail', icon: Eye, onClick: () => onView(row) }, { label: text.viewJson, tooltip: 'Open raw request history JSON', icon: Code, onClick: () => onJson(row) }, { label: text.delete, tooltip: 'Delete this request history entry', icon: Trash2, danger: true, onClick: () => onDelete(row) }]} /></td>
              </tr>
            ))}
            {pageRows.length < 1 && <tr><td colSpan="6" className="empty-cell">No request history matches the selected filters</td></tr>}
          </tbody>
        </table>
      </div>
    </div>
  );
}

function LatencyCell({ value, rows }) {
  const max = Math.max(1, ...rows.map(row => Number(row.durationMs || 0)));
  return <div className="latency-cell" title={`Request latency ${formatDuration(value)}`}><span>{formatDuration(value)}</span><div><i style={{ width: `${Math.max(4, (Number(value || 0) / max) * 100)}%` }} /></div></div>;
}

function RequestHistoryDetailModal({ row, onClose }) {
  return (
    <Modal title="Request History Detail" onClose={onClose} wide full>
      <div className="detail-content request-history-detail">
        <div className="detail-section">
          <div className="detail-header-row">
            <div className="detail-id" title="Unique request history entry ID">
              <label>ID:</label>
              <CopyableId value={row.id} label="Request ID" />
            </div>
            <span className={`http-status ${statusClass(row.statusCode)}`} title={`HTTP status ${row.statusCode}`}>{row.statusCode || '-'}</span>
          </div>
          <div className="endpoint-display" title={`${row.method} ${row.path}`}>
            <span className={`method-badge ${String(row.method).toLowerCase()}`}>{row.method}</span>
            <code className="endpoint-url">{row.path}</code>
            <CopyButton value={row.path} titleText="Copy request path" />
          </div>
        </div>

        <div className="detail-section">
          <h3>Timing</h3>
          <table className="timing-table">
            <tbody>
              <tr><th title="UTC timestamp when this request was captured">Created</th><td>{formatDate(row.createdUtc)}</td></tr>
              <tr><th title="Total request duration in milliseconds">Response Time</th><td>{formatDuration(row.durationMs)}</td></tr>
              <tr><th title="Milliseconds until the first token was received for chat requests">Time to first token (ms)</th><td>{formatNumber(row.timeToFirstTokenMs)}</td></tr>
              <tr><th title="Milliseconds spent receiving streamed tokens">Streaming time (ms)</th><td>{formatNumber(row.streamingTimeMs)}</td></tr>
              <tr><th title="Total model inference time for chat requests">Total time (ms)</th><td>{formatNumber(row.totalTimeMs)}</td></tr>
              <tr><th title="Estimated tokens used by this request">Tokens used</th><td>{row.tokensUsed || 0}</td></tr>
              <tr><th title="HTTP method used by this request">Method</th><td><span className={`method-badge ${String(row.method).toLowerCase()}`}>{row.method}</span></td></tr>
              <tr><th title="HTTP response status code">HTTP Status</th><td><span className={`http-status ${statusClass(row.statusCode)}`}>{row.statusCode || '-'}</span></td></tr>
            </tbody>
          </table>
        </div>

        <div className="detail-section">
          <h3>Principal</h3>
          <KeyValueTable rows={[
            ['tenantId', row.tenantId],
            ['userId', row.userId]
          ]} />
        </div>

        <CollapsiblePayload title="Request Headers" value={row.requestHeaders} />
        <CollapsiblePayload title="Request Body" value={row.requestBody} />
        <CollapsiblePayload title="Response Headers" value={row.responseHeaders} />
        <CollapsiblePayload title="Response Body" value={row.responseBody} />

        <div className="detail-section">
          <h3>Record</h3>
          <div className="detail-item full-width">
            <label title="Raw request history JSON captured by Wilson">Entry JSON:</label>
            <pre className="code-block" title="Raw request history JSON">{JSON.stringify(row, null, 2)}</pre>
          </div>
        </div>
      </div>
    </Modal>
  );
}

function CollapsiblePayload({ title, value }) {
  const [open, setOpen] = useState(false);
  const [pretty, setPretty] = useState(true);
  const formatted = pretty ? prettyValue(value) : String(value || '');
  return (
    <div className="detail-section payload-section">
      <button className="payload-toggle" title={`Expand or collapse ${title}`} onClick={() => setOpen(!open)}>{open ? <ChevronDown size={16} /> : <ChevronRight size={16} />}{title}</button>
      {open && (
        <div className="payload-body">
          <div className="payload-actions">
            <CopyButton value={formatted} titleText={`Copy ${title}`} />
            <button className="secondary" title={`Toggle prettified JSON display for ${title}`} onClick={() => setPretty(!pretty)}>{pretty ? 'Raw' : 'Prettify JSON'}</button>
          </div>
          <pre className="code-block" title={title}>{formatted || 'No data captured'}</pre>
        </div>
      )}
    </div>
  );
}

function CopyButton({ value, titleText }) {
  const [copied, setCopied] = useState(false);
  async function copy(event) {
    event.stopPropagation();
    try {
      if (navigator.clipboard && window.isSecureContext) await navigator.clipboard.writeText(String(value || ''));
      else {
        const area = document.createElement('textarea');
        area.value = String(value || '');
        area.setAttribute('readonly', '');
        area.style.position = 'fixed';
        area.style.left = '-9999px';
        document.body.appendChild(area);
        area.select();
        document.execCommand('copy');
        document.body.removeChild(area);
      }
      setCopied(true);
      setTimeout(() => setCopied(false), 1200);
    } catch {
      window.prompt(titleText || 'Copy value', String(value || ''));
    }
  }
  return <button className="copy-btn" title={copied ? 'Copied' : titleText} onClick={copy}><Copy size={14} />{copied ? 'Copied' : ''}</button>;
}

function ApiExplorer({ api }) {
  const [spec, setSpec] = useState(null);
  const [operationKey, setOperationKey] = useState('');
  const [body, setBody] = useState('');
  const [pathValues, setPathValues] = useState({});
  const [response, setResponse] = useState('');
  const [confirmRun, setConfirmRun] = useState(false);
  useEffect(() => {
    api.openApi().then(data => {
      setSpec(data);
      const operations = getOpenApiOperations(data);
      if (operations[0]) {
        setOperationKey(operations[0].key);
        setBody(sampleRequestBody(operations[0]));
      }
    });
  }, [api]);
  const operations = useMemo(() => getOpenApiOperations(spec), [spec]);
  const operation = operations.find(item => item.key === operationKey) || operations[0];
  const pathParams = useMemo(() => [...(operation?.path.matchAll(/\{([^}]+)\}/g) || [])].map(match => match[1]), [operation]);
  async function execute() {
    if (!operation) return;
    if ((operation.method === 'DELETE' || operation.path.includes('bulk')) && !confirmRun) { setConfirmRun(true); return; }
    setConfirmRun(false);
    const path = pathParams.reduce((current, name) => current.replace(`{${name}}`, encodeURIComponent(pathValues[name] || '')), operation.path);
    const res = await api.raw(operation.method, path, body);
    setResponse(`${res.status}\n${await res.text()}`);
  }
  return (
    <div className="page explorer">
      <PageIntro title={text.explorer} description="Explore named Wilson APIs, fill path parameters, run sample requests, and open the generated OpenAPI or Swagger documentation." actions={
        <div className="page-actions">
          <a className="secondary" title="Open the OpenAPI JSON document" href={`${api.baseUrl}/openapi.json`} target="_blank" rel="noreferrer"><Code size={16} />OpenAPI JSON</a>
          <a className="secondary" title="Open Swagger UI for this Wilson server" href={`${api.baseUrl}/swagger`} target="_blank" rel="noreferrer"><Play size={16} />Swagger</a>
        </div>
      } />
      <div className="explorer-grid">
        <div>
          <label title="Choose an API operation by its documented name">API<select title="Choose an API operation by its documented name" value={operation?.key || ''} onChange={e => { const next = operations.find(item => item.key === e.target.value); setOperationKey(e.target.value); setBody(sampleRequestBody(next)); setPathValues({}); setResponse(''); }}>{operations.map(item => <option key={item.key} value={item.key}>{item.name}</option>)}</select></label>
          {operation && <div className="operation-summary" title={`${operation.method} ${operation.path}`}><span className={`method-pill method-${operation.method.toLowerCase()}`}>{operation.method}</span><code>{operation.path}</code></div>}
          {pathParams.length > 0 && <div className="path-param-grid">{pathParams.map(name => <FormInput key={name} label={humanize(name)} tooltip={`Value to substitute into URL parameter {${name}}`} value={pathValues[name] || ''} onChange={v => setPathValues(prev => ({ ...prev, [name]: v }))} />)}</div>}
          <label title="Request body JSON for this API operation">Sample request body<textarea title="Request body JSON for this API operation" value={body} onChange={e => setBody(e.target.value)} placeholder={operation?.method === 'GET' || operation?.method === 'DELETE' ? 'No request body required for most read/delete APIs' : '{}'} /></label>
          <button className="primary" title={operation ? `Execute ${operation.name}` : 'Select an API operation first'} onClick={execute} disabled={!operation}><Play size={16} />Execute</button>
        </div>
        <pre title="API response">{response}</pre>
      </div>
      {confirmRun && <ConfirmModal title="Run destructive request" message="This request can delete data. Execute it now?" onCancel={() => setConfirmRun(false)} onConfirm={execute} />}
    </div>
  );
}

function getOpenApiOperations(spec) {
  if (!spec?.paths) return [];
  const methods = ['get', 'post', 'put', 'delete', 'patch'];
  return Object.entries(spec.paths).flatMap(([path, config]) => methods
    .filter(method => config?.[method])
    .map(method => {
      const operation = config[method];
      return {
        key: `${method.toUpperCase()} ${path}`,
        method: method.toUpperCase(),
        path,
        name: operation.summary || `${method.toUpperCase()} ${path}`
      };
    }))
    .sort((a, b) => a.name.localeCompare(b.name));
}

function sampleRequestBody(operation) {
  if (!operation || operation.method === 'GET' || operation.method === 'DELETE') return '';
  if (operation.path.includes('/auth/token')) return JSON.stringify({ accessKey: 'wilsonadmin' }, null, 2);
  if (operation.path.includes('/chat')) return JSON.stringify({ conversationId: null, runnerId: 'ollama-local', model: 'llama3.2', prompt: 'Hello' }, null, 2);
  if (operation.path.includes('/model-runners') && operation.path.includes('/pull')) return JSON.stringify({ model: 'llama3.2' }, null, 2);
  if (operation.path.includes('/tenants')) return JSON.stringify({ name: 'Example tenant', active: true }, null, 2);
  if (operation.path.includes('/users')) return JSON.stringify({ tenantId: 'tenant-id', email: 'user@example.com', firstName: 'Example', lastName: 'User', isAdmin: false, isTenantAdmin: false, active: true }, null, 2);
  if (operation.path.includes('/credentials')) return JSON.stringify({ tenantId: 'tenant-id', userId: 'user-id', name: 'Example credential', active: true }, null, 2);
  if (operation.path.includes('/feedback')) return JSON.stringify({ conversationId: 'conversation-id', messageId: 'message-id', rating: 1, comment: 'Useful answer.' }, null, 2);
  return '{}';
}

function TenantAdmin({ api }) {
  const [rows, setRows] = useState([]); const [name, setName] = useState(''); const [modal, setModal] = useState(null); const [error, setError] = useState(''); const [enumeration, setEnumeration] = useState(null); const [page, setPage] = useState(0); const [pageSize, setPageSize] = useState(25);
  const load = useCallback(async () => { try { setError(''); const result = await api.tenants({ pageNumber: page + 1, pageSize }); setEnumeration(result); setRows(enumerationObjects(result)); } catch (err) { setError(String(err.message || err)); } }, [api, page, pageSize]);
  useEffect(() => { load(); }, [load]);
  async function create() { await api.createTenant({ name, active: true }); setName(''); load(); }
  return <div className="page"><PageIntro title={text.tenants} description="Create and manage tenant records, review tenant status, and update tenant metadata." />{error && <PermissionPanel message={error} />}<div className="create-row"><input title="Tenant name for the new tenant" value={name} onChange={e => setName(e.target.value)} placeholder="Tenant name" /><button className="primary" title="Create a new tenant" onClick={create} disabled={!name.trim()}><Check size={16} />Create</button></div><DataTable rows={rows} enumeration={enumeration} page={page} pageSize={pageSize} setPage={setPage} setPageSize={setPageSize} columns={['id', 'name', 'active', 'createdUtc']} onRefresh={load} onRowClick={(row) => setModal({ type: 'edit', row })} actions={(row) => entityActions(row, setModal)} />{renderEntityModal(modal, setModal, api.updateTenant, api.deleteTenant, load, 'Tenant')}</div>;
}

function UserAdmin({ api }) {
  const [rows, setRows] = useState([]); const [email, setEmail] = useState(''); const [modal, setModal] = useState(null); const [error, setError] = useState(''); const [enumeration, setEnumeration] = useState(null); const [page, setPage] = useState(0); const [pageSize, setPageSize] = useState(25);
  const load = useCallback(async () => { try { setError(''); const result = await api.users('', { pageNumber: page + 1, pageSize }); setEnumeration(result); setRows(enumerationObjects(result)); } catch (err) { setError(String(err.message || err)); } }, [api, page, pageSize]);
  useEffect(() => { load(); }, [load]);
  async function create() { await api.createUser({ email, firstName: 'New', lastName: 'User', isTenantAdmin: false, isAdmin: false, active: true }); setEmail(''); load(); }
  return <div className="page"><PageIntro title={text.users} description="Manage dashboard and API users, including tenant assignment, email identity, administrator access, and active status." />{error && <PermissionPanel message={error} />}<div className="create-row"><input title="Email address for the new user" value={email} onChange={e => setEmail(e.target.value)} placeholder="Email" /><button className="primary" title="Create a new user" onClick={create} disabled={!email.trim()}><Check size={16} />Create</button></div><DataTable rows={rows} enumeration={enumeration} page={page} pageSize={pageSize} setPage={setPage} setPageSize={setPageSize} columns={['tenantId', 'id', 'email', 'firstName', 'lastName', 'isAdmin', 'isTenantAdmin', 'active']} onRefresh={load} onRowClick={(row) => setModal({ type: 'edit', row })} actions={(row) => entityActions(row, setModal)} />{renderEntityModal(modal, setModal, api.updateUser, (id, row) => api.deleteUser(id, row.tenantId), load, 'User')}</div>;
}

function CredentialAdmin({ api }) {
  const [rows, setRows] = useState([]); const [name, setName] = useState(''); const [modal, setModal] = useState(null); const [error, setError] = useState(''); const [enumeration, setEnumeration] = useState(null); const [page, setPage] = useState(0); const [pageSize, setPageSize] = useState(25);
  const load = useCallback(async () => { try { setError(''); const result = await api.credentials('', { pageNumber: page + 1, pageSize }); setEnumeration(result); setRows(enumerationObjects(result)); } catch (err) { setError(String(err.message || err)); } }, [api, page, pageSize]);
  useEffect(() => { load(); }, [load]);
  async function create() { const users = enumerationObjects(await api.users('', { pageNumber: 1, pageSize: 1 })); await api.createCredential({ tenantId: users[0]?.tenantId, userId: users[0]?.id, name }); setName(''); load(); }
  return <div className="page"><PageIntro title={text.credentials} description="Manage bearer credentials used to authenticate users against Wilson, including active state and last-use metadata." />{error && <PermissionPanel message={error} />}<div className="create-row"><input title="Name for the new credential" value={name} onChange={e => setName(e.target.value)} placeholder="Credential name" /><button className="primary" title="Create a credential for the first visible user" onClick={create} disabled={!name.trim()}><Check size={16} />Create</button></div><DataTable rows={rows} enumeration={enumeration} page={page} pageSize={pageSize} setPage={setPage} setPageSize={setPageSize} columns={['tenantId', 'userId', 'id', 'name', 'accessKey', 'active', 'lastUsedUtc']} onRefresh={load} onRowClick={(row) => setModal({ type: 'edit', row })} actions={(row) => entityActions(row, setModal)} />{renderEntityModal(modal, setModal, api.updateCredential, (id, row) => api.deleteCredential(id, row.tenantId), load, 'Credential')}</div>;
}

function FeedbackAdmin({ api }) {
  const [rows, setRows] = useState([]); const [modal, setModal] = useState(null); const [error, setError] = useState(''); const [enumeration, setEnumeration] = useState(null); const [page, setPage] = useState(0); const [pageSize, setPageSize] = useState(25);
  const load = useCallback(async () => { try { setError(''); const result = await api.feedback(null, { pageNumber: page + 1, pageSize }); setEnumeration(result); setRows(enumerationObjects(result)); } catch (err) { setError(String(err.message || err)); } }, [api, page, pageSize]);
  useEffect(() => { load(); }, [load]);
  return <div className="page feedback-page"><PageIntro title={text.feedback} description="Review user ratings and comments on assistant responses, including message timing and token metadata when available." />{error && <PermissionPanel message={error} />}<DataTable rows={rows} enumeration={enumeration} page={page} pageSize={pageSize} setPage={setPage} setPageSize={setPageSize} columns={['id', 'conversationId', 'messageId', 'rating', 'comment', 'createdUtc']} onRefresh={load} onRowClick={(row) => setModal({ type: 'view', row })} actions={(row) => [{ label: text.view, tooltip: 'View feedback details', icon: Eye, onClick: () => setModal({ type: 'view', row }) }, { label: text.viewJson, tooltip: 'View feedback JSON', icon: Code, onClick: () => setModal({ type: 'json', row }) }]} />{modal?.type === 'view' && <FeedbackDetailModal row={modal.row} onClose={() => setModal(null)} />}{modal?.type === 'json' && <JsonModal title="Feedback JSON" row={modal.row} onClose={() => setModal(null)} />}</div>;
}

function FeedbackDetailModal({ row, onClose }) {
  return (
    <Modal title="Feedback Detail" onClose={onClose} wide>
      <div className="detail-content">
        <div className="feedback-detail-header">
          <div className={row.rating > 0 ? 'feedback-tone positive' : 'feedback-tone negative'} title="Submitted user rating">
            {row.rating > 0 ? <ThumbsUp size={18} /> : <ThumbsDown size={18} />}
            <span>{row.rating > 0 ? 'Helpful' : 'Not helpful'}</span>
          </div>
        </div>
        <KeyValueTable rows={[
          ['conversationId', row.conversationId],
          ['messageId', row.messageId],
          ['userId', row.userId || '-'],
          ['createdUtc', row.createdUtc],
          ['timeToFirstTokenMs', formatNumber(row.timeToFirstTokenMs)],
          ['streamingTimeMs', formatNumber(row.streamingTimeMs)],
          ['totalTimeMs', formatNumber(row.totalTimeMs)],
          ['tokensUsed', row.tokensUsed || 0]
        ]} />
        <div className="detail-section">
          <h3>Opinion</h3>
          <pre className="feedback-comment-display" title="Free-form user feedback">{row.comment || 'No written comment was provided.'}</pre>
        </div>
      </div>
    </Modal>
  );
}

function SettingsAdmin({ api }) {
  const [settings, setSettings] = useState(null);
  const [draft, setDraft] = useState(null);
  const [error, setError] = useState('');
  const [saved, setSaved] = useState('');
  const [jsonOpen, setJsonOpen] = useState(false);
  const load = useCallback(async () => {
    try { setError(''); const data = await api.settings(); setSettings(data); setDraft(structuredCloneSafe(data)); } catch (err) { setError(String(err.message || err)); }
  }, [api]);
  useEffect(() => { load(); }, [load]);

  async function save() {
    try {
      setError('');
      const updated = await api.settings(draft);
      setSettings(updated);
      setDraft(structuredCloneSafe(updated));
      setSaved('Settings saved and applied to the running server where supported. Listener and database changes require a server restart.');
    } catch (err) {
      setError(String(err.message || err));
    }
  }

  if (!draft) return <div className="page settings-page"><h1 title="Server settings">{text.settings}</h1>{error ? <PermissionPanel message={error} /> : <p title="Settings loading status">Loading settings...</p>}</div>;

  const set = (path, value) => setDraft(prev => setPath(prev, path, value));
  const runners = draft.modelRunners || [];

  return (
    <div className="page settings-page">
      <PageIntro title={text.settings} description="Edit the running Wilson configuration. REST listener and database connection changes require a server restart." actions={
        <div className="table-controls">
          <button className="icon-button" title="Reload settings from the server" onClick={load}><RefreshCw size={16} /></button>
          <button className="secondary" title="View the current settings as JSON" onClick={() => setJsonOpen(true)}><Code size={16} />{text.viewJson}</button>
          <button className="primary" title="Save settings to disk and apply supported running-server changes" onClick={save}><Save size={16} />Save Settings</button>
        </div>
      } />
      {error && <PermissionPanel message={error} />}
      {saved && <div className="success-panel" title="Settings save result">{saved}</div>}
      <div className="settings-form">
        <SettingsSection title="REST Listener" restart>
          <FormInput label="Hostname" tooltip="Hostname/IP the REST listener binds to. Requires restart." value={draft.rest?.hostname || ''} onChange={v => set(['rest', 'hostname'], v)} />
          <FormInput label="Port" tooltip="TCP port for the REST listener. Requires restart." type="number" value={draft.rest?.port ?? 9400} onChange={v => set(['rest', 'port'], Number(v))} />
          <FormCheck label="TLS enabled" tooltip="Enable HTTPS listener mode. Requires restart." checked={!!draft.rest?.ssl} onChange={v => set(['rest', 'ssl'], v)} />
        </SettingsSection>
        <SettingsSection title="Database" restart>
          <FormSelect label="Database type" tooltip="Database provider. Requires restart." value={draft.database?.type || 'Sqlite'} options={['Sqlite', 'Postgres']} onChange={v => set(['database', 'type'], v)} />
          <FormInput label="SQLite filename" tooltip="SQLite database filename. Requires restart." value={draft.database?.filename || ''} onChange={v => set(['database', 'filename'], v)} />
          <FormInput label="PostgreSQL connection string" tooltip="PostgreSQL connection string. Requires restart." value={draft.database?.connectionString || ''} onChange={v => set(['database', 'connectionString'], v)} />
        </SettingsSection>
        <SettingsSection title="CORS">
          <CorsSettingsEditor
            cors={draft.cors || {}}
            onEnabledChange={v => set(['cors', 'enabled'], v)}
            onOriginsChange={v => set(['cors', 'allowedOrigins'], v)}
            onMethodsChange={v => set(['cors', 'allowedMethods'], v)}
            onHeadersChange={v => set(['cors', 'allowedHeaders'], v)}
          />
        </SettingsSection>
        <SettingsSection title="Authentication">
          <AuthenticationSettingsEditor
            auth={draft.auth || {}}
            onTokensChange={v => set(['auth', 'adminBearerTokens'], v)}
            onSessionHoursChange={v => set(['auth', 'sessionHours'], Number(v))}
          />
        </SettingsSection>
        <SettingsSection title="Request History">
          <FormCheck label="Capture enabled" tooltip="Enable request history capture for API requests." checked={!!draft.requestHistory?.enabled} onChange={v => set(['requestHistory', 'enabled'], v)} />
          <FormInput label="Retention days" tooltip="Number of days request history should be retained." type="number" value={draft.requestHistory?.retentionDays ?? 30} onChange={v => set(['requestHistory', 'retentionDays'], Number(v))} />
        </SettingsSection>
        <SettingsSection title="Model Servers">
          {runners.map((runner, index) => <ModelRunnerEditor key={index} runner={runner} index={index} onChange={(next) => set(['modelRunners', index], next)} onDelete={() => set(['modelRunners'], runners.filter((_, i) => i !== index))} />)}
          <button className="secondary" title="Add a new model server definition" onClick={() => set(['modelRunners'], [...runners, { ...newModelRunner(), id: '' }])}><Plus size={16} />Add Server</button>
        </SettingsSection>
        <SettingsSection title="Seed User" restart>
          <FormInput label="Tenant name" tooltip="Default tenant name used during first-run seeding. Requires reseed/restart to affect existing data." value={draft.seed?.tenantName || ''} onChange={v => set(['seed', 'tenantName'], v)} />
          <FormInput label="User email" tooltip="Default user email used during first-run seeding. Requires reseed/restart to affect existing data." value={draft.seed?.userEmail || ''} onChange={v => set(['seed', 'userEmail'], v)} />
          <FormInput label="First name" tooltip="Default user first name used during first-run seeding." value={draft.seed?.firstName || ''} onChange={v => set(['seed', 'firstName'], v)} />
          <FormInput label="Last name" tooltip="Default user last name used during first-run seeding." value={draft.seed?.lastName || ''} onChange={v => set(['seed', 'lastName'], v)} />
          <FormInput label="Access key" tooltip="Default access key used during first-run seeding." value={draft.seed?.accessKey || ''} onChange={v => set(['seed', 'accessKey'], v)} />
        </SettingsSection>
      </div>
      {jsonOpen && <JsonModal title="Settings JSON" row={settings || draft} onClose={() => setJsonOpen(false)} />}
    </div>
  );
}

function ModelRunnerEditor({ runner, index, onChange, onDelete }) {
  const set = (key, value) => onChange({ ...runner, [key]: value });
  return (
    <div className="runner-editor" title={`Model server ${index + 1}`}>
      <div className="runner-editor-header"><strong>Server {index + 1}</strong><button className="icon-button" title="Remove this model server" onClick={onDelete}><Trash2 size={16} /></button></div>
      <FormInput label="Server ID" tooltip="Unique model server identifier." value={runner.id || ''} onChange={v => set('id', v)} />
      <FormInput label="Name" tooltip="Display name for this model server." value={runner.name || ''} onChange={v => set('name', v)} />
      <FormSelect label="API type" tooltip="Model server API compatibility." value={runner.apiType || 'Ollama'} options={['Ollama', 'OpenAI', 'OpenAICompatible']} onChange={v => set('apiType', v)} />
      <FormInput label="Endpoint" tooltip="Base URL for the model server." value={runner.endpoint || ''} onChange={v => set('endpoint', v)} />
      <FormInput label="API key" tooltip="Optional API key for the model server." value={runner.apiKey || ''} onChange={v => set('apiKey', v || null)} />
      <FormList label="Configured models" tooltip="Known model names. Leave empty for Ollama to let Wilson query the Ollama server." value={runner.models || []} onChange={v => set('models', v)} />
      <FormInput label="Context window tokens" tooltip="Context window used for prompt truncation." type="number" value={runner.contextWindowTokens ?? 8192} onChange={v => set('contextWindowTokens', Number(v))} />
      <div className="health-editor-block">
        <div className="runner-editor-header"><strong>Health Checks</strong></div>
        <FormCheck label="Enabled" tooltip="Run periodic background health probes for this model server." checked={runner.healthCheckEnabled !== false} onChange={v => set('healthCheckEnabled', v)} />
        <FormInput label="Health URL" tooltip="Absolute URL or path to probe for health." value={runner.healthCheckUrl || defaultHealthCheckSettings(runner.apiType, runner.endpoint, runner.apiKey).healthCheckUrl} onChange={v => set('healthCheckUrl', v)} />
        <FormSelect label="Method" tooltip="HTTP method used for health probes." value={runner.healthCheckMethod || 'GET'} options={['GET', 'HEAD']} onChange={v => set('healthCheckMethod', v)} />
        <FormInput label="Expected status" tooltip="HTTP status code required for a healthy response." type="number" value={runner.healthCheckExpectedStatusCode ?? 200} onChange={v => set('healthCheckExpectedStatusCode', Number(v))} />
        <FormInput label="Interval (ms)" tooltip="Milliseconds between health probes." type="number" value={runner.healthCheckIntervalMs ?? defaultHealthCheckSettings(runner.apiType).healthCheckIntervalMs} onChange={v => set('healthCheckIntervalMs', Number(v))} />
        <FormInput label="Timeout (ms)" tooltip="Milliseconds to wait before a health probe fails." type="number" value={runner.healthCheckTimeoutMs ?? defaultHealthCheckSettings(runner.apiType).healthCheckTimeoutMs} onChange={v => set('healthCheckTimeoutMs', Number(v))} />
        <FormInput label="Healthy threshold" tooltip="Consecutive successful probes required to mark healthy." type="number" value={runner.healthyThreshold ?? 2} onChange={v => set('healthyThreshold', Number(v))} />
        <FormInput label="Unhealthy threshold" tooltip="Consecutive failed probes required to mark unhealthy." type="number" value={runner.unhealthyThreshold ?? 2} onChange={v => set('unhealthyThreshold', Number(v))} />
        <FormCheck label="Use API key" tooltip="Send the model server API key with health probes." checked={!!runner.healthCheckUseAuth} onChange={v => set('healthCheckUseAuth', v)} />
      </div>
    </div>
  );
}

function CorsSettingsEditor({ cors, onEnabledChange, onOriginsChange, onMethodsChange, onHeadersChange }) {
  return (
    <div className="cors-editor">
      <div className="cors-enabled">
        <FormCheck label="CORS enabled" tooltip="Enable cross-origin request handling." checked={!!cors.enabled} onChange={onEnabledChange} />
      </div>
      <CorsListRows label="Allowed origins" tooltip="Origins that may call the API from a browser." value={cors.allowedOrigins || []} onChange={onOriginsChange} wide placeholder="https://example.com" />
      <CorsListRows label="Allowed methods" tooltip="HTTP methods accepted by CORS preflight requests." value={cors.allowedMethods || []} onChange={onMethodsChange} compact placeholder="GET" />
      <CorsListRows label="Allowed headers" tooltip="Request headers accepted by CORS preflight requests." value={cors.allowedHeaders || []} onChange={onHeadersChange} placeholder="authorization" />
    </div>
  );
}

function CorsListRows({ label, tooltip, value, onChange, wide = false, compact = false, placeholder = '' }) {
  const rows = [...(value || []), ''];
  function update(index, nextValue) {
    const next = [...(value || [])];
    if (index >= next.length) next.push(nextValue);
    else next[index] = nextValue;
    onChange(next.map(item => item.trim()).filter(Boolean));
  }
  function remove(index) {
    onChange((value || []).filter((_, itemIndex) => itemIndex !== index));
  }
  return (
    <div className={wide ? 'cors-list-panel wide' : 'cors-list-panel'} title={tooltip}>
      <div className="cors-list-header">
        <label>{label}</label>
        <span title={`Configured ${label.toLowerCase()} count`}>{(value || []).length}</span>
      </div>
      <div className={compact ? 'cors-list-rows compact' : 'cors-list-rows'}>
        {rows.map((item, index) => {
          const isNew = index >= (value || []).length;
          return (
            <div key={index} className="cors-list-row">
              <input title={tooltip} value={item} onChange={e => update(index, e.target.value)} placeholder={isNew ? placeholder || `Add ${label.toLowerCase()}` : label} />
              {isNew ? <button className="icon-button" title={`Add ${label.toLowerCase()} row`} onClick={() => update(index, item)}><Plus size={15} /></button> : <button className="icon-button" title={`Remove ${item}`} onClick={() => remove(index)}><Trash2 size={15} /></button>}
            </div>
          );
        })}
      </div>
    </div>
  );
}

function AuthenticationSettingsEditor({ auth, onTokensChange, onSessionHoursChange }) {
  return (
    <div className="auth-editor">
      <div className="auth-session-panel">
        <FormInput label="Session hours" tooltip="Session lifetime in hours." type="number" value={auth.sessionHours ?? 24} onChange={onSessionHoursChange} />
      </div>
      <TokenListRows label="Admin bearer tokens" tooltip="Global administrator bearer tokens used for full dashboard/API administration." value={auth.adminBearerTokens || []} onChange={onTokensChange} />
    </div>
  );
}

function TokenListRows({ label, tooltip, value, onChange }) {
  const rows = [...(value || []), ''];
  function update(index, nextValue) {
    const next = [...(value || [])];
    if (index >= next.length) next.push(nextValue);
    else next[index] = nextValue;
    onChange(next.map(item => item.trim()).filter(Boolean));
  }
  function remove(index) {
    onChange((value || []).filter((_, itemIndex) => itemIndex !== index));
  }
  return (
    <div className="token-list-panel" title={tooltip}>
      <div className="cors-list-header">
        <label>{label}</label>
        <span title="Configured administrator bearer token count">{(value || []).length}</span>
      </div>
      <div className="token-list-rows">
        {rows.map((item, index) => {
          const isNew = index >= (value || []).length;
          return (
            <div key={index} className="token-list-row">
              <input title={tooltip} value={item} onChange={e => update(index, e.target.value)} placeholder={isNew ? 'Add admin bearer token' : label} />
              {isNew ? <button className="icon-button" title="Add admin bearer token row" onClick={() => update(index, item)}><Plus size={15} /></button> : <button className="icon-button" title="Remove this admin bearer token" onClick={() => remove(index)}><Trash2 size={15} /></button>}
            </div>
          );
        })}
      </div>
    </div>
  );
}

function entityActions(row, setModal) {
  return [
    { label: text.view, tooltip: 'View this record', icon: Eye, onClick: () => setModal({ type: 'view', row }) },
    { label: text.viewJson, tooltip: 'View raw JSON for this record', icon: Code, onClick: () => setModal({ type: 'json', row }) },
    { label: text.edit, tooltip: 'Edit this record', icon: Pencil, onClick: () => setModal({ type: 'edit', row }) },
    { label: text.delete, tooltip: 'Delete this record', icon: Trash2, danger: true, onClick: () => setModal({ type: 'delete', row }) }
  ];
}

function renderEntityModal(modal, setModal, updateItem, deleteItem, load, name) {
  if (!modal) return null;
  if (modal.type === 'view') return <RecordModal title={name} row={modal.row} onClose={() => setModal(null)} />;
  if (modal.type === 'json') return <JsonModal title={`${name} JSON`} row={modal.row} onClose={() => setModal(null)} />;
  if (modal.type === 'edit') return <EditRecordModal title={`Edit ${name}`} row={modal.row} onClose={() => setModal(null)} onSave={async (next) => { await updateItem(modal.row.id, next); setModal(null); load(); }} />;
  if (modal.type === 'delete') return <ConfirmModal title={`Delete ${name}`} message={`Delete ${modal.row.name || modal.row.email || modal.row.id}?`} onCancel={() => setModal(null)} onConfirm={async () => { await deleteItem(modal.row.id, modal.row); setModal(null); load(); }} />;
  return null;
}

function Segmented({ value, options, onChange }) {
  return <div className="segmented">{Object.entries(options).map(([key, option]) => <button key={key} title={`Show ${option.label.toLowerCase()} request history`} className={value === key ? 'active' : ''} onClick={() => onChange(key)}>{option.label}</button>)}</div>;
}

function DataTable({ rows, columns, onRefresh, onRowClick, actions, enumeration = null, page: controlledPage, pageSize: controlledPageSize, setPage: controlledSetPage, setPageSize: controlledSetPageSize }) {
  const [localPage, setLocalPage] = useState(0);
  const [localPageSize, setLocalPageSize] = useState(10);
  const [filter, setFilter] = useState('');
  const page = controlledPage ?? localPage;
  const pageSize = controlledPageSize ?? localPageSize;
  const setPage = controlledSetPage ?? setLocalPage;
  const setPageSize = controlledSetPageSize ?? setLocalPageSize;
  const filtered = rows.filter(row => !filter.trim() || JSON.stringify(row).toLowerCase().includes(filter.toLowerCase()));
  const serverPaged = Boolean(enumeration);
  const totalRecords = serverPaged ? enumeration.totalRecords || 0 : filtered.length;
  const totalPages = serverPaged ? Math.max(1, enumeration.totalPages || 1) : Math.max(1, Math.ceil(filtered.length / pageSize));
  const safePage = Math.min(page, totalPages - 1);
  const pageRows = serverPaged ? filtered : filtered.slice(safePage * pageSize, safePage * pageSize + pageSize);
  useEffect(() => { if (page !== safePage) setPage(safePage); }, [page, safePage]);
  return (
    <div className="operator-table">
      <div className="table-controls">
        <div className="table-stats" title="Number of matching records and total pages"><strong>{totalRecords}</strong> records<span>{` across ${totalPages} pages`}</span></div>
        <input className="table-filter" title="Filter records by any visible or hidden value" value={filter} onChange={e => { setFilter(e.target.value); setPage(0); }} placeholder="Filter records" />
      </div>
      <Pagination total={totalRecords} totalPagesOverride={totalPages} page={safePage} pageSize={pageSize} setPage={setPage} setPageSize={setPageSize} onRefresh={onRefresh} refreshTitle="Reload this table" />
      <div className="table-wrap">
        <table>
          <thead><tr>{columns.map(c => <HeaderCell key={c} column={c} />)}{actions && <th className="actions-col" title="Available row actions">Actions</th>}</tr></thead>
          <tbody>
            {pageRows.map((row, i) => (
              <tr key={row.id || i} onClick={() => onRowClick?.(row)} className={onRowClick ? 'clickable' : ''} title={onRowClick ? 'Click to edit this record' : 'Record row'}>
                {columns.map(c => <td key={c}>{formatCell(row[c], c)}</td>)}
                {actions && <td className="actions-col" onClick={(event) => event.stopPropagation()}><ActionMenu items={actions(row)} /></td>}
              </tr>
            ))}
            {pageRows.length < 1 && <tr><td colSpan={columns.length + (actions ? 1 : 0)} className="empty-cell">No records</td></tr>}
          </tbody>
        </table>
      </div>
    </div>
  );
}

function HeaderCell({ column }) {
  const [label, tooltip] = getFieldMeta(column);
  return <th title={tooltip}>{label}</th>;
}

function Pagination({ total, page, pageSize, setPage, setPageSize, totalPagesOverride = null, onRefresh = null, refreshing = false, refreshTitle = 'Reload this table' }) {
  const totalPages = totalPagesOverride ?? Math.max(1, Math.ceil(total / pageSize));
  const start = total === 0 ? 0 : page * pageSize + 1;
  const end = Math.min(total, (page + 1) * pageSize);
  return (
    <div className="pagination-top">
      <div title="Current visible row range">{start}-{end} of {total}</div>
      <label title="Rows shown per page">Rows<select title="Select rows per page" value={pageSize} onChange={e => { setPageSize(Number(e.target.value)); setPage(0); }}><option>10</option><option>25</option><option>50</option><option>100</option></select></label>
      <button className="icon-button" title="Go to previous page" onClick={() => setPage(Math.max(0, page - 1))} disabled={page === 0}><ChevronLeft size={16} /></button>
      <span title="Current page number">Page {page + 1} of {totalPages}</span>
      <label title="Jump to a specific page">Jump<input className="page-jump" type="number" min="1" max={totalPages} value={page + 1} onChange={e => setPage(Math.min(totalPages - 1, Math.max(0, Number(e.target.value || 1) - 1)))} /></label>
      <button className="icon-button" title="Go to next page" onClick={() => setPage(Math.min(totalPages - 1, page + 1))} disabled={page >= totalPages - 1}><ChevronRight size={16} /></button>
      {onRefresh && <button className="icon-button" title={refreshTitle} onClick={onRefresh} disabled={refreshing}><RefreshCw size={16} className={refreshing ? 'spin' : ''} /></button>}
    </div>
  );
}

function ActionMenu({ items, title = 'Open row actions menu' }) {
  const [open, setOpen] = useState(false);
  const buttonRef = useRef(null);
  const [position, setPosition] = useState({ top: 0, left: 0 });
  function toggle() {
    const rect = buttonRef.current?.getBoundingClientRect();
    if (rect) setPosition({ top: rect.bottom + 4, left: Math.max(8, rect.right - 178) });
    setOpen(!open);
  }
  return (
    <div className="action-menu">
      <button ref={buttonRef} className="icon-button" title={title} onClick={toggle}><MoreVertical size={16} /></button>
      {open && <div className="action-menu-panel" style={{ top: position.top, left: position.left }}>{items.map((item, index) => {
        const Icon = item.icon;
        return <button key={index} title={item.tooltip || item.label} className={item.danger ? 'danger' : ''} onClick={() => { setOpen(false); item.onClick(); }}>{Icon && <Icon size={15} />}{item.label}</button>;
      })}</div>}
    </div>
  );
}

function RecordModal({ title, row, onClose }) {
  const primaryRows = Object.entries(row).filter(([key]) => !['createdUtc', 'lastUpdateUtc', 'lastUsedUtc', 'accessKey'].includes(key));
  const auditRows = ['createdUtc', 'lastUpdateUtc', 'lastUsedUtc', 'accessKey'].filter(key => key in row).map(key => [key, row[key]]);
  return (
    <Modal title={title} onClose={onClose} wide>
      <div className="entity-modal">
        <div className="entity-hero">
          <div>
            <span>{title}</span>
            <strong>{row.name || row.email || row.title || row.id}</strong>
          </div>
          {row.id && <CopyableId value={row.id} label={`${title} ID`} />}
        </div>
        <div className="entity-section">
          <h3>Primary</h3>
          <KeyValueTable rows={primaryRows} />
        </div>
        <div className="entity-section">
          <h3>Audit</h3>
          <KeyValueTable rows={auditRows} />
        </div>
      </div>
    </Modal>
  );
}

function KeyValueTable({ rows }) {
  return (
    <table className="key-value-table">
      <tbody>
        {rows.map(([key, value]) => {
          const [label, tooltip] = getFieldMeta(key);
          return <tr key={key}><th title={tooltip}>{label}</th><td title={tooltip}>{formatCell(value, key) || '-'}</td></tr>;
        })}
      </tbody>
    </table>
  );
}

function DetailField({ name, value }) {
  const [label, tooltip] = getFieldMeta(name);
  return <div><label title={tooltip}>{label}</label><div title={tooltip}>{formatCell(value, name)}</div></div>;
}

function JsonModal({ title, row, onClose }) {
  return <Modal title={title} onClose={onClose} wide><pre className="json-pre" title="Formatted JSON">{JSON.stringify(row, null, 2)}</pre></Modal>;
}

function EditRecordModal({ title, row, onClose, onSave }) {
  const [draft, setDraft] = useState(structuredCloneSafe(row));
  const [error, setError] = useState('');
  const readonly = new Set(['id', 'createdUtc', 'lastUpdateUtc', 'lastUsedUtc', 'accessKey', 'secretLast4']);
  async function save() {
    try { setError(''); await onSave(draft); } catch (err) { setError(String(err.message || err)); }
  }
  return (
    <Modal title={title} onClose={onClose} wide>
      <div className="entity-modal">
        <div className="entity-hero">
          <div>
            <span>{title}</span>
            <strong>{draft.name || draft.email || draft.title || draft.id}</strong>
          </div>
          {draft.id && <CopyableId value={draft.id} label={`${title} ID`} />}
        </div>
      <div className="form-grid entity-edit-grid">
        {Object.entries(draft).map(([key, value]) => {
          const [label, tooltip] = getFieldMeta(key);
          if (readonly.has(key)) return <DetailField key={key} name={key} value={value} />;
          if (typeof value === 'boolean') return <label key={key} className="check-row" title={tooltip}><input title={tooltip} type="checkbox" checked={value} onChange={e => setDraft(prev => ({ ...prev, [key]: e.target.checked }))} />{label}</label>;
          return <label key={key} title={tooltip}>{label}<input title={tooltip} value={value ?? ''} onChange={e => setDraft(prev => ({ ...prev, [key]: e.target.value }))} /></label>;
        })}
      </div>
      </div>
      {error && <div className="error" title="Save error">{error}</div>}
      <div className="modal-actions"><button className="secondary" title="Close without saving changes" onClick={onClose}>Cancel</button><button className="primary" title="Save changes to this record" onClick={save}><Save size={16} />Save</button></div>
    </Modal>
  );
}

function ConfirmModal({ title, message, onCancel, onConfirm }) {
  return <Modal title={title} onClose={onCancel}><p className="confirm-message" title={message}>{message}</p><div className="modal-actions"><button className="secondary" title="Cancel this action" onClick={onCancel}>Cancel</button><button className="danger-button" title="Confirm this destructive action" onClick={onConfirm}><Trash2 size={16} />Confirm</button></div></Modal>;
}

function Modal({ title, onClose, children, wide = false, full = false }) {
  return <div className="modal-backdrop" role="dialog" aria-modal="true" onMouseDown={onClose}><div className={`${wide ? 'modal modal-wide' : 'modal'}${full ? ' modal-full' : ''}`} onMouseDown={e => e.stopPropagation()}><header><h2 title={title}>{title}</h2><button className="icon-button" title="Close this modal" onClick={onClose}><X size={18} /></button></header>{children}</div></div>;
}

function SettingsSection({ title, children, restart = false }) {
  return <section className="settings-section" title={restart ? `${title}: changes require a server restart` : `${title}: changes apply when saved`}><h2>{title}{restart && <span title="This section requires a server restart for changes to take effect">Restart required</span>}</h2><div className="form-grid">{children}</div></section>;
}

function FormInput({ label, tooltip, value, onChange, type = 'text' }) {
  return <label title={tooltip}>{label}<input title={tooltip} type={type} value={value} onChange={e => onChange(e.target.value)} /></label>;
}

function FormSelect({ label, tooltip, value, options, onChange }) {
  return <label title={tooltip}>{label}<select title={tooltip} value={value} onChange={e => onChange(e.target.value)}>{options.map(option => <option key={option} value={option}>{option}</option>)}</select></label>;
}

function FormCheck({ label, tooltip, checked, onChange }) {
  return <label className="check-row" title={tooltip}><input title={tooltip} type="checkbox" checked={checked} onChange={e => onChange(e.target.checked)} />{label}</label>;
}

function FormList({ label, tooltip, value, onChange }) {
  return <StringListRows label={label} tooltip={tooltip} value={value} onChange={onChange} />;
}

function StringListRows({ label, tooltip, value, onChange }) {
  const rows = [...(value || []), ''];
  function update(index, nextValue) {
    const next = [...(value || [])];
    if (index >= next.length) next.push(nextValue);
    else next[index] = nextValue;
    onChange(next.map(item => item.trim()).filter(Boolean));
  }
  function remove(index) {
    onChange((value || []).filter((_, itemIndex) => itemIndex !== index));
  }
  return (
    <div className="string-list-field" title={tooltip}>
      <label>{label}</label>
      {rows.map((item, index) => {
        const isNew = index >= (value || []).length;
        return (
          <div key={index} className="string-list-row">
            <input title={tooltip} value={item} onChange={e => update(index, e.target.value)} placeholder={isNew ? `Add ${label.toLowerCase()}` : label} />
            {isNew ? <button className="icon-button" title={`Add ${label.toLowerCase()} row`} onClick={() => update(index, item)}><Plus size={15} /></button> : <button className="icon-button" title={`Remove ${item}`} onClick={() => remove(index)}><Trash2 size={15} /></button>}
          </div>
        );
      })}
    </div>
  );
}

function CopyableId({ value, label = 'ID' }) {
  const [copied, setCopied] = useState(false);
  if (!value) return <code className="id-text">N/A</code>;
  async function copy(event) {
    event.stopPropagation();
    const textValue = String(value);
    try {
      if (navigator.clipboard && window.isSecureContext) await navigator.clipboard.writeText(textValue);
      else {
        const area = document.createElement('textarea');
        area.value = textValue;
        area.setAttribute('readonly', '');
        area.style.position = 'fixed';
        area.style.left = '-9999px';
        document.body.appendChild(area);
        area.select();
        document.execCommand('copy');
        document.body.removeChild(area);
      }
      setCopied(true);
      setTimeout(() => setCopied(false), 1200);
    } catch {
      window.prompt(`Copy ${label}`, textValue);
    }
  }
  return <span className="copy-id" title={`${label}: ${value}`}><code>{value}</code><button title={`Copy ${label} to clipboard`} onClick={copy}><Copy size={13} />{copied ? 'Copied' : ''}</button></span>;
}

function PermissionPanel({ message }) {
  return <div className="permission-panel" title="Permission or loading error"><strong>Unable to load this view.</strong><span>{message}</span></div>;
}

function formatCell(value, key = '') {
  if (value === null || value === undefined || value === '') return '';
  if (idFields.has(key)) return <CopyableId value={value} label={getFieldMeta(key)[0]} />;
  if (typeof value === 'boolean') return value ? 'Yes' : 'No';
  if (key.toLowerCase().includes('utc')) return formatDate(value);
  if (typeof value === 'object') return JSON.stringify(value);
  return String(value);
}

function getFieldMeta(key) {
  return fieldMeta[key] || [humanize(key), `Field ${humanize(key)}`];
}

function humanize(key) {
  return String(key).replace(/([A-Z])/g, ' $1').replace(/^./, c => c.toUpperCase()).replace(/\bId\b/g, 'ID').trim();
}

function formatDate(value) {
  if (!value) return '';
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return String(value);
  return date.toLocaleString();
}

function formatDuration(value) {
  const ms = Number(value || 0);
  if (ms >= 1000) return `${(ms / 1000).toFixed(2)}s`;
  return `${ms.toFixed(ms >= 10 ? 0 : 1)}ms`;
}

function formatNumber(value) {
  return Number(value || 0).toFixed(1);
}

function prettyValue(value) {
  if (!value) return '';
  if (typeof value === 'object') return JSON.stringify(value, null, 2);
  const textValue = String(value);
  try {
    return JSON.stringify(JSON.parse(textValue), null, 2);
  } catch {
    return textValue;
  }
}

function statusClass(statusCode) {
  const status = Number(statusCode || 0);
  if (status >= 500) return 'status-error';
  if (status >= 400) return 'status-warn';
  if (status >= 300) return 'status-redirect';
  return 'status-ok';
}

function structuredCloneSafe(value) {
  return JSON.parse(JSON.stringify(value));
}

function setPath(source, path, value) {
  const next = structuredCloneSafe(source);
  let cursor = next;
  for (let i = 0; i < path.length - 1; i++) {
    if (cursor[path[i]] === undefined || cursor[path[i]] === null) cursor[path[i]] = typeof path[i + 1] === 'number' ? [] : {};
    cursor = cursor[path[i]];
  }
  cursor[path[path.length - 1]] = value;
  return next;
}

export default App;
