using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MovieWeb.Data;
using MovieWeb.Models.Entities;

namespace MovieWeb.Services
{
    public interface IRecommendationService
    {
        Task<List<Movie>> GetRecommendationsAsync(string genre, string country, string type, int? year);
    }

    public class RecommendationService : IRecommendationService
    {
        private readonly MovieWebDbContext _context;
        private readonly ILogger<RecommendationService> _logger;

        public RecommendationService(MovieWebDbContext context, ILogger<RecommendationService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<List<Movie>> GetRecommendationsAsync(string genre, string country, string type, int? year)
        {
            try
            {
                _logger.LogInformation("Getting recommendations: Genre={Genre}, Country={Country}, Type={Type}, Year={Year}", 
                    genre, country, type, year);

                // Bắt đầu với query cơ bản: chỉ lấy phim active
                var query = _context.Movies
                    .Include(m => m.Categories)
                    .Include(m => m.Countries)
                    .Where(m => m.IsActive == true);

                // ===== LỌC THEO THỂ LOẠI (CATEGORY) =====
                if (!string.IsNullOrEmpty(genre))
                {
                    // Chuẩn hóa tên thể loại (bỏ dấu, bỏ khoảng trắng thừa)
                    var genreNormalized = genre.Trim();

                    // Tìm trong bảng Categories
                    query = query.Where(m => m.Categories.Any(c => 
                        EF.Functions.ILike(c.Name, $"%{genreNormalized}%")
                    ));
                }

                // ===== LỌC THEO QUỐC GIA (COUNTRY) =====
                if (!string.IsNullOrEmpty(country))
                {
                    var countryNormalized = country.Trim();

                    query = query.Where(m => m.Countries.Any(c => 
                        EF.Functions.ILike(c.Name, $"%{countryNormalized}%")
                    ));
                }

                // ===== LỌC THEO LOẠI PHIM (TYPE) =====
                if (!string.IsNullOrEmpty(type))
                {
                    var typeNormalized = type.Trim().ToLower();

                    // Map từ tiếng Việt sang giá trị trong DB
                    string dbType = typeNormalized switch
                    {
                        "phim bộ" or "phim bo" or "series" => "series",
                        "phim lẻ" or "phim le" or "single" => "single",
                        "hoạt hình" or "hoat hinh" or "animation" => "hoathinh",
                        _ => typeNormalized // Giữ nguyên nếu không match
                    };

                    query = query.Where(m => 
                        EF.Functions.ILike(m.Type, dbType)
                    );
                }

                // ===== LỌC THEO NĂM (YEAR) =====
                if (year.HasValue && year.Value > 1900)
                {
                    query = query.Where(m => m.Year == year.Value);
                }

                // ===== SẮP XẾP VÀ LẤY TOP 5 =====
                var recommendations = await query
                    .OrderByDescending(m => m.Rating) // Ưu tiên rating cao
                    .ThenByDescending(m => m.ViewCount) // Sau đó là lượt xem
                    .ThenByDescending(m => m.Year) // Cuối cùng là phim mới
                    .Take(5)
                    .Select(m => new Movie
                    {
                        MovieId = m.MovieId,
                        Name = m.Name,
                        OriginalName = m.OriginalName,
                        Slug = m.Slug,
                        Year = m.Year,
                        Rating = m.Rating,
                        PosterUrl = m.PosterUrl,
                        Type = m.Type == "series" ? "Phim bộ" :
                               m.Type == "single" ? "Phim lẻ" :
                               m.Type == "hoathinh" ? "Hoạt hình" :
                               m.Type, // Giữ nguyên nếu là loại khác
                        Description = m.Description
                    })
                    .ToListAsync();

                _logger.LogInformation("Found {Count} recommendations", recommendations.Count);

                return recommendations;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting recommendations");
                return new List<Movie>();
            }
        }
    }
}