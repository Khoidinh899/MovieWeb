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
using Microsoft.AspNetCore.SignalR; // <-- THÊM DÒNG NÀY
using StripeLib = Stripe;
using Hangfire;
using Hangfire.SqlServer;
using MovieWeb.Filters;
using MovieWeb.Jobs;
using SendGrid.Extensions.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);

// ✅ Load .env file - Ưu tiên .env.local cho local development
if (builder.Environment.IsDevelopment())
{
    if (File.Exists(".env.local"))
    {
        Env.Load(".env.local");
        Console.WriteLine("✅ Loaded .env.local");  // ← THÊM DÒNG NÀY
    }
    else if (File.Exists(".env"))
    {
        Env.Load();
        Console.WriteLine("⚠️ Loaded .env (fallback)");  // ← THÊM DÒNG NÀY
    }
}
else
{
    if (File.Exists(".env"))
    {
        Env.Load();
        Console.WriteLine("🌐 Loaded .env (production)");  // ← THÊM DÒNG NÀY
    }
}

builder.Configuration.AddEnvironmentVariables();

// ✅ THÊM ĐOẠN NÀY ĐỂ DEBUG
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
Console.WriteLine($"🔍 Environment: {builder.Environment.EnvironmentName}");
Console.WriteLine($"🗄️ Connection String: {connectionString?[..Math.Min(60, connectionString?.Length ?? 0)]}...");
Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");

// Kết nối DbContext với SQL Server
builder.Services.AddDbContext<MovieWebDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// Configure Identity
builder.Services.AddIdentity<User, Role>(options =>
{
    options.Password.RequiredLength = 6;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequireDigit = false;
    options.Password.RequireUppercase = false;
    options.Password.RequireLowercase = false;
    options.User.RequireUniqueEmail = true;
    
    // ✅ Chỉ yêu cầu xác thực email 1 lần duy nhất
    options.SignIn.RequireConfirmedEmail = true;
    options.SignIn.RequireConfirmedAccount = false;
    
    // ✅ Cấu hình token xác thực email
    options.Tokens.EmailConfirmationTokenProvider = TokenOptions.DefaultEmailProvider;
})
.AddEntityFrameworkStores<MovieWebDbContext>()
.AddDefaultTokenProviders();

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
    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
    options.Cookie.Name = "MoonPhim.Auth";
    
    // ⬇️ API trả JSON thay vì redirect
    options.Events.OnRedirectToLogin = context =>
    {
        if (context.Request.Path.StartsWithSegments("/api"))
        {
            context.Response.StatusCode = 401;
            return Task.CompletedTask;
        }
        context.Response.Redirect(context.RedirectUri);
        return Task.CompletedTask;
    };
    
    options.Events.OnRedirectToAccessDenied = context =>
    {
        if (context.Request.Path.StartsWithSegments("/api"))
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
    .UseSqlServerStorage(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        new SqlServerStorageOptions
        {
            CommandBatchMaxTimeout = TimeSpan.FromMinutes(5),
            SlidingInvisibilityTimeout = TimeSpan.FromMinutes(5),
            QueuePollInterval = TimeSpan.Zero,
            UseRecommendedIsolationLevel = true,
            DisableGlobalLocks = true,
            SchemaName = "hangfire"
        }
    ));

builder.Services.AddHangfireServer(options =>
{
    options.WorkerCount = 2;
    options.ServerName = "MoonPhim-BackgroundServer";
});

// Register Hangfire Jobs
builder.Services.AddScoped<PaymentReminderJob>();
//  SignalR Job
builder.Services.AddScoped<SendRealtimeNotificationJob>();

// // ===== EMAIL SETTINGS =====
// builder.Services.Configure<EmailSettings>(builder.Configuration.GetSection("EmailSettings"));
// builder.Services.AddScoped<IEmailService, EmailService>();
// builder.Services.AddScoped<IStudentEmailService, StudentEmailService>();

// ===== SENDGRID EMAIL SENDER =====
// ===== CẤU HÌNH SENDGRID MỚI (ĐÃ SỬA LỖI) =====

// 1. Đăng ký lớp SendGridOptions để đọc cấu hình từ .env
builder.Services.Configure<SendGridOptions>(builder.Configuration.GetSection("SendGrid"));

// 2. Đăng ký SendGrid client
builder.Services.AddSendGrid(options =>
{
    // Tự động đọc API Key từ file .env (SendGrid__ApiKey)
    options.ApiKey = builder.Configuration.GetSection("SendGrid")["ApiKey"];
});

// Dòng này cho Identity UI (như 'Quên mật khẩu')
builder.Services.AddTransient<Microsoft.AspNetCore.Identity.UI.Services.IEmailSender, SendGridEmailSender>();

// Dòng này cho Identity Core (dùng lớp "User" của bạn)
builder.Services.AddTransient<IEmailSender<User>, SendGridEmailSender>();

