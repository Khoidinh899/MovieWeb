// Models/API/VSMovModels.cs
// DTO classes để deserialize JSON response từ VSMov API (https://vsmov.com/api/...)
using Newtonsoft.Json;
using System;
using System.Collections.Generic;

namespace MovieWeb.Models.API
{
    // ============================================================
    // 📋 Response wrapper cho API danh sách phim
    // Endpoint: /api/danh-sach/phim-moi-cap-nhat?page=1
    // ============================================================
    public class VSMovListResponse
    {
        [JsonProperty("status")]
        public bool Status { get; set; }

        [JsonProperty("items")]
        public List<VSMovItem> Items { get; set; } = new List<VSMovItem>();

        [JsonProperty("pathImage")]
        public string PathImage { get; set; }

        [JsonProperty("pagination")]
        public VSMovPagination Pagination { get; set; }
    }

    // ============================================================
    // 🎬 Response wrapper cho API chi tiết phim
    // Endpoint: /api/phim/{slug}
    // ============================================================
    public class VSMovDetailResponse
    {
        [JsonProperty("status")]
        public bool Status { get; set; }

        [JsonProperty("movie")]
        public VSMovItem Item { get; set; }

        [JsonProperty("episodes")]
        public List<VSMovEpisodeServer> Episodes { get; set; } = new List<VSMovEpisodeServer>();
    }

    // ============================================================
    // 🎥 Thông tin phim từ VSMov
    // Cấu trúc tương tự Movie (OPhim) nhưng có thêm tmdb, imdb
    // và episodes sử dụng link_embed thay vì link_m3u8
    // ============================================================
    public class VSMovItem
    {
        [JsonProperty("tmdb")]
        public VSMovTmdb Tmdb { get; set; }

        [JsonProperty("imdb")]
        public VSMovImdb Imdb { get; set; }

        [JsonProperty("modified")]
        public ModifiedInfo Modified { get; set; }

        [JsonProperty("_id")]
        public int Id { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("origin_name")]
        public string OriginName { get; set; }

        [JsonProperty("slug")]
        public string Slug { get; set; }

        [JsonProperty("content")]
        public string Content { get; set; }

        [JsonProperty("type")]
        public string Type { get; set; }

        [JsonProperty("status")]
        public string Status { get; set; }

        [JsonProperty("poster_url")]
        [JsonConverter(typeof(SafeStringConverter))]
        public string PosterUrl { get; set; }

        [JsonProperty("thumb_url")]
        [JsonConverter(typeof(SafeStringConverter))]
        public string ThumbUrl { get; set; }

        [JsonProperty("trailer_url")]
        [JsonConverter(typeof(SafeStringConverter))]
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

        [JsonProperty("actor")]
        public List<string> Actor { get; set; }

        [JsonProperty("director")]
        public List<string> Director { get; set; }

        [JsonProperty("category")]
        public List<Category> Category { get; set; }

        [JsonProperty("country")]
        public List<Country> Country { get; set; }

        [JsonProperty("episodes")]
        public List<VSMovEpisodeServer> Episodes { get; set; } = new List<VSMovEpisodeServer>();
    }

    // ============================================================
    // 📺 Thông tin TMDB đính kèm (type, id, season, vote)
    // ============================================================
    public class VSMovTmdb
    {
        [JsonProperty("type")]
        public string Type { get; set; }

        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("season")]
        public int? Season { get; set; }

        [JsonProperty("vote_average")]
        public string VoteAverage { get; set; }

        [JsonProperty("vote_count")]
        public int? VoteCount { get; set; }
    }

    // ============================================================
    // 🎞️ Thông tin IMDB đính kèm
    // ============================================================
    public class VSMovImdb
    {
        [JsonProperty("id")]
        public string Id { get; set; }
    }

    // ============================================================
    // 🖥️ Server phát tập phim (Vietsub #1, Thuyết Minh #1, ...)
    // Tương tự Episode (OPhim) nhưng server_data chứa VSMovEpisodeData
    // ============================================================
    public class VSMovEpisodeServer
    {
        [JsonProperty("server_name")]
        public string ServerName { get; set; }

        [JsonProperty("server_data")]
        public List<VSMovEpisodeData> ServerData { get; set; } = new List<VSMovEpisodeData>();
    }

    // ============================================================
    // ▶️ Dữ liệu từng tập phim
    // Khác với OPhim: dùng link_embed thay vì link_m3u8
    // ============================================================
    public class VSMovEpisodeData
    {
        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("slug")]
        public string Slug { get; set; }

        [JsonProperty("filename")]
        public string Filename { get; set; }

        [JsonProperty("link_embed")]
        public string LinkEmbed { get; set; }
    }

    // ============================================================
    // 📄 Thông tin phân trang từ VSMov API
    // Cấu trúc giống Pagination (OPhim)
    // ============================================================
    public class VSMovPagination
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

    // ============================================================
    // 🛠️ Custom JsonConverter để tránh lỗi khi API trả về {} thay vì string
    // ============================================================
    public class SafeStringConverter : JsonConverter<string>
    {
        public override string ReadJson(JsonReader reader, Type objectType, string existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.StartObject)
            {
                Newtonsoft.Json.Linq.JObject.Load(reader);
                return null;
            }
            if (reader.TokenType == JsonToken.StartArray)
            {
                Newtonsoft.Json.Linq.JArray.Load(reader);
                return null;
            }
            return reader.Value?.ToString();
        }

        public override void WriteJson(JsonWriter writer, string value, JsonSerializer serializer)
        {
            writer.WriteValue(value);
        }
    }
}
