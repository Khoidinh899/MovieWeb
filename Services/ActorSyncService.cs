// File: Services/ActorSyncService.cs (FIXED - Không duplicate)
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
    public interface IActorSyncService
    {
        Task<List<Actor>> SyncActorsAsync(List<string> apiActors);
    }

    public class ActorSyncService : IActorSyncService
    {
        private readonly MovieWebDbContext _context;
        private readonly ILogger<ActorSyncService> _logger;

        public ActorSyncService(MovieWebDbContext context, ILogger<ActorSyncService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<List<Actor>> SyncActorsAsync(List<string> apiActors)
        {
            if (apiActors == null || !apiActors.Any())
            {
                return new List<Actor>();
            }

            var syncedActors = new List<Actor>();
            var processedSlugs = new HashSet<string>(); // QUAN TRỌNG: Track slugs đã xử lý

            foreach (var actorName in apiActors)
            {
                try
                {
                    if (string.IsNullOrWhiteSpace(actorName))
                        continue;

                    string cleanName = actorName.Trim();
                    string slug = CategorySyncService.GenerateSlug(cleanName);

                    if (string.IsNullOrWhiteSpace(slug))
                        continue;

                    // QUAN TRỌNG: Bỏ qua nếu đã xử lý slug này rồi
                    if (processedSlugs.Contains(slug))
                    {
                        // Tìm actor đã add vào list
                        var existingInList = syncedActors.FirstOrDefault(a => a.Slug == slug);
                        if (existingInList != null)
                        {
                            syncedActors.Add(existingInList);
                        }
                        continue;
                    }

                    processedSlugs.Add(slug);

                    // Check DB
                    var existingActor = await _context.Actors
                        .AsNoTracking()
                        .FirstOrDefaultAsync(a => a.Slug == slug);

                    if (existingActor == null)
                    {
                        var newActor = new Actor
                        {
                            Name = cleanName,
                            Slug = slug,
                            Avatar = null,
                            Biography = null,
                            IsActive = true,
                            CreatedAt = DateTime.Now
                        };

                        _context.Actors.Add(newActor);
                        await _context.SaveChangesAsync(); // Save ngay để có ID
                        
                        syncedActors.Add(newActor);
                        _logger.LogInformation($"✅ Thêm actor mới: {newActor.Name} (slug: {slug})");
                    }
                    else
                    {
                        var trackedActor = _context.Actors.Local.FirstOrDefault(a => a.ActorId == existingActor.ActorId);
                        if (trackedActor == null)
                        {
                            _context.Actors.Attach(existingActor);
                            trackedActor = existingActor;
                        }
                        
                        syncedActors.Add(trackedActor);
                        _logger.LogInformation($"📌 Actor đã tồn tại: {trackedActor.Name}");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Lỗi khi sync actor: {actorName}");
                }
            }

            return syncedActors;
        }
    }
}