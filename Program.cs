using MovieWeb.Services;
using MovieWeb.Repositories; // Thêm using này
using Microsoft.EntityFrameworkCore;
using MovieWeb.Models.Entities;
using MovieWeb.Data;

var builder = WebApplication.CreateBuilder(args);

// Kết nối DbContext với SQL Server
builder.Services.AddDbContext<MovieWebDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// Add services to the container.
builder.Services.AddControllersWithViews();

// Thêm HttpClient
builder.Services.AddHttpClient();

// Đăng ký Services
builder.Services.AddScoped<IOPhimService, OPhimService>();
builder.Services.AddScoped<IMovieSyncService, MovieSyncService>();

// Đăng ký Repositories - THÊM DÒNG NÀY
builder.Services.AddScoped<IMovieRepository, MovieRepository>();

// Đăng ký BackgroundSyncService
builder.Services.AddHostedService<BackgroundSyncService>();

// Thêm Memory Cache để cache dữ liệu
builder.Services.AddMemoryCache();

var app = builder.Build();

// Configure the HTTP request pipeline.
// if (!app.Environment.IsDevelopment())
// {
//     app.UseExceptionHandler("/Home/Error");
//     // The default HSTS value is 30 days. You may want to change this for production scenarios.
//     app.UseHsts();
// }

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=TrangChu}/{action=TrangChu}/{id?}");

// Route cho trang chi tiết phim
app.MapControllerRoute(
    name: "movieDetail",
    pattern: "phim/{slug}",
    defaults: new { controller = "Movie", action = "Detail" });

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