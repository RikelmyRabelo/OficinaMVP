using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OficinaAPI.Data;
using OficinaAPI.Models;
using ClosedXML.Excel;
using System.Text;

namespace OficinaAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class BackupController : ControllerBase
    {
        private readonly OficinaContext _context;
        private readonly string _pastaRaiz = @"C:\Backups_Oficina";

        public BackupController(OficinaContext context) { _context = context; }

        private string ObterPastaDoDia()
        {
            string nomePasta = DateTime.Now.ToString("dd-MM-yyyy");
            string caminhoCompleto = Path.Combine(_pastaRaiz, nomePasta);
            if (!Directory.Exists(caminhoCompleto)) Directory.CreateDirectory(caminhoCompleto);
            return caminhoCompleto;
        }

        [HttpPost("exportar-excel")]
        public async Task<IActionResult> ExportarExcel()
        {
            try
            {
                string pastaDestino = ObterPastaDoDia();
                using var workbook = new XLWorkbook();
                var nomesParaOcultar = new List<string> { "Taylor", "Felipe" };

                // 1. ESTOQUE
                var wsEstoque = workbook.Worksheets.Add("Estoque");
                var produtos = await _context.Products.AsNoTracking().Select(p => new { Código = p.Code, Descrição = p.Name, Preço = p.SalePrice, Estoque = p.StockQuantity }).ToListAsync();
                wsEstoque.Cell(1, 1).InsertTable(produtos);

                // 2. VENDAS
                var wsVendas = workbook.Worksheets.Add("Vendas");
                var ordensBanco = await _context.ServiceOrders.Include(s => s.Vehicle).Include(s => s.Items)
                    .Where(s => s.Status == "Completed" && !s.IsDeleted).ToListAsync();

                var pastaOS = Path.Combine(pastaDestino, "Ordens_de_Servico");
                if (!Directory.Exists(pastaOS)) Directory.CreateDirectory(pastaOS);

                var vendasParaExcel = ordensBanco.Select(s => {
                    string nomeArquivoOS = $"OS_{s.Id}.html";
                    System.IO.File.WriteAllText(Path.Combine(pastaOS, nomeArquivoOS), GerarTemplateHtmlOS(s), Encoding.UTF8);
                    return new { OS = s.Id, Cliente = s.Vehicle?.CustomerName, Total = s.TotalAmount, Pago = s.AmountPaid, Forma = s.PaymentMethod };
                }).ToList();
                wsVendas.Cell(1, 1).InsertTable(vendasParaExcel);

                // 3. EQUIPE
                var wsEquipe = workbook.Worksheets.Add("Equipe");
                var equipe = await _context.Employees.AsNoTracking().Where(e => !nomesParaOcultar.Contains(e.Name))
                    .Select(e => new { Nome = e.Name, Cargo = e.Role, Salário = e.BaseSalary }).ToListAsync();
                wsEquipe.Cell(1, 1).InsertTable(equipe);

                // 4. PAGAMENTOS
                var wsPagos = workbook.Worksheets.Add("Histórico de Pagamentos");
                var pagamentos = await _context.PaymentRecords.Include(p => p.Employee)
                    .Where(p => p.Employee != null && !nomesParaOcultar.Contains(p.Employee.Name))
                    .Select(p => new { Funcionario = p.Employee.Name, Valor = p.Amount, Data = p.PaymentDate }).ToListAsync();
                wsPagos.Cell(1, 1).InsertTable(pagamentos);

                string caminhoExcel = Path.Combine(pastaDestino, $"Relatorio_{DateTime.Now:HHmm}.xlsx");
                workbook.SaveAs(caminhoExcel);
                return Ok(new { mensagem = "Backup Gerado!", caminho = caminhoExcel });
            }
            catch (Exception ex) { return BadRequest(new { erro = ex.Message }); }
        }

        private string GerarTemplateHtmlOS(ServiceOrder os)
        {
            var sb = new StringBuilder();
            sb.AppendLine("<html><body style='font-family:sans-serif;'><h1>FJ CENTRO AUTOMOTIVO - OS #" + os.Id + "</h1>");
            sb.AppendLine("<table border='1' width='100%'><tr><th>Item</th><th>Garantia</th><th>Preço</th></tr>");
            foreach (var item in os.Items)
            {
                string statusGarantia = "Sem Garantia";
                if (item.WarrantyExpirationDate.HasValue)
                {
                    statusGarantia = item.WarrantyExpirationDate.Value >= DateTime.Today
                        ? $"VÁLIDA (até {item.WarrantyExpirationDate.Value:dd/MM/yyyy})"
                        : $"EXPIRADA (em {item.WarrantyExpirationDate.Value:dd/MM/yyyy})";
                }
                sb.AppendLine($"<tr><td>{item.Description}</td><td>{statusGarantia}</td><td>{item.Price:N2}</td></tr>");
            }
            sb.AppendLine("</table><p>Total: " + os.TotalAmount.ToString("C2") + "</p></body></html>");
            return sb.ToString();
        }
    }
}