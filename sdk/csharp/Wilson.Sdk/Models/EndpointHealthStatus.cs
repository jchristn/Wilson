namespace Wilson.Sdk.Models
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json.Serialization;

    public sealed class EndpointHealthStatus
    {
        [JsonPropertyName("endpointId")]
        public string EndpointId { get; set; } = string.Empty;

        [JsonPropertyName("endpointName")]
        public string EndpointName { get; set; } = string.Empty;

        [JsonPropertyName("tenantId")]
        public string TenantId { get; set; } = string.Empty;

        [JsonPropertyName("isHealthy")]
        public bool IsHealthy { get; set; }

        [JsonPropertyName("firstCheckUtc")]
        public DateTime FirstCheckUtc { get; set; }

        [JsonPropertyName("lastCheckUtc")]
        public DateTime? LastCheckUtc { get; set; }

        [JsonPropertyName("lastHealthyUtc")]
        public DateTime? LastHealthyUtc { get; set; }

        [JsonPropertyName("lastUnhealthyUtc")]
        public DateTime? LastUnhealthyUtc { get; set; }

        [JsonPropertyName("lastStateChangeUtc")]
        public DateTime? LastStateChangeUtc { get; set; }

        [JsonPropertyName("totalUptimeMs")]
        public long TotalUptimeMs { get; set; }

        [JsonPropertyName("totalDowntimeMs")]
        public long TotalDowntimeMs { get; set; }

        [JsonPropertyName("uptimePercentage")]
        public double UptimePercentage { get; set; }

        [JsonPropertyName("consecutiveSuccesses")]
        public int ConsecutiveSuccesses { get; set; }

        [JsonPropertyName("consecutiveFailures")]
        public int ConsecutiveFailures { get; set; }

        [JsonPropertyName("lastError")]
        public string? LastError { get; set; }

        [JsonPropertyName("history")]
        public List<HealthCheckRecord> History { get; set; } = new List<HealthCheckRecord>();
    }
}
