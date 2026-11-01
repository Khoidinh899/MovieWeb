# CLAUDE.md — Luật bắt buộc cho mọi phiên làm việc trong MoonPhim

> File này là **ràng buộc**, không phải gợi ý. Mọi agent/model (Opus, Sonnet, Haiku…) làm việc trong repo này
> phải đọc và tuân thủ toàn bộ trước khi sửa bất kỳ dòng code nào. Khi có mâu thuẫn giữa file này và một
> yêu cầu/plan khác, **file này thắng** — dừng lại và hỏi chủ dự án.

---

## 0. Bối cảnh sống còn: ĐÂY LÀ PRODUCTION

- MoonPhim (`https://moonphim.me`) **đang chạy thật**, có người dùng và traffic ổn định.
- **Push lên `main` = deploy thẳng lên production**: `.github/workflows/deploy.yml` SSH vào EC2, `git pull origin main`,
  `docker compose build --no-cache app && docker compose up -d --no-deps app`. Không có staging, không có test gate,
  không có health check, không có rollback tự động.
- Khi app khởi động, `Program.cs` **tự chạy `context.Database.Migrate()`** trên DB production. Mọi migration lọt vào
  `main` sẽ chạy ngay lên DB thật của user.

### Luật cứng (vi phạm = dừng ngay)

1. **KHÔNG BAO GIỜ** commit, merge, push, rebase, force-push vào `main`. Không tạo/merge PR vào `main`.
   Chỉ chủ dự án được merge vào `main`, sau khi tự kiểm tra trên localhost.
2. **KHÔNG** chạy `workflow_dispatch` của workflow deploy, không SSH vào server, không sửa secrets GitHub.
3. **KHÔNG** sửa `.github/workflows/deploy.yml`, `docker-compose.yml`, `Dockerfile`, `nginx/**` trừ khi task ghi rõ
   và chủ dự án đồng ý — mọi thay đổi ở đây ảnh hưởng trực tiếp server.
4. **KHÔNG** kết nối tới DB production, Stripe live key, SMTP thật, FCM thật. Chỉ dùng DB local
   (`appsettings.Development.json`: `localhost:5433/moonphim_local`) và Stripe **test mode**.
5. **KHÔNG** thêm migration EF Core mà chưa được chủ dự án duyệt riêng. Nếu buộc phải có: migration chỉ được
   **thêm** (add column nullable / add table), không drop/rename cột, không xóa dữ liệu; phải ghi rõ trong mô tả commit.
6. **KHÔNG** commit secret (`.env*`, `firebase-*.json`, key API, connection string thật). Nếu thấy secret trong code → báo, không lặp lại giá trị.
7. **KHÔNG** xóa file/route/API mà chưa chứng minh được là không dùng (xem §4). App Flutter/APK (`/downloads/*.apk`,
   `Controllers/API/**`) đang gọi API — phá API = phá app mobile của user.
8. **KHÔNG** đổi URL công khai đang được Google index (`/phim/{slug}`, `/the-loai/{slug}`, sitemap, robots) —
   SEO và traffic phụ thuộc vào chúng.

---

## 1. Quy trình làm việc an toàn

- Mỗi đợt việc làm trên **nhánh riêng** (`v2/<phase>-<mô-tả>` hoặc nhánh được giao trong phiên). Commit nhỏ, mỗi
  commit một mục đích, message dạng `type(scope): mô tả` (theo lịch sử repo: `feat`, `fix`, `refactor`, `chore`, `style`).
- Thay đổi lớn (giao diện mới) đặt sau **feature flag** trong `appsettings.json > FeatureFlags` (repo đã có cơ chế này,
  ví dụ `ShowUpgradeAnnouncementModal`, `PublicWatchParty`). Mặc định flag **tắt** → merge vào main cũng không đổi gì cho user
  cho tới khi chủ dự án bật.
- Không gom nhiều phase vào một nhánh. Mỗi phase phải tự chạy được độc lập.

### Checklist bắt buộc trước khi báo "xong" một task

```bash
dotnet build MovieWeb.csproj -c Release   # 0 error; không thêm warning mới
```
1. Chạy app local (`ASPNETCORE_ENVIRONMENT=Development`, DB Postgres local) và mở bằng trình duyệt thật
   (Playwright/Chromium có sẵn trong môi trường cloud).
