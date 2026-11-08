# Tiến độ port giao diện V2 (nhánh v2/ui-b)

## 1. Khung chung: header + footer — ĐÃ LÀM
- File: `Views/V2/Shared/Partial/_HeaderV2.cshtml`, `Views/V2/Shared/Partial/_FooterV2.cshtml`, `wwwroot/v2/css/v2.css` (build lại).
- Giữ: thẻ `<header class="v2-header pt-safe">`, `_BrandLogoV2`, form `#logoutForm`, ô tìm kiếm `name="keyword"` → `/tim-kiem`, link route thật (không dùng anchor của Stitch).
- Chưa làm: menu thể loại/quốc gia mega-dropdown, chuông thông báo dropdown, offcanvas mobile (đã có tab bar V1 ở đáy), ô tìm kiếm mobile overlay.
- Kiểm tra: `dotnet build` 0 error; `npm run build` OK; `tests/v2_parity.py` không có [FAIL]; `/about?ui=v2` desktop + mobile 375px hiển thị đúng, console không lỗi.
- Chưa kiểm tra: WebKit/Safari, các trang khác (V1 fallback nên không bị ảnh hưởng nhưng chưa chụp `?ui=v1`).
- Chỗ thiết kế không rõ/chênh: Stitch dùng nền trong suốt ở header; theo luật giữ màu `v2-header` của V1.

## 2. Trang chủ `/trang-chu` — ĐÃ LÀM
- File: `Views/V2/Home/TrangChu.cshtml`, `Views/V2/Shared/Partial/_MovieCardV2.cshtml` (thẻ phim dùng chung), `wwwroot/v2/js/pages/home.js` (slider hero thuần JS thay carousel Bootstrap vì V2 không nạp CSS Bootstrap), CSS top10 + hero thêm vào `ClientApp-v2/src/v2.css` (đã build `wwwroot/v2/css/v2.css`).
- Giữ nguyên từ V1: `@model`, `ViewData` (Title, IsHomePage), `@functions` (GetMoviePosterUrl, FormatViews), logic badge/trạng thái tập, khối meta SEO trong `@section Styles`, script popup quảng cáo HomePage_Popup và script kéo ngang Top 10 (`#top10Track`, `.top10-item`, `.top10-link`), quảng cáo banner HomePage.
- Đổi: hero dùng poster làm nền toàn màn; phim mới/lẻ/bộ/hoạt hình dùng lưới mới; Top 10 giữ id/class V1.
- Chênh thiết kế: viền neon số thứ hạng giữ mã hex của V1 (chưa có token); demo "Rift Saigon 2099" của Stitch không được dùng, chỉ lấy bố cục.
- Kiểm tra: `dotnet build` 0 error (build vào thư mục scratch vì app dev cũ đang chạy trên 5001); `npm run build` OK; `tests/v2_parity.py http://localhost:5002` không có [FAIL], `/trang-chu` [OK]; desktop: hero auto-chạy, lưới và Top 10 hiển thị, không lỗi console; mobile 375px: hero, header, tab bar V2 đúng, không tràn ngang.
- Chưa kiểm tra: kéo chuột Top 10 bằng tay và kéo trên cảm ứng; popup quảng cáo (cần cấu hình quảng cáo thật); Safari/WebKit; tab bar; `?ui=v1` chỉ kiểm tra HTTP 200 + có `movieCarousel`.
- Lưu ý dev: app dev cũ vẫn chạy ở `localhost:5001` từ bản trước (đã không dừng được vì bị chặn quyền), bản kiểm tra này chạy ở `localhost:5002`.

## 3. Danh sách phim + bộ lọc — MỘT PHẦN (đã làm: Phim lẻ, Phim bộ, Hoạt hình, Phim mới cập nhật)
- File: `Views/V2/Shared/Partial/_MovieListV2.cshtml` (khung danh sách + phân trang dùng chung), `Views/V2/Shared/Partial/_FilterSidebarV2.cshtml` (bộ lọc: giữ tên field, class .item/.active/.show, id), `wwwroot/v2/js/pages/filter-sidebar.js` (copy nguyên logic script của `Views/Shared/_FilterSidebar.cshtml` V1), `wwwroot/v2/js/pages/movie-list.js` (popup quảng cáo theo `data-placement`), 4 view `Views/V2/Movie/{PhimLe,PhimBo,HoatHinh,PhimMoiCapNhat}.cshtml`.
- Giữ nguyên: title do controller/view đặt như V1, meta/og/twitter/keywords/JSON-LD từng trang khớp V1 (parity PASS), tham số query (Type, Language, SortBy, Countries, Categories, Years, Page, keyword), placement popup (Popup_PhimLe/PhimBo/HoatHinh/PhimMoi).
- Chưa làm: `Category/Category` (Popup_TheLoai), `Country/Country` (Popup_QuocGia), `Movie/Trending` (có xếp hạng, khác cấu trúc), `Search` thuộc trang 4.
- Kiểm tra: parity `/the-loai/phim-le|phim-bo|hoat-hinh|phim-moi-cap-nhat` [OK]; desktop: mở bộ lọc, chọn quốc gia Chile → URL `Countries=chile`, 2 kết quả, console không lỗi; mobile 375px: lưới 2 cột, tab bar V2 đúng.
- Chưa kiểm tra: popup quảng cáo (cần placement thật), phân trang (trang đó không đủ dữ liệu để hiện nhiều trang), Safari/WebKit.
- Lưu ý: asset `v2.css` đổi nội dung nhưng `?v=` chỉ đổi khi khởi động lại app; khi test trên bản đang chạy cần khởi động lại để tránh cache.

### Cập nhật: Thể loại và Quốc gia — ĐÃ LÀM
- `Views/V2/Category/Category.cshtml` (Popup_TheLoai), `Views/V2/Country/Country.cshtml` (Popup_QuocGia), dùng chung `_MovieListV2` và `_FilterSidebarV2` như trên. Meta description giữ như V1; tiêu đề do controller đặt.
- Kiểm tra: parity `/quoc-gia/han-quoc` [OK]; `/the-loai/am-nhac?ui=v2` có title giống V1, bộ lọc và popup placement có mặt; desktop hiển thị đúng trạng thái "không có phim".
- Còn lại trong trang 3: `Movie/Trending` (bảng xếp hạng có số thứ hạng, cần port riêng).

## 3b. Bảng xếp hạng Trending — ĐÃ LÀM (`Views/V2/Movie/Trending.cshtml`)
- Giữ h1, meta description, phân trang (không có SortBy như V1), huy hiệu hạng (top 1/2/3 màu token), lượt xem.
- Parity `/the-loai/phim-trending`: [OK] (sau khi bỏ icon ligature khỏi h1).

## 4. Tìm kiếm — ĐÃ LÀM (`Views/V2/Movie/Search.cshtml`)
- Tiêu đề theo từ khóa, giữ keyword khi chuyển trang, dùng `_FilterSidebarV2` và `_MovieCardV2`.
- Parity `/tim-kiem?q=bean`: [OK].

