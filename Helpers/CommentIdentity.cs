using System.Text.RegularExpressions;
using MovieWeb.Models.Entities;

namespace MovieWeb.Helpers;

/// <summary>
/// Tên hiển thị công khai của người bình luận. Không bao giờ lộ email/username đăng nhập:
/// - Bình thường: Họ và tên; nếu chưa có họ tên thì dùng biệt danh.
/// - Ẩn danh: biệt danh (người dùng tự đặt, hoặc hệ thống tự sinh cố định theo tài khoản), ảnh mặc định.
/// Admin xem được danh tính gốc qua <see cref="RealIdentity"/>.
/// </summary>
public static partial class CommentIdentity
{
    public const string DefaultAvatar = "/images/nouser.png";
    public const int NicknameMaxLength = 30;

    private static readonly string[] Adjectives =
    [
        "Bright", "Sharp", "Eager", "Smart", "Brave", "Calm", "Clever", "Cosmic", "Happy", "Lucky",
        "Swift", "Gentle", "Jolly", "Mighty", "Noble", "Quiet", "Sunny", "Witty", "Cool", "Moon"
    ];

    private static readonly string[] Animals =
    [
        "Koala", "Otter", "Bison", "Panda", "Falcon", "Fox", "Owl", "Tiger", "Dolphin", "Rabbit",
        "Penguin", "Lynx", "Raven", "Turtle", "Wolf", "Deer", "Hawk", "Cat", "Bear", "Whale"
    ];

    /// <summary>Biệt danh tự sinh, cố định theo UserId (không cần lưu DB), vd "BrightKoala7992".</summary>
    public static string GeneratedNickname(int userId)
    {
        var x = (uint)userId * 2654435761u; // trộn bit để id liền nhau ra tên khác nhau
        var adjective = Adjectives[x % (uint)Adjectives.Length];
        var animal = Animals[(x / 31) % (uint)Animals.Length];
        var number = 1000 + (int)((x / 977) % 9000);
        return $"{adjective}{animal}{number}";
    }

    public static string Nickname(User? user) => user is null
        ? "Ẩn danh"
        : string.IsNullOrWhiteSpace(user.CommentNickname) ? GeneratedNickname(user.Id) : user.CommentNickname.Trim();

    /// <summary>Họ tên kiểu Việt: đăng ký lưu tên (chữ cuối) vào FirstName, họ + đệm vào LastName → "LastName FirstName".</summary>
    public static string VietnameseFullName(User user) => $"{user.LastName} {user.FirstName}".Trim();

    public static string PublicName(User? user, bool anonymous)
    {
        if (user is null) return "Ẩn danh";
        if (anonymous) return Nickname(user);
        var fullName = VietnameseFullName(user);
        return string.IsNullOrWhiteSpace(fullName) || fullName.Contains('@') ? Nickname(user) : fullName;
    }

    public static string PublicAvatar(User? user, bool anonymous) =>
        anonymous || string.IsNullOrEmpty(user?.Avatar) ? DefaultAvatar : user.Avatar!;

    /// <summary>Danh tính gốc, chỉ trả về cho admin.</summary>
    public static string RealIdentity(User? user)
    {
        if (user is null) return "";
        var fullName = VietnameseFullName(user);
        return string.IsNullOrWhiteSpace(fullName) ? user.Email ?? user.UserName ?? "" : $"{fullName} · {user.Email ?? user.UserName}";
    }

    /// <summary>Kiểm tra biệt danh người dùng tự đặt. Trả về thông báo lỗi, hoặc null nếu hợp lệ.</summary>
    public static string? ValidateNickname(string? nickname)
    {
        var value = (nickname ?? "").Trim();
        if (value.Length < 2 || value.Length > NicknameMaxLength)
            return $"Biệt danh dài từ 2 đến {NicknameMaxLength} ký tự.";
        if (!NicknamePattern().IsMatch(value))
            return "Biệt danh chỉ gồm chữ, số, khoảng trắng, dấu gạch dưới, gạch ngang hoặc dấu chấm.";
        var lower = value.ToLowerInvariant();
        if (lower.Contains("admin") || lower.Contains("moonphim") || lower.Contains("quản trị") || lower.Contains("quan tri"))
            return "Biệt danh không được chứa tên quản trị viên hoặc MoonPhim.";
        return null;
    }

    [GeneratedRegex(@"^[\p{L}\p{M}\p{N} _.\-]+$")]
    private static partial Regex NicknamePattern();
}
