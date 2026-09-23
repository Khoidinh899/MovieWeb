using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MovieWeb.Data;
using MovieWeb.Models.Entities;
using MovieWeb.Services;
using System.Text.Encodings.Web;

namespace MovieWeb.Controllers
{
    [Route("api/copyright")]
    [ApiController]
    public class CopyrightController : ControllerBase
    {
        private readonly MovieWebDbContext _context;
        private readonly IEmailService _emailService;
        private readonly ILogger<CopyrightController> _logger;

        private const string ADMIN_EMAIL = "khoidinh899@gmail.com";

        public CopyrightController(
            MovieWebDbContext context,
            IEmailService emailService,
            ILogger<CopyrightController> logger)
        {
            _context = context;
            _emailService = emailService;
            _logger = logger;
        }

        [HttpPost("report")]
        public async Task<IActionResult> SubmitReport([FromForm] CopyrightReportDto dto)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(dto.FullName) ||
                    string.IsNullOrWhiteSpace(dto.Email) ||
                    string.IsNullOrWhiteSpace(dto.MovieUrl) ||
                    string.IsNullOrWhiteSpace(dto.ProofDetails))
                {
                    return BadRequest(new { success = false, message = "Vui lòng điền đầy đủ các thông tin bắt buộc." });
                }

                var report = new CopyrightReport
                {
                    FullName = dto.FullName.Trim(),
                    Email = dto.Email.Trim(),
                    Organization = dto.Organization?.Trim(),
                    MovieUrl = dto.MovieUrl.Trim(),
                    MovieTitle = dto.MovieTitle?.Trim(),
                    ProofDetails = dto.ProofDetails.Trim(),
                    Status = "Pending",
                    CreatedAt = DateTime.Now
                };

                _context.CopyrightReports.Add(report);
                await _context.SaveChangesAsync();

                // Gửi email thông báo khẩn tới Admin (khoidinh899@gmail.com)
                try
                {
                    string safeFullName = HtmlEncoder.Default.Encode(report.FullName);
                    string safeEmail = HtmlEncoder.Default.Encode(report.Email);
                    string safeOrg = HtmlEncoder.Default.Encode(report.Organization ?? "Cá nhân");
                    string safeUrl = HtmlEncoder.Default.Encode(report.MovieUrl);
                    string safeTitle = HtmlEncoder.Default.Encode(report.MovieTitle ?? "Không xác định");
                    string safeDetails = HtmlEncoder.Default.Encode(report.ProofDetails).Replace("\n", "<br/>");

                    string subject = $"[DMCA REPORT] Báo cáo vi phạm bản quyền - {report.MovieTitle ?? "MoonPhim"}";
                    string htmlBody = $@"
                        <div style='font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto; padding: 20px; border: 1px solid #e0e0e0; border-radius: 8px;'>
                            <h2 style='color: #dc2626; border-bottom: 2px solid #dc2626; padding-bottom: 10px;'>
                                🚨 BÁO CÁO VI PHẠM BẢN QUYỀN MỚI
                            </h2>
                            <p>Hệ thống MoonPhim vừa nhận được một yêu cầu báo cáo vi phạm bản quyền (DMCA Notice) từ người dùng:</p>

                            <table style='width: 100%; border-collapse: collapse; margin-top: 15px;'>
                                <tr>
                                    <td style='padding: 8px; font-weight: bold; background: #f8fafc; width: 35%;'>Mã báo cáo:</td>
                                    <td style='padding: 8px;'>#{report.ReportId}</td>
                                </tr>
                                <tr>
                                    <td style='padding: 8px; font-weight: bold; background: #f8fafc;'>Họ và tên:</td>
                                    <td style='padding: 8px;'>{safeFullName}</td>
                                </tr>
                                <tr>
                                    <td style='padding: 8px; font-weight: bold; background: #f8fafc;'>Email liên hệ:</td>
                                    <td style='padding: 8px;'><a href='mailto:{safeEmail}'>{safeEmail}</a></td>
                                </tr>
                                <tr>
                                    <td style='padding: 8px; font-weight: bold; background: #f8fafc;'>Tổ chức/Doanh nghiệp:</td>
                                    <td style='padding: 8px;'>{safeOrg}</td>
                                </tr>
                                <tr>
                                    <td style='padding: 8px; font-weight: bold; background: #f8fafc;'>Phim bị báo cáo:</td>
                                    <td style='padding: 8px;'>{safeTitle}</td>
                                </tr>
                                <tr>
                                    <td style='padding: 8px; font-weight: bold; background: #f8fafc;'>URL vi phạm:</td>
                                    <td style='padding: 8px;'><a href='{safeUrl}' target='_blank'>{safeUrl}</a></td>
                                </tr>
                                <tr>
                                    <td style='padding: 8px; font-weight: bold; background: #f8fafc;'>Thời gian gửi:</td>
                                    <td style='padding: 8px;'>{report.CreatedAt:dd/MM/yyyy HH:mm:ss}</td>
                                </tr>
                            </table>

                            <div style='margin-top: 20px; padding: 15px; background: #fff1f2; border-left: 4px solid #e11d48;'>
                                <h4 style='margin-top: 0; color: #9f1239;'>📌 Chi tiết bằng chứng & Yêu cầu:</h4>
                                <p style='margin-bottom: 0; white-space: pre-wrap;'>{safeDetails}</p>
                            </div>

                            <div style='margin-top: 25px; text-align: center;'>
                                <a href='https://moonphim.me/Admin/Movies' style='background: #2563eb; color: #ffffff; padding: 10px 20px; text-decoration: none; border-radius: 5px; font-weight: bold;'>
                                    TRUY CẬP TRANG QUẢN TRỊ ĐỂ XỬ LÝ
                                </a>
                            </div>

                            <hr style='margin-top: 30px; border: none; border-top: 1px solid #e0e0e0;' />
                            <p style='font-size: 12px; color: #64748b; text-align: center;'>
                                Email tự động được gửi từ hệ thống MoonPhim DMCA Protection System.
                            </p>
                        </div>";

                    await _emailService.SendEmailAsync(ADMIN_EMAIL, subject, htmlBody);
                    _logger.LogInformation("✅ Đã gửi email báo cáo DMCA #{ReportId} tới {AdminEmail}", report.ReportId, ADMIN_EMAIL);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "❌ Lỗi khi gửi email DMCA cho Admin");
                }

                return Ok(new
                {
                    success = true,
                    message = "Báo cáo vi phạm bản quyền của bạn đã được gửi thành công. Ban quản trị MoonPhim sẽ xem xét và phản hồi qua email trong vòng 24-48 giờ làm việc."
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Lỗi khi xử lý báo cáo vi phạm bản quyền");
                return StatusCode(500, new { success = false, message = "Có lỗi xảy ra trên hệ thống. Vui lòng thử lại sau." });
            }
        }
    }

    public class CopyrightReportDto
    {
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string? Organization { get; set; }
        public string MovieUrl { get; set; } = string.Empty;
        public string? MovieTitle { get; set; }
        public string ProofDetails { get; set; } = string.Empty;
    }
}
