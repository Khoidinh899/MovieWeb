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
    options.SignIn.RequireConfirmedEmail = true;
    options.SignIn.RequireConfirmedAccount = true;
})
.AddEntityFrameworkStores<MovieWebDbContext>()
.AddDefaultTokenProviders();

// Configure cookie
builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Auth/Login";
    options.LogoutPath = "/Auth/Logout";
    options.AccessDeniedPath = "/Auth/AccessDenied";
    options.ExpireTimeSpan = TimeSpan.FromDays(30);
    options.SlidingExpiration = true;
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true; // Quan trọng
    options.Cookie.SameSite = SameSiteMode.Lax; // Thêm dòng này
    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
});

// JWT
builder.Services.AddAuthentication()
    .AddJwtBearer(options =>
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

// Add services to the container
builder.Services.AddControllersWithViews();
builder.Services.AddHttpClient();
builder.Services.AddScoped<IOPhimService, OPhimService>();
builder.Services.AddScoped<IMovieSyncService, MovieSyncService>();
builder.Services.AddScoped<IMovieRepository, MovieRepository>();
builder.Services.AddHostedService<BackgroundSyncService>();
builder.Services.AddMemoryCache();

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

app.MapControllerRoute(
    name: "auth",
    pattern: "auth/{action}",
    defaults: new { controller = "Auth" });

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

app.Run();