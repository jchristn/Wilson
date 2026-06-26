namespace Wilson.Core.Tools.Tools
{
    using System;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Text.Json;
    using System.Text.RegularExpressions;
    using System.Threading;
    using System.Threading.Tasks;
    using Wilson.Core.Models;

    /// <summary>
    /// Retrieves text from an HTTP or HTTPS URL.
    /// </summary>
    public sealed class WebRetrieveTool : IToolExecutor
    {
        /// <inheritdoc />
        public string Name => "web_retrieve";
        /// <inheritdoc />
        public string Description => "Retrieves text content from an absolute HTTP or HTTPS URL. HTML responses are converted to readable text.";
        /// <inheritdoc />
        public object ParametersSchema => new
        {
            type = "object",
            properties = new
            {
                url = new { type = "string", description = "Absolute HTTP or HTTPS URL to retrieve." },
                timeout_ms = new { type = "integer", description = "Request timeout in milliseconds. Defaults to the configured tool timeout." },
                max_content_chars = new { type = "integer", description = "Maximum response text characters to return. Defaults to the configured tool output limit." }
            },
            required = new[] { "url" },
            additionalProperties = false
        };
        /// <inheritdoc />
        public string Category => ToolCategories.Web;
        /// <inheritdoc />
        public bool RequiresApproval => false;
        /// <inheritdoc />
        public bool Dangerous => false;

        /// <inheritdoc />
        public async Task<ToolResult> ExecuteAsync(string toolCallId, JsonElement arguments, ToolExecutionContext context, CancellationToken token)
        {
            try
            {
                ToolJson.RejectUnknownProperties(arguments, "url", "timeout_ms", "max_content_chars");
                string url = ToolJson.RequiredString(arguments, "url");
                if (!Uri.TryCreate(url, UriKind.Absolute, out Uri? uri)) return ToolResultFactory.Error(toolCallId, "invalid_url", "URL must be absolute.");
                if (!String.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) && !String.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
                    return ToolResultFactory.Error(toolCallId, "unsupported_url_scheme", "Only http and https URLs are supported.");

                int timeoutMs = ToolJson.OptionalInt(arguments, "timeout_ms", context.SafetyLimits.ToolTimeoutMs, 1000, context.SafetyLimits.ToolTimeoutMs, true);
                int maxChars = ToolJson.OptionalInt(arguments, "max_content_chars", context.SafetyLimits.MaxToolOutputChars, 1, context.SafetyLimits.MaxToolOutputChars, true);

                using CancellationTokenSource timeoutSource = new CancellationTokenSource(timeoutMs);
                using CancellationTokenSource linkedSource = CancellationTokenSource.CreateLinkedTokenSource(token, timeoutSource.Token);
                using HttpClientHandler handler = new HttpClientHandler { AllowAutoRedirect = true };
                using HttpClient client = new HttpClient(handler);
                using HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, uri);
                request.Headers.UserAgent.ParseAdd("WilsonTool/1.0");
                request.Headers.Accept.ParseAdd("text/html, text/plain, application/json;q=0.8, */*;q=0.5");

                using HttpResponseMessage response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, linkedSource.Token).ConfigureAwait(false);
                string content = await response.Content.ReadAsStringAsync(linkedSource.Token).ConfigureAwait(false);
                string contentType = response.Content.Headers.ContentType?.MediaType ?? String.Empty;
                string text = IsHtml(contentType, content) ? HtmlToText(content) : content;
                text = NormalizeWhitespace(text);
                string title = IsHtml(contentType, content) ? ExtractTitle(content) : String.Empty;
                int originalCharacters = text.Length;
                bool truncated = text.Length > maxChars;
                if (truncated) text = text.Substring(0, maxChars);

                return ToolResultFactory.SuccessJson(toolCallId, new
                {
                    url = uri.ToString(),
                    final_url = response.RequestMessage?.RequestUri?.ToString() ?? uri.ToString(),
                    status_code = (int)response.StatusCode,
                    success_status = response.IsSuccessStatusCode,
                    content_type = contentType,
                    title,
                    text,
                    truncated,
                    original_characters = originalCharacters
                }, context);
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                return ToolResultFactory.Error(toolCallId, "cancelled", "Tool execution was cancelled.");
            }
            catch (OperationCanceledException)
            {
                return ToolResultFactory.Error(toolCallId, "request_timed_out", "Web retrieval timed out.");
            }
            catch (ToolExecutionException ex)
            {
                return ToolResultFactory.Error(toolCallId, ex);
            }
            catch (HttpRequestException ex)
            {
                return ToolResultFactory.Error(toolCallId, "request_failed", ex.Message);
            }
            catch (Exception ex)
            {
                return ToolResultFactory.Error(toolCallId, "web_retrieve_error", ex.Message);
            }
        }

        private static bool IsHtml(string contentType, string content)
        {
            return contentType.Contains("html", StringComparison.OrdinalIgnoreCase)
                || content.TrimStart().StartsWith("<!doctype html", StringComparison.OrdinalIgnoreCase)
                || content.TrimStart().StartsWith("<html", StringComparison.OrdinalIgnoreCase);
        }

        private static string ExtractTitle(string html)
        {
            Match match = Regex.Match(html, "<title[^>]*>(.*?)</title>", RegexOptions.IgnoreCase | RegexOptions.Singleline, TimeSpan.FromSeconds(1));
            return match.Success ? WebUtility.HtmlDecode(match.Groups[1].Value).Trim() : String.Empty;
        }

        private static string HtmlToText(string html)
        {
            string withoutScripts = Regex.Replace(html, "<(script|style)[^>]*>.*?</\\1>", " ", RegexOptions.IgnoreCase | RegexOptions.Singleline, TimeSpan.FromSeconds(1));
            string withBreaks = Regex.Replace(withoutScripts, "</?(p|div|section|article|header|footer|br|li|tr|h[1-6])[^>]*>", "\n", RegexOptions.IgnoreCase, TimeSpan.FromSeconds(1));
            string withoutTags = Regex.Replace(withBreaks, "<[^>]+>", " ", RegexOptions.Singleline, TimeSpan.FromSeconds(1));
            return WebUtility.HtmlDecode(withoutTags);
        }

        private static string NormalizeWhitespace(string text)
        {
            string normalized = text.Replace("\r", "\n");
            string[] lines = normalized
                .Split('\n')
                .Select(line => Regex.Replace(line, "[\\t ]+", " ", RegexOptions.None, TimeSpan.FromSeconds(1)).Trim())
                .Where(line => line.Length > 0)
                .ToArray();
            return String.Join("\n", lines);
        }
    }
}
