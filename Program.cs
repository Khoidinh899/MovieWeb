using MovieWeb.Services;
using MovieWeb.Services.Interfaces;
using MovieWeb.Repositories;
using Microsoft.EntityFrameworkCore;
using MovieWeb.Models.Entities;
using MovieWeb.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using MovieWeb.Models;
using DotNetEnv;
using MovieWeb.Hubs;
using Microsoft.AspNetCore.SignalR;
using StripeLib = Stripe;
using Hangfire;
using Hangfire.PostgreSql;
using MovieWeb.Filters;
using MovieWeb.Jobs;
using SendGrid.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using MovieWeb.Middlewares;

var builder = WebApplication.CreateBuilder(args);

// ✅ Hỗ trợ lưu DateTime không thuộc múi giờ UTC vào PostgreSQL (giống SQL Server)
AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

// ✅ Load .env file - Ưu tiên .env.local cho local development
if (builder.Environment.IsDevelopment())
{
    if (File.Exists(".env.local"))
    {
        Env.Load(".env.local");
        Console.WriteLine("✅ Loaded .env.local");
    }
    else if (File.Exists(".env"))
    {
        Env.Load();
        Console.WriteLine("⚠️ Loaded .env (fallback)");
    }
}
else
{
    if (File.Exists(".env"))
    {
        Env.Load();
        Console.WriteLine("🌐 Loaded .env (production)");
    }
}

builder.Configuration.AddEnvironmentVariables();

// ✅ THÊM ĐOẠN NÀY ĐỂ DEBUG
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
Console.WriteLine($"🔍 Environment: {builder.Environment.EnvironmentName}");
Console.WriteLine($"🗄️ Connection String: {connectionString?[..Math.Min(60, connectionString?.Length ?? 0)]}...");
Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");

// ===== SERVICE CONFIGURATION =====
// ✅ Đăng ký Global Exception Filter
builder.Services.AddControllersWithViews(options =>
{
    options.Filters.Add<GlobalExceptionFilter>();
});

// Kết nối DbContext với PostgreSQL
builder.Services.AddDbContext<MovieWebDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// Configure Identity
builder.Services.AddIdentity<User, Role>(options =>
{
    options.Password.RequiredLength = 6;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequireDigit = false;
    options.Password.RequireUppercase = false;
    options.Password.RequireLowercase = false;
    options.User.RequireUniqueEmail = true;
    options.SignIn.RequireConfirmedEmail = true;
    options.SignIn.RequireConfirmedAccount = false;
    options.Tokens.EmailConfirmationTokenProvider = TokenOptions.DefaultEmailProvider;
})
.AddEntityFrameworkStores<MovieWebDbContext>()
.AddUserStore<UserStore<User, Role, MovieWebDbContext, int>>()
.AddRoleStore<RoleStore<Role, MovieWebDbContext, int>>()
.AddDefaultTokenProviders()
.AddClaimsPrincipalFactory<CustomUserClaimsPrincipalFactory>(); // ⭐ THÊM DÒNG NÀY

builder.Services.Configure<SecurityStampValidatorOptions>(options =>
{
    options.ValidationInterval = TimeSpan.FromMinutes(30);
});

// ✅ Cấu hình Cookie sau khi AddIdentity
builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Auth/Login";
    options.LogoutPath = "/Auth/Logout";
    options.AccessDeniedPath = "/Auth/AccessDenied";
    options.ExpireTimeSpan = TimeSpan.FromDays(30);
    options.SlidingExpiration = true;
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    // options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.Cookie.Name = "MoonPhim.Auth.Azure"; 
    
    options.Events.OnRedirectToLogin = context =>
    {
        if (context.Request.Path.StartsWithSegments("/api") || 
            context.Request.Path.StartsWithSegments("/notificationHub"))
        {
            context.Response.StatusCode = 401;
            return Task.CompletedTask;
        }
        context.Response.Redirect(context.RedirectUri);
        return Task.CompletedTask;
    };
    
    options.Events.OnRedirectToAccessDenied = context =>
    {
        if (context.Request.Path.StartsWithSegments("/api") || 
            context.Request.Path.StartsWithSegments("/notificationHub"))
        {
            context.Response.StatusCode = 403;
            return Task.CompletedTask;
        }
        context.Response.Redirect(context.RedirectUri);
        return Task.CompletedTask;
    };
});

