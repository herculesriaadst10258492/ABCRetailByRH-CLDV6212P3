using System.ComponentModel.DataAnnotations;

namespace ABCRetailByRH.ViewModels
{
    public class CreateOrderInput
    {
        [Required]
        public string Customer { get; set; } = "";

        [Range(0.01, double.MaxValue)]
        public double Total { get; set; }
    }
}
