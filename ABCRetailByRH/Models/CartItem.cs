using System.ComponentModel.DataAnnotations;

namespace ABCRetailByRH.Models
{
    public class CartItem
    {
        [Key]
        public int Id { get; set; }

        [Required, MaxLength(100)]
        public string CustomerUsername { get; set; } = string.Empty;

        [Required, MaxLength(100)]
        public string ProductId { get; set; } = string.Empty;

        [Required]
        public int Quantity { get; set; }

        // === NEW FIELDS (RECOMMENDED FOR POE EVIDENCE) ===

        [Required, MaxLength(200)]
        public string ProductName { get; set; } = string.Empty;

        [Required]
        public double UnitPrice { get; set; }

        [MaxLength(500)]
        public string? ImageUrl { get; set; }
    }
}
