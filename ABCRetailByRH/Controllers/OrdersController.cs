using System;
using System.Collections.Generic;
using System.Linq;
using ABCRetailByRH.Filters;
using ABCRetailByRH.Models;
using ABCRetailByRH.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace ABCRetailByRH.Controllers
{
    public class OrdersController : Controller
    {
        private readonly IAzureStorageService _store;
        private const string SessionUserKey = "UserName";
        private const string SessionRoleKey = "UserRole";

        public OrdersController(IAzureStorageService store) => _store = store;

        private string? CurrentUser() => HttpContext.Session.GetString(SessionUserKey);
        private string? CurrentRole() => HttpContext.Session.GetString(SessionRoleKey);

        // ==============================================================
        // GROUPED ORDER OBJECT
        // ==============================================================
        public class GroupedOrderVm
        {
            public string OrderId { get; set; } = "";
            public string Username { get; set; } = "";
            public DateTime CreatedAtUtc { get; set; }
            public string Status { get; set; } = "";
            public int TotalItems { get; set; }
            public double TotalAmount { get; set; }
        }

        // ==============================================================
        // ORDER DETAILS VM
        // ==============================================================
        public class OrderDetailsVm
        {
            public string OrderId { get; set; } = "";
            public string Customer { get; set; } = "";
            public string Status { get; set; } = "";
            public DateTime CreatedAtUtc { get; set; }
            public List<Order> Items { get; set; } = new();
            public double TotalAmount { get; set; }

            public string? ContractFileName { get; set; }
            public string? ContractOriginalName { get; set; }
            public string? ContractContentType { get; set; }
        }

        // ==============================================================
        // CUSTOMER: MY ORDERS (GROUPED)
        // ==============================================================
        [SessionAuthorize(Role = "Customer")]
        public IActionResult MyOrders(string? status = null)
        {
            var user = CurrentUser();
            if (string.IsNullOrWhiteSpace(user))
                return RedirectToAction("Login", "Login");

            var lines = _store.ListOrdersByCustomer(user, status)
                              .OrderByDescending(o => o.CreatedAtUtc)
                              .ToList();

            var grouped = lines
                .GroupBy(o => o.OrderId)
                .Select(g => new GroupedOrderVm
                {
                    OrderId = g.Key,
                    Username = user,
                    CreatedAtUtc = g.First().CreatedAtUtc,
                    Status = g.First().Status ?? "Submitted",
                    TotalItems = g.Sum(x => x.Quantity),
                    TotalAmount = g.Sum(x => (double)x.TotalPrice)
                })
                .OrderByDescending(o => o.CreatedAtUtc)
                .ToList();

            return View(grouped);
        }

        // ==============================================================
        // ADMIN: MANAGE ALL ORDERS (GROUPED)
        // ==============================================================
        [SessionAuthorize(Role = "Admin")]
        public IActionResult Manage(string? status = null)
        {
            if (CurrentRole() != "Admin")
                return RedirectToAction("Login", "Login");

            var lines = _store.ListOrders(status)
                              .OrderByDescending(o => o.CreatedAtUtc)
                              .ToList();

            var grouped = lines
                .GroupBy(o => o.OrderId)
                .Select(g => new GroupedOrderVm
                {
                    OrderId = g.Key,
                    Username = g.First().Username ?? "",
                    CreatedAtUtc = g.First().CreatedAtUtc,
                    Status = g.First().Status ?? "Submitted",
                    TotalItems = g.Sum(x => x.Quantity),
                    TotalAmount = g.Sum(x => (double)x.TotalPrice)
                })
                .OrderByDescending(o => o.CreatedAtUtc)
                .ToList();

            ViewBag.Status = status;
            return View(grouped);
        }

        // ==============================================================
        // ADMIN: UPDATE STATUS
        // ==============================================================
        [SessionAuthorize(Role = "Admin")]
        [HttpPost, ValidateAntiForgeryToken]
        public IActionResult UpdateStatus(string id, string status)
        {
            if (CurrentRole() != "Admin")
                return RedirectToAction("Login", "Login");

            if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(status))
                return BadRequest();

            var allowed = new[] { "Submitted", "Processing", "Completed", "Cancelled" };
            if (!allowed.Contains(status)) return BadRequest("Invalid status.");

            var lines = _store.ListOrders(null)
                              .Where(o => o.OrderId == id)
                              .ToList();

            foreach (var line in lines)
                _store.UpdateOrderStatus(line.RowKey, status);

            TempData["Msg"] = "Status updated.";
            return RedirectToAction(nameof(Manage));
        }

        // ==============================================================
        // ORDER DETAILS (GROUPED)
        // ==============================================================
        [HttpGet]
        public IActionResult Details(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
                return NotFound();

            var rows = _store.ListOrders(null)
                             .Where(o => o.OrderId == id)
                             .OrderBy(o => o.ProductName)
                             .ToList();

            if (!rows.Any())
                return NotFound();

            var first = rows.First();

            var vm = new OrderDetailsVm
            {
                OrderId = id,
                Customer = first.Username ?? "",
                Status = first.Status ?? "Submitted",
                CreatedAtUtc = first.CreatedAtUtc,
                Items = rows,
                TotalAmount = rows.Sum(x => (double)x.TotalPrice),
                ContractFileName = first.ContractFileName,
                ContractOriginalName = first.ContractOriginalFileName,
                ContractContentType = first.ContractContentType
            };

            ViewBag.Role = CurrentRole();
            return View(vm);
        }
    }
}
