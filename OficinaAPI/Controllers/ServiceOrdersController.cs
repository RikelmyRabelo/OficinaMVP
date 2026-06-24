using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OficinaAPI.Data;
using OficinaAPI.Models;
using OficinaAPI.DTOs;
using OficinaAPI.Services;

namespace OficinaAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ServiceOrdersController : ControllerBase
    {
        private readonly OficinaContext _context;
        private readonly IOSService _osService;

        public ServiceOrdersController(OficinaContext context, IOSService osService)
        {
            _context = context;
            _osService = osService;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<ServiceOrder>>> GetServiceOrders([FromQuery] int skip = 0, [FromQuery] int take = 100)
        {
            return await _context.ServiceOrders.AsNoTracking()
                .Include(o => o.Vehicle)
                .Include(o => o.Items)
                .Include(o => o.Payments)
                .OrderByDescending(o => o.Id)
                .Skip(skip)
                .Take(take)
                .ToListAsync();
        }

        [HttpGet("active")]
        public async Task<ActionResult<IEnumerable<ServiceOrder>>> GetActiveServiceOrders([FromQuery] int skip = 0, [FromQuery] int take = 100)
        {
            return await _context.ServiceOrders.AsNoTracking()
                .Include(o => o.Vehicle)
                .Where(o => o.Status != "Completed")
                .OrderByDescending(o => o.Id)
                .Skip(skip)
                .Take(take)
                .ToListAsync();
        }

        [HttpGet("completed")]
        public async Task<ActionResult<IEnumerable<ServiceOrder>>> GetCompletedServiceOrders([FromQuery] int skip = 0, [FromQuery] int take = 50)
        {
            return await _context.ServiceOrders.AsNoTracking()
                .Include(o => o.Vehicle)
                .Where(o => o.Status == "Completed")
                .OrderByDescending(o => o.Id)
                .Skip(skip)
                .Take(take)
                .ToListAsync();
        }

        [HttpGet("periodo")]
        public async Task<ActionResult<IEnumerable<ServiceOrder>>> GetServiceOrdersByPeriod([FromQuery] int mes, [FromQuery] int ano)
        {
            return await _context.ServiceOrders.AsNoTracking()
                .Where(o => o.AccountingMonth == mes && o.AccountingYear == ano)
                .OrderByDescending(o => o.Id)
                .ToListAsync();
        }

        [HttpGet("alerts")]
        public async Task<ActionResult<IEnumerable<ServiceOrder>>> GetCollectionAlerts()
        {
            var today = DateTime.Today;
            return await _context.ServiceOrders.AsNoTracking()
                .Where(o => o.Status == "Completed" && (o.TotalAmount - o.AmountPaid) > 0 && o.PromisedPaymentDate != null && o.PromisedPaymentDate.Value.Date <= today)
                .ToListAsync();
        }

        [HttpPost]
        public async Task<ActionResult<ServiceOrder>> PostServiceOrder([FromBody] CreateOSDTO request)
        {
            var settings = await _context.SystemSettings.AsNoTracking().FirstOrDefaultAsync();
            var os = await _osService.CreateOSAsync(request, settings?.ActiveMonth ?? DateTime.Now.Month, settings?.ActiveYear ?? DateTime.Now.Year);
            return Ok(os);
        }

        [HttpPost("{id}/items")]
        public async Task<ActionResult<ServiceItem>> AddItem(int id, [FromBody] AddItemDTO dto)
        {
            return Ok(await _osService.AddProductItemAsync(id, dto));
        }

        [HttpPost("{id}/labor")]
        public async Task<ActionResult<ServiceItem>> AddLabor(int id, [FromBody] AddLaborDTO dto)
        {
            return Ok(await _osService.AddLaborAsync(id, dto));
        }

        [HttpPost("{id}/custom-items")]
        public async Task<ActionResult<ServiceItem>> AddCustomItem(int id, [FromBody] AddCustomItemDTO dto)
        {
            return Ok(await _osService.AddCustomItemAsync(id, dto));
        }

        [HttpPut("{id}/complete")]
        public async Task<IActionResult> CompleteOrder(int id, [FromBody] CompletionDTO completion)
        {
            if (await _osService.CompleteOrderAsync(id, completion.CompletionDate)) return NoContent();
            return BadRequest("Erro ao finalizar O.S.");
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> SoftDelete(int id)
        {
            var os = await _context.ServiceOrders.FindAsync(id);
            if (os == null) return NotFound();
            os.IsDeleted = true;
            os.DeletionDate = DateTime.Now;
            await _context.SaveChangesAsync();
            return NoContent();
        }

        [HttpGet("trash")]
        public async Task<ActionResult<IEnumerable<ServiceOrder>>> GetTrash()
        {
            return await _context.ServiceOrders.IgnoreQueryFilters()
                .Where(o => o.IsDeleted).OrderByDescending(o => o.DeletionDate).ToListAsync();
        }

        [HttpPut("{id}/restore")]
        public async Task<IActionResult> Restore(int id)
        {
            var os = await _context.ServiceOrders.IgnoreQueryFilters().FirstOrDefaultAsync(o => o.Id == id);
            if (os == null) return NotFound();
            os.IsDeleted = false;
            os.DeletionDate = null;
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
            await _context.SaveChangesAsync();
            return NoContent();
        }
    }
}