// ✅ Thêm JWT Authentication (cho API nếu cần)
builder.Services.AddAuthentication()
.AddJwtBearer("JwtScheme", options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = builder.Configuration["JwtSettings:Issuer"],
        ValidAudience = builder.Configuration["JwtSettings:Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(builder.Configuration["JwtSettings:SecretKey"] 
                ?? throw new InvalidOperationException("JWT SecretKey not found"))),
        ClockSkew = TimeSpan.Zero
    };
})
    .AddGoogle(options =>
    {
        options.ClientId = Environment.GetEnvironmentVariable("Authentication__Google__ClientId") ?? "";
        options.ClientSecret = Environment.GetEnvironmentVariable("Authentication__Google__ClientSecret") ?? "";
        options.CallbackPath = "/signin-google";
        options.Events = new Microsoft.AspNetCore.Authentication.OAuth.OAuthEvents
        {
            OnRedirectToAuthorizationEndpoint = context =>
            {
                context.Response.Redirect(context.RedirectUri + "&prompt=select_account");
                return System.Threading.Tasks.Task.CompletedTask;
            }
        };
    });

// Authorization policies
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", policy => policy.RequireClaim("RoleId", "1"));
    options.AddPolicy("UserOrAdmin", policy => policy.RequireAssertion(context =>
        context.User.HasClaim("RoleId", "1") || context.User.HasClaim("RoleId", "2")));
});

// ===== HANGFIRE CONFIGURATION =====
builder.Services.AddHangfire(configuration => configuration
    .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
    .UseSimpleAssemblyNameTypeSerializer()
    .UseRecommendedSerializerSettings()
    .UsePostgreSqlStorage(options =>
        options.UseNpgsqlConnection(
            builder.Configuration.GetConnectionString("DefaultConnection")
        )
    ));

builder.Services.AddHangfireServer(options =>
{
    options.WorkerCount = 2;
    options.ServerName = "MoonPhim-BackgroundServer";
});

// Register Hangfire Jobs
builder.Services.AddScoped<PaymentReminderJob>();
builder.Services.AddScoped<SendRealtimeNotificationJob>();

// ===== SENDGRID EMAIL SENDER =====
builder.Services.Configure<SendGridOptions>(builder.Configuration.GetSection("SendGrid"));

builder.Services.AddSendGrid(options =>
{
    options.ApiKey = builder.Configuration.GetSection("SendGrid")["ApiKey"];
});

builder.Services.AddTransient<Microsoft.AspNetCore.Identity.UI.Services.IEmailSender, SendGridEmailSender>();
builder.Services.AddTransient<IEmailSender<User>, SendGridEmailSender>();
builder.Services.AddTransient<IEmailService, SendGridEmailSender>();
builder.Services.AddScoped<IStudentEmailService, StudentEmailService>();

// ===== STRIPE CONFIGURATION =====
builder.Services.Configure<StripeSettings>(builder.Configuration.GetSection("StripeSettings"));
StripeLib.StripeConfiguration.ApiKey = builder.Configuration["StripeSettings:SecretKey"];

// ===== CORE SERVICES =====
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IProfileService, ProfileService>();
builder.Services.AddSingleton<IFcmNotificationService, FcmNotificationService>();
builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddScoped<IStripeService, StripeService>();
builder.Services.AddScoped<ISubscriptionService, SubscriptionService>();

// ===== MOVIE SERVICES =====
builder.Services.AddScoped<IOPhimService, OPhimService>();
builder.Services.AddScoped<IMovieRepository, MovieRepository>();
builder.Services.AddHostedService<BackgroundSyncService>();
builder.Services.AddScoped<ICategorySyncService, CategorySyncService>();
builder.Services.AddScoped<ICountrySyncService, CountrySyncService>();
builder.Services.AddScoped<IActorSyncService, ActorSyncService>();
builder.Services.AddScoped<IDirectorSyncService, DirectorSyncService>();
builder.Services.AddScoped<IMovieSyncService, MovieSyncService>();

