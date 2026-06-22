namespace Wilson.Sdk.Models
{
    using System;
    using System.Text.Json.Serialization;

    public sealed class HealthCheckRecord
    {
        [JsonPropertyName("timestampUtc")]
        public DateTime TimestampUtc { get; set; }

        [JsonPropertyName("success")]
        public bool Success { get; set; }
    }
}
