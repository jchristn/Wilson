namespace Wilson.Core.Tools.Tools
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Net.Http.Headers;
    using System.Text;
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using System.Text.RegularExpressions;
    using System.Threading;
    using System.Threading.Tasks;
    using Wilson.Core.Models;
    using Wilson.Core.Settings;

    /// <summary>
    /// Performs a configured provider-backed web search.
    /// </summary>
    public sealed class WebSearchTool : IToolExecutor
    {
        /// <inheritdoc />
        public string Name => "web_search";
        /// <inheritdoc />
        public string Description => "Searches the web through a configured provider and returns a bounded list of result titles, URLs, and snippets.";
        /// <inheritdoc />
        public object ParametersSchema => new
        {
            type = "object",
            properties = new
            {
                query = new { type = "string", description = "Search query." },
                max_results = new { type = "integer", description = "Maximum result count. Defaults to the configured tool result item limit." },
                provider = new { type = "string", description = "Optional configured provider name." }
            },
            required = new[] { "query" },
            additionalProperties = false
        };
        /// <inheritdoc />
        public string Category => ToolCategories.Search;
        /// <inheritdoc />
        public bool RequiresApproval => false;
        /// <inheritdoc />
        public bool Dangerous => false;

        /// <inheritdoc />
        public async Task<ToolResult> ExecuteAsync(string toolCallId, JsonElement arguments, ToolExecutionContext context, CancellationToken token)
        {
            try
            {
                ToolJson.RejectUnknownProperties(arguments, "query", "max_results", "provider");
                string query = ToolJson.RequiredString(arguments, "query");
                string providerName = ToolJson.OptionalString(arguments, "provider", String.Empty);
                int maxResults = ToolJson.OptionalInt(arguments, "max_results", context.SafetyLimits.MaxToolResultItems, 1, context.SafetyLimits.MaxToolResultItems, true);
                WebSearchProviderSettings provider = SelectProvider(context.Settings.Tools.WebSearch, providerName);
                List<WebSearchResultItem> results = await SearchProviderAsync(provider, query, maxResults, token).ConfigureAwait(false);

                return ToolResultFactory.SuccessJson(toolCallId, new
                {
                    query,
                    provider = provider.Name,
                    result_count = results.Count,
                    results
                }, context);
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                return ToolResultFactory.Error(toolCallId, "cancelled", "Tool execution was cancelled.");
            }
            catch (OperationCanceledException)
            {
                return ToolResultFactory.Error(toolCallId, "request_timed_out", "Web search timed out.");
            }
            catch (ToolExecutionException ex)
            {
                return ToolResultFactory.Error(toolCallId, ex);
            }
            catch (HttpRequestException ex)
            {
                return ToolResultFactory.Error(toolCallId, "request_failed", ex.Message);
            }
            catch (JsonException ex)
            {
                return ToolResultFactory.Error(toolCallId, "invalid_provider_response", ex.Message);
            }
            catch (Exception ex)
            {
                return ToolResultFactory.Error(toolCallId, "web_search_error", ex.Message);
            }
        }

        private static WebSearchProviderSettings SelectProvider(WebSearchToolSettings settings, string providerName)
        {
            if (settings == null || !settings.Enabled)
                throw new ToolExecutionException("web_search_disabled", "Web search is disabled.");

            List<WebSearchProviderSettings> providers = (settings.Providers ?? new List<WebSearchProviderSettings>())
                .Where(provider => provider != null && provider.Enabled)
                .ToList();
            if (providers.Count == 0)
                throw new ToolExecutionException("web_search_provider_missing", "No enabled web search provider is configured.");

            if (!String.IsNullOrWhiteSpace(providerName))
            {
                WebSearchProviderSettings? named = providers.FirstOrDefault(provider => String.Equals(provider.Name, providerName, StringComparison.OrdinalIgnoreCase));
                if (named == null) throw new ToolExecutionException("web_search_provider_missing", "Requested web search provider is not configured or enabled.");
                return named;
            }

            return providers.FirstOrDefault(provider => provider.IsDefault) ?? providers[0];
        }

        private static async Task<List<WebSearchResultItem>> SearchProviderAsync(WebSearchProviderSettings provider, string query, int maxResults, CancellationToken token)
        {
            string providerType = provider.ProviderType ?? String.Empty;
            if (String.Equals(providerType, "tavily", StringComparison.OrdinalIgnoreCase))
                return await SearchTavilyAsync(provider, query, maxResults, token).ConfigureAwait(false);
            if (String.Equals(providerType, "you", StringComparison.OrdinalIgnoreCase) || String.Equals(providerType, "you.com", StringComparison.OrdinalIgnoreCase))
                return await SearchYouAsync(provider, query, maxResults, token).ConfigureAwait(false);
            if (String.Equals(providerType, "duckduckgo", StringComparison.OrdinalIgnoreCase) || String.Equals(providerType, "duckduckgo_html", StringComparison.OrdinalIgnoreCase))
                return await SearchDuckDuckGoHtmlAsync(provider, query, maxResults, token).ConfigureAwait(false);

            return await SearchGenericJsonAsync(provider, query, maxResults, token).ConfigureAwait(false);
        }

        private static async Task<List<WebSearchResultItem>> SearchTavilyAsync(WebSearchProviderSettings provider, string query, int maxResults, CancellationToken token)
        {
            string endpoint = String.IsNullOrWhiteSpace(provider.Endpoint) ? "https://api.tavily.com/search" : provider.Endpoint;
            using HttpClient client = CreateClient(provider);
            using HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, endpoint);
            string apiKey = ResolveApiKey(provider.ApiKey);
            request.Content = new StringContent(JsonSerializer.Serialize(new
            {
                api_key = apiKey,
                query,
                max_results = maxResults,
                include_answer = false,
                include_raw_content = false
            }), Encoding.UTF8, "application/json");

            using HttpResponseMessage response = await client.SendAsync(request, token).ConfigureAwait(false);
            string json = await ReadSuccessBodyAsync(response, token).ConfigureAwait(false);
            using JsonDocument document = JsonDocument.Parse(json);
            JsonElement root = document.RootElement;
            JsonElement results = root.TryGetProperty("results", out JsonElement value) && value.ValueKind == JsonValueKind.Array ? value : default;
            return ParseResultArray(results, maxResults, "title", "url", "content");
        }

        private static async Task<List<WebSearchResultItem>> SearchYouAsync(WebSearchProviderSettings provider, string query, int maxResults, CancellationToken token)
        {
            string endpoint = String.IsNullOrWhiteSpace(provider.Endpoint) ? "https://api.ydc-index.io/search" : provider.Endpoint;
            UriBuilder builder = new UriBuilder(endpoint);
            string prefix = String.IsNullOrWhiteSpace(builder.Query) ? String.Empty : builder.Query.TrimStart('&', '?') + "&";
            builder.Query = prefix + "query=" + Uri.EscapeDataString(query) + "&num_web_results=" + maxResults.ToString(System.Globalization.CultureInfo.InvariantCulture);

            using HttpClient client = CreateClient(provider);
            using HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, builder.Uri);
            string apiKey = ResolveApiKey(provider.ApiKey);
            if (!String.IsNullOrWhiteSpace(apiKey)) request.Headers.Add("X-API-Key", apiKey);

            using HttpResponseMessage response = await client.SendAsync(request, token).ConfigureAwait(false);
            string json = await ReadSuccessBodyAsync(response, token).ConfigureAwait(false);
            using JsonDocument document = JsonDocument.Parse(json);
            JsonElement root = document.RootElement;
            JsonElement results = TryArray(root, "hits");
            if (results.ValueKind != JsonValueKind.Array) results = TryArray(root, "results");
            return ParseResultArray(results, maxResults, "title", "url", "snippet");
        }

        private static async Task<List<WebSearchResultItem>> SearchGenericJsonAsync(WebSearchProviderSettings provider, string query, int maxResults, CancellationToken token)
        {
            if (String.IsNullOrWhiteSpace(provider.Endpoint))
                throw new ToolExecutionException("web_search_endpoint_missing", "Web search provider endpoint is required.");

            UriBuilder builder = new UriBuilder(provider.Endpoint);
            string prefix = String.IsNullOrWhiteSpace(builder.Query) ? String.Empty : builder.Query.TrimStart('&', '?') + "&";
            builder.Query = prefix + "q=" + Uri.EscapeDataString(query) + "&count=" + maxResults.ToString(System.Globalization.CultureInfo.InvariantCulture);

            using HttpClient client = CreateClient(provider);
            using HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, builder.Uri);
            string apiKey = ResolveApiKey(provider.ApiKey);
            if (!String.IsNullOrWhiteSpace(apiKey)) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

            using HttpResponseMessage response = await client.SendAsync(request, token).ConfigureAwait(false);
            string json = await ReadSuccessBodyAsync(response, token).ConfigureAwait(false);
            using JsonDocument document = JsonDocument.Parse(json);
            JsonElement root = document.RootElement;
            JsonElement results = TryArray(root, "results");
            if (results.ValueKind != JsonValueKind.Array) results = TryArray(root, "items");
            if (results.ValueKind != JsonValueKind.Array) results = TryArray(root, "webPages", "value");
            return ParseResultArray(results, maxResults, "title", "url", "snippet");
        }

        private static async Task<List<WebSearchResultItem>> SearchDuckDuckGoHtmlAsync(WebSearchProviderSettings provider, string query, int maxResults, CancellationToken token)
        {
            string endpoint = String.IsNullOrWhiteSpace(provider.Endpoint) ? "https://html.duckduckgo.com/html/" : provider.Endpoint;
            UriBuilder builder = new UriBuilder(endpoint);
            string prefix = String.IsNullOrWhiteSpace(builder.Query) ? String.Empty : builder.Query.TrimStart('&', '?') + "&";
            builder.Query = prefix + "q=" + Uri.EscapeDataString(query);

            using HttpClient client = CreateClient(provider);
            using HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, builder.Uri);
            request.Headers.Accept.ParseAdd("text/html,application/xhtml+xml");

            using HttpResponseMessage response = await client.SendAsync(request, token).ConfigureAwait(false);
            string html = await ReadSuccessBodyAsync(response, token).ConfigureAwait(false);
            return ParseDuckDuckGoHtml(html, maxResults);
        }

        private static HttpClient CreateClient(WebSearchProviderSettings provider)
        {
            HttpClient client = new HttpClient();
            client.Timeout = TimeSpan.FromMilliseconds(Math.Clamp(provider.TimeoutMs, 1000, 300000));
            client.DefaultRequestHeaders.UserAgent.ParseAdd("WilsonTool/1.0");
            return client;
        }

        private static async Task<string> ReadSuccessBodyAsync(HttpResponseMessage response, CancellationToken token)
        {
            string json = await response.Content.ReadAsStringAsync(token).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
                throw new HttpRequestException("Search provider returned HTTP " + ((int)response.StatusCode).ToString(System.Globalization.CultureInfo.InvariantCulture) + ".");
            return json;
        }

        private static string ResolveApiKey(string configured)
        {
            if (String.IsNullOrWhiteSpace(configured)) return String.Empty;
            string value = configured.Trim();
            if (value.StartsWith("env:", StringComparison.OrdinalIgnoreCase))
            {
                string name = value.Substring("env:".Length).Trim();
                return String.IsNullOrWhiteSpace(name) ? String.Empty : Environment.GetEnvironmentVariable(name) ?? String.Empty;
            }

            return value;
        }

        private static List<WebSearchResultItem> ParseDuckDuckGoHtml(string html, int maxResults)
        {
            List<WebSearchResultItem> items = new List<WebSearchResultItem>();
            if (String.IsNullOrWhiteSpace(html)) return items;

            MatchCollection matches = Regex.Matches(html, "<a[^>]+class=[\"'][^\"']*result__a[^\"']*[\"'][^>]+href=[\"'](?<href>[^\"']+)[\"'][^>]*>(?<title>.*?)</a>", RegexOptions.IgnoreCase | RegexOptions.Singleline, TimeSpan.FromSeconds(1));
            foreach (Match match in matches)
            {
                string url = DecodeDuckDuckGoUrl(match.Groups["href"].Value);
                if (String.IsNullOrWhiteSpace(url)) continue;

                string title = HtmlToText(match.Groups["title"].Value);
                string trailingHtml = html.Substring(match.Index, Math.Min(1200, html.Length - match.Index));
                string snippet = ExtractDuckDuckGoSnippet(trailingHtml);
                AddResult(items, title, url, snippet, maxResults);
                if (items.Count >= maxResults) break;
            }

            return items;
        }

        private static string ExtractDuckDuckGoSnippet(string html)
        {
            Match match = Regex.Match(html, "<a[^>]+class=[\"'][^\"']*result__snippet[^\"']*[\"'][^>]*>(?<snippet>.*?)</a>", RegexOptions.IgnoreCase | RegexOptions.Singleline, TimeSpan.FromSeconds(1));
            if (!match.Success)
                match = Regex.Match(html, "<div[^>]+class=[\"'][^\"']*result__snippet[^\"']*[\"'][^>]*>(?<snippet>.*?)</div>", RegexOptions.IgnoreCase | RegexOptions.Singleline, TimeSpan.FromSeconds(1));
            return match.Success ? HtmlToText(match.Groups["snippet"].Value) : String.Empty;
        }

        private static string DecodeDuckDuckGoUrl(string value)
        {
            string decoded = WebUtility.HtmlDecode(value ?? String.Empty).Trim();
            if (String.IsNullOrWhiteSpace(decoded)) return String.Empty;
            if (decoded.StartsWith("//", StringComparison.Ordinal)) decoded = "https:" + decoded;

            if (!Uri.TryCreate(decoded, UriKind.Absolute, out Uri? uri)) return String.Empty;
            if (String.Equals(uri.Host, "duckduckgo.com", StringComparison.OrdinalIgnoreCase)
                && uri.AbsolutePath.StartsWith("/l/", StringComparison.OrdinalIgnoreCase))
            {
                string? redirected = QueryValue(uri.Query, "uddg");
                if (!String.IsNullOrWhiteSpace(redirected)) decoded = redirected;
            }

            return Uri.TryCreate(decoded, UriKind.Absolute, out Uri? finalUri)
                && (String.Equals(finalUri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) || String.Equals(finalUri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
                ? finalUri.ToString()
                : String.Empty;
        }

        private static string? QueryValue(string query, string name)
        {
            string trimmed = (query ?? String.Empty).TrimStart('?');
            foreach (string part in trimmed.Split('&', StringSplitOptions.RemoveEmptyEntries))
            {
                string[] pieces = part.Split('=', 2);
                string key = Uri.UnescapeDataString(pieces[0].Replace("+", " "));
                if (!String.Equals(key, name, StringComparison.OrdinalIgnoreCase)) continue;
                return pieces.Length > 1 ? Uri.UnescapeDataString(pieces[1].Replace("+", " ")) : String.Empty;
            }

            return null;
        }

        private static string HtmlToText(string html)
        {
            string withoutTags = Regex.Replace(html ?? String.Empty, "<[^>]+>", " ", RegexOptions.Singleline, TimeSpan.FromSeconds(1));
            string decoded = WebUtility.HtmlDecode(withoutTags);
            return Regex.Replace(decoded, "[\\t\\r\\n ]+", " ", RegexOptions.None, TimeSpan.FromSeconds(1)).Trim();
        }

        private static void AddResult(List<WebSearchResultItem> items, string title, string url, string snippet, int maxResults)
        {
            if (items.Count >= maxResults) return;
            if (items.Any(item => String.Equals(item.Url, url, StringComparison.OrdinalIgnoreCase))) return;
            items.Add(new WebSearchResultItem
            {
                Title = title,
                Url = url,
                Snippet = snippet
            });
        }

        private static JsonElement TryArray(JsonElement root, string property)
        {
            return root.ValueKind == JsonValueKind.Object && root.TryGetProperty(property, out JsonElement value) && value.ValueKind == JsonValueKind.Array ? value : default;
        }

        private static JsonElement TryArray(JsonElement root, string parent, string property)
        {
            if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty(parent, out JsonElement parentElement) || parentElement.ValueKind != JsonValueKind.Object)
                return default;
            return parentElement.TryGetProperty(property, out JsonElement value) && value.ValueKind == JsonValueKind.Array ? value : default;
        }

        private static List<WebSearchResultItem> ParseResultArray(JsonElement results, int maxResults, string titleProperty, string urlProperty, string snippetProperty)
        {
            List<WebSearchResultItem> items = new List<WebSearchResultItem>();
            if (results.ValueKind != JsonValueKind.Array) return items;

            foreach (JsonElement result in results.EnumerateArray())
            {
                if (result.ValueKind != JsonValueKind.Object) continue;
                string title = StringValue(result, titleProperty);
                string url = StringValue(result, urlProperty);
                string snippet = StringValue(result, snippetProperty);
                if (String.IsNullOrWhiteSpace(url)) continue;

                items.Add(new WebSearchResultItem
                {
                    Title = title,
                    Url = url,
                    Snippet = snippet
                });

                if (items.Count >= maxResults) break;
            }

            return items;
        }

        private static string StringValue(JsonElement element, string property)
        {
            return element.TryGetProperty(property, out JsonElement value) && value.ValueKind == JsonValueKind.String ? value.GetString() ?? String.Empty : String.Empty;
        }

        private sealed class WebSearchResultItem
        {
            [JsonPropertyName("title")]
            public string Title { get; set; } = String.Empty;
            [JsonPropertyName("url")]
            public string Url { get; set; } = String.Empty;
            [JsonPropertyName("snippet")]
            public string Snippet { get; set; } = String.Empty;
        }
    }
}
