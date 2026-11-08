# MoonPhim V2 — Bàn giao từ phiên cloud sang phiên local

> **Hướng dẫn cho Claude ở phiên mới (chạy trên máy Windows của anh Khôi):**
> Đọc hết file này, làm **Bước A** (lấy code về), rồi đọc `CLAUDE.md` và làm tiếp **Bước B**.
> Trả lời anh Khôi bằng tiếng Việt.

---

## Bước A — Lấy code về máy (chạy trong `D:\Doan\webnc\MovieWeb`)

1. `git status`. Nếu có file đang sửa dở thì chạy `git stash -u` (nhớ `git stash pop` lại ở cuối).
2. `git fetch origin claude/vigilant-archimedes-voq72k`
3. `git switch -c v2/dev origin/claude/vigilant-archimedes-voq72k`
   (nếu `v2/dev` đã có thì dùng `git switch v2/dev` rồi `git merge origin/claude/vigilant-archimedes-voq72k`)
4. Kiểm tra đã có `CLAUDE.md` và `docs/V2_HANDOFF.md`. **Đọc toàn bộ `CLAUDE.md`, đó là luật bắt buộc.**
5. **Không làm gì trên `main`. Không push `main`** (push `main` = tự deploy lên EC2 production).

---

## Bối cảnh (những gì đã bàn với anh Khôi)

- MoonPhim (`https://moonphim.me`) là **production đang có user, traffic ổn định**. Chỉ được sửa và test trên localhost
  hoặc staging riêng. Không được để code lỗi lên server.
- Mục tiêu V2:
  1. **Dựng lại giao diện** theo các thiết kế anh Khôi **đã có sẵn**. Việc của mình là **chuyển đổi (port) đúng thiết kế**,
     không tự sáng tác lại.
  2. **Rà soát lỗ hổng bảo mật** còn tồn đọng.
  3. **Dọn code và file thừa** để source nhẹ hơn.
  4. **Thêm animation** dùng repo https://github.com/imskyleen/animate-ui, và **nút hiệu ứng 3D lồi lõm**.
- Opus (phiên cloud) **lập kế hoạch tổng thể**, **Sonnet 5.5 thực thi** toàn bộ từ code đến test.
- Chưa có file plan nào trước đó. File `docs/MOONPHIM_V2_UPGRADE_PLAN.md` **không tồn tại**. Phiên này phải viết plan.

### Quyết định anh Khôi đã chốt
| # | Câu hỏi | Quyết định |
|---|---------|-----------|
| 1 | Plan | Claude tự lập plan tổng thể để Sonnet 5.5 thực thi |
| 2 | animate-ui (React/Motion/Tailwind) | **Dùng Node + Vite + React** (chấp nhận rủi ro cao hơn để giữ đúng hiệu ứng). **Bắt buộc giữ nguyên toàn bộ script quảng cáo và SEO đang chạy ổn định**, không xóa, không đổi zone/ID |
| 3 | Môi trường test | Tạo staging **nếu miễn phí**, nếu không thì dùng localhost |
| 4 | App mobile | Mã nguồn ở `D:\Doan\webnc\moonphim_mobile`. Đọc để lấy danh sách API app đang gọi |
| 5 | Nơi làm việc | Phiên Claude Code **local trên máy anh**, không làm qua cloud/GitHub nữa |

### Việc còn phải hỏi anh Khôi
- **File thiết kế giao diện V2 nằm ở đâu, dạng gì** (HTML/CSS tĩnh? Figma? ảnh? project React?). Plan Phase 4 phụ thuộc vào đây.
- Thư mục deploy `D:\Doan\webnc` có gì cần lưu ý (không đọc file `.env` hay key ra màn hình).
- EC2 đang dùng loại máy nào, bao nhiêu RAM (build Vite trong Docker trên EC2 có thể thiếu RAM).
- DNS `moonphim.me` có đang dùng Cloudflare không (để chọn cách làm staging).

---

## Sự thật đã xác minh trong repo (phiên cloud đã đọc code)

- **Deploy:** `.github/workflows/deploy.yml` chạy khi push `main`: SSH lên EC2 → `git pull origin main` →
  `docker compose build --no-cache app` → `up -d`. Không có test, health check hay rollback.
- **Auto-migrate:** `Program.cs` gọi `Database.Migrate()` khi khởi động, nên migration lọt vào `main` sẽ chạy thẳng lên DB thật.
- **Stack:** ASP.NET Core 9 MVC + Razor (61 file `.cshtml`), Bootstrap 5, CSS/JS thuần, jQuery, SignalR, PostgreSQL 16,
  EF Core, Hangfire, Stripe, Gemini, FCM, Turnstile. Hiện **chưa có** Node/Vite.
