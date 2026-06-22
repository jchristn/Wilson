# Changelog

## 0.3.0

- Added background model server health checks with configurable URL, method, interval, timeout, expected status, thresholds, and auth.
- Added model server health API routes and embedded health snapshots in model server list responses.
- Added a fast model server listing mode that returns configured servers and cached health without waiting on upstream model APIs.
- Added dashboard health summary metrics, health badges, recent health histograms, health detail modal, and health-check settings editors.
- Added C#, JavaScript, and Python SDK surfaces for model runner and model runner health APIs.
- Added a Postman collection and updated configuration examples for health-check fields.

## 0.2.0

- Added dashboard branding, favicon, GitHub link, theme toggle, and improved topbar identity display.
- Added Model Servers page with Ollama available/loaded model status, model pull, model load, and model server CRUD.
- Added conversation management with rename/delete actions and a Conversations workspace page.
- Improved Chat streaming, model-load UX, response timing details, feedback comments, and invalid-model error handling.
- Added request history charts, detailed request/response metadata capture, collapsible payload sections, and copy/prettify controls.
- Added structured Settings forms, row-based list editing, API Explorer improvements, OpenAPI JSON, and Swagger UI.
- Added Docker factory reset scripts and updated default backend/dashboard ports to 9400/9401.

## 0.1.0

- Initial Wilson backend, dashboard, database, inference, Docker, and test implementation.
