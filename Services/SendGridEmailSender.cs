using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.Extensions.Options;
using SendGrid;
using SendGrid.Helpers.Mail;
using Microsoft.AspNetCore.Identity;
using MovieWeb.Services.Interfaces;
using MovieWeb.Models.Entities;
using System.Net;

namespace MovieWeb.Services
{
    public class SendGridOptions
    {
        public string? ApiKey { get; set; }
        public string? SenderEmail { get; set; }
        public string? SenderName { get; set; }
    }

    public class SendGridEmailSender : IEmailSender, IEmailService, IEmailSender<User>
    {
        private readonly ILogger _logger;
        private readonly SendGridOptions _options;

        public SendGridEmailSender(
            IOptions<SendGridOptions> optionsAccessor,
            ILogger<SendGridEmailSender> logger)
        {
            _options = optionsAccessor.Value;
            _logger = logger;
        }

        public async Task SendEmailAsync(string email, string subject, string htmlMessage)
        {
            if (string.IsNullOrEmpty(_options.ApiKey))
            {
                _logger.LogError("❌ SendGrid:ApiKey chưa được cấu hình!");
                throw new Exception("Lỗi: SendGrid:ApiKey chưa được cấu hình.");
            }

            // ✅ TẠO CLIENT TRỰC TIẾP TỪ API KEY
            var client = new SendGridClient(_options.ApiKey);

            var fromEmail = _options.SenderEmail ?? "noreply@yourdomain.com";
            var fromName = _options.SenderName ?? "MovieWeb";

            _logger.LogInformation($"🔹 Chuẩn bị gửi email:");
            _logger.LogInformation($"   - From: {fromEmail} ({fromName})");
            _logger.LogInformation($"   - To: {email}");
            _logger.LogInformation($"   - Subject: {subject}");

            var msg = new SendGridMessage()
            {
                From = new EmailAddress(fromEmail, fromName),
                Subject = subject,
                PlainTextContent = StripHtml(htmlMessage),
                HtmlContent = htmlMessage
            };
            msg.AddTo(new EmailAddress(email));

            var response = await client.SendEmailAsync(msg);

            if (response.StatusCode == HttpStatusCode.Accepted ||
                response.StatusCode == HttpStatusCode.OK)
            {
                _logger.LogInformation($"✅ [SendGrid] Gửi email tới {email} THÀNH CÔNG!");
            }
            else
            {
                var responseBody = await response.Body.ReadAsStringAsync();
                _logger.LogError($"❌ [SendGrid] Gửi email THẤT BẠI!");
                _logger.LogError($"   - Status Code: {response.StatusCode}");
                _logger.LogError($"   - Response: {responseBody}");

                throw new Exception($"SendGrid failed: {response.StatusCode} - {responseBody}");
            }
        }

        // =================================================================
        // EMAIL XÁC THỰC TÀI KHOẢN (TEMPLATE ĐẸP)
        // =================================================================
        public async Task SendEmailConfirmationAsync(string email, string subject, string confirmationLink)
        {
            // Lấy username từ email (hoặc có thể truyền vào nếu cần)
            var userName = email.Split('@')[0];

            var htmlBody = $@"
            <div style='font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto; padding: 20px;'>
                <div style='text-align: center; margin-bottom: 30px;'>
                    <h1 style='color: #333; margin-bottom: 10px;'>MoonPhim</h1>
                    <p style='color: #666; font-size: 16px;'>Bay bổng cùng điện ảnh</p>
                </div>
                
                <div style='background: #f8f9fa; padding: 30px; border-radius: 10px; border-left: 4px solid #007bff;'>
                    <h2 style='color: #333; margin-bottom: 20px;'>Chào mừng {userName}!</h2>
                    <p style='color: #555; font-size: 16px; line-height: 1.6; margin-bottom: 20px;'>
                        Cảm ơn bạn đã đăng ký tài khoản tại MoonPhim. Để hoàn tất quá trình đăng ký, 
                        vui lòng nhấn vào nút bên dưới để xác thực email của bạn.
                    </p>
                    
                    <div style='text-align: center; margin: 30px 0;'>
                        <a href='{confirmationLink}' 
                           style='background: linear-gradient(45deg, #007bff, #0056b3); 
                                  color: white; 
                                  padding: 15px 30px; 
                                  text-decoration: none; 
                                  border-radius: 25px; 
                                  font-weight: bold; 
                                  font-size: 16px;
                                  display: inline-block;'>
                            Xác thực tài khoản
                        </a>
                    </div>
                    
                    <p style='color: #777; font-size: 14px; margin-top: 20px;'>
                        Nếu bạn không thể nhấn vào nút trên, hãy copy và paste link sau vào trình duyệt:
                    </p>
                    <p style='background: #e9ecef; padding: 10px; border-radius: 5px; word-break: break-all; font-size: 12px;'>
                        {confirmationLink}
                    </p>
                </div>
                
                <div style='text-align: center; margin-top: 30px; color: #999; font-size: 12px;'>
                    <p>© 2025 MoonPhim. All rights reserved.</p>
                    <p>Nếu bạn không đăng ký tài khoản này, vui lòng bỏ qua email này.</p>
                </div>
            </div>";

            await SendEmailAsync(email, "Xác thực tài khoản MoonPhim", htmlBody);
        }

