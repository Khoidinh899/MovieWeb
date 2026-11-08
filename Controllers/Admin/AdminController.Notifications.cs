using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MovieWeb.Models.DTOs;
using MovieWeb.Models.Entities;
using MovieWeb.Models.ViewModels;

namespace MovieWeb.Controllers
{
    public partial class AdminController : Controller
    {
        // ========================================================
        // 🔔 QUẢN LÝ THÔNG BÁO HỆ THỐNG & BROADCASTS (NOTIFICATIONS)
        // ========================================================

        [HttpGet]
        public async Task<IActionResult> Notifications(string tab = "templates", int page = 1, int? reuseId = null, int? templateId = null)
        {
            if (!await IsAdminAsync())
            {
                return Forbid();
            }

            tab = tab.ToLowerInvariant();
            if (tab != "templates" && tab != "send" && tab != "history")
            {
                tab = "templates";
            }

            var templates = await _context.NotificationTemplates
                .OrderByDescending(t => t.UpdatedAt)
                .ToListAsync();

            const int pageSize = 15;
            page = Math.Max(1, page);

            var totalBroadcasts = await _context.AdminBroadcasts.CountAsync();
            var totalPages = (int)Math.Ceiling(totalBroadcasts / (double)pageSize);
            if (totalPages == 0) totalPages = 1;

            var rawBroadcasts = await _context.AdminBroadcasts
                .OrderByDescending(b => b.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var broadcastList = new List<BroadcastHistoryItemDto>();
            foreach (var b in rawBroadcasts)
            {
                var readCount = await _context.Notifications
                    .CountAsync(n => n.BroadcastId == b.Id && n.IsRead == true);

                broadcastList.Add(new BroadcastHistoryItemDto
                {
                    Id = b.Id,
                    Title = b.Title,
                    Content = b.Content,
                    Url = b.Url,
                    StyleJson = b.StyleJson,
                    TargetType = b.TargetType,
                    RecipientCount = b.RecipientCount,
                    ReadCount = readCount,
                    SendPush = b.SendPush,
                    Status = b.Status,
                    CreatedAt = b.CreatedAt,
                    CompletedAt = b.CompletedAt
                });
            }

            string? presetTitle = null;
            string? presetContent = null;
            string? presetUrl = null;
            string? presetStyleJson = null;

            if (reuseId.HasValue)
            {
                var rb = await _context.AdminBroadcasts.FindAsync(reuseId.Value);
                if (rb != null)
                {
                    presetTitle = rb.Title;
                    presetContent = rb.Content;
                    presetUrl = rb.Url;
                    presetStyleJson = rb.StyleJson;
                    tab = "send";
                }
            }
            else if (templateId.HasValue)
            {
                var tmpl = await _context.NotificationTemplates.FindAsync(templateId.Value);
                if (tmpl != null)
                {
                    presetTitle = tmpl.Title;
                    presetContent = tmpl.Content;
                    presetStyleJson = tmpl.StyleJson;
                    tab = "send";
                }
            }

            var totalActiveMembers = await _context.Users.CountAsync(u => u.IsActive != false);

            var vm = new AdminNotificationsViewModel
            {
                ActiveTab = tab,
                Templates = templates,
                Broadcasts = broadcastList,
                CurrentPage = page,
                TotalPages = totalPages,
                TotalBroadcasts = totalBroadcasts,
                PresetTitle = presetTitle,
                PresetContent = presetContent,
                PresetUrl = presetUrl,
                PresetStyleJson = presetStyleJson,
                TotalActiveMembers = totalActiveMembers
            };

            return View(vm);
        }

        // --- TEMPLATES CRUD ---

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateNotificationTemplate(string name, string title, string content, string? styleJson)
        {
            if (!await IsAdminAsync()) return Forbid();

            name = (name ?? "").Trim();
            title = (title ?? "").Trim();
            content = (content ?? "").Trim();

            if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(title) || string.IsNullOrEmpty(content))
            {
                TempData["ErrorMessage"] = "Vui lòng nhập đầy đủ Tên mẫu, Tiêu đề và Nội dung.";
                return RedirectToAction(nameof(Notifications), new { tab = "templates" });
            }

            if (title.Length > 150) title = title.Substring(0, 150);
            if (content.Length > 1000) content = content.Substring(0, 1000);

            var validatedStyleJson = SanitizeStyleJson(styleJson);

            var user = await _userManager.GetUserAsync(User);
            var tmpl = new NotificationTemplate
            {
                Name = name,
                Title = title,
                Content = content,
                StyleJson = validatedStyleJson,
                CreatedBy = user?.Id,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _context.NotificationTemplates.Add(tmpl);
            await _context.SaveChangesAsync();

            await LogAdminActionAsync("CreateNotificationTemplate", $"Tạo mẫu thông báo: {name}");
            TempData["SuccessMessage"] = "Đã lưu mẫu thiết kế thông báo mới thành công!";

            return RedirectToAction(nameof(Notifications), new { tab = "templates" });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteNotificationTemplate(int id)
        {
            if (!await IsAdminAsync()) return Forbid();

            var tmpl = await _context.NotificationTemplates.FindAsync(id);
            if (tmpl != null)
            {
                _context.NotificationTemplates.Remove(tmpl);
                await _context.SaveChangesAsync();
                await LogAdminActionAsync("DeleteNotificationTemplate", $"Xóa mẫu thông báo: {tmpl.Name}");
                TempData["SuccessMessage"] = "Đã xóa mẫu thông báo.";
            }

            return RedirectToAction(nameof(Notifications), new { tab = "templates" });
        }

        // --- SEARCH USERS (API FOR CHIP SELECTION) ---

        [HttpGet]
        public async Task<IActionResult> SearchUsersForNotification(string? q)
        {
            if (!await IsAdminAsync()) return Forbid();

            q = (q ?? "").Trim();
            if (string.IsNullOrEmpty(q) || q.Length < 2)
            {
                return Json(new List<object>());
            }

            var users = await _context.Users
                .AsNoTracking()
                .Where(u => u.IsActive != false &&
                           (EF.Functions.ILike(u.UserName!, $"%{q}%") ||
                            EF.Functions.ILike(u.Email!, $"%{q}%") ||
                            EF.Functions.ILike(u.FullName!, $"%{q}%")))
                .Take(20)
                .Select(u => new
                {
                    id = u.Id,
                    username = u.UserName,
                    fullName = u.FullName ?? u.UserName,
                    email = u.Email,
                    avatar = u.Avatar ?? "/images/nouser.png"
                })
                .ToListAsync();

            return Json(users);
        }

        // --- IMAGE UPLOAD ---

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UploadNotificationImage(IFormFile? file)
        {
            if (!await IsAdminAsync()) return Forbid();

            if (file == null || file.Length == 0)
            {
                return Json(new { success = false, message = "Không tìm thấy file ảnh." });
            }

            if (file.Length > 1024 * 1024)
            {
                return Json(new { success = false, message = "Dung lượng ảnh tối đa là 1MB." });
            }

            var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
            var allowedExts = new[] { ".jpg", ".jpeg", ".png", ".webp" };
            if (!allowedExts.Contains(ext))
            {
                return Json(new { success = false, message = "Chỉ chấp nhận file ảnh định dạng .jpg, .jpeg, .png, .webp." });
            }

            var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "images", "uploads", "notifications");
            if (!Directory.Exists(uploadsFolder))
            {
                Directory.CreateDirectory(uploadsFolder);
            }

            var fileName = $"{Guid.NewGuid():N}{ext}";
            var filePath = Path.Combine(uploadsFolder, fileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            var url = $"/images/uploads/notifications/{fileName}";
            return Json(new { success = true, url = url });
        }

        // --- SEND BROADCAST ---

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SendBroadcastNotification(
            string title,
            string content,
            string? url,
            string? styleJson,
            string targetType,
            string? selectedUserIds,
            bool sendPush = false)
        {
            if (!await IsAdminAsync()) return Forbid();

            title = (title ?? "").Trim();
            content = (content ?? "").Trim();
            url = (url ?? "").Trim();

            if (string.IsNullOrEmpty(title) || string.IsNullOrEmpty(content))
            {
                TempData["ErrorMessage"] = "Tiêu đề và nội dung thông báo không được để trống.";
                return RedirectToAction(nameof(Notifications), new { tab = "send" });
            }

            if (title.Length > 150) title = title.Substring(0, 150);
            if (content.Length > 1000) content = content.Substring(0, 1000);

            if (!string.IsNullOrEmpty(url))
            {
                if (!url.StartsWith("/") && !url.StartsWith("https://"))
                {
                    TempData["ErrorMessage"] = "Đường dẫn liên kết phải bắt đầu bằng / hoặc https://";
                    return RedirectToAction(nameof(Notifications), new { tab = "send" });
                }
            }
            else
            {
                url = null;
            }

            var validatedStyleJson = SanitizeStyleJson(styleJson);

            List<int>? userIds = null;
            if (targetType == "selected")
            {
                if (string.IsNullOrWhiteSpace(selectedUserIds))
                {
                    TempData["ErrorMessage"] = "Vui lòng chọn ít nhất một thành viên để gửi.";
                    return RedirectToAction(nameof(Notifications), new { tab = "send" });
                }

                userIds = selectedUserIds.Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Select(s => int.TryParse(s.Trim(), out int id) ? id : 0)
                    .Where(id => id > 0)
                    .Distinct()
                    .ToList();

                if (!userIds.Any())
                {
                    TempData["ErrorMessage"] = "Danh sách người nhận không hợp lệ.";
                    return RedirectToAction(nameof(Notifications), new { tab = "send" });
                }
            }

            var user = await _userManager.GetUserAsync(User);
            var broadcast = await _broadcastService.CreateAndEnqueueBroadcastAsync(
                title,
                content,
                validatedStyleJson,
                url,
                targetType,
                userIds,
                sendPush,
                user?.Id);

            await LogAdminActionAsync("SendBroadcastNotification", $"Gửi broadcast thông báo #{broadcast.Id}: '{title}' ({targetType})");

            TempData["SuccessMessage"] = targetType == "all"
                ? "Đã đưa thông báo vào hàng đợi gửi cho toàn bộ thành viên hệ thống!"
                : $"Đã đưa thông báo vào hàng đợi gửi cho {userIds?.Count ?? 0} thành viên đã chọn!";

            return RedirectToAction(nameof(Notifications), new { tab = "history" });
        }

        // --- REVOKE BROADCAST ---

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RevokeBroadcast(int id)
        {
            if (!await IsAdminAsync()) return Forbid();

            var success = await _broadcastService.RevokeBroadcastAsync(id);
            if (success)
            {
                await LogAdminActionAsync("RevokeBroadcast", $"Thu hồi thông báo #{id}");
                TempData["SuccessMessage"] = $"Đã thu hồi thành công thông báo #{id}. Các thông báo chưa đọc đã được xóa khỏi chuông người dùng.";
            }
            else
            {
                TempData["ErrorMessage"] = "Không tìm thấy thông báo để thu hồi.";
            }

            return RedirectToAction(nameof(Notifications), new { tab = "history" });
        }

        // --- HELPER: SANITIZE STYLE JSON ---

        private static string? SanitizeStyleJson(string? rawJson)
        {
            if (string.IsNullOrWhiteSpace(rawJson)) return null;

            try
            {
                using var doc = JsonDocument.Parse(rawJson);
                var root = doc.RootElement;
                if (root.ValueKind != JsonValueKind.Object) return null;

                var safeDto = new NotificationStyleDto();

                if (root.TryGetProperty("icon", out var iconProp) && iconProp.ValueKind == JsonValueKind.String)
                {
                    var iconVal = iconProp.GetString()?.Trim() ?? "";
                    if (AllowedIcons.Contains(iconVal))
                    {
                        safeDto.Icon = iconVal;
                    }
                }

                if (root.TryGetProperty("accent", out var accentProp) && accentProp.ValueKind == JsonValueKind.String)
                {
                    var accentVal = accentProp.GetString()?.Trim().ToLowerInvariant() ?? "";
                    if (AllowedAccents.Contains(accentVal))
                    {
                        safeDto.Accent = accentVal;
                    }
                }

                if (root.TryGetProperty("imageUrl", out var imgProp) && imgProp.ValueKind == JsonValueKind.String)
                {
                    var imgVal = imgProp.GetString()?.Trim() ?? "";
                    if (imgVal.StartsWith("https://") || imgVal.StartsWith("/images/uploads/notifications/"))
                    {
                        safeDto.ImageUrl = imgVal;
                    }
                }

                if (root.TryGetProperty("buttonText", out var btnProp) && btnProp.ValueKind == JsonValueKind.String)
                {
                    var btnVal = btnProp.GetString()?.Trim() ?? "";
                    if (btnVal.Length > 50) btnVal = btnVal.Substring(0, 50);
                    safeDto.ButtonText = btnVal;
                }

                if (root.TryGetProperty("url", out var urlProp) && urlProp.ValueKind == JsonValueKind.String)
                {
                    var urlVal = urlProp.GetString()?.Trim() ?? "";
                    if (urlVal.StartsWith("/") || urlVal.StartsWith("https://"))
                    {
                        safeDto.Url = urlVal;
                    }
                }

                if (root.TryGetProperty("badge", out var badgeProp) && badgeProp.ValueKind == JsonValueKind.String)
                {
                    var badgeVal = badgeProp.GetString()?.Trim() ?? "";
                    if (badgeVal.Length > 20) badgeVal = badgeVal.Substring(0, 20);
                    safeDto.Badge = badgeVal;
                }

                return JsonSerializer.Serialize(safeDto, new JsonSerializerOptions
                {
                    DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
                });
            }
            catch
            {
                return null;
            }
        }

        private static readonly HashSet<string> AllowedAccents = new(StringComparer.OrdinalIgnoreCase)
        {
            "primary", "secondary", "success", "danger", "warning", "info", "purple", "cyan"
        };

        private static readonly HashSet<string> AllowedIcons = new(StringComparer.OrdinalIgnoreCase)
        {
            "bi-bell-fill", "bi-stars", "bi-gift-fill", "bi-exclamation-triangle-fill", "bi-patch-check-fill",
            "bi-trophy-fill", "bi-ticket-perforated-fill", "bi-camera-reels-fill", "bi-palette-fill",
            "bi-info-circle-fill", "bi-heart-fill", "bi-fire", "bi-gem", "bi-shield-check",
            "bi-lightning-charge-fill", "bi-film", "bi-tv", "bi-megaphone-fill", "bi-chat-dots-fill",
            "bi-person-check-fill", "bi-award-fill", "bi-sparkles", "bi-rocket-takeoff-fill",
            "bi-broadcast", "bi-clock-history", "bi-bell", "bi-gear-fill", "bi-shield-lock-fill",
            "bi-check-circle-fill", "bi-hand-thumbs-up-fill"
        };
    }
}
