using MovieWeb.Models.API;
using Newtonsoft.Json;
using System.Net.Http;
using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace MovieWeb.Services
{
    public interface IOPhimService
    {
        Task<OPhimResponse> GetLatestMoviesAsync(int page = 1);
        Task<OPhimResponse> GetMoviesByTypeAsync(string type, int page = 1);
        Task<OPhimDetailResponse?> GetMovieDetailAsync(string slug);
        Task<OPhimResponse> SearchMoviesAsync(string keyword, int page = 1);
        Task<List<Movie>> GetHotMoviesAsync(int limit = 6);
    }

    public class OPhimService : IOPhimService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<OPhimService> _logger;
        private readonly string _baseUrl = "https://ophim1.com";

        public OPhimService(HttpClient httpClient, ILogger<OPhimService> logger)
        {
            _httpClient = httpClient;
            _logger = logger;
            _httpClient.BaseAddress = new Uri(_baseUrl);
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "MovieWeb/1.0");
            _httpClient.Timeout = TimeSpan.FromSeconds(30);
        }

        public async Task<OPhimDetailResponse?> GetMovieDetailAsync(string slug)
        {
            try
            {
                // ===> SỬA LỖI QUAN TRỌNG NHẤT: TRẢ LẠI ĐÚNG URL API <===
                var url = $"/v1/api/phim/{slug}"; 

                var response = await _httpClient.GetAsync(url);

                if (response.IsSuccessStatusCode)
                {
                    var jsonContent = await response.Content.ReadAsStringAsync();
                    var jObject = JObject.Parse(jsonContent);
                    var dataObject = jObject["data"];

                    if (dataObject != null)
                    {
                        return dataObject.ToObject<OPhimDetailResponse>();
                    }
                }

                _logger.LogWarning($"Không tìm thấy chi tiết phim: {slug} (Status: {response.StatusCode})");
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Lỗi khi gọi API chi tiết cho slug: {slug}");
                return null;
            }
        }
        
        // --- CÁC HÀM CŨ GIỮ NGUYÊN ---
        public async Task<OPhimResponse> GetLatestMoviesAsync(int page = 1)
        {
            return await GetApiResponseAsync($"/v1/api/danh-sach/phim-moi-cap-nhat?page={page}");
        }

        public async Task<OPhimResponse> GetMoviesByTypeAsync(string type, int page = 1)
        {
            return await GetApiResponseAsync($"/v1/api/danh-sach/{type}?page={page}");
        }

        public async Task<OPhimResponse> SearchMoviesAsync(string keyword, int page = 1)
        {
            return await GetApiResponseAsync($"/v1/api/tim-kiem?keyword={Uri.EscapeDataString(keyword)}&page={page}");
        }

        public async Task<List<Movie>> GetHotMoviesAsync(int limit = 6)
        {
            try
            {
                var response = await _httpClient.GetAsync("/v1/api/home");
                if (response.IsSuccessStatusCode)
                {
                    var jsonContent = await response.Content.ReadAsStringAsync();
                    var result = JsonConvert.DeserializeObject<dynamic>(jsonContent);
                    var recommendJson = result?["data"]?["recommend"]?.ToString();
                    if (!string.IsNullOrEmpty(recommendJson))
                    {
                        var recommendMovies = JsonConvert.DeserializeObject<List<Movie>>(recommendJson);
                        return recommendMovies?.Take(limit).ToList() ?? new List<Movie>();
                    }
                }
                return new List<Movie>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calling OPhim API for hot movies");
                return new List<Movie>();
            }
        }
        
        private async Task<OPhimResponse> GetApiResponseAsync(string url)
        {
            try
            {
                var response = await _httpClient.GetAsync(url);
                if (response.IsSuccessStatusCode)
                {
                    var jsonContent = await response.Content.ReadAsStringAsync();
                    return JsonConvert.DeserializeObject<OPhimResponse>(jsonContent) ?? new OPhimResponse { Data = new OPhimData { Items = new List<Movie>() } };
                }
                _logger.LogError($"API call failed: {url} => {response.StatusCode}");
                return new OPhimResponse { Status = "error", Message = "Không thể tải dữ liệu", Data = new OPhimData { Items = new List<Movie>() } };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error calling OPhim API {url}");
                return new OPhimResponse { Status = "error", Message = ex.Message, Data = new OPhimData { Items = new List<Movie>() } };
            }
        }
    }
}