// ===== OTHER SERVICES =====
builder.Services.AddControllersWithViews()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
    });
builder.Services.AddHttpClient();
builder.Services.AddMemoryCache();
builder.Services.AddHttpContextAccessor();
builder.Services.AddSignalR();
builder.Services.AddSingleton<IUserIdProvider, CustomUserIdProvider>();
builder.Services.AddScoped<IRecommendationService, RecommendationService>();
builder.Services.AddHttpClient<IGeminiService, GeminiService>();
builder.Services.AddScoped<IMovieRequestService, MovieRequestService>();

builder.Services.AddAntiforgery(options =>
{
    options.HeaderName = "RequestVerificationToken";
});

// ===== CORS =====
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

builder.Services.AddLogging();

// Thêm vào trước builder.Build()
builder.Services.AddScoped<MovieWeb.Services.Interfaces.IFavoriteService, MovieWeb.Services.FavoriteService>();
builder.Services.AddScoped<MovieWeb.Services.Interfaces.IWatchHistoryService, MovieWeb.Services.WatchHistoryService>();

var app = builder.Build();

// Configure app to trust forwarded headers from Nginx reverse proxy
var forwardedHeadersOptions = new ForwardedHeadersOptions
{
    ForwardedHeaders = Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedFor | Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedProto
};
forwardedHeadersOptions.KnownNetworks.Clear();
forwardedHeadersOptions.KnownProxies.Clear();
app.UseForwardedHeaders(forwardedHeadersOptions);

// ===== AUTO MIGRATE DATABASE =====
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var logger = services.GetRequiredService<ILogger<Program>>();

    try
    {
        var context = services.GetRequiredService<MovieWebDbContext>();
        logger.LogInformation("🔄 Checking for pending database migrations...");

        var pendingMigrations = context.Database.GetPendingMigrations();

        if (pendingMigrations.Any())
        {
            logger.LogWarning($"⚠️ Found {pendingMigrations.Count()} pending migrations. Applying...");
            context.Database.Migrate();
            logger.LogInformation("✅ Database migrations applied successfully!");
        }
        else
        {
            logger.LogInformation("✅ Database is up to date. No pending migrations.");
        }
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "❌ An error occurred while migrating the database.");
        logger.LogError("⚠️ App will continue but database may not be in sync!");
    }
}

