# Changelog

## v0.1.1

Dependency update release. The Docker images were rebuilt in place under the existing `v0.1.0` tag and `latest`.

### Dependencies

- Updated PolyPrompt from 2.6.0 to 3.1.0. Inference now uses the 3.x per-capability clients: `OllamaCompletionClient`/`OpenAiCompletionClient` for chat and tool chat, and `OllamaModelClient`/`OpenAiModelClient` for model listing. Chat options moved to `CompletionOptions`/`OllamaCompletionOptions`. Tool-chat model, temperature, top-p, and max tokens now go in `ToolChatRequest.Options`.
- Updated Voltaic from 2.0.0 to 2.2.1, Watson from 7.2.0 to 7.2.2, Touchstone (Core, Cli, XunitAdapter, NunitAdapter) from 0.1.12 to 0.2.0, and NUnit from 4.6.1 to 5.0.0.
- Bumped the server, dashboard, SDK, and OpenAPI document versions to `0.1.1`.

### Fixes

- MCP `tools/call` results with `isError: true` are now reported as failed `mcp_call_failed` tool results instead of successes. Voltaic 2.2 returns input-schema violations, such as undeclared arguments, this way instead of as a JSON-RPC error.

### Build

- Added `build-all.sh`, `build-server.sh`, and `build-dashboard.sh`. They match the existing `.bat` scripts: multi-platform buildx push for `latest` plus a version tag, then a local pull.

### Tests

- Added a `PolyPrompt option mapping` test that covers tool-chat `Options` mapping, defaulting to client settings, and Ollama versus OpenAI completion option types.

## v0.1.0

Initial published release (`jchristn77/wilson-server:v0.1.0` and `jchristn77/wilson-dashboard:v0.1.0`). Consolidates all development up to the first published images.

### Core platform

- Initial Wilson backend, dashboard, database, inference, Docker, and test implementation.
- Added Docker factory reset scripts and updated default backend/dashboard ports to 9400/9401.

### Dashboard

- Added dashboard branding, favicon, GitHub link, theme toggle, and improved topbar identity display.
- Added Model Servers page with Ollama available/loaded model status, model pull, model load, and model server CRUD.
- Added conversation management with rename/delete actions and a Conversations workspace page.
- Improved Chat streaming, model-load UX, response timing details, feedback comments, and invalid-model error handling.
- Added request history charts, detailed request/response metadata capture, collapsible payload sections, and copy/prettify controls.
- Added structured Settings forms, row-based list editing, API Explorer improvements, OpenAPI JSON, and Swagger UI.

### Model server health

- Added background model server health checks with configurable URL, method, interval, timeout, expected status, thresholds, and auth.
- Added model server health API routes and embedded health snapshots in model server list responses.
- Added a fast model server listing mode that returns configured servers and cached health without waiting on upstream model APIs.
- Added dashboard health summary metrics, health badges, recent health histograms, health detail modal, and health-check settings editors.

### Prompts and tools

- Added tenant-scoped system and tool prompt templates with seeded defaults, dashboard management, chat prompt selection, REST/OpenAPI coverage, SDK methods, and request-history prompt metadata.
- Added opt-in model tool calling with built-in file read/discovery, file/directory mutation, and process execution tools, runner capability controls, and dashboard tool enablement settings.
- Added persisted tool runs and redacted tool-call records linked to conversations, assistant messages, and request history.
- Added tool catalog, tool-run, conversation tool-call, and request-history tool-call REST/OpenAPI endpoints.
- Added compact dashboard tool activity in chat, conversation reload support, and request-history tool activity details.
- Added admin tool policy validation and readiness diagnostics endpoints, dashboard settings controls, OpenAPI coverage, automated route tests, SDK methods, and Postman requests.
- Added MCP tools discovered from enabled stdio or streamable HTTP MCP servers, via Voltaic 2.0.0. MCP servers built on Voltaic 2.x publish only application tools (no `ping`/`echo`/`getTime`/`getSessions` demo tools), answer `ping` with `{}`, and enforce `additionalProperties`/`patternProperties` in tool input schemas; undeclared arguments surface as `mcp_call_failed` tool errors.

### SDKs, API docs, and Postman

- Added C#, JavaScript, and Python SDK surfaces for model runner and model runner health APIs.
- Added C#, JavaScript, and Python SDK methods plus Postman requests for implemented tool metadata/history and diagnostics APIs.
- Added a Postman collection and updated configuration examples for health-check fields.
- Added `REST_API.md` coverage for tool enablement, safe chat traces, request-history metrics, tool-call reads, and diagnostics.
- Aligned the server, dashboard, SDK, and OpenAPI document versions to `0.1.0`.

### Dependencies and tests

- Updated Voltaic from 0.6.1 to 2.0.0, Watson to 7.2.0, PolyPrompt to 2.6.0, Microsoft.Data.Sqlite.Core to 10.0.12, and Microsoft.NET.Test.Sdk to 18.10.1.
- Expanded MCP tool tests: exact discovered-tool set, no legacy demo tools, lenient schemas accept extra arguments, strict schemas reject undeclared and missing required arguments.
- Made the PostgreSQL test container readiness wait deadline-based (120 seconds) and report the last connection error.
