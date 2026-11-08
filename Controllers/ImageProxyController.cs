using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using SkiaSharp;

namespace MovieWeb.Controllers;

/// <summary>
/// Trạm ảnh WebP nội bộ: GET /img/w{width}/{path}.webp → tải ảnh từ https://vsmov.com/storage/images/{path},
/// thu nhỏ theo bề rộng, đổi sang WebP, lưu cache đĩa. Chỉ nhận nguồn vsmov.com (chống SSRF), bề rộng thuộc tập cố định,
/// tối đa 2 tác vụ resize đồng thời (container ~768 MB RAM). Lỗi → 302 về ảnh gốc.
/// </summary>
public class ImageProxyController : Controller
{
    private const string SourceBase = "https://vsmov.com/storage/images/";
    private const long MaxSourceBytes = 15L * 1024 * 1024;
    private const int MaxSourceDimension = 12000;
    private static readonly int[] AllowedWidths = { 200, 400, 800, 1280 };
    private static readonly string[] AllowedExtensions = { ".jpg", ".jpeg", ".png", ".webp", ".gif" };
    private static readonly SemaphoreSlim ResizeGate = new(2, 2);
    private static readonly HttpClient Http = new(new SocketsHttpHandler { PooledConnectionLifetime = TimeSpan.FromMinutes(10) })
    {
        Timeout = TimeSpan.FromSeconds(10)
    };

    private readonly IWebHostEnvironment _env;
    private readonly ILogger<ImageProxyController> _logger;

    public ImageProxyController(IWebHostEnvironment env, ILogger<ImageProxyController> logger)
    {
        _env = env;
        _logger = logger;
    }