// ===== AUTO SYNC STRIPE PLANS ON NEW ACCOUNT =====
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var logger = services.GetRequiredService<ILogger<Program>>();
    try
    {
        var context = services.GetRequiredService<MovieWebDbContext>();
        var stripeService = services.GetRequiredService<IStripeService>();
        
        var plans = await context.SubscriptionPlans.ToListAsync();
        
        if (plans.Count == 0)
        {
            logger.LogInformation("🌱 Seeding default subscription plans...");
            var defaultPlans = new List<SubscriptionPlan>
            {
                new SubscriptionPlan { PlanName = "MoonPro_1M", DisplayName = "Premium 1 Tháng", Description = "Gói Premium dùng thử", PriceVND = 59000, PriceUSD = 2.36m, DurationMonths = 1, ActualMonths = 1, BonusMonths = 0, PlanType = "Premium", IsActive = true, IsPopular = false, DisplayOrder = 1, CreatedAt = DateTime.UtcNow },
                new SubscriptionPlan { PlanName = "MoonPro_6M", DisplayName = "Premium 6 Tháng", Description = "Gói Premium 6 tháng - Tặng 1 tháng", PriceVND = 295000, PriceUSD = 11.80m, DurationMonths = 6, ActualMonths = 7, BonusMonths = 1, PlanType = "Premium", IsActive = true, IsPopular = true, DisplayOrder = 2, CreatedAt = DateTime.UtcNow },
                new SubscriptionPlan { PlanName = "MoonPro_12M", DisplayName = "Premium 12 Tháng", Description = "Gói Premium 12 tháng - Tặng 1 tháng", PriceVND = 649000, PriceUSD = 25.96m, DurationMonths = 12, ActualMonths = 13, BonusMonths = 1, PlanType = "Premium", IsActive = true, IsPopular = false, DisplayOrder = 3, CreatedAt = DateTime.UtcNow },
                new SubscriptionPlan { PlanName = "MoonStu_1M", DisplayName = "Student 1 Tháng", Description = "Gói Student dùng thử", PriceVND = 39000, PriceUSD = 1.56m, DurationMonths = 1, ActualMonths = 1, BonusMonths = 0, PlanType = "Student", IsActive = true, IsPopular = false, DisplayOrder = 4, CreatedAt = DateTime.UtcNow },
                new SubscriptionPlan { PlanName = "MoonStu_6M", DisplayName = "Student 6 Tháng", Description = "Gói Student 6 tháng - Tặng 1 tháng", PriceVND = 195000, PriceUSD = 7.80m, DurationMonths = 6, ActualMonths = 7, BonusMonths = 1, PlanType = "Student", IsActive = true, IsPopular = true, DisplayOrder = 5, CreatedAt = DateTime.UtcNow },
                new SubscriptionPlan { PlanName = "MoonStu_12M", DisplayName = "Student 12 Tháng", Description = "Gói Student 12 tháng - Tặng 1 tháng", PriceVND = 429000, PriceUSD = 17.16m, DurationMonths = 12, ActualMonths = 13, BonusMonths = 1, PlanType = "Student", IsActive = true, IsPopular = false, DisplayOrder = 6, CreatedAt = DateTime.UtcNow }
            };
            await context.SubscriptionPlans.AddRangeAsync(defaultPlans);
            await context.SaveChangesAsync();
            plans = await context.SubscriptionPlans.ToListAsync();
        }

        bool needsSync = false;
        
        foreach (var plan in plans)
        {
            if (string.IsNullOrEmpty(plan.StripePriceId) || string.IsNullOrEmpty(plan.StripeProductId))
            {
                needsSync = true;
            }
            else if (!plan.StripePriceId.Contains("1TCm949wspvAXAKP"))
            {
                logger.LogWarning($"⚠️ Resetting stale Stripe Product/Price IDs for plan '{plan.DisplayName}' (old account detected).");
                plan.StripeProductId = null;
                plan.StripePriceId = null;
                needsSync = true;
            }
        }
        
        if (needsSync)
        {
            // Reset all users' StripeCustomerId because Stripe does not allow mixing USD and VND for a single customer
            logger.LogWarning("⚠️ Resetting all users' StripeCustomerIds due to currency change (USD -> VND).");
            var users = await context.Users.ToListAsync();
            foreach (var u in users)
            {
                u.StripeCustomerId = null;
            }
            
            await context.SaveChangesAsync();
            logger.LogInformation("🔄 Syncing subscription plans with the new Stripe account...");
            await stripeService.SyncSubscriptionPlansToStripeAsync();
            logger.LogInformation("✅ Stripe plans synchronized successfully!");
        }
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "❌ Failed to auto-sync Stripe plans on startup.");
    }
}

// ===== MIDDLEWARE CONFIGURATION =====
// ✅ Đăng ký Global Exception Handling Middleware (PHẢI ĐẶT ĐẦU TIÊN)
app.UseGlobalExceptionHandling();

