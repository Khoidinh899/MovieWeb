// File: Services/CategorySyncService.cs (FIXED - Không duplicate)
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MovieWeb.Data;
using MovieWeb.Models.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MovieWeb.Services
{
    public interface ICategorySyncService
    {
        Task<List<Category>> SyncCategoriesAsync(List<MovieWeb.Models.API.Category> apiCategories);
    }

    public class CategorySyncService : ICategorySyncService
    {
        private readonly MovieWebDbContext _context;
        private readonly ILogger<CategorySyncService> _logger;

        public CategorySyncService(MovieWebDbContext context, ILogger<CategorySyncService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<List<Category>> SyncCategoriesAsync(List<MovieWeb.Models.API.Category> apiCategories)
        {
            if (apiCategories == null || !apiCategories.Any())
            {
                return new List<Category>();
            }

            var syncedCategories = new List<Category>();
            var processedSlugs = new HashSet<string>(); // QUAN TRỌNG: Track slugs đã xử lý

            foreach (var apiCategory in apiCategories)
            {
                try
                {
                    if (string.IsNullOrWhiteSpace(apiCategory.Name) || string.IsNullOrWhiteSpace(apiCategory.Slug))
                        continue;

                    // QUAN TRỌNG: Bỏ qua nếu đã xử lý slug này rồi
                    if (processedSlugs.Contains(apiCategory.Slug))
                    {
                        // Tìm category đã add vào list
                        var existingInList = syncedCategories.FirstOrDefault(c => c.Slug == apiCategory.Slug);
                        if (existingInList != null)
                        {
                            syncedCategories.Add(existingInList);
                        }
                        continue;
                    }

                    processedSlugs.Add(apiCategory.Slug);

                    // Check DB
                    var existingCategory = await _context.Categories
                        .AsNoTracking()
                        .FirstOrDefaultAsync(c => c.Slug == apiCategory.Slug);

                    if (existingCategory == null)
                    {
                        var newCategory = new Category
                        {
                            Name = apiCategory.Name,
                            Slug = apiCategory.Slug,
                            Description = null,
                            IsActive = true,
                            CreatedAt = DateTime.Now
                        };

                        _context.Categories.Add(newCategory);
                        await _context.SaveChangesAsync(); // Save ngay để có ID
                        
                        syncedCategories.Add(newCategory);
                        _logger.LogInformation($"✅ Thêm category mới: {newCategory.Name}");
                    }
                    else
                    {
                        var trackedCategory = _context.Categories.Local.FirstOrDefault(c => c.CategoryId == existingCategory.CategoryId);
                        if (trackedCategory == null)
                        {
                            _context.Categories.Attach(existingCategory);
                            trackedCategory = existingCategory;
                        }
                        
                        syncedCategories.Add(trackedCategory);
                        _logger.LogInformation($"📌 Category đã tồn tại: {trackedCategory.Name}");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Lỗi khi sync category: {apiCategory?.Name}");
                }
            }

            return syncedCategories;
        }

        // Helper: Generate slug từ tên (dùng chung cho các service khác)
        public static string GenerateSlug(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return string.Empty;

            string slug = name.ToLowerInvariant();
            slug = RemoveVietnameseTone(slug);
            slug = System.Text.RegularExpressions.Regex.Replace(slug, @"[^a-z0-9\s-]", "");
            slug = System.Text.RegularExpressions.Regex.Replace(slug, @"\s+", "-");
            slug = System.Text.RegularExpressions.Regex.Replace(slug, @"-+", "-");
            slug = slug.Trim('-');

            return slug;
        }

        private static string RemoveVietnameseTone(string text)
        {
            string[] vietnameseSigns = new string[]
            {
                "aAeEoOuUiIdDyY",
                "áàạảãâấầậẩẫăắằặẳẵ",
                "ÁÀẠẢÃÂẤẦẬẨẪĂẮẰẶẲẴ",
                "éèẹẻẽêếềệểễ",
                "ÉÈẸẺẼÊẾỀỆỂỄ",
                "óòọỏõôốồộổỗơớờợởỡ",
                "ÓÒỌỎÕÔỐỒỘỔỖƠỚỜỢỞỠ",
                "úùụủũưứừựửữ",
                "ÚÙỤỦŨƯỨỪỰỬỮ",
                "íìịỉĩ",
                "ÍÌỊỈĨ",
                "đ",
                "Đ",
                "ýỳỵỷỹ",
                "ÝỲỴỶỸ"
            };

            for (int i = 1; i < vietnameseSigns.Length; i++)
            {
                for (int j = 0; j < vietnameseSigns[i].Length; j++)
                {
                    text = text.Replace(vietnameseSigns[i][j], vietnameseSigns[0][i - 1]);
                }
            }

            return text;
        }
    }
}