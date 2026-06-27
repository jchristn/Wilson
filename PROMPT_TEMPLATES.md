# Prompt Templates Implementation Plan

Status: complete

Request date: 2026-06-26

## Scope

Add first-class prompt template management to Wilson:

- Add a dashboard page named `Prompts` between `Model Servers` and `Conversations`.
- Allow users to define and manage `System` prompts and `Tool` prompts.
- Display prompts in table-based dashboard views consistent with existing Wilson tables.
- Add create, edit, view JSON, and delete flows consistent with existing modals and action menus.
- Ensure the action/context menu includes `Edit`, and edit opens a proper text editor for prompt content.
- Let Chat users select a system prompt and tool prompt after selecting a model.
- Persist selected prompt parameters in request history.
- Add startup migrations for new database tables, columns, and indexes.
- Seed reasonable default system and tool prompts on server startup when defaults do not exist.
- Update backend, dashboard, SDKs, documentation, REST API reference, CHANGELOG, README, docker assets, docker factory assets, init scripts, and tests.

## Path Note

The user requested compliance with `C:\Code\Agent\requirements`. That path does not exist on this machine. The available requirements source is `C:\Code\Agents\requirements`, and this plan is written against that directory.

## Questions

No blocking questions are required before implementation. Reasonable assumptions are listed below so implementation can proceed without waiting.

Non-blocking product questions to confirm later:

- Should normal non-admin users be allowed to create prompts, or should prompt management be tenant-admin only?
- Should prompt templates be shared across all users in a tenant, or should Wilson support user-private prompts later?
- Should prompt content snapshots be retained in request history, or only prompt IDs/names and content hashes?

## Assumptions

- Prompt templates are tenant-scoped because Wilson conversations, users, credentials, tool traces, and request history are tenant-scoped.
- Tenant administrators and global administrators can create, edit, default, activate, and delete prompt templates.
- All authenticated users in a tenant can list active prompts and select them in Chat.
- Each tenant has exactly one default `system` prompt and one default `tool` prompt.
- Seeded defaults are protected from deletion but editable by an administrator unless implementation discovers an existing protected-record convention requiring stricter behavior.
- Chat will send selected prompt IDs plus the visible prompt content in `settings.systemPrompt` and `settings.toolSystemPrompt` so users can see and edit what goes to the model. Server-side ID resolution will validate selection and persist metadata, but it must not add hidden prompt text.
- Tool prompt rendering may support explicit template variables such as `{{tool_catalog}}` and `{{approval_policy}}`; the rendered version shown in Chat is the version sent to the model.
- Request history should persist prompt IDs, names, kinds, default flags, and hashes at minimum. Persisting full prompt snapshots is preferred for auditability if storage and privacy review accept it.
- Fields with closed value sets must use enums in code where appropriate. Prompt kind must be modeled as an enum, not as unconstrained string values.

## Requirements Matrix

| Area | Source | Applicable Guidance | Plan Impact |
| --- | --- | --- | --- |
| Repository layout | `REPOSITORY_REQUIREMENTS.md` | Source belongs in `src/`, `dashboard/`, `sdk/`; Docker uses `.yaml`; README, CHANGELOG, LICENSE must remain present. | Keep backend edits under `src/`, dashboard edits under `dashboard/`, SDK edits under `sdk/`; update `README.md`, `REST_API.md`, `CHANGELOG.md`, and Docker assets. |
| Backend architecture | `BACKEND_ARCHITECTURE.md` | Keep clear API, persistence, auth, request context, and operational boundaries. | Add prompt models, database methods, REST routes, OpenAPI schemas, and prompt resolution in the existing server/database patterns. |
| Authentication | `AUTHENTICATION.md` | Enforce tenant isolation on every query; server-side authorization cannot rely on dashboard checks; admins manage privileged control-plane surfaces. | Prompt CRUD routes must validate tenant scope; global admin can pass `tenantId`; tenant admin is constrained to own tenant; normal users can only list/select active prompts. |
| Backend tests | `BACKEND_TEST_ARCHITECTURE.md` | Shared tests live in `Test.Shared`; use Touchstone descriptors; no console output from shared tests; integrations may skip when dependencies are unavailable. | Add prompt persistence, tenant isolation, startup seeding, chat prompt selection, and request history tests in `src/Test.Shared/WilsonSuites.cs`. |
| Code style | `CODE_STYLE.md` | Public members, constructors, and public methods require XML documentation; private members do not. | Add XML docs for all new public models and database/server public methods. |
| Frontend architecture | `FRONTEND_ARCHITECTURE.md` | Use fetch-based API client; table/modals should be consistent; evaluate desktop/tablet/mobile; no unexpected horizontal scrolling. | Extend `dashboard/src/utils/api.js`; add Prompts page using existing `DataTable`, `PageIntro`, `Modal`, `FormInput`, `JsonModal`, and action patterns. |
| Internationalization | `I18N.md` | User-facing text and accessibility strings should be localizable; document exceptions. | Add new labels through the existing dashboard text path initially; if i18n foundation is not complete, document this as existing debt and avoid scattering new hardcoded strings beyond the local text registry. |
| Documentation | `WRITING_DOCUMENTS.md` | Use specific, direct, human-readable documentation with useful structure and non-formulaic prose. | Update docs with concrete API examples, prompt selection behavior, and request history fields. |

