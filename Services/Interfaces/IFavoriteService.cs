using MovieWeb.Models.DTOs;

namespace MovieWeb.Services.Interfaces
{
    public interface IFavoriteService
    {
        Task<FavoriteListDto> GetUserFavoritesAsync(int userId, int page, int pageSize);
        Task<bool> AddFavoriteAsync(int userId, int movieId);
        Task<bool> RemoveFavoriteAsync(int userId, int movieId);
        Task<CheckFavoriteDto> CheckFavoriteAsync(int userId, int movieId);
    }
}