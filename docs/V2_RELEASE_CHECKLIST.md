# V2 UI — Checklist phát hành (nhánh `v2/ui-b`)

> Soạn 01/11/2026 cho chủ dự án. Agent KHÔNG merge/push. Mặc định `FeatureFlags:UiV2 = false` → merge xong site vẫn là V1 y như `main`.

## 1. Commit cần đưa lên
Nhánh `v2/ui-b` hơn `main` (`1029503`) 66+ commit. Gom theo nhóm (xem `git log main..v2/ui-b`):

| Nhóm | Nội dung | Rủi ro với V1 |
|---|---|---|
| Hạ tầng V2 | flag `UiV2`, `?ui=v2`, `Views/V2/**`, Tailwind `ClientApp-v2`, `wwwroot/v2/**` | Thấp (chỉ chạy khi bật flag/cookie admin) |
| Trang V2 | trang chủ, danh sách, chi tiết/player, tìm kiếm, trending, auth, About/Contact/Privacy/DMCA, 8 trang cá nhân, nâng cấp gói, Watch Party, landing `/`, Admin Dashboard | Thấp |
| Trạm ảnh WebP | `Controllers/ImageProxyController.cs`, `Helpers/ImageHelper.cs`, SkiaSharp (`MovieWeb.csproj`) | **Trung bình**: chạy trên mọi trang V2; V1 không dùng. Đã thử Docker Linux (mục 3) |
| Sitemap | `SitemapController`: `/sitemap.xml` thành **sitemapindex** + `/sitemap-episodes-N.xml` | **Trung bình**: ảnh hưởng SEO ngay cả khi flag tắt |
| Backend đọc-only | cột bổ sung ở Movie/Category/Country controller (chỉ dùng cho V2) | Thấp |
| Sửa lỗi phân trang | kẹp `page >= 1` trước `Skip` (sửa lỗi Npgsql `OFFSET must not be negative`) | Thấp, có lợi cho V1 |
| Test/docs | `tests/**`, `docs/V2_UI_PROGRESS.md`, prompt | Không chạy trên server |

Không có migration EF Core nào trong nhánh này.

## 2. File KHÔNG được đưa vào nhánh public
- `ops/server-snapshot-20261101/**` và `ops/server-sync-runbook.txt` (cấu hình nginx/IP server) — đang được **track** trong nhánh; nên bỏ khỏi bản merge hoặc để ở repo private.
- `CLAUDE.md`, `docs/V2_HANDOFF.md`: chứa host/IP/đường dẫn key SSH — kiểm tra trước khi public.
- Không track (đã đúng): `docs/AWS-*.md`, `docs/SERVER_SETUP_*.md`, `.agents/**`, `tools/*.py`, `stitch_moonphim_*`, `*.pem`, `.env*`.
- `App_Data/` đã nằm trong `.gitignore` (cache ảnh).

## 3. Kết quả kiểm tra kỹ thuật (local)
- `dotnet build -c Release`: 0 lỗi.
- `python tests/v2_parity.py http://localhost:5001`: 20 `[OK]`, không `[FAIL]`, không `[V1 fallback]` (kể cả `/`).
- Khi **không** có cookie `moonphim_ui=v2`: `/`, `/trang-chu`, `/nang-cap`, `/watch-party`, `/Admin` đều ra V1 (không nạp `v2.css`).
- Trạm ảnh trong **Docker Linux** (`docker build` từ Dockerfile hiện tại, RAM giới hạn 768 MB): SkiaSharp chạy được, không `DllNotFoundException`; 105/105 ảnh trả `200 image/webp`; 20 request đồng thời cùng ảnh + 20 ảnh khác nhau: 0 body rỗng, 0 file `.tmp` mồ côi; RAM đỉnh ~230 MiB.
- Watch Party: tạo phòng, 2 phiên cùng vào, chat realtime — Chromium + WebKit đều OK.

