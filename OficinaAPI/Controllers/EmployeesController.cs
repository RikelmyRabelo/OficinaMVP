using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OficinaAPI.Data;
using OficinaAPI.Models;

namespace OficinaAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class EmployeesController : ControllerBase
    {
        private readonly OficinaContext _context;

        public EmployeesController(OficinaContext context)
        {
            _context = context;
        }

        // GET: api/employees
        [HttpGet]
        public async Task<ActionResult<IEnumerable<Employee>>> GetEmployees()
        {
            return await _context.Employees
                .Include(e => e.Payments) // Carrega a lista de pagamentos junto
                .ToListAsync();
        }

        // POST: api/employees
        [HttpPost]
        public async Task<ActionResult<Employee>> PostEmployee(Employee employee)
        {
            _context.Employees.Add(employee);
            await _context.SaveChangesAsync();
            return CreatedAtAction("GetEmployees", new { id = employee.Id }, employee);
        }

        // POST: api/employees/5/payments
        [HttpPost("{id}/payments")]
        public async Task<ActionResult<PaymentRecord>> AddPayment(int id, PaymentRecord payment)
        {
            var employee = await _context.Employees.FindAsync(id);
            if (employee == null) return NotFound("Funcionário não encontrado.");

            payment.EmployeeId = id;
            // Garante que nasce como não pago e sem data
            payment.IsPaid = false;
            payment.PaymentDate = null;

            _context.PaymentRecords.Add(payment);
            await _context.SaveChangesAsync();

            return Ok(payment);
        }

        // PUT: api/employees/payments/10/confirm
        [HttpPut("payments/{paymentId}/confirm")]
        public async Task<IActionResult> ConfirmPayment(int paymentId)
        {
            var payment = await _context.PaymentRecords.FindAsync(paymentId);
            if (payment == null) return NotFound("Pagamento não encontrado.");

            // ATUALIZAÇÃO SOLICITADA:
            payment.IsPaid = true;
            payment.PaymentDate = DateTime.Now; // <--- Salva a data/hora atual
            payment.AdminNotes += " | Confirmado via Web";

            await _context.SaveChangesAsync();

            return Ok(payment);
        }

        // DELETE: api/employees/5
        // NOVO MÉTODO PARA EXCLUIR
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteEmployee(int id)
        {
            var employee = await _context.Employees.FindAsync(id);
            if (employee == null)
            {
                return NotFound();
            }

            try
            {
                _context.Employees.Remove(employee);
                await _context.SaveChangesAsync();
                return NoContent();
            }
            catch (Exception)
            {
                // Retorna erro 409 (Conflict) se o funcionário tiver pagamentos vinculados
                return StatusCode(409, new { message = "Não é possível excluir este funcionário pois existem pagamentos registrados no histórico." });
            }
        }
    }
}