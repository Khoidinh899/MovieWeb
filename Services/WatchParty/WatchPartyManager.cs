using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MovieWeb.Models.ViewModels.WatchParty;

namespace MovieWeb.Services.WatchParty
{
    public class WatchPartyManager : IWatchPartyManager
    {
        private readonly ConcurrentDictionary<string, WatchPartyRoomSession> _sessions = new(StringComparer.OrdinalIgnoreCase);
        private readonly ConcurrentDictionary<string, string> _connectionToRoom = new();
        private readonly ILogger<WatchPartyManager> _logger;

        public WatchPartyManager(ILogger<WatchPartyManager> logger)
        {
            _logger = logger;
        }

        public WatchPartyRoomSession GetOrCreateSession(Guid roomId, string roomCode, string title, string? shareToken, 
            int movieId, string movieSlug, int? episodeId, int? episodeNumber, string? serverName, 
            int hostUserId, string hostName, string? hostAvatar, bool isPrivate, string? pinCode, 
            int maxMembers, bool onlyHostControl)
        {
            return _sessions.GetOrAdd(roomCode, key => new WatchPartyRoomSession
            {
                RoomId = roomId,
                RoomCode = key,
                Title = title,
                ShareToken = shareToken,
                MovieId = movieId,
                MovieSlug = movieSlug,
                EpisodeId = episodeId,
                EpisodeNumber = episodeNumber,
                ServerName = serverName,
                HostUserId = hostUserId,
                HostName = hostName,
                HostAvatar = hostAvatar,
                IsPrivate = isPrivate,
                PinCode = pinCode,
                MaxMembers = maxMembers,
                OnlyHostControl = onlyHostControl,
                CreatedAt = DateTime.UtcNow,
                LastStateUpdateUtc = DateTime.UtcNow
            });
        }

        public WatchPartyRoomSession? GetSession(string roomCode)
        {
            if (string.IsNullOrWhiteSpace(roomCode)) return null;
            _sessions.TryGetValue(roomCode, out var session);
            return session;
        }

        public WatchPartyRoomSession? GetSessionByRoomId(Guid roomId)
        {
            return _sessions.Values.FirstOrDefault(s => s.RoomId == roomId);
        }

        public IEnumerable<WatchPartyRoomSession> GetAllActiveSessions()
        {
            return _sessions.Values.ToList();
        }

        public void AddMember(string roomCode, string connectionId, int? userId, string userName, string? avatarUrl, bool isHost)
        {
            var session = GetSession(roomCode);
            if (session == null) return;

            var member = new WatchPartyMemberSession
            {
                ConnectionId = connectionId,
                UserId = userId,
                UserName = userName,
                AvatarUrl = avatarUrl,
                IsHost = isHost,
                JoinedAt = DateTime.UtcNow
            };

            session.Members.AddOrUpdate(connectionId, member, (k, old) => member);
            _connectionToRoom[connectionId] = roomCode;

            _logger.LogInformation("WatchParty: User {UserName} (Connection: {ConnectionId}, Host: {IsHost}) joined room {RoomCode}",
                userName, connectionId, isHost, roomCode);
        }

        public WatchPartyMemberSession? RemoveMember(string connectionId, out string? roomCode, out bool wasHost)
        {
            roomCode = null;
            wasHost = false;

            if (!_connectionToRoom.TryRemove(connectionId, out roomCode) || string.IsNullOrEmpty(roomCode))
            {
                return null;
            }

            var session = GetSession(roomCode);
            if (session == null) return null;

            if (session.Members.TryRemove(connectionId, out var member))
            {
                wasHost = member.IsHost || (member.UserId.HasValue && member.UserId.Value == session.HostUserId);
                _logger.LogInformation("WatchParty: User {UserName} left room {RoomCode} (WasHost: {WasHost})",
                    member.UserName, roomCode, wasHost);
                return member;
            }

            return null;
        }

        public void UpdatePlayback(string roomCode, double currentTime, bool isPlaying)
        {
            var session = GetSession(roomCode);
            if (session == null) return;

            session.CurrentTime = currentTime;
            session.IsPlaying = isPlaying;
            session.LastStateUpdateUtc = DateTime.UtcNow;
        }

