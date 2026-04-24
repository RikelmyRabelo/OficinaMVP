using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace OficinaWeb.Models
{
    public class SystemSettingsDTO
    {
        [JsonPropertyName("activeMonth")]
        public int ActiveMonth { get; set; }

        [JsonPropertyName("activeYear")]
        public int ActiveYear { get; set; }
    }

    public class FinancialSummaryDTO
    {
        [JsonPropertyName("faturamentoTotal")]
        public decimal FaturamentoTotal { get; set; }

        [JsonPropertyName("faturamentoSemanal")]
        public decimal FaturamentoSemanal { get; set; }

        [JsonPropertyName("inadimplencia")]
        public decimal Inadimplencia { get; set; }

        [JsonPropertyName("totalPix")]
        public decimal TotalPix { get; set; }

        [JsonPropertyName("totalCredito")]
        public decimal TotalCredito { get; set; }

        [JsonPropertyName("totalDebito")]
        public decimal TotalDebito { get; set; }
    }

    public class CashAdjustmentDTO
    {
        [JsonPropertyName("amount")]
        public decimal Amount { get; set; }

        [JsonPropertyName("description")]
        public string Description { get; set; } = "";
    }

    public class ServiceOrderDTO
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("vehicleId")]
        public int VehicleId { get; set; }

        [JsonPropertyName("vehicle")]
        public VehicleDTO? Vehicle { get; set; }

        [JsonPropertyName("entryDate")]
        public DateTime EntryDate { get; set; }

        [JsonPropertyName("completionDate")]
        public DateTime? CompletionDate { get; set; }

        [JsonPropertyName("status")]
        public string Status { get; set; } = "";

        [JsonPropertyName("totalAmount")]
        public decimal TotalAmount { get; set; }

        [JsonPropertyName("amountPaid")]
        public decimal AmountPaid { get; set; }

        [JsonPropertyName("paymentMethod")]
        public string? PaymentMethod { get; set; }

        [JsonPropertyName("promisedPaymentDate")]
        public DateTime? PromisedPaymentDate { get; set; }

        [JsonPropertyName("accountingMonth")]
        public int AccountingMonth { get; set; }

        [JsonPropertyName("accountingYear")]
        public int AccountingYear { get; set; }

        [JsonPropertyName("isDeleted")]
        public bool IsDeleted { get; set; }

        [JsonPropertyName("deletionDate")]
        public DateTime? DeletionDate { get; set; }

        [JsonPropertyName("items")]
        public List<ServiceItemDTO> Items { get; set; } = new();

        [JsonPropertyName("payments")]
        public List<PaymentSplitDTO> Payments { get; set; } = new();

        [JsonPropertyName("attachments")]
        public List<AttachmentDTO> Attachments { get; set; } = new();

        [JsonIgnore]
        public decimal RemainingAmount => TotalAmount - AmountPaid;

        [JsonIgnore]
        public decimal OverpaidAmount => Math.Max(0, AmountPaid - TotalAmount);
    }

    public class VehicleDTO
    {
        [JsonPropertyName("model")]
        public string Model { get; set; } = "";

        [JsonPropertyName("customerName")]
        public string CustomerName { get; set; } = "";

        [JsonPropertyName("customerAddress")]
        public string CustomerAddress { get; set; } = "";

        [JsonPropertyName("customerPhone")]
        public string CustomerPhone { get; set; } = "";
    }

    public class ServiceItemDTO
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("description")]
        public string Description { get; set; } = "";

        [JsonPropertyName("price")]
        public decimal Price { get; set; }

        [JsonPropertyName("productId")]
        public int? ProductId { get; set; }

        [JsonPropertyName("warrantyPeriod")]
        public string? WarrantyPeriod { get; set; }

        [JsonPropertyName("mechanic")]
        public EmployeeListDTO? Mechanic { get; set; }

        [JsonPropertyName("quantity")]
        public int Quantity { get; set; } = 1;

        [JsonPropertyName("mechanicId")]
        public int? MechanicId { get; set; }

        [JsonPropertyName("itemType")]
        public string ItemType { get; set; } = "Product";

        [JsonPropertyName("printLine")]
        public int PrintLine { get; set; }

        [JsonIgnore]
        public string ProductCodeDisplay
        {
            get
            {
                if (string.IsNullOrEmpty(Description)) return "-";
                int idx = Description.IndexOf(" - ");
                return idx > 0 ? Description.Substring(0, idx) : "-";
            }
        }

        [JsonIgnore]
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
        [JsonPropertyName("paymentMethod")]
        public string PaymentMethod { get; set; } = "";

        [JsonPropertyName("amount")]
        public decimal Amount { get; set; }

        [JsonPropertyName("paymentDate")]
        public DateTime PaymentDate { get; set; } = DateTime.Now;
    }

    public class UpdatePaymentDTO
    {
        [JsonPropertyName("amountPaid")]
        public decimal AmountPaid { get; set; }

        [JsonPropertyName("paymentMethod")]
        public string? PaymentMethod { get; set; }

        [JsonPropertyName("promisedPaymentDate")]
        public DateTime? PromisedPaymentDate { get; set; }

        [JsonPropertyName("payments")]
        public List<PaymentSplitDTO> Payments { get; set; } = new();
    }

    public class AttachmentDTO
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("fileName")]
        public string FileName { get; set; } = "";

        [JsonPropertyName("fileType")]
        public string FileType { get; set; } = "";

        [JsonPropertyName("base64Content")]
        public string Base64Content { get; set; } = "";
    }

    public class ProductDTO
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("code")]
        public string Code { get; set; } = "";

        [JsonPropertyName("name")]
        public string Name { get; set; } = "";

        [JsonPropertyName("salePrice")]
        public decimal? SalePrice { get; set; }

        [JsonPropertyName("stockQuantity")]
        public int? StockQuantity { get; set; }

        [JsonPropertyName("minimumStock")]
        public int MinimumStock { get; set; } = 5;

        [JsonPropertyName("isExternal")]
        public bool IsExternal { get; set; }
    }

    public class ProductListDTO
    {
        [JsonPropertyName("id")]
        public int id { get; set; }

        [JsonPropertyName("code")]
        public string code { get; set; } = "";

        [JsonPropertyName("name")]
        public string name { get; set; } = "";

        [JsonPropertyName("salePrice")]
        public decimal salePrice { get; set; }

        [JsonPropertyName("stockQuantity")]
        public int stockQuantity { get; set; }
    }

    public class EmployeeListDTO
    {
        [JsonPropertyName("id")]
        public int id { get; set; }

        [JsonPropertyName("name")]
        public string name { get; set; } = "";
    }

    public class LaborServiceDTO
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; } = "";

        [JsonPropertyName("defaultPrice")]
        public decimal DefaultPrice { get; set; }
    }

    public class CreateOsInput
    {
        [JsonPropertyName("clientName")]
        public string ClientName { get; set; } = "";

        [JsonPropertyName("vehicleModel")]
        public string VehicleModel { get; set; } = "";

        [JsonPropertyName("customerAddress")]
        public string CustomerAddress { get; set; } = "";

        [JsonPropertyName("customerPhone")]
        public string CustomerPhone { get; set; } = "";
    }

    public class AddItemInput
    {
        [JsonPropertyName("productId")]
        public int ProductId { get; set; }

        [JsonPropertyName("quantity")]
        public int Quantity { get; set; } = 1;

        [JsonPropertyName("price")]
        public decimal? Price { get; set; }

        [JsonPropertyName("warrantyPeriod")]
        public string? WarrantyPeriod { get; set; }
    }

    public class AddCustomItemInput
    {
        [JsonPropertyName("description")]
        public string Description { get; set; } = "";

        [JsonPropertyName("quantity")]
        public int Quantity { get; set; } = 1;

        [JsonPropertyName("price")]
        public decimal Price { get; set; }

        [JsonPropertyName("warrantyPeriod")]
        public string? WarrantyPeriod { get; set; }
    }

    public class AddLaborInput
    {
        [JsonPropertyName("description")]
        public string Description { get; set; } = "";

        [JsonPropertyName("mechanicId")]
        public int MechanicId { get; set; }

        [JsonPropertyName("quantity")]
        public int Quantity { get; set; } = 1;

        [JsonPropertyName("price")]
        public decimal Price { get; set; }
    }

    public class UpdateVehicleDTO
    {
        [JsonPropertyName("customerName")]
        public string CustomerName { get; set; } = "";

        [JsonPropertyName("vehicleModel")]
        public string VehicleModel { get; set; } = "";

        [JsonPropertyName("customerAddress")]
        public string CustomerAddress { get; set; } = "";

        [JsonPropertyName("customerPhone")]
        public string CustomerPhone { get; set; } = "";
    }

    public class UpdateTotalDTO
    {
        [JsonPropertyName("totalAmount")]
        public decimal TotalAmount { get; set; }
    }

    public class UpdateServiceItemDTO
    {
        [JsonPropertyName("description")]
        public string Description { get; set; } = "";

        [JsonPropertyName("price")]
        public decimal Price { get; set; }

        [JsonPropertyName("warrantyPeriod")]
        public string? WarrantyPeriod { get; set; }

        [JsonPropertyName("quantity")]
        public int Quantity { get; set; } = 1;

        [JsonPropertyName("mechanicId")]
        public int? MechanicId { get; set; }

        [JsonPropertyName("itemType")]
        public string ItemType { get; set; } = "Product";
    }

    public class HistoricalValueDTO
    {
        [JsonPropertyName("currentInventoryValue")]
        public decimal CurrentInventoryValue { get; set; }

        [JsonPropertyName("totalExitedValue")]
        public decimal TotalExitedValue { get; set; }

        [JsonPropertyName("totalHistoricalValue")]
        public decimal TotalHistoricalValue { get; set; }
    }

    public class AuditItemDTO
    {
        [JsonPropertyName("osId")]
        public int OsId { get; set; }

        [JsonPropertyName("data")]
        public DateTime Data { get; set; }

        [JsonPropertyName("descricao")]
        public string Descricao { get; set; } = "";

        [JsonPropertyName("quantidade")]
        public int Quantidade { get; set; }

        [JsonPropertyName("valorTotal")]
        public decimal ValorTotal { get; set; }
    }
}