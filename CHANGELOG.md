# Changelog

## v0.1.1

Dependency update release. The Docker images were rebuilt in place under the existing `v0.1.0` tag and `latest`.

### Dependencies

- Updated PolyPrompt from 2.6.0 to 3.1.0, then to 3.4.0. Inference now uses the 3.x per-capability clients: `OllamaCompletionClient`/`OpenAiCompletionClient` for chat and tool chat, and `OllamaModelClient`/`OpenAiModelClient` for model listing. Chat options moved to `CompletionOptions`/`OllamaCompletionOptions`. Tool-chat model, temperature, top-p, and max tokens now go in `ToolChatRequest.Options`.
- Updated Voltaic from 2.0.0 to 2.2.1, Watson from 7.2.0 to 7.2.2, Touchstone (Core, Cli, XunitAdapter, NunitAdapter) from 0.1.12 to 0.2.0, and NUnit from 4.6.1 to 5.0.0.
- Updated PolyPrompt from 3.4.0 to 3.5.0, Voltaic from 2.2.1 to 2.4.0, Watson from 7.2.2 to 7.3.2, Microsoft.Data.Sqlite.Core from 10.0.12 to 10.0.14, PrettyId from 2.0.1 to 2.0.2, Radiant from 0.1.2 to 0.1.3, Touchstone (Core, Cli, XunitAdapter, NunitAdapter) from 0.2.0 to 0.2.1, Microsoft.NET.Test.Sdk from 18.10.1 to 18.11.0, xunit.runner.visualstudio from 4.0.0 to 4.0.1, and NUnit3TestAdapter from 6.3.0 to 6.3.1. Versions and image tags are unchanged; the images were rebuilt under `v0.1.0` and `latest`.
- Adopted the PolyPrompt 3.5.0 streaming contract: a stream is accepted only when its final `State` is `Completed`. A plain chat stream that ends without a provider completion signal (for example Ollama NDJSON with no `done:true`, a corrupt record, or a native error) now ends with an SSE `error` event, and the partial answer is not saved as a finished message. A streaming tool turn that does not complete is reported as failed, so provisional tool calls are never executed. The diagnostic comes from PolyPrompt's `StreamError`. Added the `streaming-requires-provider-completion` test.
- Bumped the server, dashboard, SDK, and OpenAPI document versions to `0.1.1`.

### Providers and endpoint types

- PolyPrompt 3.2.0 through 3.4.0 added endpoint types and providers beyond the original Ollama/OpenAI pair. Wilson now integrates all of them through a provider catalog (`Wilson.Core.Settings.ApiTypes`): Ollama, Ollama Cloud, OpenAI, OpenAI-compatible, Azure OpenAI, xAI Grok, Anthropic, Google Gemini, Google Vertex AI, AWS Bedrock, Cohere, VoyageAI, and Hugging Face Text Embeddings Inference.
- Added `ApiTypes`, `ToolCallingApiFormats`, and `ApiTypeDescriptor`. Descriptors carry default endpoints, default health paths and timings, per-provider default regions, capability flags for chat, embeddings, model listing, streaming, and tools, the default tool-calling format, and required credential fields.
- `InferenceService` now creates a per-provider PolyPrompt client for chat, embeddings, and model listing instead of switching between just Ollama and OpenAI. Credentials are provider-specific: static bearer token for Vertex AI, static AWS credentials or the AWS environment chain for Bedrock, required keys for hosted providers.
- Provider-specific completion options are built per API type: `OllamaCompletionOptions` for Ollama and Ollama Cloud, `OpenAiCompletionOptions` for OpenAI/OpenAI-compatible/Azure OpenAI, `XAiCompletionOptions`, `AnthropicCompletionOptions`, `GeminiCompletionOptions` for Gemini and Vertex AI, and `CohereCompletionOptions`.
- Added a `PolyPromptNative` tool-calling format that dispatches through the provider-native PolyPrompt tool-chat clients, which unlocks tool calls on xAI, Anthropic, Gemini, Cohere, Bedrock, and Azure OpenAI.
- `ValidateRunnerAsync` embedding checks now use the PolyPrompt embedding clients instead of hand-built provider HTTP calls, so embeddings work across every provider that supports them.

### Configuration