// DÒNG NÀY SỬA LỖI CRASH:
// Dòng này cung cấp IEmailService cho AuthService của bạn
builder.Services.AddTransient<IEmailService, SendGridEmailSender>();
// Dòng này cung cấp IStudentEmailService cho StudentEmailService của bạn
builder.Services.AddScoped<IStudentEmailService, StudentEmailService>();

// ===== STRIPE CONFIGURATION =====
builder.Services.Configure<StripeSettings>(builder.Configuration.GetSection("StripeSettings"));
StripeLib.StripeConfiguration.ApiKey = builder.Configuration["StripeSettings:SecretKey"];

// ===== CORE SERVICES =====
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IProfileService, ProfileService>();

// ===== NOTIFICATION SERVICE ===== 
builder.Services.AddScoped<INotificationService, NotificationService>();

// ===== SUBSCRIPTION SERVICES =====
builder.Services.AddScoped<IStripeService, StripeService>();
builder.Services.AddScoped<ISubscriptionService, SubscriptionService>();

// ===== MOVIE SERVICES =====
builder.Services.AddScoped<IOPhimService, OPhimService>();
//builder.Services.AddScoped<IMovieSyncService, MovieSyncService>();
builder.Services.AddScoped<IMovieRepository, MovieRepository>();
builder.Services.AddHostedService<BackgroundSyncService>();
builder.Services.AddScoped<ICategorySyncService, CategorySyncService>();
builder.Services.AddScoped<ICountrySyncService, CountrySyncService>();
builder.Services.AddScoped<IActorSyncService, ActorSyncService>();
builder.Services.AddScoped<IDirectorSyncService, DirectorSyncService>();
// == Hàm BackfillService chỉ chạy thủ công khi cần sửa lỗi tập phim, đạo diễn, quốc gia, diễn viên, thể loại ==
// builder.Services.AddHostedService<BackfillService>();

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
// Đăng ký RecommendationService
builder.Services.AddScoped<IRecommendationService,RecommendationService>();
// Đăng ký GeminiService
builder.Services.AddHttpClient<IGeminiService, GeminiService>();
// Đăng ký MovieRequestService
builder.Services.AddScoped<IMovieRequestService, MovieRequestService>();
// Thêm dịch vụ AntiForgery và cấu hình nó
builder.Services.AddAntiforgery(options =>
{
    // Bảo nó tìm token trong header tên là "RequestVerificationToken"
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

var app = builder.Build();

// ===== AUTO MIGRATE DATABASE =====
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var logger = services.GetRequiredService<ILogger<Program>>();

    try
    {
        var context = services.GetRequiredService<MovieWebDbContext>();

        logger.LogInformation("🔄 Checking for pending database migrations...");

        // Kiểm tra xem có migrations chưa apply không
        var pendingMigrations = context.Database.GetPendingMigrations();

        if (pendingMigrations.Any())
        {
            logger.LogWarning($"⚠️ Found {pendingMigrations.Count()} pending migrations. Applying...");

            // Apply migrations
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

        // KHÔNG throw exception để app vẫn có thể start
        // Nhưng ghi log để debug
        logger.LogError("⚠️ App will continue but database may not be in sync!");
    }
}

// ===== MIDDLEWARE CONFIGURATION =====
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

//app.UseHttpsRedirection();
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
        TimeZone = TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time")
    }
);
// Gửi Realtime Notifications (Chạy mỗi 5 phút)
RecurringJob.AddOrUpdate<SendRealtimeNotificationJob>(
    recurringJobId: "send-realtime-notifications",
    methodCall: job => job.ExecuteForAllUsers(),
    cronExpression: "*/5 * * * *", // Mỗi 5 phút
    options: new RecurringJobOptions
    {
        TimeZone = TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time")
    }
);

// ===== ROUTE CONFIGURATION =====

// 🎯 Landing Page
app.MapControllerRoute(
    name: "landing",
    pattern: "",
    defaults: new { controller = "Home", action = "Index" });

// 🎯 Trang chủ chính
app.MapControllerRoute(
    name: "trangchu",
    pattern: "trang-chu",
    defaults: new { controller = "TrangChu", action = "TrangChu" });

// Admin routes
app.MapControllerRoute(
    name: "admin",
    pattern: "Admin/{action=Dashboard}/{id?}",
    defaults: new { controller = "Admin" });

// Auth routes
app.MapControllerRoute(
    name: "confirmEmail",
    pattern: "auth/confirm-email",
    defaults: new { controller = "Auth", action = "ConfirmEmail" });

app.MapControllerRoute(
    name: "resetPassword",
    pattern: "auth/reset-password",
    defaults: new { controller = "Auth", action = "ResetPassword" });

// Profile routes
app.MapControllerRoute(
    name: "profile",
    pattern: "user/{action}",
    defaults: new { controller = "Profile", action = "Profile" });

// Movie routes
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
    pattern: "the-loai/{type}",
    defaults: new { controller = "Movie", action = "Category" });

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

// Nâng cấp tài khoản
app.MapControllerRoute(
    name: "nangCapTaiKhoan",
    pattern: "nang-cap",
    defaults: new { controller = "NangCap", action = "Index" });

// Default route
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