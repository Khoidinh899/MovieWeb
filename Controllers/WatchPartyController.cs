using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MovieWeb.Data;
using MovieWeb.Models.Entities;
using MovieWeb.Models.ViewModels.WatchParty;
using MovieWeb.Services.Interfaces;
using MovieWeb.Services.WatchParty;

namespace MovieWeb.Controllers
{
    public class WatchPartyController : Controller
    {
        private readonly MovieWebDbContext _context;
        private readonly IWatchPartyManager _watchPartyManager;
        private readonly UserManager<User> _userManager;
        private readonly IAuthService _authService;

        public WatchPartyController(
            MovieWebDbContext context,
            IWatchPartyManager watchPartyManager,
            UserManager<User> userManager,
            IAuthService authService)
        {
            _context = context;
            _watchPartyManager = watchPartyManager;
            _userManager = userManager;
            _authService = authService;
        }

        // ==========================================
        // 🏠 TRANG CHỦ XEM CHUNG (/watch-party)
        // ==========================================
        [HttpGet("watch-party")]
        public async Task<IActionResult> Index()
        {
            var activeRooms = await _context.WatchPartyRooms
                .Include(r => r.Movie)
                .Include(r => r.HostUser)
                .Where(r => r.IsActive)
                .OrderByDescending(r => r.CreatedAt)
                .ToListAsync();

            var roomCards = new List<WatchPartyRoomCardDto>();
            int totalWatching = 0;

            foreach (var room in activeRooms)
            {
                var session = _watchPartyManager.GetSession(room.RoomCode);
                int membersCount = session?.Members.Count ?? 0;
                totalWatching += membersCount;

                roomCards.Add(new WatchPartyRoomCardDto
                {
                    Id = room.Id,
                    RoomCode = room.RoomCode,
                    Title = room.Title,
                    ShareToken = room.ShareToken,
                    MovieId = room.MovieId,
                    MovieTitle = room.Movie?.Name ?? "Phim không xác định",
                    MovieSlug = room.MovieSlug,
                    PosterUrl = room.Movie?.PosterUrl,
                    ThumbUrl = room.Movie?.ThumbUrl,
                    EpisodeNumber = room.EpisodeNumber ?? 1,
                    ServerName = room.ServerName,
                    HostUserId = room.HostUserId,
                    HostName = room.HostUser?.FullName ?? room.HostUser?.UserName ?? "Chủ phòng",
                    HostAvatar = room.HostUser?.Avatar,
                    IsPlaying = session?.IsPlaying ?? room.IsPlaying,
                    IsPrivate = room.IsPrivate,
                    MaxMembers = room.MaxMembers,
                    CurrentMembersCount = membersCount,
                    CreatedAt = room.CreatedAt
                });
            }

            var currentUser = await _authService.GetCurrentUserAsync();

            var viewModel = new WatchPartyHubViewModel
            {
                PublicRooms = roomCards.Where(r => !r.IsPrivate).ToList(),
                PrivateRooms = roomCards.Where(r => r.IsPrivate).ToList(),
                TotalActiveRooms = roomCards.Count,
                TotalWatchingUsers = totalWatching,
                CurrentUserFullName = currentUser?.FullName ?? currentUser?.UserName
            };

            return View(viewModel);
        }

