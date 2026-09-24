using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MovieWeb.Data;
using MovieWeb.Models.ViewModels.WatchParty;
using MovieWeb.Services.WatchParty;

namespace MovieWeb.Hubs
{
    [Authorize]
    public class WatchPartyHub : Hub
    {
        private readonly IWatchPartyManager _watchPartyManager;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<WatchPartyHub> _logger;

        public WatchPartyHub(
            IWatchPartyManager watchPartyManager,
            IServiceScopeFactory scopeFactory,
            ILogger<WatchPartyHub> logger)
        {
            _watchPartyManager = watchPartyManager;
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        // ==========================================
        // 1️⃣ THAM GIA PHÒNG (JOIN ROOM)
        // ==========================================
        public async Task JoinRoom(string roomCode, string? token, string? pinCode)
        {
            if (string.IsNullOrWhiteSpace(roomCode))
            {
                await Clients.Caller.SendAsync("OnError", "Mã phòng không hợp lệ.");
                return;
            }

            var userIdStr = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(userIdStr, out int userId))
            {
                await Clients.Caller.SendAsync("OnError", "Bạn cần đăng nhập để tham gia phòng xem chung.");
                return;
            }

            var userName = Context.User?.Identity?.Name ?? $"Thành viên #{userId}";
            var avatarUrl = Context.User?.FindFirst("Avatar")?.Value;

            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<MovieWebDbContext>();

            if (string.IsNullOrWhiteSpace(avatarUrl))
            {
                var dbUser = await db.Users.FindAsync(userId);
                avatarUrl = dbUser?.Avatar;
            }
            if (string.IsNullOrWhiteSpace(avatarUrl))
            {
                avatarUrl = "/images/nouser.png";
            }

            var roomEntity = await db.WatchPartyRooms
                .Include(r => r.Movie)
                .Include(r => r.HostUser)
                .FirstOrDefaultAsync(r => r.RoomCode == roomCode && r.IsActive);

            if (roomEntity == null)
            {
                await Clients.Caller.SendAsync("OnError", "Phòng xem chung không tồn tại hoặc đã kết thúc.");
                return;
            }

            // Kiểm tra quyền vào phòng riêng tư (bỏ qua nếu có direct share token hợp lệ)
            bool isDirectTokenValid = !string.IsNullOrEmpty(token) && 
                string.Equals(roomEntity.ShareToken, token, StringComparison.OrdinalIgnoreCase);

            if (roomEntity.IsPrivate && !isDirectTokenValid && roomEntity.HostUserId != userId)
            {
                if (string.IsNullOrWhiteSpace(pinCode) || roomEntity.PinCode != pinCode.Trim())
                {
                    await Clients.Caller.SendAsync("OnError", "Mã PIN phòng không chính xác.");
                    return;
                }
            }

            // Lấy hoặc tạo Session trong Memory
            var session = _watchPartyManager.GetOrCreateSession(
                roomEntity.Id, roomEntity.RoomCode, roomEntity.Title, roomEntity.ShareToken,
                roomEntity.MovieId, roomEntity.MovieSlug, roomEntity.EpisodeId, roomEntity.EpisodeNumber, roomEntity.ServerName,
                roomEntity.HostUserId, roomEntity.HostUser?.FullName ?? roomEntity.HostUser?.UserName ?? "Chủ phòng",
                roomEntity.HostUser?.Avatar, roomEntity.IsPrivate, roomEntity.PinCode,
                roomEntity.MaxMembers, roomEntity.OnlyHostControl
            );

            // Kiểm tra giới hạn thành viên (trừ Host)
            if (session.Members.Count >= session.MaxMembers && !session.Members.ContainsKey(Context.ConnectionId) && roomEntity.HostUserId != userId)
            {
                await Clients.Caller.SendAsync("OnError", "Phòng xem chung đã đạt số lượng thành viên tối đa.");
                return;
            }

            bool isHost = (roomEntity.HostUserId == userId);

            // Nếu Host kết nối lại -> Hủy Grace Period timer (120s)
            if (isHost)
            {
                bool wasInGrace = _watchPartyManager.CancelHostGracePeriod(roomCode);
                if (wasInGrace)
                {
                    await Clients.Group($"Room_{roomCode}").SendAsync("OnHostReconnected", new
                    {
                        hostUserId = userId,
                        hostName = userName,
                        message = "Chủ phòng đã kết nối lại!"
                    });
                }
            }

            // Thêm kết nối vào SignalR Group
            await Groups.AddToGroupAsync(Context.ConnectionId, $"Room_{roomCode}");

            // Lưu member vào session
            _watchPartyManager.AddMember(roomCode, Context.ConnectionId, userId, userName, avatarUrl, isHost);

            // Cập nhật member vào DB
            try
            {
                var existingMember = await db.WatchPartyMembers
                    .FirstOrDefaultAsync(m => m.RoomId == roomEntity.Id && m.UserId == userId);

                if (existingMember == null)
                {
                    db.WatchPartyMembers.Add(new Models.Entities.WatchPartyMember
                    {
                        RoomId = roomEntity.Id,
                        UserId = userId,
                        UserName = userName,
                        AvatarUrl = avatarUrl,
                        ConnectionId = Context.ConnectionId,
                        IsHost = isHost,
                        IsActive = true,
                        JoinedAt = DateTime.UtcNow
                    });
                }
                else
                {
                    existingMember.ConnectionId = Context.ConnectionId;
                    existingMember.IsActive = true;
                    existingMember.LeftAt = null;
                }
                await db.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Lỗi cập nhật WatchPartyMember vào DB cho room {RoomCode}", roomCode);
            }

            // Gửi state ban đầu cho người mới vào
            var membersList = session.Members.Values.Select(m => new WatchPartyMemberDto
            {
                UserId = m.UserId,
                UserName = m.UserName,
                AvatarUrl = m.AvatarUrl,
                IsHost = m.IsHost,
                JoinedAt = m.JoinedAt
            }).ToList();

            var recentMessages = _watchPartyManager.GetRecentMessages(roomCode);

            await Clients.Caller.SendAsync("OnInitialState", new
            {
                roomCode = session.RoomCode,
                title = session.Title,
                isHost = isHost,
                hostUserId = session.HostUserId,
                hostName = session.HostName,
                currentTime = session.CurrentTime,
                isPlaying = session.IsPlaying,
                onlyHostControl = session.OnlyHostControl,
                allowDanmaku = session.AllowDanmaku,
                episodeId = session.EpisodeId,
                episodeNumber = session.EpisodeNumber,
                serverName = session.ServerName,
                members = membersList,
                messages = recentMessages
            });

            // Thông báo toàn phòng có người mới tham gia
            await Clients.Group($"Room_{roomCode}").SendAsync("OnUserJoined", new
            {
                userId = userId,
                userName = userName,
                avatarUrl = avatarUrl,
                isHost = isHost,
                memberCount = session.Members.Count,
                members = membersList
            });

            _logger.LogInformation("SignalR: User {UserName} joined WatchParty room {RoomCode}", userName, roomCode);
        }

