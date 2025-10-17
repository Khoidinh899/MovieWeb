// File mới: Models/API/OPhimDetailResponse.cs

using Newtonsoft.Json;

namespace MovieWeb.Models.API
{
    public class OPhimDetailResponse
    {
        [JsonProperty("seoOnPage")]
        public SeoOnPage? SeoOnPage { get; set; }

        [JsonProperty("item")]
        public Movie? Item { get; set; } // Movie chính là class ApiMovie của bạn
    }

    public class SeoOnPage
    {
        [JsonProperty("descriptionHead")]
        public string? DescriptionHead { get; set; }
    }
}