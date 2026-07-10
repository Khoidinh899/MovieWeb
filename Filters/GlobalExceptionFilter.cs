using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using System.Net;
using Microsoft.EntityFrameworkCore;

namespace MovieWeb.Filters
{
    /// <summary>
    /// Global Exception Filter - Bắt mọi exception xảy ra trong Controllers
    /// Tự động phân biệt API request và Web request để trả về response phù hợp
    /// </summary>
    public class GlobalExceptionFilter : IExceptionFilter
    {
        private readonly ILogger<GlobalExceptionFilter> _logger;
        private readonly IWebHostEnvironment _env;

        public GlobalExceptionFilter(ILogger<GlobalExceptionFilter> logger, IWebHostEnvironment env)
        {
            _logger = logger;
            _env = env;
        }

        public void OnException(ExceptionContext context)
        {
            // Log chi tiết exception
            LogException(context);

            // Kiểm tra xem có phải API request không
            if (IsApiRequest(context.HttpContext.Request))
            {
                HandleApiException(context);
            }
            else
            {
                HandleWebException(context);
            }

            // Đánh dấu exception đã được xử lý
            context.ExceptionHandled = true;
        }

        /// <summary>
        /// Xử lý exception cho API requests - Trả về JSON
        /// </summary>
        private void HandleApiException(ExceptionContext context)
        {
            var statusCode = GetStatusCode(context.Exception);
            var response = new
            {
                success = false,
                message = GetUserFriendlyMessage(context.Exception),
                error = _env.IsDevelopment() ? context.Exception.Message : null,
                stackTrace = _env.IsDevelopment() ? context.Exception.StackTrace : null,
                timestamp = DateTime.Now,
                path = context.HttpContext.Request.Path.Value
            };

            context.Result = new JsonResult(response)
            {
                StatusCode = (int)statusCode
            };

            context.HttpContext.Response.ContentType = "application/json";
        }

        /// <summary>
        /// Xử lý exception cho Web requests - Redirect đến trang lỗi
        /// </summary>
        private void HandleWebException(ExceptionContext context)
        {
            var statusCode = GetStatusCode(context.Exception);
            var errorMessage = GetUserFriendlyMessage(context.Exception);

            // Lưu thông tin lỗi vào TempData để hiển thị trên trang Error
            context.HttpContext.Items["ErrorMessage"] = errorMessage;
            context.HttpContext.Items["StatusCode"] = (int)statusCode;

            if (_env.IsDevelopment())
            {
                context.HttpContext.Items["ExceptionDetails"] = context.Exception.ToString();
            }

            // Redirect đến trang Error
            context.Result = new ViewResult
            {
                ViewName = "~/Views/Shared/Error.cshtml",
                ViewData = new ViewDataDictionary(new EmptyModelMetadataProvider(), context.ModelState)
                {
                    ["ErrorMessage"] = errorMessage,
                    ["StatusCode"] = (int)statusCode,
                    ["ExceptionDetails"] = _env.IsDevelopment() ? context.Exception.ToString() : null,
                    ["RequestId"] = context.HttpContext.TraceIdentifier
                }
            };

            context.HttpContext.Response.StatusCode = (int)statusCode;
        }

        /// <summary>
        /// Kiểm tra xem request có phải từ API không
        /// </summary>
        private bool IsApiRequest(HttpRequest request)
        {
            // Kiểm tra path bắt đầu bằng /api
            if (request.Path.StartsWithSegments("/api"))
                return true;

            // Kiểm tra Accept header
            var acceptHeader = request.Headers.Accept.ToString();
            if (acceptHeader.Contains("application/json") && !acceptHeader.Contains("text/html"))
                return true;

            // Kiểm tra Content-Type
            if (request.ContentType?.Contains("application/json") == true)
                return true;

            // Kiểm tra X-Requested-With header (AJAX)
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
                DbUpdateException => "Có lỗi khi cập nhật dữ liệu. Vui lòng thử lại.",
                _ => _env.IsDevelopment() 
                    ? exception.Message 
                    : "Đã xảy ra lỗi không mong muốn. Vui lòng thử lại sau."
            };
        }

        /// <summary>
        /// Log chi tiết exception
        /// </summary>
        private void LogException(ExceptionContext context)
        {
            var request = context.HttpContext.Request;
            var user = context.HttpContext.User?.Identity?.Name ?? "Anonymous";

            _logger.LogError(
                context.Exception,
                "⚠️ UNHANDLED EXCEPTION | User: {User} | Method: {Method} | Path: {Path} | Query: {Query}",
                user,
                request.Method,
                request.Path,
                request.QueryString
            );
        }
    }
}
