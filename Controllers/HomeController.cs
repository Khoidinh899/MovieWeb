using Microsoft.AspNetCore.Mvc;

namespace MovieWeb.Controllers
{
    public class HomeController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}