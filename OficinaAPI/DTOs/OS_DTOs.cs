namespace OficinaAPI.DTOs
{
    public class CreateOSDTO { public string ClientName { get; set; } = ""; public string VehicleModel { get; set; } = ""; public string CustomerAddress { get; set; } = ""; public string CustomerPhone { get; set; } = ""; }
    public class UpdateVehicleDTO { public string CustomerName { get; set; } = ""; public string VehicleModel { get; set; } = ""; public string CustomerAddress { get; set; } = ""; public string CustomerPhone { get; set; } = ""; }
    public class AddItemDTO { public int ProductId { get; set; } public int Quantity { get; set; } public decimal? Price { get; set; } public decimal? CostPrice { get; set; } public string? WarrantyPeriod { get; set; } }
    public class AddLaborDTO { public string Description { get; set; } = ""; public decimal Price { get; set; } public string? WarrantyPeriod { get; set; } }
    public class AddCustomItemDTO { public string Description { get; set; } = ""; public int Quantity { get; set; } = 1; public decimal Price { get; set; } public decimal CostPrice { get; set; } public string? WarrantyPeriod { get; set; } }
    public class CompletionDTO { public DateTime CompletionDate { get; set; } }
    public class UpdateTotalDTO { public decimal TotalAmount { get; set; } }
    public class UpdateServiceItemDTO { public string Dsescription { get; set; } = ""; public decimal Price { get; set; } public decimal CostPrice { get; set; } public string? WarrantyPeriod { get; set; } public int Quantity { get; set; } = 1; public string? ItemType { get; set; } }
    public class PaymentSplitDTO { public string PaymentMethod { get; set; } = ""; public decimal Amount { get; set; } public DateTime PaymentDate { get; set; } = DateTime.Now; }
    public class UpdatePaymentDTO { public decimal AmountPaid { get; set; } public string? PaymentMethod { get; set; } public DateTime? PromisedPaymentDate { get; set; } public List<PaymentSplitDTO> Payments { get; set; } = new(); }
    public class CashAdjustmentDTO { public decimal Amount { get; set; } public string Description { get; set; } = ""; }
}