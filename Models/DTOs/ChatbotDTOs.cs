using System;
using System.Collections.Generic;

namespace MovieWeb.Models.DTOs
{
    // ===== CHAT MESSAGE RESPONSE =====
    
    /// <summary>
    /// DTO chuẩn cho tất cả tin nhắn gửi về client qua SignalR
    /// </summary>
    public class ChatMessageResponse
    {
        public string Role { get; set; } = ""; // "user" hoặc "mooner"
        public string Message { get; set; } = "";
        public DateTime Timestamp { get; set; }
        
        // Optional fields cho các trường hợp đặc biệt
        public string? MovieUrl { get; set; }
        public int? RequestId { get; set; }
        public bool? HasMovie { get; set; }
    }

    // ===== TYPING INDICATOR =====
    
    /// <summary>
    /// DTO cho typing indicator
    /// </summary>
    public class BotTypingResponse
    {
        public bool IsTyping { get; set; }
    }

    // ===== CHAT HISTORY DTOs =====
    
    /// <summary>
    /// DTO cho lịch sử chat (dùng cho export hoặc admin view)
    /// </summary>
    public class ChatHistoryDto
    {
        public string UserId { get; set; } = "";
        public List<ChatMessageDto> Messages { get; set; } = new();
        public int AttemptCount { get; set; }
        public DateTime SessionStarted { get; set; }
    }

    /// <summary>
    /// DTO cho một tin nhắn trong lịch sử
    /// </summary>
    public class ChatMessageDto
    {
        public string Role { get; set; } = "";
        public string Message { get; set; } = "";
        public DateTime Timestamp { get; set; }
    }

    /// <summary>
    /// DTO cho session info (dùng cho monitoring/debugging)
    /// </summary>
    public class ChatSessionInfo
    {
        public string UserId { get; set; } = "";
        public int MessageCount { get; set; }
        public int AttemptCount { get; set; }
        public DateTime CreatedAt { get; set; }
        public TimeSpan Duration { get; set; }
        public bool IsActive { get; set; }
    }

    // ===== ADMIN DTOs =====
    
    /// <summary>
    /// DTO cho admin xem tất cả active sessions
    /// </summary>
    public class ActiveSessionsResponse
    {
        public int TotalSessions { get; set; }
        public List<ChatSessionInfo> Sessions { get; set; } = new();
        public DateTime RetrievedAt { get; set; }
    }
}