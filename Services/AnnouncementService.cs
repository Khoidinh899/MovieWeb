using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using MovieWeb.Models.DTOs;
using MovieWeb.Services.Interfaces;

namespace MovieWeb.Services
{
    public class AnnouncementService : IAnnouncementService
    {
        private readonly IWebHostEnvironment _env;
        private readonly IMemoryCache _cache;
        private readonly ILogger<AnnouncementService> _logger;
        private static readonly SemaphoreSlim _fileLock = new SemaphoreSlim(1, 1);
        private const string CacheKey = "SystemAnnouncementSettings";
        private readonly string _filePath;

        public AnnouncementService(IWebHostEnvironment env, IMemoryCache cache, ILogger<AnnouncementService> logger)
        {
            _env = env;
            _cache = cache;
            _logger = logger;

            var dataDir = Path.Combine(_env.ContentRootPath, "App_Data");
            if (!Directory.Exists(dataDir))
            {
                Directory.CreateDirectory(dataDir);
            }
            _filePath = Path.Combine(dataDir, "announcement_settings.json");
        }

        public async Task<AnnouncementSettingsDto> GetSettingsAsync()
        {
            if (_cache.TryGetValue(CacheKey, out AnnouncementSettingsDto? cachedSettings) && cachedSettings != null)
            {
                return cachedSettings;
            }

            AnnouncementSettingsDto settings;
            if (File.Exists(_filePath))
            {
                try
                {
                    await _fileLock.WaitAsync();
                    try
                    {
                        var json = await File.ReadAllTextAsync(_filePath);
                        settings = JsonSerializer.Deserialize<AnnouncementSettingsDto>(json, new JsonSerializerOptions
                        {
                            PropertyNameCaseInsensitive = true
                        }) ?? new AnnouncementSettingsDto();
                    }
                    finally
                    {
                        _fileLock.Release();
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error reading announcement settings from file, using defaults.");
                    settings = new AnnouncementSettingsDto();
                }
            }
            else
            {
                settings = new AnnouncementSettingsDto();
            }

            _cache.Set(CacheKey, settings, TimeSpan.FromMinutes(30));
            return settings;
        }

        public async Task<bool> SaveSettingsAsync(AnnouncementSettingsDto settings)
        {
            if (settings == null) return false;

            settings.UpdatedAt = DateTime.Now;
            try
            {
                await _fileLock.WaitAsync();
                try
                {
                    var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions
                    {
                        WriteIndented = true
                    });
                    await File.WriteAllTextAsync(_filePath, json);
                }
                finally
                {
                    _fileLock.Release();
                }

                // Cập nhật lại cache ngay lập tức
                _cache.Set(CacheKey, settings, TimeSpan.FromMinutes(30));
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving announcement settings to file.");
                return false;
            }
        }
    }
}