        // ==========================================
        // 2️⃣ RỜI PHÒNG (LEAVE ROOM)
        // ==========================================
        public async Task LeaveRoom(string roomCode)
        {
            if (string.IsNullOrWhiteSpace(roomCode)) return;

            var member = _watchPartyManager.RemoveMember(Context.ConnectionId, out _, out bool wasHost);
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"Room_{roomCode}");

            if (member != null)
            {
                var session = _watchPartyManager.GetSession(roomCode);
                var memberCount = session?.Members.Count ?? 0;

                await Clients.Group($"Room_{roomCode}").SendAsync("OnUserLeft", new
                {
                    userId = member.UserId,
                    userName = member.UserName,
                    memberCount = memberCount
                });
            }
        }

        // ==========================================
        // 3️⃣ ĐỒNG BỘ PLAYBACK (PLAY / PAUSE / SEEK)
        // ==========================================
        public async Task SyncPlay(string roomCode, double currentTime)
        {
            var session = _watchPartyManager.GetSession(roomCode);
            if (session == null) return;

            var userIdStr = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            int.TryParse(userIdStr, out int userId);
            bool isHost = (session.HostUserId == userId);

            if (session.OnlyHostControl && !isHost)
            {
                await Clients.Caller.SendAsync("OnError", "Chỉ chủ phòng mới có quyền điều khiển phát video.");
                return;
            }

            _watchPartyManager.UpdatePlayback(roomCode, currentTime, true);

            var userName = Context.User?.Identity?.Name ?? "Thành viên";
            await Clients.OthersInGroup($"Room_{roomCode}").SendAsync("OnSyncPlay", new
            {
                currentTime = currentTime,
                senderName = userName
            });
        }

        public async Task SyncPause(string roomCode, double currentTime)
        {
            var session = _watchPartyManager.GetSession(roomCode);
            if (session == null) return;

            var userIdStr = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            int.TryParse(userIdStr, out int userId);
            bool isHost = (session.HostUserId == userId);

            if (session.OnlyHostControl && !isHost)
            {
                await Clients.Caller.SendAsync("OnError", "Chỉ chủ phòng mới có quyền tạm dừng video.");
                return;
            }

            _watchPartyManager.UpdatePlayback(roomCode, currentTime, false);

            var userName = Context.User?.Identity?.Name ?? "Thành viên";
            await Clients.OthersInGroup($"Room_{roomCode}").SendAsync("OnSyncPause", new
            {
                currentTime = currentTime,
                senderName = userName
            });
        }

