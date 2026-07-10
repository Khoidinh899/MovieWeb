using Microsoft.AspNetCore.Mvc;

namespace MovieWeb.Controllers
{
    public class HomeController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }

        [Route("about")]
        public IActionResult About()
        {
            return View();
        }

        [Route("privacy")]
        public IActionResult Privacy()
        {
            return View();
        }

        [Route("contact")]
        public IActionResult Contact()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error(int? statusCode = null)
        {
            ViewBag.StatusCode = statusCode;
            ViewBag.ErrorMessage = HttpContext.Items["ErrorMessage"]?.ToString();
            
            return View("~/Views/Shared/Error.cshtml", new MovieWeb.Controllers.ErrorViewModel
            {
                RequestId = System.Diagnostics.Activity.Current?.Id ?? HttpContext.TraceIdentifier
            });
        }
    }

    public class ErrorViewModel
    {
        public string? RequestId { get; set; }
        public bool ShowRequestId => !string.IsNullOrEmpty(RequestId);
    }
}