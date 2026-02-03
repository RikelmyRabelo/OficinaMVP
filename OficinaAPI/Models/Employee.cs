using System.ComponentModel.DataAnnotations.Schema;

namespace OficinaAPI.Models
{
    public class Employee
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public bool Active { get; set; } = true;

        [Column(TypeName = "decimal(18,2)")]
        public decimal BaseSalary { get; set; }

        public List<PaymentRecord> Payments { get; set; } = new();
    }
}