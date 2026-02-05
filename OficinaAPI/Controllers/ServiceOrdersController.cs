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

        public ServiceOrdersController(OficinaContext context)
        {
            _context = context;
        }

        // GET: api/serviceorders
        [HttpGet]
        public async Task<ActionResult<IEnumerable<ServiceOrder>>> GetServiceOrders()
        {
            return await _context.ServiceOrders
                .Include(o => o.Vehicle)
                .Include(o => o.Items)
                .OrderByDescending(o => o.Id)
                .ToListAsync();
        }

        // POST: api/serviceorders (Cria OS + Veículo)
        [HttpPost]
        public async Task<ActionResult<ServiceOrder>> PostServiceOrder(CreateOSDTO request)
        {
            // 1. Cria o Veículo
            var newVehicle = new Vehicle
            {
                CustomerName = request.ClientName,
                Model = request.VehicleModel,
                LicensePlate = "SEM-PLACA",
                CustomerPhone = ""
            };

            _context.Vehicles.Add(newVehicle);
            await _context.SaveChangesAsync();

            // 2. Cria a OS
            var serviceOrder = new ServiceOrder
            {
                VehicleId = newVehicle.Id,
                EntryDate = DateTime.Now,
                Status = "Pending",
                TotalAmount = 0
            };

            _context.ServiceOrders.Add(serviceOrder);
            await _context.SaveChangesAsync();

            serviceOrder.Vehicle = newVehicle;

            return CreatedAtAction("GetServiceOrders", new { id = serviceOrder.Id }, serviceOrder);
        }

        // POST: api/serviceorders/5/items (Adiciona PEÇA)
        [HttpPost("{id}/items")]
        public async Task<ActionResult<ServiceItem>> AddItem(int id, AddItemDTO itemDto)
        {
            var os = await _context.ServiceOrders.Include(o => o.Items).FirstOrDefaultAsync(o => o.Id == id);
            if (os == null) return NotFound("Ordem de Serviço não encontrada.");

            var product = await _context.Products.FindAsync(itemDto.ProductId);
            if (product == null) return BadRequest("Produto não encontrado.");

            // Verifica Estoque
            if (product.StockQuantity < itemDto.Quantity)
                return BadRequest($"Estoque insuficiente.");

            // Cria o Item
            var newItem = new ServiceItem
            {
                ServiceOrderId = id,
                ProductId = product.Id,
                Description = product.Name,
                Price = product.SalePrice * itemDto.Quantity,
                WarrantyExpirationDate = DateTime.Now.AddMonths(3)
            };

            // Baixa no estoque
            product.StockQuantity -= itemDto.Quantity;

            // Atualiza total da OS
            os.TotalAmount += newItem.Price;

            _context.ServiceItems.Add(newItem);
            await _context.SaveChangesAsync();

            return Ok(newItem);
        }

        // POST: api/serviceorders/5/labor (Adiciona SERVIÇO / MÃO DE OBRA)
        [HttpPost("{id}/labor")]
        public async Task<ActionResult<ServiceItem>> AddLabor(int id, AddLaborDTO laborDto)
        {
            var os = await _context.ServiceOrders.Include(o => o.Items).FirstOrDefaultAsync(o => o.Id == id);
            if (os == null) return NotFound("Ordem de Serviço não encontrada.");

            var mechanic = await _context.Employees.FindAsync(laborDto.MechanicId);
            if (mechanic == null) return BadRequest("Mecânico não encontrado.");

            var newItem = new ServiceItem
            {
                ServiceOrderId = id,
                ProductId = null,
                MechanicId = mechanic.Id,
                Description = laborDto.Description,
                Price = laborDto.Price,
                WarrantyExpirationDate = DateTime.Now.AddMonths(3)
            };

            os.TotalAmount += newItem.Price;

            _context.ServiceItems.Add(newItem);
            await _context.SaveChangesAsync();

            return Ok(newItem);
        }

        // PUT: api/serviceorders/5/complete
        [HttpPut("{id}/complete")]
        public async Task<IActionResult> CompleteOrder(int id)
        {
            var os = await _context.ServiceOrders.FindAsync(id);
            if (os == null) return NotFound();

            os.Status = "Completed";
            os.CompletionDate = DateTime.Now;

            await _context.SaveChangesAsync();
            return NoContent();
        }

        // DELETE: api/serviceorders/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteServiceOrder(int id)
        {
            var serviceOrder = await _context.ServiceOrders
                .Include(so => so.Items)
                .FirstOrDefaultAsync(so => so.Id == id);

            if (serviceOrder == null) return NotFound();

            // 1. Remove os itens vinculados primeiro para evitar erro de chave estrangeira (FK)
            if (serviceOrder.Items != null && serviceOrder.Items.Any())
            {
                _context.ServiceItems.RemoveRange(serviceOrder.Items);
            }

            // 2. Remove a ordem de serviço
            _context.ServiceOrders.Remove(serviceOrder);

            await _context.SaveChangesAsync();
            return NoContent();
        }

        // --- DTOs ---
        public class CreateOSDTO
        {
            public string ClientName { get; set; } = string.Empty;
            public string VehicleModel { get; set; } = string.Empty;
        }

        public class AddItemDTO
        {
            public int ProductId { get; set; }
            public int Quantity { get; set; }
        }

        public class AddLaborDTO
        {
            public int MechanicId { get; set; }
            public string Description { get; set; } = string.Empty;
            public decimal Price { get; set; }
        }
    }
}