# ✅ CHECKLIST SEO HOÀN CHỈNH CHO MOONPHIM

## 🎯 ĐÃ HOÀN THÀNH:

### 1. Meta Tags & Open Graph
- ✅ Title động cho từng trang
- ✅ Meta Description tối ưu
- ✅ Meta Keywords
- ✅ Open Graph (Facebook)
- ✅ Twitter Card
- ✅ Canonical URL
- ✅ Theme color & Author

**Áp dụng cho:**
- ✅ Trang Phim Lẻ
- ✅ Trang Hoạt Hình
- ✅ Trang Phim Mới Cập Nhật
- ✅ Layout chung (_Layout.cshtml)

### 2. Sitemap
- ✅ Sitemap.xml tự động
- ✅ Cache thông minh (60 phút)
- ✅ Bao gồm tất cả trang quan trọng
- ✅ Priority SEO dựa trên ViewCount
- ✅ API clear cache
- ✅ Background job tự động refresh

### 3. Robots.txt
- ✅ File robots.txt
- ✅ Controller serve robots.txt
- ✅ Chặn bot xấu
- ✅ Cho phép Googlebot
- ✅ Link đến sitemap

### 4. URL Structure
- ✅ URL thân thiện: `/phim/{slug}`
- ✅ URL danh mục: `/the-loai/{slug}`
- ✅ URL có ý nghĩa, không có ID

### 5. Performance
- ✅ Response caching
- ✅ Memory caching
- ✅ Lazy loading images
- ✅ Minified CSS/JS

---

## 📋 CẦN LÀM THÊM (KHUYẾN NGHỊ):

### 1. Structured Data (Schema.org)
Thêm JSON-LD cho từng loại trang:

**a) Trang chi tiết phim (Detail.cshtml):**
```html
<script type="application/ld+json">
{
  "@context": "https://schema.org",
  "@type": "Movie",
  "name": "@movie.Name",
  "alternateName": "@movie.OriginalName",
  "image": "@movie.PosterUrl",
  "description": "@movie.Description",
  "datePublished": "@movie.CreatedAt",
  "aggregateRating": {
    "@type": "AggregateRating",
    "ratingValue": "@movie.Rating",
    "reviewCount": "@movie.RatingCount"
  }
}
</script>
```

**b) Trang danh sách phim:**
```html
<script type="application/ld+json">
{
  "@context": "https://schema.org",
  "@type": "ItemList",
  "numberOfItems": @Model.Count(),
  "itemListElement": [
    @foreach(var movie in Model) {
      {
        "@type": "ListItem",
        "position": @(Model.IndexOf(movie) + 1),
        "item": {
          "@type": "Movie",
          "name": "@movie.Name",
          "url": "@Url.Action("Detail", "Movie", new { slug = movie.Slug })"
        }
      }
    }
  ]
}
</script>
```

### 2. Breadcrumb
Thêm breadcrumb navigation:

```html
<nav aria-label="breadcrumb">
  <ol class="breadcrumb">
    <li class="breadcrumb-item"><a href="/">Trang chủ</a></li>
    <li class="breadcrumb-item"><a href="/the-loai/phim-le">Phim Lẻ</a></li>
    <li class="breadcrumb-item active" aria-current="page">@movie.Name</li>
  </ol>
</nav>

<script type="application/ld+json">
{
  "@context": "https://schema.org",
  "@type": "BreadcrumbList",
  "itemListElement": [{
    "@type": "ListItem",
    "position": 1,
    "name": "Trang chủ",
    "item": "https://moonphim.me"
  },{
    "@type": "ListItem",
    "position": 2,
    "name": "Phim Lẻ",
    "item": "https://moonphim.me/the-loai/phim-le"
  },{
    "@type": "ListItem",
    "position": 3,
    "name": "@movie.Name"
  }]
}
</script>
```

### 3. Alt Text cho Hình Ảnh
Đảm bảo tất cả `<img>` đều có alt text:

```html
<!-- ❌ SAI -->
<img src="@movie.PosterUrl" />

<!-- ✅ ĐÚNG -->
<img src="@movie.PosterUrl" 
     alt="@movie.Name - Phim @movie.Type @movie.Year"
     title="Xem phim @movie.Name" />
```

