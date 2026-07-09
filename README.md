# 🌙 MoonPhim – Nền tảng xem phim trực tuyến

> Ứng dụng web xem phim trực tuyến đầy đủ tính năng, xây dựng bằng **ASP.NET Core 9 MVC**. Hỗ trợ gói miễn phí và trả phí, thông báo real-time, chatbot AI, hệ thống gợi ý phim thông minh và tích hợp thanh toán qua Stripe.

---

## 🛠️ Công nghệ sử dụng

![ASP.NET Core](https://img.shields.io/badge/ASP.NET%20Core-9.0-512BD4?logo=dotnet&logoColor=white)
![SQL Server](https://img.shields.io/badge/SQL%20Server-2022-CC2927?logo=microsoftsqlserver&logoColor=white)
![Entity Framework Core](https://img.shields.io/badge/EF%20Core-9.0-512BD4?logo=dotnet&logoColor=white)
![SignalR](https://img.shields.io/badge/SignalR-Realtime-00AFF0?logo=dotnet)
![Stripe](https://img.shields.io/badge/Stripe-Thanh%20toán-6772E5?logo=stripe&logoColor=white)
![Hangfire](https://img.shields.io/badge/Hangfire-Background%20Jobs-2D5D9E)
![Gemini AI](https://img.shields.io/badge/Gemini%20AI-Chatbot-4285F4?logo=google&logoColor=white)
![Firebase](https://img.shields.io/badge/Firebase-FCM%20Push-FFCA28?logo=firebase&logoColor=black)
![SendGrid](https://img.shields.io/badge/SendGrid-Email-1A82E2?logo=sendgrid&logoColor=white)

---

## ✨ Tính năng nổi bật

### 🎬 Phim & Nội dung
- Duyệt và tìm kiếm phim theo thể loại, quốc gia, định dạng (phim bộ / phim lẻ / hoạt hình)
- Trang chi tiết phim: danh sách tập, diễn viên, đạo diễn
- **Tự động đồng bộ** dữ liệu phim từ API bên ngoài (OPhim) thông qua background service
- URL thân thiện SEO (`/phim/{slug}`, `/the-loai/{slug}`) với Sitemap & Robots.txt động

### 👤 Người dùng & Xác thực
- Xác thực bằng **Cookie** với ASP.NET Core Identity
- Đăng nhập bằng **Google OAuth 2.0**
- **JWT Bearer** cho các API endpoint
- Xác nhận email khi đăng ký (qua SendGrid)
- Đặt lại mật khẩu qua email
- Phân quyền theo vai trò: `Admin` / `User`
- Custom Claims factory tích hợp role vào token

### 💳 Gói đăng ký & Thanh toán
- Hệ thống gói **Miễn phí / Trả phí** với kiểm soát quyền truy cập nội dung
- Thanh toán đăng ký qua **Stripe Checkout** (đơn vị tiền VND)
- Xử lý **Stripe Webhook** cho toàn bộ vòng đời thanh toán
- Ưu đãi **học sinh/sinh viên** qua email `.edu.vn`, `.edu`, `.ac.vn`
- Nhắc nhở gia hạn tự động trước 7, 3, 1 ngày (Hangfire cron job)
- Tự động đồng bộ sản phẩm & giá Stripe khi khởi động

### 🔔 Thông báo
- **Thông báo real-time** qua SignalR (`NotificationHub`)
- **Firebase Cloud Messaging (FCM)** cho push notification thiết bị di động
- Gửi thông báo tự động mỗi 5 phút (Hangfire recurring job)
- Hộp thư thông báo trong app với trạng thái đã đọc / chưa đọc

### 🤖 Tính năng AI
- **Chatbot phim AI** dùng **Google Gemini 2.5 Flash**, streaming real-time qua SignalR (`ChatbotHub`)
- **Hệ thống gợi ý phim** dựa trên lịch sử xem và sở thích người dùng
- Tính năng **yêu cầu thêm phim** – người dùng có thể đề xuất phim muốn xem

### 📊 Quản trị (Admin)
- Quản lý phim, tập phim, thể loại, quốc gia
- Quản lý người dùng (khóa tài khoản, phân quyền)
- Xem nhật ký hoạt động admin
- Dashboard Hangfire tại `/hangfire`

### ❤️ Trải nghiệm người dùng
- Danh sách **yêu thích** riêng cho từng tài khoản
- Theo dõi **lịch sử xem phim**
- Hệ thống **bình luận** theo phim
- Hỗ trợ **đánh giá** phim
- Quản lý quảng cáo (Advertisement)

---

## 🏗️ Kiến trúc & Thiết kế hệ thống

```
MovieWeb/
├── Controllers/         # MVC Controllers (Movie, Auth, Profile, Admin, Payment, ...)
│   ├── API/             # REST API cho AJAX / Flutter client
│   └── Payment/         # Stripe webhook & checkout
├── Services/            # Tầng nghiệp vụ (interface-driven DI)
│   ├── AuthService      # Đăng ký, đăng nhập, JWT, Google OAuth
│   ├── MovieSyncService # Pipeline đồng bộ OPhim API → Database
│   ├── GeminiService    # Tích hợp Google Gemini AI
│   ├── StripeService    # Quản lý thanh toán & gói đăng ký
│   ├── NotificationService
│   ├── RecommendationService
│   └── ...
├── Repositories/        # Tầng truy cập dữ liệu (Repository Pattern)
├── Models/
│   ├── Entities/        # EF Core entities (User, Movie, Episode, Subscription, ...)
│   ├── DTOs/            # Data Transfer Objects
│   └── ViewModels/      # MVC ViewModels
├── Hubs/                # SignalR Hubs (NotificationHub, ChatbotHub)
├── Jobs/                # Hangfire background jobs
├── Middlewares/         # Middleware tùy chỉnh (Global Exception Handling)
├── Filters/             # Action Filters (GlobalExceptionFilter, HangfireAuth)
├── Migrations/          # EF Core database migrations
└── Data/                # DbContext (MovieWebDbContext)
```

### Bảng tổng hợp kỹ thuật

| Vấn đề | Giải pháp |
|---|---|
| Xác thực | Cookie Identity + JWT cho API + Google OAuth |
| Background Job | Hangfire với SQL Server storage |
| Real-time | ASP.NET Core SignalR |
| Gửi email | SendGrid transactional email |
| Push notification | Firebase FCM |
| AI Chatbot | Google Gemini 2.5 Flash (streaming) |
| Thanh toán | Stripe Checkout Session + Webhook |
| Đồng bộ dữ liệu | IHostedService + các sync service |
| Xử lý lỗi | Global Exception Middleware + Action Filter |
| SEO | Custom Sitemap, Robots.txt + Schema markup |

---

## 🗄️ Các Entity chính

| Entity | Mô tả |
|---|---|
| `User` | Kế thừa ASP.NET Identity, có Stripe customer ID, avatar, trạng thái đăng ký |
| `Movie` | Dùng slug, phân loại (series/single/hoathinh), đồng bộ từ OPhim |
| `Episode` | Thuộc Movie, lưu URL stream |
| `Category` / `Country` / `Actor` / `Director` | Metadata cho phim |
| `SubscriptionPlan` | Gói Miễn phí/Trả phí với Stripe product/price ID |
| `UserSubscription` | Liên kết user-gói với thời gian bắt đầu/kết thúc |
| `Transaction` | Lịch sử giao dịch thanh toán |
| `Notification` | Thông báo trong app với trạng thái đọc |
| `WatchHistory` | Lịch sử xem theo người dùng |
| `Favorite` | Danh sách phim yêu thích |
| `Comment` | Bình luận theo phim |
| `RequestsMovie` | Yêu cầu thêm phim từ người dùng |

---

## ⚙️ Hướng dẫn cài đặt & chạy

### Yêu cầu
- [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9)
- SQL Server 2019 trở lên (hoặc Azure SQL)
- Các API key: **Stripe**, **SendGrid**, **Google OAuth**, **Gemini AI**, **Firebase Admin SDK**

### 1. Clone dự án

```bash
git clone https://github.com/Khoidinh899/MovieWeb.git
cd MovieWeb
```

### 2. Cấu hình biến môi trường

Tạo file `.env.local` tại thư mục gốc của dự án (file này đã được gitignore):

```env
# Database
ConnectionStrings__DefaultConnection=Server=localhost;Database=MovieWebDb;Trusted_Connection=True;TrustServerCertificate=True;

# Google OAuth
Authentication__Google__ClientId=YOUR_GOOGLE_CLIENT_ID
Authentication__Google__ClientSecret=YOUR_GOOGLE_CLIENT_SECRET

# JWT
JwtSettings__SecretKey=YOUR_JWT_SECRET_KEY_AT_LEAST_32_CHARS

# Stripe
StripeSettings__PublishableKey=pk_test_...
StripeSettings__SecretKey=sk_test_...
StripeSettings__WebhookSecret=whsec_...

# SendGrid
SendGrid__ApiKey=SG.your_sendgrid_api_key
SendGrid__SenderEmail=noreply@yourdomain.com
SendGrid__SenderName=MoonPhim

# Google Gemini AI
Gemini__ApiKey=YOUR_GEMINI_API_KEY

# Firebase (đường dẫn tới file service account JSON)
Firebase__ServiceAccountPath=firebase-adminsdk.json
```

### 3. Chạy migration database

```bash
dotnet ef database update
```

### 4. Chạy ứng dụng

```bash
dotnet run
```

Ứng dụng sẽ chạy tại `https://localhost:5001`.

> ✅ Khi khởi động lần đầu, migration database sẽ được tự động apply và các gói Stripe sẽ được đồng bộ.

---

## 🔑 Tài khoản Admin mặc định

Sau lần chạy đầu tiên, tài khoản admin được seed từ `appsettings.json`:

```
Email:    admin@moonphim.com
Mật khẩu: (cấu hình tại AdminSettings:Password)
```

---

## 🌐 API Endpoints (REST)

Ứng dụng cung cấp REST API tại `/api/` cho AJAX và client Flutter:

| Endpoint | Mô tả |
|---|---|
| `POST /api/auth/login` | Đăng nhập, trả về cookie session |
| `POST /api/auth/google` | Xác thực token Google |
| `GET /api/movies` | Danh sách phim có lọc/tìm kiếm |
| `POST /api/payment/checkout` | Tạo Stripe Checkout session |
| `POST /api/payment/webhook` | Nhận webhook từ Stripe |
| `GET /api/notifications` | Lấy thông báo của người dùng |
| `GET /api/favorites` | Danh sách phim yêu thích |
| `GET /api/watch-history` | Lịch sử xem phim |

Xem toàn bộ: [`postman_collection.json`](./postman_collection.json)

---

## 🔄 Background Jobs (Hangfire)

| Job | Lịch chạy | Mô tả |
|---|---|---|
| `PaymentReminderJob` | Hàng ngày lúc 00:00 (ICT) | Gửi email nhắc nhở gia hạn gói |
| `SendRealtimeNotificationJob` | Mỗi 5 phút | Đẩy thông báo tới người dùng online qua SignalR |
| `SitemapCacheRefreshJob` | Cron tùy chỉnh | Làm mới cache sitemap |

Dashboard quản lý tại `/hangfire` (chỉ Admin).

---

## 🧠 Luồng hoạt động Chatbot AI

```
Người dùng gửi tin → ChatbotHub (SignalR) → GeminiService
    → Google Gemini 2.5 Flash (streaming)
    → Phản hồi token-by-token về client
```

Context được xây dựng từ metadata phim và lịch sử xem để đưa ra gợi ý cá nhân hóa.

---

## 📦 Các NuGet Package chính

| Package | Mục đích |
|---|---|
| `Microsoft.AspNetCore.Identity.EntityFrameworkCore` | Quản lý người dùng & phân quyền |
| `Microsoft.AspNetCore.Authentication.Google` | Đăng nhập Google OAuth |
| `Microsoft.AspNetCore.SignalR.Core` | Giao tiếp real-time |
| `Hangfire.SqlServer` | Lên lịch background job |
| `Stripe.net` | Tích hợp thanh toán |
| `SendGrid` | Gửi email giao dịch |
| `FirebaseAdmin` | Push notification FCM |
| `Google.Apis.Auth` | Xác thực token Google |
| `DotNetEnv` | Đọc file `.env` |
