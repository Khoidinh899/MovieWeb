# 🎬 Movie Detail API - Flutter Integration Guide

## 📌 Overview

New JSON API endpoint created for Flutter/Mobile apps to get complete movie details without HTML parsing.

**Created:** January 29, 2026  
**Location:** `Controllers/API/ApiMovieController.cs`  
**Base Endpoint:** `/api/movie`

---

## 🚀 API Endpoint

### Get Movie Detail by Slug

**URL:** `GET /api/movie/{slug}`  
**Authentication:** Not required  
**Cache:** 30 minutes in-memory  
**Global Exception Handler:** ✅ Enabled

---

## 📋 Request Example

```dart
// Flutter/Dart Example
import 'package:http/http.dart' as http;
import 'dart:convert';

Future<Map<String, dynamic>> getMovieDetail(String slug) async {
  final url = Uri.parse('https://moonphim.me/api/movie/$slug');
  
  try {
    final response = await http.get(url);
    
    if (response.statusCode == 200) {
      return json.decode(response.body);
    } else {
      // Global Exception Handler will return error JSON
      final error = json.decode(response.body);
      throw Exception(error['message']);
    }
  } catch (e) {
    print('Error fetching movie: $e');
    rethrow;
  }
}

// Usage
void main() async {
  final movie = await getMovieDetail('avengers-endgame');
  print('Movie Name: ${movie['data']['name']}');
  print('Rating: ${movie['data']['rating']}');
}
```

---

## ✅ Success Response (200)

```json
{
  "success": true,
  "data": {
    "movieId": 123,
    "slug": "avengers-endgame",
    "name": "Avengers: Endgame",
    "originalName": "Avengers: Endgame",
    "content": "Sau sự kiện của Infinity War, vũ trụ đang trong tình trạng hỗn loạn...",
    "description": "Phim siêu anh hùng về hành trình cứu vũ trụ",
    "type": "single",
    "status": "completed",
    
    // 🖼️ Media URLs (Normalized to absolute paths)
    "thumbUrl": "https://img.ophim.live/uploads/movies/avengers-thumb.jpg",
    "posterUrl": "https://img.ophim.live/uploads/movies/avengers-poster.jpg",
    "poster": "https://img.ophim.live/uploads/movies/avengers-poster2.jpg",
    "backdrop": "https://img.ophim.live/uploads/movies/avengers-backdrop.jpg",
    "trailerUrl": "https://youtube.com/watch?v=...",
    "trailer": "https://youtube.com/watch?v=...",
    
    // 📊 Movie Details
    "time": "181 phút",
    "episodeCurrent": "Full",
    "episodeTotal": "1",
    "quality": "HD",
    "language": "Vietsub",
    "year": 2019,
    
    // 📈 Statistics
    "viewCount": 150000,
    "rating": 8.5,
    "ratingCount": 1200,
    
    // 🚩 Flags
    "isBanner": true,
    "isCopyright": false,
    "isActive": true,
    
    // 🕒 Timestamps
    "createdAt": "2026-01-01T00:00:00",
    "updatedAt": "2026-01-29T15:30:00",
    
    // 🏷️ Categories (with ID/Slug for filtering)
    "categories": [
      {
        "categoryId": 1,
        "name": "Hành Động",
        "slug": "hanh-dong"
      },
      {
        "categoryId": 5,
        "name": "Viễn Tưởng",
        "slug": "vien-tuong"
      }
    ],
    
    // 🌍 Countries (with ID/Slug for filtering)
    "countries": [
      {
        "countryId": 3,
        "name": "Mỹ",
        "slug": "my"
      }
    ],
    
    // 👥 Actors
    "actors": [
      {
        "actorId": 10,
        "name": "Robert Downey Jr."
      },
      {
        "actorId": 15,
        "name": "Chris Evans"
      },
      {
        "actorId": 20,
        "name": "Scarlett Johansson"
      }
    ],
    
    // 🎬 Directors
    "directors": [
      {
        "directorId": 5,
        "name": "Anthony Russo"
      },
      {
        "directorId": 6,
        "name": "Joe Russo"
      }
    ],
    
    // 🎞️ Episodes (grouped by server)
    "episodes": [
      {
        "serverName": "vietsub",
        "displayName": "Vietsub",
        "episodes": [
          {
            "episodeId": 1,
            "episodeName": "Full",
            "slug": "full",
            "linkM3u8": "https://..."
          }
        ]
      },
      {
        "serverName": "thuyết minh",
        "displayName": "Thuyết Minh",
        "episodes": [
          {
            "episodeId": 2,
            "episodeName": "Full",
            "slug": "full",
            "linkM3u8": "https://..."
          }
        ]
      }
    ],
    
    // 🔗 Related Movies (same categories, 8 max)
    "relatedMovies": [
      {
        "movieId": 124,
        "slug": "avengers-infinity-war",
        "name": "Avengers: Infinity War",
        "originalName": "Avengers: Infinity War",
        "thumbUrl": "https://img.ophim.live/uploads/movies/...",
        "posterUrl": "https://img.ophim.live/uploads/movies/...",
        "year": 2018,
        "quality": "HD",
        "language": "Vietsub",
        "episodeCurrent": "Full",
        "episodeTotal": "1",
        "rating": 8.3,
        "viewCount": 140000
      }
    ]
  }
}
```

