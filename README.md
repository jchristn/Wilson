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

- Chat with local Ollama models or OpenAI-compatible APIs
- Stream model responses over server-sent events
- Keep conversation history in a database
- Manage multiple configured model servers
- Check model server health in the background with thresholds and recent history
- Pull and load Ollama models from the dashboard
- Show which Ollama models are available and currently loaded
- Capture request history, response timing, and request/response payload metadata
- Collect thumbs-up/thumbs-down feedback and optional free-form comments
- Expose model-directed tools when explicitly enabled, with safe chat traces and persisted tool-call history
- Expose OpenAPI JSON and Swagger UI for the backend API

The waves never answer. Wilson does.

## Features

- **Dashboard chat**: browser-based chat with model server/model selectors, streaming responses, response timing details, feedback buttons, and conversation rename/delete.
- **Model server management**: configure Ollama, OpenAI, or OpenAI-compatible runners; inspect health, uptime, and recent health history; inspect available models; inspect loaded Ollama models; pull and load Ollama models.
- **Tenant-aware auth**: tenants, users, credentials, admin tokens, tenant admins, and bearer-token authentication.
- **Conversation storage**: saved conversations and messages backed by SQLite or PostgreSQL.
- **Request history**: latency summary, activity chart, detailed request/response metadata, headers, bodies, timing, and token estimates.
- **Tool activity**: optional model tool execution with safe inline chat activity, persisted tool runs/tool calls, request-history linkage, and dashboard settings controls.
- **Feedback review**: admin view for ratings, comments, related message IDs, and model timing fields.
- **Settings editor**: dashboard form for editing Wilson configuration without dumping raw JSON as the primary workflow.
- **API tools**: named API explorer, OpenAPI JSON at `/openapi.json`, and Swagger UI at `/swagger`.
- **Docker support**: Docker Compose setup, dashboard container, backend container, and factory reset scripts.
- **Local-first defaults**: backend defaults to port `9400`; dashboard defaults to port `9401`.

## Use Cases

- Run a private local chat dashboard for Ollama models
- Test and compare local and OpenAI-compatible model runners
- Give a small team tenant-aware access to shared local model infrastructure
- Capture operational request history while developing model-backed workflows
- Collect human feedback on model responses
- Manage Ollama model pulls and loaded-model state without leaving the dashboard
- Keep a conversation partner around when the island is quiet

## Quick Start

### Prerequisites

- .NET 10 SDK/runtime
- Node.js and npm
- Optional: Docker Desktop
- Optional: Ollama if you want local model inference

### Run The Backend

```powershell
dotnet run --project src\Wilson.Server
```

The backend defaults to:

```text
http://127.0.0.1:9400
```

On first start, Wilson creates `wilson.json` and seeds default credentials:

- Admin bearer token: `wilson-admin-dev-token`
- User access key: `wilsonadmin`

### Run The Dashboard

```powershell
cd dashboard
npm install
npm run dev
```

The dashboard defaults to:

```text
http://127.0.0.1:9401
```

On the login page:

- Server URL: `http://127.0.0.1:9400`
- Access key: `wilsonadmin` or `wilson-admin-dev-token`

Welcome to the island. Wilson's been expecting you.

## Docker

```powershell
cd docker
docker compose up --build
```

Docker exposes:

- Backend: `http://127.0.0.1:9400`
- Dashboard: `http://127.0.0.1:9401`

Factory reset scripts:

- Windows: `docker/factory/reset.bat`
- Linux/macOS: `docker/factory/reset.sh`

These reset Docker data and restore Docker settings from `docker/factory`.

## Configuration

Wilson reads settings from `wilson.json`.

Important sections:

- `rest`: listener hostname, port, and TLS flag
- `database`: SQLite/PostgreSQL settings
- `cors`: allowed origins, methods, and headers
- `auth`: admin bearer tokens and session lifetime
- `requestHistory`: request capture settings
- `tools`: global tool enablement, built-in tool policy, safety limits, allowed roots, web search, and MCP settings
- `modelRunners`: Ollama/OpenAI/OpenAI-compatible model servers
- `seed`: first-run tenant, user, and access key

Each `modelRunners` entry supports endpoint health checks:

- `healthCheckEnabled`: enables background probing
- `healthCheckUrl`: absolute URL or endpoint path; defaults to `/api/tags` for Ollama and `/v1/models` for OpenAI-compatible APIs
- `healthCheckMethod`: `GET` or `HEAD`
- `healthCheckIntervalMs`, `healthCheckTimeoutMs`
- `healthCheckExpectedStatusCode`
- `healthyThreshold`, `unhealthyThreshold`
- `healthCheckUseAuth`: sends the runner API key as a bearer token during probes

The dashboard Settings page edits the same configuration file. Some changes apply immediately; listener and database changes require a server restart.

Tools are disabled by default. To use built-in file tools, enable `tools.enabled`, configure `tools.workingDirectory`, and include at least one path in `tools.allowedRoots`. Individual model runners also have tool-capability controls (`toolsEnabled`, `supportsTools`, and `toolCallingApiFormat`) so runners that cannot speak a tool-call protocol continue to use normal chat.

The Settings page includes tool diagnostics for administrators. Validate checks draft tool settings before saving, and Test adds selected-runner readiness checks without calling a model or executing tools.

Implemented built-in tools:

- Read/discover: `read_file`, `file_metadata`, `list_directory`, `glob`, `grep`
- Modify files/directories: `write_file`, `edit_file`, `multi_edit`, `delete_file`, `manage_directory`
- Process execution: `run_process`

Destructive and process tools are marked dangerous and approval-required. Keep allowed roots narrow, especially when using automatic approval for trusted admin-only workflows.

## API

- OpenAPI JSON: `http://127.0.0.1:9400/openapi.json`
- Swagger UI: `http://127.0.0.1:9400/swagger`
- Dashboard API Explorer: available inside the dashboard after login
- Model server health: `GET /v1.0/api/model-runners/health`
- Single model server health: `GET /v1.0/api/model-runners/{id}/health`
- Tool catalog: `GET /v1.0/api/tools`
- Conversation tool calls: `GET /v1.0/api/conversations/{id}/tool-calls`
- Request-history tool calls: `GET /v1.0/api/request-history/{id}/tool-calls`
- Tool run detail: `GET /v1.0/api/tool-runs/{id}`

Model server list responses also include the latest health snapshot when health checks are enabled.
Use `GET /v1.0/api/model-runners?includeLiveStatus=false` to return configured servers and cached health without waiting on upstream model list or loaded-model calls.
The dashboard refreshes model server health summaries and health detail modals every 15 seconds.
Tool call records returned through chat and conversation APIs are safe summaries: raw model arguments, raw tool output, provider request IDs, and secrets are not exposed in normal chat traces.

## SDKs And Postman

- C# SDK: `sdk/csharp`
- JavaScript SDK: `sdk/javascript`
- Python SDK: `sdk/python`
- Postman collection: `postman/Wilson.postman_collection.json`

## Tests And Checks

```powershell
dotnet build src\Wilson.slnx
dotnet run --project src\Test.Automated
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
- Model runner type, for example Ollama or OpenAI-compatible
- Model name
- Steps to reproduce
- Expected behavior
- Actual behavior
- Relevant logs or screenshots

If Wilson stops talking back, include the logs. They are probably more useful than shouting at the horizon.

## License

Wilson is released under the MIT License. See [LICENSE.md](LICENSE.md).
