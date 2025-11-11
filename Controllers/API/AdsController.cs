using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MovieWeb.Data;
using MovieWeb.Models.Entities;
using MovieWeb.Services; // Bạn phải 'using' IAuthService của bạn
using System.Collections.Generic; // Cần cho List
using System.Linq; // Cần cho Contains
using System.Threading.Tasks; // Cần cho async

namespace MovieWeb.Controllers.API
{
    [Route("api/[controller]")]
    [ApiController]
    public class AdsController : ControllerBase
    {
        private readonly MovieWebDbContext _context;
        private readonly IAuthService _authService; // Dịch vụ kiểm tra user

        public AdsController(MovieWebDbContext context, IAuthService authService)
        {
            _context = context;
            _authService = authService;
        }

        /// <summary>
        /// API để lấy quảng cáo dựa trên vị trí đặt.
        /// Client sẽ gọi: /api/ads/get-placements?placements=HomePage&placements=PreRoll
        /// </summary>
        /// <param name="placements">Một mảng các vị trí (string) mà client cần.</param>
        /// <returns>Một danh sách các quảng cáo, hoặc danh sách rỗng nếu là Premium.</returns>
        [HttpGet("get-placements")]
        public async Task<IActionResult> GetAdsForPlacements([FromQuery] string[] placements)
        {
            try
            {
                var currentUser = await _authService.GetCurrentUserAsync();

                // **Logic quan trọng nhất: User premium KHÔNG BAO GIỜ thấy quảng cáo**
                if (currentUser?.IsPremium == true)
                {
                    // Trả về danh sách rỗng
                    return Ok(new List<Advertisement>()); 
                }

                // User thường (hoặc khách) -> Tìm quảng cáo
                var ads = await _context.Advertisements
                    .Where(ad => ad.IsActive && placements.Contains(ad.Placement))
                    .OrderBy(ad => ad.DisplayOrder) // Sắp xếp theo thứ tự
                    .ToListAsync();
                    
                // Trả về các quảng cáo hợp lệ
                return Ok(ads);
            }
            catch (Exception ex)
            {
                // Ghi log lỗi (Bạn nên có ILogger ở đây, nhưng tạm thời dùng Console)
                Console.WriteLine($"Lỗi API GetAdsForPlacements: {ex.Message}");
                // Trả về lỗi server
                return StatusCode(500, new { message = "Lỗi máy chủ nội bộ." });
            }
        }
    }
}