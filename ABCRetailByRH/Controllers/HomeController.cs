using ABCRetailByRH.Models;
using ABCRetailByRH.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Linq;

namespace ABCRetailByRH.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly IAzureStorageService _svc;

        private const string SessionRoleKey = "UserRole";

        public HomeController(ILogger<HomeController> logger, IAzureStorageService svc)
        {
            _logger = logger;
            _svc = svc;
        }

        public IActionResult Index()
        {
            var all = _svc.GetAllProducts();

            // Shuffle randomly on every request
            var featured = all
                .OrderBy(x => Guid.NewGuid())   // randomize
                .Take(3)                        // pick 4 items only
                .ToList();

            return View(new HomeViewModel
            {
                FeaturedProducts = featured
            });
        }


        [HttpGet] public IActionResult Contact() => View();
        public IActionResult Privacy() => View();

        public IActionResult AdminDashboard()
        {
            if (HttpContext.Session.GetString(SessionRoleKey) != "Admin")
                return RedirectToAction("Login", "Login");
            return View();
        }

        public IActionResult CustomerDashboard()
        {
            // Only logged-in customers
            var role = HttpContext.Session.GetString(SessionRoleKey);
            if (string.IsNullOrWhiteSpace(role)) return RedirectToAction("Login", "Login");
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
            => View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