        public async Task SyncSeek(string roomCode, double currentTime)
        {
            var session = _watchPartyManager.GetSession(roomCode);
            if (session == null) return;

            var userIdStr = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            int.TryParse(userIdStr, out int userId);
            bool isHost = (session.HostUserId == userId);

            if (session.OnlyHostControl && !isHost)
            {
                await Clients.Caller.SendAsync("OnError", "Chỉ chủ phòng mới có quyền tua video.");
                return;
            }

            _watchPartyManager.UpdatePlayback(roomCode, currentTime, session.IsPlaying);

            var userName = Context.User?.Identity?.Name ?? "Thành viên";
            await Clients.OthersInGroup($"Room_{roomCode}").SendAsync("OnSyncSeek", new
            {
                currentTime = currentTime,
                senderName = userName
            });
        }

        // ==========================================
        // 4️⃣ ĐỔI TẬP PHIM (CHANGE EPISODE)
        // ==========================================
        public async Task ChangeEpisode(string roomCode, int episodeId, int episodeNumber, string serverName, string videoUrl)
        {
            var session = _watchPartyManager.GetSession(roomCode);
            if (session == null) return;

            var userIdStr = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            int.TryParse(userIdStr, out int userId);
            bool isHost = (session.HostUserId == userId);

            if (!isHost)
            {
                await Clients.Caller.SendAsync("OnError", "Chỉ chủ phòng mới có quyền đổi tập phim.");
                return;
            }

            _watchPartyManager.UpdateEpisode(roomCode, episodeId, episodeNumber, serverName);

            using (var scope = _scopeFactory.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<MovieWebDbContext>();
                var room = await db.WatchPartyRooms.FirstOrDefaultAsync(r => r.RoomCode == roomCode);
                if (room != null)
                {
                    room.EpisodeId = episodeId;
                    room.EpisodeNumber = episodeNumber;
                    room.ServerName = serverName;
                    room.CurrentTime = 0;
                    room.IsPlaying = true;
                    room.UpdatedAt = DateTime.UtcNow;
                    await db.SaveChangesAsync();
                }
            }

            await Clients.Group($"Room_{roomCode}").SendAsync("OnEpisodeChanged", new
            {
                episodeId = episodeId,
                episodeNumber = episodeNumber,
                serverName = serverName,
                videoUrl = videoUrl
            });
        }

        // ==========================================
        // 5️⃣ GỬI TIN NHẮN CHAT (CHAT MESSAGE)
        // ==========================================
        public async Task SendChatMessage(string roomCode, string message)
        {
            if (string.IsNullOrWhiteSpace(message)) return;

            var session = _watchPartyManager.GetSession(roomCode);
            if (session == null) return;

            var userIdStr = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            int.TryParse(userIdStr, out int userId);
            var userName = Context.User?.Identity?.Name ?? $"Thành viên #{userId}";
            var avatarUrl = Context.User?.FindFirst("Avatar")?.Value;
            if (string.IsNullOrWhiteSpace(avatarUrl))
            {
                var member = session.Members.Values.FirstOrDefault(m => m.UserId == userId);
                avatarUrl = member?.AvatarUrl ?? "/images/nouser.png";
            }
            bool isHost = (session.HostUserId == userId);

            var chatDto = new WatchPartyChatMessageDto
            {
                UserId = userId,
                UserName = userName,
                AvatarUrl = avatarUrl,
                Message = message.Trim(),
                IsHost = isHost,
                IsSystem = false,
                Timestamp = DateTime.UtcNow
            };

            _watchPartyManager.AddChatMessage(roomCode, chatDto);

            await Clients.Group($"Room_{roomCode}").SendAsync("OnReceiveChatMessage", chatDto);
        }

