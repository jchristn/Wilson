# Wilson Dashboard

React/Vite dashboard for Wilson.

## Development

```powershell
npm install
npm run dev
```

The dashboard defaults to `http://127.0.0.1:9401` and connects to a Wilson server at `http://127.0.0.1:9400`.

## Model Server Health

The Model Servers view shows:

- Aggregate healthy, unhealthy, and awaiting-check counts
- Per-server health badges
- Recent health check histograms
- Automatic health refresh every 15 seconds on the page and health detail modal
- Health detail modal with uptime, consecutive success/failure counts, last error, timestamps, history, and probe configuration
- Health-check settings in both the Model Server editor and Settings page

## Tool Activity

When Wilson tools are enabled by the server and by the selected model runner, Chat shows a compact tool toggle, approval-policy selector, and available-tool catalog. Tool responses render a low-profile "Tool activity" disclosure under assistant messages. Reopened conversations reload persisted tool calls, and request-history details show linked tool activity for administrators. Settings includes web-search provider controls plus MCP server configuration, status, and reload controls for discovered external tools.

The Settings page exposes the global tool switch, built-in tool policy, working directory, allowed roots, limits, and per-runner tool capability flags. Fresh tool-capable configs are enabled by default for safe tools while destructive/process tools remain approval-required.

Tool diagnostics are built into the same Settings page. Validate checks draft settings before saving, while Test checks the selected runner's tool readiness without calling a model or executing a tool.

## Checks

```powershell
npm run lint
npm run build
```
