// Application-wide constants for the NotDory dashboard.

export const STORAGE_KEYS = {
  serverUrl: 'notdory_server_url',
  token: 'notdory_token',
  tenantId: 'notdory_tenant_id',
  theme: 'notdory_theme',
  locale: 'notdory.locale',
  explorerHistory: 'notdory_api_explorer_history'
};

// Runtime override (window.__NOTDORY_CONFIG__.serverUrl) wins over the Vite
// build-time __DEFAULT_SERVER_URL__ define; the compile-time value is only a
// fallback for local dev where /config.js is not served. This lets operators
// pin the login default per environment without rebuilding the image.
const runtimeServerUrl =
  typeof window !== 'undefined' &&
  window.__NOTDORY_CONFIG__ &&
  typeof window.__NOTDORY_CONFIG__.serverUrl === 'string' &&
  window.__NOTDORY_CONFIG__.serverUrl.length > 0
    ? window.__NOTDORY_CONFIG__.serverUrl
    : null;
export const DEFAULT_SERVER_URL =
  runtimeServerUrl ??
  (typeof __DEFAULT_SERVER_URL__ !== 'undefined' ? __DEFAULT_SERVER_URL__ : 'http://127.0.0.1:8700');
export const DEFAULT_ADMIN_EMAIL =
  typeof __DEFAULT_ADMIN_EMAIL__ !== 'undefined' ? __DEFAULT_ADMIN_EMAIL__ : 'admin@notdory.local';
export const DEFAULT_TENANT_ID =
  typeof __DEFAULT_TENANT_ID__ !== 'undefined' ? __DEFAULT_TENANT_ID__ : 'ten_default';

export const GITHUB_URL = 'https://github.com/jchristn/notdory';
export const DISCORD_URL = 'https://discord.gg/tRAN8HgvK5';

export const PAGE_SIZE_OPTIONS = [10, 25, 50, 100, 250, 500, 1000];
export const DEFAULT_PAGE_SIZE = 25;

export const STORE_PROVIDERS = ['RecallDb', 'Filesystem'];

export const QUERY_EXPANSION_MODES = ['Auto', 'On', 'Off'];
export const FILESYSTEM_LAYOUTS = ['SingleFile', 'Hierarchy', 'OkfBundle'];

// Friendly labels for the filesystem layout options.
export const FILESYSTEM_LAYOUT_LABELS = {
  SingleFile: 'Single file',
  Hierarchy: 'Hierarchy (one file per memory)',
  OkfBundle: 'OKF bundle (keyword search only; git-trackable)'
};
export const MEMORY_TYPES = ['User', 'Feedback', 'Project', 'Reference'];
export const SEARCH_MODES = ['Keyword', 'Semantic', 'Hybrid'];
export const ENDPOINT_KINDS = ['Embedding', 'Inference'];
export const API_FORMATS = ['Ollama', 'OpenAI', 'VLlm', 'Gemini'];
// Every model that is not an embedding model is an inference endpoint. Every format can rerank (chat models by rating
// candidates in a prompt); Tei and Cohere are cross-encoders that can only rerank.
export const INFERENCE_API_FORMATS = ['Ollama', 'OpenAI', 'VLlm', 'Gemini', 'Tei', 'Cohere'];
export const RERANK_ONLY_FORMATS = ['Tei', 'Cohere'];
export const canChat = (format) => !RERANK_ONLY_FORMATS.includes(format);
// How much a reasoning model thinks on NotDory's calls to an inference endpoint (ModelEndpoint.Reasoning).
export const REASONING_MODES = ['Default', 'Off', 'Low', 'Medium', 'High'];

export const HEALTH_METHODS = ['GET', 'HEAD'];
export const AUTH_TYPES = ['None', 'BearerToken', 'ApiKeyHeader', 'QueryParam', 'BasicAuth', 'AccessKeySecret'];
export const INSTRUCTION_MERGE_MODES = ['Append', 'Replace', 'Hide'];

// Chunking of oversized memory bodies (embedding scopes).
export const CHUNKING_MODES = ['OnOverflow', 'Always', 'Off'];
export const CHUNK_STRATEGIES = ['FixedTokenCount', 'SentenceBased', 'ParagraphBased', 'Recursive'];

// Friendly labels for the chunking mode options.
export const CHUNKING_MODE_LABELS = {
  OnOverflow: 'On overflow (only when a body exceeds the model budget)',
  Always: 'Always (chunk every memory)',
  Off: 'Off (never chunk; embed the whole body)'
};

// External observability services surfaced on the Home page (local dev defaults).
export const EXTERNAL_SERVICES = [
  { key: 'grafana', name: 'Grafana', url: 'http://127.0.0.1:3000', creds: 'admin / admin' },
  { key: 'prometheus', name: 'Prometheus', url: 'http://127.0.0.1:9090', creds: 'No authentication' },
  { key: 'tempo', name: 'Tempo', url: 'http://127.0.0.1:3200', creds: 'No authentication' },
  { key: 'recalldb', name: 'RecallDB', url: 'http://127.0.0.1:8601', creds: 'API key: recalldbadmin' }
];
