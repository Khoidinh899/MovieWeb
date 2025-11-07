using Newtonsoft.Json;
using System.Collections.Generic;

namespace MovieWeb.Models.API
{
    public class OPhimDetailResponse
    {
        [JsonProperty("seoOnPage")]
        public SeoOnPage? SeoOnPage { get; set; }

        [JsonProperty("item")]
        public Movie? Item { get; set; }
    }

    public class SeoOnPage
    {
        [JsonProperty("descriptionHead")]
        public string? DescriptionHead { get; set; }
    }

    // 🎬 Phim chi tiết
    public class ApiMovie
    {
        [JsonProperty("name")]
        public string? Name { get; set; }

        [JsonProperty("slug")]
        public string? Slug { get; set; }

        [JsonProperty("origin_name")]
        public string? OriginName { get; set; }

        [JsonProperty("content")]
        public string? Content { get; set; }

        [JsonProperty("year")]
        public int? Year { get; set; }

        [JsonProperty("time")]
        public string? Duration { get; set; }

        [JsonProperty("poster_url")]
        public string? PosterUrl { get; set; }

        [JsonProperty("thumb_url")]
        public string? ThumbUrl { get; set; }

        [JsonProperty("trailer_url")]
        public string? TrailerUrl { get; set; }

        [JsonProperty("type")]
        public string? Type { get; set; }

        [JsonProperty("status")]
        public string? Status { get; set; }

        [JsonProperty("country")]
        public List<ApiCountry>? Countries { get; set; }

        [JsonProperty("category")]
        public List<ApiCategory>? Categories { get; set; }

        [JsonProperty("actor")]
        public List<ApiActor>? Actors { get; set; }

        [JsonProperty("director")]
        public List<ApiDirector>? Directors { get; set; }
    }

    public class ApiCountry
    {
        [JsonProperty("name")]
        public string? Name { get; set; }

        [JsonProperty("slug")]
        public string? Slug { get; set; }
    }

    public class ApiCategory
    {
        [JsonProperty("name")]
        public string? Name { get; set; }

        [JsonProperty("slug")]
        public string? Slug { get; set; }
    }

    public class ApiActor
    {
        [JsonProperty("name")]
        public string? Name { get; set; }

        [JsonProperty("slug")]
        public string? Slug { get; set; }

        [JsonProperty("avatar")]
        public string? Avatar { get; set; }

        [JsonProperty("biography")]
        public string? Biography { get; set; }
    }

    public class ApiDirector
    {
        [JsonProperty("name")]
        public string? Name { get; set; }

        [JsonProperty("slug")]
        public string? Slug { get; set; }

        [JsonProperty("avatar")]
        public string? Avatar { get; set; }

        [JsonProperty("biography")]
        public string? Biography { get; set; }
    }
}
