# Changelog

## v0.1.1

Dependency update release. The Docker images were rebuilt in place under the existing `v0.1.0` tag and `latest`.

### Dependencies

- Updated PolyPrompt from 2.6.0 to 3.4.0 and integrated the newer provider and endpoint types it exposes.
- Updated Voltaic from 2.0.0 to 2.2.1, Watson from 7.2.0 to 7.2.2, Touchstone (Core, Cli, XunitAdapter, NunitAdapter) from 0.1.12 to 0.2.0, and NUnit from 4.6.1 to 5.0.0.
- Bumped the server, dashboard, SDK, and OpenAPI document versions to `0.1.1`.

### Model server providers

- Added an API type catalog (`Wilson.Core.Settings.ApiTypes`) covering `Ollama`, `OllamaCloud`, `OpenAI`, `OpenAICompatible`, `AzureOpenAI`, `Anthropic`, `Gemini`, `VertexAI`, `Bedrock`, `Cohere`, `XAI`, `TEI`, and `VoyageAI`, each with a default endpoint, default health path, and capability flags.
- Chat, streaming chat, tool chat, embeddings, and model listing now select the matching PolyPrompt 3.x client per API type instead of assuming Ollama or OpenAI.
- Added `deployment`, `apiVersion`, `region`, and `projectId` runner settings for Azure OpenAI, Vertex AI, and AWS Bedrock. `region` is resolved per provider: Vertex AI uses `GOOGLE_CLOUD_REGION`/`GOOGLE_CLOUD_LOCATION` and then `us-central1`, while AWS Bedrock uses `AWS_REGION`/`AWS_DEFAULT_REGION` and then `us-east-1`.
- Added the `PolyPromptNative` tool-calling format for providers that speak their own tool protocol, and made tool-call, parallel-tool, and streaming-tool defaults API-type aware.
- Added a read-only `GET /v1.0/api/api-types` endpoint, OpenAPI coverage, C#/JavaScript/Python SDK methods, and dashboard provider fields.
- Embedding validation now uses the PolyPrompt embedding clients instead of hand-built provider HTTP calls, which adds Azure OpenAI, Gemini, Vertex AI, Bedrock, Cohere, xAI, TEI, and VoyageAI embedding support.
- Unsupported API types are preserved in configuration, keep normal chat behavior, and have tools disabled. Chat, reasoning chat, and embeddings for an unrecognized `apiType` keep the pre-3.x Ollama-family clients, so an existing custom configuration is never silently broken. Cataloged providers that genuinely lack a capability, such as `TEI` and `VoyageAI` for chat or `Anthropic` for embeddings, still fail with an explicit error.
- The dashboard model server editor validates against provider requirements instead of requiring a nonblank endpoint for every API type. Vertex AI, AWS Bedrock, and every provider with a catalog default endpoint can be saved with a blank endpoint, while `OpenAICompatible` still requires one and `AzureOpenAI` requires a deployment or a configured model.

### Fixes

- Provider health-header regression checks now run over local TCP sockets in the default automated suite; the external Postman fixture remains an optional additional contract run.

- Both dashboard model-runner editors refresh protocol, capability, and health defaults when API type changes. Catalog-default endpoints switch to the new provider; custom endpoint/probe URLs and deliberately disabled controls are preserved.

- Bedrock static and environment credentials now receive the runner's resolved region for completion, embeddings, and model-management clients. Tests exercise real PolyPrompt constructors for every advertised provider capability and validate AWS credential resolution without network calls.

- Health probes use Anthropic, Gemini, and Azure API-key headers, include the Anthropic API version, and authenticate Ollama Cloud. VoyageAI no longer probes an unsupported model-list path. Provider headers are exercised against the local Postman HTTP fixture.

- Resource-derived cloud runners without a probeable endpoint disable default health checks and remain available. Explicit absolute health URLs remain supported; malformed probe URLs produce an actionable error before any network request. Dashboard defaults match the server.

- Region resolution is now provider-specific. `VertexAI` runners resolve `region` from the runner, then `GOOGLE_CLOUD_REGION`/`GOOGLE_CLOUD_LOCATION`, then the Google default `us-central1`; `Bedrock` runners keep the AWS chain (`AWS_REGION`/`AWS_DEFAULT_REGION`, then `us-east-1`). Previously both providers shared one resolver, so a Vertex AI runner with no region silently received `us-east-1` and PolyPrompt built the invalid endpoint `https://us-east-1-aiplatform.googleapis.com`. AWS region environment variables are no longer read for Vertex AI.
- The API type catalog now publishes a per-provider `defaultRegion` (`us-central1` for `VertexAI`, `us-east-1` for `Bedrock`, empty for every other provider), so the API, SDK, and dashboard can show the region a blank setting resolves to.
- MCP `tools/call` results with `isError: true` are now reported as failed `mcp_call_failed` tool results instead of successes. Voltaic 2.2 returns input-schema violations, such as undeclared arguments, this way instead of as a JSON-RPC error.

### Build

- Added `build-all.sh`, `build-server.sh`, and `build-dashboard.sh`. They match the existing `.bat` scripts: multi-platform buildx push for `latest` plus a version tag, then a local pull.

### Tests

- Added a `PolyPrompt option mapping` test that covers tool-chat `Options` mapping, defaulting to client settings, and Ollama versus OpenAI completion option types.
- Added API type catalog tests covering catalog completeness, provider capability flags, API-type-aware health and tool defaults, provider completion option selection, and rejection of unsupported values.
- Added an `API type unknown chat fallback` test that drives a fake Ollama chat endpoint through chat, reasoning chat, and tool chat for an unrecognized `apiType`, asserting normal chat still works, no tool definitions are sent, tools stay disabled, and a cataloged embeddings-only provider still refuses chat.
- Added dashboard tests for provider-aware runner validation: cloud runners with blank endpoints, providers with catalog default endpoints, Bedrock runners without configured models, and the negative cases for `OpenAICompatible`, `AzureOpenAI`, and missing identity fields.

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
