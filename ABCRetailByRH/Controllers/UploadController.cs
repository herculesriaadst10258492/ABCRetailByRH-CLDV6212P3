using Microsoft.AspNetCore.Mvc;
using ABCRetailByRH.Services;
using Microsoft.AspNetCore.Http;
using System.Linq;
using System.Collections.Generic;
using ABCRetailByRH.Models;

namespace ABCRetailByRH.Controllers
{
    public class UploadController : Controller
    {
        private readonly IAzureStorageService _svc;

        public UploadController(IAzureStorageService svc)
        {
            _svc = svc;
        }

        // ================================================================
        // CUSTOMER PAGE (UPLOAD PROOF OF PAYMENT)
        // ================================================================
        [HttpGet]
        public IActionResult Index(string? orderId = null)
        {
            var user = HttpContext.Session.GetString("UserName");

            if (string.IsNullOrWhiteSpace(user))
                return RedirectToAction("Login", "Login");

            // Load orders for dropdown
            var orders = _svc.ListOrdersByCustomer(user)
                             .GroupBy(o => o.OrderId)
                             .Select(g => new
                             {
                                 OrderId = g.Key,
                                 Total = g.Sum(x => (double)x.TotalPrice),
                                 Date = g.First().CreatedAtUtc
                             })
                             .OrderByDescending(x => x.Date)
                             .ToList();

            ViewBag.Orders = orders;
            ViewBag.SelectedOrderId = orderId;
            ViewBag.Message = TempData["Message"];

            // ============================================================
            // FIXED: Load ONLY PoP files for the selected order (Option B)
            // ============================================================

            var customerFiles = new List<string>();

            if (!string.IsNullOrWhiteSpace(orderId))
            {
                var rows = _svc.ListOrdersByCustomer(user)
                               .Where(o => o.OrderId == orderId &&
                                           !string.IsNullOrWhiteSpace(o.ContractFileName))
                               .ToList();

                foreach (var r in rows)
                {
                    if (!string.IsNullOrWhiteSpace(r.ContractFileName))
                        customerFiles.Add(r.ContractFileName);
                }
            }

            return View(customerFiles);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Index(IFormFile file, string? orderId)
        {
            var user = HttpContext.Session.GetString("UserName");

            if (string.IsNullOrWhiteSpace(user))
                return RedirectToAction("Login", "Login");

            if (file == null || file.Length == 0)
            {
                TempData["Message"] = "Choose a file.";
                return RedirectToAction(nameof(Index));
            }

            if (string.IsNullOrWhiteSpace(orderId))
            {
                TempData["Message"] = "Select an order.";
                return RedirectToAction(nameof(Index));
            }

            // Attach PoP to ALL rows in the order
            var allRows = _svc.ListOrdersByCustomer(user)
                              .Where(o => o.OrderId == orderId)
                              .ToList();

            if (!allRows.Any())
            {
                TempData["Message"] = "Order not found.";
                return RedirectToAction(nameof(Index));
            }

            foreach (var row in allRows)
                _svc.AttachProofOfPayment(row.RowKey, file);

            TempData["Message"] = "Proof of payment uploaded.";
            return RedirectToAction(nameof(Index), new { orderId });
        }

        // ================================================================
        // CUSTOMER DOWNLOAD
        // ================================================================
        [HttpGet]
        public IActionResult Download(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return BadRequest();

            var bytes = _svc.DownloadFile(name, out string contentType);
            if (bytes == null || bytes.Length == 0)
                return NotFound();

            return File(bytes,
                        string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType,
                        name);
        }

        // ================================================================
        // CUSTOMER DELETE (DISABLED)
        // ================================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Delete(string name)
        {
            TempData["Message"] = "Deleting payment proofs is restricted.";
            return RedirectToAction(nameof(Index));
        }

        // ================================================================
        // DETAILS PLACEHOLDER
        // ================================================================
        [HttpGet]
        public IActionResult Details(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return NotFound();
            ViewBag.Name = name;
            return View(model: $"Details not implemented for '{name}' yet.");
        }

        // ================================================================
        // ADMIN VIEW ALL PROOF OF PAYMENTS
        // ================================================================
        [HttpGet]
        public IActionResult Admin()
        {
            var role = HttpContext.Session.GetString("UserRole");
            if (!string.Equals(role, "Admin", System.StringComparison.OrdinalIgnoreCase))
                return RedirectToAction("Login", "Login");

            var rawFiles = _svc.ListFiles();
            var allOrders = _svc.ListOrders();

            var poPFiles = new List<(string FileName, string OrderId, string Customer)>();

            foreach (var f in rawFiles)
            {
                if (!f.StartsWith("proof-")) continue;

                var parts = f.Split('-');
                if (parts.Length < 3) continue;

                var rowKey = parts[1];

                var row = allOrders.FirstOrDefault(o => o.RowKey == rowKey);
                if (row != null)
                {
                    poPFiles.Add((
                        FileName: f,
                        OrderId: row.OrderId,
                        Customer: row.Username ?? ""
                    ));
                }
            }

            ViewBag.Message = TempData["Message"];
            return View(poPFiles);
        }

        // ================================================================
        // ADMIN DELETE POP
        // ================================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult AdminDelete(string name, string orderId)
        {
            var role = HttpContext.Session.GetString("UserRole");
            if (!string.Equals(role, "Admin", System.StringComparison.OrdinalIgnoreCase))
                return RedirectToAction("Login", "Login");

            if (!string.IsNullOrWhiteSpace(name) && !string.IsNullOrWhiteSpace(orderId))
            {
                _svc.DeleteFile(name);

                var allRows = _svc.ListOrders()
                                  .Where(o => o.OrderId == orderId)
                                  .ToList();

                foreach (var row in allRows)
                {
                    row.ContractFileName = null;
                    row.ContractOriginalFileName = null;
                    row.ContractContentType = null;

                    _svc.UpdateOrderStatus(row.RowKey, row.Status);
                }

                TempData["Message"] = "Payment proof deleted.";
            }

            return RedirectToAction(nameof(Admin));
        }
    }
}