2. Smoke test tối thiểu, **desktop + mobile viewport (390×844) + Safari/WebKit**:
   trang chủ, danh sách phim (phim bộ / lẻ / hoạt hình / mới cập nhật) + bộ lọc, chi tiết phim, **xem phim (player)**,
   tìm kiếm, đăng nhập/đăng ký (modal), profile / yêu thích / lịch sử, trang nâng cấp gói, admin dashboard.
3. Console trình duyệt không có lỗi JS mới; không có request 404 tới asset mới.
4. Không chạm logic thanh toán, auth, quảng cáo nếu task không yêu cầu. Nếu có chạm: test lại luồng đó bằng Stripe test.
5. Báo cáo trung thực: cái gì đã test, cái gì chưa test được, kèm ảnh chụp/ log nếu có.

> Lịch sử repo có nhiều commit `fix(safari)` — **Safari/iOS là nền tảng dễ vỡ nhất**. Mọi CSS/animation mới phải kiểm
> tra trên WebKit, thêm `-webkit-` prefix khi cần, tránh tính năng CSS chưa hỗ trợ trên iOS 15+.

---

## 2. Kiến trúc thật của dự án (đừng giả định khác)

- **ASP.NET Core 9 MVC + Razor Views** (`Views/**.cshtml`), **không** có React/Vue/Node build pipeline.
  Frontend = Bootstrap 5 + CSS thuần (`wwwroot/css/*.css`) + JS thuần (`wwwroot/js/*.js`), thư viện ngoài qua CDN
  (Bootstrap Icons, Font Awesome) hoặc `libman.json` (SignalR).
- PostgreSQL 16 + EF Core 9 (`Data/`, `Migrations/`), Identity + Cookie + JWT (cho API mobile) + Google OAuth.
- Hangfire (`/hangfire`), SignalR (`NotificationHub`, `WatchPartyHub` được map; `Hubs/ChatbotHub.cs` tồn tại nhưng **không được map** trong `Program.cs` — chatbot hiện chạy qua `ChatbotController`, cần xác minh), Stripe, Gemini, FCM, Turnstile, Google AdSense.
- Layouts: `_Layout` (site chính), `_LandingLayout`, `_UserLayout`, `_AdminLayout`; partial ở `Views/Shared/Partial/`.
- Static file được nginx cache 7 ngày → **mọi `<link>`/`<script>` local phải có `asp-append-version="true"`**, nếu không
  user sẽ dính CSS/JS cũ sau deploy.

---

## 3. Quy tắc cho giao diện V2 & animation

- Giao diện mới do chủ dự án cung cấp sẵn → nhiệm vụ là **chuyển đổi (port) đúng thiết kế** sang Razor/CSS/JS,
  không tự sáng tác lại bố cục. Chỗ nào thiết kế không rõ → hỏi, không đoán.
- **Giữ nguyên** mọi `id`, `name`, `data-*`, `asp-*`, form action, anti-forgery token, hook JS mà code hiện tại dựa vào
  (`favorite-handler.js`, `watch-progress-tracker.js`, `video-ads-handler.js`, `auth.js`, `search.js`, filter sidebar…).
  Đổi markup = phải grep JS/C# để chắc không làm gãy selector.
- **animate-ui (`imskyleen/animate-ui`) là thư viện React/Motion/Tailwind (shadcn registry)** — không cài trực tiếp được vào
  dự án Razor này. Cách dùng đã thống nhất: xem §6 (quyết định của chủ dự án). Không được tự ý thêm React/Node/Tailwind build.
- Animation / nút 3D:
  - Chỉ animate `transform` và `opacity` (tránh animate `width/height/top/left/box-shadow` liên tục trên list lớn).
  - Bắt buộc tôn trọng `@media (prefers-reduced-motion: reduce)` → tắt/giảm hiệu ứng.
  - Không chặn tương tác: nút vẫn bấm được ngay, không delay điều hướng vì animation.
  - Không làm vỡ vùng player, iframe embed, quảng cáo, bottom nav mobile, safe-area iOS.
  - Thư viện thêm vào phải **self-host trong `wwwroot/lib/`** (qua `libman.json`) hoặc CDN có SRI, và kích thước hợp lý
    (ghi lại dung lượng trong commit message).

---

## 4. Quy tắc dọn dẹp code thừa

Chỉ được xóa khi có **bằng chứng** không dùng:
1. `grep` toàn repo (C#, cshtml, js, css) không còn tham chiếu — tính cả gọi qua chuỗi (route name, `asp-action`,
   `Url.Action`, `fetch('/api/...')`, tên class CSS ghép động).