- `ModelRunnerSettings` gained `Deployment`, `ApiVersion`, `Region`, `ProjectId`, `ApiSecretKey`, and `AwsSessionToken`. `ApplyProviderDefaults` fills the provider endpoint, API version, and region defaults, and `ApplyHealthCheckDefaults`/`ApplyToolDefaults` are now descriptor-driven.
- Added `POST`-safe normalization (`WilsonServer.NormalizeModelRunner`) that trims provider fields and canonicalizes `apiType` casing so configuration never breaks on casing or whitespace.
- An unrecognized `apiType` keeps the legacy Ollama-family clients so existing configuration still chats. Cataloged providers that genuinely lack a capability fail explicitly instead of silently degrading.

### API

- Added `GET /v1.0/api/model-runners/api-types`, returning the provider catalog. It is documented in the OpenAPI document and Swagger UI, and `ApiTypeDescriptor` is a component schema.
- `ModelRunnerStatus` now reports `apiTypeDisplayName`, `apiTypeSupported`, `supportsChatCompletions`, `supportsEmbeddings`, `supportsModelListing`, `deployment`, `apiVersion`, `region`, and `projectId`.

### Dashboard

- Added `dashboard/src/utils/apiTypes.js`, a provider catalog mirroring the server's, with `describeApiType`, `normalizeApiType`, `isSupportedApiType`, `defaultHealthSettings`, `defaultToolSettings`, and `validateRunnerDraft`.
- Both model runner editors list all thirteen API types and render provider-specific fields (deployment, API version, project ID, region) only when the selected provider needs them. Save is blocked with a specific message when a required credential field is missing.
- Changing `apiType` in either model server editor now re-derives the new provider's defaults through one shared function. Selecting a provider from a fresh Ollama draft yields that provider's endpoint, health URL and timings, tool format, and capability flags instead of keeping Ollama's. Values still equal to the previous provider's defaults are replaced; a custom endpoint, a custom health URL or timing, and any control the operator disabled are preserved. Enablement follows the health URL the draft actually retains, so an enabled custom absolute URL keeps monitoring on even for providers with no default probe route (Vertex AI, Bedrock, VoyageAI). Embeddings-only providers (VoyageAI, Text Embeddings Inference) turn tool support off, and deployment/resource-derived providers (Azure OpenAI, Vertex AI, Bedrock) get their own required fields.
- The dashboard refuses to save, with an actionable message, a draft whose health checks are enabled but whose health URL cannot resolve to an absolute HTTP(S) URL.
- The model server card shows the provider display name, provider fields, and capability summary, and warns when `apiTypeSupported` is false.
- Runner draft validation no longer requires an endpoint for providers that derive one. Blank endpoints are valid for AWS Bedrock (regional endpoint from the AWS SDK) and Google Vertex AI (regional resource URL built from the project and region). A supplied endpoint must still be an absolute http or https URL.

### SDKs, documentation, Postman

- Added `ApiTypeDescriptor` and the new `ModelRunnerStatus` fields to the C# SDK, plus `GetModelRunnerApiTypesAsync`. Added `modelRunnerApiTypes()` to the JavaScript SDK and `model_runner_api_types()` to the Python SDK.
- Documented the provider catalog, `apiType` table, provider-specific configuration fields, and the `PolyPromptNative` tool format in `README.md`, `REST_API.md`, and `DOCKERHUB_README.md`.
- Added the API type catalog request to the Postman collection.

### Fixes

- Health probe batches now distinguish credentials using SHA-256 of the trimmed key, preserve path/query case and trailing slashes, and include the effective authentication headers and timeout. Different credentials or request targets no longer inherit another runner's healthy result; identical authenticated and anonymous requests still deduplicate.

- Health monitor grouping now includes the final provider request URL. Azure OpenAI runners using different API versions receive independent probe results instead of sharing a potentially false healthy result. A shared local HTTP regression checks both versions together, including the invalid version returning HTTP 400.

