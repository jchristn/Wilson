namespace Wilson.Core.Observability
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using Radiant;

    /// <summary>
    /// A tracked unit of work encapsulating span + duration histogram + counter + in-flight up/down counter. On
    /// dispose it records the elapsed seconds and the counter with the accumulated metric tags, then decrements the
    /// in-flight counter and disposes the span. All emits are best-effort.
    /// </summary>
    public sealed class TelemetryOperation : IDisposable
    {
        private readonly TelemetryService _Telemetry;
        private readonly RadiantSpan? _Span;
        private readonly Convention? _Count;
        private readonly Convention? _Duration;
        private readonly Convention? _Inflight;
        private readonly RadiantTag[] _BaseTags;
        private readonly List<RadiantTag> _MetricTags;
        private readonly long _StartTicks;
        private bool _Disposed;

        internal TelemetryOperation(TelemetryService telemetry, RadiantSpan? span, Convention? count, Convention? duration, Convention? inflight, RadiantTag[]? baseTags)
        {
            _Telemetry = telemetry;
            _Span = span;
            _Count = count;
            _Duration = duration;
            _Inflight = inflight;
            _BaseTags = baseTags ?? Array.Empty<RadiantTag>();
            _MetricTags = new List<RadiantTag>(_BaseTags);
            _StartTicks = Stopwatch.GetTimestamp();
            if (_Inflight != null) _Telemetry.Add(_Inflight, 1, _BaseTags);
        }

        /// <summary>Set a high-cardinality tag on the span only.</summary>
        /// <param name="key">Tag key.</param>
        /// <param name="value">Tag value.</param>
        /// <returns>This operation.</returns>
        public TelemetryOperation SetTag(string key, object value)
        {
            _Span?.SetTag(key, value);
            return this;
        }

        /// <summary>Add a low-cardinality tag applied to the counter and duration histogram.</summary>
        /// <param name="key">Tag key.</param>
        /// <param name="value">Tag value.</param>
        /// <returns>This operation.</returns>
        public TelemetryOperation Tag(string key, object value)
        {
            _MetricTags.Add(new RadiantTag(key, value));
            return this;
        }

        /// <summary>Mark the span successful and add an <c>outcome=ok</c> metric tag.</summary>
        /// <returns>This operation.</returns>
        public TelemetryOperation SetOk()
        {
            _MetricTags.Add(new RadiantTag("outcome", "ok"));
            _Span?.SetOk(String.Empty);
            return this;
        }

        /// <summary>Record an HTTP status code on the span and add status-class/status-code metric tags.</summary>
        /// <param name="statusCode">HTTP status code.</param>
        /// <returns>This operation.</returns>
        public TelemetryOperation SetStatus(int statusCode)
        {
            string statusClass = (statusCode / 100).ToString(System.Globalization.CultureInfo.InvariantCulture) + "xx";
            _MetricTags.Add(new RadiantTag("status_class", statusClass));
            _MetricTags.Add(new RadiantTag(SemConv.Http.AttributeStatusCode, statusCode));
            _Span?.SetTag(SemConv.Http.AttributeStatusCode, statusCode);
            if (statusCode >= 500) _Span?.SetError("HTTP " + statusCode.ToString(System.Globalization.CultureInfo.InvariantCulture));
            else _Span?.SetOk(String.Empty);
            return this;
        }

        /// <summary>Mark the operation failed: record the exception on the span and add an <c>outcome=error</c> tag.</summary>
        /// <param name="exception">The exception.</param>
        public void Fail(Exception exception)
        {
            _MetricTags.Add(new RadiantTag("outcome", "error"));
            _Span?.RecordException(exception, true);
        }

        /// <inheritdoc />
        public void Dispose()
        {
            if (_Disposed) return;
            _Disposed = true;

            try
            {
                double seconds = (Stopwatch.GetTimestamp() - _StartTicks) / (double)Stopwatch.Frequency;
                RadiantTag[] metricTags = _MetricTags.ToArray();
                if (_Duration != null) _Telemetry.Record(_Duration, seconds, metricTags);
                if (_Count != null) _Telemetry.Increment(_Count, 1, metricTags);
                if (_Inflight != null) _Telemetry.Add(_Inflight, -1, _BaseTags);
            }
            catch (Exception)
            {
            }

            _Span?.Dispose();
        }
    }
}