---

## ❌ Error Responses

### 400 - Bad Request (Invalid Slug)

```json
{
  "success": false,
  "message": "Dữ liệu đầu vào không hợp lệ.",
  "error": "Slug không được để trống",
  "statusCode": 400,
  "path": "/api/movie/",
  "timestamp": "2026-01-29T15:30:00"
}
```

### 404 - Movie Not Found

```json
{
  "success": false,
  "message": "Không tìm thấy dữ liệu yêu cầu.",
  "error": "Không tìm thấy phim với slug: xyz",
  "stackTrace": "...",
  "timestamp": "2026-01-29T15:30:00",
  "path": "/api/movie/xyz",
  "statusCode": 404
}
```

### 500 - Internal Server Error

```json
{
  "success": false,
  "message": "Đã xảy ra lỗi không mong muốn. Vui lòng thử lại sau.",
  "error": "NullReferenceException: ...",
  "stackTrace": "... (only in Development mode)",
  "statusCode": 500,
  "path": "/api/movie/some-slug",
  "timestamp": "2026-01-29T15:30:00"
}
```

---

## 🔧 Additional Endpoint: Clear Cache

**URL:** `DELETE /api/movie/{slug}/cache`  
**Use Case:** Clear cache for a specific movie (Admin/Testing)

**Response:**
```json
{
  "success": true,
  "message": "Cache cleared for movie: avengers-endgame"
}
```

**⚠️ Note:** Should add `[Authorize]` in production!

---

## 🎯 Key Features

### ✅ Complete Data Structure
- All movie information in single request
- No need for multiple API calls
- Includes relationships (categories, countries, actors, directors, episodes)

### ✅ Mobile-Friendly
- Absolute image URLs (no relative paths)
- Clean JSON structure
- No HTML parsing needed
- Perfect for Flutter/React Native

### ✅ Performance Optimized
- **30-minute cache** (configurable)
- **Fire-and-forget** view count increment (async)
- **Single database query** with `.Include()`
- Related movies pre-filtered by categories

### ✅ Filtering Support
- Categories with `categoryId` and `slug`
- Countries with `countryId` and `slug`
- Can build filter/search features in Flutter

### ✅ Error Handling
- **Global Exception Handler** integration
- Consistent error format across all APIs
- User-friendly Vietnamese messages
- Stack traces in Development mode only

---

## 📱 Flutter Model Classes

