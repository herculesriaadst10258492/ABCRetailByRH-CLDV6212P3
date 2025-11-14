using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ABCRetailByRH.Data;
using ABCRetailByRH.Models;
using ABCRetailByRH.ViewModels;
using ABCRetailByRH.Services;
using ABCRetailByRH.Filters;

namespace ABCRetailByRH.Controllers
{
    [SessionAuthorize(Role = "Customer")]
    public class CartController : Controller
    {
        private readonly AuthDbContext _db;
        private readonly IAzureStorageService _store;
        private readonly IFunctionsClient _fx;

        private const string SessionUserKey = "UserName";
        private const string DefaultProductPK = "PRODUCTS";

        public CartController(AuthDbContext db, IAzureStorageService store, IFunctionsClient fx)
        {
            _db = db;
            _store = store;
            _fx = fx;
        }

        private string CurrentUser() =>
            HttpContext.Session.GetString(SessionUserKey) ?? "";

        // GET: /Cart
        public async Task<IActionResult> Index()
        {
            var username = CurrentUser();

            var items = await _db.Cart
                .Where(c => c.CustomerUsername == username)
                .ToListAsync();

            double subtotal = items.Sum(i => i.UnitPrice * i.Quantity);
            double vat = System.Math.Round(subtotal * 0.15, 2);
            double total = subtotal + vat;

            return View(new CartViewModel
            {
                Items = items.Select(i => new CartItemViewModel
                {
                    Id = i.Id,                    // 🔥 ADDED
                    ProductId = i.ProductId,
                    Name = i.ProductName,
                    UnitPrice = i.UnitPrice,
                    Quantity = i.Quantity,
                    ImageUrl = i.ImageUrl
                }).ToList(),
                Subtotal = subtotal,
                VAT = vat,
                GrandTotal = total,
                User = username
            });
        }

        // POST: /Cart/Add
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Add(string id, int qty = 1)
        {
            var username = CurrentUser();
            if (string.IsNullOrWhiteSpace(username))
                return RedirectToAction("Login", "Login");

            var p = _store.GetProduct(DefaultProductPK, id);
            if (p == null)
                return NotFound();

            var existing = await _db.Cart
                .FirstOrDefaultAsync(c => c.CustomerUsername == username && c.ProductId == id);

            if (existing == null)
            {
                _db.Cart.Add(new CartItem
                {
                    CustomerUsername = username,
                    ProductId = id,
                    ProductName = p.Name,
                    UnitPrice = p.Price,
                    Quantity = qty,
                    ImageUrl = p.ImageUrl
                });
            }
            else
            {
                existing.Quantity += qty;
            }

            await _db.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        // POST: /Cart/UpdateQty
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateQty(int id, int qty)
        {
            var item = await _db.Cart.FindAsync(id);

            if (item != null)
            {
                if (qty <= 0)
                    _db.Cart.Remove(item);
                else
                    item.Quantity = qty;

                await _db.SaveChangesAsync();
            }

            return RedirectToAction(nameof(Index));
        }

        // POST: /Cart/Remove
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Remove(int id)
        {
            var item = await _db.Cart.FindAsync(id);
            if (item != null)
            {
                _db.Cart.Remove(item);
                await _db.SaveChangesAsync();
            }

            return RedirectToAction(nameof(Index));
        }

        // POST: /Cart/Clear
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Clear()
        {
            var username = CurrentUser();
            var items = _db.Cart.Where(c => c.CustomerUsername == username);

            _db.Cart.RemoveRange(items);
            await _db.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }

        // POST: /Cart/Checkout
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Checkout()
        {
            var username = CurrentUser();
            var items = await _db.Cart.Where(c => c.CustomerUsername == username).ToListAsync();

            if (items.Count == 0)
                return RedirectToAction(nameof(Index));

            double subtotal = items.Sum(i => i.UnitPrice * i.Quantity);
            double vat = System.Math.Round(subtotal * 0.15, 2);
            double total = subtotal + vat;

            var payload = System.Text.Json.JsonSerializer.Serialize(new
            {
                OrderId = (string?)null,
                Customer = username,
                Total = total,
                Items = items.Select(i => new
                {
                    i.ProductId,
                    i.ProductName,
                    i.UnitPrice,
                    i.Quantity
                })
            });

            await _fx.EnqueueRawAsync(payload);

            // Clear SQL cart
            _db.Cart.RemoveRange(items);
            await _db.SaveChangesAsync();

            return RedirectToAction(nameof(Confirmation));
        }

        public IActionResult Confirmation() => View();
    }
}
