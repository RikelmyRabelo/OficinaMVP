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

        // --- AQUI ESTAVA O POSSÍVEL ERRO (FALTA DO HTTPPOST) ---
        [HttpPost]
        public async Task<ActionResult<ServiceOrder>> OpenServiceOrder(ServiceOrder order)
        {
            order.EntryDate = DateTime.Now;
            order.Status = "Pending";

            _context.ServiceOrders.Add(order);
            await _context.SaveChangesAsync();

            return CreatedAtAction("GetServiceOrder", new { id = order.Id }, order);
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<ServiceOrder>>> GetServiceOrders()
        {
            return await _context.ServiceOrders
                .Include(o => o.Vehicle)
                .ToListAsync();
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<ServiceOrder>> GetServiceOrder(int id)
        {
            var order = await _context.ServiceOrders
                .Include(o => o.Vehicle)
                .Include(o => o.Items)
                    .ThenInclude(i => i.Product)
                .Include(o => o.Items)
                    .ThenInclude(i => i.Mechanic)
                .FirstOrDefaultAsync(o => o.Id == id);

            if (order == null) return NotFound();

            return order;
        }

        [HttpPost("{orderId}/items")]
        public async Task<ActionResult<ServiceItem>> AddItem(int orderId, ServiceItem item)
        {
            var order = await _context.ServiceOrders.FindAsync(orderId);
            if (order == null) return NotFound("OS não encontrada.");

            item.ServiceOrderId = orderId;
            _context.ServiceItems.Add(item);
            await _context.SaveChangesAsync();

            return Ok(item);
        }

        [HttpPut("{id}/finish")]
        public async Task<IActionResult> FinishOrder(int id)
        {
            var order = await _context.ServiceOrders.Include(o => o.Items).FirstOrDefaultAsync(o => o.Id == id);
            if (order == null) return NotFound();

            decimal total = 0;
            foreach (var item in order.Items)
            {
                total += item.Price;
                item.WarrantyExpirationDate = DateTime.Now.AddDays(90);

                if (item.ProductId != null)
                {
                    var product = await _context.Products.FindAsync(item.ProductId);
                    if (product != null) product.StockQuantity -= 1;
                }
            }

            order.TotalAmount = total;
            order.Status = "Completed";
            order.CompletionDate = DateTime.Now;

            await _context.SaveChangesAsync();
            return Ok(new { message = "Finalizado", order });
        }
    }
}