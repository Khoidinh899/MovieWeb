using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Razor;
using Microsoft.AspNetCore.Mvc.ViewEngines;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using MovieWeb.Data;
using MovieWeb.Models.DTOs;
using MovieWeb.Services.Interfaces;

namespace MovieWeb.Helpers;

/// <summary>
/// Chooses between the current UI (A, Views/**) and the V2 UI (B, Views/V2/**).
/// Priority:
/// 1. FeatureFlags:UiV2 = true -> V2 for all.
/// 2. Preview cookie ?ui=v2 / ?ui=v1 for admin/Development.
/// 3. mode = "off" -> V1 for all.
/// 4. mode = "members": Guests -> V1. Members -> UserUiPreferences ("v2" -> V2, else V1).
/// 5. mode = "all": Guests -> V2. Members -> UserUiPreferences ("v1" -> V1, else V2).
/// </summary>
public static class UiVersion
{
    public const string CookieName = "moonphim_ui";
    public const string SettingKey = "ui.rollout";
    private const string ItemKey = "__moonphim_ui_v2";
    public const string UserPrefCachePrefix = "UserUiPref_";
    public static readonly TimeSpan UserPrefCacheDuration = TimeSpan.FromMinutes(10);

    public static bool IsV2(HttpContext context)
    {
        if (context.Items.TryGetValue(ItemKey, out var cached) && cached is bool b) return b;
        var result = Compute(context);
        context.Items[ItemKey] = result;
        return result;
    }

    private static bool Compute(HttpContext context)
    {
        // 1. FeatureFlag cứng (dùng làm khóa rollback hoặc override toàn cục)
        var config = context.RequestServices.GetService<IConfiguration>();
        if (config != null && config.GetValue<bool>("FeatureFlags:UiV2")) return true;

        // 2. Cookie xem trước ?ui=v2 / ?ui=v1 (admin / Development)
        if (CanPreview(context))
        {
            var cookie = context.Request.Cookies[CookieName];
            if (cookie == "v2") return true;
            if (cookie == "v1") return false;
        }

        // Bỏ qua nếu là request API dùng Bearer token (JWT của app mobile)
        var authHeader = context.Request.Headers.Authorization.ToString();
        if (!string.IsNullOrEmpty(authHeader) && authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        // 3. Đọc cấu hình rollout từ SiteSettings
        var siteSettings = context.RequestServices.GetService<ISiteSettingsService>();
        var rollout = siteSettings?.Get<UiRolloutSetting>(SettingKey);
        var mode = (rollout?.Mode ?? "off").Trim().ToLowerInvariant();

        if (mode == "off")
        {
            return false;
        }

        var isAuth = context.User?.Identity?.IsAuthenticated == true;
        int? userId = null;
        if (isAuth)
        {
            var claimVal = context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (int.TryParse(claimVal, out int uid))
            {
                userId = uid;
            }
        }

        if (mode == "members")
        {
            // Khách -> luôn V1 (Googlebot cũng là khách, giữ nguyên SEO)
            if (!isAuth || !userId.HasValue)
            {
                return false;
            }

            var pref = GetUserUiPreference(context, userId.Value);
            return pref == "v2";
        }

        if (mode == "all")
        {
            // Khách -> V2
            if (!isAuth || !userId.HasValue)
            {
                return true;
            }

            var pref = GetUserUiPreference(context, userId.Value);
            return pref != "v1"; // Nếu đã chọn V1 thì giữ V1, còn lại (V2 hoặc chưa chọn) -> V2
        }

        return false;
    }

    public static string GetUserUiPreference(HttpContext context, int userId)
    {
        var cache = context.RequestServices.GetService<IMemoryCache>();
        var cacheKey = UserPrefCachePrefix + userId;

        if (cache != null && cache.TryGetValue(cacheKey, out string? cached) && cached != null)
        {
            return cached;
        }

        var db = context.RequestServices.GetService<MovieWebDbContext>();
        string prefVal = "";
        if (db != null)
        {
            var pref = db.UserUiPreferences.AsNoTracking().FirstOrDefault(p => p.UserId == userId);
            if (pref != null && !string.IsNullOrWhiteSpace(pref.Ui))
            {
                prefVal = pref.Ui.Trim().ToLowerInvariant();
            }
        }

        if (cache != null)
        {
            cache.Set(cacheKey, prefVal, UserPrefCacheDuration);
        }

        return prefVal;
    }

    public static void InvalidateUserPreferenceCache(IMemoryCache cache, int userId)
    {
        cache.Remove(UserPrefCachePrefix + userId);
    }

    public static string GetRolloutMode(HttpContext context)
    {
        var siteSettings = context.RequestServices.GetService<ISiteSettingsService>();
        var rollout = siteSettings?.Get<UiRolloutSetting>(SettingKey);
        return (rollout?.Mode ?? "off").Trim().ToLowerInvariant();
    }

    public static bool CanPreview(HttpContext context)
    {
        var env = context.RequestServices.GetRequiredService<IWebHostEnvironment>();
        return env.IsDevelopment() || context.User.HasClaim("RoleId", "1");
    }

    /// <summary>?ui=v2 / ?ui=v1 toggles the preview cookie for allowed users. Runs after authentication.</summary>
    public static IApplicationBuilder UseUiVersionSwitch(this IApplicationBuilder app) => app.Use(async (context, next) =>
    {
        var ui = context.Request.Query["ui"].ToString();
        if ((ui == "v2" || ui == "v1") && CanPreview(context))
        {
            if (ui == "v2")
                context.Response.Cookies.Append(CookieName, "v2", new CookieOptions
                {
                    HttpOnly = true, Secure = context.Request.IsHttps, SameSite = SameSiteMode.Lax,
                    MaxAge = TimeSpan.FromDays(7), IsEssential = true
                });
            else
                context.Response.Cookies.Delete(CookieName);
            context.Items[ItemKey] = ui == "v2";
        }
        await next();
    });
}

/// <summary>
/// Explicit paths (View("~/Views/User/Profile.cshtml"), PartialAsync("~/Views/...")) bypass the expander,
/// so in V2 mode "~/Views/X" first tries "~/Views/V2/X" and falls back to the original.
/// </summary>
public sealed class UiV2CompositeViewEngine : ICompositeViewEngine
{
    private const string Root = "~/Views/";
    private readonly CompositeViewEngine _inner;
    private readonly IHttpContextAccessor _http;