if (!app.Environment.IsDevelopment())
{
    // app.UseExceptionHandler("/Home/Error"); // ❌ BỎ dòng này vì đã có middleware custom
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseStaticFiles(); 

// ===== STRIPE WEBHOOK RAW BODY MIDDLEWARE =====
app.Use(async (context, next) =>
{
    if (context.Request.Path.StartsWithSegments("/api/payment/webhook"))
    {
        context.Request.EnableBuffering();
    }
    await next();
});

app.UseRouting();
app.UseCors("AllowAll");
app.UseAuthentication();
app.UseAuthorization();

// ===== MAP HUBS & ENDPOINTS =====
app.MapHub<NotificationHub>("/notificationHub");

// ===== HANGFIRE DASHBOARD =====
app.UseHangfireDashboard("/hangfire", new DashboardOptions
{
    Authorization = new[] { new HangfireAuthorizationFilter() },
    DashboardTitle = "🌙 MoonPhim - Background Jobs Dashboard",
    StatsPollingInterval = 10000,
    DisplayStorageConnectionString = false
});

// ===== ĐĂNG KÝ RECURRING JOBS =====
RecurringJob.AddOrUpdate<PaymentReminderJob>(
    recurringJobId: "payment-reminder-daily",
    methodCall: job => job.Execute(),
    cronExpression: "0 0 * * *",
    options: new RecurringJobOptions
    {
        TimeZone = TimeZoneInfo.FindSystemTimeZoneById(
            OperatingSystem.IsWindows() ? "SE Asia Standard Time" : "Asia/Bangkok")
    }
);

RecurringJob.AddOrUpdate<SendRealtimeNotificationJob>(
    recurringJobId: "send-realtime-notifications",
    methodCall: job => job.ExecuteForAllUsers(),
    cronExpression: "*/5 * * * *",
    options: new RecurringJobOptions
    {
        TimeZone = TimeZoneInfo.FindSystemTimeZoneById(
            OperatingSystem.IsWindows() ? "SE Asia Standard Time" : "Asia/Bangkok")
    }
);
/*===== SITEMAP CACHE REFRESH JOB =====*/
MovieWeb.Jobs.SitemapCacheRefreshJob.ScheduleRecurringJob();

// ===== ROUTE CONFIGURATION =====
app.MapControllerRoute(
    name: "landing",
    pattern: "",
    defaults: new { controller = "Home", action = "Index" });

app.MapControllerRoute(
    name: "trangchu",
    pattern: "trang-chu",
    defaults: new { controller = "TrangChu", action = "TrangChu" });

app.MapControllerRoute(
    name: "admin",
    pattern: "Admin/{action=Dashboard}/{id?}",
    defaults: new { controller = "Admin" });

app.MapControllerRoute(
    name: "confirmEmail",
    pattern: "auth/confirm-email",
    defaults: new { controller = "Auth", action = "ConfirmEmail" });

app.MapControllerRoute(
    name: "resetPassword",
    pattern: "auth/reset-password",
    defaults: new { controller = "Auth", action = "ResetPassword" });

app.MapControllerRoute(
    name: "profile",
    pattern: "user/{action}",
    defaults: new { controller = "Profile", action = "Profile" });

app.MapControllerRoute(
    name: "movieDetail",
    pattern: "phim/{slug}",
    defaults: new { controller = "Movie", action = "Detail" });

app.MapControllerRoute(
    name: "search",
    pattern: "tim-kiem",
    defaults: new { controller = "Movie", action = "Search" });

app.MapControllerRoute(
    name: "category",
    pattern: "the-loai/{slug}", // ✅ SỬA: type -> slug
    defaults: new { controller = "Category", action = "Index" }); // ✅ SỬA: Movie -> Category

app.MapControllerRoute(
    name: "hoathinh",
    pattern: "phim/hoathinh/{page?}",
    defaults: new { controller = "Movie", action = "ByType", type = "hoathinh" });

app.MapControllerRoute(
    name: "series",
    pattern: "phim/series/{page?}",
    defaults: new { controller = "Movie", action = "ByType", type = "series" });

app.MapControllerRoute(
    name: "single",
    pattern: "phim/single/{page?}",
    defaults: new { controller = "Movie", action = "ByType", type = "single" });

app.MapControllerRoute(
    name: "nangCapTaiKhoan",
    pattern: "nang-cap",
    defaults: new { controller = "NangCap", action = "Index" });

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=TrangChu}/{action=TrangChu}/{id?}");

app.Run();

// ===== STRIPE SETTINGS CLASS =====
public class StripeSettings
{
    public string PublishableKey { get; set; } = string.Empty;
    public string SecretKey { get; set; } = string.Empty;
    public string WebhookSecret { get; set; } = string.Empty;
    public string Currency { get; set; } = "usd";
    public string SuccessUrl { get; set; } = string.Empty;
    public string CancelUrl { get; set; } = string.Empty;
    public string WebhookEndpoint { get; set; } = string.Empty;
}