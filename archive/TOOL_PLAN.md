# Wilson Tool-Calling Implementation Plan

This plan describes what is necessary to make Wilson expose tools to models and execute tool calls on behalf of the model, using the local Mux implementation in `C:\Code\Mux` as the baseline tool reference and the local AssistantHub implementation in `C:\Code\AssistantHub` as the closest product reference. Wilson should not depend on either product directly; port or reimplement the relevant concepts in Wilson-owned namespaces and product surfaces.

Use this file as a working checklist. Developers and agents should mark each item with progress notes, PR links, commit SHAs, or completion dates.

## Governing Requirements From `C:\Code\Agents\requirements`

The remaining implementation must follow the local Agents requirements in addition to the Wilson-specific tool plan.

- [~] Enforce backend code style from `CODE_STYLE.md`.
  - Use namespace-first C# files with `using` statements inside the namespace.
  - Do not introduce `var` or tuples.
  - Add XML documentation for all public types and public members.
  - Every new async public API must accept a `CancellationToken` and use `.ConfigureAwait(false)`.
  - Use specific exception types with contextual messages.
  - Progress: requirements reviewed on 2026-06-26; new tool work must follow these rules. Existing legacy code may need cleanup when touched.
- [~] Preserve tenant and authorization boundaries from `AUTHENTICATION.md` and `BACKEND_ARCHITECTURE.md`.
  - Tool runs, tool calls, approvals, request-history links, and audit views must always be tenant-scoped.
  - Admin routes may widen scope only through explicit admin checks.
  - Public chat traces must never expose raw secrets, raw arguments, raw outputs, provider IDs, or hidden policy details.
  - Progress: requirements reviewed on 2026-06-26; persistence and API slices must include tenant-scoped tests.
- [~] Keep dashboard work aligned with `FRONTEND_ARCHITECTURE.md` and `I18N.md`.
  - Settings, chat, request history, and explorer changes must remain responsive at desktop/tablet/mobile breakpoints.
  - New user-facing strings should be planned for the existing i18n runtime or migrated as part of the dashboard i18n cleanup.
  - Avoid new UI dependencies unless justified.
  - Progress: requirements reviewed on 2026-06-26; current dashboard still has hard-coded strings, so tool UI must be tracked in the broader i18n backlog.
- [~] Expand tests according to `BACKEND_TEST_ARCHITECTURE.md`.
  - Shared tests must stay self-contained, avoid console output, and throw specific exceptions on failure.
  - Tool persistence, security, redaction, request-history, SDK, and dashboard behavior must receive focused tests before completion.
  - Progress: requirements reviewed on 2026-06-26; next backend slices must add tests with the implementation.
- [~] Keep documentation human-authored per `WRITING_DOCUMENTS.md`.
  - README, REST API, SDK, and tool docs must use concrete Wilson language rather than generic generated prose.
  - Progress: requirements reviewed on 2026-06-26; documentation slices remain pending.

## Status Legend

- `[ ]` Not started
- `[~]` In progress
- `[x]` Complete
- `[!]` Blocked; add a note immediately after the item

## Reference Inventory From Mux

Implementation branch requirement:

- [x] Create and switch to a dedicated implementation branch before code changes: `git checkout -b feature/tools`.
  - Progress: created and switched to `feature/tools`.
- [x] Keep all original tool-calling implementation commits on `feature/tools`; do not do initial implementation work directly on `main`.
  - Progress: `feature/tools` was merged into `main`, pushed, verified, and deleted locally/remotely. Remaining plan completion now continues on `main` because the feature branch lifecycle has already been closed.

Use these Mux files as implementation references:

- `C:\Code\Mux\src\Mux.Core\Tools\BuiltInToolRegistry.cs`
- `C:\Code\Mux\src\Mux.Core\Tools\IToolExecutor.cs`
- `C:\Code\Mux\src\Mux.Core\Tools\ToolSafetyLimits.cs`
- `C:\Code\Mux\src\Mux.Core\Tools\Tools\*.cs`
- `C:\Code\Mux\src\Mux.Core\Agent\AgentLoop.cs`
- `C:\Code\Mux\src\Mux.Core\Agent\ToolCallProposedEvent.cs`
- `C:\Code\Mux\src\Mux.Core\Agent\ToolCallApprovedEvent.cs`
- `C:\Code\Mux\src\Mux.Core\Agent\ToolCallCompletedEvent.cs`
- `C:\Code\Mux\src\Mux.Core\Llm\GenericOpenAiAdapter.cs`
- `C:\Code\Mux\src\Mux.Core\Llm\OpenAiAdapter.cs`
- `C:\Code\Mux\src\Mux.Core\Llm\OllamaAdapter.cs`
- `C:\Code\Mux\src\Mux.Core\Tools\McpToolManager.cs`
- `C:\Code\Mux\src\Mux.Search\*`

Baseline built-in tools to replicate:

- `read_file`: read a file with line numbers, optional `offset` and `limit`, max file size protection.
- `write_file`: write full file content, create parent directories, preserve existing line endings.
- `edit_file`: one exact string replacement, fail on no match or multiple matches.
- `multi_edit`: multiple exact replacements in one file, validate before write, apply sequentially.
- `delete_file`: delete a file.
- `file_metadata`: return metadata for a file or directory.
- `list_directory`: list immediate child directories and files.
- `manage_directory`: create, delete recursively, or rename a directory.
- `glob`: search for files by glob pattern.
- `grep`: regex search across files with match limits.
- `run_process`: run a shell command with working directory, timeout, stdout/stderr capture, exit code, and truncation.
- `web_retrieve`: fetch a URL through Playwright and return rendered text, title, final URL, status, content type, optional HTML.
- `web_search`: optional external provider-backed search using Tavily/You.com-style providers.
- MCP tools: discover and execute external tools through stdio or streamable HTTP MCP servers, with server-prefixed tool names.

## Reference Inventory From AssistantHub

Use these AssistantHub files as implementation references for the Wilson product integration. AssistantHub has RAG-specific tools that Wilson does not need to copy, but its chat/tool loop, public progress events, audit persistence, endpoint capability checks, dashboard rendering, SDK coverage, and docs are directly relevant.

- `C:\Code\AssistantHub\src\AssistantHub.Core\Models\ToolCapableInferenceRequest.cs`
- `C:\Code\AssistantHub\src\AssistantHub.Core\Models\ToolCapableInferenceResponse.cs`
- `C:\Code\AssistantHub\src\AssistantHub.Core\Models\AssistantModelToolDefinition.cs`
- `C:\Code\AssistantHub\src\AssistantHub.Core\Models\AssistantModelToolCall.cs`
- `C:\Code\AssistantHub\src\AssistantHub.Core\Models\ChatCompletionMessage.cs`
- `C:\Code\AssistantHub\src\AssistantHub.Core\Models\ChatCompletionToolTrace.cs`
- `C:\Code\AssistantHub\src\AssistantHub.Core\Models\AssistantToolPolicy.cs`
- `C:\Code\AssistantHub\src\AssistantHub.Core\Models\AssistantToolDescriptor.cs`
- `C:\Code\AssistantHub\src\AssistantHub.Core\Models\AssistantToolProgressEvent.cs`
- `C:\Code\AssistantHub\src\AssistantHub.Core\Models\AssistantToolCallRecord.cs`
- `C:\Code\AssistantHub\src\AssistantHub.Core\Models\PartioEndpointToolMetadata.cs`
- `C:\Code\AssistantHub\src\AssistantHub.Core\Services\InferenceService.cs`
- `C:\Code\AssistantHub\src\AssistantHub.Core\Services\InferenceServiceResponseBase.cs`
- `C:\Code\AssistantHub\src\AssistantHub.Server\Services\AssistantChatService.cs`
- `C:\Code\AssistantHub\src\AssistantHub.Server\Services\AssistantChatServiceBase.cs`
- `C:\Code\AssistantHub\src\AssistantHub.Server\Services\AssistantToolRegistry.cs`
- `C:\Code\AssistantHub\src\AssistantHub.Server\Services\AssistantToolPolicyResolver.cs`
- `C:\Code\AssistantHub\src\AssistantHub.Server\Services\AssistantToolExecutor.cs`
- `C:\Code\AssistantHub\src\AssistantHub.Server\Services\AssistantToolArgumentValidator.cs`
- `C:\Code\AssistantHub\src\AssistantHub.Server\Services\AssistantToolOutputLimiter.cs`
- `C:\Code\AssistantHub\src\AssistantHub.Server\Services\AssistantToolAuditWriter.cs`
- `C:\Code\AssistantHub\src\AssistantHub.Server\Handlers\ChatHandler.cs`
- `C:\Code\AssistantHub\src\AssistantHub.Server\Handlers\AssistantSettingsHandler.cs`
- `C:\Code\AssistantHub\src\AssistantHub.Server\Handlers\AssistantToolCallHandler.cs`
- `C:\Code\AssistantHub\dashboard\src\components\ChatPanel.jsx`
- `C:\Code\AssistantHub\dashboard\src\components\modals\AssistantToolCallTraceSection.jsx`
- `C:\Code\AssistantHub\dashboard\src\views\AssistantSettingsView.jsx`
- `C:\Code\AssistantHub\REST_API.md`
- `C:\Code\AssistantHub\MCP_API.md`
- `C:\Code\AssistantHub\postman\AssistantHub.postman_collection.json`
- `C:\Code\AssistantHub\sdk\csharp\AssistantHub.Sdk\Models\ChatCompletionToolTrace.cs`
- `C:\Code\AssistantHub\sdk\js\src\types.ts`
- `C:\Code\AssistantHub\sdk\python\assistanthub_sdk\models.py`
- `C:\Code\AssistantHub\src\Test.Shared\ServiceSuite.cs`
- `C:\Code\AssistantHub\src\Test.Shared\ApiSuite.cs`

AssistantHub learnings to carry into Wilson:

- [ ] Keep model tool schemas, effective tool availability, execution, public progress events, and persisted audit records as separate concepts.
- [ ] Build a provider-neutral tool-capable inference request/response shape before changing chat UI.
- [ ] Add a resolver that explains why a tool is unavailable, not just a registry that omits it.
- [ ] Recheck policy and prerequisites inside the executor immediately before every dispatch.
- [ ] Validate model arguments with JSON Schema plus server-side allowed-property/type validation; models may send unknown fields or type-shaped strings.
- [ ] Apply both per-call and per-turn output limits before sending tool output back to the model.
- [ ] Split safe public traces from redacted admin audit records. Public chat should not expose raw arguments, raw output, provider IDs, object keys, or hidden policy details unless a deliberate admin-only view is being used.
- [ ] Emit lightweight lifecycle events while tools run: iteration started, call started, heartbeat/running, completed, failed, denied, and loop-guard stopped.
- [ ] Persist trace/request/conversation linkage even when the chat message ID is not known until after completion; AssistantHub links records by trace ID after chat history persistence.
- [ ] Add tool loop guards that stop repeated discovery/read cycles once enough evidence has been gathered or limits are reached, then ask the model for a best-effort final answer.
- [ ] Add endpoint capability metadata and diagnostics so admins can validate that a selected runner actually supports the intended tool-call wire format before users chat.
- [ ] Cover chat UI, history/request modals, analytics/request history, SDKs, REST docs, OpenAPI, MCP docs if applicable, Postman, and tests in the same feature rollout.