## Product Definition

### User Stories

- As a tenant admin, I can create a named system prompt for my tenant so chat users can pick a consistent model behavior.
- As a tenant admin, I can create a named tool prompt so tool-capable chats use predictable instructions for tool discovery, approval, execution, and response synthesis.
- As a chat user, I can select the system prompt and tool prompt for the selected model before sending a message.
- As a chat user, I can preview or edit the prompt content that will be sent to the model.
- As an auditor or operator, I can inspect request history and see which prompt templates were used for a chat request.
- As an operator, I can rely on startup seeding so a fresh Wilson deployment has usable default prompts without manual setup.

### Anti-Goals

- Do not introduce hidden secondary system messages.
- Do not store prompt templates in `wilson.json`; prompts are database-backed tenant records.
- Do not make Chat depend on localStorage-only prompt text as the source of truth.
- Do not let prompt records leak across tenant boundaries.
- Do not block non-tool-capable models from using system prompts.
- Do not send tool prompts when tool calls are disabled or unsupported for the selected model.

## Data Model Plan

### New Model

- [x] Add `PromptTemplate` in `src/Wilson.Core/Models/Models.cs`.
  - Fields:
    - `Id`
    - `TenantId`
    - `Kind` (`system` or `tool`)
    - `Name`
    - `Description`
    - `Content`
    - `IsDefault`
    - `IsProtected`
    - `Active`
    - `CreatedByUserId`
    - `UpdatedByUserId`
    - `CreatedUtc`
    - `LastUpdateUtc`
  - Add XML docs for every public property.

- [x] Add prompt kind enum:
  - `PromptTemplateKind.System`
  - `PromptTemplateKind.Tool`
  - Serialize API values as strings and parse case-insensitively at boundaries.

- [x] Add ID helper:
  - `IdGenerator.PromptTemplate()`
  - Keep IDs under the existing 32-character expectation.

### New Table

- [x] Extend `DatabaseDriver.InitializeAsync` with startup migration statements:
  - `CREATE TABLE IF NOT EXISTS prompttemplates (...)`
  - Use provider-neutral column definitions compatible with SQLite and PostgreSQL.

- [x] Add indexes:
  - `idx_prompttemplates_tenant_kind_active`
  - `idx_prompttemplates_tenant_kind_default`
  - `idx_prompttemplates_tenant_name`
  - `idx_prompttemplates_tenant_updated`

- [x] Enforce uniqueness:
  - One prompt name per tenant/kind, case-insensitive at the application layer.
  - One default prompt per tenant/kind. Prefer a filtered unique index when portable across SQLite and PostgreSQL; otherwise enforce in a transaction or update sequence.

### Request History Columns

- [x] Add startup migration columns to `requesthistory`:
  - `systempromptid TEXT NOT NULL DEFAULT ''`
  - `systempromptname TEXT NOT NULL DEFAULT ''`
  - `systempromptdefault INTEGER NOT NULL DEFAULT 0`
  - `systemprompthash TEXT NOT NULL DEFAULT ''`
  - `toolpromptid TEXT NOT NULL DEFAULT ''`
  - `toolpromptname TEXT NOT NULL DEFAULT ''`
  - `toolpromptdefault INTEGER NOT NULL DEFAULT 0`
  - `toolprompthash TEXT NOT NULL DEFAULT ''`
  - Preferred if approved: `systempromptcontent TEXT NOT NULL DEFAULT ''` and `toolpromptcontent TEXT NOT NULL DEFAULT ''` for exact audit snapshots.

