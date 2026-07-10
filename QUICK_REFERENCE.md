# 🚀 Quick Reference - Global Exception Handling

## ✅ Files Created

```
MovieWeb/
├── Filters/
│   └── GlobalExceptionFilter.cs          ← Bắt lỗi trong Controllers
├── Middlewares/
│   └── ExceptionHandlingMiddleware.cs    ← Bắt lỗi trong toàn bộ pipeline
├── Views/Shared/
│   └── Error.cshtml                      ← Trang hiển thị lỗi (đã update)
├── Controllers/
│   └── TestErrorController.cs            ← Controller test exception
├── Program.cs                             ← Đã đăng ký Filter & Middleware
├── GLOBAL_EXCEPTION_HANDLING.md          ← Documentation đầy đủ
└── QUICK_REFERENCE.md                     ← File này
```

---

## 🎯 Test ngay

### 1️⃣ Khởi động ứng dụng:
```powershell
dotnet run
```

### 2️⃣ Truy cập trang test:
```
https://localhost:5001/test-error
```

### 3️⃣ Test từng loại lỗi:
- Click vào các nút để test Web requests
- Click "Test API Error" để test API response

---

## 📝 Code Examples

### ❌ Cũ (Phải viết try-catch):
```csharp
public async Task<IActionResult> Detail(string slug)
{
    try
    {
        var movie = await _context.Movies.FirstOrDefaultAsync(m => m.Slug == slug);
        
        if (movie == null)
            return NotFound();
        
        return View(movie);
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Error loading movie");
        return StatusCode(500, "Có lỗi xảy ra");
    }
}
```

### ✅ Mới (Throw exception trực tiếp):
```csharp
public async Task<IActionResult> Detail(string slug)
{
    var movie = await _context.Movies.FirstOrDefaultAsync(m => m.Slug == slug);
    
    if (movie == null)
        throw new KeyNotFoundException($"Không tìm thấy phim: {slug}");
    
    return View(movie);
    
    // Exception sẽ được GlobalExceptionFilter bắt tự động!
}
```

---

## 🔥 Exception → Status Code Mapping

| Throw Exception | HTTP Status | Message cho User |
|----------------|-------------|------------------|
| `throw new KeyNotFoundException()` | 404 | "Không tìm thấy dữ liệu yêu cầu." |
| `throw new UnauthorizedAccessException()` | 401 | "Bạn không có quyền truy cập." |
| `throw new ArgumentException()` | 400 | "Dữ liệu đầu vào không hợp lệ." |
| `throw new ArgumentNullException()` | 400 | "Thiếu thông tin bắt buộc." |
| `throw new InvalidOperationException()` | 400 | (Message gốc) |
| `throw new TimeoutException()` | 408 | "Request bị timeout." |
| `throw new Exception()` | 500 | "Lỗi máy chủ không mong muốn." |

---

## 🌐 Request Type Detection

Global Exception Handler tự động phân biệt:

### API Request (trả JSON) khi:
- Path bắt đầu bằng `/api`
- Header `Accept: application/json`
- Header `Content-Type: application/json`
- Header `X-Requested-With: XMLHttpRequest`

### Web Request (redirect trang lỗi) khi:
- Không match điều kiện trên

---

## 🎨 Response Examples

### API Response (JSON):
```json
{
  "success": false,
  "message": "Không tìm thấy dữ liệu yêu cầu.",
  "error": "Movie with slug 'xyz' not found",
  "stackTrace": "at MovieWeb.Controllers...",
  "timestamp": "2026-01-29T15:30:00",
  "path": "/api/movies/xyz",
  "statusCode": 404
}
```

### Web Response:
Redirect về `/Home/Error?statusCode=404` → Hiển thị trang Error.cshtml đẹp mắt

---

## 🛠️ Customization

### Thêm custom exception mapping:

**File:** `Filters/GlobalExceptionFilter.cs` và `Middlewares/ExceptionHandlingMiddleware.cs`

```csharp
private HttpStatusCode GetStatusCode(Exception exception)
{
    return exception switch
    {
        MovieNotFoundException => HttpStatusCode.NotFound,        // ← Thêm
        PaymentFailedException => HttpStatusCode.PaymentRequired, // ← Thêm
        UnauthorizedAccessException => HttpStatusCode.Unauthorized,
        // ... existing mappings
        _ => HttpStatusCode.InternalServerError
    };
}

private string GetUserFriendlyMessage(Exception exception)
{
    return exception switch
    {
        MovieNotFoundException => "Không tìm thấy phim.", // ← Thêm
        PaymentFailedException => "Thanh toán thất bại.", // ← Thêm
        UnauthorizedAccessException => "Bạn không có quyền truy cập.",
        // ... existing mappings
        _ => "Đã xảy ra lỗi."
    };
}
```

---

## 📊 Logging Format

Mọi exception đều được log tự động:

```
⚠️ UNHANDLED EXCEPTION | User: john@example.com | Method: GET | Path: /phim/avengers | Query: ?season=1
```

Log location:
- **Console** (Development)
- **Application Insights / Serilog** (Production)

---

## ⚡ Performance

- **Middleware** đặt ở đầu pipeline → Catch sớm
- **Filter** áp dụng trong Controllers → Catch business logic errors
- **Error page** được cache → Load nhanh

---

## 🐛 Common Issues

### Exception vẫn không được bắt?
✅ Check: Middleware đã đăng ký trong `Program.cs` chưa?
✅ Check: Middleware có ở **đầu** pipeline không?

### API vẫn trả HTML?
✅ Check: Request có header `Accept: application/json` không?
✅ Check: Path có bắt đầu bằng `/api` không?

### Trang lỗi không hiển thị?
✅ Check: File `Views/Shared/Error.cshtml` có tồn tại không?
✅ Check: ViewData có được truyền đúng không?

---

## 📞 Support

- **Documentation:** `GLOBAL_EXCEPTION_HANDLING.md`
- **Test Page:** `https://localhost:5001/test-error`
- **Log Location:** Console / Application Insights

---

**Last Updated:** 29/01/2026
**Version:** 1.0.0
