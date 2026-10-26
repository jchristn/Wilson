# Wilson

<p align="center">
  <img src="assets/logo.png" alt="Wilson logo" width="192" height="192">
</p>

Wilson is a local-first chat server and dashboard for talking to Ollama, OpenAI, and OpenAI-compatible model runners. It gives you a browser dashboard, a REST API, tenant-aware users and credentials, request history, feedback capture, and model-runner management.

You're stranded on an island. At least Wilson talks back.

No signal. No noise. No rescue ships. Just Wilson, your local model, and a chat window that does not need to phone home.

## What It Does

Wilson runs a C# backend using Watson and a React dashboard. The backend stores tenants, users, credentials, conversations, messages, feedback, request history, and model runner configuration. The dashboard gives users a ChatGPT-style experience and gives administrators tools to manage the system.

Wilson can:

- Chat with local Ollama models, Ollama Cloud, OpenAI, Azure OpenAI, xAI Grok, Anthropic, Google Gemini, Vertex AI, AWS Bedrock, Cohere, VoyageAI, Text Embeddings Inference, or any OpenAI-compatible API
- Stream model responses over server-sent events
- Keep conversation history in a database
- Manage multiple configured model servers
- Check model server health in the background with thresholds and recent history
- Pull and load Ollama models from the dashboard
- Show which Ollama models are available and currently loaded
- Capture request history, response timing, and request/response payload metadata
- Collect thumbs-up/thumbs-down feedback and optional free-form comments
- Manage tenant-scoped system and tool prompt templates, including seeded defaults
- Expose model-directed tools when explicitly enabled, with safe chat traces and persisted tool-call history
- Expose OpenAPI JSON and Swagger UI for the backend API

The waves never answer. Wilson does.

## Features

- **Dashboard chat**: browser-based chat with model server/model selectors, streaming responses, response timing details, feedback buttons, and conversation rename/delete.
- **Model server management**: configure any of the thirteen supported provider API types (Ollama, Ollama Cloud, OpenAI, OpenAI-compatible, Azure OpenAI, xAI Grok, Anthropic, Google Gemini, Google Vertex AI, AWS Bedrock, Cohere, VoyageAI, Text Embeddings Inference); inspect health, uptime, and recent health history; inspect available models; inspect loaded Ollama models; pull and load Ollama models.
- **Tenant-aware auth**: tenants, users, credentials, admin tokens, tenant admins, and bearer-token authentication.
- **Conversation storage**: saved conversations and messages backed by SQLite or PostgreSQL.
- **Prompt templates**: a Prompts page for tenant-scoped system prompts and tool prompts, seeded defaults, chat prompt selection, and request-history prompt metadata.
- **Request history**: latency summary, activity chart, detailed request/response metadata, headers, bodies, timing, and token estimates.
- **Tool activity**: optional model tool execution with safe inline chat activity, persisted tool runs/tool calls, request-history linkage, and dashboard settings controls.
- **Feedback review**: admin view for ratings, comments, related message IDs, and model timing fields.
- **Settings editor**: dashboard form for editing Wilson configuration without dumping raw JSON as the primary workflow.
- **API tools**: named API explorer, OpenAPI JSON at `/openapi.json`, and Swagger UI at `/swagger`.
- **Docker support**: Docker Compose setup, dashboard container, backend container, and factory reset scripts.
- **Local-first defaults**: backend defaults to port `9400`; dashboard defaults to port `9401`.

## Use Cases

- Run a private local chat dashboard for Ollama models
- Test and compare local, cloud-hosted, and OpenAI-compatible model runners
- Give a small team tenant-aware access to shared local model infrastructure
- Capture operational request history while developing model-backed workflows
- Collect human feedback on model responses
- Manage Ollama model pulls and loaded-model state without leaving the dashboard
- Keep a conversation partner around when the island is quiet

## Quick Start

Run Wilson with Docker. It brings up the backend, the dashboard, and the observability stack together from pinned, prebuilt images — no toolchain to install and nothing to compile. Docker is the only setup these instructions support; running the pieces by hand is for people developing Wilson itself, not for deploying or evaluating it.

### Prerequisites

- Docker Desktop, or Docker Engine with the Compose plugin
- Optional: Ollama if you want local model inference

### Run With Docker

```powershell
cd docker
docker compose pull
docker compose up -d
```