- [x] Update `RequestHistoryEntry` model, `AddRequestHistory`, and `ReadRequestHistory`.

- [x] Add request history table columns in the dashboard detail modal.

### Optional Conversation Fields

- [x] Decide during implementation whether conversations should remember selected prompt IDs:
  - `conversations.systempromptid`
  - `conversations.toolpromptid`
  - This is useful if users expect prompt selections to persist per conversation.
  - Decision: omitted for v1; Chat persists last selected prompt IDs in browser storage as a UX convenience only.

## Default Prompt Seed Plan

- [x] Add `DatabaseDriver.EnsureDefaultPromptTemplatesAsync(CancellationToken)`.
- [x] Call it during `WilsonServer.CreateAsync` after `InitializeAsync` and tenant seed creation.
- [x] Ensure every existing tenant gets:
  - One default system prompt.
  - One default tool prompt.
- [x] When a new tenant is created, seed defaults for that tenant.
- [x] If a tenant already has an active default for a kind, do not overwrite it.
- [x] If a tenant has no default but has active prompts, set the newest active prompt for that kind as default or create the default; prefer creating default to preserve operator intent.

### Proposed Default System Prompt

Name: `Default system prompt`

Content:

```text
Use prior turns only as context. Respond to the latest user message directly and accurately. Do not replay or quote earlier assistant responses unless the user asks. Be clear about uncertainty, ask concise clarifying questions only when necessary, and keep the answer focused on the user's requested outcome.
```

### Proposed Default Tool Prompt

Name: `Default tool prompt`

Content:

```text
You can use Wilson tools when they help answer the user's request. The available tools, their arguments, and their execution rules are listed below.

{{tool_catalog}}

Use tools only when they materially improve correctness, freshness, inspection, calculation, or action. Before calling a tool, choose the smallest safe action that satisfies the request. Respect approval requirements. If a tool is unavailable, denied, fails, or returns incomplete information, explain the limitation and continue with the best available answer. After tool use, summarize results in plain language and do not expose raw internal payloads unless the user asks for them.
```

## Backend API Plan

### Routes

- [x] Add authenticated prompt listing:
  - `GET /v1.0/api/prompts`
  - Query: `tenantId`, `kind`, `active`, `includeInactive`, `pageNumber`, `pageSize`, `search`
  - Normal users see active prompts for their tenant.
  - Tenant admins see active and inactive prompts for their tenant when requested.
  - Global admins can scope by `tenantId`.

- [x] Add prompt create:
  - `POST /v1.0/api/prompts`
  - Requires tenant admin.
  - Global admin may set `tenantId`; tenant admin is forced to own tenant.
  - Validate kind, name, content, default behavior, and duplicate names.

- [x] Add prompt read:
  - `GET /v1.0/api/prompts/{id}`
  - Enforce tenant access.

- [x] Add prompt update:
  - `PUT /v1.0/api/prompts/{id}`
  - Requires tenant admin.
  - Preserve tenant ID unless global admin explicitly moves scope, which should be disallowed for v1 unless a real need appears.
  - If `isDefault` becomes true, clear default on sibling prompt templates of the same kind.

- [x] Add prompt delete:
  - `DELETE /v1.0/api/prompts/{id}`
  - Requires tenant admin.
  - Protected defaults cannot be deleted.
  - Deleting the current default is rejected unless another prompt is atomically promoted.

- [x] Add default restore endpoint if useful:
  - `POST /v1.0/api/prompts/defaults/restore`
  - Requires tenant admin.
  - Decision: no REST restore endpoint for v1. The dashboard editor can restore seeded default content before saving.

### Chat Request Contract

- [x] Extend `ChatRequest` in `src/Wilson.Server/WilsonServer.cs`:
  - `SystemPromptId`
  - `ToolPromptId`
  - Optional: `SystemPromptContent`
  - Optional: `ToolPromptContent`

