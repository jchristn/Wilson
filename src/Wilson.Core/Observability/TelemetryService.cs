namespace Wilson.Core.Observability
{
    using System;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Logging.Abstractions;
    using Radiant;
    using Wilson.Core.Settings;

    /// <summary>
    /// Owns the Radiant/OpenTelemetry host and exposes best-effort emit helpers. Every method is safe to call when
    /// telemetry is disabled or failed to initialize; in that case emits are no-ops and loggers are null loggers.
    /// </summary>
    public sealed class TelemetryService : IDisposable
    {
        private readonly RadiantHost? _Host;
        private readonly ILoggerFactory _LoggerFactory;

        /// <summary>
        /// Instantiate from telemetry settings. Never throws; on failure telemetry is inert.
        /// </summary>
        /// <param name="settings">Telemetry settings.</param>
        public TelemetryService(TelemetrySettings? settings)
        {
            _LoggerFactory = NullLoggerFactory.Instance;
            if (settings == null || !settings.Enabled) return;

            try
            {
                RadiantSettings radiant = new RadiantSettings(String.IsNullOrWhiteSpace(settings.ServiceName) ? "wilson-server" : settings.ServiceName)
                {
                    Enable = true
                };
                radiant.Otlp.Enable = true;
                radiant.Otlp.Endpoint = settings.OtlpEndpoint;
                radiant.Otlp.Protocol = String.Equals(settings.OtlpProtocol, "httpprotobuf", StringComparison.OrdinalIgnoreCase)
                    ? OtlpProtocolEnum.HttpProtobuf
                    : OtlpProtocolEnum.Grpc;

                radiant.Metrics.Enable = settings.MetricsEnabled;
                radiant.Metrics.IncludeProcess = settings.IncludeProcessMetrics;
                radiant.Metrics.IncludeRuntime = settings.IncludeRuntimeMetrics;
                radiant.Metrics.LabelPolicy = settings.MetricCatalogStrict ? LabelPolicyEnum.Strict : LabelPolicyEnum.Lenient;
                radiant.Metrics.DefineAll(WilsonConventions.All);

                radiant.Traces.Enable = settings.TracesEnabled;
                radiant.Traces.SamplingRatio = Math.Clamp(settings.TracesSamplingRatio, 0.0, 1.0);

                radiant.Logs.Enable = settings.LogsEnabled;
                radiant.Logs.MinimumSeverity = SeverityFromName(settings.LogMinimumSeverity);

                radiant.Prometheus.Enable = settings.PrometheusScrapeEnabled;
                if (settings.PrometheusScrapePort > 0 && settings.PrometheusScrapePort <= 65535)
                    radiant.Prometheus.Port = settings.PrometheusScrapePort;

                // Emit under "Wilson" and also subscribe to Watson 7.1's own "Watson" meter/activity source so
                // connection/exception/stream telemetry and the per-request server span are captured, with Wilson
                // spans nesting under Watson's request span via Activity.Current.
                radiant.Sources.AddMeter(WilsonConventions.SourceName);
                radiant.Sources.AddActivitySource(WilsonConventions.SourceName);
                radiant.Sources.AddMeter("Watson");
                radiant.Sources.AddActivitySource("Watson");

                _Host = RadiantHost.Start(radiant);
                if (_Host?.LoggerFactory != null) _LoggerFactory = _Host.LoggerFactory;
            }
            catch (Exception)
            {
                _Host = null;
                _LoggerFactory = NullLoggerFactory.Instance;
            }
        }

        /// <summary>Whether telemetry is active.</summary>
        public bool Enabled => _Host != null;

        /// <summary>Create a category logger. Returns a null logger when telemetry is disabled.</summary>
        /// <param name="category">Logger category.</param>
        /// <returns>Logger.</returns>
        public ILogger CreateLogger(string category) => _LoggerFactory.CreateLogger(category);

        /// <summary>Start a span. Returns null when telemetry is disabled or unsampled.</summary>
        /// <param name="name">Span name.</param>
        /// <param name="kind">Span kind.</param>
        /// <returns>Span or null.</returns>
        public RadiantSpan? StartSpan(string name, SpanKindEnum kind = SpanKindEnum.Internal)
        {
            if (_Host == null) return null;
            try { return _Host.StartSpan(name, kind); }
            catch (Exception) { return null; }
        }

        /// <summary>Increment a counter (best effort).</summary>
        public void Increment(Convention convention, double value, params RadiantTag[] tags)
        {
            if (_Host == null) return;
            try { _Host.Client.Increment(convention, value, tags); } catch (Exception) { }
        }

        /// <summary>Record a histogram sample (best effort).</summary>
        public void Record(Convention convention, double value, params RadiantTag[] tags)
        {
            if (_Host == null) return;
            try { _Host.Client.Record(convention, value, tags); } catch (Exception) { }
        }

        /// <summary>Add to an up/down counter (best effort).</summary>
        public void Add(Convention convention, double value, params RadiantTag[] tags)
        {
            if (_Host == null) return;
            try { _Host.Client.Add(convention, value, tags); } catch (Exception) { }
        }

        /// <summary>Register an observable gauge (best effort).</summary>
        public void RegisterGauge(Convention convention, Func<double> observe, params RadiantTag[] tags)
        {
            if (_Host == null) return;
            try { _Host.Client.RegisterGauge(convention, observe, tags); } catch (Exception) { }
        }

        /// <summary>
        /// Begin a tracked unit of work: opens a span and increments the in-flight counter. Dispose the returned
        /// operation to record duration and count and decrement in-flight.
        /// </summary>
        /// <param name="spanName">Span name.</param>
        /// <param name="kind">Span kind.</param>
        /// <param name="count">Optional counter recorded on completion.</param>
        /// <param name="duration">Optional duration histogram recorded on completion.</param>
        /// <param name="inflight">Optional in-flight up/down counter.</param>
        /// <param name="baseTags">Tags applied to the in-flight counter and forwarded to count/duration.</param>
        /// <returns>A tracked operation.</returns>
        public TelemetryOperation Track(string spanName, SpanKindEnum kind, Convention? count, Convention? duration, Convention? inflight, params RadiantTag[] baseTags)
        {
            RadiantSpan? span = StartSpan(spanName, kind);
            return new TelemetryOperation(this, span, count, duration, inflight, baseTags);
        }

        /// <inheritdoc />
        public void Dispose()
        {
            if (_Host == null) return;
            try { _Host.ForceFlush(5000); } catch (Exception) { }
            try { _Host.Dispose(); } catch (Exception) { }
        }

        private static int SeverityFromName(string? name)
        {
            switch ((name ?? String.Empty).Trim().ToLowerInvariant())
            {
                case "trace": return 0;
                case "debug": return 1;
                case "info":
                case "information": return 2;
                case "warn":
                case "warning": return 4;
                case "error": return 5;
                case "critical": return 6;
                case "none": return 7;
                default: return 2;
            }
        }
    }
}
