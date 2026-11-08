using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using MovieWeb.Models.Entities;

namespace MovieWeb.Helpers;

public static partial class SeoContentHelper
{
    /// <summary>
    /// Sinh văn bản SEO Spintax độc nhất (180 - 250 từ) cho từng tập phim với Deterministic Random (hạt giống cố định theo minEpisodeId).
    /// </summary>
    public static string GenerateEpisodeSpintax(
        Movie movie, 
        Episode? currentEpisode, 
        List<Episode> allEpisodes, 
        Dictionary<string, string>? serverDisplayNames = null)
    {
        if (movie == null) return string.Empty;

        int seed = (currentEpisode?.EpisodeId ?? movie.MovieId) * 397 ^ (movie.MovieId * 17);
        var rnd = new Random(seed);

        string movieName = movie.Name ?? "Bộ phim";
        string originalName = !string.IsNullOrWhiteSpace(movie.OriginalName) && !movie.OriginalName.Equals(movie.Name, StringComparison.OrdinalIgnoreCase)
            ? $" ({movie.OriginalName})" : string.Empty;
        string epName = currentEpisode != null ? currentEpisode.EpisodeName : (movie.EpisodeCurrent ?? "Full");
        string yearStr = movie.Year.HasValue ? $"năm {movie.Year.Value}" : "mới nhất";
        string quality = !string.IsNullOrWhiteSpace(movie.Quality) ? movie.Quality : "Full HD 1080p";

        // Tìm tập trước và tập sau
        int? epNum = ExtractEpisodeNumber(epName);
        string prevEpisodeText = string.Empty;
        string nextEpisodeText = string.Empty;

        if (epNum.HasValue && epNum.Value > 1)
        {
            int prevNum = epNum.Value - 1;
            prevEpisodeText = PickRandom(rnd,
                $"Sau những diễn biến kịch tính ở tập {prevNum}, câu chuyện tiếp tục được đẩy lên cao trào. ",
                $"Nối tiếp mạch cảm xúc gay cấn từ tập {prevNum}, tình tiết ở tập này trở nên bất ngờ hơn bao giờ hết. ",
                $"Khép lại những khúc mắc chưa có lời giải ở tập {prevNum}, các nhân vật bước vào giai đoạn quyết định. ");
        }

        if (epNum.HasValue)
        {
            int nextNum = epNum.Value + 1;
            bool hasNext = allEpisodes.Any(e => ExtractEpisodeNumber(e.EpisodeName) == nextNum);
            if (hasNext)
            {
                nextEpisodeText = PickRandom(rnd,
                    $"Đừng quên đón xem tập {nextNum} tiếp theo trên MoonPhim để theo dõi bước ngoặt mới! ",
                    $"Những bí mật sẽ tiếp tục được hé lộ trong tập {nextNum} sắp tới, hãy chú ý theo dõi nhé. ",
                    $"Tập {nextNum} kế tiếp hứa hẹn sẽ mang đến nhiều bất ngờ không thể rời mắt. ");
            }
            else
            {
                nextEpisodeText = PickRandom(rnd,
                    $"Các tập tiếp theo của bộ phim đang được MoonPhim cập nhật liên tục với tốc độ nhanh nhất. ",
                    $"Hãy bấm lưu phim hoặc chia sẻ để nhận thông báo sớm nhất ngay khi tập mới ra mắt. ",
                    $"Theo dõi lịch phát sóng của phim trên MoonPhim để không bỏ lỡ tập phát sóng kế tiếp. ");
            }
        }

        // Danh sách server
        var servers = serverDisplayNames?.Values.Distinct().ToList() ?? new List<string>();
        string serverText;
        if (servers.Any())
        {
            string serverListStr = string.Join(", ", servers);
            serverText = PickRandom(rnd,
                $"Hệ thống máy chủ hỗ trợ đa dạng định dạng gồm {serverListStr}, tối ưu hóa băng thông tải nhanh và ổn định.",
                $"Người xem có thể linh hoạt chuyển đổi giữa các nguồn phát ({serverListStr}) để có trải nghiệm xem mượt mà nhất.",
                $"MoonPhim cung cấp các lựa chọn nguồn xem phong phú như {serverListStr}, chuẩn âm thanh sống động và hình ảnh sắc nét.");
        }
        else
        {
            serverText = "Hệ thống phát video chất lượng cao, tự động chuyển server dự phòng khi gặp sự cố đường truyền.";
        }

        // Ekip & Diễn viên
        var actors = movie.Actors?.Select(a => a.Name).Take(5).ToList() ?? new List<string>();
        var directors = movie.Directors?.Select(d => d.Name).Take(3).ToList() ?? new List<string>();
        var categories = movie.Categories?.Select(c => c.Name).Take(4).ToList() ?? new List<string>();

        string actorStr = actors.Any() ? string.Join(", ", actors) : "dàn diễn viên tài năng";
        string directorStr = directors.Any() ? string.Join(", ", directors) : "đạo diễn danh tiếng";
        string categoryStr = categories.Any() ? string.Join(", ", categories) : "đa thể loại đặc sắc";

        // Tóm tắt ngắn cốt truyện (120 - 180 ký tự)
        string plotSnippet = CleanDescription(movie.Description ?? movie.Content ?? string.Empty, 180);
        if (!string.IsNullOrWhiteSpace(plotSnippet))
        {
            plotSnippet = $"Nội dung tóm lược: {plotSnippet} ";
        }

        // Ghép các khối văn bản Spintax
        string opening = PickRandom(rnd,
            $"Chào mừng bạn đến với trang xem phim {movieName}{originalName} tập {epName} chuẩn {quality} online.",
            $"Thưởng thức trọn vẹn bộ phim {movieName}{originalName} tập {epName} chất lượng {quality} miễn phí.",
            $"Bạn đang theo dõi phim truyền hình {movieName}{originalName} tập {epName} với độ nét {quality} đỉnh cao.");

        string midBlock = PickRandom(rnd,
            $"Tác phẩm ra mắt {yearStr}, thuộc thể loại {categoryStr}, được thực hiện dưới sự chỉ đạo của {directorStr} cùng màn hóa thân ấn tượng của {actorStr}.",
            $"Quy tụ đội ngũ sáng tạo tài hoa gồm {directorStr} và dàn diễn viên {actorStr}, dự án phim {yearStr} khai thác chủ đề {categoryStr} đầy cuốn hút.",
            $"Mang đậm phong cách {categoryStr}, bộ phim {yearStr} ghi dấu ấn mạnh mẽ nhờ bàn tay của {directorStr} và tài năng diễn xuất từ {actorStr}.");

        string closing = PickRandom(rnd,
            $"Chúc bạn có những giây phút giải trí tuyệt vời cùng {movieName} tập {epName} tại MoonPhim!",
            $"MoonPhim tự hào mang đến cho bạn trải nghiệm điện ảnh chân thực và tiện lợi nhất.",
            $"Cảm ơn bạn đã luôn đồng hành và lựa chọn MoonPhim để xem phim mỗi ngày.");

        var sb = new StringBuilder();
        sb.Append(opening).Append(' ');
        if (!string.IsNullOrWhiteSpace(prevEpisodeText)) sb.Append(prevEpisodeText);
        sb.Append(midBlock).Append(' ');
        if (!string.IsNullOrWhiteSpace(plotSnippet)) sb.Append(plotSnippet);
        sb.Append(serverText).Append(' ');
        if (!string.IsNullOrWhiteSpace(nextEpisodeText)) sb.Append(nextEpisodeText);
        sb.Append(closing);

        return sb.ToString().Trim();
    }

    private static int? ExtractEpisodeNumber(string? epName)
    {
        if (string.IsNullOrWhiteSpace(epName)) return null;
        var match = NumberRegex().Match(epName);
        if (match.Success && int.TryParse(match.Value, out var num))
        {
            return num;
        }
        return null;
    }

    private static string PickRandom(Random rnd, params string[] options)
    {
        if (options == null || options.Length == 0) return string.Empty;
        return options[rnd.Next(options.Length)];
    }

    private static string CleanDescription(string raw, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(raw)) return string.Empty;
        var text = WebUtility.HtmlDecode(HtmlTagsRegex().Replace(raw, " "));
        text = MultiSpacesRegex().Replace(text, " ").Trim();
        if (text.Length <= maxLength) return text;
        int end = text.LastIndexOf(' ', maxLength);
        return (end > 50 ? text[..end] : text[..maxLength]).TrimEnd() + "…";
    }

    [GeneratedRegex(@"\d+")]
    private static partial Regex NumberRegex();

    [GeneratedRegex(@"<[^>]*>")]
    private static partial Regex HtmlTagsRegex();

    [GeneratedRegex(@"\s+")]
    private static partial Regex MultiSpacesRegex();
}
