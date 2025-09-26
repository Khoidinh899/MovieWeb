using MovieWeb.Models.Entities;
using MovieWeb.Models.DTOs;

namespace MovieWeb.Repositories
{
    public interface IMovieRepository
    {
        // Lấy danh sách phim
        Task<PagedResult<Movie>> GetLatestMoviesAsync(int page = 1, int pageSize = 24);
        Task<PagedResult<Movie>> GetMoviesByTypeAsync(string type, int page = 1, int pageSize = 24);
        Task<PagedResult<Movie>> GetMoviesByCategoryAsync(string categorySlug, int page = 1, int pageSize = 24);
        Task<PagedResult<Movie>> SearchMoviesAsync(string keyword, int page = 1, int pageSize = 24);
        
        // Phim hot, phim đề xuất
        Task<List<Movie>> GetHotMoviesAsync(int take = 6);
        Task<List<Movie>> GetRecommendedMoviesAsync(int take = 12);
        Task<List<Movie>> GetRecentlyUpdatedAsync(int take = 12);
        
        // Chi tiết phim
        Task<Movie?> GetMovieBySlugAsync(string slug);
        Task<Movie?> GetMovieByIdAsync(int movieId);
        
        // Thống kê
        Task<int> GetTotalMoviesCountAsync();
        Task<List<Movie>> GetTopViewedMoviesAsync(int take = 10);
        Task<List<Movie>> GetTopRatedMoviesAsync(int take = 10);
        
        // Cập nhật thông tin phim
        Task UpdateViewCountAsync(int movieId);
        Task UpdateRatingAsync(int movieId);
        
        // Categories và Countries
        Task<List<Category>> GetCategoriesAsync();
        Task<List<Country>> GetCountriesAsync();
        Task<Category?> GetCategoryBySlugAsync(string slug);
    }
}