using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using MovieWeb.Models.Entities;

namespace MovieWeb.Helpers;

public static class SeoSchemaHelper
{
    private const string BaseUrl = "https://moonphim.me";

    /// <summary>
    /// Sinh JSON-LD cho Movie hoặc TVSeries.
    /// Không có aggregateRating: Google chỉ chấp nhận điểm do chính user của site chấm; điểm tự sinh
    /// bị coi là structured data spam (có thể bị phạt thủ công toàn site).
    /// </summary>
    public static string BuildMovieOrSeriesSchema(Movie movie, string canonicalUrl)
    {
        if (movie == null) return "{}";

        bool isSingle = string.Equals(movie.Type, "single", StringComparison.OrdinalIgnoreCase);
        string schemaType = isSingle ? "Movie" : "TVSeries";

        var schema = new Dictionary<string, object?>
        {
            ["@context"] = "https://schema.org",
            ["@type"] = schemaType,
            ["@id"] = canonicalUrl + "#schema",
            ["name"] = movie.Name,
            ["url"] = canonicalUrl,
            ["image"] = GetFullImageUrl(movie.PosterUrl ?? movie.ThumbUrl),
            ["description"] = SeoMetadata.Description(movie.Description, movie.Content, movie.Name)
        };

        if (movie.Year.HasValue)
        {
            schema["datePublished"] = $"{movie.Year.Value}-01-01";
        }

        if (!string.IsNullOrWhiteSpace(movie.OriginalName) && !movie.OriginalName.Equals(movie.Name, StringComparison.OrdinalIgnoreCase))
        {
            schema["alternateName"] = movie.OriginalName;
        }

        if (movie.Actors?.Any() == true)
        {
            schema["actor"] = movie.Actors.Take(10)
                .Select(a => new Dictionary<string, object> { ["@type"] = "Person", ["name"] = a.Name }).ToArray();
        }

        if (movie.Directors?.Any() == true)
        {
            schema["director"] = movie.Directors.Take(5)
                .Select(d => new Dictionary<string, object> { ["@type"] = "Person", ["name"] = d.Name }).ToArray();
        }

        if (movie.Categories?.Any() == true)
        {
            schema["genre"] = movie.Categories.Select(c => c.Name).ToArray();
        }

        if (movie.Countries?.Any() == true)
        {
            schema["countryOfOrigin"] = movie.Countries
                .Select(c => new Dictionary<string, object> { ["@type"] = "Country", ["name"] = c.Name }).ToArray();
        }

        return JsonSerializer.Serialize(schema);
    }

    /// <summary>
    /// Sinh JSON-LD cho TVEpisode và VideoObject khi đang xem tập cụ thể.
    /// </summary>
    public static string? BuildEpisodeAndVideoSchema(Movie movie, Episode? episode, string episodeCanonicalUrl, string spintaxDescription)
    {
        if (movie == null || episode == null) return null;

        var epName = episode.EpisodeName ?? "1";
        var videoName = $"{movie.Name} - Tập {epName} Full HD Vietsub";
        var thumb = GetFullImageUrl(movie.ThumbUrl ?? movie.PosterUrl);
        var uploadDate = (movie.UpdatedAt ?? movie.CreatedAt ?? DateTime.UtcNow).ToString("yyyy-MM-ddTHH:mm:sszzz");

        // Parse duration (ví dụ: "45 phút" -> PT45M)
        string durationIso = ParseDurationToIso(movie.Time);

        var videoObject = new Dictionary<string, object?>
        {
            ["@context"] = "https://schema.org",
            ["@type"] = "VideoObject",
            ["@id"] = episodeCanonicalUrl + "#video",
            ["name"] = videoName,
            ["description"] = spintaxDescription,
            ["thumbnailUrl"] = new[] { thumb },
            ["uploadDate"] = uploadDate,
            ["duration"] = durationIso,
            ["embedUrl"] = episodeCanonicalUrl,
            ["publication"] = new Dictionary<string, object>
            {
                ["@type"] = "BroadcastEvent",
                ["isLiveBroadcast"] = false,
                ["startDate"] = uploadDate
            }
        };

        var episodeSchema = new Dictionary<string, object?>
        {
            ["@context"] = "https://schema.org",
            ["@type"] = "TVEpisode",
            ["@id"] = episodeCanonicalUrl + "#episode",
            ["name"] = videoName,
            ["episodeNumber"] = epName,
            ["partOfSeries"] = new Dictionary<string, object>
            {
                ["@type"] = "TVSeries",
                ["name"] = movie.Name,
                ["url"] = $"{BaseUrl}{SeoUrlHelper.MovieUrl(movie.Slug, movie.MovieId)}"
            },
            ["video"] = new Dictionary<string, object>
            {
                ["@id"] = episodeCanonicalUrl + "#video"
            }
        };

        var graph = new Dictionary<string, object>
        {
            ["@context"] = "https://schema.org",
            ["@graph"] = new object[] { episodeSchema, videoObject }
        };

        return JsonSerializer.Serialize(graph);
    }

    /// <summary>
    /// Sinh JSON-LD BreadcrumbList 4 cấp: Trang chủ > Thể loại > Tên Phim > Tập X.
    /// </summary>
    public static string BuildBreadcrumbSchema(Movie movie, Episode? episode, string movieCanonicalUrl, string? episodeCanonicalUrl)
    {
        var items = new List<Dictionary<string, object>>
        {
            new()
            {
                ["@type"] = "ListItem",
                ["position"] = 1,
                ["name"] = "Trang Chủ",
                ["item"] = BaseUrl
            }
        };

        var mainCategory = movie.Categories?.FirstOrDefault();
        if (mainCategory != null && !string.IsNullOrWhiteSpace(mainCategory.Slug))
        {
            items.Add(new Dictionary<string, object>
            {
                ["@type"] = "ListItem",
                ["position"] = items.Count + 1,
                ["name"] = mainCategory.Name,
                ["item"] = $"{BaseUrl}/the-loai/{mainCategory.Slug}"
            });
        }

        items.Add(new Dictionary<string, object>
        {
            ["@type"] = "ListItem",
            ["position"] = items.Count + 1,
            ["name"] = movie.Name,
            ["item"] = movieCanonicalUrl
        });

        if (episode != null && !string.IsNullOrWhiteSpace(episodeCanonicalUrl))
        {
            items.Add(new Dictionary<string, object>
            {
                ["@type"] = "ListItem",
                ["position"] = items.Count + 1,
                ["name"] = $"Tập {episode.EpisodeName}",
                ["item"] = episodeCanonicalUrl
            });
        }

        var breadcrumb = new Dictionary<string, object>
        {
            ["@context"] = "https://schema.org",
            ["@type"] = "BreadcrumbList",
            ["itemListElement"] = items
        };

        return JsonSerializer.Serialize(breadcrumb);
    }

    private static string GetFullImageUrl(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return $"{BaseUrl}/images/default-poster.jpg";
        if (path.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || path.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            return path;
        }
        return $"{BaseUrl}/storage/images/{path.TrimStart('/')}";
    }

    private static string ParseDurationToIso(string? timeStr)
    {
        if (string.IsNullOrWhiteSpace(timeStr)) return "PT45M";
        var match = System.Text.RegularExpressions.Regex.Match(timeStr, @"\d+");
        if (match.Success && int.TryParse(match.Value, out var minutes))
        {
            return $"PT{minutes}M";
        }
        return "PT45M";
    }
}
