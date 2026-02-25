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

                // Pastas para salvar os arquivos
                string pastaAnexos = Path.Combine(pastaDestino, "Anexos");
                if (!Directory.Exists(pastaAnexos)) Directory.CreateDirectory(pastaAnexos);

                string pastaOS = Path.Combine(pastaDestino, "Ordens_de_Servico");
                if (!Directory.Exists(pastaOS)) Directory.CreateDirectory(pastaOS);

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

                // 2. VENDAS (ORDENS CONCLUÍDAS COM EXTRAÇÃO DOS ARQUIVOS E GERAÇÃO DA O.S.)
                var wsVendas = workbook.Worksheets.Add("Vendas");
                var ordensBanco = await _context.ServiceOrders
                    .Include(s => s.Vehicle)
                    .Include(s => s.Items).ThenInclude(i => i.Mechanic)
                    .Include(s => s.Attachments)
                    .Where(s => s.Status == "Completed")
                    .ToListAsync();

                // Lista que vai efetivamente preencher as linhas do Excel
                var vendasParaExcel = new List<object>();

                foreach (var s in ordensBanco)
                {
                    // A) Extrair Anexos (Comprovantes/Fotos)
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

                    // B) Gerar a O.S. em HTML (Visual de impressão)
                    string nomeClienteSeguro = string.IsNullOrWhiteSpace(s.Vehicle?.CustomerName) ? "Sem_Nome" : string.Join("_", s.Vehicle.CustomerName.Split(Path.GetInvalidFileNameChars()));
                    string nomeArquivoOS = $"OS_{s.Id}_{nomeClienteSeguro}.html";
                    string caminhoHtmlOS = Path.Combine(pastaOS, nomeArquivoOS);

                    string htmlOS = GerarTemplateHtmlOS(s);
                    System.IO.File.WriteAllText(caminhoHtmlOS, htmlOS, Encoding.UTF8);

                    // C) Adicionar os dados formatados à linha da planilha
                    vendasParaExcel.Add(new
                    {
                        OS = s.Id,
                        Cliente = s.Vehicle != null ? s.Vehicle.CustomerName : "",
                        Total = s.TotalAmount,
                        Pago = s.AmountPaid,
                        Data_Entrada = s.EntryDate,
                        Data_Saída = s.CompletionDate,
                        Documento_OS = nomeArquivoOS,
                        Anexos = string.Join(" | ", nomesArquivos)
                    });
                }

                wsVendas.Cell(1, 1).InsertTable(vendasParaExcel);
                wsVendas.Column(3).Style.NumberFormat.Format = "R$ #,##0.00";
                wsVendas.Column(4).Style.NumberFormat.Format = "R$ #,##0.00";
                wsVendas.Column(5).Style.NumberFormat.Format = "dd/mm/yyyy hh:mm";
                wsVendas.Column(6).Style.NumberFormat.Format = "dd/mm/yyyy hh:mm";

                // 3. EQUIPE (GERAL)
                var wsEquipe = workbook.Worksheets.Add("Equipe");
                var equipe = await _context.Employees.Select(e => new {
                    Nome = e.Name,
                    Cargo = e.Role,
                    Salário_Base = e.BaseSalary
                }).ToListAsync();
                wsEquipe.Cell(1, 1).InsertTable(equipe);
                wsEquipe.Column(3).Style.NumberFormat.Format = "R$ #,##0.00";

                // 4. PAGAMENTOS (COM DATA E HORA)
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
                wsPagos.Column(4).Style.NumberFormat.Format = "dd/mm/yyyy hh:mm:ss";

                // Ajustes finais de layout
                foreach (var ws in workbook.Worksheets)
                {
                    ws.Columns().AdjustToContents();
                    ws.SheetView.FreezeRows(1);
                }

                string nomeArquivoExcel = $"Relatorio_Oficina_{DateTime.Now:HHmm}.xlsx";
                string caminhoExcel = Path.Combine(pastaDestino, nomeArquivoExcel);
                workbook.SaveAs(caminhoExcel);

                return Ok(new { mensagem = "Planilha e arquivos gerados!", caminho = caminhoExcel });
            }
            catch (Exception ex) { return BadRequest(new { erro = ex.Message }); }
        }

        // --- FUNÇÃO PARA CRIAR O VISUAL DA O.S EM HTML ---
        private string GerarTemplateHtmlOS(ServiceOrder os)
        {
            var sb = new StringBuilder();
            sb.AppendLine("<!DOCTYPE html><html><head><meta charset='UTF-8'><title>O.S. #" + os.Id + "</title>");
            sb.AppendLine("<style>");
            sb.AppendLine("body { font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif; padding: 40px; color: #333; max-width: 800px; margin: auto; background-color: #f9f9f9; }");
            sb.AppendLine(".container { background-color: #fff; padding: 30px; border-radius: 8px; box-shadow: 0 4px 8px rgba(0,0,0,0.1); border-top: 6px solid #ff6600; }");
            sb.AppendLine(".header { text-align: center; margin-bottom: 30px; border-bottom: 2px solid #eee; padding-bottom: 20px; }");
            sb.AppendLine(".header h1 { color: #ff6600; margin: 0; }");
            sb.AppendLine(".header h2 { color: #555; margin-top: 5px; }");
            sb.AppendLine(".info-grid { display: grid; grid-template-columns: 1fr 1fr; gap: 15px; margin-bottom: 30px; }");
            sb.AppendLine(".info-box { background: #fdfdfd; border: 1px solid #eee; padding: 15px; border-radius: 5px; }");
            sb.AppendLine("table { width: 100%; border-collapse: collapse; margin-top: 20px; font-size: 14px; }");
            sb.AppendLine("th, td { border: 1px solid #ddd; padding: 12px; text-align: left; }");
            sb.AppendLine("th { background-color: #f8f9fa; color: #333; }");
            sb.AppendLine(".totals { margin-top: 30px; font-size: 16px; text-align: right; padding-top: 20px; border-top: 2px solid #eee; }");
            sb.AppendLine(".totals p { margin: 5px 0; }");
            sb.AppendLine(".total-destaque { font-size: 20px; font-weight: bold; color: #28a745; }");
            sb.AppendLine("</style></head><body>");

            sb.AppendLine("<div class='container'>");

            sb.AppendLine("<div class='header'>");
            sb.AppendLine("<h1>FJ CENTRO AUTOMOTIVO</h1>");
            sb.AppendLine($"<h2>Ordem de Serviço #{os.Id}</h2>");
            sb.AppendLine("</div>");

            sb.AppendLine("<div class='info-grid'>");
            sb.AppendLine("<div class='info-box'>");
            sb.AppendLine("<h4>Dados do Cliente</h4>");
            sb.AppendLine($"<p><strong>Nome:</strong> {os.Vehicle?.CustomerName}</p>");
            sb.AppendLine($"<p><strong>Telefone:</strong> {os.Vehicle?.CustomerPhone}</p>");
            sb.AppendLine($"<p><strong>Endereço:</strong> {os.Vehicle?.CustomerAddress}</p>");
            sb.AppendLine("</div>");

            sb.AppendLine("<div class='info-box'>");
            sb.AppendLine("<h4>Dados do Veículo</h4>");
            sb.AppendLine($"<p><strong>Modelo/Placa:</strong> {os.Vehicle?.Model}</p>");
            sb.AppendLine($"<p><strong>Data de Entrada:</strong> {os.EntryDate:dd/MM/yyyy HH:mm}</p>");
            sb.AppendLine($"<p><strong>Finalização:</strong> {os.CompletionDate?.ToString("dd/MM/yyyy HH:mm")}</p>");
            sb.AppendLine("</div>");
            sb.AppendLine("</div>");

            sb.AppendLine("<h3>Itens e Serviços Realizados</h3>");
            sb.AppendLine("<table>");
            sb.AppendLine("<tr><th>Cód / Tipo</th><th>Descrição</th><th>Garantia</th><th style='text-align: right;'>Valor (R$)</th></tr>");

            foreach (var item in os.Items)
            {
                string tipo = item.ProductId != null ? "Peça" : "Serviço";
                string mec = item.Mechanic != null ? $" <small>({item.Mechanic.Name})</small>" : "";

                sb.AppendLine("<tr>");
                sb.AppendLine($"<td>{tipo}</td>");
                sb.AppendLine($"<td>{item.Description}{mec}</td>");
                sb.AppendLine($"<td>{item.WarrantyPeriod ?? "-"}</td>");
                sb.AppendLine($"<td style='text-align: right;'>{item.Price:N2}</td>");
                sb.AppendLine("</tr>");
            }
            sb.AppendLine("</table>");

            sb.AppendLine("<div class='totals'>");
            sb.AppendLine($"<p>Subtotal: R$ {os.TotalAmount:N2}</p>");
            sb.AppendLine($"<p>Valor Pago: R$ {os.AmountPaid:N2}</p>");
            sb.AppendLine($"<p class='total-destaque'>Falta Receber: R$ {(os.TotalAmount - os.AmountPaid):N2}</p>");
            sb.AppendLine("</div>");

            sb.AppendLine("</div>"); // Fim do container
            sb.AppendLine("</body></html>");

            return sb.ToString();
        }
    }
}