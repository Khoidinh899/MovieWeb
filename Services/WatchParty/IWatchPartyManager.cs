using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using MovieWeb.Models.ViewModels.WatchParty;

namespace MovieWeb.Services.WatchParty
{
    public interface IWatchPartyManager
    {
        WatchPartyRoomSession GetOrCreateSession(Guid roomId, string roomCode, string title, string? shareToken, 
            int movieId, string movieSlug, int? episodeId, int? episodeNumber, string? serverName, 
            int hostUserId, string hostName, string? hostAvatar, bool isPrivate, string? pinCode, 
            int maxMembers, bool onlyHostControl);

        WatchPartyRoomSession? GetSession(string roomCode);
        WatchPartyRoomSession? GetSessionByRoomId(Guid roomId);
        IEnumerable<WatchPartyRoomSession> GetAllActiveSessions();

        void AddMember(string roomCode, string connectionId, int? userId, string userName, string? avatarUrl, bool isHost);
        WatchPartyMemberSession? RemoveMember(string connectionId, out string? roomCode, out bool wasHost);
        
        void UpdatePlayback(string roomCode, double currentTime, bool isPlaying);
        void UpdateEpisode(string roomCode, int episodeId, int episodeNumber, string serverName);
        void UpdateSettings(string roomCode, bool onlyHostControl);
        void TransferHost(string roomCode, int newHostUserId, string newHostName);

        void AddChatMessage(string roomCode, WatchPartyChatMessageDto message);
        List<WatchPartyChatMessageDto> GetRecentMessages(string roomCode);

        void StartHostGracePeriod(string roomCode, int seconds, Func<string, Task> onExpiredCallback);
        bool CancelHostGracePeriod(string roomCode);

        void RemoveSession(string roomCode);
    }

    public class WatchPartyRoomSession
    {
        public Guid RoomId { get; set; }
        public string RoomCode { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string? ShareToken { get; set; }
        public int MovieId { get; set; }
        public string MovieSlug { get; set; } = string.Empty;
        public int? EpisodeId { get; set; }
        public int? EpisodeNumber { get; set; }
        public string? ServerName { get; set; }

        public int HostUserId { get; set; }
        public string HostName { get; set; } = string.Empty;
        public string? HostAvatar { get; set; }

        public double CurrentTime { get; set; }
        public bool IsPlaying { get; set; }
        public DateTime LastStateUpdateUtc { get; set; } = DateTime.UtcNow;

        public bool IsPrivate { get; set; }
        public string? PinCode { get; set; }
        public int MaxMembers { get; set; } = 20;
        public bool OnlyHostControl { get; set; } = true;

        public ConcurrentDictionary<string, WatchPartyMemberSession> Members { get; set; } = new();
        public List<WatchPartyChatMessageDto> Messages { get; set; } = new();
        public object MessagesLock { get; } = new();

        public CancellationTokenSource? HostGraceCts { get; set; }
        public DateTime? HostGraceExpiresAtUtc { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    public class WatchPartyMemberSession
    {
        public string ConnectionId { get; set; } = string.Empty;
        public int? UserId { get; set; }
        public string UserName { get; set; } = string.Empty;
        public string? AvatarUrl { get; set; }
        public bool IsHost { get; set; }
        public DateTime JoinedAt { get; set; } = DateTime.UtcNow;
    }
}
