using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;
using MovieWeb.Models.DTOs;
using MovieWeb.Services.Interfaces;

namespace MovieWeb.Services
{
    public class AnnouncementService : IAnnouncementService
    {
        private readonly ISiteSettingsService _siteSettings;
        private readonly IWebHostEnvironment _env;
        private readonly ILogger<AnnouncementService> _logger;
        private const string SettingKey = "announcement.settings";
        private static readonly SemaphoreSlim _importLock = new(1, 1);
        private static bool _importedCheckDone = false;

        public AnnouncementService(
            ISiteSettingsService siteSettings,
            IWebHostEnvironment env,
            ILogger<AnnouncementService> logger)
        {
            _siteSettings = siteSettings;
            _env = env;
            _logger = logger;
        }

        public async Task<AnnouncementSettingsDto> GetSettingsAsync()
        {
            var settings = await _siteSettings.GetAsync<AnnouncementSettingsDto>(SettingKey);

            if (settings == null && !_importedCheckDone)
            {
                await _importLock.WaitAsync();
                try
                {
                    // Re-check after acquiring lock
                    settings = await _siteSettings.GetAsync<AnnouncementSettingsDto>(SettingKey);
                    if (settings == null)
                    {
                        var dataDir = Path.Combine(_env.ContentRootPath, "App_Data");
                        var legacyFilePath = Path.Combine(dataDir, "announcement_settings.json");

                        if (File.Exists(legacyFilePath))
                        {
                            try
                            {
                                var json = await File.ReadAllTextAsync(legacyFilePath);
                                settings = JsonSerializer.Deserialize<AnnouncementSettingsDto>(json, new JsonSerializerOptions
                                {
                                    PropertyNameCaseInsensitive = true
                                });

                                if (settings != null)
                                {
                                    NormalizeSettings(settings);
                                    await _siteSettings.SetAsync(SettingKey, settings);
                                    _logger.LogInformation("Imported announcement settings from legacy App_Data/announcement_settings.json into SiteSettings DB.");
                                }
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(ex, "Failed to import legacy announcement settings from {Path}.", legacyFilePath);
                            }
                        }

                        if (settings == null)
                        {
                            settings = new AnnouncementSettingsDto();
                            NormalizeSettings(settings);
                        }
                    }
                    _importedCheckDone = true;
                }
                finally
                {
                    _importLock.Release();
                }
            }

            if (settings == null)
            {
                settings = new AnnouncementSettingsDto();
            }

            NormalizeSettings(settings);
            return settings;
        }

        public async Task<bool> SaveSettingsAsync(AnnouncementSettingsDto settings)
        {
            if (settings == null) return false;

            try
            {
                settings.UpdatedAt = DateTime.Now;
                NormalizeSettings(settings);
                await _siteSettings.SetAsync(SettingKey, settings);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving announcement settings to SiteSettings.");
                return false;
            }
        }

        private static void NormalizeSettings(AnnouncementSettingsDto s)
        {
            if (string.IsNullOrWhiteSpace(s.FrequencyMode))
            {
                s.FrequencyMode = s.ShowOncePerUser ? "once" : "always";
            }
            if (s.IntervalHours <= 0) s.IntervalHours = 24;
            if (s.DelaySeconds < 0) s.DelaySeconds = 1;
            if (string.IsNullOrWhiteSpace(s.Audience)) s.Audience = "all";
        }
    }
}
