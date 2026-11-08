using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MovieWeb.Helpers;
using MovieWeb.Services.Interfaces;

namespace MovieWeb.Services;

public class IndexNowService : IIndexNowService
{
    private const string DefaultKey = "7b3e8a91c2f445d09b6a1248e3d5f89a";
    private const string BaseHost = "moonphim.me";
    private const string IndexNowApiEndpoint = "https://api.indexnow.org/indexnow";
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<IndexNowService> _logger;

    public IndexNowService(HttpClient httpClient, IConfiguration configuration, ILogger<IndexNowService> logger)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _logger = logger;
    }

    public string GetIndexNowKey()
    {
        return _configuration["IndexNow:Key"] ?? DefaultKey;
    }

    public async Task<bool> SubmitUrlsAsync(IEnumerable<string> urls)
    {
        var urlList = urls?.Where(u => !string.IsNullOrWhiteSpace(u)).Distinct().ToList();
        if (urlList == null || !urlList.Any()) return true;

        var key = GetIndexNowKey();
        var payload = new
        {
            host = BaseHost,
            key = key,
            keyLocation = $"https://{BaseHost}/{key}.txt",
            urlList = urlList
        };

        try
        {
            var json = JsonSerializer.Serialize(payload);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync(IndexNowApiEndpoint, content);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Successfully submitted {Count} URLs to IndexNow API", urlList.Count);
            }
            else
            {
                var respBody = await response.Content.ReadAsStringAsync();
                _logger.LogWarning("IndexNow API returned non-success code {StatusCode}: {Body}", response.StatusCode, respBody);
            }

            // Đồng thời ping Google Search Console cập nhật Sitemap
            _ = Task.Run(async () =>
            {
                try
                {
                    var pingUrl = $"https://www.google.com/ping?sitemap=https://{BaseHost}/sitemap.xml";
                    await _httpClient.GetAsync(pingUrl);
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Google sitemap ping ignored");
                }
            });

            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to submit URLs to IndexNow API");
            return false;
        }
    }

    public async Task<bool> NotifyNewMovieOrEpisodeAsync(string movieSlug, int movieId, string? episodeSlug = null, int? episodeId = null)
    {
        var urls = new List<string>
        {
            $"https://{BaseHost}{SeoUrlHelper.MovieUrl(movieSlug, movieId)}"
        };

        if (!string.IsNullOrWhiteSpace(episodeSlug) && episodeId.HasValue)
        {
            urls.Add($"https://{BaseHost}{SeoUrlHelper.EpisodeUrl(movieSlug, episodeSlug, episodeId.Value)}");
        }

        return await SubmitUrlsAsync(urls);
    }
}
