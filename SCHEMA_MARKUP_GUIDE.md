# ⭐ SCHEMA MARKUP (JSON-LD) - RICH SNIPPETS

## ✅ ĐÃ TÍCH HỢP:

### 1. Trang Chi Tiết Phim (Detail.cshtml)
**Schema Type: Movie**

Bao gồm:
- ✅ Tên phim (name, alternateName)
- ✅ Hình ảnh poster (image)
- ✅ Mô tả (description)
- ✅ Năm sản xuất (dateCreated)
- ✅ **Đánh giá sao** (aggregateRating) - ⭐⭐⭐⭐⭐
- ✅ Trailer (trailer.VideoObject)
- ✅ Diễn viên (actor)
- ✅ Đạo diễn (director)
- ✅ Thể loại (genre)
- ✅ Quốc gia (countryOfOrigin)
- ✅ Chất lượng (contentRating)
- ✅ Ngôn ngữ (inLanguage)
- ✅ Thời lượng (duration)

### 2. Trang Danh Sách (PhimLe, HoatHinh, PhimMoiCapNhat)
**Schema Type: BreadcrumbList**

Giúp Google hiểu cấu trúc trang:
- ✅ Trang chủ → Phim Lẻ
- ✅ Trang chủ → Hoạt Hình
- ✅ Trang chủ → Phim Mới Cập Nhật

---

## 🎯 KẾT QUẢ TRÊN GOOGLE:

Khi search "tên phim" trên Google, người dùng sẽ thấy:

```
🎬 Avatar 2: The Way of Water - MoonPhim
⭐⭐⭐⭐⭐ 8.5/10 (2,456 đánh giá)
🎭 Đạo diễn: James Cameron
🎬 Diễn viên: Sam Worthington, Zoe Saldana
📅 2022 • 192 phút • Khoa học viễn tưởng, Hành động
[Ảnh poster đẹp]

Xem phim Avatar 2 vietsub, thuyết minh. Cập nhật phim Avatar 2 mới nhất tại MoonPhim...
```

**So sánh với không có Schema:**
```
Avatar 2 - MoonPhim
Xem phim Avatar 2 vietsub...
(Không có ảnh, không có sao, không có thông tin)
```

---

## 🧪 KIỂM TRA SCHEMA:

### 1. Google Rich Results Test:
```
https://search.google.com/test/rich-results
```
Nhập URL: `https://moonphim.me/phim/avatar-2`

### 2. Schema.org Validator:
```
https://validator.schema.org/
```
Paste source code của trang

### 3. Xem source code:
```
Ctrl + U (View Source)
Tìm: <script type="application/ld+json">
```

---

## 📊 CÁC LOẠI RICH SNIPPETS CHO WEB PHIM:

### 1. Movie Schema (Detail.cshtml) ✅
Hiển thị:
- ⭐ Đánh giá sao
- 🎬 Diễn viên, đạo diễn
- 📅 Năm sản xuất
- 🎭 Thể loại
- 🖼️ Poster image

### 2. Breadcrumb (Danh sách phim) ✅
Hiển thị:
- 🏠 Trang chủ > Phim Lẻ > Avatar 2
- Giúp SEO tốt hơn

### 3. VideoObject (Có thể thêm sau)
Hiển thị:
- ▶️ Video player trực tiếp trên Google
- ⏱️ Thời lượng video
- 📅 Upload date

### 4. Review Schema (Có thể thêm sau)
Hiển thị:
- 💬 Bình luận của người dùng
- ⭐ Đánh giá cá nhân

---

## 🚀 LÀM THÊM (TÙY CHỌN):

### 1. Thêm VideoObject cho từng tập phim:

```json
{
  "@type": "TVSeries",
  "episode": [{
    "@type": "TVEpisode",
    "episodeNumber": 1,
    "name": "Tập 1",
    "video": {
      "@type": "VideoObject",
      "name": "Avatar 2 - Tập 1",
      "uploadDate": "2024-01-01",
      "duration": "PT45M"
    }
  }]
}
```

### 2. Thêm Organization Schema (_Layout.cshtml):

```json
{
  "@type": "Organization",
  "name": "MoonPhim",
  "url": "https://moonphim.me",
  "logo": "https://moonphim.me/images/logo.png",
  "sameAs": [
    "https://facebook.com/moonphim",
    "https://twitter.com/moonphim"
  ]
}
```

### 3. Thêm WebSite Schema (_Layout.cshtml):

```json
{
  "@type": "WebSite",
  "url": "https://moonphim.me",
  "potentialAction": {
    "@type": "SearchAction",
    "target": "https://moonphim.me/tim-kiem?keyword={search_term_string}",
    "query-input": "required name=search_term_string"
  }
}
```

---

## 📈 THEO DÕI HIỆU QUẢ:

### Google Search Console:
1. Vào: https://search.google.com/search-console
2. Menu: **Enhancement** → **Rich Results**
3. Xem:
   - Số trang có Rich Results
   - Lỗi nếu có
   - Click-through rate (CTR)

### Kết quả mong đợi:
- **CTR tăng 20-40%** (do có ảnh + sao đẹp hơn)
- **Thứ hạng tăng** (Google ưu tiên trang có Schema)
- **Traffic tăng** từ Google Search

---

## ⚠️ LƯU Ý QUAN TRỌNG:

1. **Không fake data**:
   - Đánh giá phải thật (từ user comments)
   - Số lượng rating phải đúng
   - Google sẽ penalty nếu phát hiện fake

2. **Cập nhật thường xuyên**:
   - Rating thay đổi → Cập nhật Schema
   - Thêm phim mới → Schema tự động tạo

3. **Test trước khi deploy**:
   - Dùng Rich Results Test
   - Check không có lỗi JSON

4. **Chờ Google index**:
   - Schema không hiển thị ngay
   - Đợi 1-2 tuần sau khi deploy
   - Submit lại sitemap để Google re-crawl

---

## 🎓 TÀI LIỆU THAM KHẢO:

- [Schema.org Movie](https://schema.org/Movie)
- [Google Rich Results - Movie](https://developers.google.com/search/docs/appearance/structured-data/movie)
- [JSON-LD Guide](https://json-ld.org/)
- [Breadcrumb Structured Data](https://developers.google.com/search/docs/appearance/structured-data/breadcrumb)

---

**Status**: ✅ HOÀN THÀNH  
**Ngày**: 20/11/2025  
**Impact**: 🚀 CỰC KỲ CAO cho SEO web phim!