    [HttpGet("img/w{width:int}/{**path}")]
    public async Task<IActionResult> Get(int width, string? path, CancellationToken cancellationToken)
    {
        if (!AllowedWidths.Contains(width)) return BadRequest("Unsupported width");
        if (!TryGetSourcePath(path, out var sourcePath)) return BadRequest("Invalid path");

        var sourceUrl = SourceBase + string.Join('/', sourcePath.Split('/').Select(Uri.EscapeDataString));
        var cacheFile = CacheFile(width, sourcePath);

        try
        {
            if (!HasCache(cacheFile))
            {
                await ResizeGate.WaitAsync(cancellationToken);
                try
                {
                    if (!HasCache(cacheFile))
                    {
                        var bytes = await BuildWebpAsync(sourceUrl, width, cancellationToken);
                        if (bytes is null || bytes.Length == 0) return Redirect(sourceUrl); // không bao giờ trả 200 rỗng
                        if (!TryWriteCache(cacheFile, bytes)) return WebpResult(bytes);
                    }
                }
                finally
                {
                    ResizeGate.Release();
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Image proxy failed for {Url}", sourceUrl);
            return Redirect(sourceUrl);
        }

        Response.Headers.CacheControl = "public, max-age=31536000, immutable";
        return PhysicalFile(cacheFile, "image/webp");
    }

    // File cache rỗng (ghi dở/lỗi đĩa) coi như chưa có cache.
    private static bool HasCache(string file)
    {
        var info = new FileInfo(file);
        return info.Exists && info.Length > 0;
    }

    private static bool TryGetSourcePath(string? path, out string sourcePath)
    {
        sourcePath = "";
        if (string.IsNullOrWhiteSpace(path)) return false;
        // Đuôi .webp chỉ để Cloudflare/nginx coi là ảnh; nguồn là phần phía trước.
        if (!path.EndsWith(".webp", StringComparison.OrdinalIgnoreCase)) return false;
        var source = path[..^".webp".Length];
        if (source.Length is 0 or > 300) return false;
        if (source.Contains("..") || source.Contains('\\') || source.Contains(':') || source.Contains('%') || source.StartsWith('/')) return false;
        var segments = source.Split('/');
        if (segments.Any(s => s.Length == 0 || s.StartsWith('.'))) return false;
        if (!AllowedExtensions.Contains(Path.GetExtension(source).ToLowerInvariant())) return false;
        if (!source.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_' or '.' or '/')) return false;
        sourcePath = source;
        return true;
    }

    private string CacheFile(int width, string sourcePath)
    {
        var hash = Convert.ToHexString(SHA1.HashData(Encoding.UTF8.GetBytes(sourcePath))).ToLowerInvariant();
        return Path.Combine(_env.ContentRootPath, "App_Data", "img-cache", $"w{width}", hash + ".webp");
    }

    private static async Task<byte[]?> BuildWebpAsync(string sourceUrl, int width, CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(10));
        using var request = new HttpRequestMessage(HttpMethod.Get, sourceUrl);
        request.Headers.UserAgent.ParseAdd("MoonPhimImageProxy/1.0");
        using var response = await Http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
        if (!response.IsSuccessStatusCode) return null;
        if (response.Content.Headers.ContentLength is > MaxSourceBytes) return null;

        // Đọc có giới hạn dung lượng (không tin Content-Length).
        await using var network = await response.Content.ReadAsStreamAsync(timeout.Token);
        using var buffer = new MemoryStream();
        var chunk = new byte[81920];
        int read;
        while ((read = await network.ReadAsync(chunk, timeout.Token)) > 0)
        {
            buffer.Write(chunk, 0, read);
            if (buffer.Length > MaxSourceBytes) return null;
        }
        buffer.Position = 0;

        return await Task.Run(() => Encode(buffer, width), cancellationToken);
    }

    private static byte[]? Encode(Stream source, int width)
    {
        using var codec = SKCodec.Create(source);
        if (codec is null) return null;
        var info = codec.Info;
        if (info.Width <= 0 || info.Height <= 0 || info.Width > MaxSourceDimension || info.Height > MaxSourceDimension) return null;

        var targetWidth = Math.Min(width, info.Width); // chỉ thu nhỏ, không phóng to
        var targetHeight = Math.Max(1, (int)Math.Round(info.Height * (targetWidth / (double)info.Width)));

        // Giải mã ở kích thước thu nhỏ do codec hỗ trợ (JPEG: 1/2, 1/4, 1/8) để đỡ RAM.
        var scaled = codec.GetScaledDimensions((float)targetWidth / info.Width);
        var decodeInfo = new SKImageInfo(Math.Max(scaled.Width, 1), Math.Max(scaled.Height, 1), SKColorType.Rgba8888, SKAlphaType.Premul);
        using var decoded = new SKBitmap(decodeInfo);
        if (codec.GetPixels(decodeInfo, decoded.GetPixels()) != SKCodecResult.Success) return null;

        SKBitmap output = decoded;
        SKBitmap? resized = null;
        try
        {
            if (decoded.Width != targetWidth)
            {
                resized = decoded.Resize(new SKImageInfo(targetWidth, targetHeight, SKColorType.Rgba8888, SKAlphaType.Premul), SKFilterQuality.Medium);
                if (resized is null) return null;
                output = resized;
            }
            using var image = SKImage.FromBitmap(output);
            using var data = image.Encode(SKEncodedImageFormat.Webp, 75);
            return data?.ToArray();
        }
        finally
        {
            resized?.Dispose();
        }
    }

    private static bool TryWriteCache(string file, byte[] bytes)
    {
        string? temp = null;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(file)!);
            temp = file + "." + Guid.NewGuid().ToString("N") + ".tmp";
            System.IO.File.WriteAllBytes(temp, bytes);
            System.IO.File.Move(temp, file, overwrite: true);
            return true;
        }
        catch
        {
            try { if (temp is not null) System.IO.File.Delete(temp); } catch { /* bỏ qua */ }
            return false; // ví dụ thư mục chỉ đọc: vẫn trả ảnh nhưng không cache đĩa
        }
    }

    private FileContentResult WebpResult(byte[] bytes)
    {
        Response.Headers.CacheControl = "public, max-age=86400";
        return File(bytes, "image/webp");
    }
}