## 4. Việc chủ dự án cần duyệt / làm trước khi bật
1. **Volume `App_Data/img-cache` trong docker-compose** (hiện cache mất mỗi lần `up` lại container → ảnh phải nén lại). Không tự sửa compose (luật 3).
2. **Hạ log SQL EF Core ở Production** (log đầy câu SELECT, tốn đĩa/CPU) — cần sửa `appsettings.Production.json`/`Program.cs`.
3. **Submit lại `/sitemap.xml` trong Google Search Console** (nay là sitemapindex) sau khi deploy; kiểm tra `/sitemap-main.xml`, `/sitemap-episodes-1.xml` trả 200.
4. Nginx `sub_filter` Monetag: nếu bật V2, kiểm tra lại quảng cáo (`ops/.../moonphim-ads-loader.inc` khớp inline script `_Layout`, không khớp `_LayoutV2`).
5. **Stripe test-mode chưa test được** trên trang nâng cấp/lịch sử thanh toán (máy local không có test key). Cần test bằng thẻ 4242… trước khi bật V2 cho user: tạo gói → lịch sử thanh toán có dữ liệu → hủy gói.
6. Test trên iPhone/Safari thật (Playwright WebKit chỉ gần đúng).

## 5. Cách bật / rollback
- Bật thử: admin vào `?ui=v2` (cookie 7 ngày); `?ui=v1` để tắt.
- Bật cho mọi người: `FeatureFlags:UiV2 = true` (biến môi trường `FeatureFlags__UiV2=true`), restart container `app`.
- **Rollback nhanh**: đặt lại `FeatureFlags:UiV2 = false` rồi restart `app` (không cần deploy lại).
- Rollback cứng: revert merge commit và để deploy chạy lại. Trạm ảnh/sitemap là thay đổi chạy cả khi flag tắt → revert commit tương ứng nếu cần.
- Rollback sitemap riêng: revert commit `7b3bffb` (sitemap index) rồi submit lại Search Console.

## 6. Ghi chú thêm (02/11/2026)
- **Giảm chuyển động**: toàn V2 đã bỏ `prefers-reduced-motion` theo yêu cầu chủ dự án (xem báo cáo Đợt 5). Cân nhắc lại về khả năng truy cập trước khi bật cho mọi user.
- Thêm cho V2: nút 3D, sidebar mobile, chatbot, nút lên đầu trang, popup xem dở; các thay đổi chỉ chạy khi bật `UiV2`.

---

## 7. Bổ sung Đợt 6 (06/11/2026) — Nhánh `v2/ui-rollout`

### 7.1 Cơ sở dữ liệu & Migration
- Migration mới: `20261106154147_AddUiPreferenceAndBroadcasts`.
  - Gồm 5 bảng mới: `SiteSettings`, `UserUiPreferences`, `UiFeedbacks`, `NotificationTemplates`, `AdminBroadcasts`.
  - 1 cột bổ sung: `Notifications.BroadcastId` (integer nullable, FK AdminBroadcasts).
  - Không xóa, không đổi kiểu, không sửa bất kỳ bảng/cột cũ nào.
- ⚠️ **QUAN TRỌNG: Backup DB trước khi merge/deploy**:
  - Chạy `pg_dump` tạo bản sao lưu an toàn trước khi merge code vào `main`.
  - Khi container `app` khởi động, `Database.Migrate()` sẽ tự động áp dụng migration lên cơ sở dữ liệu production.

