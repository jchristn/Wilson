# Wilson JavaScript SDK

Small browser/Node client for Wilson authentication, model-server listing, and model-server health.

```js
import { WilsonClient } from './index.js';

const client = new WilsonClient('http://127.0.0.1:9400');
await client.login('wilsonadmin');

const runners = await client.modelRunners({ pageNumber: 1, pageSize: 100 });
const health = await client.modelRunnerHealth();
const local = await client.modelRunnerHealthById('local-ollama');
```

Health responses match the Wilson API contract: `endpointId`, `endpointName`, `isHealthy`, `lastCheckUtc`, `uptimePercentage`, `consecutiveSuccesses`, `consecutiveFailures`, `lastError`, and `history`.
