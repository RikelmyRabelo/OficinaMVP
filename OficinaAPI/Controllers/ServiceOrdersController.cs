using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OficinaAPI.Data;
using OficinaAPI.Models;

namespace OficinaAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ServiceOrdersController : ControllerBase
    {
        private readonly OficinaContext _context;
        public ServiceOrdersController(OficinaContext context) { _context = context; }

        private async Task<DateTime> GetActiveReferenceDate()
        {
            var settings = await _context.SystemSettings.AsNoTracking().FirstOrDefaultAsync();
            var agora = DateTime.Now;

            if (settings != null && (settings.ActiveMonth != agora.Month || settings.ActiveYear != agora.Year))
            {
                return new DateTime(settings.ActiveYear, settings.ActiveMonth, 1, agora.Hour, agora.Minute, agora.Second);
            }
            return agora;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<ServiceOrder>>> GetServiceOrders()
        {
            return await _context.ServiceOrders
                .AsNoTracking()
                .Include(o => o.Vehicle)
                .Include(o => o.Items)
                .Include(o => o.Payments)
                .Where(o => !o.IsDeleted)
                .OrderByDescending(o => o.Id).ToListAsync();
        }

        [HttpGet("trash")]
        public async Task<ActionResult<IEnumerable<ServiceOrder>>> GetTrash()
        {
            var threshold = DateTime.Now.AddDays(-30);
            var expiredOrders = await _context.ServiceOrders
                .Where(o => o.IsDeleted && o.DeletionDate < threshold)
                .ToListAsync();

            if (expiredOrders.Any())
            {
                _context.ServiceOrders.RemoveRange(expiredOrders);
                await _context.SaveChangesAsync();
            }

            return await _context.ServiceOrders
                .AsNoTracking()
                .Include(o => o.Vehicle)
                .Include(o => o.Items)
                .Include(o => o.Payments)
                .Where(o => o.IsDeleted)
                .OrderByDescending(o => o.DeletionDate).ToListAsync();
        }

        [HttpPost]
        public async Task<ActionResult<ServiceOrder>> PostServiceOrder(CreateOSDTO request)
        {
            var newVehicle = new Vehicle
            {
                CustomerName = request.ClientName,
                Model = request.VehicleModel,
                LicensePlate = "SEM-PLACA",
                CustomerAddress = request.CustomerAddress,
                CustomerPhone = request.CustomerPhone
            };
            _context.Vehicles.Add(newVehicle);
            await _context.SaveChangesAsync();

            var os = new ServiceOrder
            {
                VehicleId = newVehicle.Id,
                EntryDate = DateTime.Now,
                Status = "Pending",
                TotalAmount = 0,
                AmountPaid = 0
            };
            _context.ServiceOrders.Add(os);
            await _context.SaveChangesAsync();

            os.Vehicle = newVehicle;
            return CreatedAtAction("GetServiceOrders", new { id = os.Id }, os);
        }

        [HttpPost("{id}/items")]
        public async Task<ActionResult<ServiceItem>> AddItem(int id, AddItemDTO itemDto)
        {
            var os = await _context.ServiceOrders.Include(o => o.Items).FirstOrDefaultAsync(o => o.Id == id);
            if (os == null) return NotFound();

            var product = await _context.Products.FindAsync(itemDto.ProductId);
            if (product == null) return BadRequest("Produto não encontrado.");

            decimal precoUnitario = itemDto.Price ?? product.SalePrice;

            var newItem = new ServiceItem
            {
                ServiceOrderId = id,
                ProductId = product.Id,
                Description = $"{product.Code} - {product.Name}",
                Price = precoUnitario * itemDto.Quantity,
                WarrantyPeriod = itemDto.WarrantyPeriod,
                Quantity = itemDto.Quantity,
                ItemType = "Product"
            };

            os.TotalAmount += newItem.Price;
            if (os.Status == "Completed") product.StockQuantity -= itemDto.Quantity;

            _context.ServiceItems.Add(newItem);
            await _context.SaveChangesAsync();
            return Ok(newItem);
        }

        [HttpPost("{id}/labor")]
        public async Task<ActionResult<ServiceItem>> AddLabor(int id, AddLaborDTO laborDto)
        {
            var os = await _context.ServiceOrders.Include(o => o.Items).FirstOrDefaultAsync(o => o.Id == id);
            if (os == null) return NotFound();

            var newItem = new ServiceItem
            {
                ServiceOrderId = id,
                Description = laborDto.Description,
                Price = laborDto.Price,
                WarrantyPeriod = laborDto.WarrantyPeriod,
                Quantity = 1,
                ItemType = "Service"
            };

            os.TotalAmount += newItem.Price;
            _context.ServiceItems.Add(newItem);
            await _context.SaveChangesAsync();
            return Ok(newItem);
        }

        [HttpPost("{id}/custom-items")]
        public async Task<ActionResult<ServiceItem>> AddCustomItem(int id, AddCustomItemDTO customDto)
        {
            var os = await _context.ServiceOrders.Include(o => o.Items).FirstOrDefaultAsync(o => o.Id == id);
            if (os == null) return NotFound();

            var newItem = new ServiceItem
            {
                ServiceOrderId = id,
                Description = customDto.Description,
                Price = customDto.Price,
                WarrantyPeriod = customDto.WarrantyPeriod,
                Quantity = customDto.Quantity,
                ItemType = "Custom"
            };

            os.TotalAmount += newItem.Price;
            _context.ServiceItems.Add(newItem);
            await _context.SaveChangesAsync();
            return Ok(newItem);
        }

        [HttpPut("{id}/complete")]
        public async Task<IActionResult> CompleteOrder(int id, [FromBody] CompletionDTO completion)
        {
            var os = await _context.ServiceOrders.Include(o => o.Items).FirstOrDefaultAsync(o => o.Id == id);
            if (os == null) return NotFound();
            if (os.Status == "Completed") return BadRequest("O.S. já finalizada.");

            foreach (var item in os.Items)
            {
                if (item.ProductId != null)
                {
                    var p = await _context.Products.FindAsync(item.ProductId);
                    if (p != null) p.StockQuantity -= item.Quantity;
                }
            }

            os.Status = "Completed";
            os.CompletionDate = await GetActiveReferenceDate();

            await _context.SaveChangesAsync();
            return NoContent();
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> SoftDeleteServiceOrder(int id)
        {
            var os = await _context.ServiceOrders.FindAsync(id);
            if (os == null) return NotFound();
            os.IsDeleted = true;
            os.DeletionDate = DateTime.Now;
            await _context.SaveChangesAsync();
            return NoContent();
        }

        [HttpDelete("{id}/permanent")]
        public async Task<IActionResult> PermanentDeleteServiceOrder(int id)
        {
            var os = await _context.ServiceOrders.FindAsync(id);
            if (os == null) return NotFound();
            _context.ServiceOrders.Remove(os);
            await _context.SaveChangesAsync();
            return NoContent();
        }

        [HttpPut("{id}/restore")]
        public async Task<IActionResult> RestoreServiceOrder(int id)
        {
            var os = await _context.ServiceOrders.FindAsync(id);
            if (os == null) return NotFound();
            os.IsDeleted = false;
            os.DeletionDate = null;
            await _context.SaveChangesAsync();
            return NoContent();
        }

        [HttpPut("{id}/vehicle")]
        public async Task<IActionResult> UpdateVehicleData(int id, [FromBody] UpdateVehicleDTO request)
        {
            var os = await _context.ServiceOrders.Include(o => o.Vehicle).FirstOrDefaultAsync(o => o.Id == id);
            if (os == null || os.Vehicle == null) return NotFound();

            os.Vehicle.CustomerName = request.CustomerName;
            os.Vehicle.Model = request.VehicleModel;
            os.Vehicle.CustomerAddress = request.CustomerAddress;
            os.Vehicle.CustomerPhone = request.CustomerPhone;
            await _context.SaveChangesAsync();
            return NoContent();
        }

        [HttpPut("{id}/total")]
        public async Task<IActionResult> UpdateTotalAmount(int id, [FromBody] UpdateTotalDTO request)
        {
            var os = await _context.ServiceOrders.FindAsync(id);
            if (os == null) return NotFound();
            os.TotalAmount = request.TotalAmount;
            await _context.SaveChangesAsync();
            return NoContent();
        }

        [HttpPut("{id}/payment")]
        public async Task<IActionResult> UpdateAmountPaid(int id, [FromBody] UpdatePaymentDTO request)
        {
            var os = await _context.ServiceOrders.Include(o => o.Payments).FirstOrDefaultAsync(o => o.Id == id);
            if (os == null) return NotFound();

            os.AmountPaid = request.AmountPaid;
            os.PaymentMethod = request.PaymentMethod;
            os.PromisedPaymentDate = request.PromisedPaymentDate;

            _context.ServiceOrderPayments.RemoveRange(os.Payments);

            if (request.Payments != null && request.Payments.Any())
            {
                var refDate = await GetActiveReferenceDate();
                foreach (var split in request.Payments)
                {
                    os.Payments.Add(new ServiceOrderPayment
                    {
                        ServiceOrderId = id,
                        PaymentMethod = split.PaymentMethod,
                        Amount = split.Amount,
                        PaymentDate = split.PaymentDate != DateTime.MinValue ? split.PaymentDate : refDate
                    });
                }
            }

            await _context.SaveChangesAsync();
            return NoContent();
        }

        [HttpPut("{id}/items/{itemId}")]
        public async Task<IActionResult> UpdateServiceItem(int id, int itemId, [FromBody] UpdateServiceItemDTO request)
        {
            var item = await _context.ServiceItems.FirstOrDefaultAsync(i => i.Id == itemId && i.ServiceOrderId == id);
            if (item == null) return NotFound();

            var os = await _context.ServiceOrders.FindAsync(id);
            if (os != null)
            {
                os.TotalAmount -= item.Price;
                os.TotalAmount += request.Price;

                if (os.Status == "Completed" && item.ProductId != null)
                {
                    var p = await _context.Products.FindAsync(item.ProductId);
                    if (p != null) p.StockQuantity -= (request.Quantity - item.Quantity);
                }
            }

            item.Description = request.Description;
            item.Price = request.Price;
            item.WarrantyPeriod = request.WarrantyPeriod;
            item.Quantity = request.Quantity;
            if (!string.IsNullOrEmpty(request.ItemType)) item.ItemType = request.ItemType;

            await _context.SaveChangesAsync();
            return NoContent();
        }

        [HttpDelete("{id}/items/{itemId}")]
        public async Task<IActionResult> DeleteServiceItem(int id, int itemId)
        {
            var os = await _context.ServiceOrders.Include(o => o.Items).FirstOrDefaultAsync(o => o.Id == id);
            if (os == null) return NotFound();

            var item = os.Items.FirstOrDefault(i => i.Id == itemId);
            if (item == null) return NotFound();

            if (os.Status == "Completed" && item.ProductId != null)
            {
                var p = await _context.Products.FindAsync(item.ProductId);
                if (p != null) p.StockQuantity += item.Quantity;
            }

            os.TotalAmount -= item.Price;
            _context.ServiceItems.Remove(item);
            await _context.SaveChangesAsync();
            return NoContent();
        }

        [HttpGet("{id}/attachments")]
        public async Task<ActionResult<IEnumerable<ServiceOrderAttachment>>> GetAttachments(int id)
        {
            return await _context.Set<ServiceOrderAttachment>().AsNoTracking().Where(a => a.ServiceOrderId == id).ToListAsync();
        }

        [HttpPost("{id}/attachments")]
        public async Task<IActionResult> AddAttachment(int id, [FromBody] UploadAttachmentDTO request)
        {
            var os = await _context.ServiceOrders.FindAsync(id);
            if (os == null) return NotFound();
            _context.Set<ServiceOrderAttachment>().Add(new ServiceOrderAttachment { ServiceOrderId = id, FileName = request.FileName, FileType = request.FileType, Base64Content = request.Base64Content });
            await _context.SaveChangesAsync();
            return Ok();
        }

        [HttpDelete("{id}/attachments/{attachmentId}")]
        public async Task<IActionResult> DeleteAttachment(int id, int attachmentId)
        {
            var a = await _context.Set<ServiceOrderAttachment>().FirstOrDefaultAsync(a => a.Id == attachmentId && a.ServiceOrderId == id);
            if (a == null) return NotFound();
            _context.Set<ServiceOrderAttachment>().Remove(a);
            await _context.SaveChangesAsync();
            return NoContent();
        }

        [HttpGet("financial-summary")]
        public async Task<ActionResult<FinancialSummaryDTO>> GetFinancialSummary()
        {
            var settings = await _context.SystemSettings.AsNoTracking().FirstOrDefaultAsync();
            DateTime hoje = DateTime.Now.Date;
            if (settings != null && (settings.ActiveMonth != hoje.Month || settings.ActiveYear != hoje.Year))
                hoje = new DateTime(settings.ActiveYear, settings.ActiveMonth, 1);

            DateTime inicioSemana = hoje.AddDays(-(int)hoje.DayOfWeek);

            var osData = await _context.ServiceOrders.AsNoTracking().Where(o => o.Status == "Completed" && !o.IsDeleted)
                .Select(o => new { o.Id, o.TotalAmount, o.AmountPaid, o.PaymentMethod, Data = o.CompletionDate ?? DateTime.MinValue }).ToListAsync();

            var multiPayments = await _context.ServiceOrderPayments.AsNoTracking().Include(p => p.ServiceOrder)
                .Where(p => p.ServiceOrder != null && p.ServiceOrder.Status == "Completed" && !p.ServiceOrder.IsDeleted)
                .Select(p => new { p.ServiceOrderId, Metodo = (p.PaymentMethod ?? "").ToUpper(), p.Amount }).ToListAsync();

            var summary = new FinancialSummaryDTO();

            foreach (var os in osData)
            {
                summary.FaturamentoTotal += os.AmountPaid;
                summary.Inadimplencia += Math.Max(0, os.TotalAmount - os.AmountPaid);
                if (os.Data >= inicioSemana) summary.FaturamentoSemanal += os.AmountPaid;

                var pgtos = multiPayments.Where(p => p.ServiceOrderId == os.Id).ToList();
                if (pgtos.Any())
                {
                    summary.TotalPix += pgtos.Where(p => p.Metodo == "PIX").Sum(p => p.Amount);
                    summary.TotalCredito += pgtos.Where(p => p.Metodo.Contains("CREDITO") || p.Metodo.Contains("CRÉDITO")).Sum(p => p.Amount);
                    summary.TotalDebito += pgtos.Where(p => p.Metodo.Contains("DEBITO") || p.Metodo.Contains("DÉBITO")).Sum(p => p.Amount);
                }
                else
                {
                    string m = (os.PaymentMethod ?? "").ToUpper();
                    if (m == "PIX") summary.TotalPix += os.AmountPaid;
                    else if (m.Contains("CREDITO") || m.Contains("CRÉDITO")) summary.TotalCredito += os.AmountPaid;
                    else if (m.Contains("DEBITO") || m.Contains("DÉBITO")) summary.TotalDebito += os.AmountPaid;
                }
            }
            return Ok(summary);
        }

        [HttpGet("cash-balance")]
        public async Task<ActionResult<decimal>> GetCashBalance()
        {
            var multi = await _context.ServiceOrderPayments.Include(p => p.ServiceOrder).Where(p => p.PaymentMethod == "Dinheiro" && p.ServiceOrder != null && !p.ServiceOrder.IsDeleted).SumAsync(p => p.Amount);
            var legacy = await _context.ServiceOrders.Where(o => o.PaymentMethod == "Dinheiro" && !o.IsDeleted && !o.Payments.Any()).SumAsync(o => o.AmountPaid);
            var adjust = await _context.Set<CashTransaction>().SumAsync(t => t.Amount);
            return Ok(multi + legacy + adjust);
        }

        [HttpPost("cash-adjustment")]
        public async Task<IActionResult> PostAdjustment([FromBody] CashAdjustmentDTO req)
        {
            _context.Set<CashTransaction>().Add(new CashTransaction { Amount = req.Amount, Description = req.Description, Date = await GetActiveReferenceDate() });
            await _context.SaveChangesAsync();
            return Ok();
        }

        [HttpGet("revenue-adjustments")]
        public async Task<ActionResult<IEnumerable<RevenueAdjustment>>> GetRevenueAdjustments()
        {
            return await _context.Set<RevenueAdjustment>().AsNoTracking().ToListAsync();
        }

        [HttpPost("revenue-adjustment")]
        public async Task<IActionResult> PostRevenueAdjustment([FromBody] CashAdjustmentDTO req)
        {
            _context.Set<RevenueAdjustment>().Add(new RevenueAdjustment { Amount = req.Amount, Description = req.Description, Date = await GetActiveReferenceDate() });
            await _context.SaveChangesAsync();
            return Ok();
        }

        public class CreateOSDTO { public string ClientName { get; set; } = ""; public string VehicleModel { get; set; } = ""; public string CustomerAddress { get; set; } = ""; public string CustomerPhone { get; set; } = ""; }
        public class UpdateVehicleDTO { public string CustomerName { get; set; } = ""; public string VehicleModel { get; set; } = ""; public string CustomerAddress { get; set; } = ""; public string CustomerPhone { get; set; } = ""; }
        public class AddItemDTO { public int ProductId { get; set; } public int Quantity { get; set; } public decimal? Price { get; set; } public string? WarrantyPeriod { get; set; } }
        public class AddLaborDTO { public string Description { get; set; } = ""; public decimal Price { get; set; } public string? WarrantyPeriod { get; set; } }
        public class AddCustomItemDTO { public string Description { get; set; } = ""; public int Quantity { get; set; } = 1; public decimal Price { get; set; } public string? WarrantyPeriod { get; set; } }
        public class CompletionDTO { public DateTime CompletionDate { get; set; } }
        public class UpdateTotalDTO { public decimal TotalAmount { get; set; } }
        public class UpdateServiceItemDTO { public string Description { get; set; } = ""; public decimal Price { get; set; } public string? WarrantyPeriod { get; set; } public int Quantity { get; set; } = 1; public string? ItemType { get; set; } }
        public class PaymentSplitDTO { public string PaymentMethod { get; set; } = ""; public decimal Amount { get; set; } public DateTime PaymentDate { get; set; } = DateTime.Now; }
        public class UpdatePaymentDTO { public decimal AmountPaid { get; set; } public string? PaymentMethod { get; set; } public DateTime? PromisedPaymentDate { get; set; } public List<PaymentSplitDTO> Payments { get; set; } = new(); }
        public class UploadAttachmentDTO { public string FileName { get; set; } = ""; public string FileType { get; set; } = ""; public string Base64Content { get; set; } = ""; }
        public class CashAdjustmentDTO { public decimal Amount { get; set; } public string Description { get; set; } = ""; }
        public class FinancialSummaryDTO { public decimal FaturamentoTotal { get; set; } public decimal FaturamentoSemanal { get; set; } public decimal Inadimplencia { get; set; } public decimal TotalPix { get; set; } public decimal TotalCredito { get; set; } public decimal TotalDebito { get; set; } }
    }
}