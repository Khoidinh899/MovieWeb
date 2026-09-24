using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using MovieWeb.Models.Entities;

namespace MovieWeb.Models.ViewModels.WatchParty
{
    public class WatchPartyHubViewModel
    {
        public List<WatchPartyRoomCardDto> PublicRooms { get; set; } = new();
        public List<WatchPartyRoomCardDto> PrivateRooms { get; set; } = new();
        public int TotalActiveRooms { get; set; }
        public int TotalWatchingUsers { get; set; }
        public string? CurrentUserFullName { get; set; }
    }

    public class WatchPartyRoomCardDto
    {
        public Guid Id { get; set; }
        public string RoomCode { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string? ShareToken { get; set; }
        public int MovieId { get; set; }
        public string MovieTitle { get; set; } = string.Empty;
        public string MovieSlug { get; set; } = string.Empty;
        public string? PosterUrl { get; set; }
        public string? ThumbUrl { get; set; }
        public int? EpisodeNumber { get; set; }
        public string? ServerName { get; set; }
        public int HostUserId { get; set; }
        public string HostName { get; set; } = string.Empty;
        public string? HostAvatar { get; set; }
        public bool IsPlaying { get; set; }
        public bool IsPrivate { get; set; }
        public int MaxMembers { get; set; }
        public int CurrentMembersCount { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class WatchPartyRoomViewModel
    {
        public Guid RoomId { get; set; }
        public string RoomCode { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string? ShareToken { get; set; }
        public bool IsPrivate { get; set; }
        public bool OnlyHostControl { get; set; }
        public bool AllowDanmaku { get; set; } = true;
        public int MaxMembers { get; set; }
        public double CurrentTime { get; set; }
        public bool IsPlaying { get; set; }

        // Host info
        public int HostUserId { get; set; }
        public string HostName { get; set; } = string.Empty;
        public string? HostAvatar { get; set; }
        public bool IsCurrentUserHost { get; set; }

        // Current User info
        public int CurrentUserId { get; set; }
        public string CurrentUserName { get; set; } = string.Empty;
        public string? CurrentUserAvatar { get; set; }

        // Movie & Episode details
        public Movie Movie { get; set; } = null!;
        public int? CurrentEpisodeId { get; set; }
        public int? CurrentEpisodeNumber { get; set; }
        public string? CurrentServerName { get; set; }
        public string? VideoStreamUrl { get; set; }
        public Dictionary<string, List<Episode>> GroupedEpisodes { get; set; } = new();
        public Dictionary<string, string> ServerDisplayNames { get; set; } = new();
        public string DefaultServer { get; set; } = string.Empty;

        // Share Link
        public string ShareUrl { get; set; } = string.Empty;
    }

    public class CreateWatchPartyInput
    {
        [Required(ErrorMessage = "Vui lòng chọn phim")]
        public int MovieId { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập tên phòng")]
        [StringLength(200, ErrorMessage = "Tên phòng tối đa 200 ký tự")]
        public string Title { get; set; } = string.Empty;

        public int? EpisodeId { get; set; }
        public int? EpisodeNumber { get; set; }
        public string? ServerName { get; set; }

        public bool IsPrivate { get; set; }

        [RegularExpression(@"^\d{6}$", ErrorMessage = "Mã PIN phòng phải bao gồm đúng 6 chữ số")]
        [StringLength(6, MinimumLength = 6, ErrorMessage = "Mã PIN phòng phải bao gồm đúng 6 chữ số")]
        public string? PinCode { get; set; }

        [Range(2, 100, ErrorMessage = "Số lượng thành viên từ 2 đến 100")]
        public int MaxMembers { get; set; } = 20;

        public bool OnlyHostControl { get; set; } = true;
    }

    public class WatchPartyChatMessageDto
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public int? UserId { get; set; }
        public string UserName { get; set; } = string.Empty;
        public string? AvatarUrl { get; set; }
        public string Message { get; set; } = string.Empty;
        public bool IsHost { get; set; }
        public bool IsSystem { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }

    public class WatchPartyDanmakuDto
    {
        public string Text { get; set; } = string.Empty;
        public string Color { get; set; } = "#ffffff";
        public string Position { get; set; } = "scroll"; // "scroll", "top", "bottom"
        public string SenderName { get; set; } = string.Empty;
        public double VideoTime { get; set; }
    }

    public class WatchPartyMemberDto
    {
        public int? UserId { get; set; }
        public string UserName { get; set; } = string.Empty;
        public string? AvatarUrl { get; set; }
        public bool IsHost { get; set; }
        public DateTime JoinedAt { get; set; }
    }
}