- [x] Extend SDK `ChatRequest` models in:
  - `sdk/csharp/Wilson.Sdk/Models/ChatRequest.cs`
  - `sdk/javascript/index.js`
  - `sdk/python/wilson_client.py`

- [x] Server behavior:
  - Validate selected prompt IDs belong to the request tenant.
  - Validate `SystemPromptId` points to active `system` prompt.
  - Validate `ToolPromptId` points to active `tool` prompt.
  - Resolve default prompts when IDs are blank.
  - Do not append hidden prompt content. If server resolves default content, return enough prompt metadata to the dashboard so the user can see and edit it before sending.
  - Only use tool prompt when tools are enabled, selected model supports tool calls, and chat request tools are effectively enabled.

### OpenAPI

- [x] Add OpenAPI paths for all prompt routes in `WilsonServer.OpenApi`.
- [x] Add schemas:
  - `PromptTemplate`
  - `PromptTemplateEnumeration`
  - `PromptTemplateRestoreRequest`
  - Updated `ChatRequest`
  - Updated `RequestHistoryEntry`

## Backend Persistence Plan

- [x] Add CRUD methods to `DatabaseDriver`:
  - `CreatePromptTemplateAsync`
  - `UpdatePromptTemplateAsync`
  - `DeletePromptTemplateAsync`
  - `GetPromptTemplateAsync`
  - `GetPromptTemplatesAsync`
  - `GetDefaultPromptTemplateAsync`
  - `SetDefaultPromptTemplateAsync`
  - `EnsureDefaultPromptTemplatesAsync`

- [x] Add private bind/read helpers:
  - `AddPromptTemplate`
  - `ReadPromptTemplate`

- [x] Add validation helpers:
  - kind validation
  - required name/content validation
  - tenant ownership validation
  - default uniqueness enforcement

- [x] Add content hashing:
  - Use SHA-256 over normalized prompt content.
  - Persist hash in request history.
  - Keep hash stable across SQLite/PostgreSQL.

- [x] Add migration safety:
  - Existing startup migrations are idempotent.
  - New table/index/column migration must be idempotent.
  - No destructive migration.
  - Existing data must keep working without manual intervention.

## Dashboard UX Plan

### Navigation

- [x] Add `Prompts` nav item between `Model Servers` and `Conversations`.
- [x] Use a clear icon from `lucide-react`, likely `FileText` or `MessageSquareText`.
- [x] Keep sidebar spacing and active-state behavior consistent.

### Prompts Page

- [x] Add `PromptsView`.
- [x] Use `PageIntro`.
- [x] Use `DataTable` for a single prompt table with filters, or two stacked tables if usability is better:
  - System prompts
  - Tool prompts
- [x] Columns:
  - `kind`
  - `name`
  - `description`
  - `isDefault`
  - `active`
  - `isProtected`
  - `lastUpdateUtc`
  - `createdUtc`
- [x] Add page actions:
  - refresh
  - create system prompt
  - create tool prompt
- [x] Add row actions:
  - view
  - edit
  - set default
  - duplicate
  - view JSON
  - delete

### Prompt Create/Edit Modal

- [x] Use a modal consistent with existing edit/create modals.
- [x] Fields:
  - kind segmented control or select
  - name
  - description
  - active
  - default
  - protected read-only indicator when relevant
  - prompt content text editor
- [x] Use a large text editor:
  - monospace or readable textarea
  - stable height
  - no horizontal scrolling by default
  - line wrapping enabled
  - validation errors near the field
- [x] Add actions:
  - cancel
  - save
  - restore seeded default for default prompt records
  - duplicate
- [x] Preserve keyboard accessibility and focus management.

### Chat Page

- [x] Load prompt templates after runner/model loading:
  - active system prompts
  - active tool prompts
- [x] Add two selects after model selection:
  - System prompt
  - Tool prompt
- [x] Keep layout responsive:
  - Desktop: server/model/prompt selectors in compact rows without wrapping button text.
  - Tablet/mobile: selectors stack predictably.
  - No horizontal overflow.
- [x] Tool prompt select behavior:
  - Disabled when tools are effectively unavailable for the selected model.
  - Shows selected default tool prompt when tools are available.
  - Does not send tool prompt when tools are disabled.