        // ==========================================
        // 🍿 PHÒNG XEM CHUNG CHI TIẾT (/watch-party/{roomCode})
        // ==========================================
        [HttpGet("watch-party/{roomCode}")]
        public async Task<IActionResult> Room(string roomCode, [FromQuery] string? token)
        {
            if (string.IsNullOrWhiteSpace(roomCode))
                return RedirectToAction(nameof(Index));

            var currentUser = await _authService.GetCurrentUserAsync();
            if (currentUser == null)
            {
                var currentUrl = Request.Path + Request.QueryString;
                return Redirect($"/Auth/Login?returnUrl={Uri.EscapeDataString(currentUrl)}");
            }

            var room = await _context.WatchPartyRooms
                .Include(r => r.Movie)
                .Include(r => r.HostUser)
                .FirstOrDefaultAsync(r => r.RoomCode == roomCode && r.IsActive);

            if (room == null || room.Movie == null)
            {
                TempData["ErrorMessage"] = "Phòng xem chung không tồn tại hoặc đã kết thúc.";
                return RedirectToAction(nameof(Index));
            }

            // Lấy tất cả tập phim của bộ phim
            var allEpisodes = await _context.Episodes
                .Where(e => e.MovieId == room.MovieId)
                .OrderBy(e => e.EpisodeName)
                .ToListAsync();

            // Phân nhóm Server
            string NormalizeKey(string? s) => string.IsNullOrWhiteSpace(s) ? "khac" : Regex.Replace(s.Trim().ToLowerInvariant(), @"\s+", " ");
            string DisplayName(string? s) => string.IsNullOrWhiteSpace(s) ? "Khác" : Regex.Replace(s.Trim(), @"\s+", " ");

            var serverDisplayNames = new Dictionary<string, string>();
            var groupedByNormalized = new Dictionary<string, List<Episode>>();

            foreach (var ep in allEpisodes)
            {
                var key = NormalizeKey(ep.ServerName);
                var display = DisplayName(ep.ServerName);

                if (!serverDisplayNames.ContainsKey(key))
                    serverDisplayNames[key] = display;

                if (!groupedByNormalized.ContainsKey(key))
                    groupedByNormalized[key] = new List<Episode>();

                groupedByNormalized[key].Add(ep);
            }

            string defaultServerKey = "";
            if (serverDisplayNames.Keys.Any(k => serverDisplayNames[k].Contains("vietsub", StringComparison.OrdinalIgnoreCase)))
            {
                defaultServerKey = serverDisplayNames.First(x => x.Value.Contains("vietsub", StringComparison.OrdinalIgnoreCase)).Key;
            }
            else
            {
                defaultServerKey = serverDisplayNames.Keys.FirstOrDefault() ?? "";
            }

            // Tìm tập phim đang phát hiện tại
            Episode? currentEp = null;
            if (room.EpisodeId.HasValue)
            {
                currentEp = allEpisodes.FirstOrDefault(e => e.EpisodeId == room.EpisodeId.Value);
            }
            if (currentEp == null)
            {
                currentEp = allEpisodes.FirstOrDefault();
            }

            string? videoUrl = currentEp?.LinkM3u8 ?? room.Movie.TrailerUrl;

            // Direct share link
            var request = HttpContext.Request;
            var baseUrl = $"{request.Scheme}://{request.Host}";
            var shareUrl = string.IsNullOrEmpty(room.ShareToken) 
                ? $"{baseUrl}/watch-party/{room.RoomCode}" 
                : $"{baseUrl}/watch-party/{room.RoomCode}?token={room.ShareToken}";

            var session = _watchPartyManager.GetSession(room.RoomCode);

            var viewModel = new WatchPartyRoomViewModel
            {
                RoomId = room.Id,
                RoomCode = room.RoomCode,
                Title = room.Title,
                ShareToken = room.ShareToken,
                IsPrivate = room.IsPrivate,
                OnlyHostControl = room.OnlyHostControl,
                MaxMembers = room.MaxMembers,
                CurrentTime = session?.CurrentTime ?? room.CurrentTime,
                IsPlaying = session?.IsPlaying ?? room.IsPlaying,

                HostUserId = room.HostUserId,
                HostName = room.HostUser?.FullName ?? room.HostUser?.UserName ?? "Chủ phòng",
                HostAvatar = room.HostUser?.Avatar,
                IsCurrentUserHost = (room.HostUserId == currentUser.Id),

                CurrentUserId = currentUser.Id,
                CurrentUserName = currentUser.FullName ?? currentUser.UserName ?? $"User #{currentUser.Id}",
                CurrentUserAvatar = currentUser.Avatar,

                Movie = room.Movie,
                CurrentEpisodeId = currentEp?.EpisodeId,
                CurrentEpisodeNumber = room.EpisodeNumber ?? 1,
                CurrentServerName = currentEp?.ServerName ?? defaultServerKey,
                VideoStreamUrl = videoUrl,
                GroupedEpisodes = groupedByNormalized,
                ServerDisplayNames = serverDisplayNames,
                DefaultServer = defaultServerKey,

                ShareUrl = shareUrl
            };

            return View(viewModel);
        }

