using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OficinaAPI.Data;
using OficinaAPI.Models;
using ClosedXML.Excel;
using System.Text;
using System.Globalization;

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

                var wsEstoque = workbook.Worksheets.Add("Estoque");
                var produtos = await _context.Products.AsNoTracking()
                    .Select(p => new { Código = p.Code, Descrição = p.Name, Preço = p.SalePrice, Estoque = p.StockQuantity })
                    .ToListAsync();
                wsEstoque.Cell(1, 1).InsertTable(produtos);

                var wsVendas = workbook.Worksheets.Add("Vendas_OS");
                var ordensBanco = await _context.ServiceOrders.Include(s => s.Vehicle).Include(s => s.Items)
                    .Where(s => s.Status == "Completed" && !s.IsDeleted).ToListAsync();

                var pastaOS = Path.Combine(pastaDestino, "Ordens_de_Servico");
                if (!Directory.Exists(pastaOS)) Directory.CreateDirectory(pastaOS);

                foreach (var s in ordensBanco)
                {
                    string nomeArquivoOS = $"OS_{s.Id}.html";
                    System.IO.File.WriteAllText(Path.Combine(pastaOS, nomeArquivoOS), GerarTemplateHtmlOS(s), Encoding.UTF8);
                }

                var vendasParaExcel = ordensBanco.Select(s => new {
                    OS = s.Id,
                    Cliente = s.Vehicle?.CustomerName,
                    Total = s.TotalAmount,
                    Pago = s.AmountPaid,
                    Forma = s.PaymentMethod
                }).ToList();
                wsVendas.Cell(1, 1).InsertTable(vendasParaExcel);

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
                return BadRequest(new { erro = ex.Message });
            }
        }

        private string GerarTemplateHtmlOS(ServiceOrder os)
        {
            var sb = new StringBuilder();
            sb.AppendLine("<!DOCTYPE html>");
            sb.AppendLine("<html><head><meta charset='utf-8'/><style>");
            sb.AppendLine("body { margin: 0; padding: 0; background-color: #f0f0f0; display: flex; justify-content: center; }");
            sb.AppendLine(".print-container { position: relative; width: 210mm; height: 297mm; background-color: white; overflow: hidden; }");
            sb.AppendLine(".bg-img { position: absolute; width: 100%; height: 100%; top: 0; left: 0; z-index: 1; }");
            sb.AppendLine(".print-field { position: absolute; font-family: Arial, sans-serif; font-size: 10pt; color: black; white-space: nowrap; z-index: 2; line-height: 1; }");
            sb.AppendLine("</style></head><body>");

            sb.AppendLine("<div class='print-container'>");
            sb.AppendLine("<img class='bg-img' src='https://raw.githubusercontent.com/rikelmyrabelo/oficinamvp/main/OficinaWeb/wwwroot/Images/OrdemServico.png' />");

            // Cabeçalho
            sb.AppendLine($"<div class='print-field' style='top: 17.1%; left: 81.5%; font-size: 14pt; font-weight: bold;'>#{os.Id}</div>");
            sb.AppendLine($"<div class='print-field' style='top: 21.1%; left: 19.5%;'>{os.Vehicle?.CustomerName}</div>");
            if (!string.IsNullOrEmpty(os.Vehicle?.CustomerAddress))
                sb.AppendLine($"<div class='print-field' style='top: 24.1%; left: 19.5%; font-size: 9pt;'>{os.Vehicle?.CustomerAddress}</div>");
            sb.AppendLine($"<div class='print-field' style='top: 26.7%; left: 22%;'>{os.Vehicle?.Model}</div>");
            sb.AppendLine($"<div class='print-field' style='top: 34%; left: 30%;'>{os.EntryDate:dd/MM/yyyy}</div>");

            // Seção de Peças (Slots)
            var pecas = os.Items.Where(i => i.ItemType != "Service").ToList();
            double[] coordenadasSlots = { 42, 45, 48, 51.5, 55, 58, 62 };

            for (int i = 0; i < Math.Min(pecas.Count, 7); i++)
            {
                var item = pecas[i];
                string topCSS = coordenadasSlots[i].ToString("0.00", CultureInfo.InvariantCulture);
                string codigo = item.Description.Contains(" - ") ? item.Description.Split(" - ")[0] : "AVULSO";
                string desc = item.Description.Contains(" - ") ? item.Description.Split(" - ")[1] : item.Description;
                string unitario = (item.Price / (item.Quantity > 0 ? item.Quantity : 1)).ToString("N2");
                string descCompleta = item.Quantity > 1 ? $"{desc} (Qtd: {item.Quantity} - V.Un: R$ {unitario})" : desc;

                sb.AppendLine($"<div class='print-field' style='top: {topCSS}%; left: 8%; width: 55pt;'>{codigo}</div>");
                sb.AppendLine($"<div class='print-field' style='top: {topCSS}%; left: 20%; width: 330pt; overflow: hidden; text-overflow: ellipsis;'>{descCompleta}</div>");
                sb.AppendLine($"<div class='print-field' style='top: {topCSS}%; left: 62%; width: 100pt; text-align: right;'>R$ {item.Price:N2}</div>");
            }

            // Seção de Mão de Obra
            sb.AppendLine("<div class='print-field' style='top: 69.8%; left: 7.5%; width: 85%;'>");
            foreach (var item in os.Items.Where(i => i.ItemType == "Service"))
            {
                sb.AppendLine("<div style='position:relative; height: 14pt; margin-bottom: 2pt;'>");
                sb.AppendLine($"<span style='position:absolute; left:0;'><strong>[M.O]</strong> {item.Description}</span>");
                sb.AppendLine($"<span style='position:absolute; right:0;'>R$ {item.Price:N2}</span>");
                sb.AppendLine("</div>");
            }

            // Desconto (se houver)
            decimal subTotal = os.Items.Sum(i => i.Price);
            decimal desconto = subTotal - os.TotalAmount;
            if (desconto > 0)
            {
                sb.AppendLine("<div style='position:relative; height: 14pt; margin-top: 4pt; color: #cc0000;'>");
                sb.AppendLine("<span style='position:absolute; left:0;'><strong>[DESCONTO APLICADO]</strong></span>");
                sb.AppendLine($"<span style='position:absolute; right:0;'>- R$ {desconto:N2}</span>");
                sb.AppendLine("</div>");
            }
            sb.AppendLine("</div>");

            // Totais
            sb.AppendLine($"<div class='print-field' style='top: 80.2%; left: 81%; font-size: 16pt; font-weight: bold;'>R$ {os.TotalAmount:N2}</div>");
            sb.AppendLine($"<div class='print-field' style='top: 85.9%; left: 30%; font-size: 13pt; font-weight: bold;'>R$ {os.AmountPaid:N2}</div>");
            decimal falta = os.TotalAmount - os.AmountPaid;
            sb.AppendLine($"<div class='print-field' style='top: 85.9%; left: 81%; font-size: 13pt; font-weight: bold;'>R$ {(falta > 0 ? falta : 0):N2}</div>");

            sb.AppendLine("</div></body></html>");
            return sb.ToString();
        }
    }
}