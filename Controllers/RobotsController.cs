using Microsoft.AspNetCore.Mvc;

namespace MovieWeb.Controllers
{
    public class RobotsController : Controller
    {
        private readonly IWebHostEnvironment _env;

        public RobotsController(IWebHostEnvironment env)
        {
            _env = env;
        }

        [Route("robots.txt")]
        [ResponseCache(Duration = 86400, Location = ResponseCacheLocation.Any)] // Cache 24 giờ
        public IActionResult RobotsTxt()
        {
            var robotsPath = Path.Combine(_env.WebRootPath, "robots.txt");
            
            if (System.IO.File.Exists(robotsPath))
            {
                var content = System.IO.File.ReadAllText(robotsPath);
                return Content(content, "text/plain");
            }

            // Fallback nếu file không tồn tại
            var fallbackContent = @"User-agent: *
Allow: /
Disallow: /Admin/
Disallow: /api/

Sitemap: https://moonphim.me/sitemap.xml";

            return Content(fallbackContent, "text/plain");
        }
    }
}
