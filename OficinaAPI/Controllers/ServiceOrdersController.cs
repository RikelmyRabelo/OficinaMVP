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

        [HttpGet]
        public async Task<ActionResult<IEnumerable<ServiceOrder>>> GetServiceOrders()
        {
            return await _context.ServiceOrders
                .Include(o => o.Vehicle)
                .Include(o => o.Items)
                .OrderByDescending(o => o.Id)
                .ToListAsync();
        }

        [HttpPost]
        public async Task<ActionResult<ServiceOrder>> PostServiceOrder(CreateOSDTO request)
        {
            var newVehicle = new Vehicle
            {
                CustomerName = request.ClientName,
                Model = request.VehicleModel,
                LicensePlate = "SEM-PLACA",
                CustomerPhone = ""
            };

            _context.Vehicles.Add(newVehicle);
            await _context.SaveChangesAsync();

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

        [HttpPost("{id}/items")]
        public async Task<ActionResult<ServiceItem>> AddItem(int id, AddItemDTO itemDto)
        {
            var os = await _context.ServiceOrders.Include(o => o.Items).FirstOrDefaultAsync(o => o.Id == id);
            if (os == null) return NotFound();

            var product = await _context.Products.FindAsync(itemDto.ProductId);
            if (product == null) return BadRequest();

            if (product.StockQuantity < itemDto.Quantity)
                return BadRequest("Estoque insuficiente.");

            var newItem = new ServiceItem
            {
                ServiceOrderId = id,
                ProductId = product.Id,
                Description = product.Name,
                Price = product.SalePrice * itemDto.Quantity,
                WarrantyPeriod = itemDto.WarrantyPeriod
            };

            product.StockQuantity -= itemDto.Quantity;
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

            var mechanic = await _context.Employees.FindAsync(laborDto.MechanicId);
            if (mechanic == null) return BadRequest();

            var newItem = new ServiceItem
            {
                ServiceOrderId = id,
                ProductId = null,
                MechanicId = mechanic.Id,
                Description = laborDto.Description,
                Price = laborDto.Price,
                WarrantyPeriod = laborDto.WarrantyPeriod
            };

            os.TotalAmount += newItem.Price;
            _context.ServiceItems.Add(newItem);
            await _context.SaveChangesAsync();
            return Ok(newItem);
        }

        [HttpPut("{id}/complete")]
        public async Task<IActionResult> CompleteOrder(int id, [FromBody] CompletionDTO completion)
        {
            var os = await _context.ServiceOrders.FindAsync(id);
            if (os == null) return NotFound();

            os.Status = "Completed";
            os.CompletionDate = completion.CompletionDate;

            await _context.SaveChangesAsync();
            return NoContent();
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteServiceOrder(int id)
        {
            var serviceOrder = await _context.ServiceOrders.Include(so => so.Items).FirstOrDefaultAsync(so => so.Id == id);
            if (serviceOrder == null) return NotFound();
            _context.ServiceItems.RemoveRange(serviceOrder.Items);
            _context.ServiceOrders.Remove(serviceOrder);
            await _context.SaveChangesAsync();
            return NoContent();
        }

        public class CreateOSDTO { public string ClientName { get; set; } = ""; public string VehicleModel { get; set; } = ""; }
        public class AddItemDTO { public int ProductId { get; set; } public int Quantity { get; set; } public string? WarrantyPeriod { get; set; } }
        public class AddLaborDTO { public int MechanicId { get; set; } public string Description { get; set; } = ""; public decimal Price { get; set; } public string? WarrantyPeriod { get; set; } }
        public class CompletionDTO { public DateTime CompletionDate { get; set; } }
    }
}