## Product Goals

- [ ] Models can receive tool definitions and return tool calls through OpenAI-compatible chat-completions semantics.
- [ ] Wilson can execute tool calls safely on behalf of the model, append tool results to model context, and continue the model loop until a final assistant response is produced.
- [ ] The dashboard chat experience shows active tool work inline without dominating the conversation.
- [ ] Expanded tool activity shows the tool name, status, arguments, approval state, start/end timestamps, runtime, success/failure, result preview, and error details.
- [ ] Public chat users see safe tool progress and summaries; admins can inspect redacted arguments and outputs in a deeper audit view.
- [ ] Tool calls and results are persisted with conversation history and remain visible after reload.
- [ ] Admins can configure tool availability, safety limits, working directories, allowed roots, web search, MCP servers, and default approval behavior.
- [ ] Existing chat behavior remains compatible for users with tools disabled.
- [ ] REST API, OpenAPI, SDKs, Postman, README, dashboard README, and tests all reflect the feature.

## Explicit Non-Goals For The First Implementation

- [ ] Do not introduce a direct dependency on Mux assemblies.
- [ ] Do not require all model runners to support tools. Runners without tool support must continue normal chat.
- [ ] Do not expose file/process tools by default without an explicit configured working directory and allowed root.
- [ ] Do not make the chat UI a separate agent console. Tool activity must be subordinate to the normal message flow.
- [ ] Do not copy AssistantHub's RAG/document-ingestion-specific tools into Wilson unless Wilson later adds equivalent data-management features.

## Phase 1: Core Models And Settings

Progress, 2026-06-25: foundational model contracts, settings, config defaults, runner capability metadata, server normalization, no-op catalog endpoints, and focused tests are implemented in the working tree. `dotnet build src\Wilson.slnx` and `dotnet run --project src\Test.Automated` pass with the pre-existing NU1903 SQLite package advisory.

### Tool Models

- [x] Add `src/Wilson.Core/Models/ToolDefinition.cs` or extend `Models.cs` with `ToolDefinition`.
  - Fields: `Name`, `Description`, `ParametersSchema`, `Category`, `BuiltIn`, `RequiresApproval`, `Dangerous`, `Enabled`.
  - JSON names should be camelCase through Wilson's existing serializer policy.
- [x] Add `ToolDescriptor`.
  - Fields: `Name`, `DisplayName`, `Category`, `EnabledByPolicy`, `Available`, `UnavailableReason`, `RequiresApproval`, `Dangerous`.
  - Use this for dashboard/settings diagnostics and `GET /tools`; do not send unavailable descriptors to the model.
- [x] Add `ModelToolDefinition`, `ModelToolFunctionDefinition`, `ModelToolCall`, and `ModelToolFunctionCall`.
  - Use AssistantHub's OpenAI-compatible function shape as the starting point.
  - Preserve provider-supplied tool-call IDs and raw function argument strings.
- [x] Add `ToolCall`.
  - Fields: `Id`, `Name`, `Arguments`, `ArgumentsJson`, `Status`, `CreatedUtc`.
  - Store model-provided arguments as the exact JSON string and expose parsed JSON only where needed.
- [x] Add `ToolResult`.
  - Fields: `ToolCallId`, `Success`, `Content`, `ContentJson`, `ErrorCode`, `ErrorMessage`, `Truncated`, `OutputBytes`.
- [x] Add `ToolExecutionRecord`.
  - Fields: `Id`, `TenantId`, `UserId`, `ConversationId`, `RunId`, `RequestHistoryId`, `TraceId`, `Origin`, `AssistantMessageId`, `ProviderToolCallId`, `ToolCallId`, `ToolName`, `Iteration`, `SequenceNumber`, `Status`, `ApprovalPolicy`, `ApprovedByUserId`, `ArgumentsJson`, `ResultJson`, `ResultSummaryJson`, `ResultPreview`, `Success`, `Denied`, `Truncated`, `OutputCharacters`, `InputBytes`, `OutputBytes`, `ErrorType`, `ErrorCode`, `ErrorMessage`, `Provider`, `Model`, `StartedUtc`, `CompletedUtc`, `ElapsedMs`, `CreatedUtc`, `UpdatedUtc`.
- [x] Add `ToolRun`.
  - Fields: `RunId`, `TenantId`, `UserId`, `ConversationId`, `RunnerId`, `Model`, `Status`, `StartedUtc`, `CompletedUtc`, `ElapsedMs`, `IterationCount`, `ToolCallCount`, `ErrorCount`.
- [x] Add `ToolProgressEvent`.
  - Fields: `EventType`, `ToolCallId`, `ToolName`, `DisplayLabel`, `StatusCode`, `Iteration`, `SequenceNumber`, `StartedUtc`, `CompletedUtc`, `ElapsedMs`, `ResultCount`, `Truncated`, `Denied`, `Success`, `Summary`.
  - Public payloads must be safe by default and must not include raw arguments or raw output.
- [x] Add `ToolTrace`.
  - Fields: `ToolCallId`, `ToolName`, `DisplayLabel`, `Iteration`, `SequenceNumber`, `Success`, `Denied`, `Truncated`, `OutputCharacters`, `ResultCount`, `ElapsedMs`, `Summary`, `StartedUtc`, `CompletedUtc`.
  - Use this for `ChatResponse.toolCalls` and final SSE `done` metadata.
- [x] Add `ToolCapableInferenceRequest` and `ToolCapableInferenceResponse`.
  - Request fields: `Messages`, `Model`, `MaxTokens`, `Temperature`, `TopP`, `Provider`, `Endpoint`, `ApiKey`, `Tools`, `ToolChoice`.
  - Response fields: `Success`, `Content`, `ToolCalls`, `FinishReason`, `ErrorMessage`, `Telemetry`.
  - This is the adapter boundary between Wilson's agent loop and provider-specific HTTP calls.
- [x] Add enums or string constants for:
  - Tool statuses: `proposed`, `pending_approval`, `approved`, `running`, `completed`, `failed`, `denied`, `cancelled`, `timed_out`.
  - Approval policies: `deny`, `ask`, `auto`.
  - Tool categories: `filesystem`, `process`, `web`, `search`, `mcp`, `wilson`, `custom`.
  - Tool choice modes: `auto`, `required`, `none`, `allowed_only`.
  - Tool event types: `tool_iteration.started`, `tool_iteration.stopped`, `tool_call.started`, `tool_call.heartbeat`, `tool_call.completed`, `tool_call.failed`, `tool_call.denied`.

### Settings

- [x] Add `ToolsSettings` under `Settings` in `src/Wilson.Core/Settings/Settings.cs`.
- [x] Add `Settings.Tools.Enabled`.
  - Default: `false` for generated settings and examples.
- [x] Add `Settings.Tools.BuiltInsEnabled`.
  - Default: `true`.
- [x] Add `Settings.Tools.DefaultApprovalPolicy`.
  - Default: `ask`.
  - Accepted values: `deny`, `ask`, `auto`.
- [x] Add `Settings.Tools.DestructiveToolsRequireApproval`.
  - Default: `true`.
  - Applies to `write_file`, `edit_file`, `multi_edit`, `delete_file`, `manage_directory` delete/rename, and `run_process`.
- [x] Add `Settings.Tools.BlockSecretPaths`.
  - Default: `true`.
  - Used by `WorkingDirectoryGuard` to block known secret-bearing directories and files unless an admin deliberately disables the guard.
- [x] Add `Settings.Tools.WorkingDirectory`.
  - Default: empty string.
  - If empty, file/process tools must be unavailable even when tools are globally enabled.
- [x] Add `Settings.Tools.AllowedRoots`.
  - Default: empty list.
  - All file and process working directories must resolve inside one of these roots.
- [x] Add `Settings.Tools.MaxAgentIterations`.
  - Default: `25`; clamp `1..100`.
- [x] Add `Settings.Tools.ToolTimeoutMs`.
  - Default: `30000`; clamp `1000..300000`.
- [x] Add `Settings.Tools.MaxToolIterations`.
  - Default: same as `MaxAgentIterations` initially; clamp `1..20`.
- [x] Add `Settings.Tools.MaxToolCallsPerTurn`.
  - Default: `12`; clamp `1..50`.
- [x] Add `Settings.Tools.ToolChoiceMode`.
  - Default: `auto`; accepted values: `auto`, `required`, `none`, `allowed_only`.
- [x] Add `Settings.Tools.AllowParallelToolCalls` and `Settings.Tools.MaxParallelToolCalls`.
  - Default: false and 1. Defer actual parallel execution until explicitly implemented.
- [x] Add `Settings.Tools.EmitProgressEvents`.
  - Default: `true`; controls safe SSE heartbeat/progress events.
- [x] Add `Settings.Tools.ExposeToolTracesToUsers`.
  - Default: `true` for Wilson dashboard users, but trace payload must be safe. Keep raw audit details behind auth-scoped audit endpoints.
- [x] Add `Settings.Tools.ProcessTimeoutMs`.
  - Default: `120000`; clamp `1000..600000`.
- [x] Add `Settings.Tools.MaxReadFileBytes`.
  - Default: `1048576`.
- [x] Add `Settings.Tools.MaxToolResultBytes`.
  - Default: `102400`.
- [x] Add `Settings.Tools.StoreToolResults`.
  - Default: `true`.
- [x] Add `Settings.Tools.StoreFullToolResults`.
  - Default: `false`; when false, persist only capped previews while still sending capped tool output to the model.
- [x] Add `Settings.Tools.StoreToolArguments`.
  - Default: `true`; always redact before persistence.
- [x] Add `Settings.Tools.MaxToolOutputChars`.
  - Default: `12000`; clamp `1024..200000`.
- [x] Add `Settings.Tools.MaxToolOutputCharsPerTurn`.
  - Default: `50000`; clamp at least `MaxToolOutputChars` and at most `500000`.
- [x] Add `Settings.Tools.MaxToolResultItems`.
  - Default: `20`; clamp `1..1000`.
- [x] Add `Settings.Tools.EnabledToolNames`.
  - Empty means all registered, safety-eligible tools are allowed.
- [x] Add `Settings.Tools.DisabledToolNames`.
  - Explicit deny-list that wins over enabled list.
- [x] Add `Settings.Tools.WebSearch`.
  - Fields: `Enabled`, `AllowFallback`, `Providers`.
  - Provider fields: `Name`, `ProviderType`, `Endpoint`, `ApiKey`, `Enabled`, `IsDefault`, `TimeoutMs`.
- [x] Add `Settings.Tools.Mcp`.
  - Fields: `Enabled`, `Servers`.
  - Server fields: `Name`, `Transport`, `Command`, `Args`, `Env`, `Url`, `McpPath`, `Enabled`.
- [x] Add per-runner tool capability settings to `ModelRunnerSettings`.
  - Fields: `ToolsEnabled`, `SupportsTools`, `ToolCallingApiFormat`, `SupportsParallelToolCalls`, `SupportsStreamingToolCalls`, `ChatCompletionsPath`.
  - Default `ToolsEnabled`: `true` so global `Settings.Tools.Enabled` controls feature exposure.
  - Default `SupportsTools`: `true` for OpenAI/OpenAICompatible; for Ollama, `true` only when `ToolCallingApiFormat` is configured as `OllamaChat` or the runner is configured as OpenAI-compatible with `/v1/chat/completions`.
  - Valid `ToolCallingApiFormat` values for first release: `OpenAIChatCompletions`, `OllamaChat`.
