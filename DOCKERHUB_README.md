# Wilson — a chat server that does not phone home

**Wilson** is a local-first chat server and dashboard for talking to Ollama, OpenAI, and OpenAI-compatible model runners. It gives you a browser dashboard, a REST API, tenant-aware users and credentials, conversation storage, request history, feedback capture, model-directed tools, and background model-runner health checks — all running on infrastructure you control.

No signal. No noise. No rescue ships. Just Wilson, your local model, and a chat window that does not need to reach the outside world.

This image runs the **Wilson backend server**: a C# service on Watson 7.1 that hosts the REST API, streams model responses over server-sent events, stores conversations in SQLite or PostgreSQL, runs the agentic tool loop, and emits OpenTelemetry metrics, traces, and logs over OTLP. It is meant to run as part of the Wilson Docker Compose stack alongside the dashboard and, when you want them, Prometheus, Tempo, Loki, and Grafana.

![Wilson](https://raw.githubusercontent.com/jchristn/Wilson/main/assets/logo.png)

## Who it's for

Wilson fits three kinds of people, and the same server serves all of them.

- **Anyone who wants a private ChatGPT-style window** over a local Ollama model. Point Wilson at your runner, open the dashboard, and start talking. Reasoning models get a collapsible **Thinking** panel so you can watch the model work without cluttering the answer.
- **Small teams sharing model infrastructure.** Tenants, users, credentials, admin tokens, and bearer-token auth keep access scoped. Everyone's conversations, feedback, and request history land in one database instead of scattered browser tabs.
- **Developers building model-backed workflows.** There is an OpenAPI-described REST API, C#/JS/Python SDKs, a Postman collection, request-history capture with timing and token estimates, and model-directed tools with redacted audit trails — enough to instrument a workflow and see exactly what happened.

## What it does

Wilson chats with local Ollama models or OpenAI-compatible APIs and streams the responses as they arrive. It keeps conversation history, manages several configured model servers at once, and probes each one in the background with healthy/unhealthy thresholds and recent history. From the dashboard you can pull and load Ollama models, see which models are available and loaded, and switch runners mid-conversation.

The parts that matter for operators sit close to the surface. Every request is captured with status, latency, time-to-first-token, and token estimates. Thumbs-up and thumbs-down feedback, with optional comments, is stored against the message that earned it. When you enable tools, the model can read and write files, run processes, retrieve and search the web, and call external MCP servers — each execution approval-gated where it matters, with raw arguments and output redacted before anything is persisted or returned. Prompt templates are tenant-scoped database records, seeded with a sensible default system prompt and tool prompt for every tenant.

## Architecture

```
Browser dashboard ─┐
REST / SDK client ─┼─► Wilson backend (Watson 7.1, C# / .NET 10)
MCP-aware client  ─┘        │
                            ├─ Inference (PolyPrompt) ─► Ollama / OpenAI-compatible runners
                            ├─ Agentic tool loop ─► built-in tools + MCP servers
                            ├─ Storage ─► SQLite or PostgreSQL
                            └─ Telemetry (Radiant/OTLP) ─► Collector ─► Prometheus / Tempo / Loki ─► Grafana
```

A chat request flows in over `/v1.0/api/chat` (or its streaming sibling), Wilson builds the prompt with truncation, calls the runner through PolyPrompt, separates any model reasoning from the visible answer, and persists the turn. Tool-enabled chats run the same path through an agentic loop that streams text and reasoning live while executing approved tool calls. Metrics and spans are emitted for the webserver, every request path, and each service, so a slow conversation or a failing tool shows up on a Grafana dashboard organized by functional area.

## Getting started

Run the Compose stack from the repository rather than this image alone — the server expects the dashboard next to it, and the observability services are wired to a collector.

```bash
git clone https://github.com/jchristn/Wilson
cd Wilson/docker
docker compose pull
docker compose up -d
```

Then open the dashboard at `http://127.0.0.1:9401` and the backend at `http://127.0.0.1:9400` (OpenAPI at `/openapi.json`, Swagger UI at `/swagger`). Grafana comes up at `http://127.0.0.1:3000` with dashboards for HTTP, chat and inference, tools, model runners, the database, and MCP.

On the login page, use server URL `http://127.0.0.1:9400` with access key `wilsonadmin` or admin token `wilson-admin-dev-token`. Both are seeded defaults — **change them before exposing Wilson to anything you do not trust.**

Configuration lives in a mounted `wilson.json`: the listener, database, CORS, auth, request history, tools and MCP, model runners, telemetry, and first-run seed values. The dashboard Settings page edits the same file; most changes apply immediately, while listener and database changes need a restart. The Compose file mounts a named `/workspace` volume that Wilson's file and process tools are scoped to — keep `tools.workingDirectory` and `tools.allowedRoots` pointed at that container path, and do not mount a broad host directory into a runner you have exposed.

## Related images

The dashboard ships as its own image, `jchristn77/wilson-dashboard`, and the two are built and versioned together. The rest of the stack pulls stock upstream images (PostgreSQL, the OpenTelemetry Collector, Prometheus, Tempo, Loki, and Grafana), pinned in the Compose file.

## Tags

- `latest` — the most recent build.
- Version tags such as `v0.1.0` track releases recorded in `CHANGELOG.md`. Pin an exact version for anything real; Wilson is in its `0.x` series and behavior can change between releases.

## License

MIT. Copyright (c) 2026 Joel Christner. See [LICENSE.md](https://github.com/jchristn/Wilson/blob/main/LICENSE.md).

If Wilson stops talking back, grab the logs. They tend to be more useful than shouting at the horizon.