- `.gitignore` chặn mọi `*.md` (trừ `README.md`, `CLAUDE.md`). Muốn commit file trong `docs/` thì dùng `git add -f`.
- **Script quảng cáo phải giữ nguyên** (đang ở `Views/Shared/_Layout.cshtml`, ngoài ra còn ở `_UserLayout`, `Movie/Detail`,
  `js/mobile-bottom-nav.js`, `js/video-ads-handler.js`, partial `_Advertisement`):
  - Google AdSense `ca-pub-8229451063311920` (trong `<head>`), cùng `wwwroot/ads.txt`
  - `<meta name="monetag" content="1bd43555dd0c57f4ee0258b218db6891">`
  - Monetag Vignette zone `11708850` (`n6wxm.com/vignette.min.js`, nạp sau `load` bằng `requestIdleCallback`)
  - Monetag OnClick/Popunder zone `11708858`, cooldown 2 giờ (localStorage + cookie `moonphim_pop_done` +
    `MemoryCache` theo IP, gọi `/api/ads/record-popunder`)
- **SEO phải giữ nguyên:** JSON-LD (`application/ld+json`), meta keywords/author/og:*, canonical, title từng trang,
  `SitemapController`, `RobotsController`, URL `/phim/{slug}`, `/the-loai/{slug}`… (có ở `_Layout`, `_LandingLayout`,
  `Home/Index`, `Home/TrangChu`, `Movie/Detail|PhimLe|HoatHinh|PhimMoiCapNhat`).
- **Feature flags** có sẵn: `appsettings.json > FeatureFlags`, đọc bằng `Configuration.GetValue<bool>("FeatureFlags:...")`.
- **Lỗ hổng:** xem §5 trong `CLAUDE.md`. Quan trọng nhất là **Turnstile SecretKey đã bị commit** trong `appsettings.json`.
  Anh Khôi cần rotate key trên Cloudflare. Ngoài ra: CORS AllowAll, `/test-error/*` mở công khai, ForwardedHeaders tin mọi
  proxy, `add_header` trong location static của nginx làm mất các security header, chưa có rate limit.
- **Ứng viên code thừa:** xem §4 trong `CLAUDE.md` (TestErrorController, `Hubs/ChatbotHub.cs` không được map, APK 40MB trong git,
  avatar user trong git, package thừa trong csproj, manifest/icon bị trùng).

---

## Bước B — Việc phiên local cần làm tiếp

1. Làm Bước A. Đọc `CLAUDE.md`.
2. Đọc `D:\Doan\webnc\moonphim_mobile` → liệt kê **mọi endpoint** app gọi (method, path, cần auth hay không) vào
   `docs/MOBILE_API_CONTRACT.md`. Những endpoint này **không được phá**.
3. Hỏi anh Khôi vị trí file thiết kế V2, rồi khảo sát thiết kế (liệt kê màn hình, component, map sang view Razor tương ứng).
4. Kiểm tra máy có đủ: .NET SDK 9 (`global.json` = 9.0.301), Node 22, PostgreSQL local cổng `5433`
   (DB `moonphim_local`) hoặc Docker Desktop. Chạy được app V1 ở localhost trước khi đổi bất cứ gì.
5. Viết **`docs/MOONPHIM_V2_MASTER_PLAN.md`** dựa trên dàn ý bên dưới, bổ sung bằng dữ liệu thật vừa thu thập.
   Cập nhật §6 của `CLAUDE.md` (đánh dấu các mục đã chốt). Trình anh Khôi duyệt plan **trước khi** Sonnet bắt đầu code.

---

## Dàn ý kế hoạch tổng thể (Opus đề xuất, phiên local hoàn thiện)

### Kiến trúc frontend V2: "Razor SSR + React islands" (không chuyển sang SPA)
- **Lý do:** SEO cần HTML render sẵn từ server, script AdSense/Monetag cần nằm trong layout Razor như hiện tại, app mobile
  dùng API riêng. SPA toàn phần sẽ phá SEO và quảng cáo.
- Razor vẫn render nội dung SEO (title, meta, JSON-LD, tiêu đề phim, link, danh sách phim). React chỉ gắn vào các vùng
  tương tác (header, search, carousel, modal, nút, hiệu ứng), qua điểm gắn `<div data-island="Ten" data-props='@json'>`.
- Thư mục `ClientApp/` gồm Vite + React + TypeScript + Tailwind v4 + `motion` + component animate-ui (cài bằng `shadcn` CLI
  từ registry animate-ui). Build ra `wwwroot/dist/` kèm `manifest.json`.
- Tag helper hoặc helper Razor đọc `manifest.json` của Vite để chèn file có hash. Khi Development thì trỏ tới Vite dev server
  `localhost:5173` để có HMR.
- **Tailwind không được phá Bootstrap:** tắt preflight hoặc scope trong `.v2-root`. Tốt nhất là `_LayoutV2.cshtml` mới
  không nạp Bootstrap. Layout này phải **copy nguyên văn khối quảng cáo và SEO** từ `_Layout.cshtml`.