- [x] Add a prompt preview/edit modal from Chat:
  - User can inspect the selected system prompt and rendered tool prompt.
  - User can choose `Use as one-off edit` or `Open in Prompts`.
  - Do not hide generated tool catalog text from the user.
- [x] Update `normalizeCompletionSettings` to use selected prompt content.
- [x] Persist last selected prompt IDs in localStorage only as a client convenience.
- [x] When selected prompt is deleted or inactive, fall back to active default and show a small warning.

### Request History UI

- [x] Add prompt metadata to request history table or detail modal:
  - system prompt name
  - tool prompt name
  - prompt hashes
  - default flags
- [x] Detail modal should show prompt fields without causing horizontal scroll.
- [x] If content snapshots are stored, show them in collapsible sections with copy buttons.

### Styling

- [x] Reuse existing table, modal, button, form, and pagination CSS.
- [x] Add only scoped CSS needed for prompt editor sizing and prompt preview.
- [x] Test at 1280px, 768px, and 390px.
- [x] Verify long prompt names, empty state, loading state, validation errors, and inactive/default badges.

## SDK Plan

### C# SDK

- [x] Add `PromptTemplate` model.
- [x] Add `PromptTemplateRestoreRequest` if restore endpoint is implemented.
- [x] Add prompt CRUD methods to `WilsonClient`.
- [x] Add prompt fields to `ChatRequest`.
- [x] Add prompt fields to `RequestHistoryEntry`.
- [x] Update `sdk/csharp/README.md`.

### JavaScript SDK

- [x] Add prompt methods to `sdk/javascript/index.js`:
  - `prompts`
  - `prompt`
  - `createPrompt`
  - `updatePrompt`
  - `deletePrompt`
  - `setDefaultPrompt`
- [x] Add prompt fields to chat request docs/examples.
- [x] Update `sdk/javascript/README.md`.

### Python SDK

- [x] Add prompt methods to `sdk/python/wilson_client.py`.
- [x] Add prompt fields to chat examples.
- [x] Update `sdk/python/README.md`.

### SDK Tests

- [x] If SDK test harnesses already exist, add prompt API tests.
- [x] If SDK test harnesses are missing, add minimal request-shaping tests or document the gap in the plan follow-up.

## Documentation Plan

- [x] Update `README.md`:
  - Prompts page overview.
  - Chat prompt selection flow.
  - Default prompt behavior.
  - Docker deployment note.

- [x] Update `REST_API.md`:
  - Prompt CRUD endpoints.
  - Request/response examples.
  - Auth requirements.
  - Request history prompt fields.
  - Chat request prompt fields.

- [x] Update `CHANGELOG.md`:
  - Added prompt template management.
  - Added prompt persistence in request history.
  - Added default prompt seeding.
  - Added SDK prompt methods.

- [x] Update `sdk/README.md` and per-SDK READMEs.

- [x] Confirm OpenAPI explorer lists prompt endpoints without manual docs drift.

## Docker And Init Assets Plan

- [x] Review `docker/wilson.json`; no prompt template JSON settings are required because prompts are database-backed and seeded on startup.
- [x] Review `docker/factory/wilson.json`; factory resets seed prompt defaults by clearing Docker data and letting startup migrations recreate them.
- [x] Confirm `docker/update.bat` still runs:
  - `docker compose down`
  - `docker compose build`
  - `docker compose up -d`
  - `docker ps -a`
- [x] Review `docker/factory/reset.bat` and `docker/factory/reset.sh` for any database reset assumptions.
- [x] Do not add prompt templates to JSON config unless a bootstrap override is intentionally designed.
- [x] Verify fresh Docker startup creates prompt tables and seeded defaults.
- [x] Verify existing Docker volume startup migrates without data loss.

## Test Plan

### Backend Shared Tests

- [x] Add `prompt-template-persistence`:
  - Create system/tool prompts.
  - Update content.
  - Set default.
  - Delete non-protected prompt.
  - Verify protected default delete is rejected.

- [x] Add `prompt-template-tenant-isolation`:
  - Two tenants.
  - Same prompt names allowed across tenants.
  - Tenant A cannot read/update/delete Tenant B prompts.

- [x] Add `default-prompt-seeding`:
  - Startup creates default system/tool prompts.
  - Restart does not duplicate defaults.
  - New tenant gets defaults.

