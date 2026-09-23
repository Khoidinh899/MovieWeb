using MovieWeb.Models.API;
using Newtonsoft.Json;
using System.Net.Http;
using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace MovieWeb.Services
{
    /// <summary>
    /// Interface cho service gọi API của VSMov.
    /// </summary>
    public interface IVSMovService
    {
        /// <summary>
        /// Lấy danh sách phim mới cập nhật theo trang.
        /// </summary>
        Task<VSMovListResponse?> GetLatestMoviesAsync(int page = 1);

        /// <summary>
        /// Lấy chi tiết phim theo slug.
        /// </summary>
        Task<VSMovDetailResponse?> GetMovieDetailAsync(string slug);

        /// <summary>
        /// Tìm kiếm phim theo từ khóa.
        /// </summary>
        Task<VSMovListResponse?> SearchMoviesAsync(string keyword, int page = 1);
    }

    /// <summary>
    /// Service gọi API của VSMov (https://vsmov.com).
    /// </summary>
    public class VSMovService : IVSMovService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<VSMovService> _logger;
        private readonly string _baseUrl = "https://vsmov.com";

        public VSMovService(HttpClient httpClient, ILogger<VSMovService> logger)
        {
            _httpClient = httpClient;
            _logger = logger;
            _httpClient.BaseAddress = new Uri(_baseUrl);
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "MovieWeb/1.0");
            _httpClient.Timeout = TimeSpan.FromSeconds(30);
        }

        /// <inheritdoc />
        public async Task<VSMovListResponse?> GetLatestMoviesAsync(int page = 1)
        {
            try
            {
                var url = $"/api/danh-sach/phim-moi-cap-nhat?page={page}";
                _logger.LogInformation("Đang gọi API VSMov danh sách phim mới, trang {Page}: {Url}", page, url);

                var response = await _httpClient.GetAsync(url);
                response.EnsureSuccessStatusCode();

                var json = await response.Content.ReadAsStringAsync();
                var result = JsonConvert.DeserializeObject<VSMovListResponse>(json);

                _logger.LogInformation("Lấy danh sách phim VSMov thành công, trang {Page}", page);
                return result;
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "Lỗi kết nối khi gọi API VSMov danh sách phim, trang {Page}", page);
                return null;
            }
            catch (TaskCanceledException ex)
            {
                _logger.LogError(ex, "Hết thời gian chờ khi gọi API VSMov danh sách phim, trang {Page}", page);
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi không xác định khi gọi API VSMov danh sách phim, trang {Page}", page);
                return null;
            }
        }

        /// <inheritdoc />
        public async Task<VSMovDetailResponse?> GetMovieDetailAsync(string slug)
        {
            try
            {
                var url = $"/api/phim/{slug}";
                _logger.LogInformation("Đang gọi API VSMov chi tiết phim: {Slug}", slug);

                var response = await _httpClient.GetAsync(url);
                response.EnsureSuccessStatusCode();

                var json = await response.Content.ReadAsStringAsync();
                var result = JsonConvert.DeserializeObject<VSMovDetailResponse>(json);

                _logger.LogInformation("Lấy chi tiết phim VSMov thành công: {Slug}", slug);
                return result;
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "Lỗi kết nối khi gọi API VSMov chi tiết phim: {Slug}", slug);
                return null;
            }
            catch (TaskCanceledException ex)
            {
                _logger.LogError(ex, "Hết thời gian chờ khi gọi API VSMov chi tiết phim: {Slug}", slug);
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi không xác định khi gọi API VSMov chi tiết phim: {Slug}", slug);
                return null;
            }
        }

        /// <inheritdoc />
        public async Task<VSMovListResponse?> SearchMoviesAsync(string keyword, int page = 1)
        {
            try
            {
                var url = $"/api/tim-kiem?keyword={Uri.EscapeDataString(keyword)}&page={page}";
                _logger.LogInformation("Đang gọi API VSMov tìm kiếm, keyword '{Keyword}', trang {Page}: {Url}", keyword, page, url);

                var response = await _httpClient.GetAsync(url);
                response.EnsureSuccessStatusCode();

                var json = await response.Content.ReadAsStringAsync();
                var result = JsonConvert.DeserializeObject<VSMovListResponse>(json);

                _logger.LogInformation("Tìm kiếm phim VSMov thành công, keyword '{Keyword}'", keyword);
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi gọi API VSMov tìm kiếm, keyword '{Keyword}'", keyword);
                return null;
            }
        }
    }
}
