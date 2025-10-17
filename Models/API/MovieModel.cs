// File đã sửa: Models/API/MovieModels.cs
using Newtonsoft.Json;
using System;
using System.Collections.Generic;

namespace MovieWeb.Models.API
{
    public class OPhimResponse
    {
        [JsonProperty("status")]
        public string Status { get; set; }

        [JsonProperty("msg")]
        public string Message { get; set; }

        [JsonProperty("data")]
        public OPhimData Data { get; set; }
    }

    public class OPhimData
    {
        [JsonProperty("items")]
        public List<Movie> Items { get; set; }

        [JsonProperty("params")]
        public OPhimParams Params { get; set; }
    }

    public class Movie
    {
        [JsonProperty("modified")]
        public ModifiedInfo Modified { get; set; }

        [JsonProperty("_id")]
        public string Id { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("slug")]
        public string Slug { get; set; }

        [JsonProperty("origin_name")]
        public string OriginName { get; set; }

        [JsonProperty("content")]
        public string Content { get; set; }

        [JsonProperty("type")]
        public string Type { get; set; }

        [JsonProperty("status")]
        public string Status { get; set; }

        [JsonProperty("poster_url")]
        public string PosterUrl { get; set; }

        [JsonProperty("thumb_url")]
        public string ThumbUrl { get; set; }

        [JsonProperty("trailer_url")]
        public string TrailerUrl { get; set; }

        [JsonProperty("time")]
        public string Time { get; set; }

        [JsonProperty("episode_current")]
        public string EpisodeCurrent { get; set; }

        [JsonProperty("episode_total")]
        public string EpisodeTotal { get; set; }

        [JsonProperty("quality")]
        public string Quality { get; set; }

        [JsonProperty("lang")]
        public string Lang { get; set; }

        [JsonProperty("year")]
        public int Year { get; set; }

        [JsonProperty("view")]
        public int View { get; set; }

        [JsonProperty("actor")]
        public List<string> Actor { get; set; }

        [JsonProperty("director")]
        public List<string> Director { get; set; }

        [JsonProperty("category")]
        public List<Category> Category { get; set; }

        [JsonProperty("country")]
        public List<Country> Country { get; set; }

        [JsonProperty("episodes")]
        public List<Episode> Episodes { get; set; } = new List<Episode>();
        
        // ===> CÁC THUỘC TÍNH BỊ THIẾU ĐÃ ĐƯỢC BỔ SUNG LẠI <===
        [JsonProperty("is_copyright")]
        public bool IsCopyright { get; set; }

        [JsonProperty("sub_docquyen")]
        public bool SubDocquyen { get; set; }

        [JsonProperty("chieurap")]
        public bool Chieurap { get; set; }

        [JsonProperty("notify")]
        public string Notify { get; set; }

        [JsonProperty("showtimes")]
        public string Showtimes { get; set; }
    }
    
    // Các class hỗ trợ khác giữ nguyên
    public class Episode
    {
        [JsonProperty("server_name")]
        public string ServerName { get; set; }

        [JsonProperty("server_data")]
        public List<ServerDatum> ServerData { get; set; } = new List<ServerDatum>();
    }

    public class ServerDatum
    {
        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("slug")]
        public string Slug { get; set; }

        [JsonProperty("link_m3u8")]
        public string LinkM3u8 { get; set; }
    }

    public class ModifiedInfo
    {
        [JsonProperty("time")]
        public DateTime Time { get; set; }
    }

    public class Category
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("slug")]
        public string Slug { get; set; }
    }
    public class Country
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("slug")]
        public string Slug { get; set; }
    }

    public class OPhimParams
    {
        [JsonProperty("type_slug")]
        public string TypeSlug { get; set; }

        [JsonProperty("filterCategory")]
        public List<string> FilterCategory { get; set; }

        [JsonProperty("filterCountry")]
        public List<string> FilterCountry { get; set; }

        [JsonProperty("filterYear")]
        public string FilterYear { get; set; }

        [JsonProperty("filterType")]
        public string FilterType { get; set; }

        [JsonProperty("sortField")]
        public string SortField { get; set; }

        [JsonProperty("sortType")]
        public string SortType { get; set; }

        [JsonProperty("pagination")]
        public Pagination Pagination { get; set; }
    }

    public class Pagination
    {
        [JsonProperty("totalItems")]
        public int TotalItems { get; set; }

        [JsonProperty("totalItemsPerPage")]
        public int TotalItemsPerPage { get; set; }

        [JsonProperty("currentPage")]
        public int CurrentPage { get; set; }

        [JsonProperty("totalPages")]
        public int TotalPages { get; set; }
    }
}