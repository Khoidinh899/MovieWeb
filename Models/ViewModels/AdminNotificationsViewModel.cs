using System;
using System.Collections.Generic;
using MovieWeb.Models.Entities;

namespace MovieWeb.Models.ViewModels
{
    public class AdminNotificationsViewModel
    {
        public string ActiveTab { get; set; } = "templates"; // "templates", "send", "history"

        // Tab Mẫu
        public List<NotificationTemplate> Templates { get; set; } = new();

        // Tab Lịch sử (Đã gửi)
        public List<BroadcastHistoryItemDto> Broadcasts { get; set; } = new();
        public int CurrentPage { get; set; } = 1;
        public int TotalPages { get; set; } = 1;
        public int TotalBroadcasts { get; set; }

        // Form Gửi (nạp sẵn nếu bấm Gửi lại hoặc chọn mẫu)
        public string? PresetTitle { get; set; }
        public string? PresetContent { get; set; }
        public string? PresetUrl { get; set; }
        public string? PresetStyleJson { get; set; }
        public int TotalActiveMembers { get; set; }
    }

    public class BroadcastHistoryItemDto
    {
        public int Id { get; set; }
        public string Title { get; set; } = "";
        public string Content { get; set; } = "";
        public string? Url { get; set; }
        public string? StyleJson { get; set; }
        public string TargetType { get; set; } = "all";
        public int RecipientCount { get; set; }
        public int ReadCount { get; set; }
        public bool SendPush { get; set; }
        public string Status { get; set; } = "done";
        public DateTime CreatedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
    }
}
