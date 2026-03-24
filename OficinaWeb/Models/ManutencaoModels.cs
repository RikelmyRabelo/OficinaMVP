using System;
using System.Collections.Generic;

namespace OficinaWeb.Models
{
    public class ServiceOrderDTO
    {
        public int Id { get; set; }
        public VehicleDTO? Vehicle { get; set; }
        public string Status { get; set; } = "Pending";
        public decimal TotalAmount { get; set; }
        public decimal AmountPaid { get; set; }
        public string? PaymentMethod { get; set; }
        public DateTime? PromisedPaymentDate { get; set; }
        public decimal RemainingAmount => Math.Max(0, TotalAmount - AmountPaid);
        public decimal OverpaidAmount => Math.Max(0, AmountPaid - TotalAmount);
        public List<AttachmentDTO> Attachments { get; set; } = new();
        public DateTime EntryDate { get; set; }
        public DateTime? CompletionDate { get; set; }
        public List<ServiceItemDTO> Items { get; set; } = new();
        public List<PaymentSplitDTO> Payments { get; set; } = new();

        public bool IsDeleted { get; set; } = false;
        public DateTime? DeletionDate { get; set; }
    }

    public class PaymentSplitDTO
    {
        public string PaymentMethod { get; set; } = "";
        public decimal Amount { get; set; }
        public DateTime PaymentDate { get; set; } = DateTime.Now;
    }

    public class UpdatePaymentDTO
    {
        public decimal AmountPaid { get; set; }
        public string? PaymentMethod { get; set; }
        public DateTime? PromisedPaymentDate { get; set; }
        public List<PaymentSplitDTO> Payments { get; set; } = new();
    }

    public class AttachmentDTO
    {
        public int Id { get; set; }
        public string FileName { get; set; } = "";
        public string FileType { get; set; } = "";
        public string Base64Content { get; set; } = "";
    }

    public class VehicleDTO { public string Model { get; set; } = ""; public string CustomerName { get; set; } = ""; public string CustomerAddress { get; set; } = ""; public string CustomerPhone { get; set; } = ""; }

    public class ServiceItemDTO
    {
        public int Id { get; set; }
        public string Description { get; set; } = "";
        public decimal Price { get; set; }
        public int? ProductId { get; set; }
        public string? WarrantyPeriod { get; set; }
        public EmployeeListDTO? Mechanic { get; set; }
        public int Quantity { get; set; } = 1;
        public int? MechanicId { get; set; }
        public string ItemType { get; set; } = "Product";
    }

    public class ProductListDTO { public int id { get; set; } public string code { get; set; } = ""; public string name { get; set; } = ""; public decimal salePrice { get; set; } public int stockQuantity { get; set; } }
    public class EmployeeListDTO { public int id { get; set; } public string name { get; set; } = ""; }
    public class LaborServiceDTO { public int Id { get; set; } public string Name { get; set; } = ""; public decimal DefaultPrice { get; set; } }

    public class CreateOsInput { public string ClientName { get; set; } = ""; public string VehicleModel { get; set; } = ""; public string CustomerAddress { get; set; } = ""; public string CustomerPhone { get; set; } = ""; }
    public class AddItemInput { public int ProductId { get; set; } public int Quantity { get; set; } = 1; public decimal? Price { get; set; } public string? WarrantyPeriod { get; set; } }
    public class AddCustomItemInput { public string Description { get; set; } = ""; public int Quantity { get; set; } = 1; public decimal Price { get; set; } public string? WarrantyPeriod { get; set; } }
    public class AddLaborInput { public string Description { get; set; } = ""; public int MechanicId { get; set; } public int Quantity { get; set; } = 1; public decimal Price { get; set; } }

    public class UpdateVehicleDTO { public string CustomerName { get; set; } = ""; public string VehicleModel { get; set; } = ""; public string CustomerAddress { get; set; } = ""; public string CustomerPhone { get; set; } = ""; }
    public class UpdateTotalDTO { public decimal TotalAmount { get; set; } }

    public class UpdateServiceItemDTO
    {
        public string Description { get; set; } = "";
        public decimal Price { get; set; }
        public string? WarrantyPeriod { get; set; }
        public int Quantity { get; set; } = 1;
        public int? MechanicId { get; set; }
        public string ItemType { get; set; } = "Product";
    }
}