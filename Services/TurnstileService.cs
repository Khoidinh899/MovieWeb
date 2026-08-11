using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MovieWeb.Services.Interfaces;

namespace MovieWeb.Services
{
    public class TurnstileService : ITurnstileService
    {
        private readonly HttpClient _httpClient;
        private readonly string _secretKey;
        private readonly ILogger<TurnstileService> _logger;

        public TurnstileService(
            HttpClient httpClient, 
            IConfiguration configuration, 
            ILogger<TurnstileService> logger)
        {
            _httpClient = httpClient;
            _secretKey = configuration["Turnstile:SecretKey"] ?? string.Empty;
            _logger = logger;
        }

        public async Task<bool> VerifyTokenAsync(string token, string? ipAddress)
        {
            if (string.IsNullOrEmpty(token))
            {
                _logger.LogWarning("Turnstile token is empty.");
                return false;
            }

            if (string.IsNullOrEmpty(_secretKey))
            {
                _logger.LogError("Turnstile SecretKey is not configured.");
                return false;
            }

            try
            {
                var content = new FormUrlEncodedContent(new[]
                {
                    new KeyValuePair<string, string>("secret", _secretKey),
                    new KeyValuePair<string, string>("response", token),
                    new KeyValuePair<string, string>("remoteip", ipAddress ?? "")
                });

                var response = await _httpClient.PostAsync("https://challenges.cloudflare.com/turnstile/v0/siteverify", content);
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("Cloudflare Turnstile verification failed with status: {StatusCode}", response.StatusCode);
                    return false;
                }

                var jsonString = await response.Content.ReadAsStringAsync();
                var result = JsonSerializer.Deserialize<TurnstileVerificationResult>(jsonString, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (result == null || !result.Success)
                {
                    _logger.LogWarning("Turnstile verification returned failure. Error codes: {Errors}",
                        result?.ErrorCodes != null ? string.Join(", ", result.ErrorCodes) : "none");
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error verifying Turnstile token");
                return false;
            }
        }

        private class TurnstileVerificationResult
        {
            public bool Success { get; set; }
            public string[]? ErrorCodes { get; set; }
        }
    }
}
