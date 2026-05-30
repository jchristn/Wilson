# Wilson

Wilson is a Watson 7 C# server and React dashboard for tenant-aware chat against Ollama or OpenAI-compatible model runners.

## Features

- Bearer-token authentication against Wilson
- First-run seeding for tenant, admin user, and user credential
- SQLite and PostgreSQL database support
- Model runner settings stored in editable JSON
- ChatGPT-style dashboard with conversation history, rename/delete actions, feedback, and response timing details
- Non-streaming and SSE streaming chat
- Context preservation with automatic truncation by runner context window
- Ollama model discovery, loaded-model status, model loading, and model pull support
- Feedback capture with optional free-form comments
- Admin views for model servers, conversations, tenants, users, credentials, feedback, request history, API explorer, and settings
- Request history with latency charts and captured request/response metadata
- OpenAPI JSON and Swagger UI
- Docker Compose and `docker/factory` reset/settings assets

## Run Locally

```powershell
dotnet run --project src\Wilson.Server
```

The backend defaults to `http://127.0.0.1:9400`. The server creates `wilson.json` on first start. Default credentials are also printed at startup:

- Admin bearer token: `wilson-admin-dev-token`
- User access key: `wilsonadmin`

Run the dashboard:

```powershell
cd dashboard
npm install
npm run dev
```

The dashboard defaults to `http://127.0.0.1:9401`. Use `http://127.0.0.1:9400` as the server URL and one of the bearer tokens above as the access key.

## Docker

```powershell
cd docker
docker compose up --build
```

Docker exposes the backend on `9400` and the dashboard on `9401`. Factory reset scripts are available in `docker/factory/reset.bat` and `docker/factory/reset.sh`.

## Configuration

`wilson.json` contains REST, database, CORS, auth, request history, seed, and model runner settings. Ollama runners may omit `Models`; OpenAI-compatible runners should provide model names for the selector.

The dashboard Settings page edits the same configuration file and applies supported runtime changes. REST listener and database connection changes require a server restart.

## Tests

```powershell
dotnet build src\Wilson.slnx
dotnet run --project src\Test.Automated
cd dashboard
npm run build
```

## Repository

Remote:

```powershell
git remote add origin https://github.com/jchristn/Wilson
```
