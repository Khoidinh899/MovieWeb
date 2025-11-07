using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Hosting;
using System.IO;
using System.Collections.Generic;

namespace MovieWeb.Services
{
    public interface IGeminiService
    {
        Task<GeminiResponse> AnalyzeMovieRequestAsync(string userMessage, string conversationHistory = "", string mode = "by_name");
        Task<GeminiRecommendationResponse> AnalyzeRecommendationRequestAsync(string userMessage, string conversationHistory = "");
    }

    public class GeminiService : IGeminiService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<GeminiService> _logger;
        private readonly string _apiKey;
        private readonly string _model;
        private readonly string _systemPrompt;
        private readonly string _recommendationPrompt; // THÊM PROMPT MỚI

        public GeminiService(
            HttpClient httpClient,
            IConfiguration configuration,
            ILogger<GeminiService> logger,
            IWebHostEnvironment env)
        {
            _httpClient = httpClient;
            _logger = logger;

            _apiKey = Environment.GetEnvironmentVariable("GEMINI_API_KEY")
                      ?? configuration["Gemini:ApiKey"]
                      ?? throw new Exception("GEMINI_API_KEY not found");

            _model = configuration["Gemini:Model"] ?? "gemini-2.5-flash";

            _httpClient.BaseAddress = new Uri("https://generativelanguage.googleapis.com");

            // Đọc prompt cho movie request
            try
            {
                var rootPath = env.ContentRootPath;
                var promptFilePath = Path.Combine(rootPath, "AIPrompts", "ChatbotSystemPrompt.txt");
                _systemPrompt = File.ReadAllText(promptFilePath);
                _logger.LogInformation("Nạp System Prompt từ file thành công.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "LỖI: Không thể đọc file ChatbotSystemPrompt.txt");
                _systemPrompt = "Bạn là một trợ lý AI.";
            }

            // Đọc prompt cho recommendation
            try
            {
                var rootPath = env.ContentRootPath;
                var promptFilePath = Path.Combine(rootPath, "AIPrompts", "RecommendationPrompt.txt");
                _recommendationPrompt = File.ReadAllText(promptFilePath);
                _logger.LogInformation("Nạp Recommendation Prompt từ file thành công.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "LỖI: Không thể đọc file RecommendationPrompt.txt");
                _recommendationPrompt = "Bạn là trợ lý gợi ý phim.";
            }
        }

        // ===== METHOD CŨ (CẬP NHẬT NHẬN THAM SỐ MODE) =====
        public async Task<GeminiResponse> AnalyzeMovieRequestAsync(string userMessage, string conversationHistory = "", string mode = "by_name")
        {
            try
            {
                // Thêm thông tin mode vào prompt
                var modeInstruction = mode == "by_description" 
                    ? "\n\n[QUAN TRỌNG: User đang MÔ TẢ nội dung phim. Hãy SUY LUẬN ra tên phim chính thức từ mô tả đó.]"
                    : "\n\n[User đang YÊU CẦU phim theo TÊN. Hãy trích xuất tên phim từ tin nhắn.]";

                var fullPrompt = $"{_systemPrompt}{modeInstruction}\n\nUser: {userMessage}";

                var contents = new List<object>
                {
                    new { parts = new[] { new { text = fullPrompt } } }
                };

                var safetySettings = new[]
                {
                    new { category = "HARM_CATEGORY_SEXUALLY_EXPLICIT", threshold = "BLOCK_NONE" },
                    new { category = "HARM_CATEGORY_HATE_SPEECH", threshold = "BLOCK_NONE" },
                    new { category = "HARM_CATEGORY_HARASSMENT", threshold = "BLOCK_NONE" },
                    new { category = "HARM_CATEGORY_DANGEROUS_CONTENT", threshold = "BLOCK_NONE" }
                };

                var requestBody = new
                {
                    contents = contents,
                    safetySettings = safetySettings
                };

                var endpoint = $"/v1/models/{_model}:generateContent?key={_apiKey}";

                var jsonRequest = JsonSerializer.Serialize(requestBody);
                var content = new StringContent(jsonRequest, Encoding.UTF8, "application/json");

                _logger.LogInformation("Calling Gemini API (mode: {Mode})", mode);

                var response = await _httpClient.PostAsync(endpoint, content);

                if (!response.IsSuccessStatusCode)
                {
                    var error = await response.Content.ReadAsStringAsync();
                    _logger.LogError($"Gemini API Error: {response.StatusCode} - {error}");
                    return new GeminiResponse
                    {
                        Success = false, 
                        AiMessage = "Xin lỗi, AI đang tạm thời không hoạt động 😔"
                    };
                }

                var jsonResponse = await response.Content.ReadAsStringAsync();
                _logger.LogInformation("Gemini Raw JSON Response: {jsonResponse}", jsonResponse);

                var deserializeOptions = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                };
                var geminiResult = JsonSerializer.Deserialize<GeminiApiResponse>(jsonResponse, deserializeOptions);

                if (geminiResult?.Candidates == null || geminiResult.Candidates.Count == 0)
                {
                    if (jsonResponse.Contains("finishReason") && jsonResponse.Contains("SAFETY"))
                    {
                        _logger.LogWarning("Gemini returned empty response due to safety");
                        return new GeminiResponse
                        {
                            Success = false,
                            AiMessage = "Rất tiếc, AI không thể xử lý yêu cầu này (vi phạm an toàn) 😥"
                        };
                    }

                    _logger.LogWarning("Gemini returned empty response");
                    return new GeminiResponse
                    {
                        Success = false,
                        AiMessage = "Rất tiếc, AI không thể xử lý yêu cầu này 😥"
                    };
                }

                var aiText = geminiResult.Candidates[0].Content.Parts[0].Text;
                var firstBrace = aiText.IndexOf('{');
                var lastBrace = aiText.LastIndexOf('}');

                if (firstBrace == -1 || lastBrace == -1 || lastBrace < firstBrace)
                {
                    _logger.LogWarning("Không tìm thấy JSON hợp lệ trong phản hồi của AI: {aiText}", aiText);
                    return new GeminiResponse 
                    { 
                        Success = false, 
                        AiMessage = "Xin lỗi, AI đã trả về một định dạng không hợp lệ 😥"
                    };
                }

                var cleanJson = aiText.Substring(firstBrace, lastBrace - firstBrace + 1);

                _logger.LogInformation("Cleaned JSON for parsing: {cleanJson}", cleanJson);

                var parsedResponse = JsonSerializer.Deserialize<GeminiMovieAnalysis>(cleanJson, deserializeOptions);

                return new GeminiResponse
                {
                    Success = true,
                    MovieTitle = parsedResponse?.MovieTitle,
                    MovieYear = parsedResponse?.MovieYear,
                    Confidence = parsedResponse?.Confidence ?? "low",
                    NeedMoreInfo = parsedResponse?.NeedMoreInfo ?? false,
                    FollowUpQuestion = parsedResponse?.FollowUpQuestion,
                    AiMessage = parsedResponse?.AiMessage ?? "Xin lỗi, tôi không hiểu yêu cầu của bạn. 😊",
                    RawAiResponse = aiText
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi parse JSON từ Gemini");
                return new GeminiResponse
                {
                    Success = false,
                    AiMessage = $"Lỗi hệ thống AI: {ex.Message} 😵"
                };
            }
        }

        // ===== METHOD MỚI: PHÂN TÍCH YÊU CẦU GỢI Ý PHIM =====
        public async Task<GeminiRecommendationResponse> AnalyzeRecommendationRequestAsync(string userMessage, string conversationHistory = "")
        {
            try
            {
                var fullPrompt = $"{_recommendationPrompt}\n\nConversation History:\n{conversationHistory}\n\nUser: {userMessage}";

                var contents = new List<object>
                {
                    new { parts = new[] { new { text = fullPrompt } } }
                };

                var safetySettings = new[]
                {
                    new { category = "HARM_CATEGORY_SEXUALLY_EXPLICIT", threshold = "BLOCK_NONE" },
                    new { category = "HARM_CATEGORY_HATE_SPEECH", threshold = "BLOCK_NONE" },
                    new { category = "HARM_CATEGORY_HARASSMENT", threshold = "BLOCK_NONE" },
                    new { category = "HARM_CATEGORY_DANGEROUS_CONTENT", threshold = "BLOCK_NONE" }
                };

                var requestBody = new
                {
                    contents = contents,
                    safetySettings = safetySettings
                };

                var endpoint = $"/v1/models/{_model}:generateContent?key={_apiKey}";

                var jsonRequest = JsonSerializer.Serialize(requestBody);
                var content = new StringContent(jsonRequest, Encoding.UTF8, "application/json");

                _logger.LogInformation("Calling Gemini API for recommendation");

                var response = await _httpClient.PostAsync(endpoint, content);

                if (!response.IsSuccessStatusCode)
                {
                    var error = await response.Content.ReadAsStringAsync();
                    _logger.LogError($"Gemini API Error: {response.StatusCode} - {error}");
                    return new GeminiRecommendationResponse
                    {
                        Success = false, 
                        AiMessage = "Xin lỗi, AI đang tạm thời không hoạt động 😔"
                    };
                }

                var jsonResponse = await response.Content.ReadAsStringAsync();
                _logger.LogInformation("Gemini Recommendation Response: {jsonResponse}", jsonResponse);

                var deserializeOptions = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                };
                var geminiResult = JsonSerializer.Deserialize<GeminiApiResponse>(jsonResponse, deserializeOptions);

                if (geminiResult?.Candidates == null || geminiResult.Candidates.Count == 0)
                {
                    _logger.LogWarning("Gemini returned empty response");
                    return new GeminiRecommendationResponse
                    {
                        Success = false,
                        AiMessage = "Rất tiếc, AI không thể xử lý yêu cầu này 😥"
                    };
                }

                var aiText = geminiResult.Candidates[0].Content.Parts[0].Text;
                var firstBrace = aiText.IndexOf('{');
                var lastBrace = aiText.LastIndexOf('}');

                if (firstBrace == -1 || lastBrace == -1 || lastBrace < firstBrace)
                {
                    _logger.LogWarning("Không tìm thấy JSON hợp lệ: {aiText}", aiText);
                    return new GeminiRecommendationResponse 
                    { 
                        Success = false, 
                        AiMessage = "Xin lỗi, AI đã trả về một định dạng không hợp lệ 😥"
                    };
                }

                var cleanJson = aiText.Substring(firstBrace, lastBrace - firstBrace + 1);

                _logger.LogInformation("Cleaned JSON for parsing: {cleanJson}", cleanJson);

                var parsedResponse = JsonSerializer.Deserialize<GeminiRecommendationAnalysis>(cleanJson, deserializeOptions);

                return new GeminiRecommendationResponse
                {
                    Success = true,
                    NeedMoreInfo = parsedResponse?.NeedMoreInfo ?? false,
                    AiMessage = parsedResponse?.AiMessage ?? "Xin lỗi, tôi không hiểu yêu cầu của bạn. 😊",
                    Genre = parsedResponse?.Genre,
                    Country = parsedResponse?.Country,
                    Type = parsedResponse?.Type,
                    Year = parsedResponse?.Year,
                    RawAiResponse = aiText
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi parse JSON recommendation từ Gemini");
                return new GeminiRecommendationResponse
                {
                    Success = false,
                    AiMessage = $"Lỗi hệ thống AI: {ex.Message} 😵"
                };
            }
        }
    }

    // ===== RESPONSE MODELS =====

    public class GeminiResponse
    {
        public bool Success { get; set; }
        public string? MovieTitle { get; set; }
        public int? MovieYear { get; set; }
        public string Confidence { get; set; } = "low";
        public bool NeedMoreInfo { get; set; }
        public string? FollowUpQuestion { get; set; }
        public string AiMessage { get; set; } = "";
        public string? Error { get; set; }
        public string? RawAiResponse { get; set; }
        public string? MovieUrl { get; set; } // Thêm để trả về URL phim
    }

    public class GeminiMovieAnalysis
    {
        public string? MovieTitle { get; set; }
        public int? MovieYear { get; set; }
        public string Confidence { get; set; } = "low";
        public bool NeedMoreInfo { get; set; }
        public string? FollowUpQuestion { get; set; }
        public string AiMessage { get; set; } = "";
    }

    // ===== RECOMMENDATION MODELS (MỚI) =====

    public class GeminiRecommendationResponse
    {
        public bool Success { get; set; }
        public bool NeedMoreInfo { get; set; }
        public string AiMessage { get; set; } = "";
        public string? Genre { get; set; }
        public string? Country { get; set; }
        public string? Type { get; set; }
        public int? Year { get; set; }
        public string? RawAiResponse { get; set; }
    }

    public class GeminiRecommendationAnalysis
    {
        public bool NeedMoreInfo { get; set; }
        public string AiMessage { get; set; } = "";
        public string? Genre { get; set; }
        public string? Country { get; set; }
        public string? Type { get; set; }
        public int? Year { get; set; }
    }

    // ===== GEMINI API RESPONSE MODELS =====

    public class GeminiApiResponse
    {
        public List<GeminiCandidate> Candidates { get; set; } = new();
    }

    public class GeminiCandidate
    {
        public GeminiContent Content { get; set; } = new();
    }

    public class GeminiContent
    {
        public List<GeminiPart> Parts { get; set; } = new();
    }

    public class GeminiPart
    {
        public string Text { get; set; } = "";
    }
}