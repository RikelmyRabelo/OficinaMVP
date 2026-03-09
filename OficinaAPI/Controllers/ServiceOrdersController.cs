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

        [HttpGet]
        public async Task<ActionResult<IEnumerable<ServiceOrder>>> GetServiceOrders()
        {
            // OTIMIZADO: Removido o carregamento de anexos pesados e adicionado AsNoTracking
            return await _context.ServiceOrders
                .AsNoTracking()
                .Include(o => o.Vehicle)
                .Include(o => o.Items).ThenInclude(i => i.Mechanic)
                .Include(o => o.Payments)
                .Where(o => !o.IsDeleted)
                .OrderByDescending(o => o.Id).ToListAsync();
        }

        [HttpGet("trash")]
        public async Task<ActionResult<IEnumerable<ServiceOrder>>> GetTrash()
        {
            var threshold = DateTime.Now.AddDays(-30);
            // OTIMIZADO: Removido o carregamento de anexos pesados e adicionado AsNoTracking
            return await _context.ServiceOrders
                .AsNoTracking()
                .Include(o => o.Vehicle)
                .Include(o => o.Items).ThenInclude(i => i.Mechanic)
                .Include(o => o.Payments)
                .Where(o => o.IsDeleted && o.DeletionDate >= threshold)
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
                Quantity = itemDto.Quantity
            };

            os.TotalAmount += newItem.Price;
            _context.ServiceItems.Add(newItem);
            await _context.SaveChangesAsync();
            return Ok(newItem);
        }

        [HttpPost("{id}/labor")]
        public async Task<ActionResult<ServiceItem>> AddLabor(int id, AddLaborDTO laborDto)
        {
            var os = await _context.ServiceOrders.Include(o => o.Items).FirstOrDefaultAsync(o => o.Id == id);
            if (os == null) return NotFound();

            Employee? mechanic = null;
            if (laborDto.MechanicId > 0)
            {
                mechanic = await _context.Employees.FindAsync(laborDto.MechanicId);
            }

            var newItem = new ServiceItem
            {
                ServiceOrderId = id,
                ProductId = null,
                MechanicId = mechanic?.Id,
                Description = laborDto.Description,
                Price = laborDto.Price,
                WarrantyPeriod = laborDto.WarrantyPeriod,
                Quantity = 1
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
                Quantity = customDto.Quantity
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

        //MÉTODO DE PAGAMENTO MÚLTIPLO
        [HttpPut("{id}/payment")]
        public async Task<IActionResult> UpdateAmountPaid(int id, [FromBody] UpdatePaymentDTO request)
        {
            var os = await _context.ServiceOrders.Include(o => o.Payments).FirstOrDefaultAsync(o => o.Id == id);
            if (os == null) return NotFound();

            os.AmountPaid = request.AmountPaid;
            os.PaymentMethod = request.PaymentMethod; // Salva o principal para manter compatibilidade com sistemas antigos
            os.PromisedPaymentDate = request.PromisedPaymentDate;

            // Remove todos os registros antigos e insere os novos (substituição total)
            _context.ServiceOrderPayments.RemoveRange(os.Payments);

            if (request.Payments != null && request.Payments.Any())
            {
                foreach (var split in request.Payments)
                {
                    os.Payments.Add(new ServiceOrderPayment
                    {
                        ServiceOrderId = id,
                        PaymentMethod = split.PaymentMethod,
                        Amount = split.Amount
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
            }

            item.Description = request.Description;
            item.Price = request.Price;
            item.WarrantyPeriod = request.WarrantyPeriod;
            item.Quantity = request.Quantity;

            await _context.SaveChangesAsync();
            return NoContent();
        }

        // --- GESTÃO DE ANEXOS ---

        // NOVO: Endpoint exclusivo para buscar os anexos (só as fotos de UMA ordem)
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

        // --- GESTÃO DE CAIXA E FATURAMENTO ---

        [HttpGet("cash-balance")]
        public async Task<ActionResult<decimal>> GetCashBalance()
        {

            // 1. Pega todas as frações de pagamento em dinheiro de O.S que NÃO foram excluídas
            var entradasMultiplasDinheiro = await _context.ServiceOrderPayments
                .Include(p => p.ServiceOrder) // Precisamos da O.S para saber se ela foi excluída
                .Where(p => p.PaymentMethod == "Dinheiro" && p.ServiceOrder != null && !p.ServiceOrder.IsDeleted)
                .SumAsync(p => p.Amount);

            // 2. Busca pagamentos "antigos" (Legado) que não têm divisão na tabela nova, mas estão marcados como Dinheiro
            var entradasLegadoDinheiro = await _context.ServiceOrders
                .Where(o => o.PaymentMethod == "Dinheiro" && !o.IsDeleted && !o.Payments.Any())
                .SumAsync(o => o.AmountPaid);

            // 3. Soma ajustes manuais (entradas positivas ou retiradas negativas)
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

        // AJUSTE DE GANHOS DA SEMANA

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
        public class AddLaborDTO { public int MechanicId { get; set; } public string Description { get; set; } = ""; public decimal Price { get; set; } public string? WarrantyPeriod { get; set; } }
        public class AddCustomItemDTO { public string Description { get; set; } = ""; public int Quantity { get; set; } = 1; public decimal Price { get; set; } public string? WarrantyPeriod { get; set; } }
        public class CompletionDTO { public DateTime CompletionDate { get; set; } }
        public class UpdateTotalDTO { public decimal TotalAmount { get; set; } }
        public class UpdateServiceItemDTO { public string Description { get; set; } = ""; public decimal Price { get; set; } public string? WarrantyPeriod { get; set; } public int Quantity { get; set; } = 1; }

        // DTOs DE PAGAMENTO
        public class PaymentSplitDTO { public string PaymentMethod { get; set; } = ""; public decimal Amount { get; set; } }
        public class UpdatePaymentDTO
        {
            public decimal AmountPaid { get; set; }
            public string? PaymentMethod { get; set; }
            public DateTime? PromisedPaymentDate { get; set; }
            public List<PaymentSplitDTO> Payments { get; set; } = new();
        }

        public class UploadAttachmentDTO { public string FileName { get; set; } = ""; public string FileType { get; set; } = ""; public string Base64Content { get; set; } = ""; }
        public class CashAdjustmentDTO { public decimal Amount { get; set; } public string Description { get; set; } = ""; }
    }
}