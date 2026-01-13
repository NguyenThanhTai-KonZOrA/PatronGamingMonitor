using Newtonsoft.Json;
using System.Collections.Generic;

namespace PatronGamingMonitor.Models
{
    public class PagedResult<T>
    {
        [JsonProperty("data")]
        public List<T> Items { get; set; } = new List<T>();

        [JsonProperty("totalCount")]
        public int TotalCount { get; set; }

        [JsonProperty("totalPages")]
        public int TotalPages { get; set; }

        [JsonProperty("pageIndex")]
        public int PageIndex { get; set; }

        [JsonProperty("pageSize")]
        public int PageSize { get; set; }

        [JsonProperty("filterType")]
        public string FilterType { get; set; }

        [JsonProperty("message")]
        public string Message { get; set; }

        [JsonProperty("success")]
        public bool Success { get; set; }
    }
}
