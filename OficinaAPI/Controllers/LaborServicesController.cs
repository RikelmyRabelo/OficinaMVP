using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OficinaAPI.Data;
using OficinaAPI.Models;

namespace OficinaAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class LaborServicesController : ControllerBase
    {
        private readonly OficinaContext _context;

        public LaborServicesController(OficinaContext context) { _context = context; }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<LaborService>>> GetLaborServices()
        {
            return await _context.LaborServices.AsNoTracking().OrderBy(s => s.Name).ToListAsync();
        }

        [HttpPost]
        public async Task<ActionResult<LaborService>> PostLaborService(LaborService service)
        {
            _context.LaborServices.Add(service);
            await _context.SaveChangesAsync();
            return CreatedAtAction(nameof(GetLaborServices), new { id = service.Id }, service);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteLaborService(int id)
        {
            var service = await _context.LaborServices.FindAsync(id);
            if (service == null) return NotFound();
            _context.LaborServices.Remove(service);
            await _context.SaveChangesAsync();
            return NoContent();
        }
    }
}