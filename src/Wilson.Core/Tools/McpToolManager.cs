namespace Wilson.Core.Tools
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text.Json;
    using System.Text.RegularExpressions;
    using System.Threading;
    using System.Threading.Tasks;
    using Voltaic;
    using Wilson.Core.Models;
    using Wilson.Core.Settings;
    using WilsonToolDefinition = Wilson.Core.Models.ToolDefinition;

    /// <summary>
    /// Manages MCP server connections, tool discovery, and tool execution.
    /// </summary>
    public sealed class McpToolManager : IDisposable
    {
        private readonly object _Lock = new object();
        private readonly Dictionary<string, IMcpClientConnection> _Clients = new Dictionary<string, IMcpClientConnection>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, McpToolMapping> _ToolMappings = new Dictionary<string, McpToolMapping>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, List<WilsonToolDefinition>> _ServerTools = new Dictionary<string, List<WilsonToolDefinition>>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, McpServerStatus> _Statuses = new Dictionary<string, McpServerStatus>(StringComparer.OrdinalIgnoreCase);
        private bool _Disposed = false;

        /// <summary>
        /// Initialize enabled MCP servers from settings.
        /// </summary>
        /// <param name="settings">Wilson settings.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>A task representing the initialization operation.</returns>
        public async Task InitializeAsync(Settings settings, CancellationToken token)
        {
            ArgumentNullException.ThrowIfNull(settings);
            McpToolSettings mcp = settings.Tools?.Mcp ?? new McpToolSettings();
            List<McpServerSettings> servers = mcp.Servers ?? new List<McpServerSettings>();

            ClearConnections();
            lock (_Lock)
            {
                foreach (McpServerSettings server in servers)
                {
                    if (server == null) continue;
                    _Statuses[server.Name] = new McpServerStatus
                    {
                        Name = server.Name,
                        Transport = server.Transport,
                        Enabled = server.Enabled,
                        Connected = false,
                        LastAttemptUtc = DateTime.UtcNow
                    };
                }
            }

            if (!mcp.Enabled) return;

            foreach (McpServerSettings server in servers.Where(item => item != null && item.Enabled))
            {
                token.ThrowIfCancellationRequested();
                await ConnectAndDiscoverAsync(server, token).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Return all discovered MCP tool definitions.
        /// </summary>
        /// <returns>Discovered definitions.</returns>
        public List<WilsonToolDefinition> GetToolDefinitions()
        {
            lock (_Lock)
            {
                return _ServerTools.Values.SelectMany(item => item).Select(CloneDefinition).ToList();
            }
        }

        /// <summary>
        /// Check whether a discovered MCP tool exists.
        /// </summary>
        /// <param name="toolName">Model-facing tool name.</param>
        /// <returns>True if discovered.</returns>
        public bool HasTool(string toolName)
        {
            lock (_Lock)
            {
                return _ToolMappings.ContainsKey(toolName);
            }
        }

        /// <summary>
        /// Execute a discovered MCP tool.
        /// </summary>
        /// <param name="toolCallId">Tool-call identifier.</param>
        /// <param name="toolName">Model-facing tool name.</param>
        /// <param name="arguments">Tool arguments.</param>
        /// <param name="context">Tool execution context.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Tool result.</returns>
        public async Task<ToolResult> ExecuteAsync(string toolCallId, string toolName, JsonElement arguments, ToolExecutionContext context, CancellationToken token)
        {
            McpToolMapping mapping;
            IMcpClientConnection client;
            lock (_Lock)
            {
                if (!_ToolMappings.TryGetValue(toolName, out mapping!))
                    return ToolResultFactory.Error(toolCallId, "unknown_mcp_tool", "MCP tool '" + toolName + "' is not registered.");
                if (!_Clients.TryGetValue(mapping.ServerName, out client!))
                    return ToolResultFactory.Error(toolCallId, "mcp_server_disconnected", "MCP server '" + mapping.ServerName + "' is not connected.");
            }

            try
            {
                int timeoutMs = Math.Clamp(context.SafetyLimits.ToolTimeoutMs, 1000, 300000);
                object callParams = new { name = mapping.OriginalToolName, arguments };
                JsonElement result = await client.CallAsync<JsonElement>("tools/call", callParams, timeoutMs, token).ConfigureAwait(false);
                return ToolResultFactory.SuccessJson(toolCallId, result, context);
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                return ToolResultFactory.Error(toolCallId, "cancelled", "Tool execution was cancelled.");
            }
            catch (OperationCanceledException)
            {
                return ToolResultFactory.Error(toolCallId, "mcp_call_timed_out", "MCP tool execution timed out.");
            }
            catch (Exception ex)
            {
                return ToolResultFactory.Error(toolCallId, "mcp_call_failed", ex.Message);
            }
        }

        /// <summary>
        /// Return redacted server status.
        /// </summary>
        /// <param name="settings">Wilson settings.</param>
        /// <returns>Status response.</returns>
        public McpStatusResponse GetStatus(Settings settings)
        {
            McpToolSettings mcp = settings.Tools?.Mcp ?? new McpToolSettings();
            List<McpServerStatus> servers;
            lock (_Lock)
            {
                servers = _Statuses.Values.Select(CloneStatus).OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase).ToList();
            }

            return new McpStatusResponse
            {
                Enabled = mcp.Enabled,
                ConfiguredServerCount = mcp.Servers?.Count ?? 0,
                ConnectedServerCount = servers.Count(item => item.Connected),
                ToolCount = servers.Sum(item => item.ToolCount),
                Servers = servers
            };
        }

        /// <inheritdoc />
        public void Dispose()
        {
            if (_Disposed) return;
            ClearConnections();
            _Disposed = true;
        }

        private async Task ConnectAndDiscoverAsync(McpServerSettings server, CancellationToken token)
        {
            McpServerStatus status = new McpServerStatus
            {
                Name = server.Name,
                Transport = server.Transport,
                Enabled = server.Enabled,
                LastAttemptUtc = DateTime.UtcNow
            };

            try
            {
                IMcpClientConnection client = await ConnectClientAsync(server, token).ConfigureAwait(false);
                JsonElement toolsResult = await client.CallAsync<JsonElement>("tools/list", null, 30000, token).ConfigureAwait(false);
                List<McpDiscoveredTool> discoveredTools = ParseToolDefinitions(server, toolsResult);
                List<WilsonToolDefinition> definitions = discoveredTools.Select(item => item.Definition).ToList();

                lock (_Lock)
                {
                    _Clients[server.Name] = client;
                    _ServerTools[server.Name] = definitions;
                    foreach (McpDiscoveredTool discoveredTool in discoveredTools)
                    {
                        _ToolMappings[discoveredTool.Definition.Name] = new McpToolMapping(server.Name, discoveredTool.OriginalToolName);
                    }

                    status.Connected = client.IsConnected;
                    status.ToolCount = definitions.Count;
                    status.Tools = definitions.Select(item => item.Name).ToList();
                    _Statuses[server.Name] = status;
                }
            }
            catch (Exception ex)
            {
                lock (_Lock)
                {
                    status.Connected = false;
                    status.Error = ex.Message;
                    _Statuses[server.Name] = status;
                    _ServerTools[server.Name] = new List<WilsonToolDefinition>();
                }
            }
        }

        private static List<McpDiscoveredTool> ParseToolDefinitions(McpServerSettings server, JsonElement toolsResult)
        {
            List<McpDiscoveredTool> definitions = new List<McpDiscoveredTool>();
            if (!toolsResult.TryGetProperty("tools", out JsonElement toolsArray) || toolsArray.ValueKind != JsonValueKind.Array)
                return definitions;

            foreach (JsonElement toolElement in toolsArray.EnumerateArray())
            {
                if (toolElement.ValueKind != JsonValueKind.Object || !toolElement.TryGetProperty("name", out JsonElement nameElement)) continue;
                string originalName = nameElement.GetString() ?? String.Empty;
                if (String.IsNullOrWhiteSpace(originalName)) continue;

                string description = toolElement.TryGetProperty("description", out JsonElement descriptionElement)
                    ? descriptionElement.GetString() ?? String.Empty
                    : String.Empty;
                object? schema = toolElement.TryGetProperty("inputSchema", out JsonElement schemaElement)
                    ? JsonSerializer.Deserialize<object>(schemaElement.GetRawText())
                    : new { type = "object", properties = new { }, additionalProperties = true };

                definitions.Add(new McpDiscoveredTool
                {
                    OriginalToolName = originalName,
                    Definition = new WilsonToolDefinition
                    {
                        Name = PrefixedToolName(server.Name, originalName),
                        Description = "[MCP:" + server.Name + "] " + description,
                        ParametersSchema = schema,
                        Category = ToolCategories.Mcp,
                        BuiltIn = false,
                        RequiresApproval = false,
                        Dangerous = false,
                        Enabled = true
                    }
                });
            }

            return definitions;
        }

        private static string PrefixedToolName(string serverName, string toolName)
        {
            return SafeToolSegment(serverName) + "__" + SafeToolSegment(toolName);
        }

        private static string SafeToolSegment(string value)
        {
            string normalized = Regex.Replace(value ?? String.Empty, "[^A-Za-z0-9_-]+", "_", RegexOptions.None, TimeSpan.FromSeconds(1)).Trim('_');
            return String.IsNullOrWhiteSpace(normalized) ? "mcp" : normalized;
        }

        private async Task<IMcpClientConnection> ConnectClientAsync(McpServerSettings server, CancellationToken token)
        {
            return String.Equals(server.Transport, "http", StringComparison.OrdinalIgnoreCase)
                ? await ConnectHttpClientAsync(server, token).ConfigureAwait(false)
                : await ConnectStdioClientAsync(server, token).ConfigureAwait(false);
        }

        private static async Task<IMcpClientConnection> ConnectStdioClientAsync(McpServerSettings server, CancellationToken token)
        {
            if (String.IsNullOrWhiteSpace(server.Command))
                throw new InvalidOperationException("MCP stdio server command is required.");

            foreach (KeyValuePair<string, string> kvp in server.Env ?? new Dictionary<string, string>())
            {
                Environment.SetEnvironmentVariable(kvp.Key, ResolveConfiguredValue(kvp.Value));
            }

            McpClient client = new McpClient();
            bool launched = await client.LaunchServerAsync(server.Command, (server.Args ?? new List<string>()).ToArray(), token).ConfigureAwait(false);
            if (!launched)
            {
                client.Dispose();
                throw new InvalidOperationException("Failed to launch MCP server '" + server.Name + "'.");
            }

            return new StdioMcpClientConnection(client);
        }

        private static async Task<IMcpClientConnection> ConnectHttpClientAsync(McpServerSettings server, CancellationToken token)
        {
            if (!Uri.TryCreate(server.Url, UriKind.Absolute, out Uri? uri) ||
                (!String.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) && !String.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)))
                throw new InvalidOperationException("HTTP MCP servers require an absolute http:// or https:// URL.");

            McpHttpClient client = new McpHttpClient();
            bool connected = await client.ConnectStreamableAsync(server.Url, NormalizeMcpPath(server.McpPath), token).ConfigureAwait(false);
            if (!connected)
            {
                client.Dispose();
                throw new InvalidOperationException("Failed to connect to HTTP MCP server '" + server.Name + "'.");
            }

            return new HttpMcpClientConnection(client);
        }

        private static string NormalizeMcpPath(string? path)
        {
            if (String.IsNullOrWhiteSpace(path)) return "/mcp";
            string normalized = path.Trim();
            return normalized.StartsWith("/", StringComparison.Ordinal) ? normalized : "/" + normalized;
        }

        private static string ResolveConfiguredValue(string value)
        {
            if (String.IsNullOrWhiteSpace(value)) return String.Empty;
            string trimmed = value.Trim();
            if (trimmed.StartsWith("env:", StringComparison.OrdinalIgnoreCase))
            {
                string name = trimmed.Substring("env:".Length).Trim();
                return String.IsNullOrWhiteSpace(name) ? String.Empty : Environment.GetEnvironmentVariable(name) ?? String.Empty;
            }

            return Environment.ExpandEnvironmentVariables(trimmed);
        }

        private void ClearConnections()
        {
            lock (_Lock)
            {
                foreach (IMcpClientConnection client in _Clients.Values)
                {
                    try
                    {
                        client.Shutdown();
                        client.Dispose();
                    }
                    catch (Exception)
                    {
                    }
                }

                _Clients.Clear();
                _ToolMappings.Clear();
                _ServerTools.Clear();
                _Statuses.Clear();
            }
        }

        private static WilsonToolDefinition CloneDefinition(WilsonToolDefinition definition)
        {
            return new WilsonToolDefinition
            {
                Name = definition.Name,
                Description = definition.Description,
                ParametersSchema = definition.ParametersSchema,
                Category = definition.Category,
                BuiltIn = definition.BuiltIn,
                RequiresApproval = definition.RequiresApproval,
                Dangerous = definition.Dangerous,
                Enabled = definition.Enabled
            };
        }

        private static McpServerStatus CloneStatus(McpServerStatus status)
        {
            return new McpServerStatus
            {
                Name = status.Name,
                Transport = status.Transport,
                Enabled = status.Enabled,
                Connected = status.Connected,
                ToolCount = status.ToolCount,
                Tools = new List<string>(status.Tools ?? new List<string>()),
                Error = status.Error,
                LastAttemptUtc = status.LastAttemptUtc
            };
        }

        private readonly struct McpToolMapping
        {
            public McpToolMapping(string serverName, string originalToolName)
            {
                ServerName = serverName;
                OriginalToolName = originalToolName;
            }

            public string ServerName { get; }
            public string OriginalToolName { get; }
        }

        private sealed class McpDiscoveredTool
        {
            public string OriginalToolName { get; set; } = String.Empty;
            public WilsonToolDefinition Definition { get; set; } = new WilsonToolDefinition();
        }

        private interface IMcpClientConnection : IDisposable
        {
            bool IsConnected { get; }
            Task<T> CallAsync<T>(string method, object? parameters, int timeoutMs, CancellationToken cancellationToken);
            void Shutdown();
        }

        private sealed class StdioMcpClientConnection : IMcpClientConnection
        {
            private readonly McpClient _Client;

            public StdioMcpClientConnection(McpClient client)
            {
                _Client = client;
            }

            public bool IsConnected => _Client.IsConnected;
            public Task<T> CallAsync<T>(string method, object? parameters, int timeoutMs, CancellationToken cancellationToken) => _Client.CallAsync<T>(method, parameters, timeoutMs, cancellationToken);
            public void Shutdown() => _Client.Shutdown();
            public void Dispose() => _Client.Dispose();
        }

        private sealed class HttpMcpClientConnection : IMcpClientConnection
        {
            private readonly McpHttpClient _Client;

            public HttpMcpClientConnection(McpHttpClient client)
            {
                _Client = client;
            }

            public bool IsConnected => _Client.IsConnected;
            public Task<T> CallAsync<T>(string method, object? parameters, int timeoutMs, CancellationToken cancellationToken) => _Client.CallAsync<T>(method, parameters, timeoutMs, cancellationToken);
            public void Shutdown() => _Client.Disconnect();
            public void Dispose() => _Client.Dispose();
        }
    }
}
