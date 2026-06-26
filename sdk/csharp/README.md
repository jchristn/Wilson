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
McpStatusResponse mcp = await client.GetMcpStatusAsync();
ToolPolicyValidationResult validation = await client.ValidateToolsAsync(new { enabled = true, workingDirectory = "C:/Code/Wilson", allowedRoots = new[] { "C:/Code/Wilson" }, defaultApprovalPolicy = "auto" });
ToolPolicyTestResult readiness = await client.TestToolsAsync(new { enabled = true, workingDirectory = "C:/Code/Wilson", allowedRoots = new[] { "C:/Code/Wilson" }, defaultApprovalPolicy = "auto" }, "local-ollama");
ChatResponse chat = await client.ChatAsync(new ChatRequest { RunnerId = "local-ollama", Model = "llama3.1", Prompt = "Read README.md", ToolsEnabled = true, ApprovalPolicy = "auto", ToolNames = new List<string> { "read_file" } });
ToolDescriptor readFile = await client.GetToolAsync("read_file");
EnumerationResult<ToolExecutionRecord> conversationTools = await client.GetConversationToolCallsAsync("conversation-id");
```

Health responses are exposed as `EndpointHealthStatus` with `EndpointId`, `EndpointName`, `IsHealthy`, `LastCheckUtc`, `UptimePercentage`, `ConsecutiveSuccesses`, `ConsecutiveFailures`, `LastError`, and `History`.

Tool-call history methods return redacted Wilson records. `ChatResponse.ToolCalls` contains safe trace metadata only and does not expose raw model arguments, raw tool output, or provider request IDs.

Tool diagnostics methods require an admin token. `ValidateToolsAsync` checks draft tool settings without saving them. `TestToolsAsync` adds runner capability checks when `runnerId` is supplied.
