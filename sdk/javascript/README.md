# Wilson JavaScript SDK

Small browser/Node client for Wilson authentication, model-server listing, model-server health, and tool metadata/history reads.

```js
import { WilsonClient } from './index.js';

const client = new WilsonClient('http://127.0.0.1:9400');
await client.login('wilsonadmin');

const runners = await client.modelRunners({ pageNumber: 1, pageSize: 100 });
const health = await client.modelRunnerHealth();
const local = await client.modelRunnerHealthById('local-ollama');
const tools = await client.tools();
const validation = await client.validateTools({ tools: { enabled: true, workingDirectory: 'C:/Code/Wilson', allowedRoots: ['C:/Code/Wilson'], defaultApprovalPolicy: 'auto' } });
const readiness = await client.testTools({ tools: { enabled: true, workingDirectory: 'C:/Code/Wilson', allowedRoots: ['C:/Code/Wilson'], defaultApprovalPolicy: 'auto' }, runnerId: 'local-ollama' });
const readFile = await client.tool('read_file');
const conversationTools = await client.conversationToolCalls('conversation-id', { pageNumber: 1, pageSize: 100 });
```

Health responses match the Wilson API contract: `endpointId`, `endpointName`, `isHealthy`, `lastCheckUtc`, `uptimePercentage`, `consecutiveSuccesses`, `consecutiveFailures`, `lastError`, and `history`.

Tool-call history methods return redacted Wilson records. Normal chat traces and history reads do not expose raw model arguments, raw tool output, or provider request IDs.

Tool diagnostics methods require an admin token. `validateTools` checks draft tool settings without saving them. `testTools` adds runner capability checks when `runnerId` is supplied.