- Model server health probes now authenticate with each provider's own contract instead of always sending `Authorization: Bearer`. Anthropic probes send `x-api-key` plus `anthropic-version: 2023-06-01`, Gemini probes send `x-goog-api-key`, and Azure OpenAI probes send `api-key` together with the `api-version` query parameter its models route requires. Previously a healthy Anthropic, Gemini, or Azure OpenAI endpoint answered 401 and was reported unhealthy.
- Health check defaults are now usable for every advertised provider. Providers with no route Wilson can probe (Google Vertex AI, AWS Bedrock, VoyageAI) and runners whose endpoint is not an absolute HTTP(S) URL default to `healthCheckEnabled: false` instead of deriving an empty or relative URL. An explicitly configured absolute HTTP(S) health URL still wins, and a user-disabled check stays disabled. An unresolvable probe URL now produces an actionable error before any HTTP send instead of a request against a malformed URL.
- Endpoint-relative health check paths are accepted again. A leading-slash value such as `/api/tags` or `/health` is joined onto the endpoint and checks stay enabled, matching the dashboard. .NET parses those values as absolute `file` URIs, so the previous scheme rejection disabled monitoring for any runner using a custom probe path. An explicitly supplied absolute URL with a non-HTTP scheme, such as `file:///tmp/probe` or `ftp://example.com/tags`, is still rejected with an actionable error.
- Azure OpenAI tool calls now route through the provider-native PolyPrompt client, which builds `/openai/deployments/{deployment}/...` with an `api-version` query and authenticates with the `api-key` header. Previously Azure defaulted to the generic `OpenAIChatCompletions` sender, which posted `/v1/chat/completions` with a bearer token that Azure rejects. Saving an Azure runner that pins the generic format realigns it to `PolyPromptNative`.
- MCP `tools/call` results with `isError: true` are now reported as failed `mcp_call_failed` tool results instead of successes. Voltaic 2.2 returns input-schema violations, such as undeclared arguments, this way instead of as a JSON-RPC error.

### Build

- Added `build-all.sh`, `build-server.sh`, and `build-dashboard.sh`. They match the existing `.bat` scripts: multi-platform buildx push for `latest` plus a version tag, then a local pull.

### Tests

- Added `health check batch request identity`: fourteen local HTTP scenarios, each in both subscription and ID orders, exercise the production background batch path. Assertions cover request counts, URLs, headers, independent HTTP 401/400 results, Azure versions, URL case/slashes, authentication schemes, methods, expected statuses, timeouts, trimmed credentials, and positive authenticated/anonymous deduplication.

- Added a `PolyPrompt option mapping` test that covers tool-chat `Options` mapping, defaulting to client settings, and Ollama versus OpenAI completion option types.
- Added provider catalog tests covering canonicalization, case and whitespace tolerance, unrecognized-API-type fallback, per-provider health and tool defaults, provider-specific completion option types, credential validation for each hosted provider, and explicit capability failures.
- Added an `Azure OpenAI tool request shape` test that drives a real `InferenceService.ChatWithToolsAsync` call against a local fake Azure endpoint and asserts the deployment-scoped URL, the `api-version` query value, the `api-key` header, the absence of a bearer token, and that Azure's pinned generic tool format is realigned to `PolyPromptNative`.
- Added dashboard tests for the provider catalog: coverage of all thirteen API types, case-insensitive resolution, Ollama fallback, per-provider health and tool defaults, positive/negative runner draft validation, blank-endpoint acceptance for Bedrock and Vertex AI, and Azure's native tool format.
- Added `health check provider defaults coverage`, which asserts for all thirteen API types that defaults either resolve an absolute HTTP(S) probe URL or come back disabled with an explanation, that an explicit absolute URL and a user-disabled check survive normalization, and that derived-endpoint and invalid-URL cases are handled before any HTTP send.
- Added `health check provider auth headers`, which drives the real `ModelRunnerHealthCheckService` over local HTTP and asserts the outgoing method, path, query, authentication headers, and verdict for OpenAI, Ollama Cloud, xAI, Cohere, Anthropic, Gemini, Azure OpenAI, VoyageAI, and anonymous Text Embeddings Inference, with both a valid and an invalid credential per provider.
- Added `health check invalid URL sends nothing`, which asserts an unresolvable health URL fails with an actionable error, leaves checks disabled, and opens no connection.
- Added `health check relative path URL`, which asserts a leading-slash health path resolves to the joined endpoint URL, keeps checks enabled through normalization, reports no URL problem, drives a real local HTTP probe that lands on `/api/tags`, and still rejects explicit `file:`, `ftp:`, and `unix:` schemes with an actionable error.
- Added dashboard tests for `resolveHealthCheckUrl` and `validateRunnerDraft` on endpoint-relative health paths, asserting the dashboard joins a leading-slash path the same way the server does and only rejects explicit non-HTTP schemes.
- Added dashboard tests for `applyApiTypeDefaults` covering all provider transitions from a fresh Ollama draft, embeddings-only and resource-derived providers, preservation of custom endpoints, health settings, and disabled controls, and round-tripping back to the original provider. Additional cases assert that an enabled custom absolute health URL stays enabled when switching to VoyageAI, Vertex AI, or Bedrock, and that enablement is only dropped when the retained URL cannot resolve.
- Extended `health check provider defaults coverage` to assert that an explicitly enabled custom absolute health URL survives `ApplyHealthCheckDefaults` for all thirteen API types, including the three providers with no default probe route.

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
