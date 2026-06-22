namespace Wilson.Sdk
{
    using System;
    using System.Collections.Generic;
    using System.Net.Http;
    using System.Net.Http.Headers;
    using System.Text;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using Wilson.Sdk.Models;

    public sealed class WilsonClient : IDisposable
    {
        private readonly HttpClient _HttpClient;
        private readonly bool _DisposeClient;
        private readonly string _BaseUrl;
        private readonly JsonSerializerOptions _Json = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

        public WilsonClient(string baseUrl, string? token = null, HttpClient? httpClient = null)
        {
            if (String.IsNullOrWhiteSpace(baseUrl)) throw new ArgumentException("Base URL is required.", nameof(baseUrl));
            _BaseUrl = baseUrl.TrimEnd('/');
            _HttpClient = httpClient ?? new HttpClient();
            _DisposeClient = httpClient == null;
            Token = token;
        }

        public string? Token { get; private set; }

        public void SetToken(string? token)
        {
            Token = token;
        }

        public async Task<AuthenticateResult> LoginAsync(string accessKey, CancellationToken token = default)
        {
            AuthenticateResult result = await SendAsync<AuthenticateResult>(HttpMethod.Post, "/v1.0/auth/token", new { accessKey }, token).ConfigureAwait(false);
            Token = result.Token;
            return result;
        }

        public Task<EnumerationResult<ModelRunnerStatus>> GetModelRunnersAsync(int pageNumber = 1, int pageSize = 100, CancellationToken token = default)
        {
            return SendAsync<EnumerationResult<ModelRunnerStatus>>(HttpMethod.Get, "/v1.0/api/model-runners?pageNumber=" + pageNumber + "&pageSize=" + pageSize, null, token);
        }

        public Task<List<EndpointHealthStatus>> GetModelRunnerHealthAsync(CancellationToken token = default)
        {
            return SendAsync<List<EndpointHealthStatus>>(HttpMethod.Get, "/v1.0/api/model-runners/health", null, token);
        }

        public Task<EndpointHealthStatus> GetModelRunnerHealthAsync(string runnerId, CancellationToken token = default)
        {
            if (String.IsNullOrWhiteSpace(runnerId)) throw new ArgumentException("Runner ID is required.", nameof(runnerId));
            return SendAsync<EndpointHealthStatus>(HttpMethod.Get, "/v1.0/api/model-runners/" + Uri.EscapeDataString(runnerId) + "/health", null, token);
        }

        public void Dispose()
        {
            if (_DisposeClient) _HttpClient.Dispose();
        }

        private async Task<T> SendAsync<T>(HttpMethod method, string path, object? body, CancellationToken token)
        {
            using HttpRequestMessage request = new HttpRequestMessage(method, _BaseUrl + path);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            if (!String.IsNullOrWhiteSpace(Token)) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", Token);

            if (body != null)
            {
                string json = JsonSerializer.Serialize(body, _Json);
                request.Content = new StringContent(json, Encoding.UTF8, "application/json");
            }

            using HttpResponseMessage response = await _HttpClient.SendAsync(request, token).ConfigureAwait(false);
            string responseBody = await response.Content.ReadAsStringAsync(token).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                throw new InvalidOperationException(String.IsNullOrWhiteSpace(responseBody)
                    ? "Wilson API request failed with HTTP " + (int)response.StatusCode
                    : responseBody);
            }

            T? result = JsonSerializer.Deserialize<T>(responseBody, _Json);
            if (result == null) throw new InvalidOperationException("Wilson API returned an empty response.");
            return result;
        }
    }
}
