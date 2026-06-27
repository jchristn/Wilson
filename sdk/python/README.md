# Wilson Python SDK

Small standard-library client for Wilson authentication, model-server listing, model-server health, prompt templates, and tool metadata/history reads.

```python
from wilson_client import WilsonClient

client = WilsonClient("http://127.0.0.1:9400")
client.login("wilsonadmin")

runners = client.model_runners(pageNumber=1, pageSize=100)
health = client.model_runner_health()
local = client.model_runner_health_by_id("local-ollama")
tools = client.tools()
prompts = client.prompts(kind="System")
tool_prompt = client.create_prompt({"kind": "Tool", "name": "Project tools", "content": "{{tool_catalog}}"})
validation = client.validate_tools({"tools": {"enabled": True, "workingDirectory": "C:/Code/Wilson", "allowedRoots": ["C:/Code/Wilson"], "defaultApprovalPolicy": "auto"}})
readiness = client.test_tools({"tools": {"enabled": True, "workingDirectory": "C:/Code/Wilson", "allowedRoots": ["C:/Code/Wilson"], "defaultApprovalPolicy": "auto"}, "runnerId": "local-ollama"})
mcp = client.mcp_status()
chat = client.chat({"runnerId": "local-ollama", "model": "llama3.1", "prompt": "Read README.md", "systemPromptId": prompts["objects"][0]["id"], "toolPromptId": tool_prompt["id"], "toolsEnabled": True, "approvalPolicy": "auto", "toolNames": ["read_file"]})
read_file = client.tool("read_file")
conversation_tools = client.conversation_tool_calls("conversation-id", pageNumber=1, pageSize=100)
```

Health responses include `endpointId`, `endpointName`, `isHealthy`, `lastCheckUtc`, `uptimePercentage`, `consecutiveSuccesses`, `consecutiveFailures`, `lastError`, and `history`.

Tool-call history methods return redacted Wilson records. `chat["toolCalls"]` contains safe trace metadata only and does not expose raw model arguments, raw tool output, or provider request IDs.

Tool diagnostics methods require an admin token. `validate_tools` checks draft tool settings without saving them. `test_tools` adds runner capability checks when `runnerId` is supplied.

Prompt `kind` values are enum strings: `System` or `Tool`.