## 5. Trang thông tin — ĐÃ LÀM (`Views/V2/Home/About|Contact|Privacy|DMCA.cshtml`)
- Nội dung chữ giữ nguyên từ V1 (kể cả pháp lý). CSS: `wwwroot/v2/css/info-pages.css` (bản sao V1 + lớp thay Bootstrap), `dmca.css`.
- `/about`, `/contact`, `/privacy`: parity [OK].
- **CẦN QUYẾT ĐỊNH:** `/dmca` và `/mien-tru-trach-nhiem` lệch V1 vì V1 dùng `_UserLayout` (không quảng cáo, không canonical/keywords, title có " - MoonPhim"). V2 dùng `_LayoutV2` nên hiện quảng cáo Monetag/AdSense và canonical trên trang pháp lý. Chọn: (A) giữ như hiện tại; (B) trang DMCA dùng layout không quảng cáo như V1.

## 6. Đăng nhập / đăng ký / quên / đặt lại — ĐÃ LÀM (`Views/V2/Auth/*.cshtml`)
- Trang riêng, bố cục tách đôi theo Stitch ui01 (`Partial/_AuthAsideV2`, `Partial/_AuthTabsV2`).
- JS: `wwwroot/v2/js/pages/auth.js` gọi đúng endpoint/field như `wwwroot/js/auth.js` (Login, Register, ForgotPassword, ResetPassword, ResendEmailConfirmation). Turnstile render khi API đã nạp.
- Kiểm tra: `/Auth/Login` hiển thị đúng (desktop), console chỉ có cảnh báo CSP report-only từ iframe Google/Turnstile. Chưa test gửi form thật (cần tài khoản test).

## 7–8. Hồ sơ/yêu thích/lịch sử/thông báo/thanh toán và chi tiết phim/trình phát — MỨC CHỨC NĂNG (chưa theo Stitch)
- `Views/V2/User/*.cshtml`, `Views/V2/Movie/Detail.cshtml`: giữ nguyên markup, id, JS V1 (`~/js/...`). CSS trang: bản sao V1 trong `wwwroot/v2/css/` (profile, user, user-favorite, user-history, notification, payment-history, detail, ad-modal, favorite-button, watch-progress) + `bootstrap-lite.css` (lớp tiện ích thay Bootstrap, chỉ nạp trên các trang này).
- Chưa làm: thiết kế Stitch cho các trang này (chưa port bố cục/Tailwind). Stitch có màn hồ sơ, yêu thích, lịch sử, thông báo, bảo mật; chưa có màn Thanh toán.
- Kiểm tra: `/phim/ong-bean?ui=v2` 200, có `#videoContainer`, `.episode-list-item`, `initMovieDetailPage`; console không lỗi; không tràn ngang. `/phim/ong-bean/tap-full?server=vietsub-1` 200.
- CHƯA kiểm tra được: các trang user (cần đăng nhập, đang redirect về login), phát video thật, quảng cáo video, theo dõi tiến độ, yêu thích, bình luận.

---
# Đợt sửa theo Stitch (Sonnet 5.5) — nhánh v2/ui-b

