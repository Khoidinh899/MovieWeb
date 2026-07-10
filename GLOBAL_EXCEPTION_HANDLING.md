# 🛡️ Global Exception Handling - MovieWeb

## 📌 Tổng quan

Dự án MovieWeb đã được tích hợp **Global Exception Handling** để tự động bắt và xử lý mọi lỗi xảy ra trong ứng dụng mà không cần phải viết `try-catch` ở mỗi action.

### ✨ Tính năng chính:

1. **Tự động phân biệt API và Web request** - Trả về JSON hoặc redirect về trang lỗi tùy theo loại request
2. **Logging chi tiết** - Ghi log đầy đủ thông tin exception, user, request path
3. **Thông báo thân thiện** - Hiển thị message dễ hiểu cho người dùng
4. **Environment-aware** - Hiển thị stack trace chi tiết ở Development mode
5. **HTTP Status Code mapping** - Tự động map exception sang status code phù hợp

---

## 🏗️ Kiến trúc

### 1. **GlobalExceptionFilter** (`Filters/GlobalExceptionFilter.cs`)
- Bắt exception xảy ra trong **Controllers**
- Áp dụng cho tất cả actions

### 2. **ExceptionHandlingMiddleware** (`Middlewares/ExceptionHandlingMiddleware.cs`)
- Bắt exception xảy ra trong **toàn bộ request pipeline**
- Xử lý những lỗi mà Filter không catch được (middleware khác, routing, etc.)

### 3. **Error View** (`Views/Shared/Error.cshtml`)
- Trang hiển thị lỗi đẹp mắt với gradient design
- Responsive, hỗ trợ dark mode
- Hiển thị chi tiết lỗi ở Development mode

---

## 🚀 Cách sử dụng

### ✅ Không cần làm gì thêm!

Sau khi đã đăng ký trong `Program.cs`, mọi exception sẽ tự động được bắt:

```csharp
public async Task<IActionResult> Detail(string slug)
{
    // ❌ KHÔNG CẦN try-catch nữa!
    var movie = await _context.Movies.FirstOrDefaultAsync(m => m.Slug == slug);
    
    if (movie == null)
        throw new KeyNotFoundException($"Không tìm thấy phim với slug: {slug}");
    
    return View(movie);
}
```

### 🎯 Ví dụ exception được xử lý tự động:

| Exception | Status Code | Message cho user |
|-----------|-------------|------------------|
| `KeyNotFoundException` | 404 | "Không tìm thấy dữ liệu yêu cầu." |
| `UnauthorizedAccessException` | 401 | "Bạn không có quyền truy cập tài nguyên này." |
| `ArgumentNullException` | 400 | "Thiếu thông tin bắt buộc." |
| `InvalidOperationException` | 400 | (Giữ nguyên message) |
| `TimeoutException` | 408 | "Yêu cầu quá thời gian chờ. Vui lòng thử lại." |
| `Exception` (generic) | 500 | "Đã xảy ra lỗi không mong muốn. Vui lòng thử lại sau." |

---

## 📊 Response Format

### 🔹 Với API Request:

**Request:**
```http
GET /api/movies/999 HTTP/1.1
Accept: application/json
```

**Response (404):**
```json
{
  "success": false,
  "message": "Không tìm thấy dữ liệu yêu cầu.",
  "error": "Movie with ID 999 not found", // Chỉ hiện ở Development
  "stackTrace": "...",                     // Chỉ hiện ở Development
  "timestamp": "2026-01-29T15:30:00",
  "path": "/api/movies/999"
}
```

### 🔹 Với Web Request:

Tự động redirect về trang lỗi đẹp mắt với:
- Icon tương ứng với status code (🔍 404, 💥 500, 🔒 401, etc.)
- Thông báo lỗi bằng tiếng Việt
- Nút "Về trang chủ" và "Quay lại"
- Chi tiết stack trace (chỉ ở Development mode)

---

## 🔧 Tùy chỉnh

### Thêm custom exception:

**Bước 1:** Tạo custom exception
```csharp
public class MovieNotFoundException : Exception
{
    public MovieNotFoundException(string message) : base(message) { }
}
```

**Bước 2:** Thêm mapping trong `GlobalExceptionFilter.cs` và `ExceptionHandlingMiddleware.cs`:

```csharp
private HttpStatusCode GetStatusCode(Exception exception)
{
    return exception switch
    {
        MovieNotFoundException => HttpStatusCode.NotFound,
        UnauthorizedAccessException => HttpStatusCode.Unauthorized,
        // ... existing code
        _ => HttpStatusCode.InternalServerError
    };
}

private string GetUserFriendlyMessage(Exception exception)
{
    return exception switch
    {
        MovieNotFoundException => "Không tìm thấy phim bạn yêu cầu.",
        UnauthorizedAccessException => "Bạn không có quyền truy cập.",
        // ... existing code
        _ => "Đã xảy ra lỗi không mong muốn."
    };
}
```

### Tùy chỉnh trang Error:

Chỉnh sửa file `Views/Shared/Error.cshtml` theo ý muốn.

---

## 📝 Logging

Mọi exception đều được log với format:

```
⚠️ UNHANDLED EXCEPTION | User: john@example.com | Method: GET | Path: /phim/avengers | Query: ?season=1
🔥 MIDDLEWARE CAUGHT EXCEPTION | User: Anonymous | Method: POST | Path: /api/payment/create | IP: 192.168.1.1
```

Xem log tại:
- Console (Development)
- Application Insights / Serilog (Production)

---

## ⚡ Performance Tips

1. **Middleware được đặt ở đầu pipeline** - Catch lỗi sớm nhất có thể
2. **Filter áp dụng cho Controller** - Catch lỗi trong business logic
3. **Cache Error page** - Trang lỗi được cache để tải nhanh

---

## 🐛 Troubleshooting

### ❓ Exception vẫn không được bắt?

**Kiểm tra:**
1. Đã đăng ký middleware trong `Program.cs` chưa?
2. Middleware có đặt **trước** các middleware khác không?
3. Exception có throw từ background job (Hangfire) không? → Cần xử lý riêng

### ❓ Trang lỗi không hiển thị đúng?

**Kiểm tra:**
1. View `Error.cshtml` có tồn tại trong `Views/Shared/` không?
2. ViewData có được truyền đúng không?

### ❓ API vẫn trả về HTML thay vì JSON?

**Kiểm tra:**
1. Request có header `Accept: application/json` chưa?
2. Path có bắt đầu bằng `/api` không?
3. Logic phân biệt API request trong `IsApiRequest()` có đúng không?

---

## ✅ Checklist Implementation

- [x] Tạo `GlobalExceptionFilter.cs`
- [x] Tạo `ExceptionHandlingMiddleware.cs`
- [x] Cập nhật `Error.cshtml` với design đẹp
- [x] Đăng ký Filter trong `Program.cs`
- [x] Đăng ký Middleware trong `Program.cs`
- [x] Test với API request
- [x] Test với Web request
- [x] Test trong Development mode
- [x] Test trong Production mode

---

## 📚 Tài liệu tham khảo

- [ASP.NET Core Error Handling](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/error-handling)
- [Exception Filters](https://learn.microsoft.com/en-us/aspnet/core/mvc/controllers/filters#exception-filters)
- [Custom Middleware](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/middleware/write)

---

**Tác giả:** Backend Specialist
**Ngày tạo:** 29/01/2026
**Version:** 1.0.0
