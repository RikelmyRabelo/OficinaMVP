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

        [HttpPut("{id}/complete")]
        public async Task<IActionResult> CompleteOrder(int id, [FromBody] CompletionDTO completion)
        {
            var os = await _context.ServiceOrders.Include(o => o.Items).FirstOrDefaultAsync(o => o.Id == id);
            if (os == null) return NotFound();
            if (os.Status == "Completed") return BadRequest("O.S. já finalizada.");

            // Busca o período ativo definido nas configurações
            var settings = await _context.SystemSettings.AsNoTracking().FirstOrDefaultAsync();

            foreach (var item in os.Items)
            {
                if (item.ProductId != null)
                {
                    var p = await _context.Products.FindAsync(item.ProductId);
                    if (p != null) p.StockQuantity -= item.Quantity;
                }
            }

            os.Status = "Completed";

            // 1. DATA REAL: Salva o dia exato do calendário para o Histórico/Tabelas
            os.CompletionDate = DateTime.Now;

            // 2. DATA CONTÁBIL: Vincula ao mês ativo no sistema para o Dashboard
            if (settings != null)
            {
                os.AccountingMonth = settings.ActiveMonth;
                os.AccountingYear = settings.ActiveYear;
            }

            await _context.SaveChangesAsync();
            return NoContent();
        }

        [HttpGet("financial-summary")]
        public async Task<ActionResult<FinancialSummaryDTO>> GetFinancialSummary()
        {
            var settings = await _context.SystemSettings.AsNoTracking().FirstOrDefaultAsync();
            if (settings == null) return BadRequest("Configurações de período não encontradas.");

            // Filtra o faturamento pelo "Mês Contábil" gravado na finalização
            var query = _context.ServiceOrders.AsNoTracking()
                .Where(o => o.Status == "Completed" &&
                            !o.IsDeleted &&
                            o.AccountingMonth == settings.ActiveMonth &&
                            o.AccountingYear == settings.ActiveYear);

            var osData = await query.Select(o => new {
                o.Id,
                o.TotalAmount,
                o.AmountPaid,
                o.PaymentMethod,
                Data = o.CompletionDate ?? DateTime.MinValue
            }).ToListAsync();

            var multiPayments = await _context.ServiceOrderPayments.AsNoTracking()
                .Include(p => p.ServiceOrder)
                .Where(p => p.ServiceOrder.Status == "Completed" &&
                            !p.ServiceOrder.IsDeleted &&
                            p.ServiceOrder.AccountingMonth == settings.ActiveMonth &&
                            p.ServiceOrder.AccountingYear == settings.ActiveYear)
                .Select(p => new { p.ServiceOrderId, Metodo = (p.PaymentMethod ?? "").ToUpper(), p.Amount })
                .ToListAsync();

            var summary = new FinancialSummaryDTO();

            // Define início da semana com base no período contábil para o gráfico
            DateTime refDate = new DateTime(settings.ActiveYear, settings.ActiveMonth, Math.Min(DateTime.Now.Day, DateTime.DaysInMonth(settings.ActiveYear, settings.ActiveMonth)));
            DateTime inicioDaSemana = refDate.AddDays(-(int)refDate.DayOfWeek);

            foreach (var os in osData)
            {
                summary.FaturamentoTotal += os.AmountPaid;
                summary.Inadimplencia += Math.Max(0, os.TotalAmount - os.AmountPaid);

                if (os.Data >= inicioDaSemana) summary.FaturamentoSemanal += os.AmountPaid;

                // Soma por método (Tratando pagamentos múltiplos ou únicos)
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
            var multi = await _context.ServiceOrderPayments.Include(p => p.ServiceOrder)
                .Where(p => p.PaymentMethod == "Dinheiro" && p.ServiceOrder != null && !p.ServiceOrder.IsDeleted)
                .SumAsync(p => p.Amount);

            var legacy = await _context.ServiceOrders
                .Where(o => o.PaymentMethod == "Dinheiro" && !o.IsDeleted && !o.Payments.Any())
                .SumAsync(o => o.AmountPaid);

            var adjust = await _context.Set<CashTransaction>().SumAsync(t => t.Amount);

            return Ok(multi + legacy + adjust);
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

        public class CreateOSDTO { public string ClientName { get; set; } = ""; public string VehicleModel { get; set; } = ""; public string CustomerAddress { get; set; } = ""; public string CustomerPhone { get; set; } = ""; }
        public class AddItemDTO { public int ProductId { get; set; } public int Quantity { get; set; } public decimal? Price { get; set; } public string? WarrantyPeriod { get; set; } }
        public class CompletionDTO { public DateTime CompletionDate { get; set; } }
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