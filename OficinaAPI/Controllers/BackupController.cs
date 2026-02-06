using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OficinaAPI.Data;
using OficinaAPI.Models;
using ClosedXML.Excel;

namespace OficinaAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class BackupController : ControllerBase
    {
        private readonly OficinaContext _context;
        private readonly string _pastaRaiz = @"D:\Backups_Oficina";

        public BackupController(OficinaContext context)
        {
            _context = context;
        }

        // Cria a pasta do dia se não existir e retorna o caminho
        private string ObterPastaDoDia()
        {
            string nomePasta = DateTime.Now.ToString("dd-MM-yyyy");
            string caminhoCompleto = Path.Combine(_pastaRaiz, nomePasta);

            if (!Directory.Exists(caminhoCompleto))
                Directory.CreateDirectory(caminhoCompleto);

            return caminhoCompleto;
        }

        [HttpPost("gerar")]
        public IActionResult GerarBackup()
        {
            try
            {
                string pastaDestino = ObterPastaDoDia();
                string nomeArquivo = $"Sistema_Oficina_{DateTime.Now:HHmm}.bak";
                string caminhoCompleto = Path.Combine(pastaDestino, nomeArquivo);

                // Comando para backup do banco configurado no appsettings.json
                _context.Database.ExecuteSqlRaw($@"BACKUP DATABASE [OficinaMVP] TO DISK = '{caminhoCompleto}'");

                return Ok(new { mensagem = "Backup concluído", caminho = caminhoCompleto });
            }
            catch (Exception ex) { return BadRequest(new { erro = ex.Message }); }
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
                var produtos = await _context.Products.Select(p => new {
                    Código = p.Code,
                    Descrição = p.Name,
                    Preço = p.SalePrice,
                    Estoque = p.StockQuantity,
                    Mínimo = p.MinimumStock
                }).ToListAsync();
                wsEstoque.Cell(1, 1).InsertTable(produtos);
                wsEstoque.Column(3).Style.NumberFormat.Format = "R$ #,##0.00";

                // 2. VENDAS (ORDENS CONCLUÍDAS)
                var wsVendas = workbook.Worksheets.Add("Vendas");
                var vendas = await _context.ServiceOrders.Include(s => s.Vehicle)
                    .Where(s => s.Status == "Completed")
                    .Select(s => new {
                        OS = s.Id,
                        Cliente = s.Vehicle != null ? s.Vehicle.CustomerName : "",
                        Total = s.TotalAmount,
                        Data_Entrada = s.EntryDate,
                        Data_Saída = s.CompletionDate
                    }).ToListAsync();
                wsVendas.Cell(1, 1).InsertTable(vendas);
                wsVendas.Column(3).Style.NumberFormat.Format = "R$ #,##0.00";
                wsVendas.Column(4).Style.NumberFormat.Format = "dd/mm/yyyy hh:mm";
                wsVendas.Column(5).Style.NumberFormat.Format = "dd/mm/yyyy hh:mm";

                // 3. EQUIPE (GERAL)
                var wsEquipe = workbook.Worksheets.Add("Equipe");
                var equipe = await _context.Employees.Select(e => new {
                    Nome = e.Name,
                    Cargo = e.Role,
                    Salário_Base = e.BaseSalary
                }).ToListAsync();
                wsEquipe.Cell(1, 1).InsertTable(equipe);
                wsEquipe.Column(3).Style.NumberFormat.Format = "R$ #,##0.00";

                // 4. PAGAMENTOS (COM DATA E HORA) - Pedido pelo usuário
                var wsPagos = workbook.Worksheets.Add("Histórico de Pagamentos");
                var pagamentos = await _context.PaymentRecords
                    .Include(p => p.Employee)
                    .Select(p => new {
                        Funcionário = p.Employee != null ? p.Employee.Name : "N/A",
                        Valor = p.Amount,
                        Status = p.IsPaid ? "Pago" : "Pendente",
                        Data_Hora = p.PaymentDate,
                        Notas = p.AdminNotes ?? ""
                    }).ToListAsync();
                wsPagos.Cell(1, 1).InsertTable(pagamentos);
                wsPagos.Column(2).Style.NumberFormat.Format = "R$ #,##0.00";
                // Formatação para exibir Data e Hora exata na planilha
                wsPagos.Column(4).Style.NumberFormat.Format = "dd/mm/yyyy hh:mm:ss";

                // Ajustes finais de layout
                foreach (var ws in workbook.Worksheets)
                {
                    ws.Columns().AdjustToContents();
                    ws.SheetView.FreezeRows(1);
                }

                string nomeArquivo = $"Relatorio_Oficina_{DateTime.Now:HHmm}.xlsx";
                string caminhoCompleto = Path.Combine(pastaDestino, nomeArquivo);
                workbook.SaveAs(caminhoCompleto);

                return Ok(new { mensagem = "Planilha Excel gerada!", caminho = caminhoCompleto });
            }
            catch (Exception ex) { return BadRequest(new { erro = ex.Message }); }
        }
    }
}