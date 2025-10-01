using MovieWeb.Services;
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

var builder = WebApplication.CreateBuilder(args);

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
    options.SignIn.RequireConfirmedAccount = false; // Không cần xác nhận account mỗi lần đăng nhập
    
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
    options.ExpireTimeSpan = TimeSpan.FromDays(30); // Cookie tồn tại 30 ngày
    options.SlidingExpiration = true; // Tự động gia hạn cookie
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
    options.Cookie.Name = "MoonPhim.Auth"; // Đặt tên cookie
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

// Email service
builder.Services.Configure<EmailSettings>(builder.Configuration.GetSection("EmailSettings"));
builder.Services.AddScoped<IEmailService, EmailService>();

// Auth service
builder.Services.AddScoped<IAuthService, AuthService>();

// Profile service
builder.Services.AddScoped<IProfileService, ProfileService>();

// Add services to the container
builder.Services.AddControllersWithViews();
builder.Services.AddHttpClient();
builder.Services.AddScoped<IOPhimService, OPhimService>();
builder.Services.AddScoped<IMovieSyncService, MovieSyncService>();
builder.Services.AddScoped<IMovieRepository, MovieRepository>();
builder.Services.AddHostedService<BackgroundSyncService>();
builder.Services.AddMemoryCache();

// Load .env
Env.Load();
builder.Configuration.AddEnvironmentVariables();
builder.Services.AddHttpContextAccessor();

var app = builder.Build();

// Middleware
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

// Routes
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=TrangChu}/{action=TrangChu}/{id?}");


// Auth routes
app.MapControllerRoute(
    name: "confirmEmail",
    pattern: "auth/confirm-email",
    defaults: new { controller = "Auth", action = "ConfirmEmail" });

app.MapControllerRoute(
    name: "resetPassword",
    pattern: "auth/reset-password",
    defaults: new { controller = "Auth", action = "ResetPassword" });

// Route cho trang chi tiết phim
app.MapControllerRoute(
    name: "movieDetail",
    pattern: "phim/{slug}",
    defaults: new { controller = "Movie", action = "Detail" });

// Route cho trang profile
app.MapControllerRoute(
    name: "profile",
    pattern: "tai-khoan/{action}",
    defaults: new { controller = "Profile", action = "Index" });

app.MapControllerRoute(
    name: "adminManageUsers",
    pattern: "admin/nguoi-dung/{action}/{id?}",
    defaults: new { controller = "Profile", action = "ManageUsers" });
// Route cho tìm kiếm
app.MapControllerRoute(
    name: "search",
    pattern: "tim-kiem",
    defaults: new { controller = "Movie", action = "Search" });

// Route cho danh sách theo thể loại
app.MapControllerRoute(
    name: "category",
    pattern: "the-loai/{type}",
    defaults: new { controller = "Movie", action = "Category" });

// Route cho danh sách phim hoạt hình
app.MapControllerRoute(
    name: "hoathinh",
    pattern: "phim/hoathinh/{page?}",
    defaults: new { controller = "Movie", action = "ByType", type = "hoathinh" });

// Route cho danh sách phim bộ
app.MapControllerRoute(
    name: "series",
    pattern: "phim/series/{page?}",
    defaults: new { controller = "Movie", action = "ByType", type = "series" });

// Route cho danh sách phim lẻ
app.MapControllerRoute(
    name: "single",
    pattern: "phim/single/{page?}",
    defaults: new { controller = "Movie", action = "ByType", type = "single" });

app.Run();