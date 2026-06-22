namespace Wilson.Sdk.Models
{
    using System.Text.Json.Serialization;

    public sealed class AuthenticateResult
    {
        [JsonPropertyName("token")]
        public string Token { get; set; } = string.Empty;
    }
}
