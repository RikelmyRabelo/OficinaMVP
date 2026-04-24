using System;
using System.Collections.Generic;

namespace OficinaWeb.Models
{
    public class SystemSettingsDTO
    {
        public int ActiveMonth { get; set; }
        public int ActiveYear { get; set; }
    }

    public class FinancialSummaryDTO
    {
        public decimal FaturamentoTotal { get; set; }
        public decimal Inadimplencia { get; set; }
        public decimal TotalPix { get; set; }
        public decimal TotalCredito { get; set; }
        public decimal TotalDebito { get; set; }
    }

    public class CashAdjustmentDTO
    {
        public decimal Amount { get; set; }
        public string Description { get; set; } = "";
    }

    public class ServiceOrderDTO
    {
        public int Id { get; set; }
        public int VehicleId { get; set; }
        public VehicleDTO? Vehicle { get; set; }
        public DateTime EntryDate { get; set; }
        public DateTime? CompletionDate { get; set; }
        public string Status { get; set; } = "";
        public decimal TotalAmount { get; set; }
        public decimal AmountPaid { get; set; }
        public string? PaymentMethod { get; set; }
        public DateTime? PromisedPaymentDate { get; set; }
        public int AccountingMonth { get; set; }
        public int AccountingYear { get; set; }
        public bool IsDeleted { get; set; }
        public DateTime? DeletionDate { get; set; }
        public List<ServiceItemDTO> Items { get; set; } = new();
        public List<PaymentSplitDTO> Payments { get; set; } = new();
        public List<AttachmentDTO> Attachments { get; set; } = new();

        public decimal RemainingAmount => TotalAmount - AmountPaid;
        public decimal OverpaidAmount => Math.Max(0, AmountPaid - TotalAmount);
    }

    public class VehicleDTO
    {
        public string Model { get; set; } = "";
        public string CustomerName { get; set; } = "";
        public string CustomerAddress { get; set; } = "";
        public string CustomerPhone { get; set; } = "";
    }

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
        public int PrintLine { get; set; }

        public string ProductCodeDisplay
        {
            get
            {
                if (string.IsNullOrEmpty(Description)) return "-";
                int idx = Description.IndexOf(" - ");
                return idx > 0 ? Description.Substring(0, idx) : "-";
            }
        }

        public string ProductDescriptionDisplay
        {
            get
            {
                if (string.IsNullOrEmpty(Description)) return "";
                int idx = Description.IndexOf(" - ");
                return idx > 0 ? Description.Substring(idx + 3) : Description;
            }
        }
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

    public class ProductDTO
    {
        public int Id { get; set; }
        public string Code { get; set; } = "";
        public string Name { get; set; } = "";
        public decimal? SalePrice { get; set; }
        public int? StockQuantity { get; set; }
        public int MinimumStock { get; set; } = 5;
        public bool IsExternal { get; set; }
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

    public class HistoricalValueDTO
    {
        public decimal CurrentInventoryValue { get; set; }
        public decimal TotalExitedValue { get; set; }
        public decimal TotalHistoricalValue { get; set; }
    }

    public class AuditItemDTO
    {
        public int OsId { get; set; }
        public DateTime Data { get; set; }
        public string Descricao { get; set; } = "";
        public int Quantidade { get; set; }
        public decimal ValorTotal { get; set; }
    }
}