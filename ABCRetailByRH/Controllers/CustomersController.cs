using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ABCRetailByRH.Data;
using ABCRetailByRH.Models;

namespace ABCRetailByRH.Controllers
{
    public class CustomersController : Controller
    {
        private readonly AuthDbContext _db;

        public CustomersController(AuthDbContext db)
        {
            _db = db;
        }

        // ============================================================
        // LIST ALL CUSTOMERS
        // ============================================================
        public async Task<IActionResult> Index()
        {
            var users = await _db.Users
                .OrderBy(u => u.Username)
                .ToListAsync();

            return View(users);
        }

        // ============================================================
        // DETAILS
        // ============================================================
        public async Task<IActionResult> Details(int id)
        {
            var user = await _db.Users.FindAsync(id);
            if (user == null)
                return NotFound();

            return View(user);
        }

        // ============================================================
        // CREATE CUSTOMER
        // ============================================================
        [HttpGet]
        public IActionResult Create()
        {
            return View(new AppUser());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(
            [Bind("Username,Email,Phone,PasswordHash,Role")] AppUser user)
        {
            if (!ModelState.IsValid)
                return View(user);

            _db.Users.Add(user);
            await _db.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }

        // ============================================================
        // EDIT CUSTOMER
        // ============================================================
        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var user = await _db.Users.FindAsync(id);
            if (user == null)
                return NotFound();

            return View(user);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(
            int id,
            [Bind("Id,Username,Email,Phone,Role,PasswordHash")] AppUser user)
        {
            if (id != user.Id)
                return NotFound();

            if (!ModelState.IsValid)
                return View(user);

            var existing = await _db.Users.FirstOrDefaultAsync(u => u.Id == id);
            if (existing == null)
                return NotFound();

            // Apply edited values
            existing.Username = user.Username.Trim();
            existing.Email = user.Email.Trim();
            existing.Phone = user.Phone.Trim();
            existing.Role = user.Role;

            // Attach and explicitly mark modified fields
            _db.Attach(existing);
            _db.Entry(existing).Property(x => x.Username).IsModified = true;
            _db.Entry(existing).Property(x => x.Email).IsModified = true;
            _db.Entry(existing).Property(x => x.Phone).IsModified = true;
            _db.Entry(existing).Property(x => x.Role).IsModified = true;

            // Prevent password modification
            _db.Entry(existing).Property(x => x.PasswordHash).IsModified = false;

            await _db.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }

        // ============================================================
        // DELETE CUSTOMER
        // ============================================================
        [HttpGet]
        public async Task<IActionResult> Delete(int id)
        {
            var user = await _db.Users.FindAsync(id);
            if (user == null)
                return NotFound();

            return View(user);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var user = await _db.Users.FindAsync(id);
            if (user != null)
            {
                _db.Users.Remove(user);
                await _db.SaveChangesAsync();
            }

            return RedirectToAction(nameof(Index));
        }
    }
}
