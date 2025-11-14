using System.Collections.Generic;

namespace ABCRetailByRH.ViewModels
{
    public class CartItemViewModel
    {
        public int Id { get; set; }   // 🔥 NEW (SQL primary key)

        public string ProductId { get; set; } = "";
        public string Name { get; set; } = "";
        public double UnitPrice { get; set; }
        public int Quantity { get; set; }
        public string? ImageUrl { get; set; }
    }

    public class CartViewModel
    {
        public List<CartItemViewModel> Items { get; set; } = new();

        public double Subtotal { get; set; }
        public double VAT { get; set; }
        public double Shipping { get; set; }
        public double GrandTotal { get; set; }

        public string? User { get; set; }
    }
}
