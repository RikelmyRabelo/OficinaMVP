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

                // 1. ESTOQUE
                var wsEstoque = workbook.Worksheets.Add("Estoque");
                var produtos = await _context.Products.AsNoTracking()
                    .Select(p => new { Código = p.Code, Descrição = p.Name, Preço = p.SalePrice, Estoque = p.StockQuantity })
                    .ToListAsync();
                wsEstoque.Cell(1, 1).InsertTable(produtos);

                // 2. VENDAS (Ordens de Serviço)
                var wsVendas = workbook.Worksheets.Add("Vendas_OS");
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

                // 3. NOTAS (Antigos Avulsos e Notas Gerais)
                var wsNotas = workbook.Worksheets.Add("Notas_e_Avulsos");
                var notas = await _context.Notes.AsNoTracking()
                    .Select(n => new { Data = n.CreatedAt, Conteúdo = n.Content })
                    .ToListAsync();
                wsNotas.Cell(1, 1).InsertTable(notas);

                string caminhoExcel = Path.Combine(pastaDestino, $"Relatorio_{DateTime.Now:HHmm}.xlsx");
                workbook.SaveAs(caminhoExcel);

                return Ok(new { mensagem = "Relatórios Excel gerados!", caminho = caminhoExcel });
            }
            catch (Exception ex) { return BadRequest(new { erro = ex.Message }); }
        }

        [HttpPost("gerar")]
        public async Task<IActionResult> GerarBackupDatabase()
        {
            try
            {
                string pastaDestino = ObterPastaDoDia();
                string nomeArquivoBak = $"Backup_Sistema_{DateTime.Now:yyyyMMdd_HHmm}.bak";
                string caminhoCompletoBak = Path.Combine(pastaDestino, nomeArquivoBak);

                string dbName = _context.Database.GetDbConnection().Database;
                string sqlCommand = $"BACKUP DATABASE [{dbName}] TO DISK = '{caminhoCompletoBak}' WITH FORMAT, MEDIANAME = 'OficinaBackup', NAME = 'Full Backup of {dbName}';";

                await _context.Database.ExecuteSqlRawAsync(sqlCommand);

                return Ok(new { mensagem = "Arquivo .BAK gerado com sucesso!", caminho = caminhoCompletoBak });
            }
            catch (Exception ex)
            {
                return BadRequest(new { erro = "Erro ao gerar .BAK. Verifique permissões na pasta C:\\Backups_Oficina. Detalhe: " + ex.Message });
            }
        }

        private string GerarTemplateHtmlOS(ServiceOrder os)
        {
            var sb = new StringBuilder();
            sb.AppendLine("<html><body style='font-family:sans-serif;'><h1>FREITAS AUTOCENTER - OS #" + os.Id + "</h1>");
            sb.AppendLine("<table border='1' width='100%'><tr><th>Item</th><th>Preço</th></tr>");
            foreach (var item in os.Items) { sb.AppendLine($"<tr><td>{item.Description}</td><td>{item.Price:N2}</td></tr>"); }
            sb.AppendLine("</table><p>Total: " + os.TotalAmount.ToString("C2") + "</p></body></html>");
            return sb.ToString();
        }
    }
}