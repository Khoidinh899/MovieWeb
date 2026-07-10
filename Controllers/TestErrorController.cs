using Microsoft.AspNetCore.Mvc;

namespace MovieWeb.Controllers
{
    /// <summary>
    /// Controller để test Global Exception Handling
    /// Route: /test-error/{action}
    /// </summary>
    [Route("test-error")]
    public class TestErrorController : Controller
    {
        private readonly ILogger<TestErrorController> _logger;

        public TestErrorController(ILogger<TestErrorController> logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// Test 404 Not Found
        /// URL: /test-error/not-found
        /// </summary>
        [HttpGet("not-found")]
        public IActionResult NotFound()
        {
            throw new KeyNotFoundException("Test exception: Không tìm thấy dữ liệu");
        }

        /// <summary>
        /// Test 401 Unauthorized
        /// URL: /test-error/unauthorized
        /// </summary>
        [HttpGet("unauthorized")]
        public IActionResult Unauthorized()
        {
            throw new UnauthorizedAccessException("Test exception: Bạn không có quyền truy cập");
        }

        /// <summary>
        /// Test 400 Bad Request (Argument)
        /// URL: /test-error/bad-request
        /// </summary>
        [HttpGet("bad-request")]
        public IActionResult BadRequest()
        {
            throw new ArgumentException("Test exception: Tham số không hợp lệ");
        }

        /// <summary>
        /// Test 400 Bad Request (Null)
        /// URL: /test-error/null-argument
        /// </summary>
        [HttpGet("null-argument")]
        public IActionResult NullArgument()
        {
            throw new ArgumentNullException("movieId", "Test exception: MovieId không được để trống");
        }

        /// <summary>
        /// Test 500 Internal Server Error
        /// URL: /test-error/server-error
        /// </summary>
        [HttpGet("server-error")]
        public IActionResult ServerError()
        {
            throw new Exception("Test exception: Lỗi máy chủ không mong muốn");
        }

        /// <summary>
        /// Test 408 Timeout
        /// URL: /test-error/timeout
        /// </summary>
        [HttpGet("timeout")]
        public IActionResult Timeout()
        {
            throw new TimeoutException("Test exception: Request bị timeout");
        }

        /// <summary>
        /// Test InvalidOperationException
        /// URL: /test-error/invalid-operation
        /// </summary>
        [HttpGet("invalid-operation")]
        public IActionResult InvalidOperation()
        {
            throw new InvalidOperationException("Test exception: Thao tác không hợp lệ trong context hiện tại");
        }

        /// <summary>
        /// Test API response (JSON)
        /// URL: /test-error/api-error
        /// Header: Accept: application/json
        /// </summary>
        [HttpGet("api-error")]
        public IActionResult ApiError()
        {
            _logger.LogInformation("Testing API error response...");
            throw new KeyNotFoundException("Test API exception: Movie ID 999 not found");
        }

        /// <summary>
        /// Test divide by zero
        /// URL: /test-error/divide-zero
        /// </summary>
        [HttpGet("divide-zero")]
        public IActionResult DivideByZero()
        {
            int zero = 0;
            int result = 100 / zero; // Sẽ throw DivideByZeroException
            return Ok(result);
        }

        /// <summary>
        /// Test null reference
        /// URL: /test-error/null-reference
        /// </summary>
        [HttpGet("null-reference")]
        public IActionResult NullReference()
        {
            string? nullString = null;
            int length = nullString.Length; // Sẽ throw NullReferenceException
            return Ok(length);
        }

        /// <summary>
        /// Trang test với HTML form
        /// URL: /test-error
        /// </summary>
        [HttpGet("")]
        public IActionResult Index()
        {
            return Content(@"
<!DOCTYPE html>
<html lang='vi'>
<head>
    <meta charset='UTF-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
    <title>Global Exception Handling Test</title>
    <style>
        * { margin: 0; padding: 0; box-sizing: border-box; }
        body { 
            font-family: 'Segoe UI', sans-serif;
            background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
            min-height: 100vh;
            padding: 40px 20px;
        }
        .container {
            max-width: 800px;
            margin: 0 auto;
            background: white;
            border-radius: 20px;
            padding: 40px;
            box-shadow: 0 20px 60px rgba(0,0,0,0.3);
        }
        h1 {
            background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
            -webkit-background-clip: text;
            -webkit-text-fill-color: transparent;
            margin-bottom: 10px;
            font-size: 32px;
        }
        p { color: #666; margin-bottom: 30px; line-height: 1.6; }
        .test-section { margin-bottom: 30px; }
        .test-section h2 { 
            color: #333; 
            font-size: 20px; 
            margin-bottom: 15px;
            border-bottom: 2px solid #f0f0f0;
            padding-bottom: 10px;
        }
        .btn-grid {
            display: grid;
            grid-template-columns: repeat(auto-fill, minmax(180px, 1fr));
            gap: 15px;
        }
        .btn {
            padding: 12px 20px;
            border: none;
            border-radius: 8px;
            font-weight: 600;
            cursor: pointer;
            transition: all 0.3s ease;
            text-decoration: none;
            text-align: center;
            display: inline-block;
            color: white;
        }
        .btn-error { background: #dc3545; }
        .btn-error:hover { background: #c82333; transform: translateY(-2px); }
        .btn-api { background: #17a2b8; }
        .btn-api:hover { background: #138496; transform: translateY(-2px); }
        .note {
            background: #fff3cd;
            border-left: 4px solid #ffc107;
            padding: 15px;
            margin-top: 30px;
            border-radius: 8px;
        }
        .note strong { color: #856404; }
        code {
            background: #f8f9fa;
            padding: 2px 6px;
            border-radius: 4px;
            font-family: 'Courier New', monospace;
            font-size: 14px;
        }
    </style>
</head>
<body>
    <div class='container'>
        <h1>🛡️ Global Exception Handling Test</h1>
        <p>Click vào các nút bên dưới để test cơ chế xử lý lỗi tự động. Mỗi nút sẽ throw một exception khác nhau.</p>

        <div class='test-section'>
            <h2>🌐 Web Requests (Redirect đến trang lỗi)</h2>
            <div class='btn-grid'>
                <a href='/test-error/not-found' class='btn btn-error'>404 Not Found</a>
                <a href='/test-error/unauthorized' class='btn btn-error'>401 Unauthorized</a>
                <a href='/test-error/bad-request' class='btn btn-error'>400 Bad Request</a>
                <a href='/test-error/null-argument' class='btn btn-error'>Null Argument</a>
                <a href='/test-error/server-error' class='btn btn-error'>500 Server Error</a>
                <a href='/test-error/timeout' class='btn btn-error'>408 Timeout</a>
                <a href='/test-error/invalid-operation' class='btn btn-error'>Invalid Operation</a>
                <a href='/test-error/divide-zero' class='btn btn-error'>Divide By Zero</a>
                <a href='/test-error/null-reference' class='btn btn-error'>Null Reference</a>
            </div>
        </div>

        <div class='test-section'>
            <h2>📱 API Requests (Trả về JSON)</h2>
            <div class='btn-grid'>
                <button class='btn btn-api' onclick='testApi()'>Test API Error</button>
            </div>
            <pre id='result' style='background: #f8f9fa; padding: 15px; border-radius: 8px; margin-top: 15px; display: none; max-height: 300px; overflow-y: auto;'></pre>
        </div>

        <div class='note'>
            <strong>📝 Lưu ý:</strong>
            <ul style='margin: 10px 0 0 20px; color: #856404;'>
                <li>Các nút <strong>Web Requests</strong> sẽ redirect về trang Error.cshtml</li>
                <li>Nút <strong>API Request</strong> sẽ trả về JSON response</li>
                <li>Ở <code>Development mode</code>, bạn sẽ thấy stack trace chi tiết</li>
                <li>Ở <code>Production mode</code>, thông báo sẽ thân thiện hơn và ẩn thông tin kỹ thuật</li>
            </ul>
        </div>
    </div>

    <script>
        async function testApi() {
            const resultDiv = document.getElementById('result');
            resultDiv.style.display = 'block';
            resultDiv.textContent = 'Đang gọi API...';
            
            try {
                const response = await fetch('/test-error/api-error', {
                    headers: {
                        'Accept': 'application/json'
                    }
                });
                
                const data = await response.json();
                resultDiv.textContent = JSON.stringify(data, null, 2);
            } catch (error) {
                resultDiv.textContent = 'Error: ' + error.message;
            }
        }
    </script>
</body>
</html>
", "text/html");
        }
    }
}