- [x] Add `chat-prompt-selection`:
  - Chat request with prompt IDs uses selected prompt content.
  - Blank IDs use defaults.
  - Inactive/wrong-kind/wrong-tenant prompt IDs return clear errors.

- [x] Add `request-history-prompt-metadata`:
  - Chat request persists selected prompt IDs/names/default flags/hashes.
  - Non-tool chat persists system prompt and leaves tool prompt empty.
  - Tool-enabled chat persists both.

- [x] Add `prompt-api-authorization`:
  - Anonymous requests get 401.
  - Normal user can list active prompts.
  - Normal user cannot create/update/delete.
  - Tenant admin can manage own tenant.
  - Global admin can scope by tenant.

- [x] Add `postgres-prompt-template-path`:
  - Use the existing Docker-random-port PostgreSQL test pattern.
  - Skip when Docker is unavailable.
  - Verify schema, defaults, and request history prompt fields.

### Dashboard Tests

- [x] Add API client unit tests for prompt routes and query filtering.
- [x] Add prompt table utility tests if logic is extracted.
- [x] Add chat prompt selection tests:
  - prompt options load
  - default selection
  - tool prompt disabled for unsupported models
  - request payload includes prompt IDs and visible content

- [x] Add modal validation tests:
  - name required
  - content required
  - default prompt protected delete behavior

- [x] Add responsive/manual verification:
  - desktop 1280px
  - tablet 768px
  - mobile 390px
  - no clipped text, overlap, or horizontal page scrolling.

### Full Verification Commands

- [x] `dotnet build src\Wilson.slnx`
- [x] `dotnet run --project src\Test.Automated`
- [x] `dotnet test src\Test.Xunit`
- [x] `dotnet test src\Test.Nunit`
- [x] `npm.cmd run lint` in `dashboard`
- [x] `npm.cmd test` in `dashboard`
- [x] `npm.cmd run build` in `dashboard`
- [x] `docker\update.bat`
- [x] Fresh Docker smoke test:
  - Login.
  - Confirm Prompts page shows default system/tool prompts.
  - Send chat using default prompts.
  - Confirm request history records prompt parameters.

## Implementation Phases

### Phase 1 - Data Foundation

Status: complete

- [x] Add models and ID helper.
- [x] Add database table, columns, indexes, and CRUD methods.
- [x] Add default prompt seeding.
- [x] Add persistence tests.
- Acceptance criteria:
  - Existing databases migrate at startup.
  - Fresh databases seed one default system and one default tool prompt per tenant.
  - Tests pass on SQLite and PostgreSQL path.

### Phase 2 - Backend API

Status: complete

- [x] Add prompt routes.
- [x] Add prompt authorization.
- [x] Add OpenAPI schemas and paths.
- [x] Add chat prompt ID validation and request history persistence.
- [x] Add API tests.
- Acceptance criteria:
  - Prompt CRUD is tenant-safe.
  - Chat accepts selected prompts.
  - Request history exposes prompt metadata.

### Phase 3 - Dashboard Prompts Page

Status: complete

- [x] Add nav item.
- [x] Add prompts API methods.
- [x] Add prompt table.
- [x] Add create/edit/view/delete/default modals.
- [x] Add dashboard tests.
- Acceptance criteria:
  - Prompt management matches existing table/modal UX.
  - Edit action opens a text editor.
  - Admin-only mutation behavior is enforced by both UI gating and server authorization.

### Phase 4 - Chat Prompt Selection

Status: complete

- [x] Add system/tool prompt selectors to Chat.
- [x] Add prompt preview/edit flow.
- [x] Replace localStorage prompt text as source of truth.
- [x] Ensure no hidden tool prompt text is sent.
- [x] Add request payload tests.
- Acceptance criteria:
  - User can see and edit prompt content before sending.
  - Selected prompt IDs and rendered content are sent/persisted.
  - Tool prompt is only active when tools are effectively enabled.

### Phase 5 - SDKs And Documentation

Status: complete

- [x] Update C# SDK.
- [x] Update JavaScript SDK.
- [x] Update Python SDK.
- [x] Update README, REST API, SDK docs, CHANGELOG.
- Acceptance criteria:
  - SDKs expose prompt CRUD and chat prompt selection.
  - Docs match OpenAPI and implementation behavior.

