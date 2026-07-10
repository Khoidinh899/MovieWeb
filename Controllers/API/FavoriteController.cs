using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MovieWeb.Services;
using MovieWeb.Models.DTOs;
using System.Security.Claims;
using MovieWeb.Services.Interfaces;

namespace MovieWeb.Controllers.API
{
    [Authorize(AuthenticationSchemes = "Identity.Application,JwtScheme")]
    [Route("api/favorites")]
    [ApiController]
    public class FavoriteController : ControllerBase
    {
        private readonly IFavoriteService _favoriteService;
        private readonly ILogger<FavoriteController> _logger;

        public FavoriteController(IFavoriteService favoriteService, ILogger<FavoriteController> logger)
        {
            _favoriteService = favoriteService;
            _logger = logger;
        }

        // GET: api/favorites?page=1&pageSize=20
        [HttpGet]
        public async Task<IActionResult> GetFavorites([FromQuery] int page = 1, [FromQuery] int pageSize = 20)
        {
            try
            {
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int userId))
                {
                    return Unauthorized(new { message = "Vui lòng đăng nhập để xem danh sách yêu thích" });
                }

                var result = await _favoriteService.GetUserFavoritesAsync(userId, page, pageSize);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting favorites");
                return StatusCode(500, new { message = "Đã xảy ra lỗi khi tải danh sách yêu thích" });
            }
        }

        // POST: api/favorites/{movieId}
        [HttpPost("{movieId}")]
        public async Task<IActionResult> AddFavorite(int movieId)
        {
            try
            {
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int userId))
                {
                    return Unauthorized(new { message = "Vui lòng đăng nhập để thêm yêu thích" });
                }

                var success = await _favoriteService.AddFavoriteAsync(userId, movieId);
                if (!success)
                {
                    return BadRequest(new { message = "Không thể thêm phim vào yêu thích" });
                }

                return Ok(new { message = "Đã thêm vào danh sách yêu thích", success = true });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding favorite");
                return StatusCode(500, new { message = "Đã xảy ra lỗi khi thêm yêu thích" });
            }
        }

        // DELETE: api/favorites/{movieId}
        [HttpDelete("{movieId}")]
        public async Task<IActionResult> RemoveFavorite(int movieId)
        {
            try
            {
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int userId))
                {
                    return Unauthorized(new { message = "Vui lòng đăng nhập" });
                }

                var success = await _favoriteService.RemoveFavoriteAsync(userId, movieId);
                if (!success)
                {
                    return NotFound(new { message = "Không tìm thấy phim trong danh sách yêu thích" });
                }

                return Ok(new { message = "Đã xóa khỏi danh sách yêu thích", success = true });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing favorite");
                return StatusCode(500, new { message = "Đã xảy ra lỗi khi xóa yêu thích" });
            }
        }

        // GET: api/favorites/check/{movieId}
        [HttpGet("check/{movieId}")]
        public async Task<IActionResult> CheckFavorite(int movieId)
        {
            try
            {
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int userId))
                {
                    return Ok(new CheckFavoriteDto { IsFavorited = false });
                }

                var result = await _favoriteService.CheckFavoriteAsync(userId, movieId);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking favorite");
                return StatusCode(500, new { message = "Đã xảy ra lỗi" });
            }
        }
    }
}