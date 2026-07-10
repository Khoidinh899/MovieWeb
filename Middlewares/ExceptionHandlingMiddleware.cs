using System.Net;
using System.Text.Json;

namespace MovieWeb.Middlewares
{
    /// <summary>
    /// Global Exception Handling Middleware - Bắt mọi exception trong toàn bộ request pipeline
    /// Middleware này sẽ catch những lỗi mà ExceptionFilter không bắt được
    /// (ví dụ: lỗi trong Middleware khác, lỗi trước khi đến Controller)
    /// </summary>
    public class ExceptionHandlingMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<ExceptionHandlingMiddleware> _logger;
        private readonly IWebHostEnvironment _env;

        public ExceptionHandlingMiddleware(
            RequestDelegate next,
            ILogger<ExceptionHandlingMiddleware> logger,
            IWebHostEnvironment env)
        {
            _next = next;
            _logger = logger;
            _env = env;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                await _next(context);
            }
            catch (Exception ex)
            {
                await HandleExceptionAsync(context, ex);
            }
        }

        private async Task HandleExceptionAsync(HttpContext context, Exception exception)
        {
            // Log exception với thông tin chi tiết
            LogException(context, exception);

            // Xác định status code
            var statusCode = GetStatusCode(exception);
            context.Response.StatusCode = (int)statusCode;

            // Kiểm tra xem có phải API request không
            if (IsApiRequest(context.Request))
            {
                await HandleApiException(context, exception, statusCode);
            }
            else
            {
                await HandleWebException(context, exception, statusCode);
            }
        }

        /// <summary>
        /// Xử lý exception cho API - Trả về JSON
        /// </summary>
        private async Task HandleApiException(HttpContext context, Exception exception, HttpStatusCode statusCode)
        {
            context.Response.ContentType = "application/json";

            var response = new
            {
                success = false,
                message = GetUserFriendlyMessage(exception),
                error = _env.IsDevelopment() ? exception.Message : null,
                stackTrace = _env.IsDevelopment() ? exception.StackTrace : null,
                timestamp = DateTime.Now,
                path = context.Request.Path.Value,
                statusCode = (int)statusCode
            };

            var options = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = _env.IsDevelopment()
            };

            await context.Response.WriteAsync(JsonSerializer.Serialize(response, options));
        }

        /// <summary>
        /// Xử lý exception cho Web - Redirect đến trang lỗi
        /// </summary>
        private async Task HandleWebException(HttpContext context, Exception exception, HttpStatusCode statusCode)
        {
            // Lưu thông tin lỗi vào Items để trang Error có thể đọc
            context.Items["ErrorMessage"] = GetUserFriendlyMessage(exception);
            context.Items["StatusCode"] = (int)statusCode;
            context.Items["RequestId"] = context.TraceIdentifier;

            if (_env.IsDevelopment())
            {
                context.Items["ExceptionDetails"] = exception.ToString();
            }

            // Redirect đến trang Error
            context.Response.Redirect($"/Home/Error?statusCode={(int)statusCode}");
            await Task.CompletedTask;
        }

        /// <summary>
        /// Kiểm tra xem request có phải từ API không
        /// </summary>
        private bool IsApiRequest(HttpRequest request)
        {
            if (request.Path.StartsWithSegments("/api"))
                return true;

            var acceptHeader = request.Headers.Accept.ToString();
            if (acceptHeader.Contains("application/json") && !acceptHeader.Contains("text/html"))
                return true;

            if (request.ContentType?.Contains("application/json") == true)
                return true;

            if (request.Headers["X-Requested-With"] == "XMLHttpRequest")
                return true;

            return false;
        }

        /// <summary>
        /// Xác định HTTP Status Code dựa trên loại exception
        /// </summary>
        private HttpStatusCode GetStatusCode(Exception exception)
        {
            return exception switch
            {
                UnauthorizedAccessException => HttpStatusCode.Unauthorized,
                KeyNotFoundException => HttpStatusCode.NotFound,
                ArgumentNullException => HttpStatusCode.BadRequest,
                ArgumentException => HttpStatusCode.BadRequest,
                InvalidOperationException => HttpStatusCode.BadRequest,
                NotImplementedException => HttpStatusCode.NotImplemented,
                TimeoutException => HttpStatusCode.RequestTimeout,
                _ => HttpStatusCode.InternalServerError
            };
        }

        /// <summary>
        /// Lấy message thân thiện với người dùng
        /// </summary>
        private string GetUserFriendlyMessage(Exception exception)
        {
            return exception switch
            {
                UnauthorizedAccessException => "Bạn không có quyền truy cập tài nguyên này.",
                KeyNotFoundException => "Không tìm thấy dữ liệu yêu cầu.",
                ArgumentNullException => "Thiếu thông tin bắt buộc.",
                ArgumentException => "Dữ liệu đầu vào không hợp lệ.",
                InvalidOperationException => exception.Message,
                NotImplementedException => "Chức năng này đang được phát triển.",
                TimeoutException => "Yêu cầu quá thời gian chờ. Vui lòng thử lại.",
                _ => _env.IsDevelopment()
                    ? exception.Message
                    : "Đã xảy ra lỗi không mong muốn. Vui lòng thử lại sau."
            };
        }

        /// <summary>
        /// Log chi tiết exception
        /// </summary>
        private void LogException(HttpContext context, Exception exception)
        {
            var user = context.User?.Identity?.Name ?? "Anonymous";
            var request = context.Request;

            _logger.LogError(
                exception,
                "🔥 MIDDLEWARE CAUGHT EXCEPTION | User: {User} | Method: {Method} | Path: {Path} | Query: {Query} | IP: {IP}",
                user,
                request.Method,
                request.Path,
                request.QueryString,
                context.Connection.RemoteIpAddress?.ToString()
            );
        }
    }

    /// <summary>
    /// Extension method để dễ dàng đăng ký middleware
    /// </summary>
    public static class ExceptionHandlingMiddlewareExtensions
    {
        public static IApplicationBuilder UseGlobalExceptionHandling(this IApplicationBuilder app)
        {
            return app.UseMiddleware<ExceptionHandlingMiddleware>();
        }
    }
}