```dart
class MovieDetailResponse {
  final bool success;
  final MovieDetail data;

  MovieDetailResponse({required this.success, required this.data});

  factory MovieDetailResponse.fromJson(Map<String, dynamic> json) {
    return MovieDetailResponse(
      success: json['success'],
      data: MovieDetail.fromJson(json['data']),
    );
  }
}

class MovieDetail {
  final int movieId;
  final String slug;
  final String name;
  final String? originalName;
  final String? content;
  final String? thumbUrl;
  final String? posterUrl;
  final String? trailerUrl;
  final int? year;
  final double rating;
  final int viewCount;
  final List<Category> categories;
  final List<Country> countries;
  final List<Actor> actors;
  final List<Director> directors;
  final List<EpisodeServer> episodes;
  final List<MovieSimple> relatedMovies;

  MovieDetail({
    required this.movieId,
    required this.slug,
    required this.name,
    this.originalName,
    this.content,
    this.thumbUrl,
    this.posterUrl,
    this.trailerUrl,
    this.year,
    required this.rating,
    required this.viewCount,
    required this.categories,
    required this.countries,
    required this.actors,
    required this.directors,
    required this.episodes,
    required this.relatedMovies,
  });

  factory MovieDetail.fromJson(Map<String, dynamic> json) {
    return MovieDetail(
      movieId: json['movieId'],
      slug: json['slug'],
      name: json['name'],
      originalName: json['originalName'],
      content: json['content'],
      thumbUrl: json['thumbUrl'],
      posterUrl: json['posterUrl'],
      trailerUrl: json['trailerUrl'],
      year: json['year'],
      rating: (json['rating'] ?? 0).toDouble(),
      viewCount: json['viewCount'] ?? 0,
      categories: (json['categories'] as List)
          .map((c) => Category.fromJson(c))
          .toList(),
      countries: (json['countries'] as List)
          .map((c) => Country.fromJson(c))
          .toList(),
      actors: (json['actors'] as List)
          .map((a) => Actor.fromJson(a))
          .toList(),
      directors: (json['directors'] as List)
          .map((d) => Director.fromJson(d))
          .toList(),
      episodes: (json['episodes'] as List)
          .map((e) => EpisodeServer.fromJson(e))
          .toList(),
      relatedMovies: (json['relatedMovies'] as List)
          .map((m) => MovieSimple.fromJson(m))
          .toList(),
    );
  }
}

class Category {
  final int categoryId;
  final String name;
  final String slug;

  Category({required this.categoryId, required this.name, required this.slug});

  factory Category.fromJson(Map<String, dynamic> json) {
    return Category(
      categoryId: json['categoryId'],
      name: json['name'],
      slug: json['slug'],
    );
  }
}

class Country {
  final int countryId;
  final String name;
  final String slug;

  Country({required this.countryId, required this.name, required this.slug});

  factory Country.fromJson(Map<String, dynamic> json) {
    return Country(
      countryId: json['countryId'],
      name: json['name'],
      slug: json['slug'],
    );
  }
}

class Actor {
  final int actorId;
  final String name;

  Actor({required this.actorId, required this.name});

  factory Actor.fromJson(Map<String, dynamic> json) {
    return Actor(
      actorId: json['actorId'],
      name: json['name'],
    );
  }
}

class Director {
  final int directorId;
  final String name;

  Director({required this.directorId, required this.name});

  factory Director.fromJson(Map<String, dynamic> json) {
    return Director(
      directorId: json['directorId'],
      name: json['name'],
    );
  }
}

class EpisodeServer {
  final String serverName;
  final String displayName;
  final List<Episode> episodes;

  EpisodeServer({
    required this.serverName,
    required this.displayName,
    required this.episodes,
  });

  factory EpisodeServer.fromJson(Map<String, dynamic> json) {
    return EpisodeServer(
      serverName: json['serverName'],
      displayName: json['displayName'],
      episodes: (json['episodes'] as List)
          .map((e) => Episode.fromJson(e))
          .toList(),
    );
  }
}

class Episode {
  final int episodeId;
  final String episodeName;
  final String slug;
  final String? linkM3u8;

  Episode({
    required this.episodeId,
    required this.episodeName,
    required this.slug,
    this.linkM3u8,
  });

  factory Episode.fromJson(Map<String, dynamic> json) {
    return Episode(
      episodeId: json['episodeId'],
      episodeName: json['episodeName'],
      slug: json['slug'],
      linkM3u8: json['linkM3u8'],
    );
  }
}

class MovieSimple {
  final int movieId;
  final String slug;
  final String name;
  final String? thumbUrl;
  final int? year;
  final double rating;

  MovieSimple({
    required this.movieId,
    required this.slug,
    required this.name,
    this.thumbUrl,
    this.year,
    required this.rating,
  });

  factory MovieSimple.fromJson(Map<String, dynamic> json) {
    return MovieSimple(
      movieId: json['movieId'],
      slug: json['slug'],
      name: json['name'],
      thumbUrl: json['thumbUrl'],
      year: json['year'],
      rating: (json['rating'] ?? 0).toDouble(),
    );
  }
}
```

---

## 🧪 Testing

### Test URLs

1. **Single Movie (Phim Lẻ):**
   ```
   GET https://moonphim.me/api/movie/avengers-endgame
   ```

2. **Series Movie (Phim Bộ):**
   ```
   GET https://moonphim.me/api/movie/breaking-bad
   ```

3. **Not Found:**
   ```
   GET https://moonphim.me/api/movie/xyz-not-exists
   ```

4. **Clear Cache:**
   ```
   DELETE https://moonphim.me/api/movie/avengers-endgame/cache
   ```

### Using Postman

```bash
# Get Movie Detail
GET https://moonphim.me/api/movie/avengers-endgame
Accept: application/json

# Clear Cache
DELETE https://moonphim.me/api/movie/avengers-endgame/cache
```

### Using cURL

```bash
# Get Movie Detail
curl -X GET "https://moonphim.me/api/movie/avengers-endgame" \
  -H "Accept: application/json"

# Clear Cache
curl -X DELETE "https://moonphim.me/api/movie/avengers-endgame/cache"
```

---

## 📊 Performance Considerations

### Cache Strategy
- **Cache Duration:** 30 minutes (configurable via `CacheMinutes` constant)
- **Cache Key Format:** `movie_detail_{slug}`
- **Cache Storage:** In-memory (IMemoryCache)
- **Cache Invalidation:** Manual via DELETE endpoint or expiration

### View Count Optimization
- **Fire-and-forget** async increment (doesn't block response)
- Prevents database write from slowing down API response
- Error-safe with try-catch logging

### Database Optimization
- Single query with `.Include()` for all relations
- Reduces N+1 query problem
- Related movies limited to 8 for performance

---

## 🔒 Security Notes

1. **Cache Endpoint:** 
   - Currently public (no auth)
   - **Recommendation:** Add `[Authorize]` or admin check for production

2. **Rate Limiting:**
   - Not implemented yet
   - **Recommendation:** Add rate limiting for public APIs

3. **CORS:**
   - Check `Program.cs` for CORS configuration
   - Ensure Flutter app domain is allowed

---

## 📚 Related Documentation

- **API Inventory:** `.agent/skills/api-inventory.md`
- **Global Exception Handler:** `GLOBAL_EXCEPTION_HANDLING.md`
- **Quick Reference:** `QUICK_REFERENCE.md`

---

## 🤝 Support

**Questions?** Contact backend team or check the main API inventory document.

**Last Updated:** January 29, 2026
