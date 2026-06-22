# Wilson C# SDK

Typed .NET client for Wilson authentication, model-server listing, and model-server health.

```csharp
using Wilson.Sdk;

using WilsonClient client = new WilsonClient("http://127.0.0.1:9400");
await client.LoginAsync("wilsonadmin");

var runners = await client.GetModelRunnersAsync(pageSize: 100);
var health = await client.GetModelRunnerHealthAsync();
var local = await client.GetModelRunnerHealthAsync("local-ollama");
```

Health responses are exposed as `EndpointHealthStatus` with `EndpointId`, `EndpointName`, `IsHealthy`, `LastCheckUtc`, `UptimePercentage`, `ConsecutiveSuccesses`, `ConsecutiveFailures`, `LastError`, and `History`.