### 7.2 Cơ chế Rollout giao diện (Chuyển đổi theo user)
- Cấu hình mặc định khi deploy: `ui.rollout = "off"` (toàn bộ khách và thành viên mặc định 100% V1, menu "Giao diện" ẩn).
- **Quy trình kích hoạt đề xuất**:
  1. **Bước 1 (Nghiệm thu nội bộ)**: Admin vào `https://moonphim.me/Admin/UiRollout` (yêu cầu quyền Admin), kiểm tra dashboard thống kê và cấu hình.
  2. **Bước 2 (Mời thành viên trải nghiệm)**:
     - Admin chuyển chế độ sang `members` và bấm "Lưu cấu hình".
     - Vào `/Admin/Notifications`, chọn tab "Mẫu thông báo" → chọn mẫu "Mời dùng thử giao diện V2" → bấm "Áp dụng mẫu này sang form soạn".
     - Chọn đối tượng "Tất cả người dùng" hoặc chọn danh sách thành viên muốn thử nghiệm trước.
     - Bấm "Gửi thông báo hệ thống" (job Hangfire chạy nền lô 1000, bắn realtime qua SignalR tới tab "Hệ thống" ở chuông thông báo).
     - Thành viên nhận thông báo, click vào chuyển tới `/user/giao-dien` để tự bật V2 và tham gia khảo sát góp ý (3 mức độ hài lòng + nhận xét).
  3. **Bước 3 (Theo dõi & Đánh giá)**:
     - Theo dõi tỷ lệ chuyển đổi, số lượng thành viên chọn V1/V2 và danh sách ý kiến đóng góp tại `/Admin/UiRollout`.
     - Có thể xuất file CSV (`/Admin/UiRollout/ExportFeedbackCsv`) để phân tích chi tiết.
  4. **Bước 4 (Phát hành toàn bộ)**:
     - Khi giao diện V2 đã ổn định và nhận phản hồi tốt, Admin chuyển chế độ sang `all` tại `/Admin/UiRollout`.
     - Lúc này khách và toàn bộ người dùng chưa chọn sẽ mặc định thấy V2. Thành viên vẫn có thể tự chọn về V1 nếu muốn tại `/user/giao-dien`.

### 7.3 Quản lý Popup thông báo (`/Admin/Announcements`)
- Đã chuyển cấu hình lưu trữ từ file json `App_Data` vào bảng `SiteSettings` trong database (không còn lo mất cấu hình sau mỗi lần deploy/recreate container).
- Hỗ trợ 4 chế độ tần suất hiển thị:
  - `once`: Hiện 1 lần duy nhất trên mỗi trình duyệt.
  - `session`: Hiện 1 lần mỗi phiên duyệt web (đóng trình duyệt mở lại sẽ hiện).
  - `interval`: Hiện lại sau mỗi N giờ.
  - `always`: Luôn hiển thị mỗi khi tải trang.
- Giới hạn tối đa số lần hiển thị (`MaxShows`) và độ trễ xuất hiện (`DelaySeconds`).
- Lọc đối tượng hiển thị (`Audience`): Tất cả / Chỉ khách / Thành viên thường / Thành viên VIP.
- Hỗ trợ hiển thị trên cả V1 và V2 (`_AnnouncementModalV2`).

### 7.4 Phương án Rollback
- **Rollback mềm (Khuyên dùng)**:
  - Truy cập ngay `/Admin/UiRollout` → chọn chế độ `off` → bấm "Lưu cấu hình".
  - Toàn bộ trang web ngay lập tức trở về V1 cho tất cả khách và thành viên (có hiệu lực trong vòng 60 giây do cache, hoặc ngay lập tức khi session refresh). Không cần restart container hay deploy lại code.
- **Rollback cứng**:
  - Revert merge commit của nhánh `v2/ui-rollout` trên git và deploy lại.
  - Toàn bộ bảng DB mới và cột `BroadcastId` là dạng chỉ thêm (nullable), hoàn toàn vô hại và không làm ảnh hưởng code cũ khi rollback.

---

## 8. Kiểm tra trước khi đưa lên `main` (Opus, 08/11/2026) — nhánh `v2/ui-rollout`

