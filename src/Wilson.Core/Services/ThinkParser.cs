namespace Wilson.Core.Services
{
    using System;
    using System.Diagnostics;
    using System.Text;

    /// <summary>
    /// Incremental splitter that separates model reasoning delimited by <c>&lt;think&gt;</c>/<c>&lt;/think&gt;</c>
    /// from the visible answer as text streams in. Markers may straddle chunk boundaries, so a short tail is
    /// held back until it can be classified. Also accumulates the total time spent inside think blocks.
    /// </summary>
    public sealed class ThinkParser
    {
        private const string _Open = "<think>";
        private const string _Close = "</think>";

        private bool _Inside = false;
        private string _Pending = String.Empty;
        private readonly StringBuilder _Thinking = new StringBuilder();
        private long _ThinkingMs = 0;
        private long _ThinkStartTicks = 0;

        /// <summary>The accumulated reasoning text, trimmed.</summary>
        public string Thinking { get { return _Thinking.ToString().Trim(); } }

        /// <summary>Total milliseconds spent inside think blocks.</summary>
        public long ThinkingMs { get { return _ThinkingMs; } }

        /// <summary>Feed a streamed text fragment; returns the visible (non-thinking) portion to emit.</summary>
        /// <param name="text">The incoming fragment.</param>
        /// <returns>Visible text safe to stream to the caller.</returns>
        public string Feed(string text)
        {
            return Feed(text, out _);
        }

        /// <summary>
        /// Feed a streamed text fragment; returns the visible (non-thinking) portion to emit and, via
        /// <paramref name="reasoningDelta"/>, the reasoning fragment captured during this call so callers can
        /// stream the thinking box live.
        /// </summary>
        /// <param name="text">The incoming fragment.</param>
        /// <param name="reasoningDelta">The reasoning text newly captured during this call (empty when none).</param>
        /// <returns>Visible text safe to stream to the caller.</returns>
        public string Feed(string text, out string reasoningDelta)
        {
            int reasoningStart = _Thinking.Length;
            _Pending += text ?? String.Empty;
            StringBuilder visible = new StringBuilder();

            while (_Pending.Length > 0)
            {
                if (!_Inside)
                {
                    int i = _Pending.IndexOf(_Open, StringComparison.Ordinal);
                    if (i >= 0)
                    {
                        visible.Append(_Pending, 0, i);
                        _Pending = _Pending.Substring(i + _Open.Length);
                        _Inside = true;
                        _ThinkStartTicks = Stopwatch.GetTimestamp();
                    }
                    else
                    {
                        int keep = PartialTailLength(_Pending, _Open);
                        visible.Append(_Pending, 0, _Pending.Length - keep);
                        _Pending = _Pending.Substring(_Pending.Length - keep);
                        break;
                    }
                }
                else
                {
                    int j = _Pending.IndexOf(_Close, StringComparison.Ordinal);
                    if (j >= 0)
                    {
                        _Thinking.Append(_Pending, 0, j);
                        _Pending = _Pending.Substring(j + _Close.Length);
                        _Inside = false;
                        _ThinkingMs += ElapsedMs(_ThinkStartTicks);
                    }
                    else
                    {
                        int keep = PartialTailLength(_Pending, _Close);
                        _Thinking.Append(_Pending, 0, _Pending.Length - keep);
                        _Pending = _Pending.Substring(_Pending.Length - keep);
                        break;
                    }
                }
            }

            reasoningDelta = _Thinking.Length > reasoningStart
                ? _Thinking.ToString(reasoningStart, _Thinking.Length - reasoningStart)
                : String.Empty;

            return visible.ToString();
        }

        /// <summary>Flush any held-back text at end of stream. Returns trailing visible text (empty if the
        /// stream ended mid-think, in which case the remainder is folded into the captured reasoning).</summary>
        /// <returns>Trailing visible text.</returns>
        public string Finish()
        {
            string tail = _Pending;
            _Pending = String.Empty;
            if (_Inside)
            {
                _Thinking.Append(tail);
                _ThinkingMs += ElapsedMs(_ThinkStartTicks);
                _Inside = false;
                return String.Empty;
            }
            return tail;
        }

        /// <summary>Strip all think blocks from a complete text (non-streaming).</summary>
        /// <param name="text">Full text.</param>
        /// <returns>Text with reasoning removed.</returns>
        public static string Strip(string text)
        {
            if (String.IsNullOrEmpty(text)) return text ?? String.Empty;
            ThinkParser parser = new ThinkParser();
            string visible = parser.Feed(text);
            return visible + parser.Finish();
        }

        private static long ElapsedMs(long startTicks)
        {
            return (long)((Stopwatch.GetTimestamp() - startTicks) * 1000.0 / Stopwatch.Frequency);
        }

        // Longest suffix of s that is a proper prefix of marker, so a marker split across chunk boundaries
        // is not emitted prematurely.
        private static int PartialTailLength(string s, string marker)
        {
            int max = Math.Min(s.Length, marker.Length - 1);
            for (int len = max; len > 0; len--)
            {
                if (String.CompareOrdinal(s, s.Length - len, marker, 0, len) == 0) return len;
            }
            return 0;
        }
    }
}
