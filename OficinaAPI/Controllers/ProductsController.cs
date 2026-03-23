using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OficinaAPI.Data;
using OficinaAPI.Models;

namespace OficinaAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ProductsController : ControllerBase
    {
        private readonly OficinaContext _context;

        public ProductsController(OficinaContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<Product>>> GetProducts()
        {
            return await _context.Products.Where(p => !p.IsDeleted).ToListAsync();
        }

        [HttpGet("low-stock")]
        public async Task<ActionResult<IEnumerable<Product>>> GetLowStock()
        {
            return await _context.Products
                .Where(p => !p.IsDeleted && p.StockQuantity <= 3)
                .OrderBy(p => p.StockQuantity)
                .ToListAsync();
        }

        [HttpGet("busca/{code}")]
        public async Task<ActionResult<Product>> GetProductByCode(string code)
        {
            var product = await _context.Products
                .FirstOrDefaultAsync(p => p.Code == code && !p.IsDeleted);

            if (product == null)
            {
                return NotFound(new { message = "Produto não encontrado ou removido." });
            }

            return product;
        }

        [HttpGet("historical-value")]
        public async Task<ActionResult<object>> GetHistoricalStockValue()
        {
            var currentStockValue = await _context.Products
                .Where(p => !p.IsDeleted)
                .SumAsync(p => p.StockQuantity * p.SalePrice);

            var exitedItemsValue = await _context.ServiceItems
                .Where(si => si.ProductId != null)
                .SumAsync(si => si.Quantity * si.Price);

            return Ok(new
            {
                currentInventoryValue = currentStockValue,
                totalExitedValue = exitedItemsValue,
                totalHistoricalValue = currentStockValue + exitedItemsValue
            });
        }

        [HttpPost]
        public async Task<ActionResult<Product>> PostProduct(Product product)
        {
            var existing = await _context.Products.FirstOrDefaultAsync(p => p.Code == product.Code);

            if (existing != null)
            {
                if (existing.IsDeleted)
                {
                    existing.IsDeleted = false;
                    existing.Name = product.Name;
                    existing.SalePrice = product.SalePrice;
                    existing.StockQuantity = product.StockQuantity;
                    existing.MinimumStock = product.MinimumStock;
                    await _context.SaveChangesAsync();
                    return Ok(existing);
                }
                return BadRequest("Já existe um produto ativo com este código.");
            }

            _context.Products.Add(product);
            await _context.SaveChangesAsync();

            return CreatedAtAction("GetProductByCode", new { code = product.Code }, product);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateProduct(int id, Product product)
        {
            if (id != product.Id) return BadRequest();

            _context.Entry(product).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!_context.Products.Any(e => e.Id == id && !e.IsDeleted)) return NotFound();
                else throw;
            }

            return NoContent();
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteProduct(int id)
        {
            var product = await _context.Products.FindAsync(id);
            if (product == null) return NotFound();

            product.IsDeleted = true;
            await _context.SaveChangesAsync();
            return NoContent();
        }
    }
}