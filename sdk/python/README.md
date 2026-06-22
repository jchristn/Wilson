# Wilson Python SDK

Small standard-library client for Wilson authentication, model-server listing, and model-server health.

```python
from wilson_client import WilsonClient

client = WilsonClient("http://127.0.0.1:9400")
client.login("wilsonadmin")

runners = client.model_runners(pageNumber=1, pageSize=100)
health = client.model_runner_health()
local = client.model_runner_health_by_id("local-ollama")
```

Health responses include `endpointId`, `endpointName`, `isHealthy`, `lastCheckUtc`, `uptimePercentage`, `consecutiveSuccesses`, `consecutiveFailures`, `lastError`, and `history`.
