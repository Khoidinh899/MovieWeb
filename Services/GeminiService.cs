using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Hosting; // <-- THÊM THƯ VIỆN NÀY
using System.IO;                  // <-- THÊM THƯ VIỆN NÀY
using System.Collections.Generic; // <-- THÊM THƯ VIỆN NÀY

namespace MovieWeb.Services
{
    public interface IGeminiService
    {
        Task<GeminiResponse> AnalyzeMovieRequestAsync(string userMessage, string conversationHistory = "");
    }

    public class GeminiService : IGeminiService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<GeminiService> _logger;
        private readonly string _apiKey;
        private readonly string _model;
        private readonly string _systemPrompt; // <-- Sẽ lưu prompt ở đây

        public GeminiService(
            HttpClient httpClient,
            IConfiguration configuration,
            ILogger<GeminiService> logger,
            IWebHostEnvironment env) // <-- THÊM IWebHostEnvironment
        {
            _httpClient = httpClient;
            _logger = logger;

            _apiKey = Environment.GetEnvironmentVariable("GEMINI_API_KEY")
                      ?? configuration["Gemini:ApiKey"]
                      ?? throw new Exception("GEMINI_API_KEY not found");

            _model = configuration["Gemini:Model"] ?? "gemini-2.5-flash"; // Dùng model mới

            _httpClient.BaseAddress = new Uri("https://generativelanguage.googleapis.com");

            // --- ĐỌC PROMPT TỪ FILE KHI SERVICE KHỞI ĐỘNG ---
            try
            {
                // Lấy đường dẫn gốc của project
                var rootPath = env.ContentRootPath;
                // Nối đường dẫn đến file prompt
                var promptFilePath = Path.Combine(rootPath, "AIPrompts", "ChatbotSystemPrompt.txt");
                // Đọc file và lưu vào biến _systemPrompt
                _systemPrompt = File.ReadAllText(promptFilePath);
                _logger.LogInformation("Nạp System Prompt từ file thành công.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "LỖI CHẾT NGƯỜI: Không thể đọc file ChatbotSystemPrompt.txt");
                _systemPrompt = "Bạn là một trợ lý AI."; // Một prompt dự phòng
            }
        }

        public async Task<GeminiResponse> AnalyzeMovieRequestAsync(string userMessage, string conversationHistory = "")
        {
            try
            {
                // === BƯỚC 1: QUAY VỀ CÁCH LÀM GỐC ===
                // Gộp chung System Prompt và User Message làm 1
                // (Sau này bạn có thể chèn conversationHistory vào giữa)
                var fullPrompt = $"{_systemPrompt}\n\nUser: {userMessage}";

                // 1. Chỉ tạo 'contents' (NỘI DUNG)
                var contents = new List<object>
                {
                    // Lưu ý: API v1 không có 'role', chỉ có 'parts'
                    new { parts = new[] { new { text = fullPrompt } } }
                };

                // 2. Chỉ tạo 'safetySettings' (TẮT BỘ LỌC)
                var safetySettings = new[]
                {
                    new { category = "HARM_CATEGORY_SEXUALLY_EXPLICIT", threshold = "BLOCK_NONE" },
                    new { category = "HARM_CATEGORY_HATE_SPEECH", threshold = "BLOCK_NONE" },
                    new { category = "HARM_CATEGORY_HARASSMENT", threshold = "BLOCK_NONE" },
                    new { category = "HARM_CATEGORY_DANGEROUS_CONTENT", threshold = "BLOCK_NONE" }
                };

                // 3. Gộp lại thành Request Body SIÊU ĐƠN GIẢN
                // KHÔNG CÓ systemInstruction, KHÔNG CÓ generationConfig
                var requestBody = new
                {
                    contents = contents,
                    safetySettings = safetySettings
                };

                // === BƯỚC 2: QUAY VỀ ENDPOINT v1 ===
                // (Vì v1 hỗ trợ gemini-2.5-flash)
                var endpoint = $"/v1/models/{_model}:generateContent?key={_apiKey}";

                // === BƯỚC 3: GỬI REQUEST ===
                var jsonRequest = JsonSerializer.Serialize(requestBody);
                var content = new StringContent(jsonRequest, Encoding.UTF8, "application/json");

                // === LOG DEBUG ===
                _logger.LogWarning("==================================================");
                _logger.LogWarning("GỌI LẦN CUỐI (v1): {endpoint}", endpoint);
                _logger.LogWarning("VỚI JSON (v1): {jsonRequest}", jsonRequest);
                _logger.LogWarning("==================================================");
                // =================

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
    PropertyNameCaseInsensitive = true // <-- Bảo C# đừng phân biệt hoa/thường
};
var geminiResult = JsonSerializer.Deserialize<GeminiApiResponse>(jsonResponse, deserializeOptions);

                // KIỂM TRA LỖI DO BỘ LỌC (DÙ ĐÃ TẮT)
                if (geminiResult?.Candidates == null || geminiResult.Candidates.Count == 0)
                {
                    // Đôi khi AI vẫn chặn dù đã set BLOCK_NONE
                    // Kiểm tra xem có phải do bị chặn không
                    if (jsonResponse.Contains("finishReason") && jsonResponse.Contains("SAFETY"))
                    {
                        _logger.LogWarning("Gemini returned empty response DO BỊ CHẶN AN TOÀN (dù đã tắt)");
                        return new GeminiResponse
                        {
                            Success = false,
                            AiMessage = "Rất tiếc, AI không thể xử lý yêu cầu này (vi phạm an toàn) 😥"
                        };
                    }

                    _logger.LogWarning("Gemini returned empty response (Lý do không rõ)");
                    return new GeminiResponse
                    {
                        Success = false,
                        AiMessage = "Rất tiếc, AI không thể xử lý yêu cầu này 😥"
                    };
                }

                var aiText = geminiResult.Candidates[0].Content.Parts[0].Text;
                // 1. Tìm vị trí { đầu tiên
var firstBrace = aiText.IndexOf('{');
// 2. Tìm vị trí } cuối cùng
var lastBrace = aiText.LastIndexOf('}');

// 3. Kiểm tra xem có tìm thấy không
if (firstBrace == -1 || lastBrace == -1 || lastBrace < firstBrace)
{
    _logger.LogWarning("Không tìm thấy JSON hợp lệ trong phản hồi của AI: {aiText}", aiText);
    return new GeminiResponse 
    { 
        Success = false, 
        AiMessage = "Xin lỗi, AI đã trả về một định dạng không hợp lệ 😥"
    };
}

// 4. Cắt chuỗi JSON ra
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
                // Lỗi này thường xảy ra khi AI không trả về JSON (ví dụ trả về "OK")
                // khiến hàm JsonSerializer.Deserialize<GeminiMovieAnalysis>(cleanJson) bị crash
                _logger.LogError(ex, "Lỗi khi parse JSON từ Gemini");
                return new GeminiResponse
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
        public string Confidence { get; set; } = "low"; // high, medium, low
        public bool NeedMoreInfo { get; set; }
        public string? FollowUpQuestion { get; set; }
        public string AiMessage { get; set; } = ""; // Tin nhắn thân thiện từ Mooner
        public string? Error { get; set; }
        public string? RawAiResponse { get; set; }
    }

    public class GeminiMovieAnalysis
    {
        public string? MovieTitle { get; set; }
        public int? MovieYear { get; set; }
        public string Confidence { get; set; } = "low";
        public bool NeedMoreInfo { get; set; }
        public string? FollowUpQuestion { get; set; }
        public string AiMessage { get; set; } = ""; // Tin nhắn từ Mooner
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