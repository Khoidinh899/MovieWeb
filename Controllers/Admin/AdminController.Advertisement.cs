using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering; // Cần cho SelectListItem
using Microsoft.EntityFrameworkCore;
using MovieWeb.Data; // Cần cho MovieWebDbContext
using MovieWeb.Models.Entities; // Cần cho Advertisement
using System;
using System.Collections.Generic; // Cần cho List
using System.Linq; // Cần cho GetPlacementOptions
using System.Threading.Tasks; // Cần cho async

// Phải cùng namespace với file AdminController.cs
namespace MovieWeb.Controllers
{
    // Phải là "partial class" và tên y hệt
    public partial class AdminController : Controller
    {
        // ========================================
        // 📢 QUẢN LÝ QUẢNG CÁO (ADVERTISEMENT)
        // ========================================

        // [GET] /Admin/Advertisements
        [HttpGet]
        public async Task<IActionResult> Advertisements()
        {
            var advertisements = await _context.Advertisements
                                               .OrderBy(a => a.DisplayOrder)
                                               .ThenBy(a => a.AdName)
                                               .ToListAsync();
            return View(advertisements);
        }

        // [GET] /Admin/CreateAdvertisement
        [HttpGet]
        public IActionResult CreateAdvertisement()
        {
            ViewBag.Placements = GetPlacementOptions();

            // ⭐ Set giá trị mặc định
            var model = new Advertisement
            {
                IsActive = true,  // Mặc định BẬT
                DisplayOrder = 0  // Thứ tự mặc định
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateAdvertisement(Advertisement advertisement)
        {
            ViewBag.Placements = GetPlacementOptions();

            ModelState.Remove("AdId");
            ModelState.Remove("CreatedAt");
            ModelState.Remove("UpdatedAt");

            if (ModelState.IsValid)
            {
                try
                {
                    advertisement.CreatedAt = DateTime.Now;
                    advertisement.UpdatedAt = DateTime.Now;

                    // ⭐ Không cần xử lý gì thêm, IsActive đã được bind đúng
                    _context.Advertisements.Add(advertisement);
                    await _context.SaveChangesAsync();

                    TempData["SuccessMessage"] = "Đã thêm quảng cáo mới thành công!";
                    return RedirectToAction(nameof(Advertisements));
                }
                catch (DbUpdateException ex)
                {
                    var innerException = ex.InnerException;
                    string detailedError = ex.Message;

                    while (innerException != null)
                    {
                        detailedError += "\n→ " + innerException.Message;
                        innerException = innerException.InnerException;
                    }

                    _logger.LogError(ex, "Chi tiết lỗi DB: {DetailedError}", detailedError);
                    ModelState.AddModelError("", $"Lỗi database: {detailedError}");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Lỗi tạo quảng cáo");
                    ModelState.AddModelError("", $"Lỗi: {ex.Message}");
                }
            }

            // Log lỗi validation nếu có
            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage);
                _logger.LogWarning("Lỗi validation khi tạo QC: {Errors}", string.Join(", ", errors));
            }

            return View(advertisement);
        }


        // [GET] /Admin/EditAdvertisement/{id}
        [HttpGet]
        public async Task<IActionResult> EditAdvertisement(int id)
        {
            var advertisement = await _context.Advertisements.FindAsync(id);
            if (advertisement == null)
            {
                TempData["ErrorMessage"] = "Không tìm thấy quảng cáo.";
                return RedirectToAction(nameof(Advertisements));
            }

            // Lấy danh sách Vị trí đặt (Placement)
            ViewBag.Placements = GetPlacementOptions();
            return View(advertisement);
        }

        // [POST] /Admin/EditAdvertisement/{id}
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditAdvertisement(int id, Advertisement advertisement)
        {
            if (id != advertisement.AdId)
            {
                return NotFound();
            }

            // Lấy lại danh sách Placements cho ViewBag nếu model không hợp lệ
            ViewBag.Placements = GetPlacementOptions();

            if (ModelState.IsValid)
            {
                try
                {
                    advertisement.UpdatedAt = DateTime.Now;
                    _context.Update(advertisement);
                    await _context.SaveChangesAsync();
                    TempData["SuccessMessage"] = "Cập nhật quảng cáo thành công!";
                    return RedirectToAction(nameof(Advertisements));
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!_context.Advertisements.Any(e => e.AdId == advertisement.AdId))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Lỗi khi cập nhật quảng cáo ID: {AdId}", id); // Log lỗi
                    ModelState.AddModelError("", $"Lỗi khi cập nhật: {ex.Message}");
                }
            }
            return View(advertisement);
        }

        // [POST] /Admin/DeleteAdvertisement/{id}
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteAdvertisement(int id)
        {
            var advertisement = await _context.Advertisements.FindAsync(id);
            if (advertisement == null)
            {
                TempData["ErrorMessage"] = "Không tìm thấy quảng cáo!";
                return RedirectToAction(nameof(Advertisements));
            }

            try
            {
                _context.Advertisements.Remove(advertisement);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = $"Đã xóa quảng cáo '{advertisement.AdName}' thành công.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi xóa quảng cáo ID: {AdId}", id); // Log lỗi
                TempData["ErrorMessage"] = $"Lỗi khi xóa: {ex.Message}";
            }

            return RedirectToAction(nameof(Advertisements));
        }

        // Hàm trợ giúp để lấy các vị trí đặt QC
        private List<SelectListItem> GetPlacementOptions()
        {
            return new List<SelectListItem>
            {
                new SelectListItem { Value = "HomePage", Text = "Banner Trang Chủ" },
                new SelectListItem { Value = "HomePage_Popup", Text = "Pop-up Giữa Trang Chủ" },
                new SelectListItem { Value = "WatchPage_Banner", Text = "Banner Trang Xem Phim" },
                new SelectListItem { Value = "PreRoll", Text = "Video Pre-roll (Đầu phim)" },
                new SelectListItem { Value = "ClimaxAd", Text = "Video Climax (Cuối phim)" },
                new SelectListItem { Value = "Popup_PhimLe", Text = "Pop-up Trang Phim Lẻ" },
                new SelectListItem { Value = "Popup_PhimBo", Text = "Pop-up Trang Phim Bộ" },
                new SelectListItem { Value = "Popup_TheLoai", Text = "Pop-up Trang Thể Loại" },
                new SelectListItem { Value = "Popup_QuocGia", Text = "Pop-up Trang Quốc Gia" },
            };
        }
    }
}