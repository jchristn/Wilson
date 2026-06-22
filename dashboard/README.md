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

## Checks

```powershell
npm run lint
npm run build
```