The stack serves the dashboard at `http://127.0.0.1:9401` and the backend at `http://127.0.0.1:9400`. On first start Wilson seeds default credentials:

- Admin bearer token: `wilson-admin-dev-token`
- User access key: `wilsonadmin`

Open the dashboard and log in:

- Server URL: `http://127.0.0.1:9400`
- Access key: `wilsonadmin` or `wilson-admin-dev-token`

Change those seeded credentials before exposing Wilson to anything you do not control. If you are working on the code rather than running Wilson, see [Development (from source)](#development-from-source).

Welcome to the island. Wilson's been expecting you.

## Docker

The Compose file references the published images `jchristn77/wilson-server:v0.1.0` and `jchristn77/wilson-dashboard:v0.1.0`. Pull them and bring the stack up:

```powershell
cd docker
docker compose pull
docker compose up -d
```

`docker/update.bat` (Windows) and `docker/update.sh` (Linux/macOS) wrap the same pull-and-restart flow.

Docker exposes:

- Backend: `http://127.0.0.1:9400`
- Dashboard: `http://127.0.0.1:9401`

Factory reset scripts:

- Windows: `docker/factory/reset.bat`
- Linux/macOS: `docker/factory/reset.sh`

These reset Docker data and restore Docker settings from `docker/factory`.

Docker Compose mounts a named `/workspace` volume and the Docker settings expose that path to Wilson file and process tools by default. To use a host directory instead, mount that directory to `/workspace` and keep `tools.workingDirectory` plus `tools.allowedRoots` pointed at the container path, not the host path. Do not mount broad host paths such as a home directory or source-drive root unless the deployment is isolated and trusted.

Prompt templates are not stored in `wilson.json`. Fresh Docker deployments and factory resets create the prompt database tables at server startup and seed one default system prompt plus one default tool prompt for each tenant.

## Development (from source)

These steps are for working on Wilson's own code. To run or evaluate Wilson, use [Docker](#quick-start) — the from-source path is not the supported deployment configuration, and it skips the observability stack entirely.

You will need the .NET 10 SDK, Node.js with npm, and optionally Ollama for local inference. Run the backend:

```powershell
dotnet run --project src\Wilson.Server
```

It listens on `http://127.0.0.1:9400` and, on first start, writes `wilson.json` and seeds the default credentials shown above. Run the dashboard in a second shell:

```powershell
cd dashboard
npm install
npm run dev
```

The dashboard serves on `http://127.0.0.1:9401`; log in with server URL `http://127.0.0.1:9400` and access key `wilsonadmin`.

## Configuration

Wilson reads settings from `wilson.json`.

Important sections:

- `rest`: listener hostname, port, and TLS flag
- `database`: SQLite/PostgreSQL settings
- `cors`: allowed origins, methods, and headers
- `auth`: admin bearer tokens and session lifetime
- `requestHistory`: request capture settings
- Prompt templates are database records, not JSON settings. Wilson creates default system and tool prompts on startup when a tenant is missing them.
- `tools`: global tool enablement, built-in tool policy, safety limits, allowed roots, web search, and MCP settings
- `modelRunners`: model servers for any supported provider API type (see Model Runners below)
- `seed`: first-run tenant, user, and access key

Each `modelRunners` entry supports endpoint health checks:

- `healthCheckEnabled`: enables background probing. Defaults to off for providers that expose no health or model-list route Wilson can call (Google Vertex AI, AWS Bedrock, VoyageAI) and for any runner whose endpoint is not an absolute HTTP(S) URL, because Wilson never sends a probe to an empty or relative URL. Supplying an absolute `healthCheckUrl` re-enables probing for those providers.
- `healthCheckUrl`: absolute URL or endpoint path; defaults to the provider catalog health path (`/api/tags` for Ollama and Ollama Cloud, `/v1/models` for OpenAI, Anthropic, xAI, and Cohere, `/v1beta/models` for Gemini, `/openai/models` for Azure OpenAI, `/info` for Text Embeddings Inference). An explicitly configured absolute HTTP(S) URL is always preserved. A leading-slash value such as `/api/tags` is an endpoint-relative path and is joined onto the endpoint, so custom probe paths keep working. A value that is an explicitly supplied absolute URL with a non-HTTP scheme, such as `file:///tmp/probe` or `ftp://example.com/tags`, is rejected and checks stay off.
- `healthCheckMethod`: `GET` or `HEAD`
- `healthCheckIntervalMs`, `healthCheckTimeoutMs`
- `healthCheckExpectedStatusCode`
- `healthyThreshold`, `unhealthyThreshold`
- `healthCheckUseAuth`: sends this runner's API key with health probes, using the provider's own authentication contract rather than a blanket bearer token: `Authorization: Bearer` for Ollama, Ollama Cloud, OpenAI, OpenAI-compatible, xAI, Cohere, VoyageAI, and Text Embeddings Inference; `x-api-key` plus `anthropic-version: 2023-06-01` for Anthropic; `x-goog-api-key` for Gemini; `api-key` for Azure OpenAI. Azure OpenAI probes also carry the `api-version` query parameter, which its models route requires. When a probe URL cannot be resolved to an absolute HTTP(S) URL, Wilson records an actionable error and sends no request at all.

Due health checks share a probe only when their final request URL, method, expected status, timeout, and effective authentication match. Scheme and host are case-insensitive; path and query case and trailing slashes remain significant. Authenticated probes use a SHA-256 identity of the trimmed API key for grouping, so different credentials receive independent results without storing raw keys in monitor identities. Identical anonymous probes still share a request.

The dashboard Settings page edits the same configuration file. Some changes apply immediately; listener and database changes require a server restart.

### Model Runners

PolyPrompt 3.x (Wilson uses 3.5.0) adds endpoint types and providers beyond the original Ollama/OpenAI pair, and Wilson exposes all of them. `apiType` selects the PolyPrompt client family:
| `apiType` | Provider | Chat | Embeddings | Model listing | Tools |
| --- | --- | --- | --- | --- | --- |
| `Ollama` | Local Ollama server | yes | yes | yes | yes |
| `OllamaCloud` | Ollama Cloud (hosted, native Ollama API) | yes | yes | yes | no |
| `OpenAI` | OpenAI public API | yes | yes | yes | yes |
| `OpenAICompatible` | Any OpenAI chat-completions server | yes | yes | yes | yes |
| `AzureOpenAI` | Azure OpenAI deployments | yes | yes | yes | yes |
| `XAI` | xAI Grok (native Responses API, hosted tools) | yes | yes | yes | yes |
| `Anthropic` | Anthropic Claude Messages API | yes | no | yes | yes |
| `Gemini` | Google Gemini API | yes | yes | yes | yes |
| `VertexAI` | Google Vertex AI Gemini models | yes | no | no | no |
| `Bedrock` | AWS Bedrock model runtime | yes | yes | yes | yes |
| `Cohere` | Cohere platform API | yes | yes | yes | yes |
| `VoyageAI` | VoyageAI embedding models | no | yes | no | no |
| `Tei` | Hugging Face Text Embeddings Inference | no | yes | yes | no |

`GET /v1.0/api/model-runners/api-types` returns the same catalog at runtime, including default endpoints, default health paths, default health timings, capability flags, default tool-calling format, required credential fields, whether the provider supports a health probe at all, and the health-probe authentication scheme and protocol version the provider expects. The dashboard builds its API type picker from that catalog.

Changing `apiType` in either dashboard model server editor re-derives the new provider's endpoint, health URL and timings, tool format, and capability flags, so selecting a provider from a fresh Ollama draft always yields a usable configuration. Values that still equal the previous provider's defaults are replaced; a custom endpoint, a custom health URL or timing, and any control the operator disabled are preserved. An enabled health check stays enabled when a custom absolute HTTP(S) health URL is retained, including on providers that have no default probe route (Google Vertex AI, AWS Bedrock, VoyageAI); it is only turned off when the retained URL cannot resolve to an absolute HTTP(S) URL.

Provider-specific fields beyond `apiType`, `endpoint`, and `apiKey`:

- `deployment`: Azure OpenAI deployment name (required)
- `apiVersion`: Azure OpenAI `api-version` query value; defaults to `2024-10-21`
- `region`: Vertex AI or AWS Bedrock region; defaults to `us-central1` and `us-east-1`
- `projectId`: Vertex AI Google Cloud project (required)
- `apiSecretKey` and `awsSessionToken`: AWS credentials for Bedrock. When `apiKey` and `apiSecretKey` are both set Wilson signs with static credentials; otherwise it uses the standard AWS environment/profile chain.

`endpoint` may be left blank for AWS Bedrock and Google Vertex AI, because both derive it: Bedrock uses the regional AWS endpoint from the SDK and Vertex AI builds a regional resource URL from `projectId` and `region`. Every other provider falls back to the catalog default endpoint when the field is blank.

An unrecognized `apiType` keeps Wilson's legacy Ollama-family clients so existing configuration still chats, and the model runner status reports `apiTypeSupported: false` so the dashboard can flag it. A cataloged provider that genuinely lacks a capability (for example embeddings on Anthropic, or tool calls on VoyageAI) fails explicitly instead of silently degrading.

Tools are enabled by default for tool-capable runners. Safe tools use automatic approval by default; destructive and process tools remain approval-required. If `tools.workingDirectory` or `tools.allowedRoots` are empty, Wilson normalizes them to the server working directory for local installs; Docker defaults them to `/workspace`. Individual model runners also have tool-capability controls (`toolsEnabled`, `supportsTools`, and `toolCallingApiFormat`) so runners that cannot speak a tool-call protocol continue to use normal chat.

Tool-capable runners must support a structured tool-call wire format. OpenAI and OpenAI-compatible providers should use `toolCallingApiFormat: "OpenAIChatCompletions"` and a chat completions path such as `/v1/chat/completions`. Ollama runners can use `toolCallingApiFormat: "OllamaChat"` through `/api/chat` when the selected model supports tools. Providers with a native PolyPrompt tool-chat client (Azure OpenAI, xAI, Anthropic, Gemini, Cohere, Bedrock) use `toolCallingApiFormat: "PolyPromptNative"`, which is also the default for those API types. Azure OpenAI must use the native client because its operations are deployment-scoped (`/openai/deployments/{deployment}/chat/completions?api-version=...`) and authenticate with the `api-key` header rather than a bearer token. If a runner has tools disabled, lacks tool support, or returns a non-tool-capable response, Wilson keeps normal chat available and diagnostics explain why tools are unavailable.

The Settings page includes tool diagnostics for administrators. Validate checks draft tool settings before saving, Test adds selected-runner readiness checks without calling a model or executing tools, and MCP controls reconnect configured servers and show discovered tools.

The Prompts page lets tenant administrators create, edit, duplicate, delete, and set defaults for system prompts and tool prompts. In Chat, users select a system prompt and, for tool-capable chats, a tool prompt. Wilson sends the visible prompt text selected in Chat and records prompt IDs, names, default flags, and content hashes in request history. Tool prompts can include `{{tool_catalog}}`; the dashboard renders the catalog so the user can inspect what is going to the model.

Implemented built-in tools:

- Read/discover: `read_file`, `file_metadata`, `list_directory`, `glob`, `grep`
- Modify files/directories: `write_file`, `edit_file`, `multi_edit`, `delete_file`, `manage_directory`
- Process execution: `run_process`
- Web retrieval/search: `web_retrieve` for absolute `http` and `https` URLs, and `web_search` through the default DuckDuckGo HTML provider or configured Tavily/You.com-compatible providers
- MCP: external tools discovered from enabled stdio or streamable HTTP MCP servers. Wilson exposes them with OpenAI-safe server-prefixed names such as `docs__search`. An MCP call that fails, including a `tools/call` result that sets `isError: true` (for example, a schema violation), is returned to the model as an `mcp_call_failed` tool error.

Destructive and process tools are marked dangerous and approval-required. Keep allowed roots narrow, especially when using automatic approval for trusted admin-only workflows.

Tool audit records are redacted before persistence and before API responses. Chat responses use safe tool traces that omit raw arguments, raw tool output, provider tool-call IDs, and hidden policy details. Admin audit records may include redacted arguments and, only when `tools.storeFullToolResults` is explicitly enabled, redacted capped result payloads. The database layer uses parameterized commands for tenant, user, conversation, request-history, tool-run, and tool-call values.

## Telemetry

Wilson emits the three observability signals — metrics, traces, and logs — through the Radiant OpenTelemetry SDK. The webserver and every request path are instrumented, along with the chat and inference flow, the tool and agent loop, model-runner health checks, the database layer, and MCP. Wilson also captures .NET process and runtime metrics and the Watson webserver's own HTTP metrics. Everything ships over OTLP to an OpenTelemetry Collector, which fans metrics out to Prometheus, traces to Tempo, and logs to Loki, with Grafana on top. Logs carry the active trace and span IDs, so a log line links back to the trace that produced it.

The Docker stack brings this up for you and enables telemetry in `docker/wilson.json`. With the stack running, the consoles are:

- **Grafana** — `http://127.0.0.1:3000` (anonymous access with the Admin role; no login)
- **Prometheus** — `http://127.0.0.1:9090`
- **Tempo** — `http://127.0.0.1:3200` (query API; explore traces through Grafana)
- **Loki** — `http://127.0.0.1:3100` (query API; explore logs through Grafana)

The dashboard also links these under **Administration → External Services**.

Grafana is provisioned with dashboards organized by functional area: Overview, HTTP, Chat & Inference, Tools, Model Runners, Database, and MCP. Open one, and the panels use rates and p95/p99 latency quantiles rather than averages so a slow route or a failing tool stands out. In Explore, a Tempo trace shows the per-request server span with the chat, tool, and database calls nested underneath; clicking a span jumps to that request's logs in Loki.

Telemetry is controlled by the `telemetry` block in `wilson.json`. It is off by default and turned on in the Docker configuration, pointed at `http://otel-collector:4317`. The block sets the OTLP endpoint and protocol, the trace sampling ratio, which signals to emit, the minimum log severity, and whether process and runtime metrics are included. Telemetry changes are read at startup, so restart the server after editing them. For a quick look without a collector, set `prometheusScrapeEnabled` to `true` and Wilson exposes an in-process Prometheus endpoint on `prometheusScrapePort` (default `9464`) that you can scrape directly.

## API

- OpenAPI JSON: `http://127.0.0.1:9400/openapi.json`
- Swagger UI: `http://127.0.0.1:9400/swagger`
- Dashboard API Explorer: available inside the dashboard after login
- Model runner API types: `GET /v1.0/api/model-runners/api-types`
- Model server health: `GET /v1.0/api/model-runners/health`
- Single model server health: `GET /v1.0/api/model-runners/{id}/health`
- Tool catalog: `GET /v1.0/api/tools`
- Prompt templates: `GET /v1.0/api/prompts`, `POST /v1.0/api/prompts`, `GET/PUT/DELETE /v1.0/api/prompts/{id}`, `POST /v1.0/api/prompts/{id}/default`
- Conversation tool calls: `GET /v1.0/api/conversations/{id}/tool-calls`
- Request-history tool calls: `GET /v1.0/api/request-history/{id}/tool-calls`
- Tool run detail: `GET /v1.0/api/tool-runs/{id}`

Model server list responses also include the latest health snapshot when health checks are enabled.
Use `GET /v1.0/api/model-runners?includeLiveStatus=false` to return configured servers and cached health without waiting on upstream model list or loaded-model calls.
The dashboard refreshes model server health summaries and health detail modals every 15 seconds.
Chat tool activity is safe by default: raw model arguments, raw tool output, provider tool-call IDs, and secrets are not exposed in normal chat traces. Tool-call audit APIs return redacted, tenant-scoped records.

## SDKs And Postman

- C# SDK: `sdk/csharp`
- JavaScript SDK: `sdk/javascript`
- Python SDK: `sdk/python`
- Postman collection: `postman/Wilson.postman_collection.json`

## Tests And Checks

```powershell
dotnet build src\Wilson.slnx
dotnet run --project src\Test.Automated
dotnet test src\Wilson.slnx
cd dashboard
npm run lint
npm run build
```

## Filing Issues

Please file bugs and feature requests on GitHub:

https://github.com/jchristn/Wilson/issues

Useful issue details:

- Wilson version or commit SHA
- Operating system
- Backend URL/port
- Dashboard URL/port
- Database type
- Model runner type, for example Ollama, Ollama Cloud, OpenAI, Azure OpenAI, xAI, Anthropic, Gemini, Vertex AI, Bedrock, Cohere, VoyageAI, or Text Embeddings Inference
- Model name
- Steps to reproduce
- Expected behavior
- Actual behavior
- Relevant logs or screenshots

If Wilson stops talking back, include the logs. They are probably more useful than shouting at the horizon.

## License

Wilson is released under the MIT License. See [LICENSE.md](LICENSE.md).
