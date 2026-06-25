namespace Wilson.Core.Services
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;
    using Wilson.Core.Models;
    using Wilson.Core.Settings;

    /// <summary>
    /// Background health checker for configured model servers.
    /// </summary>
    public sealed class ModelRunnerHealthCheckService : IDisposable
    {
        private static readonly TimeSpan _HistoryRetention = TimeSpan.FromHours(24);
        private readonly object _Sync = new object();
        private readonly ConcurrentDictionary<string, EndpointHealthState> _States = new ConcurrentDictionary<string, EndpointHealthState>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, DateTime> _NextChecksUtc = new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);
        private readonly HttpClient _HttpClient = new HttpClient();
        private Settings _Settings;
        private CancellationTokenSource? _LoopCancellation;
        private Task? _LoopTask;
        private bool _Disposed;

        /// <summary>
        /// Instantiate the health check service.
        /// </summary>
        /// <param name="settings">Wilson settings.</param>
        public ModelRunnerHealthCheckService(Settings settings)
        {
            ArgumentNullException.ThrowIfNull(settings);
            _Settings = settings;
            UpdateSettings(settings);
        }

        /// <summary>
        /// Start the background health check loop.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        public void Start(CancellationToken token = default)
        {
            ThrowIfDisposed();
            if (_LoopTask != null) return;
            _LoopCancellation = CancellationTokenSource.CreateLinkedTokenSource(token);
            _LoopTask = Task.Run(() => RunAsync(_LoopCancellation.Token), _LoopCancellation.Token);
        }

        /// <summary>
        /// Stop the background health check loop.
        /// </summary>
        public async Task StopAsync()
        {
            if (_LoopCancellation == null || _LoopTask == null) return;
            _LoopCancellation.Cancel();
            try
            {
                await _LoopTask.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
            }
            finally
            {
                _LoopCancellation.Dispose();
                _LoopCancellation = null;
                _LoopTask = null;
            }
        }

        /// <summary>
        /// Replace the model runner configuration monitored by this service.
        /// </summary>
        /// <param name="settings">Wilson settings.</param>
        public void UpdateSettings(Settings settings)
        {
            ThrowIfDisposed();
            ArgumentNullException.ThrowIfNull(settings);

            DateTime now = DateTime.UtcNow;
            HashSet<string> enabledRunnerIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            lock (_Sync)
            {
                _Settings = settings;
                foreach (ModelRunnerSettings runner in settings.ModelRunners ?? new List<ModelRunnerSettings>())
                {
                    if (String.IsNullOrWhiteSpace(runner.Id)) continue;
                    ModelRunnerSettings.ApplyHealthCheckDefaults(runner);
                    if (!runner.HealthCheckEnabled) continue;

                    enabledRunnerIds.Add(runner.Id);
                    EndpointHealthState state = _States.GetOrAdd(runner.Id, _ => CreateState(runner, now));
                    lock (state.Lock)
                    {
                        state.EndpointName = String.IsNullOrWhiteSpace(runner.Name) ? runner.Id : runner.Name;
                    }

                    _NextChecksUtc[runner.Id] = DateTime.MinValue;
                }

                foreach (string id in _States.Keys)
                {
                    if (enabledRunnerIds.Contains(id)) continue;
                    _States.TryRemove(id, out _);
                    _NextChecksUtc.Remove(id);
                }
            }
        }

        /// <summary>
        /// Get a single model server health status.
        /// </summary>
        /// <param name="runnerId">Model runner identifier.</param>
        /// <returns>Health status or null if not monitored.</returns>
        public EndpointHealthStatus? GetHealthStatus(string runnerId)
        {
            if (String.IsNullOrWhiteSpace(runnerId)) return null;
            if (!_States.TryGetValue(runnerId, out EndpointHealthState? state)) return null;
            return EndpointHealthStatus.FromState(state);
        }

        /// <summary>
        /// Get all model server health statuses.
        /// </summary>
        /// <returns>Health status list.</returns>
        public List<EndpointHealthStatus> GetAllHealthStatuses()
        {
            return _States.Values
                .Select(EndpointHealthStatus.FromState)
                .OrderBy(item => item.EndpointName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(item => item.EndpointId, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        /// <summary>
        /// Dispose the health check service.
        /// </summary>
        public void Dispose()
        {
            if (_Disposed) return;
            _Disposed = true;
            _LoopCancellation?.Cancel();
            _LoopCancellation?.Dispose();
            _HttpClient.Dispose();
        }

        private async Task RunAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    List<ModelRunnerSettings> dueRunners = GetDueRunners(DateTime.UtcNow);
                    if (dueRunners.Count > 0) await CheckDueRunnersAsync(dueRunners, token).ConfigureAwait(false);
                    await Task.Delay(250, token).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (token.IsCancellationRequested)
                {
                    break;
                }
                catch
                {
                    await Task.Delay(1000, token).ConfigureAwait(false);
                }
            }
        }

        private List<ModelRunnerSettings> GetDueRunners(DateTime now)
        {
            List<ModelRunnerSettings> runners = SnapshotRunners();
            List<ModelRunnerSettings> due = new List<ModelRunnerSettings>();
            HashSet<string> runnerIds = new HashSet<string>(runners.Select(item => item.Id), StringComparer.OrdinalIgnoreCase);

            lock (_Sync)
            {
                foreach (string id in _NextChecksUtc.Keys.ToList())
                {
                    if (!runnerIds.Contains(id)) _NextChecksUtc.Remove(id);
                }

                foreach (ModelRunnerSettings runner in runners)
                {
                    if (!_NextChecksUtc.TryGetValue(runner.Id, out DateTime nextCheckUtc) || now >= nextCheckUtc)
                    {
                        due.Add(runner);
                        _NextChecksUtc[runner.Id] = now.AddMilliseconds(Math.Max(1, runner.HealthCheckIntervalMs));
                    }
                }
            }

            return due;
        }

        private async Task CheckDueRunnersAsync(List<ModelRunnerSettings> runners, CancellationToken token)
        {
            foreach (IGrouping<string, ModelRunnerSettings> group in runners.GroupBy(BuildMonitorKey, StringComparer.OrdinalIgnoreCase))
            {
                token.ThrowIfCancellationRequested();
                List<ModelRunnerSettings> subscriptions = group.ToList();
                ModelRunnerSettings probeRunner = subscriptions
                    .OrderByDescending(item => item.HealthCheckTimeoutMs)
                    .ThenBy(item => item.Id, StringComparer.OrdinalIgnoreCase)
                    .First();

                HealthCheckResult result = await PerformCheckAsync(probeRunner, token).ConfigureAwait(false);
                foreach (ModelRunnerSettings runner in subscriptions)
                {
                    if (_States.TryGetValue(runner.Id, out EndpointHealthState? state))
                    {
                        UpdateState(state, result.Success, result.ErrorMessage, runner);
                    }
                }
            }
        }

        private async Task<HealthCheckResult> PerformCheckAsync(ModelRunnerSettings runner, CancellationToken token)
        {
            string url = ResolveHealthCheckUrl(runner);
            HttpMethod method = runner.HealthCheckMethod == HealthCheckMethodEnum.HEAD ? HttpMethod.Head : HttpMethod.Get;

            using HttpRequestMessage request = new HttpRequestMessage(method, url);
            if (runner.HealthCheckUseAuth && !String.IsNullOrWhiteSpace(runner.ApiKey))
            {
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", runner.ApiKey);
            }

            using CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
            timeout.CancelAfter(Math.Max(1, runner.HealthCheckTimeoutMs));

            try
            {
                using HttpResponseMessage response = await _HttpClient.SendAsync(request, timeout.Token).ConfigureAwait(false);
                int statusCode = (int)response.StatusCode;
                bool success = statusCode == runner.HealthCheckExpectedStatusCode;
                return new HealthCheckResult
                {
                    Success = success,
                    ErrorMessage = success ? null : "health check returned HTTP " + statusCode + "; expected " + runner.HealthCheckExpectedStatusCode
                };
            }
            catch (OperationCanceledException) when (!token.IsCancellationRequested)
            {
                return new HealthCheckResult { Success = false, ErrorMessage = "health check timed out after " + runner.HealthCheckTimeoutMs + "ms" };
            }
            catch (Exception ex)
            {
                return new HealthCheckResult { Success = false, ErrorMessage = ex.Message };
            }
        }

        private void UpdateState(EndpointHealthState state, bool success, string? errorMessage, ModelRunnerSettings runner)
        {
            DateTime now = DateTime.UtcNow;

            lock (state.HistoryLock)
            {
                state.CheckHistory.Add(new HealthCheckRecord { TimestampUtc = now, Success = success });
                DateTime cutoff = now - _HistoryRetention;
                state.CheckHistory.RemoveAll(item => item.TimestampUtc < cutoff);
            }

            lock (state.Lock)
            {
                state.EndpointName = String.IsNullOrWhiteSpace(runner.Name) ? runner.Id : runner.Name;
                state.LastCheckUtc = now;

                if (success)
                {
                    state.ConsecutiveSuccesses++;
                    state.ConsecutiveFailures = 0;
                    state.LastError = null;

                    if (!state.IsHealthy && state.ConsecutiveSuccesses >= runner.HealthyThreshold)
                    {
                        if (state.LastStateChangeUtc.HasValue)
                        {
                            long downtimeMs = (long)(now - state.LastStateChangeUtc.Value).TotalMilliseconds;
                            if (downtimeMs > 0) state.TotalDowntimeMs += downtimeMs;
                        }

                        state.IsHealthy = true;
                        state.LastHealthyUtc = now;
                        state.LastStateChangeUtc = now;
                    }
                }
                else
                {
                    state.ConsecutiveFailures++;
                    state.ConsecutiveSuccesses = 0;
                    state.LastError = errorMessage;
                    if (!state.IsHealthy && !state.LastUnhealthyUtc.HasValue)
                    {
                        state.LastUnhealthyUtc = now;
                    }

                    if (state.IsHealthy && state.ConsecutiveFailures >= runner.UnhealthyThreshold)
                    {
                        if (state.LastStateChangeUtc.HasValue)
                        {
                            long uptimeMs = (long)(now - state.LastStateChangeUtc.Value).TotalMilliseconds;
                            if (uptimeMs > 0) state.TotalUptimeMs += uptimeMs;
                        }

                        state.IsHealthy = false;
                        state.LastUnhealthyUtc = now;
                        state.LastStateChangeUtc = now;
                    }
                }
            }
        }

        private List<ModelRunnerSettings> SnapshotRunners()
        {
            lock (_Sync)
            {
                return (_Settings.ModelRunners ?? new List<ModelRunnerSettings>())
                    .Where(item => item.HealthCheckEnabled && !String.IsNullOrWhiteSpace(item.Id))
                    .Select(CopyRunner)
                    .ToList();
            }
        }

        private static EndpointHealthState CreateState(ModelRunnerSettings runner, DateTime now)
        {
            return new EndpointHealthState
            {
                EndpointId = runner.Id,
                EndpointName = String.IsNullOrWhiteSpace(runner.Name) ? runner.Id : runner.Name,
                FirstCheckUtc = now,
                LastStateChangeUtc = now
            };
        }

        private static ModelRunnerSettings CopyRunner(ModelRunnerSettings runner)
        {
            ModelRunnerSettings copy = new ModelRunnerSettings
            {
                Id = runner.Id,
                Name = runner.Name,
                ApiType = runner.ApiType,
                Endpoint = runner.Endpoint,
                ApiKey = runner.ApiKey,
                Models = new List<string>(runner.Models ?? new List<string>()),
                ContextWindowTokens = runner.ContextWindowTokens,
                ToolsEnabled = runner.ToolsEnabled,
                SupportsTools = runner.SupportsTools,
                ToolCallingApiFormat = runner.ToolCallingApiFormat,
                SupportsParallelToolCalls = runner.SupportsParallelToolCalls,
                SupportsStreamingToolCalls = runner.SupportsStreamingToolCalls,
                ChatCompletionsPath = runner.ChatCompletionsPath,
                HealthCheckEnabled = runner.HealthCheckEnabled,
                HealthCheckUrl = runner.HealthCheckUrl,
                HealthCheckMethod = runner.HealthCheckMethod,
                HealthCheckIntervalMs = runner.HealthCheckIntervalMs,
                HealthCheckTimeoutMs = runner.HealthCheckTimeoutMs,
                HealthCheckExpectedStatusCode = runner.HealthCheckExpectedStatusCode,
                HealthyThreshold = runner.HealthyThreshold,
                UnhealthyThreshold = runner.UnhealthyThreshold,
                HealthCheckUseAuth = runner.HealthCheckUseAuth
            };

            ModelRunnerSettings.ApplyHealthCheckDefaults(copy);
            return copy;
        }

        private static string BuildMonitorKey(ModelRunnerSettings runner)
        {
            string method = runner.HealthCheckMethod == HealthCheckMethodEnum.HEAD ? "HEAD" : "GET";
            string auth = runner.HealthCheckUseAuth ? "auth:" + (runner.ApiType ?? String.Empty).ToLowerInvariant() : "anon";
            return method + "|" + runner.HealthCheckExpectedStatusCode + "|" + auth + "|" + NormalizeUrl(ResolveHealthCheckUrl(runner));
        }

        private static string ResolveHealthCheckUrl(ModelRunnerSettings runner)
        {
            string configured = (runner.HealthCheckUrl ?? String.Empty).Trim();
            if (Uri.TryCreate(configured, UriKind.Absolute, out _)) return configured;
            if (String.IsNullOrWhiteSpace(configured))
            {
                ModelRunnerSettings.ApplyHealthCheckDefaults(runner);
                configured = runner.HealthCheckUrl ?? String.Empty;
            }

            if (configured.StartsWith("/", StringComparison.Ordinal)) return (runner.Endpoint ?? String.Empty).TrimEnd('/') + configured;
            return (runner.Endpoint ?? String.Empty).TrimEnd('/') + "/" + configured.TrimStart('/');
        }

        private static string NormalizeUrl(string url)
        {
            string trimmed = (url ?? String.Empty).Trim().TrimEnd('/');
            if (Uri.TryCreate(trimmed, UriKind.Absolute, out Uri? uri))
            {
                UriBuilder builder = new UriBuilder(uri);
                builder.Host = builder.Host.ToLowerInvariant();
                return builder.Uri.ToString().TrimEnd('/');
            }

            return trimmed.ToLowerInvariant();
        }

        private void ThrowIfDisposed()
        {
            if (_Disposed) throw new ObjectDisposedException(nameof(ModelRunnerHealthCheckService));
        }

        private sealed class HealthCheckResult
        {
            public bool Success { get; set; }
            public string? ErrorMessage { get; set; }
        }
    }
}