### 8.1 Đã sửa trước khi push
| Commit | Nội dung |
|---|---|
| `4423257` | Hết 301 sang **sai phim**: slug gốc tận cùng bằng số (`kung-fu-gau-truc-2`) bị hiểu là ID (local: 497/3.621 phim, gồm cả link ở trang chủ). |
| `671edf4` | Bỏ `aggregateRating` tự sinh (8.6–9.8 sao, số lượt giả) trong schema — Google coi là structured data spam. |
| `0b87080` | Ngừng track `CLAUDE.md`, `ops/server-snapshot-*` (IP server, đường dẫn key SSH, cấu hình nginx, danh sách lỗ hổng). Repo GitHub đang **công khai**. |
| `bed9156` | **Phương án C**: URL trang phim giữ `/phim/{slug}` (không đổi URL đã được Google index; slug phim là duy nhất). URL tập mới `/phim/{slug}/tap-{ep}-{id}` giữ nguyên. |
| `7c4846c` | Sitemap tập sau cờ `FeatureFlags:SitemapEpisodes` (mặc định **tắt**): `/sitemap.xml` chỉ gồm `/sitemap-main.xml`. |

### 8.2 Bắt buộc khi merge
1. **Merge kiểu squash** (`git merge --squash v2/ui-rollout`): lịch sử commit cũ vẫn chứa `CLAUDE.md` có IP server; merge thường sẽ đẩy cả lịch sử lên GitHub công khai.
2. **Backup DB** (`pg_dump`) trước khi push — migration `AddUiPreferenceAndBroadcasts` tự chạy khi app khởi động.
3. Nên xóa nhánh remote `claude/vigilant-archimedes-voq72k` (chứa bản `CLAUDE.md` cũ có danh sách lỗ hổng).

### 8.3 Quảng cáo (đã kiểm tra)
- `_Layout`, `_LandingLayout`, `_UserLayout`, `moonphim-monetag-onclick.js`, `moonphim-monetag-vignette.js` **giống hệt `main`**: zone 11708850, 11708858, `ca-pub-8229451063311920` còn nguyên. V2 chép nguyên khối Monetag/AdSense.
- `sub_filter` nginx trong `ops/server-snapshot-20261101` đã không khớp `_Layout` từ trước (cả bản `main` đang chạy) → quảng cáo production đang chạy bằng script inline trong layout; merge không đổi gì ở phần này.

### 8.4 SEO sau khi deploy
- Trang phim: `/phim/{slug}` (không đổi). Trang tập (mới): `/phim/{slug}/tap-{ep}-{id}`, link tập là `<a href>` thật, không có `?server=`.
- Schema: Movie/TVSeries (không có điểm đánh giá), TVEpisode + VideoObject, BreadcrumbList. Khối phim liên quan + "Phim mới cập nhật". IndexNow (file key ở web root).
- **Google Search Console**: chỉ gửi `https://moonphim.me/sitemap.xml` (sitemap index → `sitemap-main.xml`).
- Bật sitemap tập sau, khi tỉ lệ index trang phim vượt ~60%: `FeatureFlags__SitemapEpisodes=true` rồi restart `app`, gửi lại sitemap.
- Chưa làm (cần chủ dự án duyệt): 301 `www.moonphim.me` → `moonphim.me` (nginx, mẫu `nginx/seo-www-redirect.conf.example`); mô tả phim viết riêng (cần migration).

### 8.5 Giao diện V2 khi deploy
- `FeatureFlags:UiV2=false`, `ui.rollout` mặc định `off` → khách và thành viên đều thấy V1.
- Admin (`RoleId=1`) thử V2 trên production: `?ui=v2` (cookie 7 ngày), `?ui=v1` để quay lại.

### 8.6 Đã test (local, 08/11/2026)
- Build 0 lỗi (không thêm cảnh báo mới); `v2_parity.py` 20/20 `[OK]`.
- 98 URL phim (40 link trang chủ + mẫu 1/60 sitemap) đều 200; URL cũ tận cùng bằng số mở đúng phim; slug không tồn tại → 404.
- Canonical/og:url trang phim = `/phim/{slug}`; trang tập canonical về chính nó; không còn `aggregateRating`.
- **Chưa test**: quét toàn bộ 3.558 phim, Stripe test mode, iPhone/Safari thật, gửi FCM thật.
