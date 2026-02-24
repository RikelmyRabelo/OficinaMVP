using System.ComponentModel.DataAnnotations.Schema;

namespace OficinaAPI.Models
{
    public class QuickSale
    {
        public int Id { get; set; }
        public string Description { get; set; } = string.Empty;
        public int? Quantity { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal Price { get; set; }

        public DateTime SaleDate { get; set; } = DateTime.Now;
    }
}