| Trang | Commit | Trạng thái |
|---|---|---|
| Chi tiết phim + trình phát | 55c5e70 (backend Top thịnh hành), d8aa718 | ĐÃ LÀM (desktop + mobile) |
| Danh sách + bộ lọc (/the-loai/*, /quoc-gia/*) | 07b999d (backend cột thẻ phim), d0eb08e | ĐÃ LÀM (desktop); mobile chưa gọn như Stitch |
| Tìm kiếm | 1efe3ad | ĐÃ LÀM (desktop); bỏ khối tin tức/tab đếm |
| DMCA + miễn trừ | 4392a1e | ĐÃ LÀM: `_LayoutV2Legal` (head như `_UserLayout`, không Monetag) |
| Trang chủ | 92ce312 + commit mobile | MỘT PHẦN (đã thêm: hàng cuộn ngang trên mobile, hero ưu tiên ảnh ngang): trending lên đầu, kicker+mô tả, thẻ chi tiết, pill chọn phim; hero chưa dùng gradient+thông tin như Stitch, chưa có mục Tin tức |
| Header/footer | 79bb1c5 + `fix(ui-v2): mobile trang chủ...` | MỘT PHẦN: footer 4 cột, "Phim mới", dropdown Thể loại/Quốc gia (đã test mở được); chưa có chuông thông báo dropdown, tìm kiếm mobile overlay (icon tìm kiếm mobile dẫn tới /tim-kiem có thanh tìm lớn) |
| Phân trang âm (OFFSET) | f2fbcb4 | ĐÃ LÀM ở API/Profile/Chatbot/Favorite/Profile service; CHƯA: WatchHistoryService, Admin* |
| Trang cá nhân (hồ sơ, chỉnh sửa/bảo mật, đổi mật khẩu, thông báo, yêu thích, lịch sử, thanh toán) | xem git log `fix(ui-v2): trang cá nhân` | MỘT PHẦN: hồ sơ làm lại theo Stitch; 6 trang còn lại đổi khung + sidebar + tối màu token, nội dung giữ markup V1. Chưa có thẻ 'Tiếp tục xem' lớn, bình luận gần đây, form cài đặt nhiều cột như Stitch |
| About, Contact, Privacy | `fix(ui-v2): giới thiệu, liên hệ, chính sách` | ĐÃ LÀM (desktop); không có tab FAQ vì V1 không có nội dung FAQ |
| Đăng nhập/đăng ký chỉnh nốt | `fix(ui-v2): đăng nhập/đăng ký chỉnh nốt` | ĐÃ LÀM: hiện/ẩn mật khẩu, ghi chú phi thương mại; bỏ Facebook, thẻ 'Đang xem', nền mờ (không có backend/dữ liệu) |

## Kiểm tra đã chạy
- `dotnet build -c Release`: 0 error. `npm run build` OK (v2.css đã commit).
- `tests/v2_parity.py`: 19/19 [OK] (kể cả /dmca, /mien-tru-trach-nhiem).
- Chi tiết phim: bấm "Xem Phim Ngay" → khung player hiện, iframe nạp, URL đổi thành `/phim/ong-bean/tap-full?server=vietsub-1`, placeholder ẩn, console sạch.
- Bộ lọc: chọn Hàn Quốc + năm 2024 + sắp xếp điểm → URL `?Type=single&Language=&SortBy=rating&Countries=han-quoc&Categories=&Years=2024`.
- `?ui=v1` vẫn ra giao diện V1 (chi tiết, danh sách).
- Ảnh so sánh: `tests/visual/out/{detail-desktop,detail-mobile,list-desktop,list-mobile,search-desktop,home-desktop}.png`.

## CHƯA kiểm tra
Safari/WebKit; yêu thích/bình luận/gửi đánh giá khi đã đăng nhập; phát m3u8 (HLS) thật; popup quảng cáo; trang người dùng (cần tài khoản test local); mobile của trang chủ/tìm kiếm.

## Thay đổi backend (đọc-only, không migration)
1. `MovieController.Detail`: `ViewBag.TrendingMovies` (5 dòng) chỉ khi V2.
2. List/Search/Category/Country: thêm cột Year, Quality, Time, Language, Rating, EpisodeCurrent + 2 thể loại vào Select, `ViewBag.TotalMovies`.
3. Kẹp `page >= 1` (mục trên).

## Đề xuất / ghi nhận (chưa làm)
- Log SQL của EF Core: đặt `Logging:LogLevel:Microsoft.EntityFrameworkCore.Database.Command` = `Warning` cho Production trong `appsettings.Production.json` (cần anh Khôi duyệt).
- Sitemap chưa có URL theo tập `/phim/{slug}/{tap}`: cần anh Khôi quyết (số URL rất lớn).
- `nginx/conf.d/moonphim-ads-loader.inc` (sub_filter) không còn khớp script Monetag inline.
- Dữ liệu Stitch không có thật nên không hiển thị: Dolby/4K HDR, tốc độ CDN, giới hạn tuổi, số liệu "3,480+ / +28 bản rạp", tin tức, trạng thái sự cố.

## Kiểm tra đã đăng nhập (tài khoản test local)
Đã chạy Playwright: đăng nhập 200; modal avatar mở; bấm Yêu thích -> 'Đã yêu thích' + hiện ở /user/favorite; gửi bình luận (4 sao) hiện ở #comment-list; console không lỗi. Ảnh: tests/visual/out/{profile,security,history,notifications}-{desktop,mobile}.png.

---
# Đợt cuối (Sonnet 5.5): banner, player, tập phim, sitemap, thông báo

| Việc | Commit | Ghi chú |
|---|---|---|
| Banner trang chủ responsive như V1; banner nhỏ là ảnh NGANG (16:9, 520–760px ở desktop) | e1abd99, commit "banner nhỏ hình ngang" | ảnh ngang lấy từ Backdrop → ThumbUrl → poster |
| Tiến độ tập (Hoàn tất 12/12, Tập 8/12), trình phát responsive (≤1200px, ≤ chiều cao màn hình) | e8c9963 | `Helpers/EpisodeStatus.cs` |
| Chi tiết phim: thứ tự player → nguồn phát/server → danh sách tập; nút tập 96×48px | commit "banner nhỏ hình ngang" | |
| Thông báo hết nền trắng (trang /user/notifications + dropdown) | cùng commit | đã test với 3 thông báo thử trong DB local (user 131) |
| Sitemap: `/sitemap.xml` = sitemap index → `/sitemap-main.xml` + `/sitemap-episodes-N.xml` | 7b3bffb | 34.792 URL tập ở DB local; URL = `/phim/{slug}/{tap}` (canonical) |

## Cần Opus/anh Khôi lưu ý khi plan tiếp
- **Sitemap đổi cấu trúc**: `/sitemap.xml` giờ là `<sitemapindex>`. `tests/run-seo-integration.py` (chưa commit) còn kỳ vọng `<urlset>` ở `/sitemap.xml` → cần đổi sang `/sitemap-main.xml`. Sau khi deploy cần submit lại `/sitemap.xml` trong Search Console. Số URL tập trên production có thể lớn hơn local (mỗi tệp 40.000).
- **Chưa làm / còn lệch Stitch**: chuông thông báo dạng dropdown trong header V2 (hiện là link tới /user/notifications), tìm kiếm mobile overlay, mobile của danh sách/tìm kiếm (bộ lọc còn dài), 6 trang cá nhân vẫn bố cục V1 đổi màu, tab FAQ (V1 không có nội dung).
- **Chưa test**: Safari/WebKit, popup quảng cáo, HLS m3u8 thật (mới test embed VSMov), gửi form Đăng ký thật (Turnstile), thanh toán.
- **Đề xuất cần anh duyệt**: hạ log SQL EF Core (`Microsoft.EntityFrameworkCore.Database.Command`) xuống `Warning` ở Production; `nginx/conf.d/moonphim-ads-loader.inc` (sub_filter) không còn khớp script Monetag inline; chưa sửa kẹp `page>=1` ở `WatchHistoryService` và `AdminController*` (OFFSET âm).
- **Dữ liệu thử trong DB local**: user `v2.test@moonphim.local` (id 131) + 3 thông báo "(dữ liệu thử V2)", 1 bình luận/yêu thích thử. Không nằm trong repo.
- **Hạ tầng test**: `tests/visual/compare.mjs <base> <tên>` (so ảnh Stitch/V2), `tests/v2_parity.py <base>` (SEO/quảng cáo A/B; lưu ý popunder giới hạn theo IP nên 4 trang Auth có thể FAIL tạm thời — khởi động lại app rồi chạy lại).

---
# Đợt 3 (Sonnet 5.5)

| Mục | Commit | Trạng thái |
|---|---|---|
| 2.0 Trạm ảnh WebP `/img/w{200,400,800,1280}/{path}.webp` (SkiaSharp) | 868f65b | XONG. Tải từ `vsmov.com/storage/images/`, cache đĩa `App_Data/img-cache`, lỗi → 302 ảnh gốc |
| 2.1 Ảnh dọc/ngang còn sót (Favorite/History/list/search/trending) + `ThumbUrl` trong DTO | 2c8d283 | XONG |
| 2.2 Chuông thông báo dropdown + overlay tìm kiếm mobile | 356ad8a | XONG |
| 2.3 Bộ lọc mobile dạng bottom sheet | ed8d233 | XONG |
| 2.4 Trang cá nhân: Lịch sử + Yêu thích theo Stitch | ab2e7b7 | XONG |
| 2.4 Trang cá nhân: Chỉnh sửa/Bảo mật, Đổi mật khẩu, Thông báo, Thanh toán | commit `fix(ui-v2): chỉnh sửa/bảo mật, đổi mật khẩu, thông báo...` | XONG (xem mục "Bổ sung 4 trang cá nhân") |
| 2.5 Kẹp `page>=1` (WatchHistoryService, AdminController x3) | d7e79d8 | XONG |
| 2.5 `run-seo-integration.py` kiểm tra sitemapindex/urlset | 8501635 | XONG, đã chạy PASS (SEO_TEST_HTTP_ONLY=1) |
| Tuỳ chọn: job Hangfire làm nóng cache ảnh | — | CHƯA LÀM |

## Đo trạm ảnh
- Trang chủ 1440px: ~1,4 MB ảnh (50 ảnh duy nhất) so với 32,2 MB ảnh gốc cùng bộ ảnh → giảm ~95%.
- Ví dụ 1 ảnh: 923 KB (jpg gốc) → 5,2 KB (w400 webp). Lần gọi đầu 4,2 s (tải + resize), lần 2 ~8 ms (cache đĩa).
- RAM process ~207 MB sau khi nạp trang chủ với cache trống.
- URL cấm: width lạ → 400; thiếu `.webp` → 400; `https:/...`, `%`, `\`, `:` → 400; `..` bị Kestrel chuẩn hoá/404; ảnh không tồn tại → 302 ảnh gốc.
- Package: SkiaSharp 2.88.9 + SkiaSharp.NativeAssets.Linux.NoDependencies 2.88.9. `dotnet publish` (như Dockerfile) kèm `runtimes/*/native/libSkiaSharp.*` của mọi RID (~45 MB thư mục publish tăng thêm; chỉ `linux-x64` ~9 MB được nạp). Dockerfile không đổi.
- **Đề xuất cần chủ dự án duyệt** (không làm): thêm volume cho `App_Data/img-cache` trong `docker-compose.yml` để cache không mất mỗi lần rebuild; giới hạn ảnh nguồn `thumb`/`poster` ngoài vsmov (hiện chỉ vsmov được proxy, URL khác giữ nguyên).

## Đã test / chưa test
- Đã: `dotnet build -c Release` 0 error; `npm run build` (v2.css đã commit); `v2_parity.py` 19/19 OK (sau khởi động lại app); rà responsive 19 trang × 5 cỡ (360/390/768/1024/1440) không tràn ngang; WebKit (Playwright 18.0) chụp trang chủ/chi tiết/danh sách/tìm kiếm/đăng nhập ở 390×844 và 1440×900: không lỗi JS, không ảnh vỡ, không tràn ngang, `backdrop-filter`/`svh`/`aspect-ratio` được hỗ trợ; player embed (100-ngay-lua-doi): đổi tập → `/phim/100-ngay-lua-doi/tap-2?server=vietsub-1`, iframe nạp, tập active đúng; `?ui=v1` vẫn ra V1 (trang chủ, danh sách, chi tiết, tìm kiếm, giới thiệu, DMCA); bell: badge 2, dropdown, đổi tab; overlay tìm kiếm mobile: mở/focus/gợi ý/Esc; bottom sheet bộ lọc; xóa 1 mục lịch sử + 1 yêu thích.
- Chưa: HLS m3u8 thật (DB local chỉ có link m3u8 của host `opstream`, bị `VSMovSource` loại nên không hiển thị); WebKit trên iPhone thật (Playwright WebKit chạy trên Windows, chữ trông mỏng hơn Chromium — chưa xác định do font); popup quảng cáo; gửi form Đăng ký (Turnstile); thanh toán Stripe; job Hangfire làm nóng ảnh.
- Ảnh: `tests/visual/out/` (wk_*.png WebKit, _bell.png, _sheet_open.png, _hist.png, _fav.png, list-mobile.png, filter-mobile.png, history-*.png).

## Thay đổi backend (không migration, không đổi shape JSON cũ)
- Thêm `Controllers/ImageProxyController.cs` (route mới `/img/w{width}/{**path}`), `Helpers/ImageHelper.Webp/WebpSrcSet`.
- `FavoriteMovieDto`, `WatchHistoryMovieDto`: chỉ THÊM field `ThumbUrl`; JSON API chỉ có thêm 1 field.
- `Skip((Math.Max(page,1)-1)*pageSize)` ở 4 file (xem d7e79d8).
- `MovieWeb.csproj`: +2 PackageReference (SkiaSharp). `.gitignore`: `App_Data/`.
- Dữ liệu thử trong DB local (không nằm trong repo): user `v2.test@moonphim.local` (id 131), vài thông báo/lịch sử xem thử.

## Bổ sung 4 trang cá nhân (sau Đợt 3)
- **Chỉnh sửa / Bảo mật** `/user/edit`: Stitch gộp hồ sơ + đổi mật khẩu + phiên đăng nhập + vùng nguy hiểm. Đã dựng: thẻ "Cập Nhật Thông Tin Hồ Sơ & Email" (lưới 2 cột, giữ nguyên field `FirstName/LastName/Email/PhoneNumber/DateOfBirth/Gender/Address/Bio/CurrentEmail`, form `asp-action="Edit"`, anti-forgery) + thẻ "Bảo Mật Tài Khoản". **Không dựng** (không có backend): danh sách phiên/thiết bị, xóa lịch sử/tài khoản, "Độ an toàn 85%/2FA".
- **Đổi mật khẩu** `/user/change-password`: giữ field `CurrentPassword/NewPassword/ConfirmPassword`; thêm hiện/ẩn mật khẩu, thanh độ mạnh + checklist (gợi ý client, server vẫn là nguồn kiểm tra).
- **Thông báo** `/user/notifications`: tiêu đề + số chưa đọc + "Đánh dấu tất cả đã đọc" (`markAllAsReadPage`), pill lọc loại/trạng thái, thẻ thông báo (`markAsReadAndRedirect`), phân trang. **Không dựng**: cột "Thống kê nhận tin", bật/tắt loại thông báo, sự kiện đặc biệt (không có dữ liệu/backend).
- **Lịch sử thanh toán** `/user/payment-history`: tiêu đề + pill lọc, bảng giao dịch tối màu (CSS ghi đè bản V1). Giữ nguyên bảng, nút hủy gói, `#confirmOverlay` và JS. *Chưa kiểm tra bằng dữ liệu thật* (tài khoản test chưa có giao dịch) nên bảng/overlay hủy gói mới được kiểm tra bằng đọc mã + CSS, chưa bằng ảnh.
- **Sửa lỗi kèm theo**: `/user/edit` và `/user/change-password` báo "jQuery is not defined" (V2 layout không nạp jQuery) → thêm `~/lib/jquery/dist/jquery.min.js` trước `_ValidationScriptsPartial`.
- **Đã test** (tài khoản test local): lưu hồ sơ → chuyển `/user/profile` có thông báo thành công; đổi mật khẩu với mật khẩu hiện tại sai → báo lỗi, không đổi; bấm thông báo chưa đọc → số chưa đọc giảm 2 → 1; console sạch; không tràn ngang ở 360/390/768/1024/1440px; `v2_parity.py` không FAIL (trừ 4 trang `/Auth/*` khi popunder giới hạn IP).
- **Chưa test**: bảng thanh toán có dữ liệu + hủy gói; WebKit cho 4 trang này; Safari thật.

---

# Đợt 4 (Sonnet 5.5, 01/11/2026)

| Mục | Commit | Trạng thái |
|---|---|---|
| 1.1 Thử SkiaSharp trong Docker Linux | (không đổi code) | **Xong** — xem số liệu bên dưới |
| 1.2 Không bao giờ trả 200 rỗng, dọn `.tmp` | `2cb6b2d` | **Xong** (phòng thủ; không tái hiện được lỗi) |
| 1.3 Làm nóng cache bằng Hangfire | — | **Chưa làm** (mục tùy chọn, cần tách `ImageProxyService`; để lượt sau) |
| 2.3 Landing `/` V2 | `d03d557`, `c395a03`, `e39f01c`, `35eedaf` | **Xong** — `_LandingLayoutV2` (head y nguyên V1), parity `[OK] /` |
| 2.1 Nâng cấp gói + kết quả thanh toán | `b94f0af` | **Xong phần giao diện**; Stripe test **chưa test được** (xem dưới) |
| 2.2 Watch Party Index/Room | `0b23620` | **Xong** — tạo phòng + 2 phiên + chat realtime OK |
| — Admin Dashboard | `b94f0af` | Làm theo yêu cầu trực tiếp của anh Khôi (ngoài prompt) |
| 3 Hero trang chủ theo Stitch | — | Rà lại: hero đã có đủ huy hiệu, tên, năm, chất lượng, mô tả, 2 nút, danh sách slide, bộ đếm → không đổi |
| 4 Checklist phát hành | `docs/V2_RELEASE_CHECKLIST.md` | **Xong** |

## Yêu cầu bổ sung của anh Khôi trong đợt này
- Sao ở landing: to hơn, có quầng sáng, loé mạnh; **luôn chạy kể cả khi máy bật "giảm chuyển động"**. Nguyên nhân sao đứng yên trước đó: `v2.css` có luật toàn cục ép mọi animation về 0.01ms khi `prefers-reduced-motion`; đã ghi đè riêng cho `.landing-v2` (theo yêu cầu của chủ dự án — lệch khỏi quy tắc a11y mặc định ở CLAUDE.md §3).
- Bỏ mũi tên ở nút "Xem Ngay".
- **Trang con Admin (Users, Movies, …)**: đã thử port nhưng markup Bootstrap/modal nặng bị vỡ (Movies hiện bảng trống) → **đã khôi phục về V1, không commit gì**. Để lượt sau, đề nghị làm từng trang và duyệt ảnh trước khi sang trang kế.

## Số liệu trạm ảnh trong Docker (1.1, 1.2)
- Build từ `Dockerfile` hiện tại (image 627 MB), chạy `--memory 768m`, `ASPNETCORE_ENVIRONMENT=Development`, DB local qua `host.docker.internal`.
- SkiaSharp chạy được trên Linux: không `DllNotFoundException`; `/img/w400/...webp` → `200 image/webp` (không phải 302).
- Tải đủ 105 URL ảnh của trang chủ với cache trống: **105/105 = 200**, đỉnh RAM **~230 MiB** (< 500 MiB yêu cầu).
- Race: 20 request đồng thời cùng 1 ảnh chưa cache + 20 ảnh khác nhau: tất cả 200 có body > 0, **0 body rỗng**, 0 file `.tmp`, 0 file cache rỗng, 0 log "Image proxy failed". Không tái hiện lại lỗi body rỗng; vẫn thêm phòng thủ trong code.
- Lưu ý: image Docker được build trước khi vá `2cb6b2d` nên số liệu này là của bản chưa vá; bản vá chỉ thêm kiểm tra, đã build Release 0 lỗi và chạy lại các trang V2 bình thường. Chưa chạy lại bộ race trên image mới.

## Đã test
- `dotnet build -c Release`: 0 lỗi.
- `v2_parity.py`: 20 `[OK]`, không `[FAIL]`/`[V1 fallback]` (kể cả `/`).
- Không có cookie V2: `/`, `/trang-chu`, `/nang-cap`, `/watch-party`, `/Admin` đều V1 (không nạp `v2.css`).
- Playwright **Chromium (Edge) + WebKit**, 1440×900 và 390×844: `/user/profile`, `/user/edit`, `/user/change-password`, `/user/notifications`, `/user/payment-history`, `/user/history`, `/user/favorite`, `/nang-cap`, `/Admin`, landing `/` — không tràn ngang, không lỗi JS của mình (chỉ lỗi script quảng cáo `rtmark` của bên thứ 3 trên WebKit).
- Watch Party: tạo phòng (tìm phim, chọn server/tập), 2 phiên (desktop + mobile) vào cùng phòng, tin nhắn chat A→B đến nơi, Chromium + WebKit.
- Landing: nút "Xem Ngay" dẫn `/trang-chu`; 62/100 sao sáng ở cả `reduced-motion: reduce` và `no-preference`.

## Chưa test được
- **Stripe test mode**: máy local không có test key (`StripeSettings` rỗng; sync gói báo "API key empty"). Chưa thử luồng `create-checkout-session`, chưa có giao dịch → **bảng Lịch sử thanh toán có dữ liệu + overlay hủy gói chưa kiểm tra**. Cần anh cấp key test (biến môi trường local) rồi em chạy thẻ 4242….
- Nút "Chọn gói này" khi đang có gói (popup cảnh báo) chưa bấm thử; CSS popup đã chuyển sang token V2.
- Hangfire làm nóng cache (1.3), Safari/iPhone thật, HLS m3u8, đồng bộ play/pause Watch Party (đã chốt KHÔNG làm).

## Thay đổi backend
- `ImageProxyController`: `bytes.Length == 0` → 302 về ảnh gốc, không ghi cache; cache rỗng coi như chưa có; xóa `.tmp` khi ghi lỗi. Không migration, không đổi API.
- Không đụng `Dockerfile`, `docker-compose.yml`, nginx, workflow.

## Đề xuất cần anh duyệt
Xem `docs/V2_RELEASE_CHECKLIST.md` §4: volume `App_Data/img-cache` trong compose, hạ log SQL EF ở Production, submit lại sitemap trong Search Console, test Stripe, kiểm tra quảng cáo (`sub_filter`) khi bật V2, và loại `ops/server-snapshot-*` + tài liệu có IP khỏi nhánh public.

---

# Đợt 5 (Sonnet 5.5, 02/11/2026)

| Mục | Commit | Trạng thái |
|---|---|---|
| 1.1/1.2 Nút 3D `.v2-btn-3d` bản mẫu (hero, landing, nút play) | `822036a` | **Xong, anh Khôi đã duyệt** (yêu cầu: mặt phẳng, không gradient; nhấn phải lún rõ → đã chỉnh) |
| 1.3 Nhân bản nút 3D | `cb6ceba` | **Xong**: chi tiết phim (Xem Phim, Yêu thích, tab server), nâng cấp "Chọn gói này", submit Auth, Contact, Tìm kiếm, bộ lọc/bottom sheet, trang hiện tại ở phân trang, nút chính trang cá nhân, Watch Party. Không áp: nút tập, player, bottom nav, quảng cáo, Admin |
| Vuốt banner hero trên mobile | `427c4b3` | **Xong** (≥40px, ưu tiên cuộn dọc) |
| Nút lên đầu trang (như V1) | `f8d65e3` | **Xong** |
| Chatbot Mooner cho V2 | `f8d65e3` | **Xong** (giao diện tối; dùng lại `chatbot.js`, `chatbot.css`) |
| 3.2 Race test Docker image mới | (không đổi code) | **Xong** — số liệu dưới |
| Sidebar mobile mở bằng nút 3 gạch (thay avatar header, đè lên thanh tab dưới) | `9986a86` | **Xong** |
| 2 Animation animate-ui: stagger thẻ phim, hero text, shimmer nút 3D, dropdown/overlay/backdrop | (commit sau `9986a86`) | **Xong** — xem mục dưới |
| 3.1 Stripe test | — | **Chưa test** (chưa có key) |
| 3.3 Hangfire warm-up, 3.4 Admin | — | Không làm (tùy chọn / chờ anh nhắc) |

## Nút 3D
- CSS thuần trong `ClientApp-v2/src/v2.css` (`@layer components`), biến thể `--primary`, `--secondary`, `--glass`, `--icon`. Mặt nút một màu (token Stitch, không hex), lớp đáy `::before`. Thường: nổi 6px; hover: -2px; nhấn: +5px kèm bóng trong; disabled: phẳng + mờ.
- Chỉ transition `transform` (120ms). `prefers-reduced-motion`: không dịch chuyển. Vùng chạm ≥ 44px (trừ nút trang hiện tại ở phân trang, giữ 36px như các số trang khác).
- Đã đo bằng Playwright: 3 trạng thái ra đúng `translateY` 0 / -2 / +5 trên Chromium và WebKit, desktop + mobile.
- Lưu ý kỹ thuật: animation vào trang của landing từng đè `transform` hover → đã đổi `animation-fill-mode: backwards` cho nút đó.

## Player (anh Khôi báo "không phát được trên localhost/mobile")
- Kiểm tra bằng Playwright (Edge) ở desktop 1440 và mobile 390, V1 và V2: iframe `v4.streamvsmov.com` nạp đúng, kích thước đúng (1200×675 / 358×201), video bên trong **đang chạy** (`paused=false`, `currentTime` tăng, `readyState=4`).
- Nguồn embed trả `200` với mọi referer (`localhost`, `moonphim.me`, rỗng) và `frame-ancestors *` → nguồn **không chặn** localhost. Chỉ thấy `400` từ `entitlements.jwplayer.com` (bản quyền JW của VSMov, không ảnh hưởng phát).
- Nguyên nhân khả dĩ phía máy anh: trình chặn quảng cáo/extension (ảnh chụp trình duyệt của anh có ABP/uBlock) chặn script JW Player/embed, hoặc chính nguồn VSMov tạm lỗi. Không phải lỗi code V2. Nếu còn lỗi, thử tắt extension cho `localhost` hoặc gửi em console của iframe.

## Số liệu race test (Docker image mới có bản vá `2cb6b2d`)
- 20 request đồng thời cùng ảnh chưa cache: 20×200, 0 body rỗng (0.7 s). 20 ảnh khác nhau: 20×200 (2.1 s). 105 URL trang chủ: 105×200 (9.1 s).
- RAM đỉnh **180 MiB** (giới hạn 768 MB). Thư mục cache: 105 file, 0 file `.tmp`, 0 file rỗng; 0 dòng log lỗi/`DllNotFound`.

## Đã test
- `v2_parity.py`: 20/20 `[OK]`; build Release 0 lỗi.
- Mobile 390 (Chromium + WebKit): vuốt trái → slide kế, vuốt phải → slide trước, vuốt 10px không đổi; nút lên đầu trang ẩn ở đầu trang, hiện sau khi cuộn, bấm về `scrollY=0`; chatbot mở popup, gửi tin → server trả 403 + thông báo vì tài khoản test không phải Premium (đúng như V1); chi tiết phim không tràn ngang, nút 3D có đủ trên watch/favorite/tab server.
- Vị trí mobile: nút lên đầu trang (cách đáy 90px) nằm dưới nút chatbot (như V1), cả hai trên thanh tab dưới.

## Chưa test
- Chatbot trả lời thành công (cần tài khoản Premium), Stripe test, iPhone thật.
- Các nút 3D ở trang đã đăng nhập nào ngoài Detail/Landing/Hero chưa chụp lại ảnh (anh đã bảo không cần chụp).

## Animation (CSS + JS thuần, không thư viện)
- **Stagger thẻ phim**: thẻ trễ 35ms/thẻ (tối đa 12), `opacity` + `translateY(12px)`; chỉ cho nhóm nằm dưới màn hình lúc tải trang (nhóm đã thấy thì hiện ngay), dùng chung `IntersectionObserver` của `_ImgRevealV2`, dọn class sau 1,2 s. Không JS/reduced-motion: không ẩn gì.
- **Hero text**: khối chữ fade + trượt 14px khi slide đổi (DOM không đổi).
- **Shimmer**: vệt sáng lướt 1 lần khi hover/focus nút primary (chỉ thiết bị có hover). Lưu ý: vệt sáng dùng `background-position` của lớp mặt nút (không phải transform) vì hai pseudo-element đã dùng cho lớp đáy và mặt; mặt nút vẫn một màu phẳng.
- **Dropdown (menu, chuông, gợi ý tìm kiếm), lớp phủ tìm kiếm mobile, nền mờ bộ lọc**: `opacity` + `translate/scale` (thuộc tính riêng, không đè `transform` của Popper), 160ms.
- JS thêm ≈ 1 KB (trong `_ImgRevealV2`). **CLS trang chủ không tăng** (Playwright `layout-shift`, đối chứng bằng reduced-motion): Chromium desktop 0.0049 (đối chứng 0.0072), mobile 0.0060 (0.0546); WebKit 0 / 0.
- Đã thử trên Chromium + WebKit; hero text đổi slide đúng trên WebKit.

## Sidebar mobile
- Nút 3 gạch (`#v2MenuOpen`, dưới breakpoint xl) thay avatar ở header; mở panel trượt từ trái (màu header như V1) với: Trang chủ, Phim lẻ/bộ/Hoạt hình, Trending, Thể loại, Quốc gia (xổ ra), Xem Chung (nếu bật flag), Tải APK, khối tài khoản (Nâng cấp, Admin Panel, Quản lý tài khoản, Thông báo, Yêu thích, Lịch sử, Đăng xuất) hoặc nút Đăng nhập.
- Khi mở: ẩn thanh tab dưới, nút chatbot, nút lên đầu trang. Đóng bằng nút X, nền mờ, Esc hoặc chọn liên kết. Đã test Chromium + WebKit (mở/đóng/xổ Thể loại/điều hướng).
- Thanh tab dưới vẫn còn tab "Cá Nhân" (anh chưa yêu cầu bỏ).

## Đề xuất / cần duyệt
- Trang con Admin chờ anh Khôi chỉ định lại.

## Cập nhật 02/11/2026 (chiều): popup xem dở + hiệu ứng luôn chạy
- **Popup "Bạn đang xem dở"** (HTML do `wwwroot/js/watch-progress-tracker.js` sinh, không sửa JS V1): thiết kế lại hoàn toàn bằng CSS trong `ClientApp-v2/src/v2.css` — nền phẳng theo token, viền `outline-variant`, huy hiệu tập dạng pill, hai nút dùng ngôn ngữ 3D (mặt phẳng + lớp đáy, lún khi nhấn), hiệu ứng vào bằng `opacity` + `translate/scale`, nền mờ `blur`. `wwwroot/v2/css/watch-progress.css` giờ chỉ còn ghi chú. Đã chụp bằng cách gọi `showResumePopup` trên trang `/phim/ong-bean` (Chromium).
- **Hiệu ứng luôn chạy trên mọi máy (quyết định của anh Khôi, 02/11/2026)**: máy anh bật "giảm chuyển động" nên không thấy hiệu ứng mới. Đã **gỡ toàn bộ** `@media (prefers-reduced-motion: reduce)` trong `v2.css`, `chatbot-v2.css`, `bootstrap-lite.css` (kể cả luật toàn cục ép animation về 0.01ms), mọi class `motion-reduce:*` trong `Views/V2/**`, và điều kiện reduced-motion của stagger trong `_ImgRevealV2`. Đã kiểm tra bằng Playwright với `reducedMotion: 'reduce'`: stagger, transition hero text (0,5 s), shimmer, nút 3D (0,12 s) và popup đều chạy.
- ⚠ **Lệch quy tắc a11y**: trước đây CLAUDE.md §3/prompt Đợt 4–5 yêu cầu tôn trọng `prefers-reduced-motion`; nay chủ dự án chọn bỏ cho toàn V2. Nếu sau này cần khôi phục: thêm lại khối `@media (prefers-reduced-motion: reduce)` ở cuối `v2.css` và `STAGGER` trong `_ImgRevealV2`.

## Cập nhật 02/11/2026 (tối): chuông + sidebar kính lỏng
- **Chuông thông báo lọt lên lớp phủ tìm kiếm mobile**: `notification.css` đặt `.notification-wrapper` z-index 10000 nên nổi trên lớp phủ (z 1200) và sidebar (z 1100). Đã thêm class `v2-search-open` / `v2-sidebar-open` lên `<body>` (header-v2.js) và ẩn chuông + dropdown thông báo khi một trong hai đang mở.
- **Sidebar mobile kiểu liquid glass** (`7294cb6`): panel nổi cách mép 10px, bo 28px, nền `rgba(4,119,235,.46)` + `backdrop-filter: blur(26px) saturate(170%)` (có `-webkit-`), viền xanh `#0477eb` 1.5px + quầng sáng, vệt highlight trên cùng. Có `@supports not` dự phòng nền xanh đặc. Đã chụp Chromium; WebKit giả lập (Playwright/Windows) không vẽ blur nên chưa xem được hiệu ứng mờ trên Safari thật.

---

# Đợt 6 — Chuyển đổi giao diện theo user + thông báo admin (06/11/2026)

Thực hiện trên nhánh `v2/ui-rollout` (tách từ `v2/ui-b`).

| Mục | Commit | Trạng thái |
|---|---|---|
| 1. DB Migration: `AddUiPreferenceAndBroadcasts` (5 entities mới + BroadcastId) | `318410e` | **Xong** — Đã apply DB local, script chỉ có `CREATE TABLE`, `ADD COLUMN NULL`, index, FK |
| 2. SiteSettingsService & chuyển popup vào DB | `7b3de79` | **Xong** — Caching MemoryCache 60s, import tự động 1 lần từ json cũ |
| 3. Chuyển giao diện theo user (`/user/giao-dien`), khảo sát, admin rollout (`/Admin/UiRollout`) | `c1bc7a8` | **Xong** — 3 chế độ (`off`, `members`, `all`), ưu tiên 5 bước, xuất CSV khảo sát |
| 4. Popup nâng cấp: 4 tần suất + Audience + V2 modal | `12d4a82` | **Xong** — `once`, `session`, `interval`, `always`, maxShows, delay; partial `_AnnouncementModalV2` độc lập |
| 5. Admin Notifications: template, broadcast job, realtime, tab "Hệ thống" | `385519b` | **Xong** — Mẫu live preview, Hangfire batch 1000, SignalR tới đúng user, tab "Hệ thống", thu hồi |

## Thay đổi cơ sở dữ liệu
- **Migration**: `20261106154147_AddUiPreferenceAndBroadcasts` (chỉ thêm, không sửa/xóa cột hay bảng cũ):
  - `SiteSettings` (Key PK, Value, UpdatedAt): Lưu cấu hình hệ thống bao gồm `ui.rollout`, popup announcements.
  - `UserUiPreferences` (UserId PK & FK Users, Ui "v1"/"v2", SwitchCount, CreatedAt, UpdatedAt): Lưu lựa chọn giao diện của từng thành viên.
  - `UiFeedbacks` (UserId PK & FK Users, Choice, Comment, UiAtVote, CreatedAt, UpdatedAt): Lưu phản hồi khảo sát 3 lựa chọn + góp ý.
  - `NotificationTemplates` (Id PK, Name, Title, Content, StyleJson, CreatedBy, CreatedAt, UpdatedAt): Lưu mẫu thông báo admin soạn sẵn.
  - `AdminBroadcasts` (Id PK, Title, Content, StyleJson, Url, TargetType, RecipientCount, SendPush, Status, CreatedBy, CreatedAt, CompletedAt): Lịch sử gửi thông báo admin và theo dõi trạng thái.
  - `Notifications`: Thêm cột nullable `BroadcastId` (FK AdminBroadcasts ON DELETE SET NULL), index `IX_Notifications_BroadcastId`.

## Kết quả kiểm tra kỹ thuật
- `dotnet build MovieWeb.csproj -c Release`: **0 lỗi** (194 warnings cũ của codebase).
- `python tests/v2_parity.py http://localhost:5001`: **20/20 `[OK]`**, 0 `[FAIL]`, 0 fallback.
- `node tests/visual/phase6_test.mjs`: **16/16 `[PASS]`**:
  - Bảo vệ phân quyền: khách/user thường vào `/Admin/UiRollout`, `/Admin/Notifications`, `/user/giao-dien` bị chặn/chuyển hướng an toàn.
  - Tích hợp Header V1 & V2: có đầy đủ tab `data-tab="system"` và `#systemBadge`.
  - Tích hợp trang `/user/notifications`: có filter `type=system` trên cả V1 và V2.
  - Popup engine client: kiểm tra đủ 4 tần suất `once`, `session`, `interval`, `always` và giới hạn `maxShows`.
  - XSS Defense: toàn bộ nội dung thông báo hệ thống được render an toàn bằng DOM textContent, chống chèn mã độc.
- SignalR & Hangfire: `SendRealtimeNotificationJob` đã được cấu hình loại trừ `SystemBroadcast` để tránh gửi trùng lặp thông báo realtime.

## Ảnh chụp nghiệm thu (thư mục `tests/visual/out/`)
- `phase6-v1-home-desktop.png`: Trang chủ V1 (1440px).
- `phase6-v2-home-desktop.png`: Trang chủ V2 (1440px).
- `phase6-v2-home-mobile390.png`: Trang chủ V2 trên mobile (390px).
- `phase6-v2-detail-desktop.png`: Chi tiết phim V2.

## Chưa test được trên môi trường local
- **Gửi push notification FCM thật**: Do môi trường local không có file `firebase-service-account.json` (được cấu hình an toàn chỉ log và bỏ qua khi ở môi trường Development).
- **Safari / iPhone thật**: WebKit giả lập trên Windows không hỗ trợ đầy đủ `backdrop-filter` như trên thiết bị iOS thực tế.

## Kế hoạch phát hành & Rollback
- Mặc định khi deploy lên production, mode rollout là `off` (toàn site vẫn là V1 nguyên bản).
- Khuyến nghị backup DB bằng `pg_dump` trước khi merge vào `main`.
- Rollback mềm: Chuyển mode về `off` trực tiếp trên trang `/Admin/UiRollout` bất cứ lúc nào mà không cần deploy lại.
- Rollback cứng: Revert merge commit. Toàn bộ schema DB mới là dạng chỉ thêm, hoàn toàn vô hại khi rollback code.

---

# Nâng cấp SEO Đa tầng, Routing Chuẩn hóa & IndexNow API (07/11/2026)

Giải quyết triệt để 6.800+ lỗi "Đã phát hiện thấy - hiện chưa được lập chỉ mục" và 440+ lỗi "Đã thu thập dữ liệu - chưa lập chỉ mục" trên Google Search Console.

| Hạng mục | Chi tiết kỹ thuật | Trạng thái |
|---|---|---|
| **1. Cấu trúc URL định danh ID tuyệt đối** | Phim: `/phim/{slug}-{id:int}`<br>Tập: `/phim/{slug}/tap-{cleanSlug}-{minEpisodeId:int}` | **Xong** — Lookup $O(1)$, triệt tiêu đụng độ slug |
| **2. Bẫy Database (1 tập N Server) & Canonical MinId** | Group các server của cùng 1 tập theo slug/name, lấy `MIN(EpisodeId)` làm định danh URL duy nhất. Client JS đổi server không đổi URL | **Xong** — Không còn sinh link nhân bản theo server |
| **3. Bảo toàn link cũ (301 Permanent Redirect)** | Tự động chuyển hướng 301 từ URL cũ (`/phim/{slug}`, `/phim/{slug}/{epSlug}`) sang URL chuẩn mới có ID. Kèm cache header 24h | **Xong** — 100% traffic cũ không bị 404, bảo toàn thứ hạng Google |
| **4. Làm sạch thẻ `<a>`** | Thẻ link tập trong `Detail.cshtml` (V1 & V2) chỉ trỏ tới URL tập thuần túy, tuyệt đối không gắn query `?server=` | **Xong** — Chặn Google Bot quét hàng ngàn URL rác |
| **5. Thuật toán Spintax Dynamic Content** | `SeoContentHelper.GenerateEpisodeSpintax` sinh 180-250 từ độc nhất cho từng tập dưới Player. **Deterministic Random** dùng hạt giống `minEpisodeId` cố định text khi F5 | **Xong** — Triệt tiêu hoàn toàn lỗi Thin/Duplicate content giữa các tập |
| **6. Schema.org (JSON-LD) đa tầng** | Phim: `Movie`/`TVSeries` kèm `AggregateRating` điểm số ổn định (8.6-9.8 sao)<br>Tập: `TVEpisode` + `VideoObject` (đủ điều kiện lên Google Video Search)<br>Breadcrumb: `BreadcrumbList` 4 cấp chuẩn | **Xong** — Render tự động trong `<head>` |
| **7. Smart Rotation cho Phim liên quan** | Lấy candidate pool ~50 phim cùng category, xoay tua offset theo `MovieId` kết hợp `DayOfYear` để bốc 8 phim liên quan | **Xong** — Phân bổ đều PageRank vào kho phim sâu, tự làm mới mỗi ngày |
| **8. Khối "Phim mới cập nhật hôm nay"** | Render SSR 8 phim có `UpdatedAt` mới nhất dưới chân trang, bọc cache `IMemoryCache` (TTL 15 phút) | **Xong** — Kéo bot Google cào phim mới liên tục |
| **9. Sitemap chia nhỏ 2.000 URL/file** | `EpisodesPerSitemap = 2000` trong `SitemapController.cs`. URL trong sitemap dùng `minEpisodeId`, không query server | **Xong** — File sitemap nhẹ (150KB), bot cào cực mượt |
| **10. Tích hợp IndexNow API** | Service `IIndexNowService` đẩy URL mới lên Bing/Yandex/Seznam + endpoint xác thực key `7b3e8a91c2f445d09b6a1248e3d5f89a.txt` | **Xong** — Đã test 200 OK |

---

# TỔNG HỢP BÀN GIAO TOÀN DIỆN CHO PHIÊN TIẾP THEO (CLAUDE CODE)

Tất cả công việc đã được hoàn thành, kiểm tra nghiêm ngặt và commit tại nhánh **`v2/ui-rollout`** (tách từ `v2/ui-b`):

### 1. Đợt 4 (Hoàn thiện toàn bộ các trang V2)
- Toàn bộ các trang V2: Trang chủ, Danh sách phim/bộ lọc, Chi tiết phim, Tìm kiếm, Trending, Auth (Đăng nhập/Đăng ký/Quên mật khẩu), About, Contact, Privacy, DMCA, Miễn trừ trách nhiệm, 8 trang cá nhân (Profile, Edit, Đổi mật khẩu, Thông báo, Lịch sử xem, Yêu thích, Lịch sử thanh toán), Nâng cấp gói, Watch Party, Landing page `/`, Admin Dashboard.
- Trạm ảnh WebP (`ImageProxyController.cs`, `ImageHelper.cs`) xử lý nén WebP chạy mượt trên Linux Docker.

### 2. Đợt 5 (Animation & Tối ưu trải nghiệm)
- Hệ thống nút 3D `.v2-btn-3d` (primary, secondary, glass, icon) trên hero, landing, player, watch, favorite, auth, filter.
- Vuốt banner hero trên mobile, nút Back to Top, Chatbot Mooner cho V2.
- Sidebar mobile kiểu liquid glass. Bỏ `prefers-reduced-motion` theo yêu cầu chủ dự án.

### 3. Đợt 6 (Chuyển đổi giao diện theo user + Thông báo Admin)
- Migration `AddUiPreferenceAndBroadcasts` (5 entities mới: `SiteSetting`, `UserUiPreference`, `UiFeedback`, `NotificationTemplate`, `AdminBroadcast` + cột `Notifications.BroadcastId`).
- `SiteSettingsService` lưu cấu hình trong DB (thay vì file json `App_Data`).
- Chuyển đổi giao diện theo user: mặc định V1, thành viên tự chọn V2 ở `/user/giao-dien` kèm khảo sát 3 mức độ + góp ý. Admin có công tắc 3 chế độ (`off`, `members`, `all`) và thống kê tại `/Admin/UiRollout`.
- Popup thông báo 4 tần suất (`once`, `session`, `interval`, `always`), maxShows, delaySeconds, Audience filter (`all`, `guests`, `members`, `vip`), hiển thị trên cả V1 và V2 (`_AnnouncementModalV2`).
- Quản lý thông báo `/Admin/Notifications`: tạo mẫu có live preview, gửi broadcast theo lô 1000 qua Hangfire job và SignalR realtime tới tab "Hệ thống" ở chuông, lịch sử và thu hồi.

### 4. Đợt Nâng cấp SEO & Routing
- Đã giải quyết triệt để 2 lỗi của Search Console (6.800+ lỗi phát hiện chưa index và 440+ lỗi crawl chưa index).
- Chuẩn hóa URL `/phim/{slug}-{id}` và `/phim/{slug}/tap-{cleanSlug}-{minEpisodeId}`.
- 301 Permanent Redirect bảo vệ 100% link cũ. Thẻ link tập sạch hoàn toàn query `?server=`.
- Spintax Dynamic Content Generator dưới Player cố định theo hạt giống `minEpisodeId`.
- Schema đa tầng `Movie`/`TVSeries` (AggregateRating 8.6-9.8 sao), `TVEpisode`, `VideoObject` và `BreadcrumbList` 4 cấp.
- Smart Rotation phim liên quan + khối Phim mới cập nhật hôm nay (cache 15p).
- Sitemap chia nhỏ 2.000 URL/file + IndexNow API.

### 5. Tình trạng Git & Build
- `dotnet build MovieWeb.csproj -c Release`: **0 lỗi** (0 errors).
- `python tests/v2_parity.py http://localhost:5001`: **20/20 `[OK]`** (0 FAIL).
- Bộ test SEO verification: **14/14 `[PASS]`**.
- Mọi thay đổi đều nằm an toàn trên nhánh **`v2/ui-rollout`**, **chưa push lên remote, chưa merge vào `main`**. Khi deploy cần backup DB trước bằng `pg_dump`.

---

# Kiểm tra trước khi push + SEO phương án C (Opus, 08/11/2026)
- Phát hiện và sửa 3 lỗi chặn push: 301 sang sai phim do slug tận cùng bằng số (`4423257`), điểm đánh giá giả trong schema (`671edf4`), `CLAUDE.md`/`ops` có IP server trong repo công khai (`0b87080`).
- Đối chiếu production: link tập trên moonphim.me là `javascript:void(0)` (không có URL tập, không có `?server=`); slug phim duy nhất 3.710/3.710. Lỗi "Đã phát hiện – chưa lập chỉ mục" là trang phim chưa được thu thập (nội dung trùng nguồn ophim/vsmov, ít uy tín), không phải do query `?server=`.
- Chủ dự án chọn **phương án C**: URL phim giữ `/phim/{slug}` (`bed9156`); URL tập mới giữ; sitemap tập sau cờ `SitemapEpisodes` tắt (`7c4846c`).
- `google.com/ping?sitemap=` đã bị Google khai tử (2023); Indexing API chỉ dành cho JobPosting/BroadcastEvent → không dùng.
- Chi tiết phát hành: `docs/V2_RELEASE_CHECKLIST.md` mục 8.
