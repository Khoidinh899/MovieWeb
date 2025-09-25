using MovieWeb.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();

// Thêm HttpClient
builder.Services.AddHttpClient();

// Đăng ký OPhimService
builder.Services.AddScoped<IOPhimService, OPhimService>();

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

app.Run();