        // =================================================================
        // EMAIL ĐẶT LẠI MẬT KHẨU (TEMPLATE ĐẸP)
        // =================================================================
        public async Task SendPasswordResetAsync(string email, string subject, string resetLink)
        {
            var userName = email.Split('@')[0];

            var htmlBody = $@"
            <div style='font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto; padding: 20px;'>
                <div style='text-align: center; margin-bottom: 30px;'>
                    <h1 style='color: #333; margin-bottom: 10px;'>MoonPhim</h1>
                    <p style='color: #666; font-size: 16px;'>Bay bổng cùng điện ảnh</p>
                </div>
                
                <div style='background: #f8f9fa; padding: 30px; border-radius: 10px; border-left: 4px solid #dc3545;'>
                    <h2 style='color: #333; margin-bottom: 20px;'>Đặt lại mật khẩu</h2>
                    <p style='color: #555; font-size: 16px; line-height: 1.6; margin-bottom: 20px;'>
                        Chào {userName},<br><br>
                        Chúng tôi nhận được yêu cầu đặt lại mật khẩu cho tài khoản của bạn. 
                        Nếu bạn đã yêu cầu điều này, vui lòng nhấn vào nút bên dưới để đặt lại mật khẩu.
                    </p>
                    
                    <div style='text-align: center; margin: 30px 0;'>
                        <a href='{resetLink}' 
                           style='background: linear-gradient(45deg, #dc3545, #c82333); 
                                  color: white; 
                                  padding: 15px 30px; 
                                  text-decoration: none; 
                                  border-radius: 25px; 
                                  font-weight: bold; 
                                  font-size: 16px;
                                  display: inline-block;'>
                            Đặt lại mật khẩu
                        </a>
                    </div>
                    
                    <p style='color: #777; font-size: 14px; margin-top: 20px;'>
                        Link này sẽ hết hạn sau 24 giờ vì lý do bảo mật.
                    </p>
                    
                    <p style='color: #777; font-size: 14px; margin-top: 10px;'>
                        Nếu bạn không thể nhấn vào nút trên, hãy copy và paste link sau vào trình duyệt:
                    </p>
                    <p style='background: #e9ecef; padding: 10px; border-radius: 5px; word-break: break-all; font-size: 12px;'>
                        {resetLink}
                    </p>
                </div>
                
                <div style='text-align: center; margin-top: 30px; color: #999; font-size: 12px;'>
                    <p>© 2025 MoonPhim. All rights reserved.</p>
                    <p>Nếu bạn không yêu cầu đặt lại mật khẩu, vui lòng bỏ qua email này.</p>
                </div>
            </div>";

            await SendEmailAsync(email, "Đặt lại mật khẩu MoonPhim", htmlBody);
        }

        // =================================================================
        // EMAIL CHÀO MỪNG (TEMPLATE ĐẸP)
        // =================================================================
        public async Task SendWelcomeEmailAsync(string email, string username)
        {
            var htmlBody = $@"
            <div style='font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto; padding: 20px;'>
                <div style='text-align: center; margin-bottom: 30px;'>
                    <h1 style='color: #333; margin-bottom: 10px;'>MoonPhim</h1>
                    <p style='color: #666; font-size: 16px;'>Bay bổng cùng điện ảnh</p>
                </div>
                
                <div style='background: #f8f9fa; padding: 30px; border-radius: 10px; border-left: 4px solid #28a745;'>
                    <h2 style='color: #333; margin-bottom: 20px;'>Chào mừng {username}!</h2>
                    <p style='color: #555; font-size: 16px; line-height: 1.6; margin-bottom: 20px;'>
                        Chúc mừng bạn đã trở thành thành viên của MoonPhim! Bây giờ bạn có thể:
                    </p>
                    
                    <ul style='color: #555; font-size: 16px; line-height: 1.8; margin-bottom: 20px;'>
                        <li>Xem hàng ngàn bộ phim chất lượng cao</li>
                        <li>Lưu phim yêu thích vào danh sách cá nhân</li>
                        <li>Theo dõi lịch sử xem phim</li>
                        <li>Đánh giá và bình luận phim</li>
                        <li>Nhận thông báo khi có phim mới</li>
                    </ul>
                    
                    <div style='text-align: center; margin: 30px 0;'>
                        <a href='https://moonphim.com' 
                           style='background: linear-gradient(45deg, #28a745, #20c997); 
                                  color: white; 
                                  padding: 15px 30px; 
                                  text-decoration: none; 
                                  border-radius: 25px; 
                                  font-weight: bold; 
                                  font-size: 16px;
                                  display: inline-block;'>
                            Bắt đầu xem phim
                        </a>
                    </div>
                </div>
                
                <div style='text-align: center; margin-top: 30px; color: #999; font-size: 12px;'>
                    <p>© 2025 MoonPhim. All rights reserved.</p>
                    <p>Cảm ơn bạn đã tin tưởng và lựa chọn MoonPhim!</p>
                </div>
            </div>";

            await SendEmailAsync(email, "Chào mừng bạn đến với MoonPhim", htmlBody);
        }