        // ==========================================
        // 6️⃣ BÌNH LUẬN BAY (DANMAKU BULLET COMMENTS)
        // ==========================================
        public async Task SendDanmaku(string roomCode, string text, string color, string position, double videoTime)
        {
            if (string.IsNullOrWhiteSpace(text)) return;

            var session = _watchPartyManager.GetSession(roomCode);
            if (session == null) return;

            if (!session.AllowDanmaku)
            {
                await Clients.Caller.SendAsync("OnError", "Chủ phòng đã tắt tính năng bình luận bay trong phòng này.");
                return;
            }

            var userName = Context.User?.Identity?.Name ?? "Ẩn danh";

            var danmakuDto = new WatchPartyDanmakuDto
            {
                Text = text.Trim(),
                Color = string.IsNullOrWhiteSpace(color) ? "#ffffff" : color,
                Position = string.IsNullOrWhiteSpace(position) ? "scroll" : position,
                SenderName = userName,
                VideoTime = videoTime
            };

            // Đồng thời cũng lưu vào danh sách tin nhắn chat
            var userIdStr = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            int.TryParse(userIdStr, out int userId);
            var avatarUrl = Context.User?.FindFirst("Avatar")?.Value;
            if (string.IsNullOrWhiteSpace(avatarUrl))
            {
                var member = session.Members.Values.FirstOrDefault(m => m.UserId == userId);
                avatarUrl = member?.AvatarUrl ?? "/images/nouser.png";
            }
            bool isHost = (session.HostUserId == userId);

            var chatDto = new WatchPartyChatMessageDto
            {
                UserId = userId,
                UserName = userName,
                AvatarUrl = avatarUrl,
                Message = text.Trim(),
                IsHost = isHost,
                IsSystem = false,
                Timestamp = DateTime.UtcNow
            };

            _watchPartyManager.AddChatMessage(roomCode, chatDto);

            // Bắn đồng thời cả Danmaku (bay trên video) và Chat (trong cột tin nhắn)
            await Clients.Group($"Room_{roomCode}").SendAsync("OnReceiveDanmaku", danmakuDto);
            await Clients.Group($"Room_{roomCode}").SendAsync("OnReceiveChatMessage", chatDto);
        }

        // ==========================================
        // 7️⃣ CẬP NHẬT CẤU HÌNH PHÒNG (SETTINGS)
        // ==========================================
        public async Task UpdateSettings(string roomCode, bool onlyHostControl, bool allowDanmaku)
        {
            var session = _watchPartyManager.GetSession(roomCode);
            if (session == null) return;

            var userIdStr = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            int.TryParse(userIdStr, out int userId);
            if (session.HostUserId != userId)
            {
                await Clients.Caller.SendAsync("OnError", "Chỉ chủ phòng mới có quyền thay đổi cài đặt.");
                return;
            }

            _watchPartyManager.UpdateSettings(roomCode, onlyHostControl, allowDanmaku);

            using (var scope = _scopeFactory.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<MovieWebDbContext>();
                var room = await db.WatchPartyRooms.FirstOrDefaultAsync(r => r.RoomCode == roomCode);
                if (room != null)
                {
                    room.OnlyHostControl = onlyHostControl;
                    room.UpdatedAt = DateTime.UtcNow;
                    await db.SaveChangesAsync();
                }
            }

            await Clients.Group($"Room_{roomCode}").SendAsync("OnSettingsUpdated", new
            {
                onlyHostControl = onlyHostControl,
                allowDanmaku = allowDanmaku
            });
        }

        // ==========================================
        // 8️⃣ CHUYỂN QUYỀN CHỦ PHÒNG (TRANSFER HOST)
        // ==========================================
        public async Task TransferHost(string roomCode, int targetUserId)
        {
            var session = _watchPartyManager.GetSession(roomCode);
            if (session == null) return;

            var userIdStr = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            int.TryParse(userIdStr, out int userId);
            if (session.HostUserId != userId)
            {
                await Clients.Caller.SendAsync("OnError", "Chỉ chủ phòng mới có quyền chuyển quyền chủ phòng.");
                return;
            }

            var targetMember = session.Members.Values.FirstOrDefault(m => m.UserId == targetUserId);
            if (targetMember == null)
            {
                await Clients.Caller.SendAsync("OnError", "Không tìm thấy thành viên được chọn.");
                return;
            }

            _watchPartyManager.TransferHost(roomCode, targetUserId, targetMember.UserName);

            using (var scope = _scopeFactory.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<MovieWebDbContext>();
                var room = await db.WatchPartyRooms.FirstOrDefaultAsync(r => r.RoomCode == roomCode);
                if (room != null)
                {
                    room.HostUserId = targetUserId;
                    room.UpdatedAt = DateTime.UtcNow;
                    await db.SaveChangesAsync();
                }
            }

            await Clients.Group($"Room_{roomCode}").SendAsync("OnHostTransferred", new
            {
                newHostUserId = targetUserId,
                newHostName = targetMember.UserName
            });
        }

