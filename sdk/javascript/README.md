# Wilson JavaScript SDK

Small browser/Node client for Wilson authentication, model-server listing, model-server health, prompt templates, and tool metadata/history reads.

```js
import { WilsonClient } from './index.js';

const client = new WilsonClient('http://127.0.0.1:9400');
await client.login('wilsonadmin');

const runners = await client.modelRunners({ pageNumber: 1, pageSize: 100 });
const health = await client.modelRunnerHealth();
const local = await client.modelRunnerHealthById('local-ollama');
const tools = await client.tools();
const prompts = await client.prompts({ kind: 'System' });
const toolPrompt = await client.createPrompt({ kind: 'Tool', name: 'Project tools', content: '{{tool_catalog}}' });
const validation = await client.validateTools({ tools: { enabled: true, workingDirectory: 'C:/Code/Wilson', allowedRoots: ['C:/Code/Wilson'], defaultApprovalPolicy: 'auto' } });
const readiness = await client.testTools({ tools: { enabled: true, workingDirectory: 'C:/Code/Wilson', allowedRoots: ['C:/Code/Wilson'], defaultApprovalPolicy: 'auto' }, runnerId: 'local-ollama' });
const mcp = await client.mcpStatus();
const chat = await client.chat({ runnerId: 'local-ollama', model: 'llama3.1', prompt: 'Read README.md', systemPromptId: prompts.objects[0].id, toolPromptId: toolPrompt.id, toolsEnabled: true, approvalPolicy: 'auto', toolNames: ['read_file'] });
const readFile = await client.tool('read_file');
const conversationTools = await client.conversationToolCalls('conversation-id', { pageNumber: 1, pageSize: 100 });
```

Health responses match the Wilson API contract: `endpointId`, `endpointName`, `isHealthy`, `lastCheckUtc`, `uptimePercentage`, `consecutiveSuccesses`, `consecutiveFailures`, `lastError`, and `history`.

Tool-call history methods return redacted Wilson records. `chat.toolCalls` contains safe trace metadata only and does not expose raw model arguments, raw tool output, or provider request IDs.

Tool diagnostics methods require an admin token. `validateTools` checks draft tool settings without saving them. `testTools` adds runner capability checks when `runnerId` is supplied.

Prompt `kind` values are enum strings: `System` or `Tool`.