- [~] Update `NormalizeSettings` in `src/Wilson.Server/WilsonServer.cs`.
  - Normalize all tool settings.
  - Apply clamps.
  - Resolve environment-variable references for search provider API keys and MCP env values at runtime only; do not write expanded secrets back to disk.
  - Preserve backwards compatibility when `tools` is absent from old `wilson.json` files.
  - Progress: settings normalization, clamps, list cleanup, and backwards-compatible defaults are implemented. Runtime env-var resolution remains for the search/MCP executor phases.

### Configuration Files

- [x] Update `wilson.example.json`.
  - Add a disabled `tools` section with safe defaults.
  - Use empty working directory and allowed roots while tools are disabled; documentation will add explicit example roots when executors are available.
- [x] Update `docker/wilson.json`.
  - Add disabled `tools` settings.
  - Use empty working directory and allowed roots while tools are disabled; documentation will add `/workspace` examples when executors are available.
- [x] Review `docker/factory/wilson.json`.
  - It is app metadata, not server settings; only update if the factory metadata must point to new settings or ports.
  - Progress: reviewed; no change required because it references `../wilson.json` and existing ports.

## Phase 2: Tool Registry And Built-In Executors

Progress, 2026-06-25: Wilson-owned executor contracts, registry, safety limits, root guard, and low-risk filesystem tools are implemented and pass build plus automated tests. Destructive, process, web retrieval, search provider, and MCP execution remain pending.
Progress, 2026-06-26: remaining local filesystem executor slice is implemented for `write_file`, `edit_file`, `multi_edit`, `delete_file`, and `manage_directory`. These remain policy-controlled by working-directory/allowed-root checks and destructive-tool approval flags; process, web, search, and MCP tools remain separate pending slices. `dotnet build src\Wilson.slnx` and `dotnet run --project src\Test.Automated` pass with the existing SQLite advisory.
Progress, 2026-06-26: `run_process` executor slice is implemented. It executes a configured executable plus argument array without shell expansion, enforces working-directory allowed roots, applies timeout/cancellation handling, captures stdout/stderr/exit code, and truncates output. Web, search, and MCP tools remain separate pending slices. `dotnet build src\Wilson.slnx` and `dotnet run --project src\Test.Automated` pass with the existing SQLite advisory.

### Core Contracts

- [x] Add `src/Wilson.Core/Tools/IToolExecutor.cs`.
  - Properties: `Name`, `Description`, `ParametersSchema`, `Category`, `RequiresApproval`, `Dangerous`.
  - Method: `Task<ToolResult> ExecuteAsync(string toolCallId, JsonElement arguments, ToolExecutionContext context, CancellationToken token)`.
- [x] Add `ToolExecutionContext`.
  - Fields: `TenantId`, `UserId`, `ConversationId`, `RunId`, `WorkingDirectory`, `AllowedRoots`, `Settings`, `Logger` if introduced.
- [x] Add `ToolSafetyLimits`.
  - Use values from `Settings.Tools`; avoid mutable static values where possible.
- [x] Add `WorkingDirectoryGuard`.
  - Resolve relative and absolute paths.
  - Reject paths outside `AllowedRoots`.
  - Reject empty working directory for filesystem and process tools.
  - Reject known secret-bearing path segments by default, including `.ssh`, `.aws`, `.azure`, `.gcp`, `.kube`, `.docker`, `.gnupg`, `secrets`, and `credentials`, unless an admin explicitly disables this guard for a trusted private deployment.
  - Reject known secret-bearing files by default, including `.env*`, `.npmrc`, `.pypirc`, `nuget.config`, `web.config`, `app.config`, `appsettings*.json`, `connectionstrings.json`, `credentials.json`, `service-account.json`, `*.pem`, `*.pfx`, `*.p12`, and `*.key`.
  - Return structured errors rather than writing to console.
- [~] Add `BuiltInToolRegistry`.
  - Register all safe built-ins.
  - Register `web_search` only when configured and at least one enabled provider is valid.
  - Expose `GetToolDefinitions`.
  - Expose `HasTool`.
  - Route `ExecuteAsync`.
  - Filter by `EnabledToolNames` and `DisabledToolNames`.
  - Progress: registry routes implemented low-risk built-ins and exposes definitions; name filtering is handled by `ToolPolicyResolver`; web_search registration remains pending.
- [~] Add `ToolPolicyResolver`.
  - Return all descriptors when asked for diagnostics and only available descriptors for model exposure.
  - Include non-secret `UnavailableReason` values such as disabled by policy, missing working directory, missing allowed root, missing web-search provider, MCP server disconnected, or runner not tool-capable.
  - Apply final `EnabledToolNames` and `DisabledToolNames` filters after prerequisite checks.
  - Progress: implemented built-in/global enablement, name filters, filesystem prerequisites, and model-vs-diagnostic filtering. Web-search, MCP, and runner-specific diagnostics remain pending.
- [~] Add `ToolArgumentValidator`.
  - Require each tool argument payload to be a JSON object.
  - Reject unknown properties per tool, even if the provider accepted the JSON Schema.
  - Deserialize to typed argument classes where practical to catch malformed numbers, booleans, arrays, and nested objects.
  - Accept common model conveniences deliberately, such as numeric strings for numeric fields, only when the tool explicitly supports them.
  - Progress: shared `ToolJson` helper validates object shape, rejects unknown fields, and validates typed string/int arguments for implemented tools. Rich typed argument classes remain pending.
- [~] Add `ToolOutputLimiter`.
  - Apply per-call serialized output limit before model feedback.
  - Apply remaining per-turn output limit before appending `role: "tool"` messages.
  - When truncating, return valid JSON containing `truncated`, `originalCharacters`, and `content`.
  - Progress: per-call truncation is implemented in `ToolResultFactory`; per-turn budgeting is implemented in the agent loop and returns valid JSON truncation payloads before tool output is appended to model context.
- [ ] Add `ToolAuditWriter`.
  - Build persisted arguments according to `StoreToolArguments`.
  - Build persisted output according to `StoreFullToolResults`.
  - Always produce a compact result summary JSON for request history and admin lists.
  - Redact field names containing `apiKey`, `password`, `secret`, `token`, `credential`, `bearer`, `accessKey`, `signedUrl`, `connectionString`, and obvious case/underscore/hyphen variants.
  - Add a model-visible redaction pass that preserves safe continuation/pagination tokens when needed.
- [~] Recheck policy in the executor.
  - Every `ExecuteAsync` call must normalize the tool name, resolve current effective policy, reject unknown or unavailable tools, enforce timeout, validate arguments, dispatch, apply provider telemetry, limit output, and return a structured result.
  - Progress: `ToolService.ExecuteAsync` rejects unknown/unavailable tools before dispatch, enforces configured tool timeout, and executors return structured results. Provider telemetry remains pending.

### Filesystem Tools

- [x] Implement `read_file`.
  - Parameters: `file_path`, `offset`, `limit`.
  - Enforce allowed roots.
  - Enforce `MaxReadFileBytes`.
  - Return line-numbered content.
  - Return JSON error for missing files, oversize files, invalid arguments, and permission failures.
  - Progress: executor implemented and automated tests pass.
- [x] Implement `write_file`.
  - Parameters: `file_path`, `content`.
  - Enforce allowed roots.
  - Preserve existing line endings.
  - Create parent directories only inside allowed roots.
  - Return path and line count.
- [x] Implement `edit_file`.
  - Parameters: `file_path`, `old_string`, `new_string`.
  - Enforce exactly one match.
  - Preserve line endings.
  - Return candidate line numbers on ambiguous match.
- [x] Implement `multi_edit`.
  - Parameters: `file_path`, `edits[]`.
  - Validate all edits before writing.
  - Apply sequentially to working content.
  - Return edit count and new line count.
- [x] Implement `delete_file`.
  - Parameters: `file_path`.
  - Enforce allowed roots.
  - Return structured success/failure.
- [x] Implement `file_metadata`.
  - Parameters: `path`.
  - Enforce allowed roots.
  - Return type, size, timestamps, attributes, and shallow counts for directories.
  - Progress: executor implemented and build validation passes.
- [x] Implement `list_directory`.
  - Parameters: `path`.
  - Enforce allowed roots.
  - Return stable sorted output.
  - Add a future-compatible `max_entries` argument even if defaulted.
  - Progress: executor implemented with `max_entries` and build validation passes.
- [x] Implement `manage_directory`.
  - Parameters: `action`, `path`, `new_path`.
  - Supported actions: `create`, `delete`, `rename`.
  - Enforce allowed roots for source and destination.
  - Treat recursive delete as dangerous and approval-required.
- [x] Implement `glob`.
  - Parameters: `pattern`, `path`.
  - Enforce allowed roots.
  - Limit max returned entries; include truncation flag.
  - Skip unreadable paths without failing the entire call unless the root itself is unreadable.
  - Progress: executor implemented with `max_results` and build validation passes.
- [~] Implement `grep`.
  - Parameters: `pattern`, `path`, `include`.
  - Enforce allowed roots.
  - Compile regex with timeout.
  - Limit matches.
  - Skip binary/unreadable files.
  - Progress: executor implemented with `max_matches`; warning/count behavior for unreadable files remains pending.

### Process Tool

- [x] Implement `run_process`.
  - Parameters: `command`, `args`, `working_directory`, `timeout_ms`.
  - Enforce process working directory inside allowed roots.
  - Use Windows-safe execution on Windows and POSIX-safe execution on Linux/macOS.
  - Capture stdout, stderr, exit code, timeout flag.
  - Kill process tree on timeout/cancellation.
  - Truncate stdout/stderr separately.
  - Mark dangerous and approval-required by default.
  - Add a setting to disable this tool independently even when other built-ins are enabled.
  - Progress: executor is implemented and approval-required/dangerous. Independent per-tool disabling is currently available through `DisabledToolNames`; a dedicated process-only switch remains pending.

### Web Tools

- [ ] Add `Microsoft.Playwright` package to `src/Wilson.Core/Wilson.Core.csproj` or a new `Wilson.Tools` project.
- [ ] Implement `web_retrieve`.
  - Parameters: `url`, `browser`, `wait_until`, `timeout_ms`, `max_content_chars`, `include_html`.
  - Allow only absolute `http` and `https` URLs.
  - Default to Chromium and `domcontentloaded`.
  - Install missing browser on first run only when allowed by settings.
  - Return URL, final URL, title, HTTP status, content type, text, truncation flags, and optional HTML.
  - Add a setting to disable automatic Playwright browser install in locked-down deployments.
- [ ] Add web search models and services.
  - Either port Mux's search provider abstractions into `src/Wilson.Core/Search` or create `src/Wilson.Search`.
  - Implement provider clients for Tavily and You.com compatible with Mux semantics.
  - Support provider selection, fallback, freshness, domains, images, offset, and provider-generated answers where available.
- [ ] Implement `web_search`.
  - Register only when `Settings.Tools.WebSearch.Enabled` and at least one provider is valid.
  - Return structured search results with title, URL, snippet, source provider, and optional images/answer.