    public UiV2CompositeViewEngine(IOptions<MvcViewOptions> options, IHttpContextAccessor http)
    {
        _inner = new CompositeViewEngine(options);
        _http = http;
    }

    public IReadOnlyList<IViewEngine> ViewEngines => _inner.ViewEngines;

    public ViewEngineResult FindView(ActionContext context, string viewName, bool isMainPage) =>
        _inner.FindView(context, viewName, isMainPage);

    public ViewEngineResult GetView(string? executingFilePath, string viewPath, bool isMainPage)
    {
        var context = _http.HttpContext;
        if (context is not null && UiVersion.IsV2(context)
            && viewPath.StartsWith(Root, StringComparison.Ordinal) && !viewPath.StartsWith(Root + "V2/", StringComparison.Ordinal))
        {
            var v2 = _inner.GetView(executingFilePath, Root + "V2/" + viewPath[Root.Length..], isMainPage);
            if (v2.Success) return v2;
        }
        return _inner.GetView(executingFilePath, viewPath, isMainPage);
    }
}

/// <summary>Looks in Views/V2/{Controller}/ and Views/V2/Shared/ first when the V2 UI is active.</summary>
public sealed class UiV2ViewLocationExpander : IViewLocationExpander
{
    public void PopulateValues(ViewLocationExpanderContext context) =>
        context.Values["ui"] = UiVersion.IsV2(context.ActionContext.HttpContext) ? "v2" : "v1";

    public IEnumerable<string> ExpandViewLocations(ViewLocationExpanderContext context, IEnumerable<string> viewLocations)
    {
        if (!context.Values.TryGetValue("ui", out var ui) || ui != "v2") return viewLocations;
        return new[] { "/Views/V2/{1}/{0}.cshtml", "/Views/V2/Shared/{0}.cshtml" }.Concat(viewLocations);
    }
}
