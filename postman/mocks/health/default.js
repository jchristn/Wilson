// Provider header contracts: Anthropic API overview; Gemini API keys; Azure models-list REST API.
const http = require('http');
http.createServer((req, res) => {
  const provider = req.url.startsWith('/probe/') ? req.url.slice('/probe/'.length) : null;
  const headers = req.headers;
  let valid = req.method === 'GET';
  if (provider === 'Anthropic') valid &&= headers['x-api-key'] === 'test-health-key' && headers['anthropic-version'] === '2023-06-01' && !headers.authorization;
  else if (provider === 'Gemini') valid &&= headers['x-goog-api-key'] === 'test-health-key' && !headers.authorization;
  else if (provider === 'AzureOpenAI') valid &&= headers['api-key'] === 'test-health-key' && !headers.authorization;
  else if (provider === 'Ollama') valid &&= !headers.authorization;
  else if (provider) valid &&= headers.authorization === 'Bearer test-health-key';
  else valid &&= req.url === '/status';
  res.writeHead(valid ? 200 : 401, { 'Content-Type': 'application/json' });
  res.end(JSON.stringify({ status: valid ? 'ok' : 'invalid provider headers' }));
}).listen(process.env.PORT);
