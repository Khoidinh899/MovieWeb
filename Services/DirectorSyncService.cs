// File: Services/DirectorSyncService.cs (FIXED - Không duplicate)
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MovieWeb.Data;
using MovieWeb.Models.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace MovieWeb.Services
{
    public interface IDirectorSyncService
    {
        Task<List<Director>> SyncDirectorsAsync(List<string> apiDirectors);
    }

    public class DirectorSyncService : IDirectorSyncService
    {
        private readonly MovieWebDbContext _context;
        private readonly ILogger<DirectorSyncService> _logger;

        public DirectorSyncService(MovieWebDbContext context, ILogger<DirectorSyncService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<List<Director>> SyncDirectorsAsync(List<string> apiDirectors)
        {
            if (apiDirectors == null || !apiDirectors.Any())
            {
                return new List<Director>();
            }

            var syncedDirectors = new List<Director>();
            var processedSlugs = new HashSet<string>(); // QUAN TRỌNG: Track slugs đã xử lý

            foreach (var directorName in apiDirectors)
            {
                try
                {
                    if (string.IsNullOrWhiteSpace(directorName))
                        continue;

                    string cleanName = directorName.Trim();
                    string slug = CategorySyncService.GenerateSlug(cleanName);

                    if (string.IsNullOrWhiteSpace(slug))
                        continue;

                    // QUAN TRỌNG: Bỏ qua nếu đã xử lý slug này rồi
                    if (processedSlugs.Contains(slug))
                    {
                        // Tìm director đã add vào list
                        var existingInList = syncedDirectors.FirstOrDefault(d => d.Slug == slug);
                        if (existingInList != null)
                        {
                            syncedDirectors.Add(existingInList);
                        }
                        continue;
                    }

                    processedSlugs.Add(slug);

                    // Check DB
                    var existingDirector = await _context.Directors
                        .AsNoTracking()
                        .FirstOrDefaultAsync(d => d.Slug == slug);

                    if (existingDirector == null)
                    {
                        var newDirector = new Director
                        {
                            Name = cleanName,
                            Slug = slug,
                            Avatar = null,
                            Biography = null,
                            IsActive = true,
                            CreatedAt = DateTime.Now
                        };

                        _context.Directors.Add(newDirector);
                        await _context.SaveChangesAsync(); // Save ngay để có ID
                        
                        syncedDirectors.Add(newDirector);
                        _logger.LogInformation($"✅ Thêm director mới: {newDirector.Name} (slug: {slug})");
                    }
                    else
                    {
                        var trackedDirector = _context.Directors.Local.FirstOrDefault(d => d.DirectorId == existingDirector.DirectorId);
                        if (trackedDirector == null)
                        {
                            _context.Directors.Attach(existingDirector);
                            trackedDirector = existingDirector;
                        }
                        
                        syncedDirectors.Add(trackedDirector);
                        _logger.LogInformation($"📌 Director đã tồn tại: {trackedDirector.Name}");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Lỗi khi sync director: {directorName}");
                }
            }

            return syncedDirectors;
        }
    }
}