        public double GetCalculatedCurrentTime(string roomCode)
        {
            var session = GetSession(roomCode);
            if (session == null) return 0;

            if (session.IsPlaying)
            {
                double elapsed = (DateTime.UtcNow - session.LastStateUpdateUtc).TotalSeconds;
                if (elapsed > 0 && elapsed < 86400)
                {
                    return session.CurrentTime + elapsed;
                }
            }

            return session.CurrentTime;
        }

        public void UpdateEpisode(string roomCode, int episodeId, int episodeNumber, string serverName)
        {
            var session = GetSession(roomCode);
            if (session == null) return;

            session.EpisodeId = episodeId;
            session.EpisodeNumber = episodeNumber;
            session.ServerName = serverName;
            session.CurrentTime = 0;
            session.IsPlaying = false;
            session.LastStateUpdateUtc = DateTime.UtcNow;
        }

        public void UpdateSettings(string roomCode, bool onlyHostControl, bool allowDanmaku)
        {
            var session = GetSession(roomCode);
            if (session == null) return;

            session.OnlyHostControl = onlyHostControl;
            session.AllowDanmaku = allowDanmaku;
        }

        public void TransferHost(string roomCode, int newHostUserId, string newHostName)
        {
            var session = GetSession(roomCode);
            if (session == null) return;

            session.HostUserId = newHostUserId;
            session.HostName = newHostName;

            foreach (var member in session.Members.Values)
            {
                member.IsHost = (member.UserId == newHostUserId);
            }
        }

        public void AddChatMessage(string roomCode, WatchPartyChatMessageDto message)
        {
            var session = GetSession(roomCode);
            if (session == null) return;

            lock (session.MessagesLock)
            {
                session.Messages.Add(message);
                if (session.Messages.Count > 100)
                {
                    session.Messages.RemoveAt(0);
                }
            }
        }

        public List<WatchPartyChatMessageDto> GetRecentMessages(string roomCode)
        {
            var session = GetSession(roomCode);
            if (session == null) return new List<WatchPartyChatMessageDto>();

            lock (session.MessagesLock)
            {
                return session.Messages.ToList();
            }
        }

        public void StartHostGracePeriod(string roomCode, int seconds, Func<string, Task> onExpiredCallback)
        {
            var session = GetSession(roomCode);
            if (session == null) return;

            CancelHostGracePeriod(roomCode);

            var cts = new CancellationTokenSource();
            session.HostGraceCts = cts;
            session.HostGraceExpiresAtUtc = DateTime.UtcNow.AddSeconds(seconds);

            _logger.LogInformation("WatchParty: Host disconnected from room {RoomCode}. Starting {Seconds}s grace period.", roomCode, seconds);

            _ = Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(seconds), cts.Token);
                    if (!cts.Token.IsCancellationRequested)
                    {
                        _logger.LogInformation("WatchParty: Grace period expired for room {RoomCode}", roomCode);
                        await onExpiredCallback(roomCode);
                    }
                }
                catch (TaskCanceledException)
                {
                    _logger.LogInformation("WatchParty: Host grace period cancelled for room {RoomCode} (Host reconnected).", roomCode);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "WatchParty: Error during grace period callback for room {RoomCode}", roomCode);
                }
            }, cts.Token);
        }

        public bool CancelHostGracePeriod(string roomCode)
        {
            var session = GetSession(roomCode);
            if (session == null || session.HostGraceCts == null) return false;

            try
            {
                session.HostGraceCts.Cancel();
                session.HostGraceCts.Dispose();
                session.HostGraceCts = null;
                session.HostGraceExpiresAtUtc = null;
                _logger.LogInformation("WatchParty: Cancelled host grace period for room {RoomCode}", roomCode);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "WatchParty: Error cancelling host grace period for room {RoomCode}", roomCode);
                return false;
            }
        }

        public void RemoveSession(string roomCode)
        {
            if (_sessions.TryRemove(roomCode, out var session))
            {
                CancelHostGracePeriod(roomCode);
                foreach (var conn in session.Members.Keys)
                {
                    _connectionToRoom.TryRemove(conn, out _);
                }
                _logger.LogInformation("WatchParty: Closed and removed session for room {RoomCode}", roomCode);
            }
        }
    }
}