- **Bật/tắt:** flag `FeatureFlags:UiV2` mặc định `false`, cộng cookie xem trước chỉ cho admin. Rollback = tắt flag.
  V1 vẫn nguyên vẹn cho tới khi bật 100%.
- **Docker:** thêm stage `node:22-alpine` (`npm ci && npm run build`) trước `dotnet publish` trong `Dockerfile`. Đây là sửa
  file deploy, cần anh Khôi duyệt và phải test `docker compose build` ở local trước. Rủi ro: EC2 ít RAM thì build Vite bị OOM.
  Phương án dự phòng: build sẵn ở local và commit `wwwroot/dist`.
- **Ngân sách hiệu năng:** JS island ban đầu ≤ ~150KB gzip, CLS < 0.1, Lighthouse mobile không tụt quá 5 điểm so với baseline V1.
- **Animation và nút 3D:** chỉ animate `transform`/`opacity`, tôn trọng `prefers-reduced-motion`, test Safari/iOS (lịch sử repo
  có rất nhiều fix Safari). Nút 3D gồm lớp đáy (shadow/edge), `translateY` khi hover/press, `whileTap` của Motion; bản CSS
  thuần cho nút trong vùng Razor.

### Staging miễn phí
- Ưu tiên **Cloudflare Tunnel** chạy trên máy anh Khôi: quick tunnel `*.trycloudflare.com` miễn phí, không cần đụng DNS.
  Nếu DNS ở Cloudflare thì có thể dùng tunnel có tên `staging.moonphim.me`, cũng miễn phí.
- **Không** dựng staging trên EC2 production vì dùng chung RAM/CPU với site thật.
- Staging dùng DB local riêng (dữ liệu phim tự đồng bộ từ OPhim bằng job có sẵn), Stripe test key, Turnstile test key
  (`1x00000000000000000000AA` / `1x0000000000000000000000000000000AA`), thêm redirect URI localhost/staging cho Google OAuth.
  Không copy dữ liệu user thật.

### Các phase (mỗi phase một nhánh `v2/pX-...` tách từ `v2/dev`, Sonnet làm một phase mỗi phiên)
- **P0 — Baseline:** chạy V1 local. Chụp ảnh mọi trang (desktop, mobile 390×844, WebKit). Đo Lighthouse. Kiểm kê
  script quảng cáo và SEO trên từng loại trang → `docs/ADS_SEO_INVENTORY.md`. Viết hợp đồng API mobile. Dựng staging.
  Tạo `tag v1-baseline`.
- **P1 — Bảo mật** (độc lập, rủi ro thấp, có thể lên prod trước): chuyển Turnstile secret sang env (anh Khôi rotate key),
  `/test-error` chỉ chạy ở Development, siết CORS theo hợp đồng API mobile, giới hạn ForwardedHeaders, thêm rate limiter
  cho login/register/chatbot/API. Thay đổi nginx thì chỉ đề xuất, chờ duyệt.
- **P2 — Dọn dẹp:** xóa theo quy tắc §4 trong `CLAUDE.md`, mỗi nhóm một commit.
- **P3 — Hạ tầng frontend:** `ClientApp/` Vite/React/TS/Tailwind/animate-ui, helper manifest, `_LayoutV2`, flag + cookie xem trước,
  stage Node trong Dockerfile, script `npm run build` chạy được cả trên Windows. Phase này giao lên với flag tắt nên
  **không đổi gì với user**.
- **P4 — Port giao diện** theo thứ tự rủi ro tăng dần: khung chung (header/footer/bottom nav) → trang chủ → danh sách +
  bộ lọc → chi tiết phim → tìm kiếm → modal đăng nhập/đăng ký → profile/yêu thích/lịch sử → nâng cấp/thanh toán →
  trang thông tin → **trang xem phim/player (cuối cùng, rủi ro cao nhất)** → admin (tùy chọn, có thể giữ V1).
  Giữ nguyên mọi `id/name/data-*`, form, anti-forgery token và hook JS hiện có.
- **P5 — Animation và nút 3D:** áp dụng animate-ui và nút 3D trên toàn site V2 theo ngân sách hiệu năng.
- **P6 — QA:** Playwright e2e cho các luồng chính, **kiểm tra quảng cáo/SEO V1 và V2 giống nhau** (chạy tự động trên từng
  loại trang: AdSense, meta monetag, zone 11708850/11708858, JSON-LD, canonical, og:*, title), kiểm tra hồi quy ảnh, Lighthouse.
- **P7 — Rollout:** anh Khôi tự merge vào `main` với flag tắt → bật cookie xem trước cho admin trên prod → bật cho mọi người
  → theo dõi → dọn V1. Rollback = tắt flag.

### Tiêu chí "xong" cho mỗi task Sonnet
`dotnet build -c Release` không lỗi; `npm run build` không lỗi; smoke test theo §1 `CLAUDE.md`; test kiểm tra quảng cáo/SEO
pass; console không lỗi mới; báo cáo đã đổi gì, đã test gì, rủi ro còn lại, cách revert.
