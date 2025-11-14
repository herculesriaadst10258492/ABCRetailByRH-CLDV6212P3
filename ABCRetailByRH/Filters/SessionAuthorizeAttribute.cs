// ABCRetailByRH/Filters/SessionAuthorizeAttribute.cs
using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace ABCRetailByRH.Filters
{
    /// <summary>
    /// Lightweight auth using Session.
    /// Example:
    ///   [SessionAuthorize]                   // any logged-in user
    ///   [SessionAuthorize(Role="Admin")]     // only Admin
    ///   [SessionAuthorize(Role="Customer")]  // only Customer
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
    public sealed class SessionAuthorizeAttribute : Attribute, IAsyncActionFilter
    {
        public string? Role { get; set; }

        private const string SessionUserKey = "UserName";
        private const string SessionRoleKey = "UserRole";

        public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            var http = context.HttpContext;
            var user = http.Session.GetString(SessionUserKey);
            var role = http.Session.GetString(SessionRoleKey);

            if (string.IsNullOrWhiteSpace(user))
            {
                context.Result = new RedirectToActionResult("Login", "Login", null);
                return;
            }

            if (!string.IsNullOrWhiteSpace(Role) && !string.Equals(Role, role, StringComparison.OrdinalIgnoreCase))
            {
                // Logged in but wrong role -> send to role-appropriate landing
                context.Result = new RedirectToActionResult(
                    role?.Equals("Admin", StringComparison.OrdinalIgnoreCase) == true ? "AdminDashboard" : "CustomerDashboard",
                    "Home",
                    null
                );
                return;
            }

            await next();
        }
    }
}
