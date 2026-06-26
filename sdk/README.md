# Wilson SDKs

Wilson includes small first-party SDK surfaces for common API automation:

- `csharp/` - typed .NET client
- `javascript/` - browser/Node client
- `python/` - standard-library Python client

Each SDK exposes authentication, model-server enumeration, model-server health, and read APIs for Wilson tool metadata:

- `POST /v1.0/api/chat`
- `GET /v1.0/api/model-runners`
- `GET /v1.0/api/model-runners/health`
- `GET /v1.0/api/model-runners/{id}/health`
- `GET /v1.0/api/tools`
- `POST /v1.0/api/tools/validate`
- `POST /v1.0/api/tools/test`
- `GET /v1.0/api/mcp`
- `POST /v1.0/api/mcp/reload`
- `GET /v1.0/api/tools/{name}`
- `GET /v1.0/api/tool-runs/{id}`
- `GET /v1.0/api/conversations/{id}/tool-calls`
- `GET /v1.0/api/request-history/{id}/tool-calls`

Tool-enabled non-streaming chat can pass `toolsEnabled`, `approvalPolicy`, `toolNames`, and admin-only `workingDirectory` request options. Chat responses expose safe `toolCalls` trace metadata and aggregate `toolMetrics`.

Tool-call records are redacted summaries intended for chat history, audit review, and operations dashboards. Normal SDK responses do not expose raw model arguments, raw tool output, or provider request identifiers from chat traces.

Tool diagnostics calls are admin-only. They validate draft settings without saving them and can test runner readiness before admins expose tools to chat users.