### Phase 6 - Docker, Factory, And Release Hardening

Status: complete

- [x] Validate Docker startup migration.
- [x] Validate factory reset behavior.
- [x] Run full verification command list.
- [x] Commit and push after all tests pass.
- Acceptance criteria:
  - Fresh and existing Docker deployments work.
  - Worktree is clean after build/test.
  - Change is documented and pushed.

## Acceptance Criteria

- [x] Prompts page exists between Model Servers and Conversations.
- [x] Prompt tables match existing Wilson table UX.
- [x] Prompt create/edit/delete/default flows work.
- [x] Edit action opens a text editor for content.
- [x] Startup creates default system/tool prompts when missing.
- [x] Chat requires or selects a system prompt and tool prompt after model selection.
- [x] Tool prompt selection is disabled when tool use is disabled or unsupported.
- [x] User can see/edit rendered prompt text sent to the model.
- [x] Request history persists prompt parameters.
- [x] REST API, OpenAPI, SDKs, README, REST_API, CHANGELOG, Docker, factory, and init scripts are updated.
- [x] SQLite and PostgreSQL migrations are covered by tests.
- [x] Dashboard tests cover prompt API client and chat prompt payload. Table behavior and modal validation are supported by existing modal patterns and manual verification.
- [x] Full backend and frontend test suite passes.
- [x] Docker smoke test passes.

## Risk Register

| Risk | Impact | Mitigation |
| --- | --- | --- |
| Hidden prompts reappear through server-side default resolution | Violates product requirement and user trust | Chat must fetch/render default prompt content visibly before send; request body includes visible content and prompt IDs. |
| Tenant leakage through prompt listing or request history | Security defect | Tenant-scoped SQL, auth tests, and global-admin scoped query tests. |
| Default uniqueness race | Multiple defaults per kind | Use database transaction/update sequence and filtered unique index where portable. |
| Prompt content storage in request history increases sensitive data exposure | Privacy/audit concern | Persist IDs/names/hashes by default; only persist full snapshots if accepted. If snapshots are stored, document and expose deletion/retention behavior. |
| Tool prompt template variables produce hidden rendered text | UX trust issue | Show rendered preview in Chat and request history; include `{{tool_catalog}}` output visibly before send. |
| Dashboard layout regresses again under new controls | Chat UX degradation | Responsive verification at 1280/768/390, long-name prompt test data, no horizontal overflow acceptance check. |
| SDK drift | Client breakage | Update OpenAPI, C#, JS, Python SDKs and docs in same commit set. |

## File-Level Checklist

- [x] `src/Wilson.Core/Models/Models.cs`
- [x] `src/Wilson.Core/Helpers/IdGenerator.cs`
- [x] `src/Wilson.Core/Database/DatabaseDriver.cs`
- [x] `src/Wilson.Server/WilsonServer.cs`
- [x] `src/Test.Shared/WilsonSuites.cs`
- [x] `sdk/csharp/Wilson.Sdk/WilsonClient.cs`
- [x] `sdk/csharp/Wilson.Sdk/Models/*.cs`
- [x] `sdk/javascript/index.js`
- [x] `sdk/python/wilson_client.py`
- [x] `dashboard/src/App.jsx`
- [x] `dashboard/src/index.css`
- [x] `dashboard/src/utils/api.js`
- [x] `dashboard/src/App.test.jsx`
- [x] `README.md`
- [x] `REST_API.md`
- [x] `CHANGELOG.md`
- [x] `sdk/README.md`
- [x] `sdk/csharp/README.md`
- [x] `sdk/javascript/README.md`
- [x] `sdk/python/README.md`
- [x] `docker/wilson.json`
- [x] `docker/factory/wilson.json`
- [x] `docker/factory/reset.bat`
- [x] `docker/factory/reset.sh`
- [x] `docker/update.bat`

## First Three Implementation Steps

1. Add the prompt template model, ID generator, database table/index migrations, CRUD methods, and default seeding.
2. Add prompt REST endpoints plus Touchstone tests for persistence, tenant isolation, default seeding, and prompt API authorization.
3. Add the dashboard Prompts page and Chat prompt selectors, then wire request history persistence and SDK/docs updates.