### MCP Tools

- [ ] Decide dependency strategy for MCP.
  - Preferred: add `Voltaic` package to Wilson and port Mux's `McpToolManager`.
  - Alternative: implement minimal JSON-RPC stdio/HTTP client in Wilson-owned code.
- [ ] Add MCP transport enum: `stdio`, `http`.
- [ ] Implement MCP server initialization when `Settings.Tools.Mcp.Enabled`.
  - Launch enabled stdio servers.
  - Connect to enabled HTTP servers.
  - Discover tools with `tools/list`.
  - Prefix names as `{serverName}.{toolName}`.
  - Redact secrets in status responses.
- [ ] Implement MCP tool execution.
  - Route prefixed tool calls to the correct server.
  - Call `tools/call`.
  - Enforce timeouts.
  - Return structured failure when server is disconnected.
- [ ] Add lifecycle handling.
  - Start MCP connections during server startup.
  - Refresh when settings are updated.
  - Dispose/stop MCP clients on shutdown.

## Phase 3: Tool-Aware Inference And Agent Loop

Progress, 2026-06-25: provider-neutral, non-streaming tool-capable inference transport is implemented in `InferenceService` for OpenAI-compatible chat completions and Ollama native chat. Parser tests pass. This slice does not switch user chat traffic to the tool loop yet.

### Protocol Transport

- [~] Add a Wilson-owned chat-completions transport.
  - Do not rely only on PolyPrompt for tool calls because Wilson must preserve assistant tool calls and tool result messages.
  - Support OpenAI-compatible request/response shapes.
  - Support streaming SSE and non-streaming JSON.
  - Progress: non-streaming JSON transport implemented; streaming tool-call transport remains pending.
- [~] Add request builder.
  - Convert Wilson conversation history to OpenAI-compatible `messages`.
  - Include `tools` as function definitions when enabled and supported.
  - Include a system instruction block for tool behavior, scoped to Wilson's tools. It must say tool outputs are untrusted, broad enumeration should be summarized rather than dumped, secret/policy details must not be revealed, and final answers should use available evidence when tool limits are reached.
  - Include `parallel_tool_calls: true` only when runner supports it.
  - Include model, temperature, top_p, max_tokens, and runner-specific fields.
  - Progress: provider message conversion, tool definition inclusion, parallel flag, and model/options fields are implemented. Tool-specific system instruction injection remains pending for the agent-loop slice.
- [x] Normalize runner URLs.
  - For `OpenAI`, default path should be `/v1/chat/completions` when endpoint is `https://api.openai.com`.
  - For `OpenAICompatible`, document whether endpoint is API root or chat-completions root; make `ChatCompletionsPath` explicit to remove ambiguity.
  - For `Ollama`, support native `OllamaChat` through `/api/chat` when selected, and support `/v1/chat/completions` only when the runner is configured as OpenAI-compatible.
  - Progress: `ChatCompletionsPath` and full `/chat/completions` endpoints are normalized; `OllamaChat` uses `/api/chat`.
- [x] Add response parser.
  - Parse assistant text.
  - Parse `tool_calls`.
  - Preserve `tool_call_id`, function name, and raw arguments.
  - Handle malformed or partial argument JSON gracefully.
  - Normalize provider differences:
    - OpenAI-compatible responses use `choices[].message.tool_calls`.
    - Ollama chat responses may use `message.tool_calls`, function arguments as objects, and no OpenAI tool-call ID.
    - Missing finish reason with non-empty tool calls should be treated as `tool_calls`.
  - Progress: non-streaming parser implemented and covered for OpenAI-compatible string arguments and Ollama raw object arguments.
- [ ] Add streaming parser.
  - Stream text deltas immediately.
  - Accumulate `tool_calls` deltas by index.
  - Emit complete tool calls on `finish_reason=tool_calls`, `[DONE]`, or stream end.
  - Handle backends that send complete tool calls in one chunk.
- [ ] Keep existing `InferenceService.ChatAsync` and `ChatStreamingAsync` for tools-disabled compatibility.
  - Route tool-enabled requests to the new agent loop.
  - Existing tests and API clients must continue to pass when tools are disabled.
- [ ] Add optional dedicated tool-routing runner support.
  - Setting: `Tools.ToolRoutingRunnerId` or equivalent; empty means use the response runner.
  - Validate that both the routing runner and final response runner are active and compatible.
  - If a dedicated routing runner returns no tool calls, call the final response runner to produce the user-facing answer.
  - If the final response runner unexpectedly returns tool calls, either execute them through the same loop or reject with a structured configuration error; choose and document one behavior.

### Agent Loop

- [x] Add `ToolAgentService` or `AgentLoopService` under `src/Wilson.Core/Services`.
  - Progress: `ToolAgentService` added with injectable non-streaming inference delegate and production constructor for `InferenceService`.
- [~] Inputs:
  - runner, model, existing message history, latest user prompt, completion settings, tool settings, tenant/user/conversation IDs, cancellation token.
  - Progress: core loop accepts runner, model, model-chat messages, completion settings, tool execution context, and cancellation token. Server chat request mapping remains pending.
- [ ] Build initial conversation:
  - System prompt from `CompletionRequestSettings.SystemPrompt`.
  - Prior Wilson messages converted to roles: `system`, `user`, `assistant`, `tool`.
  - Prior assistant tool call metadata and tool result messages must round-trip correctly.
  - Latest user prompt appended as a user message.
- [ ] Context management:
  - Extend current truncation logic to include tool call and tool result messages.
  - Tool definitions consume context; account for them in token estimates.
  - Preserve the latest complete tool-call sequence when trimming history.
  - Never send a tool result without the assistant tool call that requested it.
- [~] Run loop:
  - Send model request with available tools.
  - Stream assistant text to caller.
  - Persist or buffer assistant message content.
  - If no tool calls, finish.
  - If tool calls exist, process each one.
  - Emit proposed, approval, running, completed/failed/denied events.
  - Append tool result messages to model context.
  - Continue until final assistant answer or `MaxAgentIterations`.
  - Add one-based `Iteration` and `SequenceNumber` to every progress event and persisted record.
  - When a tool limit is reached, append a structured denial/limit output to the model and request a best-effort final answer from available evidence.
  - If the final best-effort model call fails or returns empty content, return a clear fallback assistant message and still persist tool traces.
  - Progress: non-streaming core loop implemented and tested with a fake model/tool round trip. Streaming, approval events, persistence, best-effort limit fallback, and server wiring remain pending.
- [~] Tool execution:
  - Parse raw arguments as JSON.
  - Reject unknown or disabled tools.
  - Enforce approval policy.
  - Enforce dangerous-tool approval.
  - Measure elapsed milliseconds per tool.
  - Capture started/completed timestamps.
  - Truncate result content before sending back to model according to `MaxToolResultBytes`.
  - Redact model-visible tool JSON before appending it to context.
  - Track model-visible output characters for both per-call and per-turn limits.
  - Track output bytes separately from output characters for audit and analytics.
  - Add provider-specific telemetry to tool results where available, such as web-search provider latency or credits.
  - Progress: JSON argument parsing, unknown/unavailable rejection via `ToolService`, elapsed timing, timestamps, tool-result message append, and safe trace creation are implemented for the non-streaming core loop. Approval and audit-grade redaction remain pending.
- [ ] Approval behavior:
  - `deny`: do not run; append tool result explaining denial.
  - `auto`: run without user intervention unless dangerous tool requires approval by settings.
  - `ask`: pause the agent loop and wait for dashboard/API approval.
- [ ] Approval timeout:
  - Add setting `Tools.ApprovalTimeoutMs`, default `300000`.
  - If timeout expires, mark denied and append a denial result.
- [ ] Cancellation:
  - Existing dashboard Stop button must cancel the active model stream and any active tool process/browser retrieval.
  - Mark active tool calls and runs `cancelled`.
- [ ] Completion metrics:
  - Track model time to first token.
  - Track model streaming time.
  - Track tool-routing model-call time separately from final-response model-call time.
  - Track total run time.
  - Track aggregate tool count and aggregate tool elapsed milliseconds.
  - Track iteration count and errors.
- [ ] Add loop guard rules before starting the next model/tool iteration.
  - Stop when enough tool evidence has already been gathered, based on `MaxToolOutputCharsPerTurn`.
  - Stop repeated discovery/listing cycles after multiple successful discovery calls.
  - Stop repeated read cycles after multiple successful evidence reads.
  - Stop additional reads after a read failure when earlier evidence already exists.
  - Emit a safe `tool_iteration.stopped` event, then call the final model with an instruction to answer using available evidence.

## Phase 4: Persistence And Database Migration

Progress, 2026-06-26: persistence/API/history reload slice is implemented in the working tree. Tenant-scoped `toolruns` and `toolcalls` storage, non-streaming tool-call linkage to final assistant messages, request-history metrics/linkage, read APIs, dashboard conversation reload, request-history tool activity, OpenAPI paths, and focused database tests are added. `dotnet build src\Wilson.slnx` and `dotnet run --project src\Test.Automated` pass with the existing SQLite advisory. Approval, streaming live events, MCP, web/search, and destructive/process tools remain separate pending slices unless completed later in this run.

### Schema

- [x] Update `DatabaseDriver.InitializeAsync`.
- [x] Add columns to `messages`:
  - `runid TEXT NOT NULL DEFAULT ''`
  - `toolcallsjson TEXT NOT NULL DEFAULT ''`
  - `toolcallid TEXT NOT NULL DEFAULT ''`
  - `metadatajson TEXT NOT NULL DEFAULT ''`
- [x] Add table `toolruns`.
  - Columns: `rowid`, `id`, `tenantid`, `userid`, `conversationid`, `runnerid`, `model`, `status`, `startedutc`, `completedutc`, `elapsedms`, `iterationcount`, `toolcallcount`, `errorcount`, `createdutc`.
  - Unique key on `id`.
  - Index on `tenantid,conversationid,createdutc`.
- [x] Add table `toolcalls`.
  - Columns: `rowid`, `id`, `tenantid`, `userid`, `conversationid`, `runid`, `requesthistoryid`, `traceid`, `origin`, `assistantmessageid`, `providertoolcallid`, `toolcallid`, `toolname`, `iteration`, `sequencenumber`, `status`, `approvalpolicy`, `approvedbyuserid`, `argumentsjson`, `resultjson`, `resultsummaryjson`, `resultpreview`, `success`, `denied`, `truncated`, `outputcharacters`, `inputbytes`, `outputbytes`, `errortype`, `errorcode`, `errormessage`, `provider`, `model`, `startedutc`, `completedutc`, `elapsedms`, `active`, `createdutc`, `updatedutc`.
  - Unique key on `id`.
  - Index on `tenantid,conversationid,runid`.
  - Index on `tenantid,assistantmessageid`.
  - Index on `tenantid,traceid`.
  - Index on `tenantid,requesthistoryid`.
  - Index on `tenantid,toolname,createdutc`.
  - Index on `tenantid,success,createdutc`.
- [x] Add columns to `requesthistory`:
  - `toolcallcount INTEGER NOT NULL DEFAULT 0`
  - `toolelapsedms REAL NOT NULL DEFAULT 0`
  - `agentiterations INTEGER NOT NULL DEFAULT 0`
