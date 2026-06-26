namespace Test.Shared
{
    using System;
    using System.Net;
    using System.Net.Http;
    using System.Net.Sockets;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using Voltaic;

    /// <summary>
    /// Minimal streamable HTTP MCP server fixture.
    /// </summary>
    public sealed class TestMcpHttpServer : IDisposable
    {
        private readonly McpHttpServer _Server;
        private readonly CancellationTokenSource _Cts = new CancellationTokenSource();
        private Task? _RunTask = null;
        private bool _Disposed = false;

        /// <summary>
        /// Base server URL.
        /// </summary>
        public string BaseUrl { get; }

        /// <summary>
        /// MCP path.
        /// </summary>
        public string McpPath => "/mcp";

        /// <summary>
        /// Instantiate the fixture.
        /// </summary>
        public TestMcpHttpServer()
        {
            int port = GetFreePort();
            BaseUrl = "http://127.0.0.1:" + port.ToString(System.Globalization.CultureInfo.InvariantCulture);
            _Server = new McpHttpServer("127.0.0.1", port);
            _Server.RegisterTool(
                "echo",
                "Returns the input text.",
                new
                {
                    type = "object",
                    properties = new
                    {
                        text = new { type = "string" }
                    },
                    required = new[] { "text" },
                    additionalProperties = false
                },
                args =>
                {
                    string text = args?.TryGetProperty("text", out JsonElement textElement) == true
                        ? textElement.GetString() ?? String.Empty
                        : String.Empty;

                    return (object)new
                    {
                        content = new object[]
                        {
                            new { type = "text", text }
                        }
                    };
                });
        }

        /// <summary>
        /// Start the server.
        /// </summary>
        /// <returns>A task that completes once healthy.</returns>
        public async Task StartAsync()
        {
            _RunTask = Task.Run(() => _Server.StartAsync(_Cts.Token));
            await WaitForHealthAsync().ConfigureAwait(false);
        }

        /// <summary>
        /// Stop the server.
        /// </summary>
        public void Stop()
        {
            if (_Disposed) return;
            _Cts.Cancel();
            _Server.Stop();
            try
            {
                _RunTask?.Wait(5000);
            }
            catch (AggregateException)
            {
            }
        }

        /// <inheritdoc />
        public void Dispose()
        {
            if (_Disposed) return;
            Stop();
            _Server.Dispose();
            _Cts.Dispose();
            _Disposed = true;
        }

        private async Task WaitForHealthAsync()
        {
            using HttpClient client = new HttpClient();
            for (int i = 0; i < 50; i++)
            {
                try
                {
                    using HttpResponseMessage response = await client.GetAsync(BaseUrl, _Cts.Token).ConfigureAwait(false);
                    if (response.IsSuccessStatusCode) return;
                }
                catch (HttpRequestException)
                {
                }
                catch (TaskCanceledException)
                {
                }

                await Task.Delay(100, _Cts.Token).ConfigureAwait(false);
            }

            throw new InvalidOperationException("Timed out waiting for MCP test server.");
        }

        private static int GetFreePort()
        {
            using TcpListener listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            int port = ((IPEndPoint)listener.LocalEndpoint).Port;
            listener.Stop();
            return port;
        }
    }
}
