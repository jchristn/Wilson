# Wilson C# SDK

Typed .NET client for Wilson authentication, model-server listing, model-server health, and tool metadata/history reads.

```csharp
using System.Collections.Generic;
using Wilson.Sdk;
using Wilson.Sdk.Models;

using WilsonClient client = new WilsonClient("http://127.0.0.1:9400");
await client.LoginAsync("wilsonadmin");

EnumerationResult<ModelRunnerStatus> runners = await client.GetModelRunnersAsync(pageSize: 100);
List<EndpointHealthStatus> health = await client.GetModelRunnerHealthAsync();
EndpointHealthStatus local = await client.GetModelRunnerHealthAsync("local-ollama");
List<ToolDescriptor> tools = await client.GetToolsAsync();
ToolPolicyValidationResult validation = await client.ValidateToolsAsync(new { enabled = true, workingDirectory = "C:/Code/Wilson", allowedRoots = new[] { "C:/Code/Wilson" }, defaultApprovalPolicy = "auto" });
ToolPolicyTestResult readiness = await client.TestToolsAsync(new { enabled = true, workingDirectory = "C:/Code/Wilson", allowedRoots = new[] { "C:/Code/Wilson" }, defaultApprovalPolicy = "auto" }, "local-ollama");
ToolDescriptor readFile = await client.GetToolAsync("read_file");
EnumerationResult<ToolExecutionRecord> conversationTools = await client.GetConversationToolCallsAsync("conversation-id");
```

Health responses are exposed as `EndpointHealthStatus` with `EndpointId`, `EndpointName`, `IsHealthy`, `LastCheckUtc`, `UptimePercentage`, `ConsecutiveSuccesses`, `ConsecutiveFailures`, `LastError`, and `History`.

Tool-call history methods return redacted Wilson records. Normal chat traces and history reads do not expose raw model arguments, raw tool output, or provider request IDs.

Tool diagnostics methods require an admin token. `ValidateToolsAsync` checks draft tool settings without saving them. `TestToolsAsync` adds runner capability checks when `runnerId` is supplied.
