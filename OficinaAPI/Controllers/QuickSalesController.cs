using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OficinaAPI.Data;
using OficinaAPI.Models;

namespace OficinaAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class QuickSalesController : ControllerBase
    {
        private readonly OficinaContext _context;
        public QuickSalesController(OficinaContext context) { _context = context; }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<QuickSale>>> GetQuickSales()
        {
            return await _context.QuickSales.OrderByDescending(q => q.SaleDate).ToListAsync();
        }

        [HttpPost]
        public async Task<ActionResult<QuickSale>> PostQuickSale(QuickSale quickSale)
        {
            quickSale.SaleDate = DateTime.Now;
            _context.QuickSales.Add(quickSale);
            await _context.SaveChangesAsync();
            return CreatedAtAction(nameof(GetQuickSales), new { id = quickSale.Id }, quickSale);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteQuickSale(int id)
        {
            var sale = await _context.QuickSales.FindAsync(id);
            if (sale == null) return NotFound();
            _context.QuickSales.Remove(sale);
            await _context.SaveChangesAsync();
            return NoContent();
        }
    }
}