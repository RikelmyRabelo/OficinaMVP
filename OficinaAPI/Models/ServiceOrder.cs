using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace OficinaAPI.Models
{
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
        [Column(TypeName = "decimal(18,2)")]
        public decimal AmountPaid { get; set; } = 0;
        public string? PaymentMethod { get; set; }
        public DateTime? PromisedPaymentDate { get; set; }
        public List<ServiceItem> Items { get; set; } = new();
        public List<ServiceOrderAttachment> Attachments { get; set; } = new();
        public bool IsDeleted { get; set; } = false;
        public DateTime? DeletionDate { get; set; }

        public List<ServiceOrderPayment> Payments { get; set; } = new();
    }

    public class ServiceItem
    {
        public int Id { get; set; }
        public int ServiceOrderId { get; set; }
        [JsonIgnore]
        public ServiceOrder? ServiceOrder { get; set; }
        public string Description { get; set; } = string.Empty;
        public int? ProductId { get; set; }
        public Product? Product { get; set; }

        // REMOVIDO: MechanicId e Mechanic (Employee)

        public int Quantity { get; set; } = 1;
        [Column(TypeName = "decimal(18,2)")]
        public decimal Price { get; set; }
        public string? WarrantyPeriod { get; set; }
        public DateTime? WarrantyExpirationDate { get; set; }

        public string ItemType { get; set; } = "Product";
    }

    public class Vehicle
    {
        public int Id { get; set; }
        public string LicensePlate { get; set; } = string.Empty;
        public string Model { get; set; } = string.Empty;
        public string CustomerName { get; set; } = string.Empty;
        public string CustomerPhone { get; set; } = string.Empty;
        public string CustomerAddress { get; set; } = string.Empty;
    }

    public class ServiceOrderAttachment
    {
        public int Id { get; set; }
        public int ServiceOrderId { get; set; }
        [JsonIgnore]
        public ServiceOrder? ServiceOrder { get; set; }
        public string FileName { get; set; } = string.Empty;
        public string FileType { get; set; } = string.Empty;
        public string Base64Content { get; set; } = string.Empty;
    }
}