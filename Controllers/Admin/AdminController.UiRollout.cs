using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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
        // 🎨 QUẢN LÝ ROLLOUT GIAO DIỆN & KHẢO SÁT USER (UI ROLLOUT)
        // ========================================================

        [HttpGet]
        public async Task<IActionResult> UiRollout(string filter = "all", int page = 1)
        {
            if (!await IsAdminAsync())
            {
                return Forbid();
            }

            var rolloutSetting = await _siteSettingsService.GetAsync<UiRolloutSetting>(Helpers.UiVersion.SettingKey) ?? new UiRolloutSetting();

            var totalUsers = await _context.Users.CountAsync(u => u.IsActive != false);
            var v2Users = await _context.UserUiPreferences.CountAsync(p => p.Ui == "v2");
            var v1Users = await _context.UserUiPreferences.CountAsync(p => p.Ui == "v1");
            var unpickedUsers = Math.Max(0, totalUsers - v2Users - v1Users);

            double v2Pct = totalUsers > 0 ? Math.Round((double)v2Users * 100 / totalUsers, 1) : 0;
            double v1Pct = totalUsers > 0 ? Math.Round((double)v1Users * 100 / totalUsers, 1) : 0;
            double unpickedPct = totalUsers > 0 ? Math.Round((double)unpickedUsers * 100 / totalUsers, 1) : 0;

            var revertedUsers = await _context.UserUiPreferences.CountAsync(p => p.Ui == "v1" && p.SwitchCount >= 1);
            var totalSwitches = await _context.UserUiPreferences.SumAsync(p => (int?)p.SwitchCount) ?? 0;

            // Survey Stats
            var surveyNew = await _context.UiFeedbacks.CountAsync(f => f.Choice == "new");
            var surveyOld = await _context.UiFeedbacks.CountAsync(f => f.Choice == "old");
            var surveySame = await _context.UiFeedbacks.CountAsync(f => f.Choice == "same");
            var surveyTotal = surveyNew + surveyOld + surveySame;

            double sNewPct = surveyTotal > 0 ? Math.Round((double)surveyNew * 100 / surveyTotal, 1) : 0;
            double sOldPct = surveyTotal > 0 ? Math.Round((double)surveyOld * 100 / surveyTotal, 1) : 0;
            double sSamePct = surveyTotal > 0 ? Math.Round((double)surveySame * 100 / surveyTotal, 1) : 0;

            // Broadcast conversions (lấy các broadcast gần đây có liên quan đến mời dùng thử giao diện)
            var invitationBroadcasts = await _context.AdminBroadcasts
                .Where(b => b.Url != null && b.Url.Contains("giao-dien"))
                .OrderByDescending(b => b.CreatedAt)
                .Take(5)
                .ToListAsync();

            var broadcastConversions = new List<BroadcastConversionDto>();
            foreach (var b in invitationBroadcasts)
            {
                var readCount = await _context.Notifications.CountAsync(n => n.BroadcastId == b.Id && n.IsRead == true);
                var recipientUserIds = await _context.Notifications
                    .Where(n => n.BroadcastId == b.Id)
                    .Select(n => n.UserId)
                    .ToListAsync();

                var convertedCount = await _context.UserUiPreferences
                    .CountAsync(p => recipientUserIds.Contains(p.UserId) && p.Ui == "v2" && p.UpdatedAt >= b.CreatedAt);

                broadcastConversions.Add(new BroadcastConversionDto
                {
                    BroadcastId = b.Id,
                    Title = b.Title,
                    CreatedAt = b.CreatedAt,
                    RecipientCount = b.RecipientCount,
                    ReadCount = readCount,
                    ReadRate = b.RecipientCount > 0 ? Math.Round((double)readCount * 100 / b.RecipientCount, 1) : 0,
                    ConvertedToV2Count = convertedCount,
                    ConversionRate = b.RecipientCount > 0 ? Math.Round((double)convertedCount * 100 / b.RecipientCount, 1) : 0
                });
            }

            // Feedbacks pagination
            var query = _context.UiFeedbacks.Include(f => f.User).AsQueryable();
            if (filter == "new" || filter == "old" || filter == "same")
            {
                query = query.Where(f => f.Choice == filter);
            }

            var totalFeedbacks = await query.CountAsync();
            const int pageSize = 20;
            page = Math.Max(1, page);
            var totalPages = (int)Math.Ceiling(totalFeedbacks / (double)pageSize);
            if (totalPages == 0) totalPages = 1;
            if (page > totalPages) page = totalPages;

            var feedbackList = await query
                .OrderByDescending(f => f.UpdatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(f => new UiFeedbackItemDto
                {
                    UserId = f.UserId,
                    Username = f.User != null ? f.User.UserName ?? "" : "",
                    FullName = f.User != null ? (f.User.FullName ?? f.User.UserName ?? "") : "",
                    Email = f.User != null ? f.User.Email : null,
                    Choice = f.Choice,
                    Comment = f.Comment,
                    UiAtVote = f.UiAtVote,
                    CreatedAt = f.CreatedAt,
                    UpdatedAt = f.UpdatedAt
                })
                .ToListAsync();

            var viewModel = new AdminUiRolloutViewModel
            {
                CurrentMode = rolloutSetting.Mode,
                ModeUpdatedAt = rolloutSetting.UpdatedAt,
                TotalUsers = totalUsers,
                V2Users = v2Users,
                V1Users = v1Users,
                UnpickedUsers = unpickedUsers,
                V2Percent = v2Pct,
                V1Percent = v1Pct,
                UnpickedPercent = unpickedPct,
                RevertedUsers = revertedUsers,
                TotalSwitches = totalSwitches,

                SurveyTotal = surveyTotal,
                SurveyNew = surveyNew,
                SurveyOld = surveyOld,
                SurveySame = surveySame,
                SurveyNewPercent = sNewPct,
                SurveyOldPercent = sOldPct,
                SurveySamePercent = sSamePct,

                BroadcastConversions = broadcastConversions,
                Feedbacks = feedbackList,
                CurrentPage = page,
                TotalPages = totalPages,
                TotalFeedbacks = totalFeedbacks,
                SelectedFilter = filter
            };

            return View(viewModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateRolloutMode(string mode)
        {
            if (!await IsAdminAsync())
            {
                return Forbid();
            }

            mode = (mode ?? "").Trim().ToLowerInvariant();
            if (mode != "off" && mode != "members" && mode != "all")
            {
                TempData["ErrorMessage"] = "Chế độ rollout không hợp lệ.";
                return RedirectToAction(nameof(UiRollout));
            }

            int? currentUserId = null;
            var user = await _userManager.GetUserAsync(User);
            if (user != null)
            {
                currentUserId = user.Id;
            }

            var setting = new UiRolloutSetting
            {
                Mode = mode,
                UpdatedAt = DateTime.UtcNow,
                UpdatedBy = currentUserId
            };

            await _siteSettingsService.SetAsync(Helpers.UiVersion.SettingKey, setting);

            string modeDesc = mode switch
            {
                "all" => "Tất cả mọi người (Khách & Thành viên mặc định V2)",
                "members" => "Thành viên dùng thử (Khách V1, Thành viên tự chọn)",
                _ => "Tắt (Mọi người dùng V1)"
            };

            await LogAdminActionAsync("UpdateUiRolloutMode", $"Thay đổi chế độ giao diện sang '{mode}' ({modeDesc})");
            TempData["SuccessMessage"] = $"Đã cập nhật chế độ giao diện thành công: {modeDesc}";

            return RedirectToAction(nameof(UiRollout));
        }

        [HttpGet]
        public async Task<IActionResult> ExportFeedbackCsv()
        {
            if (!await IsAdminAsync())
            {
                return Forbid();
            }

            var feedbacks = await _context.UiFeedbacks
                .Include(f => f.User)
                .OrderByDescending(f => f.UpdatedAt)
                .ToListAsync();

            var sb = new StringBuilder();
            // UTF-8 BOM
            sb.Append('\uFEFF');
            sb.AppendLine("UserId,Username,FullName,Email,Choice,UiAtVote,Comment,CreatedAt,UpdatedAt");

            foreach (var f in feedbacks)
            {
                string username = EscapeCsv(f.User?.UserName ?? "");
                string fullName = EscapeCsv(f.User?.FullName ?? "");
                string email = EscapeCsv(f.User?.Email ?? "");
                string choice = EscapeCsv(f.Choice);
                string uiAtVote = EscapeCsv(f.UiAtVote);
                string comment = EscapeCsv(f.Comment ?? "");
                string createdAt = f.CreatedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");
                string updatedAt = f.UpdatedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");

                sb.AppendLine($"{f.UserId},{username},{fullName},{email},{choice},{uiAtVote},{comment},{createdAt},{updatedAt}");
            }

            var bytes = Encoding.UTF8.GetBytes(sb.ToString());
            return File(bytes, "text/csv", $"ui-feedback-{DateTime.Now:yyyyMMdd-HHmmss}.csv");
        }

        private static string EscapeCsv(string value)
        {
            if (string.IsNullOrEmpty(value)) return "\"\"";
            if (value.Contains(",") || value.Contains("\"") || value.Contains("\n") || value.Contains("\r"))
            {
                return "\"" + value.Replace("\"", "\"\"") + "\"";
            }
            return "\"" + value + "\"";
        }
    }
}
