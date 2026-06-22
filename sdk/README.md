# Wilson SDKs

Wilson includes small first-party SDK surfaces for common API automation:

- `csharp/` - typed .NET client
- `javascript/` - browser/Node client
- `python/` - standard-library Python client

Each SDK exposes authentication, model-server enumeration, and model-server health:

- `GET /v1.0/api/model-runners`
- `GET /v1.0/api/model-runners/health`
- `GET /v1.0/api/model-runners/{id}/health`
