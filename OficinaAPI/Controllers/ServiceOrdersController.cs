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
                .Include(o => o.Vehicle)
                .Include(o => o.Items).ThenInclude(i => i.Mechanic)
                .Include(o => o.Attachments)
                .Where(o => !o.IsDeleted)
                .OrderByDescending(o => o.Id).ToListAsync();
        }

        [HttpGet("trash")]
        public async Task<ActionResult<IEnumerable<ServiceOrder>>> GetTrash()
        {
            var threshold = DateTime.Now.AddDays(-30);
            return await _context.ServiceOrders
                .Include(o => o.Vehicle)
                .Include(o => o.Items).ThenInclude(i => i.Mechanic)
                .Include(o => o.Attachments)
                .Where(o => o.IsDeleted && o.DeletionDate >= threshold)
                .OrderByDescending(o => o.DeletionDate).ToListAsync();
        }

        [HttpPost]
        public async Task<ActionResult<ServiceOrder>> PostServiceOrder(CreateOSDTO request)
        {
            var newVehicle = new Vehicle { CustomerName = request.ClientName, Model = request.VehicleModel, LicensePlate = "SEM-PLACA", CustomerAddress = request.CustomerAddress, CustomerPhone = request.CustomerPhone };
            _context.Vehicles.Add(newVehicle);
            await _context.SaveChangesAsync();

            var os = new ServiceOrder { VehicleId = newVehicle.Id, EntryDate = DateTime.Now, Status = "Pending", TotalAmount = 0, AmountPaid = 0 };
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
            if (product == null) return BadRequest();
            if (product.StockQuantity < itemDto.Quantity) return BadRequest("Estoque insuficiente.");

            var newItem = new ServiceItem
            {
                ServiceOrderId = id,
                ProductId = product.Id,
                Description = $"{product.Code} - {product.Name}",
                Price = product.SalePrice * itemDto.Quantity,
                WarrantyPeriod = itemDto.WarrantyPeriod,
                Quantity = itemDto.Quantity
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

            var newItem = new ServiceItem { ServiceOrderId = id, ProductId = null, MechanicId = mechanic.Id, Mechanic = mechanic, Description = laborDto.Description, Price = laborDto.Price, WarrantyPeriod = laborDto.WarrantyPeriod, Quantity = 1 };

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

        [HttpPut("{id}/payment")]
        public async Task<IActionResult> UpdateAmountPaid(int id, [FromBody] UpdatePaymentDTO request)
        {
            var os = await _context.ServiceOrders.FindAsync(id);
            if (os == null) return NotFound();

            os.AmountPaid = request.AmountPaid;
            os.PaymentMethod = request.PaymentMethod;
            os.PromisedPaymentDate = request.PromisedPaymentDate;

            await _context.SaveChangesAsync();
            return NoContent();
        }

        [HttpPut("{id}/items/{itemId}")]
        public async Task<IActionResult> UpdateServiceItem(int id, int itemId, [FromBody] UpdateServiceItemDTO request)
        {
            var item = await _context.ServiceItems.FirstOrDefaultAsync(i => i.Id == itemId && i.ServiceOrderId == id);
            if (item == null) return NotFound();

            if (item.ProductId != null && item.Quantity != request.Quantity)
            {
                var product = await _context.Products.FindAsync(item.ProductId);
                if (product != null)
                {
                    int diferenca = request.Quantity - item.Quantity;
                    if (diferenca > 0 && product.StockQuantity < diferenca)
                    {
                        return BadRequest("Estoque insuficiente para essa nova quantidade.");
                    }
                    product.StockQuantity -= diferenca;
                }
            }

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

        [HttpPost("{id}/attachments")]
        public async Task<IActionResult> AddAttachment(int id, [FromBody] UploadAttachmentDTO request)
        {
            var os = await _context.ServiceOrders.FindAsync(id);
            if (os == null) return NotFound();

            var attachment = new ServiceOrderAttachment
            {
                ServiceOrderId = id,
                FileName = request.FileName,
                FileType = request.FileType,
                Base64Content = request.Base64Content
            };

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

        public class CreateOSDTO { public string ClientName { get; set; } = ""; public string VehicleModel { get; set; } = ""; public string CustomerAddress { get; set; } = ""; public string CustomerPhone { get; set; } = ""; }
        public class UpdateVehicleDTO { public string CustomerName { get; set; } = ""; public string VehicleModel { get; set; } = ""; public string CustomerAddress { get; set; } = ""; public string CustomerPhone { get; set; } = ""; }
        public class AddItemDTO { public int ProductId { get; set; } public int Quantity { get; set; } public string? WarrantyPeriod { get; set; } }
        public class AddLaborDTO { public int MechanicId { get; set; } public string Description { get; set; } = ""; public decimal Price { get; set; } public string? WarrantyPeriod { get; set; } }
        public class CompletionDTO { public DateTime CompletionDate { get; set; } }
        public class UpdateTotalDTO { public decimal TotalAmount { get; set; } }
        public class UpdateServiceItemDTO { public string Description { get; set; } = ""; public decimal Price { get; set; } public string? WarrantyPeriod { get; set; } public int Quantity { get; set; } = 1; }

        public class UpdatePaymentDTO
        {
            public decimal AmountPaid { get; set; }
            public string? PaymentMethod { get; set; }
            public DateTime? PromisedPaymentDate { get; set; }
        }

        public class UploadAttachmentDTO { public string FileName { get; set; } = ""; public string FileType { get; set; } = ""; public string Base64Content { get; set; } = ""; }
    }
}