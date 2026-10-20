namespace Wilson.Core.Services
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;
    using Radiant;
    using Wilson.Core.Models;
    using Wilson.Core.Observability;
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
        private readonly TelemetryService? _Telemetry;
        private readonly HashSet<string> _HealthGauges = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private Settings _Settings;
        private CancellationTokenSource? _LoopCancellation;
        private Task? _LoopTask;
        private bool _Disposed;

        /// <summary>
        /// Instantiate the health check service.
        /// </summary>
        /// <param name="settings">Wilson settings.</param>
        /// <param name="telemetry">Optional telemetry service.</param>
        public ModelRunnerHealthCheckService(Settings settings, TelemetryService? telemetry = null)
        {
            ArgumentNullException.ThrowIfNull(settings);
            _Settings = settings;
            _Telemetry = telemetry;
            UpdateSettings(settings);
        }

        private void EnsureHealthGauge(string runnerId)
        {
            if (_Telemetry == null || !_Telemetry.Enabled || String.IsNullOrWhiteSpace(runnerId)) return;
            lock (_HealthGauges)
            {
                if (!_HealthGauges.Add(runnerId)) return;
            }

            string id = runnerId;
            _Telemetry.RegisterGauge(
                WilsonConventions.ModelRunners.Health,
                () => _States.TryGetValue(id, out EndpointHealthState? state) && state.IsHealthy ? 1 : 0,
                new RadiantTag("runner", id));
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
            HashSet<string> configuredRunnerIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            lock (_Sync)
            {
                _Settings = settings;
                foreach (ModelRunnerSettings runner in settings.ModelRunners ?? new List<ModelRunnerSettings>())
                {
                    if (String.IsNullOrWhiteSpace(runner.Id)) continue;
                    string? configuredHealthUrl = runner.HealthCheckUrl;
                    ModelRunnerSettings.ApplyHealthCheckDefaults(runner);

                    configuredRunnerIds.Add(runner.Id);
                    EndpointHealthState state = _States.GetOrAdd(runner.Id, _ => CreateState(runner, now));
                    lock (state.Lock)
                    {
                        state.EndpointName = String.IsNullOrWhiteSpace(runner.Name) ? runner.Id : runner.Name;
                        state.HealthCheckEnabled = runner.HealthCheckEnabled;
                    }

                    if (runner.HealthCheckEnabled)
                    {
                        _NextChecksUtc[runner.Id] = DateTime.MinValue;
                    }
                    else
                    {
                        MarkDisabledRunnerAvailable(state, now);
                        // When checks are off because the provider has no probe route or the URL
                        // cannot be resolved, surface the reason so the operator can fix it instead
                        // of seeing a silently disabled toggle.
                        if (ApiTypes.Describe(runner.ApiType).SupportsHealthChecks || !String.IsNullOrWhiteSpace(configuredHealthUrl))
                        {
                            lock (state.Lock)
                            {
                                state.LastError = ModelRunnerSettings.DescribeHealthCheckUrlProblem(runner);
                            }
                        }

                        _NextChecksUtc.Remove(runner.Id);
                    }
                }

                foreach (string id in _States.Keys)
                {
                    if (configuredRunnerIds.Contains(id)) continue;
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
                    EnsureHealthGauge(runner.Id);
                    if (_States.TryGetValue(runner.Id, out EndpointHealthState? state))
                    {
                        UpdateState(state, result.Success, result.ErrorMessage, runner);
                    }
                }
            }
        }

        private async Task<HealthCheckResult> PerformCheckAsync(ModelRunnerSettings runner, CancellationToken token)
        {
            using TelemetryOperation? op = _Telemetry != null && _Telemetry.Enabled
                ? _Telemetry.Track("model_runner.health_check", SpanKindEnum.Client,
                    WilsonConventions.ModelRunners.HealthChecks, WilsonConventions.ModelRunners.HealthCheckDuration, null,
                    new RadiantTag("runner", runner.Id))
                : null;
            HealthCheckResult checkResult = await PerformCheckInnerAsync(runner, token).ConfigureAwait(false);
            op?.Tag("outcome", checkResult.Success ? "ok" : "error");
            return checkResult;
        }

        private async Task<HealthCheckResult> PerformCheckInnerAsync(ModelRunnerSettings runner, CancellationToken token)
        {
            // Resolve the probe URL and refuse to send anything when Wilson cannot build an absolute
            // HTTP(S) URL. A relative or empty URL would either throw inside HttpClient or, worse,
            // hit an unintended host, so the failure is reported before the request is created.
            string url = ModelRunnerSettings.BuildHealthCheckRequestUrl(runner);
            if (String.IsNullOrWhiteSpace(url))
            {
                return new HealthCheckResult
                {
                    Success = false,
                    ErrorMessage = ModelRunnerSettings.DescribeHealthCheckUrlProblem(runner) ?? "health check URL could not be resolved; no probe was sent"
                };
            }

            HttpMethod method = runner.HealthCheckMethod == HealthCheckMethodEnum.HEAD ? HttpMethod.Head : HttpMethod.Get;

            using HttpRequestMessage request = new HttpRequestMessage(method, url);
            ApplyHealthCheckAuthentication(request, runner);

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

        /// <summary>
        /// Attach the provider's own authentication contract to a health probe. Every provider Wilson
        /// catalogs authenticates differently, so a blanket bearer token makes healthy hosted endpoints
        /// answer 401: Anthropic uses x-api-key plus anthropic-version, Gemini uses x-goog-api-key, and
        /// Azure OpenAI uses api-key with the api-version query parameter. Credentials are only sent
        /// when the runner asks for it and a key is configured.
        /// </summary>
        private static void ApplyHealthCheckAuthentication(HttpRequestMessage request, ModelRunnerSettings runner)
        {
            if (!runner.HealthCheckUseAuth) return;

            string apiKey = (runner.ApiKey ?? String.Empty).Trim();
            ApiTypeDescriptor descriptor = ApiTypes.Describe(runner.ApiType);
            string scheme = String.IsNullOrWhiteSpace(descriptor.DefaultHealthCheckAuthScheme)
                ? HealthCheckAuthSchemes.Bearer
                : descriptor.DefaultHealthCheckAuthScheme;

            if (String.Equals(scheme, HealthCheckAuthSchemes.None, StringComparison.Ordinal)) return;
            if (String.IsNullOrWhiteSpace(apiKey)) return;

            switch (scheme)
            {
                case HealthCheckAuthSchemes.AnthropicApiKey:
                    request.Headers.Remove("x-api-key");
                    request.Headers.TryAddWithoutValidation("x-api-key", apiKey);
                    string version = String.IsNullOrWhiteSpace(descriptor.DefaultHealthCheckVersion) ? "2023-06-01" : descriptor.DefaultHealthCheckVersion;
                    request.Headers.Remove("anthropic-version");
                    request.Headers.TryAddWithoutValidation("anthropic-version", version);
                    break;

                case HealthCheckAuthSchemes.GoogleApiKey:
                    request.Headers.Remove("x-goog-api-key");
                    request.Headers.TryAddWithoutValidation("x-goog-api-key", apiKey);
                    break;

                case HealthCheckAuthSchemes.AzureApiKey:
                    request.Headers.Remove("api-key");
                    request.Headers.TryAddWithoutValidation("api-key", apiKey);
                    break;

                default:
                    request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);
                    break;
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
                state.HealthCheckEnabled = true;
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
                        _Telemetry?.Increment(WilsonConventions.ModelRunners.StateTransitions, 1, new RadiantTag("runner", runner.Id), new RadiantTag("to_state", "healthy"));
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
                        _Telemetry?.Increment(WilsonConventions.ModelRunners.StateTransitions, 1, new RadiantTag("runner", runner.Id), new RadiantTag("to_state", "unhealthy"));
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
                HealthCheckEnabled = runner.HealthCheckEnabled,
                FirstCheckUtc = now,
                LastStateChangeUtc = now
            };
        }

        private static void MarkDisabledRunnerAvailable(EndpointHealthState state, DateTime now)
        {
            lock (state.Lock)
            {
                state.HealthCheckEnabled = false;
                state.LastError = null;
                state.ConsecutiveSuccesses = 0;
                state.ConsecutiveFailures = 0;
                if (!state.IsHealthy)
                {
                    if (state.LastStateChangeUtc.HasValue)
                    {
                        long downtimeMs = (long)(now - state.LastStateChangeUtc.Value).TotalMilliseconds;
                        if (downtimeMs > 0) state.TotalDowntimeMs += downtimeMs;
                    }

                    state.IsHealthy = true;
                    state.LastStateChangeUtc = now;
                }

                if (!state.LastHealthyUtc.HasValue) state.LastHealthyUtc = now;
            }
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
                ApiSecretKey = runner.ApiSecretKey,
                AwsSessionToken = runner.AwsSessionToken,
                Deployment = runner.Deployment,
                ApiVersion = runner.ApiVersion,
                Region = runner.Region,
                ProjectId = runner.ProjectId,
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
            // Include the resolved authentication scheme, not just the API type, so two runners that
            // share a URL but authenticate differently are never collapsed into one probe.
            // Use the final request URL so provider-required queries (Azure API versions) also
            // distinguish probes whose responses can differ.
            string auth = runner.HealthCheckUseAuth
                ? "auth:" + ApiTypes.Describe(runner.ApiType).DefaultHealthCheckAuthScheme
                : "anon";
            return method + "|" + runner.HealthCheckExpectedStatusCode + "|" + auth + "|" + NormalizeUrl(ModelRunnerSettings.BuildHealthCheckRequestUrl(runner));
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