- [~] Verify SQLite and PostgreSQL DDL compatibility.
  - Progress: additive DDL uses SQL accepted by SQLite and PostgreSQL (`CREATE TABLE IF NOT EXISTS`, `CREATE INDEX IF NOT EXISTS`, text timestamps, integer booleans). Automated validation covers SQLite; PostgreSQL manual/integration validation remains pending.
- [x] Add read helpers that tolerate absent columns for old databases during rolling upgrades.

### Database Methods

- [x] Add `CreateToolRunAsync`.
- [x] Add `UpdateToolRunAsync`.
- [x] Add `GetToolRunAsync`.
- [x] Add `GetToolRunsForConversationAsync`.
- [x] Add `CreateToolCallAsync`.
- [x] Add `UpdateToolCallAsync`.
- [x] Add `GetToolCallAsync`.
- [x] Add `GetToolCallsForConversationAsync`.
- [x] Add `GetToolCallsForMessageAsync`.
- [x] Add `GetToolCallsForRequestHistoryAsync`.
- [x] Add `AttachToolCallsToMessageByTraceIdAsync`.
  - Use this when tool records are created before the final assistant message row exists.
- [x] Add `DeleteExpiredToolCallsAsync`.
  - Retention should follow existing request history retention unless a tool-specific retention setting is added.
- [x] Update `CreateMessageAsync`, `ReadMessage`, and `ChatMessage`.
  - Include new columns.
  - Existing callers can leave new fields empty.
- [x] Update conversation delete.
  - Delete related `toolcalls` and `toolruns` before deleting the conversation.
- [x] Update request history create/read.
  - Include tool metrics.

### Retention And Redaction

- [x] Cap persisted `ArgumentsJson`, `ResultJson`, and `ResultPreview`.
  - Progress: non-streaming persisted records store `{}` for arguments and capped safe summaries/previews for results. Raw argument/result audit storage remains pending with `ToolAuditWriter`.
- [ ] Redact obvious secrets in tool arguments and results:
  - bearer tokens
  - API keys
  - passwords
  - environment variable values configured as secrets
- [x] Store a redacted `ResultSummaryJson` even when full result persistence is disabled.
  - Include success, tool name, denied, truncated, output character count, duration, and safe error.
- [ ] Do not persist full stdout/stderr, retrieved web HTML, or raw process output unless `StoreFullToolResults` is explicitly enabled.
- [x] Ensure public chat response traces never use `ArgumentsJson`, `ResultJson`, or provider-specific raw identifiers.
  - Progress: non-streaming chat responses now remap tool traces to Wilson-generated internal tool-call IDs and omit raw arguments/results.
- [ ] Add tests proving redaction happens before request history and tool call persistence.
  - Progress: database persistence/linkage/isolation/retention tests are added; explicit secret-redaction corpus tests remain pending for `ToolAuditWriter`.

## Phase 5: REST API And SSE Contracts

### Chat Request/Response

- [x] Extend `ChatRequest`.
  - `ToolsEnabled` nullable bool; null means server default.
  - `ApprovalPolicy` nullable string; null means server default.
  - `ToolNames` optional list to narrow tools for this request.
  - `WorkingDirectory` optional string; only admins may override, and it must be inside allowed roots.
  - Progress: server DTO now accepts `toolsEnabled`, `approvalPolicy`, `toolNames`, and admin-only `workingDirectory` override.
- [~] Extend `ChatResponse`.
  - Include `toolRun`.
  - Include `toolCalls` as safe `ToolTrace` metadata only.
  - Include aggregate tool metrics.
  - Never include raw `argumentsJson`, raw `resultJson`, provider API details, or hidden policy details in public chat responses.
  - Progress: non-streaming chat response now includes `toolRun`, safe `toolCalls`, and aggregate `toolMetrics` when tools are used. Persistence-backed reload/history remains pending.
- [ ] Preserve existing response fields:
  - `conversation`
  - `userMessage`
  - `assistantMessage`
  - `truncation`

### SSE Events

- [ ] Keep current events for compatibility:
  - `conversation`
  - `truncation`
  - `chunk`
  - `done`
  - `error`
- [ ] Add `run_started`.
  - Payload: run ID, runner ID, model, tools enabled, effective tool count, approval policy, max iterations.
- [ ] Add `tool_call_proposed`.
  - Payload: run ID, tool call ID, tool name, arguments preview, dangerous flag, requires approval flag.
- [ ] Add `tool_call_pending_approval`.
  - Payload: run ID, tool call ID, approval endpoint, timeout timestamp.
- [ ] Add `tool_call_approved`.
  - Payload: run ID, tool call ID, approved by user ID or system.
- [ ] Add `tool_call_denied`.
  - Payload: run ID, tool call ID, reason.
- [ ] Add `tool_call_running`.
  - Payload: run ID, tool execution record ID, tool call ID, tool name, started UTC.
- [ ] Add `tool_call_heartbeat`.
  - Payload: run ID, tool execution record ID, tool call ID, tool name, started UTC, current elapsed milliseconds.
  - Emit at a low frequency, for example every five seconds, and stop when the tool completes or is cancelled.
- [ ] Add `tool_call_completed`.
  - Payload: run ID, tool execution record ID, tool call ID, tool name, success, elapsed milliseconds, result preview, truncation flag, completed UTC.
- [ ] Add `tool_call_failed`.
  - Payload: same as completed plus error code/message.
- [ ] Add `tool_iteration`.
  - Payload: run ID, iteration number, max iterations.
- [ ] Add `run_completed`.
  - Payload: run ID, status, elapsed milliseconds, iteration count, tool call count, error count.
- [ ] Ensure all SSE payloads use camelCase.
- [ ] Ensure SSE progress payloads are public-safe by construction.
  - Include display label, status code, result count, runtime, success/denied/truncated flags, and safe summary.
  - Exclude raw arguments, raw output, raw stdout/stderr, object paths that policy says to hide, API keys, and provider request IDs.
- [ ] Ensure dashboard SSE parser ignores unknown events.

### Tool Catalog API

- [x] Add `GET /v1.0/api/tools`.
  - Auth required.
  - Returns effective tools visible to the current user under current settings.
  - Include enabled/disabled state and approval metadata.
  - Progress: foundation endpoint is implemented and returns the no-op catalog until executors are registered.
- [x] Add `GET /v1.0/api/tools/{name}`.
  - Auth required.
  - Returns one tool definition.
  - Progress: foundation endpoint is implemented and returns 404 while no executors are registered.
- [ ] Add `POST /v1.0/api/tools/validate`.
  - Admin required.
  - Body: draft tool settings/policy.
  - Returns normalized policy, descriptor list, warnings, and blocking errors.
- [ ] Add `POST /v1.0/api/tools/test`.
  - Admin required.
  - Performs dry-run diagnostics without executing model-directed tools.
  - Verifies global enablement, selected runner availability, runner tool capability, wire format, working directory, allowed roots, web-search provider configuration, and MCP connectivity.
- [x] Add `GET /v1.0/api/conversations/{id}/tool-calls`.
  - Auth required.
  - Conversation owner, tenant admin, or global admin only.
  - Pagination supported.
- [x] Add `GET /v1.0/api/request-history/{id}/tool-calls`.
  - Admin or tenant admin required.
  - Returns redacted audit records scoped by request history ID.
- [x] Add `GET /v1.0/api/tool-runs/{id}`.
  - Auth required.
  - Return run metadata plus tool calls.
- [ ] Add `POST /v1.0/api/tool-runs/{runId}/tool-calls/{toolCallId}/approval`.
  - Body: `{ "approved": true|false, "rememberForRun": true|false }`.
  - Auth required.
  - Only the user who initiated the chat, tenant admin, or global admin may approve.
  - Returns updated tool call record.
- [ ] Add `GET /v1.0/api/mcp`.
  - Admin required.
  - Returns configured MCP server status with secrets redacted.
- [ ] Add `POST /v1.0/api/mcp/reload`.
  - Admin required.
  - Reloads MCP connections after settings changes without restarting the whole server if feasible.

### OpenAPI

- [~] Update `OpenApi()` in `src/Wilson.Server/WilsonServer.cs`.
- [x] Add the `Tools` tag.
- [x] Add tool endpoint paths.
- [x] Add schemas:
  - `ToolDefinition`
  - `ToolCall`
  - `ToolResult`
  - `ToolExecutionRecord`
  - `ToolRun`
  - `ToolApprovalRequest`
  - `ToolRunResponse`
  - `ToolCallEnumeration`
  - Progress: tool model schemas, `ToolExecutionRecordEnumeration`, and `ToolRunResponse` are included. Approval schemas remain pending until approval endpoints exist.
- [x] Update `ChatRequest`, `ChatResponse`, `ChatMessage`, `RequestHistoryEntry`, and `Settings` schemas through model changes.
  - Progress: ChatRequest, ChatResponse, ChatMessage, RequestHistoryEntry, Settings, tool model, and tool metrics schemas are included through reflection-based schema generation.
- [ ] Update `SseEventStream` description to list all tool-related events.

## Phase 6: Server Integration

- [x] Add a `ToolService` field to `WilsonServer`.
- [x] Initialize `ToolService` after settings normalization.
- [~] Update settings PUT handler:
  - Rebuild `InferenceService`.
  - Rebuild or update `ToolService`.
  - Reload MCP connections.
  - Persist settings.
  - Progress: rebuilds `InferenceService`, rebuilds `ToolService`, and persists settings. MCP reload remains pending until MCP support exists.
- [~] Update `ChatAsync` server method.
  - Resolve effective tool settings for request.
  - Validate selected model and runner.
  - Validate runner `SupportsTools` and `ToolCallingApiFormat` before sending tools to the model.
  - Use the tool-aware path only when tools are enabled, tool choice is not `none`, and at least one executable tool definition is available.
  - Create/persist user message before run.
  - For tools disabled, preserve current path.
  - For tools enabled, call agent loop path.
  - Progress: non-streaming tool-agent integration is implemented behind effective tool settings and runner capability checks; streaming remains on the legacy path until tool SSE events are implemented.
- [~] Update non-streaming chat.
  - Run full agent loop.
  - Return final assistant message plus tool run/calls.
  - For `ask` approval, either reject with `400` explaining streaming is required for interactive approval, or support long-poll approval through the approval endpoint. Choose and document one behavior.
  - Progress: non-streaming chat runs the core agent loop, persists the final assistant message, returns safe tool run/call metadata, and rejects `ask` approval because interactive approval requires the future streaming/approval endpoint flow.
- [ ] Update streaming chat.
  - Stream all events.
  - Persist intermediate tool calls as they are proposed/running/completed.
  - Persist final assistant message.
  - Send `done` with the final assistant message after `run_completed`.
- [ ] Update request capture.
  - Capture final assistant content.
  - Capture tool metrics.
  - Capture tool model-check stages separately from final inference where request history supports stage metadata.
  - Link persisted tool-call records to the request history ID and trace ID.
  - Do not store full tool stdout/stderr in request history.
  - Progress: non-streaming request capture records tool metrics and links persisted calls to the generated request-history ID after the request-history row is saved. Stage-level capture remains pending.
- [ ] Update request-history cleanup.
  - Delete expired tool-call audit records using the same retention window as request history unless a separate retention setting is introduced.
