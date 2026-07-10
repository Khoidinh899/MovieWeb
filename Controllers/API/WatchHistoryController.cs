using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MovieWeb.Services;
using MovieWeb.Models.DTOs;
using System.Security.Claims;
using MovieWeb.Services.Interfaces;

namespace MovieWeb.Controllers.API
{
    [Authorize(AuthenticationSchemes = "Identity.Application,JwtScheme")]
    [Route("api/watch-history")]
    [ApiController]
    public class WatchHistoryController : ControllerBase
    {
        private readonly IWatchHistoryService _watchHistoryService;
        private readonly ILogger<WatchHistoryController> _logger;

        public WatchHistoryController(IWatchHistoryService watchHistoryService, ILogger<WatchHistoryController> logger)
        {
            _watchHistoryService = watchHistoryService;
            _logger = logger;
        }

        // GET: api/watch-history?page=1&pageSize=20
        [HttpGet]
        public async Task<IActionResult> GetHistory([FromQuery] int page = 1, [FromQuery] int pageSize = 20)
        {
            try
            {
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int userId))
                {
                    return Unauthorized(new { message = "Vui lòng đăng nhập để xem lịch sử" });
                }

                var result = await _watchHistoryService.GetUserHistoryAsync(userId, page, pageSize);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting watch history");
                return StatusCode(500, new { message = "Đã xảy ra lỗi khi tải lịch sử xem" });
            }
        }

        // POST: api/watch-history
        [HttpPost]
        public async Task<IActionResult> SaveHistory([FromBody] SaveWatchHistoryDto dto)
        {
            try
            {
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int userId))
                {
                    return Unauthorized(new { message = "Vui lòng đăng nhập" });
                }

                var success = await _watchHistoryService.SaveWatchHistoryAsync(userId, dto);
                if (!success)
                {
                    return BadRequest(new { message = "Không thể lưu lịch sử xem" });
                }

                return Ok(new { message = "Đã lưu lịch sử xem", success = true });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving watch history");
                return StatusCode(500, new { message = "Đã xảy ra lỗi khi lưu lịch sử" });
            }
        }

        // DELETE: api/watch-history/{historyId}
        [HttpDelete("{historyId}")]
        public async Task<IActionResult> RemoveHistory(int historyId)
        {
            try
            {
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int userId))
                {
                    return Unauthorized(new { message = "Vui lòng đăng nhập" });
                }

                var success = await _watchHistoryService.RemoveHistoryAsync(userId, historyId);
                if (!success)
                {
                    return NotFound(new { message = "Không tìm thấy lịch sử xem" });
                }

                return Ok(new { message = "Đã xóa lịch sử xem", success = true });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing history");
                return StatusCode(500, new { message = "Đã xảy ra lỗi khi xóa lịch sử" });
            }
        }
        [HttpDelete("movie/{movieId}")]
        public async Task<IActionResult> RemoveHistoryByMovie(int movieId)
        {
            try
            {
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int userId))
                {
                    return Unauthorized(new { message = "Vui lòng đăng nhập" });
                }

                var success = await _watchHistoryService.RemoveHistoryByMovieAsync(userId, movieId);
                if (!success)
                {
                    return NotFound(new { message = "Không tìm thấy lịch sử xem" });
                }

                return Ok(new { message = "Đã xóa lịch sử xem", success = true });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing history by movie");
                return StatusCode(500, new { message = "Đã xảy ra lỗi khi xóa lịch sử" });
            }
        }

        // ⭐ THÊM API: Xóa toàn bộ lịch sử
        [HttpDelete("clear-all")]
        public async Task<IActionResult> ClearAllHistory()
        {
            try
            {
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int userId))
                {
                    return Unauthorized(new { message = "Vui lòng đăng nhập" });
                }

                var success = await _watchHistoryService.ClearAllHistoryAsync(userId);
                if (!success)
                {
                    return BadRequest(new { message = "Không thể xóa lịch sử" });
                }

                return Ok(new { message = "Đã xóa toàn bộ lịch sử xem", success = true });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error clearing all history");
                return StatusCode(500, new { message = "Đã xảy ra lỗi khi xóa lịch sử" });
            }
        }

        [HttpGet("resume/{movieId}")]
        public async Task<IActionResult> GetResumeInfo(int movieId, [FromQuery] int? episodeNumber = null)
        {
            try
            {
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int userId))
                {
                    return Ok(new ResumePlaybackDto { HasHistory = false });
                }

                var result = await _watchHistoryService.GetResumeInfoAsync(userId, movieId, episodeNumber);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting resume info");
                return StatusCode(500, new { message = "Đã xảy ra lỗi" });
            }
        }
    }
}