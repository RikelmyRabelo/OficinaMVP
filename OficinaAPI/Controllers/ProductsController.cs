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
            return await _context.Products.ToListAsync();
        }

        [HttpGet("low-stock")]
        public async Task<ActionResult<IEnumerable<Product>>> GetLowStock()
        {
            return await _context.Products
                .Where(p => p.StockQuantity <= 3)
                .OrderBy(p => p.StockQuantity)
                .ToListAsync();
        }

        [HttpGet("busca/{code}")]
        public async Task<ActionResult<Product>> GetProductByCode(string code)
        {
            var product = await _context.Products
                .FirstOrDefaultAsync(p => p.Code == code);

            if (product == null)
            {
                return NotFound(new { message = "Produto não encontrado com esse código." });
            }

            return product;
        }

        [HttpGet("historical-value")]
        public async Task<ActionResult<object>> GetHistoricalStockValue()
        {
            // 1. Valor total do que está no estoque agora
            var currentStockValue = await _context.Products
                .SumAsync(p => p.StockQuantity * p.SalePrice);

            // 2. Valor total do que já saiu em O.S. (somente itens com código/ProductId)
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
            if (await _context.Products.AnyAsync(p => p.Code == product.Code))
            {
                return BadRequest("Já existe um produto com este código.");
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
                if (!_context.Products.Any(e => e.Id == id)) return NotFound();
                else throw;
            }

            return NoContent();
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteProduct(int id)
        {
            var product = await _context.Products.FindAsync(id);
            if (product == null)
            {
                return NotFound();
            }

            try
            {
                _context.Products.Remove(product);
                await _context.SaveChangesAsync();
                return NoContent();
            }
            catch (Exception)
            {
                return StatusCode(409, new { message = "Não é possível excluir este produto pois ele já está vinculado a uma Ordem de Serviço." });
            }
        }
    }
}