- [ ] Add structured logging points if Wilson has or adds logging:
  - run start/end
  - tool proposed
  - tool approval decision
  - tool completed/failed
  - MCP connect/disconnect

## Phase 7: Dashboard Chat Experience

### Chat State

- [~] Extend dashboard message state in `dashboard/src/App.jsx`.
  - Track `toolRuns` by run ID.
  - Track `toolCalls` by tool call ID.
  - Associate tool calls with the assistant message placeholder for the active run.
  - Progress: non-streaming chat responses now attach safe `toolRun`, `toolCalls`, and `toolMetrics` data to assistant messages. Streaming live state and reload merge remain pending.
- [ ] Update SSE handling.
  - Parse new tool events.
  - Update active tool call status live.
  - Treat heartbeat events as runtime updates, not separate rows.
  - Keep terminal events (`completed`, `failed`, `denied`, `cancelled`) as the source of final row state.
  - Attach completed tool records to the assistant message when `done` arrives.
  - Keep existing behavior for `chunk`, `error`, and `done`.
- [x] Update conversation load.
  - Fetch messages as today.
  - Fetch tool calls for the conversation.
  - Merge tool call activity into the rendered message list.
- [~] Add API client methods in `dashboard/src/utils/api.js`.
  - `tools()`
  - `tool(id/name)`
  - `validateTools(policy/settings)`
  - `testTools(policy/settings)`
  - `conversationToolCalls(conversationId, params)`
  - `requestHistoryToolCalls(requestHistoryId, params)`
  - `toolRun(runId)`
  - `approveToolCall(runId, toolCallId, approved, rememberForRun)`
  - `mcpStatus()`
  - `reloadMcp()`
  - Progress: `tools()`, `tool(name)`, `conversationToolCalls`, `requestHistoryToolCalls`, and `toolRun` are implemented. Diagnostics, approval, and MCP methods remain pending with their endpoints.

### Visual Design

- [~] Add a compact `ToolActivity` component under assistant bubbles.
  - Collapsed display: icon, "N tools", current status, aggregate runtime.
  - Show active/running state with a subtle spinner.
  - Use muted colors and existing spacing so assistant text remains primary.
  - Collapse completed successful tool activity by default.
  - Keep active or failed tool activity expanded until the user collapses it.
  - Match AssistantHub's low-friction pattern: one disclosure row labelled "Tool activity" with count/status/runtime summary, not a separate agent console.
  - Progress: completed non-streaming tool traces render as a compact disclosure row under assistant bubbles, collapsed for success and expanded when failures/denials are present. Running state waits for SSE tool events.
- [~] Add `ToolCallRow`.
  - Shows tool name, status chip, runtime, success/failure.
  - Shows a short argument summary.
  - Shows result preview or error preview.
  - Has a disclosure control for details.
  - Progress: row, status chip, runtime, and safe metadata details are implemented for returned traces. Argument/result previews remain pending until raw/redacted trace payload shape is added.
- [ ] Add expanded details.
  - Arguments as formatted JSON.
  - Result as formatted JSON or monospace text.
  - stdout/stderr sections for `run_process`.
  - URL/status/title for web tools.
  - Start/end timestamps.
  - Elapsed milliseconds.
  - Copy buttons for arguments and results.
  - Use safe public trace data in normal chat. If raw/redacted audit arguments or outputs are shown in chat, require admin/tenant-admin authorization and label them as audit details.
- [ ] Add approval UI for `ask`.
  - Inline pending approval row under the active assistant message.
  - Buttons: Approve, Deny, Always for this run.
  - Disable buttons after decision.
  - Show approval timeout countdown or absolute timeout.
- [ ] Add failure UI.
  - Failed tool rows should be visible but visually restrained.
  - Error detail should be expandable.
  - Do not replace the assistant message with raw tool errors unless the whole run fails before final response.
- [~] Add CSS in `dashboard/src/index.css`.
  - Stable dimensions for status chips and icon buttons.
  - Responsive behavior for mobile.
  - No nested cards inside message bubbles.
  - No large decorative panels.
  - Result previews must wrap and not overflow.
  - Use existing color variables and avoid a new dominant palette.
  - Progress: compact, responsive tool trace rows and toolbar selector styles are implemented for non-streaming traces. Live running/approval states remain pending.

### Chat Controls

- [x] Add a Tools toggle to the chat toolbar.
  - Hidden or disabled when server tools are disabled globally.
  - State should persist in local storage per user/browser.
  - Progress: toggle is based on the server tool catalog, persists in local storage, and enabling tools forces non-streaming until SSE tool events exist.
- [~] Add an approval policy selector.
  - Options: Ask, Auto, Deny.
  - Hide Auto unless server settings allow it for the user.
  - Show concise warning in tooltip, not large explanatory page text.
  - Progress: compact selector sends `auto` or `deny` with non-streaming tool requests. `ask` is visible but disabled until approval SSE and dashboard approval endpoints are implemented.
- [ ] Add a tool catalog modal from chat toolbar.
  - Lists available tool names, categories, enabled state, and approval requirement.
  - No raw schemas unless expanded.
- [ ] Ensure Stop cancels tool execution.
  - Existing `stopGeneration` must abort active SSE.
  - Server must receive cancellation through request token.
  - UI must mark active tool rows cancelled.

### Admin Settings UI

- [~] Add a Tools section to `SettingsAdmin`.
  - Enabled toggle.
  - Built-ins enabled toggle.
  - Default approval policy select.
  - Destructive tools require approval toggle.
  - Working directory input.
  - Allowed roots editable list.
  - Enabled/disabled tool names editable list.
  - Timeouts and byte limits numeric inputs.
  - Store full results toggle.
  - Store tool arguments toggle.
  - Expose safe trace metadata toggle.
  - Progress events toggle.
  - Tool choice selector.
  - Validate Policy button.
  - Test Diagnostics button.
  - Descriptor list showing enabled, available, category, approval requirement, and unavailable reason.
  - Progress: adding first dashboard controls for global enablement, built-ins, approval policy, safety limits, working directory, allowed roots, enabled/disabled tool names, and trace/progress flags.
  - Progress: initial `SettingsAdmin` Tools section is implemented and validated for the currently supported global settings; validate/test diagnostics and descriptor list remain pending.
  - Progress: continuing with the descriptor list in the Tools settings section so admins can see effective tool availability and unavailable reasons.
  - Progress: effective tool descriptor list is implemented in `SettingsAdmin` using `/v1.0/api/tools`, with refresh after settings save and a manual refresh button. Validate/test diagnostics remain pending.
  - Progress: descriptor-list and dependency-refresh slice validated on 2026-06-26 with solution build, automated tests, dashboard lint, and dashboard production build.
- [ ] Add Web Search subsection.
  - Enabled toggle.
  - Allow fallback toggle.
  - Provider list with add/edit/delete.
  - Provider type select: Tavily, You.
  - Endpoint, API key/env ref, timeout, enabled, default.
- [ ] Add MCP subsection.
  - Enabled toggle.
  - Server list with add/edit/delete.
  - Transport select: stdio, http.
  - stdio fields: command, args, env.
  - http fields: URL, MCP path.
  - Status display and reload button.
  - Redact env/API secret values in display.
- [x] Update Model Server editor.
  - Add tools enabled toggle per runner.
  - Add supports tools toggle.
  - Add supports parallel tool calls toggle.
  - Add chat completions path input.
  - Progress: both model-server edit paths now expose tools enabled, supports tools, tool API format, chat completions path, parallel tool calls, and streaming tool calls. Validated with dashboard lint/build.
- [ ] Update API Explorer.
  - Add tool catalog endpoints.
  - Add tool validate/test endpoints.
  - Add conversation tool-call endpoint.
  - Add request-history tool-call endpoint.
  - Add approval endpoint.
  - Add MCP status/reload endpoints.
- [ ] Update request history and conversation history modals.
  - Add a redacted `ToolCallTraceSection` equivalent.
  - Support filters for tool name, success, denied, trace ID, and time range.
  - Show a compact timeline ordered by iteration, sequence number, and started timestamp.
  - Show record/request/conversation/trace IDs with copy controls where existing UI patterns support them.
  - Show provider/model, bytes in/out, duration, status, safe error message, redacted arguments, result summary, and redacted output.

## Phase 8: SDKs

Progress, 2026-06-26: SDK/Postman/docs slice is implemented for the completed persistence APIs. JavaScript, Python, and C# clients expose tool catalog/run/conversation/request-history read methods; Postman, README, dashboard README, SDK README, and REST API docs are updated for the same surfaces. Validation passed: C# SDK build, JavaScript syntax check, Python bytecode compile, and Postman JSON parse. Approval, validate/test diagnostics, MCP, streaming, and destructive/process/web/search tool methods remain pending until their server endpoints exist.

### Shared SDK Requirements

- [~] Add models matching OpenAPI:
  - ToolDefinition
  - ToolDescriptor
  - ToolCall
  - ToolResult
  - ToolExecutionRecord
  - ToolRun
  - ToolTrace
  - ToolProgressEvent
  - ToolApprovalRequest
  - ToolPolicyValidationRequest
  - ToolPolicyValidationResult
  - ToolPolicyTestResult
  - ChatRequest tool fields
  - ChatResponse tool fields
  - Progress: C# SDK models were added for `ToolDescriptor`, `ToolExecutionRecord`, `ToolRun`, and `ToolRunResponse`; JavaScript/Python SDKs intentionally return parsed JSON objects by existing convention. Remaining approval/validation/progress/chat model wrappers wait for those endpoints/client chat helpers.
- [~] Add methods:
  - `ListTools`
  - `GetTool`
  - `ValidateTools`
  - `TestTools`
  - `GetConversationToolCalls`
  - `GetRequestHistoryToolCalls`
  - `GetToolRun`
  - `ApproveToolCall`
  - `GetMcpStatus`
  - `ReloadMcp`
  - Progress: implemented `ListTools`, `GetTool`, `GetConversationToolCalls`, `GetRequestHistoryToolCalls`, and `GetToolRun` equivalents in JavaScript, Python, and C#. Validate/test, approval, and MCP methods remain pending with their server endpoints.
- [ ] Add admin audit methods where appropriate:
  - `ListToolCalls`
  - `GetToolCall`
  - `DeleteToolCalls`
  - `DeleteToolCall`
- [ ] Add chat request options for tools.
- [ ] Add `toolCalls` or `tool_calls` safe trace metadata to chat response models.
  - Tests must prove SDK chat response traces do not expose raw `ArgumentsJson` or raw `ResultJson`.
- [ ] Add streaming support where practical.
  - JavaScript: async iterator over SSE events.
  - Python: generator over SSE events using standard library or document that streaming requires an optional dependency if standard library is too awkward.
  - C#: `IAsyncEnumerable<WilsonSseEvent>`.
- [ ] Preserve current simple APIs.

### JavaScript SDK

- [x] Update `sdk/javascript/index.js`.
  - Progress: added `tools`, `tool`, `toolRun`, `conversationToolCalls`, and `requestHistoryToolCalls`.
- [x] Update `sdk/javascript/README.md`.
- [ ] Add examples for:
  - list tools
  - tool-enabled non-streaming chat with auto/deny
  - streaming chat with tool events
  - approving a tool call

