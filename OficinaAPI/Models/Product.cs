using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OficinaAPI.Models
{
    public class Product
    {
        private string _code = string.Empty;

        public int Id { get; set; }

        [Required]
        public string Code
        {
            get => _code;
            set => _code = value?.ToUpper() ?? string.Empty;
        }

        [Required]
        public string Name { get; set; } = string.Empty;

        [Column(TypeName = "decimal(18,2)")]
        public decimal SalePrice { get; set; }

        public int StockQuantity { get; set; }
        public int MinimumStock { get; set; } = 5;
        public string? LocationCode { get; set; }

        public bool IsDeleted { get; set; } = false;

        public bool IsExternal { get; set; } = false;
    }
}