2. Không phải endpoint mà app mobile/APK có thể gọi (`Controllers/API/**`, các route `/api/*`).
3. Build + smoke test vẫn pass sau khi xóa.
4. Xóa theo từng commit riêng, mỗi commit một nhóm, message liệt kê file đã xóa và lý do → dễ revert.

Ứng viên đã biết (cần xác minh, **chưa được xóa tự động**):
- `Controllers/TestErrorController.cs` (`/test-error/*`) — route test lỗi đang mở công khai trên production.
- `wwwroot/downloads/MoonPhim.apk` + `MoonPhim-v2.apk` (~40MB trong git) — có thể còn đang được tải; hỏi trước.
- `wwwroot/images/uploads/avatars/*` bị commit vào git — dữ liệu user không nên nằm trong repo.
- `Hubs/ChatbotHub.cs` — không có `MapHub`, không JS nào kết nối tới.
- `Controllers/NangCapController.cs.cs` — tên file đuôi kép (chỉ đổi tên, không đổi class/route).
- Trùng lặp: 2 `apple-touch-icon`, 2 manifest (`manifest.json` + `site.webmanifest`) trong `_Layout`.
- Package nghi thừa trong `MovieWeb.csproj`: `Microsoft.AspNetCore.Mvc.Core 2.3.0`, `Microsoft.AspNetCore.SignalR.Core 1.1.0`
  (đã nằm trong shared framework .NET 9), `SendGrid.*` (đã chuyển sang SMTP Brevo?), `Microsoft.AspNetCore.Components.Web`.

---

## 5. Lỗ hổng bảo mật đã ghi nhận (ưu tiên xử lý, mỗi mục một commit)

| # | Vấn đề | Vị trí | Ghi chú |
|---|--------|--------|---------|
| S1 | **Turnstile SecretKey bị commit** vào git | `appsettings.json` | Chủ dự án phải **rotate key trên Cloudflare**; code chuyển sang env `Turnstile__SecretKey` (compose đã có). |
| S2 | CORS `AllowAnyOrigin/Method/Header` cho toàn app | `Program.cs` (`AllowAll`) | Thu hẹp về domain của mình + app mobile; kiểm tra app Flutter trước. |
| S3 | Route test lỗi công khai | `TestErrorController` | Chỉ map ở Development hoặc xóa. |
| S4 | `ForwardedHeaders` tin mọi proxy (`KnownNetworks/Proxies.Clear()`) | `Program.cs` | Giới hạn về mạng docker `moonphim_net`. |
| S5 | `add_header` trong `location` static của nginx **ghi đè** toàn bộ security headers ở cấp server | `nginx/conf.d/moonphim.conf` | Chỉ sửa khi chủ dự án đồng ý (luật 3). |
| S6 | CSP rất lỏng (`'unsafe-eval'`, `https:` cho mọi thứ) | nginx | Siết dần, test kỹ player/ads/Stripe. |
| S7 | Hangfire filter gọi `.Result` (sync-over-async) | `Filters/HangfireAuthorizationFilter.cs` | Rủi ro thấp, sửa khi tiện. |
| S8 | Không có rate limiting cho login/register/API/chatbot | `Program.cs` | Thêm `AddRateLimiter` .NET built-in. |
| S9 | Auto-migrate khi khởi động trên production | `Program.cs` | Giữ nguyên trong V2, nhưng tuân thủ luật 5. |

---

## 6. Quyết định đã chốt / đang chờ chủ dự án

> Agent thực thi: mục nào ghi **CHỜ** thì không được tự quyết — hỏi chủ dự án.

- [CHỜ] Cách dùng animate-ui trong dự án Razor (port sang CSS + Motion vanilla JS, hay dựng "island" React riêng).
- [CHỜ] Vị trí file thiết kế giao diện V2 và file plan `docs/MOONPHIM_V2_UPGRADE_PLAN.md` (hiện bị `.gitignore` chặn `*.md`
  nên chưa có trong repo — cloud session không đọc được).
- [CHỜ] Có được tạo môi trường staging (subdomain riêng) hay chỉ test localhost.
- [CHỜ] App mobile (Flutter/APK) đang dùng những endpoint nào — cần danh sách trước khi đụng `Controllers/API/**` hoặc CORS.

---

## 7. Giao tiếp

- Trả lời chủ dự án bằng **tiếng Việt**.
- Không chắc → hỏi. Không bao giờ "đoán cho nhanh" trên production.
- Báo cáo cuối mỗi task: đã đổi gì (file), đã test gì, rủi ro còn lại, cách revert.