### Python SDK

- [x] Update `sdk/python/wilson_client.py`.
  - Progress: added `tools`, `tool`, `tool_run`, `conversation_tool_calls`, and `request_history_tool_calls`.
- [x] Update `sdk/python/README.md`.
- [ ] Add examples matching JavaScript.
- [ ] Keep standard-library compatibility unless there is an explicit decision to add dependencies.

### C# SDK

- [x] Update `sdk/csharp/Wilson.Sdk/WilsonClient.cs`.
  - Progress: added `GetToolsAsync`, `GetToolAsync`, `GetToolRunAsync`, `GetConversationToolCallsAsync`, and `GetRequestHistoryToolCallsAsync` with cancellation tokens.
- [x] Add model classes under `sdk/csharp/Wilson.Sdk/Models`.
  - Progress: added typed models for tool descriptors, runs, records, and run responses.
- [x] Update `sdk/csharp/README.md`.
- [ ] Add streaming helper if feasible with `HttpCompletionOption.ResponseHeadersRead`.

### Top-Level SDK Docs

- [x] Update `sdk/README.md`.
  - Document new tool methods.
  - Explain that server settings control whether tools are available.
  - Link to REST API docs/OpenAPI.

## Phase 9: Documentation

- [~] Update `README.md`.
  - Add tool-calling capability to feature list.
  - Add safety-focused configuration section.
  - Add short quick-start note for enabling tools.
  - Add supported built-in tool list.
  - Add MCP and web search notes.
  - Add warning that file/process tools should be scoped to allowed roots.
  - Progress: completed for implemented tool enablement, built-in inventory, safety roots, SDK/Postman pointers, and persistence APIs. MCP/web/search/process notes remain pending with those features.
- [~] Create `REST_API.md` if it does not exist.
  - Document all REST endpoints in plain Markdown.
  - Include auth requirements.
  - Include request/response examples for chat with tools.
  - Document safe chat `toolCalls` metadata separately from admin audit records.
  - Include SSE event examples.
  - Include heartbeat/progress event examples.
  - Include approval workflow example.
  - Include tool policy validate/test examples and endpoint capability diagnostics.
  - Include retention behavior for tool-call audit records.
  - Link to `/openapi.json` and `/swagger`.
  - Progress: created and documented implemented auth, tool catalog, chat response metadata, persisted tool-call read APIs, request-history metrics, and safe trace behavior. Streaming SSE, approval workflow, diagnostics, and MCP examples remain pending with those endpoints.
- [x] Update `dashboard/README.md`.
  - Document chat tool activity UI.
  - Document admin tool settings.
  - Document history/request detail tool-call trace views.
- [x] Update `CHANGELOG.md`.
  - Add an unreleased entry after implementation.
- [ ] Update Docker docs in `README.md` or `docker` docs if new volume mounts are needed for tool working directories.
- [ ] Add security guidance.
  - Recommended defaults.
  - Allowed roots.
  - Approval policies.
  - Process execution risks.
  - Secret redaction limitations.
- [ ] Document model compatibility.
  - OpenAI-compatible tool calling required.
  - OpenAI-compatible providers should use `OpenAIChatCompletions`.
  - Ollama can use native `OllamaChat` or an OpenAI-compatible path only if Wilson's adapter supports that selected format.
  - Behavior when a runner rejects tools.
- [ ] Add `TOOLS.md` if `REST_API.md` becomes too large.
  - Explain built-in tool inventory, safety model, approval modes, output limits, redaction, and MCP lifecycle.
  - Cross-link from README, REST API docs, SDK READMEs, and dashboard README.

## Phase 10: Postman Collection

- [x] Update `postman/Wilson.postman_collection.json`.
- [~] Add variables:
  - `toolName`
  - `runId`
  - `toolCallId`
  - `conversationId`
  - `requestHistoryId`
  - `traceId`
  - Progress: added `toolName`, `toolRunId`, `conversationId`, `requestHistoryId`, and `tenantId`. Approval/audit-specific `toolCallId` and `traceId` remain pending until those endpoints exist.
- [~] Add folder `Tools`.
  - List Tools.
  - Get Tool.
  - Validate Tools.
  - Test Tools.
  - Get Conversation Tool Calls.
  - Get Request History Tool Calls.
  - Get Tool Run.
  - Approve Tool Call.
  - List Audit Tool Calls.
  - Get Audit Tool Call.
  - Delete Audit Tool Calls.
  - Delete Audit Tool Call.
  - Progress: added List Tools, Get Tool, Get Tool Run, Get Conversation Tool Calls, and Get Request History Tool Calls. Validation, approval, and audit delete/read requests remain pending until endpoints exist.
- [ ] Add folder `MCP`.
  - MCP Status.
  - Reload MCP.
- [ ] Update Chat requests.
  - Non-streaming chat with tools disabled.
  - Non-streaming chat with tools auto/deny.
  - Streaming endpoint note that Postman may show raw SSE frames.
- [x] Update collection description to mention tool calling.

## Phase 11: Tests

### Core Unit Tests

- [x] Add test coverage in `src/Test.Shared/WilsonSuites.cs` or create a richer test project if needed.
  - Progress: added coverage for tool ID lengths, default tool settings, runner tool defaults, diagnostic catalog behavior, allowed-root read execution, secret-path blocking, OpenAI/Ollama tool-call response parsing, and a fake-model tool-agent loop that executes `read_file` then returns a final answer. Automated tests pass for this slice.
  - Progress: added coverage for `write_file`, `edit_file`, `multi_edit`, `delete_file`, and `manage_directory`, including exact-match failures, CRLF preservation, allowed-root execution, destructive-tool metadata, and secret-path blocking. Automated tests pass for this slice.
  - Progress: added coverage for `run_process`, including successful stdout/exit-code capture, non-zero exit-code capture, timeout handling, working-directory enforcement, and dangerous/approval metadata. Automated tests pass for this slice.
- [ ] Test tool registry filtering:
  - global disabled
  - disabled by name
  - enabled subset
  - unsafe missing working directory
- [ ] Test tool policy resolver diagnostics:
  - available tool included.
  - disabled tool reports disabled reason.
  - missing working directory or allowed root reports unavailable reason.
  - missing web-search provider reports unavailable reason.
  - final enabled/disabled allow-list filtering is applied.
- [ ] Test tool argument validation:
  - arguments must be a JSON object.
  - unknown properties are rejected.
  - malformed number/bool/list values are rejected.
  - permitted numeric strings are normalized only for tools that explicitly support them.
- [ ] Test output limiter:
  - per-call truncation returns valid JSON.
  - per-turn truncation returns valid JSON.
  - truncation flags and original character counts are set.
- [ ] Test audit writer:
  - persisted arguments can be suppressed by policy.
  - persisted outputs can be summarized by policy.
  - secret-like fields are redacted recursively.
  - public/model-visible redaction preserves safe continuation tokens.
- [ ] Test `WorkingDirectoryGuard`.
  - relative paths inside root pass.
  - absolute paths inside root pass.
  - path traversal outside root fails.
  - symlink/junction behavior is defined and tested.
- [x] Test `read_file`.
  - line numbers.
  - offset/limit.
  - max size rejection.
  - outside root rejection.
- [x] Test `write_file`.
  - creates parent directories.
  - preserves line endings.
  - outside root rejection.
- [x] Test `edit_file`.
  - success.
  - no match failure.
  - multiple match failure with line numbers.
- [x] Test `multi_edit`.
  - all edits apply.
  - validation prevents partial write.
  - sequential conflict failure.
- [x] Test `delete_file`.
- [ ] Test `file_metadata`.
- [ ] Test `list_directory`.
- [x] Test `manage_directory`.
- [ ] Test `glob`.
- [ ] Test `grep`.
  - regex timeout/invalid regex.
  - match limit truncation.
- [x] Test `run_process`.
  - success.
  - non-zero exit.
  - timeout kills process.
  - output truncation.
  - working directory enforcement.
- [ ] Test `web_retrieve` behind a local test HTTP server.
  - No external network dependency.
  - Include optional HTML test.
- [ ] Test `web_search` with mocked providers.
  - default provider.
  - fallback.
  - provider failure.
- [ ] Test MCP manager with test MCP server from Mux test fixtures or a Wilson-owned minimal fixture.

### Agent Loop Tests

- [ ] Add fake chat-completions transport.
- [ ] Test no-tool path produces final assistant message.
- [ ] Test one tool call:
  - model proposes tool.
  - tool executes.
  - tool result is appended.
  - model receives tool result and returns final answer.
- [ ] Test the provider request contains `tools`, tool choice, and Wilson tool behavior instructions.
- [ ] Test the second provider request contains the assistant message with `tool_calls` and a matching `role: "tool"` result message.
- [ ] Test multiple tool calls in one assistant message.
- [ ] Test sequential tool calls across multiple model iterations.
- [ ] Test tool execution errors are returned to the model as structured non-secret tool outputs and the model can recover with a final answer.
- [ ] Test parallel tool-call support is serialized or parallelized according to implementation decision.
- [ ] Test unknown tool result.
- [ ] Test disabled tool result.
- [ ] Test approval deny.
- [ ] Test approval ask approved.
- [ ] Test approval ask timeout.
- [ ] Test max iterations reached.
- [ ] Test max tool calls per turn reached.
- [x] Test per-turn output limit reached.
- [ ] Test loop guard stops repeated discovery/read cycles and requests a best-effort final answer.
- [ ] Test final fallback message when tool limit is reached and the final model call fails or returns empty content.
- [ ] Test cancellation.
- [ ] Test truncation preserves valid assistant/tool result pairs.
- [ ] Test metrics.
- [ ] Test OpenAI-compatible tool-call parsing.
- [ ] Test Ollama tool-call parsing, including object-shaped arguments and missing OpenAI IDs.

### Database Tests

- [x] Test SQLite schema migration from old schema.
  - Progress: `ToolPersistenceAsync` initializes the schema twice against a fresh SQLite database and exercises additive columns/tables. A literal pre-feature database fixture remains optional future coverage.
- [x] Test tool run create/update/read.
- [x] Test tool call create/update/read.
- [x] Test tool-call records link to request history and conversation/message by trace ID after final message persistence.
- [x] Test conversation delete removes tool rows.
- [x] Test request history tool metrics.
- [x] Test retention deletes expired tool-call audit rows.
- [ ] Add PostgreSQL test path if existing test infrastructure supports it; otherwise document manual verification.

### Server/API Tests

- [ ] Add API tests for `GET /v1.0/api/tools`.
- [ ] Add API tests for tool validate/test diagnostics.
- [ ] Add API tests for approval endpoint authorization.
- [ ] Add API tests for conversation tool calls authorization.
- [ ] Add API tests for request-history tool calls authorization.
- [ ] Add streaming SSE parser tests for tool events.
- [ ] Add tests proving public chat `toolCalls` omit raw arguments, raw output, provider request IDs, and hidden policy fields.
- [ ] Add OpenAPI generation test proving schemas and paths are present.

### Dashboard Tests

