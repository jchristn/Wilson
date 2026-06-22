namespace Wilson.Sdk.Models
{
    using System.Collections.Generic;
    using System.Text.Json.Serialization;

    public sealed class EnumerationResult<T>
    {
        [JsonPropertyName("objects")]
        public List<T> Objects { get; set; } = new List<T>();

        [JsonPropertyName("pageNumber")]
        public int PageNumber { get; set; }

        [JsonPropertyName("pageSize")]
        public int PageSize { get; set; }

        [JsonPropertyName("totalRecords")]
        public int TotalRecords { get; set; }

        [JsonPropertyName("totalPages")]
        public int TotalPages { get; set; }
    }
}