        // ==========================================
        // 9️⃣ ĐÓNG PHÒNG (CLOSE ROOM)
        // ==========================================
        public async Task CloseRoom(string roomCode)
        {
            var session = _watchPartyManager.GetSession(roomCode);
            if (session == null) return;

            var userIdStr = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            int.TryParse(userIdStr, out int userId);
            if (session.HostUserId != userId)
            {
                await Clients.Caller.SendAsync("OnError", "Chỉ chủ phòng mới có quyền đóng phòng.");
                return;
            }

            using (var scope = _scopeFactory.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<MovieWebDbContext>();
                var room = await db.WatchPartyRooms.FirstOrDefaultAsync(r => r.RoomCode == roomCode);
                if (room != null)
                {
                    room.IsActive = false;
                    room.EndedAt = DateTime.UtcNow;
                    room.UpdatedAt = DateTime.UtcNow;
                    await db.SaveChangesAsync();
                }
            }

            _watchPartyManager.RemoveSession(roomCode);

            await Clients.Group($"Room_{roomCode}").SendAsync("OnRoomClosed", "Chủ phòng đã kết thúc phiên xem chung.");
        }

        // ==========================================
        // 🔟 XỬ LÝ NGẮT KẾT NỐI (DISCONNECT & 120s GRACE)
        // ==========================================
        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            var member = _watchPartyManager.RemoveMember(Context.ConnectionId, out string? roomCode, out bool wasHost);

            if (!string.IsNullOrEmpty(roomCode) && member != null)
            {
                var session = _watchPartyManager.GetSession(roomCode);

                if (wasHost)
                {
                    // Kích hoạt bộ đếm ân hạn 2 phút (120 giây)
                    const int graceSeconds = 120;
                    
                    await Clients.Group($"Room_{roomCode}").SendAsync("OnHostDisconnected", new
                    {
                        graceSeconds = graceSeconds,
                        message = $"Chủ phòng tạm thời mất kết nối. Đang chờ kết nối lại trong {graceSeconds} giây..."
                    });

                    _watchPartyManager.StartHostGracePeriod(roomCode, graceSeconds, async (expiredRoomCode) =>
                    {
                        var hubContext = _scopeFactory.CreateScope().ServiceProvider.GetRequiredService<IHubContext<WatchPartyHub>>();
                        var expiredSession = _watchPartyManager.GetSession(expiredRoomCode);

                        if (expiredSession != null && expiredSession.Members.Any())
                        {
                            // Tự động chuyển quyền Host cho thành viên đầu tiên còn trong phòng
                            var newHost = expiredSession.Members.Values.OrderBy(m => m.JoinedAt).First();
                            if (newHost.UserId.HasValue)
                            {
                                _watchPartyManager.TransferHost(expiredRoomCode, newHost.UserId.Value, newHost.UserName);

                                using var scope = _scopeFactory.CreateScope();
                                var db = scope.ServiceProvider.GetRequiredService<MovieWebDbContext>();
                                var room = await db.WatchPartyRooms.FirstOrDefaultAsync(r => r.RoomCode == expiredRoomCode);
                                if (room != null)
                                {
                                    room.HostUserId = newHost.UserId.Value;
                                    room.UpdatedAt = DateTime.UtcNow;
                                    await db.SaveChangesAsync();
                                }

                                await hubContext.Clients.Group($"Room_{expiredRoomCode}").SendAsync("OnHostTransferred", new
                                {
                                    newHostUserId = newHost.UserId.Value,
                                    newHostName = newHost.UserName,
                                    message = $"Hết thời gian chờ. Quyền chủ phòng đã được tự động chuyển cho {newHost.UserName}!"
                                });
                            }
                        }
                        else
                        {
                            // Không còn ai trong phòng -> Đóng phòng
                            using var scope = _scopeFactory.CreateScope();
                            var db = scope.ServiceProvider.GetRequiredService<MovieWebDbContext>();
                            var room = await db.WatchPartyRooms.FirstOrDefaultAsync(r => r.RoomCode == expiredRoomCode);
                            if (room != null)
                            {
                                room.IsActive = false;
                                room.EndedAt = DateTime.UtcNow;
                                await db.SaveChangesAsync();
                            }

                            _watchPartyManager.RemoveSession(expiredRoomCode);
                            await hubContext.Clients.Group($"Room_{expiredRoomCode}").SendAsync("OnRoomClosed", "Phòng đã tự động đóng do không còn thành viên.");
                        }
                    });
                }
                else
                {
                    // Thành viên thường rời phòng
                    await Clients.Group($"Room_{roomCode}").SendAsync("OnUserLeft", new
                    {
                        userId = member.UserId,
                        userName = member.UserName,
                        memberCount = session?.Members.Count ?? 0
                    });
                }
            }

            await base.OnDisconnectedAsync(exception);
        }
    }
}
