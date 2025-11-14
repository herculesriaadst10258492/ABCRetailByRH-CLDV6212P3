using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using ABCRetailByRH.Data;
using ABCRetailByRH.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ABCRetailByRH.Controllers
{
    public class LoginController : Controller
    {
        private readonly AuthDbContext _db;
        private const string SessionUserKey = "UserName";
        private const string SessionRoleKey = "UserRole";

        public LoginController(AuthDbContext db) => _db = db;

        [HttpGet]
        public IActionResult Index() => RedirectToAction(nameof(Login));

        [HttpGet]
        public IActionResult Login() => View(new LoginViewModel());

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel vm)
        {
            if (!ModelState.IsValid) return View(vm);

            var user = await _db.Users.FirstOrDefaultAsync(u => u.Username == vm.Username);

            if (user == null || user.PasswordHash != Hash(vm.Password))
            {
                ModelState.AddModelError("", "Invalid credentials.");
                return View(vm);
            }

            HttpContext.Session.SetString(SessionUserKey, user.Username);
            HttpContext.Session.SetString(SessionRoleKey, user.Role);

            TempData["Msg"] = $"Welcome, {user.Username}!";

            return user.Role == "Admin"
                ? RedirectToAction("AdminDashboard", "Home")
                : RedirectToAction("CustomerDashboard", "Home");
        }

        [HttpGet]
        public IActionResult Register() => View(new RegisterViewModel());

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(RegisterViewModel vm)
        {
            if (!ModelState.IsValid) return View(vm);

            var exists = await _db.Users.AnyAsync(u => u.Username == vm.Username);
            if (exists)
            {
                ModelState.AddModelError(nameof(vm.Username), "Username already exists.");
                return View(vm);
            }

            var user = new AppUser
            {
                Username = vm.Username.Trim(),
                Email = vm.Email.Trim(),
                Phone = vm.Phone.Trim(),
                PasswordHash = Hash(vm.Password),
                Role = vm.Role
            };

            _db.Users.Add(user);
            await _db.SaveChangesAsync();

            TempData["Msg"] = "Registration successful. Please log in.";
            return RedirectToAction(nameof(Login));
        }

        [HttpPost, ValidateAntiForgeryToken]
        public IActionResult Logout()
        {
            HttpContext.Session.Remove(SessionUserKey);
            HttpContext.Session.Remove(SessionRoleKey);
            TempData["Msg"] = "You have been signed out.";
            return RedirectToAction("Index", "Home");
        }

        private static string Hash(string input)
        {
            using var sha = SHA256.Create();
            var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(input ?? ""));
            return Convert.ToHexString(bytes);
        }

        public static string? CurrentUser(HttpContext ctx) => ctx.Session.GetString(SessionUserKey);
        public static string? CurrentRole(HttpContext ctx) => ctx.Session.GetString(SessionRoleKey);
    }
}