        // =================================================================
        // EMAIL GỬI OTP XÁC THỰC SINH VIÊN (TEMPLATE ĐẸP)
        // =================================================================
        public async Task SendStudentEmailOtpAsync(string email, string username, string otp)
        {
            var htmlBody = $@"
            <div style='font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto; padding: 20px;'>
                <div style='text-align: center; margin-bottom: 30px;'>
                    <h1 style='color: #333; margin-bottom: 10px;'>MoonPhim</h1>
                    <p style='color: #666; font-size: 16px;'>Bay bổng cùng điện ảnh</p>
                </div>
                
                <div style='background: #f8f9fa; padding: 30px; border-radius: 10px; border-left: 4px solid #007bff;'>
                    <h2 style='color: #333; margin-bottom: 20px;'>Xác thực Email sinh viên</h2>
                    <p style='color: #555; font-size: 16px; line-height: 1.6; margin-bottom: 20px;'>
                        Chào {username},<br><br>
                        Bạn đang thực hiện xác thực email sinh viên để nâng cấp lên gói <strong>Student</strong>.
                        Vui lòng sử dụng mã OTP bên dưới để hoàn tất quá trình xác thực.
                    </p>
                    
                    <div style='text-align: center; margin: 30px 0;'>
                        <div style='background-color: #007bff;
                                    color: white; 
                                    padding: 20px; 
                                    border-radius: 10px; 
                                    display: inline-block;
                                    font-size: 32px;
                                    font-weight: bold;
                                    letter-spacing: 8px;
                                    font-family: monospace;'>
                            {otp}
                        </div>
                    </div>
                    
                    <p style='color: #777; font-size: 14px; margin-top: 20px; text-align: center;'>
                        ⏰ Mã OTP này có hiệu lực trong <strong>5 phút</strong>
                    </p>
                    
                    <div style='background: #fff3cd; border: 1px solid #ffc107; padding: 15px; border-radius: 5px; margin-top: 20px;'>
                        <p style='color: #856404; font-size: 14px; margin: 0;'>
                            <strong>⚠️ Lưu ý:</strong> Không chia sẻ mã OTP này với bất kỳ ai. 
                            MoonPhim sẽ không bao giờ yêu cầu bạn cung cấp mã OTP qua điện thoại hoặc email.
                        </p>
                    </div>
                </div>
                
                <div style='text-align: center; margin-top: 30px; color: #999; font-size: 12px;'>
                    <p>© 2025 MoonPhim. All rights reserved.</p>
                    <p>Nếu bạn không thực hiện yêu cầu này, vui lòng bỏ qua email này.</p>
                </div>
            </div>";

            await SendEmailAsync(email, "Mã xác thực Email sinh viên - MoonPhim", htmlBody);
        }

        // =================================================================
        // CÁC HÀM HỖ TRỢ CHO IEmailSender<User>
        // =================================================================
        public Task SendConfirmationLinkAsync(User user, string email, string confirmationLink)
        {
            return SendEmailConfirmationAsync(email, "Xác nhận email của bạn", confirmationLink);
        }

        public Task SendPasswordResetLinkAsync(User user, string email, string resetLink)
        {
            return SendPasswordResetAsync(email, "Đặt lại mật khẩu của bạn", resetLink);
        }

        public Task SendPasswordResetCodeAsync(User user, string email, string resetCode)
        {
            string subject = "Mã đặt lại mật khẩu";
            string message = $@"
            <div style='font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto; padding: 20px;'>
                <h2>Mã đặt lại mật khẩu của bạn</h2>
                <p>Mã của bạn là: <strong style='font-size: 24px; color: #007bff;'>{resetCode}</strong></p>
                <p>Mã này có hiệu lực trong 15 phút.</p>
            </div>";
            return SendEmailAsync(email, subject, message);
        }

        // =================================================================
        // HÀM HELPER - XÓA HTML TAGS ĐỂ TẠO PLAIN TEXT
        // =================================================================
        private string StripHtml(string html)
        {
            if (string.IsNullOrEmpty(html)) return string.Empty;
            return System.Text.RegularExpressions.Regex.Replace(html, "<.*?>", string.Empty);
        }
    }
}