### 4. Internal Linking
Tăng internal links:

```html
<!-- Trong trang chi tiết phim -->
<p>
  Xem thêm phim 
  @foreach(var category in movie.Categories) {
    <a href="/the-loai/@category.Slug">@category.Name</a>
  }
  từ
  @foreach(var country in movie.Countries) {
    <a href="/quoc-gia/@country.Slug">@country.Name</a>
  }
</p>
```

### 5. Social Sharing Buttons
Thêm nút chia sẻ mạng xã hội:

```html
<!-- Facebook -->
<a href="https://www.facebook.com/sharer/sharer.php?u=@currentUrl" 
   target="_blank" rel="noopener">
  Chia sẻ Facebook
</a>

<!-- Twitter -->
<a href="https://twitter.com/intent/tweet?url=@currentUrl&text=@movie.Name" 
   target="_blank" rel="noopener">
  Tweet
</a>
```

### 6. Page Speed Optimization
- [ ] Optimize images (WebP format)
- [ ] Enable Gzip compression
- [ ] CDN cho static files
- [ ] Defer JavaScript loading
- [ ] Critical CSS inline

### 7. Mobile Optimization
- [ ] Kiểm tra responsive design
- [ ] Touch-friendly buttons (min 44x44px)
- [ ] Font size tối thiểu 16px
- [ ] Viewport meta tag đúng

### 8. HTTPS
- [ ] Đảm bảo toàn bộ site dùng HTTPS
- [ ] Redirect HTTP → HTTPS
- [ ] HSTS header

### 9. Analytics & Monitoring
Thêm vào _Layout.cshtml:

```html
<!-- Google Analytics -->
<script async src="https://www.googletagmanager.com/gtag/js?id=G-XXXXXXXXXX"></script>
<script>
  window.dataLayer = window.dataLayer || [];
  function gtag(){dataLayer.push(arguments);}
  gtag('js', new Date());
  gtag('config', 'G-XXXXXXXXXX');
</script>

<!-- Google Search Console -->
<meta name="google-site-verification" content="your-verification-code" />
```

### 10. Content Optimization
- [ ] Mô tả phim chi tiết (min 150 từ)
- [ ] Tiêu đề hấp dẫn, có từ khóa
- [ ] Heading hierarchy đúng (H1 > H2 > H3)
- [ ] Nội dung unique, không duplicate

---

## 🔍 KIỂM TRA SEO:

### Tools để test:
1. **Google PageSpeed Insights**: https://pagespeed.web.dev/
2. **Google Mobile-Friendly Test**: https://search.google.com/test/mobile-friendly
3. **Google Rich Results Test**: https://search.google.com/test/rich-results
4. **Lighthouse** (trong Chrome DevTools)
5. **Screaming Frog SEO Spider**

### Checklist kiểm tra:
```bash
# 1. Sitemap
https://moonphim.me/sitemap.xml

# 2. Robots.txt
https://moonphim.me/robots.txt

# 3. Meta tags
View → Page Source → Tìm <meta>

# 4. Mobile responsive
F12 → Toggle device toolbar

# 5. Page speed
< 3s load time
```

---

## 📊 THEO DÕI:

### Google Search Console:
1. Coverage: Trang được index
2. Performance: Click, impression, CTR
3. Sitemaps: Submit và kiểm tra
4. Core Web Vitals: LCP, FID, CLS

### Metrics quan trọng:
- **Organic Traffic**: Số lượt truy cập từ Google
- **Bounce Rate**: < 60% là tốt
- **Session Duration**: > 2 phút là tốt
- **Pages per Session**: > 3 trang là tốt

---

## 🎓 TÀI LIỆU THAM KHẢO:

- [Google SEO Starter Guide](https://developers.google.com/search/docs/fundamentals/seo-starter-guide)
- [Schema.org](https://schema.org/)
- [Open Graph Protocol](https://ogp.me/)
- [Twitter Cards](https://developer.twitter.com/en/docs/twitter-for-websites/cards/overview/abouts-cards)

---

**Cập nhật lần cuối**: 20/11/2025
