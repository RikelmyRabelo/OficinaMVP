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

        public BackupController(OficinaContext context)
        {
            _context = context;
        }

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

                string pastaAnexos = Path.Combine(pastaDestino, "Anexos");
                if (!Directory.Exists(pastaAnexos)) Directory.CreateDirectory(pastaAnexos);

                string pastaOS = Path.Combine(pastaDestino, "Ordens_de_Servico");
                if (!Directory.Exists(pastaOS)) Directory.CreateDirectory(pastaOS);

                // --- FILTRO DE SEGURANÇA PARA FUNCIONÁRIOS REMOVIDOS ---
                var nomesParaOcultar = new List<string> { "Taylor", "Felipe" };

                // 1. ESTOQUE
                var wsEstoque = workbook.Worksheets.Add("Estoque");
                var produtos = await _context.Products
                    .AsNoTracking()
                    .Select(p => new {
                        Código = p.Code,
                        Descrição = p.Name,
                        Preço = p.SalePrice,
                        Estoque = p.StockQuantity,
                        Mínimo = p.MinimumStock
                    }).ToListAsync();
                wsEstoque.Cell(1, 1).InsertTable(produtos);
                wsEstoque.Column(3).Style.NumberFormat.Format = "R$ #,##0.00";

                // 2. VENDAS (Mantém filtro de lixeira da O.S.)
                var wsVendas = workbook.Worksheets.Add("Vendas");
                var ordensBanco = await _context.ServiceOrders
                    .Include(s => s.Vehicle)
                    .Include(s => s.Items).ThenInclude(i => i.Mechanic)
                    .Include(s => s.Attachments)
                    .Where(s => s.Status == "Completed" && !s.IsDeleted)
                    .ToListAsync();

                var vendasParaExcel = new List<object>();

                foreach (var s in ordensBanco)
                {
                    List<string> nomesArquivos = new List<string>();
                    if (s.Attachments != null && s.Attachments.Any())
                    {
                        foreach (var a in s.Attachments)
                        {
                            try
                            {
                                string base64Data = a.Base64Content;
                                if (base64Data.Contains(",")) base64Data = base64Data.Substring(base64Data.IndexOf(",") + 1);
                                string nomeSeguro = $"OS{s.Id}_{a.FileName.Replace(" ", "_")}";
                                string caminhoArquivoFisico = Path.Combine(pastaAnexos, nomeSeguro);
                                System.IO.File.WriteAllBytes(caminhoArquivoFisico, Convert.FromBase64String(base64Data));
                                nomesArquivos.Add(nomeSeguro);
                            }
                            catch { nomesArquivos.Add($"{a.FileName} (Erro)"); }
                        }
                    }

                    string nomeClienteSeguro = string.IsNullOrWhiteSpace(s.Vehicle?.CustomerName) ? "Sem_Nome" : string.Join("_", s.Vehicle.CustomerName.Split(Path.GetInvalidFileNameChars()));
                    string nomeArquivoOS = $"OS_{s.Id}_{nomeClienteSeguro}.html";
                    string caminhoHtmlOS = Path.Combine(pastaOS, nomeArquivoOS);

                    string htmlOS = GerarTemplateHtmlOS(s);
                    System.IO.File.WriteAllText(caminhoHtmlOS, htmlOS, Encoding.UTF8);

                    vendasParaExcel.Add(new
                    {
                        OS = s.Id,
                        Cliente = s.Vehicle?.CustomerName ?? "",
                        Total = s.TotalAmount,
                        Pago = s.AmountPaid,
                        Falta_Pagar = Math.Max(0, s.TotalAmount - s.AmountPaid),
                        Extra_Caixa = Math.Max(0, s.AmountPaid - s.TotalAmount),
                        Forma_Pagamento = string.IsNullOrEmpty(s.PaymentMethod) ? "-" : s.PaymentMethod,
                        Promessa_Pagto = s.PromisedPaymentDate.HasValue ? s.PromisedPaymentDate.Value.ToString("dd/MM/yyyy") : "-",
                        Data_Entrada = s.EntryDate,
                        Data_Saída = s.CompletionDate,
                        Documento_OS = nomeArquivoOS,
                        Anexos = string.Join(" | ", nomesArquivos)
                    });
                }

                wsVendas.Cell(1, 1).InsertTable(vendasParaExcel);
                wsVendas.Column(3).Style.NumberFormat.Format = "R$ #,##0.00";
                wsVendas.Column(4).Style.NumberFormat.Format = "R$ #,##0.00";
                wsVendas.Column(5).Style.NumberFormat.Format = "R$ #,##0.00";
                wsVendas.Column(6).Style.NumberFormat.Format = "R$ #,##0.00";
                wsVendas.Column(9).Style.NumberFormat.Format = "dd/mm/yyyy hh:mm";
                wsVendas.Column(10).Style.NumberFormat.Format = "dd/mm/yyyy hh:mm";

                // 3. EQUIPE (Filtro por nome para Taylor e Felipe)
                var wsEquipe = workbook.Worksheets.Add("Equipe");
                var equipe = await _context.Employees
                    .AsNoTracking()
                    .Where(e => !nomesParaOcultar.Contains(e.Name))
                    .Select(e => new {
                        Nome = e.Name,
                        Cargo = e.Role,
                        Salário_Base = e.BaseSalary
                    }).ToListAsync();
                wsEquipe.Cell(1, 1).InsertTable(equipe);
                wsEquipe.Column(3).Style.NumberFormat.Format = "R$ #,##0.00";

                // 4. PAGAMENTOS (Filtro por nome para Taylor e Felipe)
                var wsPagos = workbook.Worksheets.Add("Histórico de Pagamentos");
                var pagamentos = await _context.PaymentRecords
                    .Include(p => p.Employee)
                    .Where(p => p.Employee != null && !nomesParaOcultar.Contains(p.Employee.Name))
                    .Select(p => new {
                        Funcionário = p.Employee.Name,
                        Valor = p.Amount,
                        Status = p.IsPaid ? "Pago" : "Pendente",
                        Data_Hora = p.PaymentDate,
                        Notas = p.AdminNotes ?? ""
                    }).ToListAsync();
                wsPagos.Cell(1, 1).InsertTable(pagamentos);
                wsPagos.Column(2).Style.NumberFormat.Format = "R$ #,##0.00";
                wsPagos.Column(4).Style.NumberFormat.Format = "dd/mm/yyyy hh:mm:ss";

                foreach (var ws in workbook.Worksheets)
                {
                    ws.Columns().AdjustToContents();
                    ws.SheetView.FreezeRows(1);
                }

                string nomeArquivoExcel = $"Relatorio_Oficina_{DateTime.Now:HHmm}.xlsx";
                string caminhoExcel = Path.Combine(pastaDestino, nomeArquivoExcel);
                workbook.SaveAs(caminhoExcel);

                return Ok(new { mensagem = "Relatório gerado com sucesso!", caminho = caminhoExcel });
            }
            catch (Exception ex) { return BadRequest(new { erro = ex.Message }); }
        }

        private string GerarTemplateHtmlOS(ServiceOrder os)
        {
            var sb = new StringBuilder();
            sb.AppendLine("<!DOCTYPE html><html><head><meta charset='UTF-8'>");
            sb.AppendLine("<style>");
            sb.AppendLine("body { font-family: sans-serif; padding: 20px; }");
            sb.AppendLine(".header { text-align: center; border-bottom: 2px solid #ff6600; padding-bottom: 10px; }");
            sb.AppendLine("table { width: 100%; border-collapse: collapse; margin-top: 20px; }");
            sb.AppendLine("th, td { border: 1px solid #ddd; padding: 8px; text-align: left; }");
            sb.AppendLine("th { background-color: #f2f2f2; }");
            sb.AppendLine("</style></head><body>");
            sb.AppendLine("<div class='header'><h1>FJ CENTRO AUTOMOTIVO</h1><h3>Ordem de Serviço #" + os.Id + "</h3></div>");
            sb.AppendLine($"<p><strong>Cliente:</strong> {os.Vehicle?.CustomerName}</p>");
            sb.AppendLine($"<p><strong>Veículo:</strong> {os.Vehicle?.Model} | Placa: {os.Vehicle?.LicensePlate}</p>");
            sb.AppendLine("<table><tr><th>Descrição</th><th>Valor</th></tr>");
            foreach (var item in os.Items)
            {
                sb.AppendLine($"<tr><td>{item.Description}</td><td>{item.Price:N2}</td></tr>");
            }
            sb.AppendLine("</table>");
            sb.AppendLine($"<p style='text-align:right'><strong>Total: R$ {os.TotalAmount:N2}</strong></p>");
            sb.AppendLine("</body></html>");
            return sb.ToString();
        }
    }
}