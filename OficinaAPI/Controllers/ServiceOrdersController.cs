using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OficinaAPI.Data;
using OficinaAPI.Models;
using System.Text.Json;

namespace OficinaAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ServiceOrdersController : ControllerBase
    {
        private readonly OficinaContext _context;
        public ServiceOrdersController(OficinaContext context) { _context = context; }

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

            decimal precoUnitario = itemDto.Price.HasValue ? itemDto.Price.Value : product.SalePrice;

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

            if (os.Status == "Completed")
            {
                product.StockQuantity -= itemDto.Quantity;
            }

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
                ProductId = null,
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
                ProductId = null,
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
            var os = await _context.ServiceOrders
                .Include(o => o.Items)
                .FirstOrDefaultAsync(o => o.Id == id);

            if (os == null) return NotFound();
            if (os.Status == "Completed") return BadRequest("Esta O.S. já foi finalizada.");

            foreach (var item in os.Items)
            {
                if (item.ProductId != null)
                {
                    var product = await _context.Products.FindAsync(item.ProductId);
                    if (product != null)
                    {
                        product.StockQuantity -= item.Quantity;
                    }
                }
            }

            os.Status = "Completed";
            os.CompletionDate = completion.CompletionDate;
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
                foreach (var split in request.Payments)
                {
                    os.Payments.Add(new ServiceOrderPayment
                    {
                        ServiceOrderId = id,
                        PaymentMethod = split.PaymentMethod,
                        Amount = split.Amount,
                        PaymentDate = split.PaymentDate != DateTime.MinValue ? split.PaymentDate : DateTime.Now
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
                    var product = await _context.Products.FindAsync(item.ProductId);
                    if (product != null)
                    {
                        var diff = request.Quantity - item.Quantity;
                        product.StockQuantity -= diff;
                    }
                }
            }

            item.Description = request.Description;
            item.Price = request.Price;
            item.WarrantyPeriod = request.WarrantyPeriod;
            item.Quantity = request.Quantity;

            if (!string.IsNullOrEmpty(request.ItemType))
            {
                item.ItemType = request.ItemType;
            }

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
                var product = await _context.Products.FindAsync(item.ProductId);
                if (product != null)
                {
                    product.StockQuantity += item.Quantity;
                }
            }

            os.TotalAmount -= item.Price;
            _context.ServiceItems.Remove(item);
            await _context.SaveChangesAsync();
            return NoContent();
        }

        [HttpGet("{id}/attachments")]
        public async Task<ActionResult<IEnumerable<ServiceOrderAttachment>>> GetAttachments(int id)
        {
            return await _context.Set<ServiceOrderAttachment>()
                .AsNoTracking()
                .Where(a => a.ServiceOrderId == id)
                .ToListAsync();
        }

        [HttpPost("{id}/attachments")]
        public async Task<IActionResult> AddAttachment(int id, [FromBody] UploadAttachmentDTO request)
        {
            var os = await _context.ServiceOrders.FindAsync(id);
            if (os == null) return NotFound();
            var attachment = new ServiceOrderAttachment { ServiceOrderId = id, FileName = request.FileName, FileType = request.FileType, Base64Content = request.Base64Content };
            _context.Set<ServiceOrderAttachment>().Add(attachment);
            await _context.SaveChangesAsync();
            return Ok();
        }

        [HttpDelete("{id}/attachments/{attachmentId}")]
        public async Task<IActionResult> DeleteAttachment(int id, int attachmentId)
        {
            var attachment = await _context.Set<ServiceOrderAttachment>().FirstOrDefaultAsync(a => a.Id == attachmentId && a.ServiceOrderId == id);
            if (attachment == null) return NotFound();
            _context.Set<ServiceOrderAttachment>().Remove(attachment);
            await _context.SaveChangesAsync();
            return NoContent();
        }

        [HttpGet("financial-summary")]
        public async Task<ActionResult<FinancialSummaryDTO>> GetFinancialSummary()
        {
            DateTime hoje = DateTime.Now.Date;
            DateTime inicioDaSemana = hoje.AddDays(-(int)hoje.DayOfWeek);

            var osData = await _context.ServiceOrders
                .AsNoTracking()
                .Where(o => o.Status == "Completed" && !o.IsDeleted)
                .Select(o => new {
                    Total = o.TotalAmount,
                    Pago = o.AmountPaid,
                    Metodo = o.PaymentMethod,
                    Data = o.CompletionDate ?? DateTime.MinValue,
                    FaltaPagar = Math.Max(0, o.TotalAmount - o.AmountPaid)
                })
                .ToListAsync();

            var multiPaymentsData = await _context.ServiceOrderPayments
                .AsNoTracking()
                .Include(p => p.ServiceOrder)
                .Where(p => p.ServiceOrder != null && p.ServiceOrder.Status == "Completed" && !p.ServiceOrder.IsDeleted)
                .Select(p => new {
                    Metodo = p.PaymentMethod,
                    Valor = p.Amount,
                    Data = p.ServiceOrder.CompletionDate ?? DateTime.MinValue
                })
                .ToListAsync();

            var summary = new FinancialSummaryDTO();

            foreach (var os in osData)
            {
                summary.FaturamentoTotal += os.Pago;
                summary.Inadimplencia += os.FaltaPagar;

                if (os.Data >= inicioDaSemana)
                {
                    summary.FaturamentoSemanal += os.Pago;
                }
            }

            summary.TotalPix = multiPaymentsData.Where(p => p.Metodo == "PIX").Sum(p => p.Valor);
            summary.TotalCredito = multiPaymentsData.Where(p => p.Metodo == "Crédito" || p.Metodo == "CRÉDITO").Sum(p => p.Valor);
            summary.TotalDebito = multiPaymentsData.Where(p => p.Metodo == "Débito" || p.Metodo == "DÉBITO").Sum(p => p.Valor);

            summary.TotalPix += osData.Where(o => o.Metodo == "PIX" && !_context.ServiceOrderPayments.Any()).Sum(o => o.Pago);
            summary.TotalCredito += osData.Where(o => (o.Metodo == "Crédito" || o.Metodo == "CRÉDITO") && !_context.ServiceOrderPayments.Any()).Sum(o => o.Pago);
            summary.TotalDebito += osData.Where(o => (o.Metodo == "Débito" || o.Metodo == "DÉBITO") && !_context.ServiceOrderPayments.Any()).Sum(o => o.Pago);

            return Ok(summary);
        }

        [HttpGet("cash-balance")]
        public async Task<ActionResult<decimal>> GetCashBalance()
        {
            var entradasMultiplasDinheiro = await _context.ServiceOrderPayments
                .Include(p => p.ServiceOrder)
                .Where(p => p.PaymentMethod == "Dinheiro" && p.ServiceOrder != null && !p.ServiceOrder.IsDeleted)
                .SumAsync(p => p.Amount);

            var entradasLegadoDinheiro = await _context.ServiceOrders
                .Where(o => o.PaymentMethod == "Dinheiro" && !o.IsDeleted && !o.Payments.Any())
                .SumAsync(o => o.AmountPaid);

            var ajustes = await _context.Set<CashTransaction>().SumAsync(t => t.Amount);

            return Ok(entradasMultiplasDinheiro + entradasLegadoDinheiro + ajustes);
        }

        [HttpPost("cash-adjustment")]
        public async Task<IActionResult> PostAdjustment([FromBody] CashAdjustmentDTO request)
        {
            var transaction = new CashTransaction
            {
                Amount = request.Amount,
                Description = request.Description,
                Date = DateTime.Now
            };

            _context.Set<CashTransaction>().Add(transaction);
            await _context.SaveChangesAsync();
            return Ok();
        }

        [HttpGet("revenue-adjustments")]
        public async Task<ActionResult<IEnumerable<RevenueAdjustment>>> GetRevenueAdjustments()
        {
            return await _context.Set<RevenueAdjustment>().AsNoTracking().ToListAsync();
        }

        [HttpPost("revenue-adjustment")]
        public async Task<IActionResult> PostRevenueAdjustment([FromBody] CashAdjustmentDTO request)
        {
            var adjustment = new RevenueAdjustment
            {
                Amount = request.Amount,
                Description = request.Description,
                Date = DateTime.Now
            };

            _context.Set<RevenueAdjustment>().Add(adjustment);
            await _context.SaveChangesAsync();
            return Ok();
        }

        // CLASSES DTO
        public class CreateOSDTO { public string ClientName { get; set; } = ""; public string VehicleModel { get; set; } = ""; public string CustomerAddress { get; set; } = ""; public string CustomerPhone { get; set; } = ""; }
        public class UpdateVehicleDTO { public string CustomerName { get; set; } = ""; public string VehicleModel { get; set; } = ""; public string CustomerAddress { get; set; } = ""; public string CustomerPhone { get; set; } = ""; }
        public class AddItemDTO { public int ProductId { get; set; } public int Quantity { get; set; } public decimal? Price { get; set; } public string? WarrantyPeriod { get; set; } }
        public class AddLaborDTO { public string Description { get; set; } = ""; public decimal Price { get; set; } public string? WarrantyPeriod { get; set; } }
        public class AddCustomItemDTO { public string Description { get; set; } = ""; public int Quantity { get; set; } = 1; public decimal Price { get; set; } public string? WarrantyPeriod { get; set; } }
        public class CompletionDTO { public DateTime CompletionDate { get; set; } }
        public class UpdateTotalDTO { public decimal TotalAmount { get; set; } }

        public class UpdateServiceItemDTO
        {
            public string Description { get; set; } = "";
            public decimal Price { get; set; }
            public string? WarrantyPeriod { get; set; }
            public int Quantity { get; set; } = 1;
            public string? ItemType { get; set; }
        }

        public class PaymentSplitDTO { public string PaymentMethod { get; set; } = ""; public decimal Amount { get; set; } public DateTime PaymentDate { get; set; } = DateTime.Now; }
        public class UpdatePaymentDTO
        {
            public decimal AmountPaid { get; set; }
            public string? PaymentMethod { get; set; }
            public DateTime? PromisedPaymentDate { get; set; }
            public List<PaymentSplitDTO> Payments { get; set; } = new();
        }

        public class UploadAttachmentDTO { public string FileName { get; set; } = ""; public string FileType { get; set; } = ""; public string Base64Content { get; set; } = ""; }
        public class CashAdjustmentDTO { public decimal Amount { get; set; } public string Description { get; set; } = ""; }

        public class FinancialSummaryDTO
        {
            public decimal FaturamentoTotal { get; set; }
            public decimal FaturamentoSemanal { get; set; }
            public decimal Inadimplencia { get; set; }
            public decimal TotalPix { get; set; }
            public decimal TotalCredito { get; set; }
            public decimal TotalDebito { get; set; }
        }
    }
}