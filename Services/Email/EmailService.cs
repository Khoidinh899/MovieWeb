using Microsoft.Extensions.Options;
using System.Net;
using System.Net.Mail;
using MovieWeb.Models;

namespace MovieWeb.Services
{
    public class EmailService : IEmailService
    {
        private readonly EmailSettings _emailSettings;
        private readonly ILogger<EmailService> _logger;

        public EmailService(IOptions<EmailSettings> emailSettings, ILogger<EmailService> logger)
        {
            _emailSettings = emailSettings.Value;
            _logger = logger;
        }

        public async Task SendEmailAsync(string toEmail, string subject, string htmlBody)
        {
            try
            {
                using var smtpClient = new SmtpClient(_emailSettings.SmtpServer, _emailSettings.Port)
                {
                    Credentials = new NetworkCredential(_emailSettings.Username, _emailSettings.Password),
                    EnableSsl = _emailSettings.EnableSsl
                };

                var mailMessage = new MailMessage
                {
                    From = new MailAddress(_emailSettings.FromEmail, _emailSettings.FromName),
                    Subject = subject,
                    Body = htmlBody,
                    IsBodyHtml = true
                };

                mailMessage.To.Add(toEmail);

                await smtpClient.SendMailAsync(mailMessage);
                _logger.LogInformation($"Email sent successfully to {toEmail}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to send email to {toEmail}");
                throw;
            }
        }

        public async Task SendEmailConfirmationAsync(string toEmail, string userName, string confirmationLink)
        {
            var subject = "Xác thực tài khoản MoonPhim";
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

            await SendEmailAsync(toEmail, subject, htmlBody);
        }

        public async Task SendPasswordResetAsync(string toEmail, string userName, string resetLink)
        {
            var subject = "Đặt lại mật khẩu MoonPhim";
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

            await SendEmailAsync(toEmail, subject, htmlBody);
        }

        public async Task SendWelcomeEmailAsync(string toEmail, string userName)
        {
            var subject = "Chào mừng bạn đến với MoonPhim";
            var htmlBody = $@"
            <div style='font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto; padding: 20px;'>
                <div style='text-align: center; margin-bottom: 30px;'>
                    <h1 style='color: #333; margin-bottom: 10px;'>MoonPhim</h1>
                    <p style='color: #666; font-size: 16px;'>Bay bổng cùng điện ảnh</p>
                </div>
                
                <div style='background: #f8f9fa; padding: 30px; border-radius: 10px; border-left: 4px solid #28a745;'>
                    <h2 style='color: #333; margin-bottom: 20px;'>Chào mừng {userName}!</h2>
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

            await SendEmailAsync(toEmail, subject, htmlBody);
        }
        public async Task SendStudentEmailOtpAsync(string toEmail, string userName, string otpCode)
        {
            var subject = "Mã xác thực Email sinh viên - MoonPhim";
            var htmlBody = $@"
    <div style='font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto; padding: 20px;'>
        <div style='text-align: center; margin-bottom: 30px;'>
            <h1 style='color: #333; margin-bottom: 10px;'>MoonPhim</h1>
            <p style='color: #666; font-size: 16px;'>Bay bổng cùng điện ảnh</p>
        </div>
        
        <div style='background: #f8f9fa; padding: 30px; border-radius: 10px; border-left: 4px solid #007bff;'>
            <h2 style='color: #333; margin-bottom: 20px;'>Xác thực Email sinh viên</h2>
            <p style='color: #555; font-size: 16px; line-height: 1.6; margin-bottom: 20px;'>
                Chào {userName},<br><br>
                Bạn đang thực hiện xác thực email sinh viên để nâng cấp lên gói <strong>Student</strong>.
                Vui lòng sử dụng mã OTP bên dưới để hoàn tất quá trình xác thực.
            </p>
            
            <div style='text-align: center; margin: 30px 0;'>
                <div style='background-color: #007bff; /* Màu xanh biển của Bootstrap */
                            color: white; 
                            padding: 20px; 
                            border-radius: 10px; 
                            display: inline-block;
                            font-size: 32px;
                            font-weight: bold;
                            letter-spacing: 8px;
                            font-family: monospace;'>
                    {otpCode}
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

            await SendEmailAsync(toEmail, subject, htmlBody);
        }
    }
}