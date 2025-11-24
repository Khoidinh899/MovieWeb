# 🚀 HƯỚNG DẪN TÍCH HỢP SITEMAP TỰ ĐỘNG

## ✅ Đã hoàn thiện:

### 1. SitemapController.cs
- Tự động tạo sitemap cho toàn bộ website
- Cache thông minh (1 giờ) để giảm tải database
- Bao gồm:
  - Trang chủ
  - Các trang danh mục (Phim Lẻ, Phim Bộ, Hoạt Hình, Phim Mới)
  - Tất cả thể loại
  - Tất cả phim (tối đa 10,000)
- Ưu tiên SEO dựa trên lượt xem
- API xóa cache: POST `/api/sitemap/clear-cache`

### 2. SitemapCacheRefreshJob.cs
- Background job tự động làm mới sitemap
- Chạy mỗi giờ (hoặc trigger thủ công)

---

## 📝 CẦN LÀM TIẾP:

### Bước 1: Cập nhật Program.cs

Thêm vào cuối file `Program.cs` (sau khi có `RecurringJob`):

```csharp
// ===== SCHEDULE SITEMAP REFRESH JOB =====
MovieWeb.Jobs.SitemapCacheRefreshJob.ScheduleRecurringJob();
```

### Bước 2: Tích hợp vào MovieSyncService

Thêm vào constructor của `MovieSyncService.cs`:

```csharp
using MovieWeb.Jobs;

// Thêm vào constructor
private readonly IMemoryCache _cache;

public MovieSyncService(
    MovieWebDbContext context,
    IOPhimService oPhimService,
    ILogger<MovieSyncService> logger,
    ICategorySyncService categorySyncService,
    ICountrySyncService countrySyncService,
    IActorSyncService actorSyncService,
    IDirectorSyncService directorSyncService,
    IMemoryCache cache) // ← THÊM DÒNG NÀY
{
    _context = context;
    _oPhimService = oPhimService;
    _logger = logger;
    _categorySyncService = categorySyncService;
    _countrySyncService = countrySyncService;
    _actorSyncService = actorSyncService;
    _directorSyncService = directorSyncService;
    _cache = cache; // ← THÊM DÒNG NÀY
}
```

Sau đó, thêm dòng này **SAU MỖI `await _context.SaveChangesAsync();`** trong các method sync:

```csharp
await _context.SaveChangesAsync();

// ✅ Xóa cache sitemap để tự động cập nhật khi có phim mới
SitemapCacheRefreshJob.TriggerImmediately();
```

**Tìm và thêm vào 7 vị trí sau trong MovieSyncService.cs:**
- Dòng 155: Sau khi sync tập phim
- Dòng 411: Sau khi backfill episodes
- Dòng 424: Sau khi backfill episodes (batch)
- Dòng 513: Sau khi backfill single movies
- Dòng 519: Sau khi backfill single movies (batch)
- Dòng 593: Sau khi sync movie by slug
- Dòng 601: Sau khi sync movie by slug (batch)

### Bước 3: Tích hợp vào BackgroundSyncService

Tương tự, thêm vào `BackgroundSyncService.cs`:

```csharp
// Sau khi hoàn thành sync phim
await _movieSyncService.SyncMoviesFromApiToDbAsync(allMovies, minYear);

// ✅ Làm mới sitemap
SitemapCacheRefreshJob.TriggerImmediately();
```

### Bước 4: Submit Sitemap lên Google Search Console

1. Truy cập: https://search.google.com/search-console
2. Chọn property `moonphim.me`
3. Vào mục **Sitemaps** ở menu bên trái
4. Nhập: `sitemap.xml`
5. Nhấn **Submit**

---

## 🧪 KIỂM TRA:

### 1. Test Sitemap:
```bash
# Truy cập:
https://moonphim.me/sitemap.xml

# Hoặc local:
http://localhost:5000/sitemap.xml
```

### 2. Test Clear Cache:
```bash
POST https://moonphim.me/api/sitemap/clear-cache
```

### 3. Kiểm tra log:
```
✅ Sitemap loaded from cache
✅ Sitemap generated and cached for 60 minutes
🗑️ Sitemap cache cleared successfully
```

---

## 📊 THỐNG KÊ SITEMAP:

Sitemap sẽ bao gồm:
- 1 trang chủ (priority: 1.0)
- 5 trang danh mục chính (priority: 0.9)
- ~10-20 trang thể loại (priority: 0.8)
- Tối đa 10,000 phim (priority: 0.6-0.9 dựa trên view count)

**Tổng cộng**: Khoảng ~10,000 - 15,000 URLs

---

## ⚡ TỐI ƯU:

1. **Cache**: Sitemap được cache 1 giờ, giảm tải DB
2. **Lazy Generation**: Chỉ tạo lại khi có request sau khi clear cache
3. **Background Refresh**: Tự động clear cache mỗi giờ
4. **Priority SEO**: Phim có view cao được ưu tiên hơn
5. **Response Cache**: HTTP cache header giúp CDN/browser cache

---

## 🔧 TÙY CHỈNH:

Trong `SitemapController.cs`, bạn có thể thay đổi:

```csharp
private const string BASE_URL = "https://moonphim.me"; // ← Domain của bạn
private const int MAX_MOVIES = 10000; // ← Số phim tối đa
private const int CACHE_MINUTES = 60; // ← Thời gian cache
```

---

## 📚 TÀI LIỆU THAM KHẢO:

- [Google Sitemap Guidelines](https://developers.google.com/search/docs/crawling-indexing/sitemaps/build-sitemap)
- [Sitemap Protocol](https://www.sitemaps.org/protocol.html)

---

**Lưu ý**: Sau khi tích hợp xong, nhớ test kỹ và submit lên Google Search Console!
