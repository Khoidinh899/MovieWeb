using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using MovieWeb.Data;
using MovieWeb.Models.Entities;
using MovieWeb.Services.Interfaces;

namespace MovieWeb.Services
{
    public class SiteSettingsService : ISiteSettingsService
    {
        private readonly MovieWebDbContext _context;
        private readonly IMemoryCache _cache;
        private readonly ILogger<SiteSettingsService> _logger;
        private const string CachePrefix = "SiteSettings_";
        private static readonly TimeSpan CacheDuration = TimeSpan.FromSeconds(60);

        private static readonly JsonSerializerOptions _jsonOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            WriteIndented = true
        };

        public SiteSettingsService(
            MovieWebDbContext context,
            IMemoryCache cache,
            ILogger<SiteSettingsService> logger)
        {
            _context = context;
            _cache = cache;
            _logger = logger;
        }

        private static string GetCacheKey(string key) => CachePrefix + key;

        public async Task<T?> GetAsync<T>(string key)
        {
            if (string.IsNullOrWhiteSpace(key)) return default;

            var cacheKey = GetCacheKey(key);
            if (_cache.TryGetValue(cacheKey, out T? cachedValue))
            {
                return cachedValue;
            }

            try
            {
                var setting = await _context.SiteSettings
                    .AsNoTracking()
                    .FirstOrDefaultAsync(s => s.Key == key);

                T? result = default;
                if (setting != null && !string.IsNullOrWhiteSpace(setting.Value))
                {
                    result = JsonSerializer.Deserialize<T>(setting.Value, _jsonOptions);
                }

                _cache.Set(cacheKey, result, CacheDuration);
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error reading site setting {Key} from database.", key);
                return default;
            }
        }

        public T? Get<T>(string key)
        {
            if (string.IsNullOrWhiteSpace(key)) return default;

            var cacheKey = GetCacheKey(key);
            if (_cache.TryGetValue(cacheKey, out T? cachedValue))
            {
                return cachedValue;
            }

            try
            {
                var setting = _context.SiteSettings
                    .AsNoTracking()
                    .FirstOrDefault(s => s.Key == key);

                T? result = default;
                if (setting != null && !string.IsNullOrWhiteSpace(setting.Value))
                {
                    result = JsonSerializer.Deserialize<T>(setting.Value, _jsonOptions);
                }

                _cache.Set(cacheKey, result, CacheDuration);
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error reading site setting {Key} from database.", key);
                return default;
            }
        }

        public async Task SetAsync<T>(string key, T value)
        {
            if (string.IsNullOrWhiteSpace(key)) return;

            var cacheKey = GetCacheKey(key);
            try
            {
                var json = JsonSerializer.Serialize(value, _jsonOptions);
                var setting = await _context.SiteSettings.FirstOrDefaultAsync(s => s.Key == key);

                if (setting == null)
                {
                    setting = new SiteSetting
                    {
                        Key = key,
                        Value = json,
                        UpdatedAt = DateTime.Now
                    };
                    _context.SiteSettings.Add(setting);
                }
                else
                {
                    setting.Value = json;
                    setting.UpdatedAt = DateTime.Now;
                }

                await _context.SaveChangesAsync();
                _cache.Remove(cacheKey);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving site setting {Key} to database.", key);
                throw;
            }
        }

        public void Set<T>(string key, T value)
        {
            if (string.IsNullOrWhiteSpace(key)) return;

            var cacheKey = GetCacheKey(key);
            try
            {
                var json = JsonSerializer.Serialize(value, _jsonOptions);
                var setting = _context.SiteSettings.FirstOrDefault(s => s.Key == key);

                if (setting == null)
                {
                    setting = new SiteSetting
                    {
                        Key = key,
                        Value = json,
                        UpdatedAt = DateTime.Now
                    };
                    _context.SiteSettings.Add(setting);
                }
                else
                {
                    setting.Value = json;
                    setting.UpdatedAt = DateTime.Now;
                }

                _context.SaveChanges();
                _cache.Remove(cacheKey);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving site setting {Key} to database.", key);
                throw;
            }
        }

        public async Task RemoveAsync(string key)
        {
            if (string.IsNullOrWhiteSpace(key)) return;

            var cacheKey = GetCacheKey(key);
            try
            {
                var setting = await _context.SiteSettings.FirstOrDefaultAsync(s => s.Key == key);
                if (setting != null)
                {
                    _context.SiteSettings.Remove(setting);
                    await _context.SaveChangesAsync();
                }
                _cache.Remove(cacheKey);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing site setting {Key} from database.", key);
                throw;
            }
        }

        public void Remove(string key)
        {
            if (string.IsNullOrWhiteSpace(key)) return;

            var cacheKey = GetCacheKey(key);
            try
            {
                var setting = _context.SiteSettings.FirstOrDefault(s => s.Key == key);
                if (setting != null)
                {
                    _context.SiteSettings.Remove(setting);
                    _context.SaveChanges();
                }
                _cache.Remove(cacheKey);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing site setting {Key} from database.", key);
                throw;
            }
        }
    }
}
