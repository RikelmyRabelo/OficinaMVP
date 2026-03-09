using System.Text.Json.Serialization;

namespace OficinaAPI.Models
{
    public class ServiceOrderPayment
    {
        public int Id { get; set; }

        public int ServiceOrderId { get; set; }

        public string PaymentMethod { get; set; } = "";

        public decimal Amount { get; set; }

        [JsonIgnore]
        public ServiceOrder? ServiceOrder { get; set; }
    }
}