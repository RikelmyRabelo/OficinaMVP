using System.ComponentModel.DataAnnotations.Schema;

namespace OficinaAPI.Models
{
    public class Vehicle
    {
        public int Id { get; set; }
        public string LicensePlate { get; set; } = string.Empty;
        public string Model { get; set; } = string.Empty;
        public string CustomerName { get; set; } = string.Empty;
        public string CustomerPhone { get; set; } = string.Empty;
    }

    public class ServiceOrder
    {
        public int Id { get; set; }

        public int VehicleId { get; set; }
        public Vehicle? Vehicle { get; set; }

        public DateTime EntryDate { get; set; } = DateTime.Now;
        public DateTime? CompletionDate { get; set; }
        public string Status { get; set; } = "Pending";

        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalAmount { get; set; }

        public List<ServiceItem> Items { get; set; } = new();
    }

    public class ServiceItem
    {
        public int Id { get; set; }
        public int ServiceOrderId { get; set; }
        public ServiceOrder? ServiceOrder { get; set; }

        public string Description { get; set; } = string.Empty;

        public int? ProductId { get; set; }
        public Product? Product { get; set; }

        public int? MechanicId { get; set; }
        public Employee? Mechanic { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal Price { get; set; }

        public DateTime? WarrantyExpirationDate { get; set; }
    }
}