        // ==========================================
        // ➕ TẠO PHÒNG XEM CHUNG
        // ==========================================
        [HttpPost("watch-party/create")]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([FromForm] CreateWatchPartyInput input)
        {
            if (!ModelState.IsValid)
            {
                TempData["ErrorMessage"] = "Thông tin tạo phòng không hợp lệ.";
                return RedirectToAction(nameof(Index));
            }

            var currentUser = await _authService.GetCurrentUserAsync();
            if (currentUser == null)
                return RedirectToAction("Login", "Auth");

            var movie = await _context.Movies
                .Include(m => m.Episodes)
                .FirstOrDefaultAsync(m => m.MovieId == input.MovieId && (m.IsActive ?? false));

            if (movie == null)
            {
                TempData["ErrorMessage"] = "Không tìm thấy bộ phim được chọn.";
                return RedirectToAction(nameof(Index));
            }

            // Tạo mã phòng ngẫu nhiên 6 ký tự
            string roomCode = GenerateUniqueRoomCode();
            string shareToken = Convert.ToHexString(RandomNumberGenerator.GetBytes(16)).ToLowerInvariant();

            // Xác định tập phim
            var selectedEpisode = input.EpisodeId.HasValue 
                ? movie.Episodes.FirstOrDefault(e => e.EpisodeId == input.EpisodeId.Value)
                : movie.Episodes.FirstOrDefault();

            int epNumber = 1;
            if (selectedEpisode != null)
            {
                var match = Regex.Match(selectedEpisode.EpisodeName, @"\d+");
                if (match.Success && int.TryParse(match.Value, out int n))
                {
                    epNumber = n;
                }
            }

            var room = new WatchPartyRoom
            {
                Id = Guid.NewGuid(),
                RoomCode = roomCode,
                Title = string.IsNullOrWhiteSpace(input.Title) 
                    ? $"Xem phim {movie.Name} cùng {currentUser.FullName ?? currentUser.UserName}"
                    : input.Title.Trim(),
                ShareToken = shareToken,
                MovieId = movie.MovieId,
                MovieSlug = movie.Slug,
                EpisodeId = selectedEpisode?.EpisodeId,
                EpisodeNumber = epNumber,
                ServerName = !string.IsNullOrWhiteSpace(input.ServerName) ? input.ServerName.Trim() : (selectedEpisode?.ServerName ?? "Mặc định"),
                HostUserId = currentUser.Id,
                CurrentTime = 0,
                IsPlaying = false,
                IsPrivate = input.IsPrivate,
                PinCode = input.IsPrivate && !string.IsNullOrWhiteSpace(input.PinCode) ? input.PinCode.Trim() : null,
                MaxMembers = Math.Clamp(input.MaxMembers, 2, 100),
                OnlyHostControl = input.OnlyHostControl,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _context.WatchPartyRooms.Add(room);
            await _context.SaveChangesAsync();

            // Khởi tạo phiên trong Memory
            _watchPartyManager.GetOrCreateSession(
                room.Id, room.RoomCode, room.Title, room.ShareToken,
                room.MovieId, room.MovieSlug, room.EpisodeId, room.EpisodeNumber, room.ServerName,
                room.HostUserId, currentUser.FullName ?? currentUser.UserName ?? "Chủ phòng",
                currentUser.Avatar, room.IsPrivate, room.PinCode,
                room.MaxMembers, room.OnlyHostControl
            );

            return RedirectToAction(nameof(Room), new { roomCode = room.RoomCode });
        }

        // ==========================================
        // 🔍 API: TÌM KIẾM PHIM ĐỂ TẠO PHÒNG (AUTOCOMPLETE)
        // ==========================================
        [HttpGet("watch-party/api/search-movies")]
        public async Task<IActionResult> SearchMovies([FromQuery] string? q)
        {
            if (string.IsNullOrWhiteSpace(q) || q.Trim().Length < 2)
            {
                return Json(new { success = true, data = new List<object>() });
            }

            var query = q.Trim().ToLower();
            var movies = await _context.Movies
                .Where(m => (m.IsActive ?? false) && (
                    m.Name.ToLower().Contains(query) || 
                    (m.OriginalName != null && m.OriginalName.ToLower().Contains(query)) ||
                    m.Slug.ToLower().Contains(query)
                ))
                .OrderByDescending(m => m.ViewCount ?? 0)
                .Take(10)
                .Select(m => new
                {
                    movieId = m.MovieId,
                    name = m.Name,
                    originalName = m.OriginalName,
                    slug = m.Slug,
                    posterUrl = m.PosterUrl,
                    thumbUrl = m.ThumbUrl,
                    year = m.Year,
                    type = m.Type,
                    episodeTotal = m.EpisodeTotal
                })
                .ToListAsync();

            return Json(new { success = true, data = movies });
        }

        // ==========================================
        // 🎞️ API: LẤY DANH SÁCH TẬP CỦA PHIM
        // ==========================================
        [HttpGet("watch-party/api/movie-episodes")]
        public async Task<IActionResult> GetMovieEpisodes([FromQuery] int movieId)
        {
            var rawEpisodes = await _context.Episodes
                .Where(e => e.MovieId == movieId)
                .ToListAsync();

            var cleanedEpisodes = rawEpisodes
                .Select(e =>
                {
                    string cleanServer = string.IsNullOrWhiteSpace(e.ServerName)
                        ? "Mặc định"
                        : Regex.Replace(e.ServerName.Trim(), @"\s+", " ");

                    int num = 1;
                    var match = Regex.Match(e.EpisodeName ?? "", @"\d+");
                    if (match.Success && int.TryParse(match.Value, out int parsed))
                    {
                        num = parsed;
                    }

                    return new
                    {
                        episodeId = e.EpisodeId,
                        episodeName = (e.EpisodeName ?? "1").Trim(),
                        episodeNumber = num,
                        serverName = cleanServer,
                        slug = e.Slug ?? ""
                    };
                })
                .OrderBy(e => e.serverName)
                .ThenBy(e => e.episodeNumber)
                .ToList();

            return Json(new { success = true, data = cleanedEpisodes });
        }

        // ==========================================
        // 🔒 API: KIỂM TRA MÃ PIN PHÒNG RIÊNG TƯ
        // ==========================================
        [HttpPost("watch-party/api/validate-pin")]
        public async Task<IActionResult> ValidatePin([FromBody] ValidatePinRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.RoomCode))
                return Json(new { success = false, message = "Dữ liệu không hợp lệ." });

            var room = await _context.WatchPartyRooms
                .FirstOrDefaultAsync(r => r.RoomCode == request.RoomCode && r.IsActive);

            if (room == null)
                return Json(new { success = false, message = "Phòng không tồn tại hoặc đã kết thúc." });

            if (!room.IsPrivate)
                return Json(new { success = true, valid = true });

            bool isValid = string.Equals(room.PinCode, request.Pin?.Trim(), StringComparison.Ordinal);
            return Json(new { success = true, valid = isValid, message = isValid ? "Mã PIN hợp lệ" : "Mã PIN không chính xác." });
        }

        private string GenerateUniqueRoomCode()
        {
            const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
            var random = new Random();
            for (int i = 0; i < 10; i++)
            {
                var code = new string(Enumerable.Repeat(chars, 6).Select(s => s[random.Next(s.Length)]).ToArray());
                if (!_context.WatchPartyRooms.Any(r => r.RoomCode == code && r.IsActive))
                {
                    return code;
                }
            }
            return Guid.NewGuid().ToString("N").Substring(0, 6).ToUpperInvariant();
        }
    }

    public class ValidatePinRequest
    {
        public string RoomCode { get; set; } = string.Empty;
        public string? Pin { get; set; }
    }
}
