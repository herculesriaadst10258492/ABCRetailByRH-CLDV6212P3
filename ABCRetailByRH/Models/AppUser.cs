using System;
using System.ComponentModel.DataAnnotations;

namespace ABCRetailByRH.Models
{
    public class AppUser
    {
        public int Id { get; set; }

        [Required, MaxLength(64)]
        public string Username { get; set; } = string.Empty;

        // FIXED: Removed [Required] so Edit does not fail validation
        [MaxLength(128)]
        public string PasswordHash { get; set; } = string.Empty;

        // NEW — Email
        [Required, MaxLength(128)]
        public string Email { get; set; } = string.Empty;

        // NEW — Phone
        [Required, MaxLength(32)]
        public string Phone { get; set; } = string.Empty;

        // "Customer" or "Admin"
        [Required, MaxLength(16)]
        public string Role { get; set; } = "Customer";

        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    }
}
