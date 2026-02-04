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

        [HttpGet]
        public async Task<ActionResult<IEnumerable<Employee>>> GetEmployees()
        {
            return await _context.Employees
                .Include(e => e.Payments)
                .ToListAsync();
        }

        [HttpPost]
        public async Task<ActionResult<Employee>> PostEmployee(Employee employee)
        {
            employee.Active = true;
            _context.Employees.Add(employee);
            await _context.SaveChangesAsync();
            return CreatedAtAction("GetEmployees", new { id = employee.Id }, employee);
        }

        [HttpPost("{id}/payments")]
        public async Task<ActionResult<PaymentRecord>> AddPayment(int id, PaymentRecord payment)
        {
            var employee = await _context.Employees.FindAsync(id);
            if (employee == null) return NotFound("Funcionário não encontrado.");

            payment.EmployeeId = id;
            payment.IsPaid = false;
            payment.PaymentDate = null;

            _context.PaymentRecords.Add(payment);
            await _context.SaveChangesAsync();

            return Ok(payment);
        }

        [HttpPut("payments/{paymentId}/confirm")]
        public async Task<IActionResult> ConfirmPayment(int paymentId)
        {
            var payment = await _context.PaymentRecords.FindAsync(paymentId);
            if (payment == null) return NotFound("Pagamento não encontrado.");

            payment.IsPaid = true;
            payment.PaymentDate = DateTime.Now;
            payment.AdminNotes += " | Confirmado via Web";

            await _context.SaveChangesAsync();

            return Ok(payment);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteEmployee(int id)
        {
            var employee = await _context.Employees.FindAsync(id);
            if (employee == null) return NotFound();

            employee.Active = false;
            await _context.SaveChangesAsync();

            return NoContent();
        }
    }
}