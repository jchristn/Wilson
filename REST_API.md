# Wilson REST API

Wilson exposes OpenAPI JSON at `/openapi.json` and Swagger UI at `/swagger`. All `/v1.0/api/*` routes require a bearer token except health checks and token creation.

## Authentication

Create or reuse a Wilson access key, then send it as a bearer token:

```http
Authorization: Bearer wilsonadmin
```

To validate an access key:

```http
POST /v1.0/auth/token
Content-Type: application/json

{
  "accessKey": "wilsonadmin"
}
```

## Tool APIs

Tools are disabled by default. Administrators enable them in settings and each model runner must advertise a supported tool-call format before Wilson sends tools to a model.

Implemented built-in tools:

- `read_file`, `file_metadata`, `list_directory`, `glob`, `grep`
- `write_file`, `edit_file`, `multi_edit`, `delete_file`, `manage_directory`
- `run_process`
- `web_retrieve` for absolute `http` and `https` URLs

Write, edit, delete, directory-management, and process tools are marked dangerous and approval-required. Use narrow `allowedRoots` and avoid automatic approval unless the deployment is trusted and admin-only.

Tool audit payloads are redacted before persistence. Persisted arguments are redacted and capped when argument storage is enabled. Full result payloads are not persisted unless `tools.storeFullToolResults` is explicitly enabled; otherwise Wilson stores redacted summaries and previews. Chat responses always use safe tool traces, not audit records.

### List Tools

```http
GET /v1.0/api/tools
```

Returns effective tool descriptors for the current server configuration. Descriptors include availability and non-secret unavailable reasons.

### Get Tool

```http
GET /v1.0/api/tools/{name}
```

Returns one tool descriptor by name.

### Validate Draft Tool Policy

```http
POST /v1.0/api/tools/validate
Content-Type: application/json

{
  "tools": {
    "enabled": true,
    "defaultApprovalPolicy": "auto",
    "workingDirectory": "C:\\Code\\Wilson",
    "allowedRoots": ["C:\\Code\\Wilson"]
  }
}
```

Requires a global administrator bearer token. Wilson normalizes the draft tool settings without saving them, then returns effective descriptors, `availableToolCount`, warnings, and blocking errors. Use this before saving settings to catch missing working directories, empty allowed roots, unknown enabled tool names, or disabled built-ins.

### Test Tool Readiness

```http
POST /v1.0/api/tools/test
Content-Type: application/json

{
  "runnerId": "local-ollama",
  "tools": {
    "enabled": true,
    "defaultApprovalPolicy": "auto",
    "workingDirectory": "C:\\Code\\Wilson",
    "allowedRoots": ["C:\\Code\\Wilson"]
  }
}
```

Requires a global administrator bearer token. The readiness test is a dry run: it does not call a model and does not execute tools. It validates the draft tool policy and, when `runnerId` is supplied, checks that the selected runner exists, enables tools, supports tool calls, and has an effective tool-call wire format.

### Conversation Tool Calls

```http
GET /v1.0/api/conversations/{id}/tool-calls?pageNumber=1&pageSize=100
```

Returns redacted persisted tool-call records for a conversation visible to the authenticated principal. Conversation owners can read their own records; tenant and global administrators can read records within their scope.

Records are tenant scoped and redacted. `argumentsJson`, `resultJson`, `resultSummaryJson`, `resultPreview`, and error fields should be treated as audit data, not raw provider or tool output.

### Request-History Tool Calls

```http
GET /v1.0/api/request-history/{id}/tool-calls?pageNumber=1&pageSize=100
```

Returns redacted tool-call records linked to one request-history entry. This route requires tenant administrator or global administrator access.

### Tool Run Detail

```http
GET /v1.0/api/tool-runs/{id}
```

Returns a persisted tool run with the redacted tool-call records for that run. Global administrators should include `tenantId` when using an admin bearer token that is not tenant-scoped.

## Chat Tool Responses

Non-streaming chat accepts these tool fields:

```json
{
  "toolsEnabled": true,
  "approvalPolicy": "auto",
  "toolNames": ["read_file"],
  "workingDirectory": "C:\\Code\\Wilson"
}
```

Only administrators may override `workingDirectory` per request. `approvalPolicy` accepts `deny`, `ask`, or `auto`; non-streaming chat rejects interactive `ask` approval until streaming approval events are implemented.

Tool-capable runners must advertise a supported tool-call format before Wilson sends tool definitions. Use `OpenAIChatCompletions` for OpenAI/OpenAI-compatible chat-completions endpoints. Use `OllamaChat` for Ollama `/api/chat` when the selected Ollama model supports tools. If a runner is disabled for tools, lacks a supported wire format, or rejects tool calls, Wilson leaves standard chat behavior available and tool diagnostics report the compatibility problem.

For container deployments, configure `workingDirectory` and `allowedRoots` with container paths. If a host workspace is mounted to `/workspace`, use `/workspace` in Wilson settings rather than the host path.

When tools run, `ChatResponse` includes:

- `toolRun`: run metadata such as run ID, status, elapsed time, iteration count, call count, and error count.
- `toolCalls`: safe chat traces containing tool name, status, runtime, counts, timestamps, and summary.
- `toolMetrics`: aggregate tool-call count, error count, iteration count, and total tool runtime.

Normal chat traces do not expose raw model arguments, raw tool output, provider request IDs, API keys, bearer tokens, passwords, or hidden policy details.

Audit APIs may return redacted arguments and redacted result summaries. Redacted full result JSON is returned only for records created while full result persistence was enabled.

## Request History

Request-history entries include tool metrics for tool-enabled chat requests:

- `toolRunId`
- `toolCallCount`
- `toolElapsedMs`
- `agentIterations`

Linked tool-call records are attached after the request-history row is persisted.
