namespace Wilson.Sdk.Models
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// Tool approval request.
    /// </summary>
    public sealed class ToolApprovalRequest
    {
        /// <summary>Whether to approve execution.</summary>
        [JsonPropertyName("approved")]
        public bool Approved { get; set; }

        /// <summary>Optional user-visible reason for denial.</summary>
        [JsonPropertyName("reason")]
        public string Reason { get; set; } = string.Empty;
    }
}