- [x] Run existing `npm run lint`.
  - Progress: passed on 2026-06-25 using `npm.cmd run lint` because local PowerShell execution policy blocks `npm.ps1`.
  - Progress: passed on 2026-06-26 after adding global/runner tool settings controls.
  - Progress: passed on 2026-06-26 after dependency refresh and Tools descriptor list.
  - Progress: passed on 2026-06-26 after persistence-backed conversation reload and request-history tool activity.
  - Progress: passed on 2026-06-26 after SDK/docs/Postman updates.
- [x] Run existing `npm run build`.
  - Progress: passed on 2026-06-25 using `npm.cmd run build`.
  - Progress: passed on 2026-06-26 after adding global/runner tool settings controls.
  - Progress: passed on 2026-06-26 after dependency refresh and Tools descriptor list.
  - Progress: passed on 2026-06-26 after persistence-backed conversation reload and request-history tool activity.
  - Progress: passed on 2026-06-26 after SDK/docs/Postman updates.
- [ ] Add unit tests if a test runner is introduced.
- [ ] Add manual QA checklist if no dashboard test framework is added:
  - tools disabled chat unchanged
  - active tool call shows running state
  - heartbeat updates runtime without adding duplicate rows
  - expanded tool shows arguments/result/runtime
  - normal chat uses safe trace data and does not expose raw audit payloads
  - request history modal shows redacted audit tool-call records
  - settings validate/test buttons show unavailable reasons before saving
  - approval workflow works
  - reload conversation preserves tool activity
  - mobile layout does not overlap

### SDK Tests

- [ ] Add JavaScript SDK tests or example validation.
- [ ] Add Python SDK tests or example validation.
- [ ] Add C# SDK tests for new methods.
- [ ] Add SDK tests proving `toolCalls` safe traces round-trip and do not expose audit-only fields.
  - Progress: 2026-06-26 artifact validation passed for implemented SDK methods: `node --check sdk\javascript\index.js`, `python -m py_compile sdk\python\wilson_client.py`, and `dotnet build sdk\csharp\Wilson.Sdk\Wilson.Sdk.csproj`. Dedicated SDK behavioral tests remain pending.

## Phase 12: Superset Tool Candidates

Implement these only after the Mux baseline is complete and tested:

- [ ] `http_request`: generic HTTP client with method, URL, headers, body, timeout, response capture, and header redaction.
- [ ] `git_status`: structured repository status without arbitrary shell execution.
- [ ] `git_diff`: bounded diff retrieval.
- [ ] `git_show`: inspect commits and files.
- [ ] `apply_patch`: patch grammar-compatible file editing with validation.
- [ ] `openapi_call`: call an API operation from a registered OpenAPI document.
- [ ] `sql_query`: read-only database query against configured safe data sources.
- [ ] `browser_screenshot`: rendered screenshot capture for web pages.
- [ ] `image_metadata`: inspect image dimensions/type without exposing full binary content.
- [ ] `wilson_request_history_search`: search Wilson request history as a model tool.
- [ ] `wilson_conversation_search`: search authorized Wilson conversations as a model tool.

Each superset tool must have:

- [ ] JSON schema.
- [ ] Safety policy.
- [ ] Allowed scopes.
- [ ] Tests.
- [ ] Dashboard rendering rules.
- [ ] Documentation.

## Security And Authorization Requirements

- [ ] Global admins can configure tools.
- [ ] Tenant admins can view tool calls for their tenant.
- [ ] Normal users can view and approve only their own active tool calls.
- [~] File/process tools cannot run unless `Settings.Tools.Enabled` is true and `WorkingDirectory` plus `AllowedRoots` are configured.
  - Progress: tightening chat request resolution so global `Settings.Tools.Enabled = false` is a hard server-side disable even when a request explicitly sends `toolsEnabled: true`.
  - Progress: server chat resolution now rejects explicit tool requests while global tools are disabled; dashboard chat sends `toolsEnabled: false` when the user toggle is off. Validated with solution build and automated tests.
- [ ] All filesystem paths must resolve inside allowed roots.
- [ ] Process execution must be disabled independently by default or marked approval-required by default.
- [ ] Destructive tools must require approval unless an admin explicitly disables `DestructiveToolsRequireApproval`.
- [ ] Secrets must be redacted before API responses, request history, logs, and dashboard display.
- [ ] Public chat traces must be generated from safe `ToolTrace`/`ToolProgressEvent` payloads, not from persisted audit rows.
- [ ] Admin audit rows must still be redacted before persistence unless a future explicit secure-secret-storage design is implemented.
- [ ] Tool arguments and results must be size capped.
- [ ] MCP server environment variables must never be returned unredacted.
- [ ] Web tools must restrict URL schemes to `http` and `https`.
- [ ] Approval endpoint must enforce conversation ownership.
- [ ] Tool calls must be tenant-scoped in every database query.
- [ ] Tool executors must recheck effective policy at execution time, even when the registry already filtered the model-visible tool list.
- [ ] Tool output appended back into model context must be treated as untrusted content and the system prompt must instruct the model accordingly.

## Rollout Plan

- [x] PR 1: Models, settings, config files, and no-op tool catalog with tools disabled.
  - Progress: implementation is in the working tree and passes solution build plus automated tests. Commit/PR packaging is still a separate repository workflow step.
- [ ] PR 2: Built-in filesystem/process/web retrieval tools with unit tests.
- [ ] PR 3: Tool-aware chat-completions transport and agent loop with fake backend tests.
- [ ] PR 4: Database persistence and API endpoints.
- [ ] PR 5: Dashboard chat tool activity UI and settings UI.
- [ ] PR 6: Web search and MCP.
- [ ] PR 7: SDKs, Postman, REST docs, README updates.
- [ ] PR 8: Hardening pass, compatibility pass, and manual QA.

## Acceptance Criteria

- [ ] With tools disabled, Wilson chat behaves the same as the current product.
- [ ] With tools enabled and a safe working directory configured, a tool-capable model can call `read_file` and receive the file result in a follow-up model turn.
- [ ] The dashboard shows an active tool call while it is running.
- [ ] The expanded chat view shows tool arguments, result preview, success/failure, timestamps, and runtime.
- [ ] Public expanded chat details are safe by default; audit-only arguments/results are available only through authorized redacted audit views.
- [ ] Tool calls remain visible after reloading the conversation.
- [ ] Tool calls are visible from request history/detail views with redacted arguments, result summaries, provider/model, byte counts, sequence numbers, and timings.
- [ ] Approval mode `ask` blocks execution until the dashboard approval endpoint is called.
- [ ] Approval denial is sent back to the model as a tool result.
- [ ] `run_process` honors timeout and cancellation.
- [ ] Tool output is truncated according to settings before persistence and model feedback.
- [ ] Per-turn output limits and loop guards stop repeated tool cycles and produce a best-effort final answer.
- [ ] Tool diagnostics catch non-tool-capable runners, unsupported wire formats, missing working directories, missing allowed roots, missing search providers, and disconnected MCP servers before chat.
- [ ] OpenAPI includes all new tool-related endpoints and schemas.
- [ ] JavaScript, Python, and C# SDKs expose the new APIs.
- [ ] Postman collection includes the new APIs.
- [x] `dotnet build src\Wilson.slnx` passes.
  - Progress: passed on 2026-06-25 after the dashboard tool-trace slice, with existing NU1903 `SQLitePCLRaw.lib.e_sqlite3` advisory warnings.
  - Progress: passed on 2026-06-26 after global tool hard-disable and dashboard settings controls, with existing NU1903 `SQLitePCLRaw.lib.e_sqlite3` advisory warnings.
  - Progress: dependency refresh started on 2026-06-26; NuGet and npm package updates are being checked before the next validation run.
  - Progress: latest package check found NuGet updates for `Microsoft.Data.Sqlite`, `Npgsql`, `PolyPrompt`, `PrettyId`, and `Watson`, plus dashboard npm updates for React/Vite/ESLint/i18next/lucide-related packages. Applying direct dependency updates now.
  - Progress: NuGet direct references are updated; dashboard `react`, `react-dom`, and `@types/react` are updated. Continuing remaining npm packages with smaller exact-version installs because bulk npm installs timed out.
  - Progress: direct NuGet references and dashboard npm dependencies are now current according to `dotnet list package --outdated` and `npm outdated`. The existing transitive `SQLitePCLRaw.lib.e_sqlite3` NU1903 advisory still appears during restore.
  - Progress: passed on 2026-06-26 after dependency refresh and Tools descriptor list, with existing transitive NU1903 `SQLitePCLRaw.lib.e_sqlite3` advisory warnings.
- [x] `dotnet run --project src\Test.Automated` passes.
  - Progress: passed on 2026-06-25 after the dashboard tool-trace slice, with existing NU1903 `SQLitePCLRaw.lib.e_sqlite3` advisory warnings.
  - Progress: passed on 2026-06-26 after global tool hard-disable and dashboard settings controls, with existing NU1903 `SQLitePCLRaw.lib.e_sqlite3` advisory warnings.
  - Progress: passed on 2026-06-26 after dependency refresh and Tools descriptor list, with existing transitive NU1903 `SQLitePCLRaw.lib.e_sqlite3` advisory warnings.
- [x] `cd dashboard && npm run lint` passes.
  - Progress: passed on 2026-06-25 using `npm.cmd run lint` because local PowerShell execution policy blocks `npm.ps1`.
  - Progress: passed on 2026-06-26 using `npm.cmd run lint`.
  - Progress: passed on 2026-06-26 after dependency refresh and Tools descriptor list.
- [x] `cd dashboard && npm run build` passes.
  - Progress: passed on 2026-06-25 using `npm.cmd run build`.
  - Progress: passed on 2026-06-26 using `npm.cmd run build`.
  - Progress: passed on 2026-06-26 after dependency refresh and Tools descriptor list using Vite 8.1.0.

## Known Risks And Decisions To Make

- [ ] Decide whether tool-enabled chat should fully replace PolyPrompt chat transport or live alongside it.
  - Recommendation: keep PolyPrompt for tools-disabled compatibility initially; introduce Wilson-owned chat-completions transport for tool-enabled runs.
- [ ] Decide whether non-streaming chat supports `ask` approval.
  - Recommendation: reject `ask` for non-streaming chat with a clear `400`, and require streaming for interactive approval.
- [ ] Decide whether `run_process` is enabled by default when built-ins are enabled.
  - Recommendation: registered but disabled by name in default settings, or enabled only with explicit approval-required policy.
- [ ] Decide whether Playwright browser auto-install is acceptable in server deployments.
  - Recommendation: default to enabled for developer installs, configurable off for locked-down deployments.
- [ ] Decide MCP support priority.
  - Recommendation: ship built-ins first, then MCP once persistence and UI are stable.
- [ ] Decide if Wilson should add `REST_API.md`.
  - Recommendation: yes, because the feature adds multi-step SSE and approval workflows that are easier to understand in prose than OpenAPI alone.
- [ ] Decide whether Wilson uses a single response runner for tool routing or adds a dedicated tool-routing runner.
  - Recommendation: support an optional dedicated routing runner after the single-runner path works, following AssistantHub's pattern.
- [ ] Decide how much audit detail normal conversation owners can view.
  - Recommendation: normal users get safe traces in chat; tenant/global admins get redacted audit records with arguments and outputs.
- [ ] Decide whether loop guard rules are generic enough for filesystem/process/web tools.
  - Recommendation: implement generic high-output and repeated-tool guards first, then add tool-category-specific guards after observing real runs.
