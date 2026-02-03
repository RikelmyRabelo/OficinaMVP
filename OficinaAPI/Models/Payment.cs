using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace OficinaAPI.Models
{
    public class PaymentRecord
    {
        public int Id { get; set; }

        public decimal Amount { get; set; }

        public bool IsPaid { get; set; } = false;

        public DateTime? PaymentDate { get; set; } 
        public string? AdminNotes { get; set; }

        public int EmployeeId { get; set; }

        [JsonIgnore]
        public Employee? Employee { get; set; }
    }
}