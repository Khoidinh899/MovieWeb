// File: Services/CountrySyncService.cs (FIXED - Không duplicate)
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
    public interface ICountrySyncService
    {
        Task<List<Country>> SyncCountriesAsync(List<MovieWeb.Models.API.Country> apiCountries);
    }

    public class CountrySyncService : ICountrySyncService
    {
        private readonly MovieWebDbContext _context;
        private readonly ILogger<CountrySyncService> _logger;

        public CountrySyncService(MovieWebDbContext context, ILogger<CountrySyncService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<List<Country>> SyncCountriesAsync(List<MovieWeb.Models.API.Country> apiCountries)
        {
            if (apiCountries == null || !apiCountries.Any())
            {
                return new List<Country>();
            }

            var syncedCountries = new List<Country>();
            var processedSlugs = new HashSet<string>(); // QUAN TRỌNG: Track slugs đã xử lý

            foreach (var apiCountry in apiCountries)
            {
                try
                {
                    if (string.IsNullOrWhiteSpace(apiCountry.Name) || string.IsNullOrWhiteSpace(apiCountry.Slug))
                        continue;

                    // QUAN TRỌNG: Bỏ qua nếu đã xử lý slug này rồi
                    if (processedSlugs.Contains(apiCountry.Slug))
                    {
                        // Tìm country đã add vào list
                        var existingInList = syncedCountries.FirstOrDefault(c => c.Slug == apiCountry.Slug);
                        if (existingInList != null)
                        {
                            syncedCountries.Add(existingInList);
                        }
                        continue;
                    }

                    processedSlugs.Add(apiCountry.Slug);

                    // Check DB
                    var existingCountry = await _context.Countries
                        .AsNoTracking()
                        .FirstOrDefaultAsync(c => c.Slug == apiCountry.Slug);

                    if (existingCountry == null)
                    {
                        var newCountry = new Country
                        {
                            Name = apiCountry.Name,
                            Slug = apiCountry.Slug,
                            IsActive = true,
                            CreatedAt = DateTime.Now
                        };

                        _context.Countries.Add(newCountry);
                        await _context.SaveChangesAsync(); // Save ngay để có ID
                        
                        syncedCountries.Add(newCountry);
                        _logger.LogInformation($"✅ Thêm country mới: {newCountry.Name}");
                    }
                    else
                    {
                        var trackedCountry = _context.Countries.Local.FirstOrDefault(c => c.CountryId == existingCountry.CountryId);
                        if (trackedCountry == null)
                        {
                            _context.Countries.Attach(existingCountry);
                            trackedCountry = existingCountry;
                        }
                        
                        syncedCountries.Add(trackedCountry);
                        _logger.LogInformation($"📌 Country đã tồn tại: {trackedCountry.Name}");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Lỗi khi sync country: {apiCountry?.Name}");
                }
            }

            return syncedCountries;
        }
    }
}