using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using MovieWeb.Models.DTOs;

namespace MovieWeb.Controllers
{
    public partial class AdminController : Controller
    {
        // ========================================================
        // 📢 QUẢN LÝ POPUP THÔNG BÁO TOÀN TRANG (SYSTEM ANNOUNCEMENT)
        // ========================================================

        // [GET] /Admin/Announcements
        [HttpGet]
        public async Task<IActionResult> Announcements()
        {
            if (!await IsAdminAsync())
            {
                return Forbid();
            }

            var settings = await _announcementService.GetSettingsAsync();
            return View(settings);
        }

        // [POST] /Admin/Announcements
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Announcements(AnnouncementSettingsDto model, bool bumpVersion = false)
        {
            if (!await IsAdminAsync())
            {
                return Forbid();
            }

            try
            {
                var currentSettings = await _announcementService.GetSettingsAsync();

                if (bumpVersion)
                {
                    // Tăng version để người dùng cũ đã đóng popup vẫn sẽ thấy popup mới này
                    model.StorageKeyVersion = "v_" + DateTime.Now.ToString("yyyyMMdd_HHmmss");
                }
                else
                {
                    model.StorageKeyVersion = currentSettings.StorageKeyVersion;
                }

                var success = await _announcementService.SaveSettingsAsync(model);
                if (success)
                {
                    await LogAdminActionAsync("UpdateAnnouncement", $"Cập nhật popup thông báo: Trạng thái {(model.IsEnabled ? "BẬT" : "TẮT")}, Tiêu đề: {model.Title}");
                    TempData["SuccessMessage"] = "Đã lưu và cập nhật popup thông báo thành công!";
                }
                else
                {
                    TempData["ErrorMessage"] = "Không thể lưu cấu hình thông báo. Vui lòng thử lại!";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving announcement settings.");
                TempData["ErrorMessage"] = "Có lỗi xảy ra trong quá trình lưu thông báo.";
            }

            return RedirectToAction(nameof(Announcements));
        }

        // [HttpPost] /Admin/ToggleAnnouncement (Quick switch from Dashboard or Ajax)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleAnnouncement()
        {
            if (!await IsAdminAsync())
            {
                return Json(new { success = false, message = "Không có quyền thực hiện." });
            }

            try
            {
                var settings = await _announcementService.GetSettingsAsync();
                settings.IsEnabled = !settings.IsEnabled;
                var success = await _announcementService.SaveSettingsAsync(settings);

                if (success)
                {
                    await LogAdminActionAsync("ToggleAnnouncement", $"Bật/tắt nhanh popup thông báo: {(settings.IsEnabled ? "BẬT" : "TẮT")}");
                    return Json(new { success = true, isEnabled = settings.IsEnabled, message = settings.IsEnabled ? "Đã BẬT popup thông báo toàn trang!" : "Đã TẮT popup thông báo toàn trang!" });
                }

                return Json(new { success = false, message = "Lỗi khi lưu trạng thái." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error toggling announcement.");
                return Json(new { success = false, message = "Lỗi hệ thống: " + ex.Message });
            }
        }
    }
}
