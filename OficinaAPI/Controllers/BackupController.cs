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

                var ordensBanco = await _context.ServiceOrders.AsNoTracking()
                    .Include(s => s.Vehicle).Include(s => s.Items)
                    .Where(s => s.Status == "Completed" && !s.IsDeleted)
                    .OrderByDescending(s => s.CompletionDate ?? s.EntryDate)
                    .ToListAsync();

                var produtos = await _context.Products.AsNoTracking().OrderBy(p => p.Name).ToListAsync();

                var wsDash = workbook.Worksheets.Add("Dashboard");
                wsDash.ShowGridLines = false;
                wsDash.Column("A").Width = 2;
                wsDash.Columns("B:I").Width = 18;

                var titleRange = wsDash.Range("B2:I3");
                titleRange.Merge().Value = $"FREITAS AUTOCENTER - PAINEL EXECUTIVO ({DateTime.Now:dd/MM/yyyy})";
                titleRange.Style.Font.SetBold().Font.FontSize = 18;
                titleRange.Style.Font.FontColor = XLColor.White;
                titleRange.Style.Fill.BackgroundColor = XLColor.FromHtml("#ff6600");
                titleRange.Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
                titleRange.Style.Alignment.SetVertical(XLAlignmentVerticalValues.Center);

                // Cálculos de Métricas
                decimal fatTotal = ordensBanco.Sum(o => o.TotalAmount);
                decimal recTotal = ordensBanco.Sum(o => o.AmountPaid);
                decimal inad = ordensBanco.Sum(o => Math.Max(0, o.TotalAmount - o.AmountPaid));
                decimal custoPecas = ordensBanco.Sum(o => o.Items?.Sum(i => i.CostPrice) ?? 0);
                decimal lucro = fatTotal - custoPecas;
                decimal valorEstoque = produtos.Sum(p => p.StockQuantity * p.SalePrice);
                int osFinalizadas = ordensBanco.Count;
                int produtosBaixoEstoque = produtos.Count(p => p.StockQuantity <= p.MinimumStock);

                // Helper para montar os Cards Visuais no Excel
                Action<string, string, string, string, string, object, string, string> AddKpi = (cStart, cEnd, rTitle, rValEnd, title, val, bgColor, fgColor) => {
                    var r1 = wsDash.Range($"{cStart}{rTitle}:{cEnd}{rTitle}");
                    r1.Merge().Value = title;
                    r1.Style.Fill.BackgroundColor = XLColor.FromHtml(bgColor);
                    r1.Style.Font.FontColor = XLColor.FromHtml(fgColor);
                    r1.Style.Font.Bold = true;
                    r1.Style.Font.FontSize = 10;
                    r1.Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
                    r1.Style.Alignment.SetVertical(XLAlignmentVerticalValues.Center);

                    var r2 = wsDash.Range($"{cStart}{int.Parse(rTitle) + 1}:{cEnd}{rValEnd}");
                    var mergedCell = r2.Merge();

                    if (val is decimal d)
                    {
                        mergedCell.Value = d;
                        r2.Style.NumberFormat.Format = "\"R$\" #,##0.00";
                    }
                    else
                    {
                        mergedCell.Value = (int)val;
                    }

                    r2.Style.Fill.BackgroundColor = XLColor.FromHtml(bgColor);
                    r2.Style.Font.FontColor = XLColor.FromHtml(fgColor);
                    r2.Style.Font.Bold = true;
                    r2.Style.Font.FontSize = 22;
                    r2.Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
                    r2.Style.Alignment.SetVertical(XLAlignmentVerticalValues.Center);
                };

                AddKpi("B", "C", "5", "7", "FATURAMENTO BRUTO", fatTotal, "#212529", "#FFFFFF");
                AddKpi("D", "E", "5", "7", "LUCRO ESTIMADO", lucro, "#198754", "#FFFFFF");
                AddKpi("F", "G", "5", "7", "CUSTO (PEÇAS)", custoPecas, "#dc3545", "#FFFFFF");
                AddKpi("H", "I", "5", "7", "INADIMPLÊNCIA", inad, "#8b0000", "#FFFFFF");

                AddKpi("B", "C", "9", "11", "TOTAL RECEBIDO", recTotal, "#0d6efd", "#FFFFFF");
                AddKpi("D", "E", "9", "11", "CAPITAL EM ESTOQUE", valorEstoque, "#6c757d", "#FFFFFF");
                AddKpi("F", "G", "9", "11", "O.S. FINALIZADAS", osFinalizadas, "#0dcaf0", "#000000");
                AddKpi("H", "I", "9", "11", "ALERTA DE ESTOQUE", produtosBaixoEstoque, "#ffc107", "#000000");

                var wsVendas = workbook.Worksheets.Add("Financeiro");
                var ordensExcel = ordensBanco.Select(s => new {
                    OS = $"#{s.Id}",
                    DATA = (s.CompletionDate ?? s.EntryDate).ToString("dd/MM/yyyy"),
                    CLIENTE = s.Vehicle?.CustomerName ?? "-",
                    VEICULO = s.Vehicle?.Model ?? "-",
                    CUSTO_PECAS = s.Items?.Sum(i => i.CostPrice) ?? 0,
                    VALOR_TOTAL = s.TotalAmount,
                    VALOR_PAGO = s.AmountPaid,
                    PENDENTE = Math.Max(0, s.TotalAmount - s.AmountPaid),
                    MEIO_PGTO = s.PaymentMethod ?? "N/A"
                }).ToList();

                if (ordensExcel.Any())
                {
                    var tableVendas = wsVendas.Cell(1, 1).InsertTable(ordensExcel);
                    tableVendas.Theme = XLTableTheme.None;
                    tableVendas.HeadersRow().Style.Fill.BackgroundColor = XLColor.FromHtml("#111111");
                    tableVendas.HeadersRow().Style.Font.FontColor = XLColor.FromHtml("#ff6600");
                    tableVendas.HeadersRow().Style.Font.Bold = true;

                    wsVendas.Column(5).Style.NumberFormat.Format = "\"R$\" #,##0.00";
                    wsVendas.Column(6).Style.NumberFormat.Format = "\"R$\" #,##0.00";
                    wsVendas.Column(7).Style.NumberFormat.Format = "\"R$\" #,##0.00";
                    wsVendas.Column(8).Style.NumberFormat.Format = "\"R$\" #,##0.00";

                    foreach (var row in tableVendas.DataRange.Rows())
                    {
                        if (row.Cell(8).GetDouble() > 0)
                        {
                            row.Cell(8).Style.Font.FontColor = XLColor.Red;
                            row.Cell(8).Style.Font.Bold = true;
                        }
                    }

                    tableVendas.ShowTotalsRow = true;
                    tableVendas.Field("CUSTO_PECAS").TotalsRowFunction = XLTotalsRowFunction.Sum;
                    tableVendas.Field("VALOR_TOTAL").TotalsRowFunction = XLTotalsRowFunction.Sum;
                    tableVendas.Field("VALOR_PAGO").TotalsRowFunction = XLTotalsRowFunction.Sum;
                    tableVendas.Field("PENDENTE").TotalsRowFunction = XLTotalsRowFunction.Sum;

                    var totalsRow = tableVendas.TotalsRow();
                    totalsRow.Style.Fill.BackgroundColor = XLColor.FromHtml("#222222");
                    totalsRow.Style.Font.FontColor = XLColor.White;
                    totalsRow.Style.Font.Bold = true;

                    wsVendas.Columns().AdjustToContents();
                }

                var wsEstoque = workbook.Worksheets.Add("Estoque");
                var produtosExcel = produtos.Select(p => new {
                    CODIGO = p.Code,
                    PRODUTO = p.Name,
                    CUSTO = p.CostPrice,
                    VENDA = p.SalePrice,
                    QTD_ATUAL = p.StockQuantity,
                    MINIMO = p.MinimumStock,
                    STATUS = p.StockQuantity <= p.MinimumStock ? "BAIXO ESTOQUE" : "NORMAL"
                }).ToList();

                if (produtosExcel.Any())
                {
                    var tableEstoque = wsEstoque.Cell(1, 1).InsertTable(produtosExcel);
                    tableEstoque.Theme = XLTableTheme.None;
                    tableEstoque.HeadersRow().Style.Fill.BackgroundColor = XLColor.FromHtml("#111111");
                    tableEstoque.HeadersRow().Style.Font.FontColor = XLColor.FromHtml("#ff6600");
                    tableEstoque.HeadersRow().Style.Font.Bold = true;

                    wsEstoque.Column(3).Style.NumberFormat.Format = "\"R$\" #,##0.00";
                    wsEstoque.Column(4).Style.NumberFormat.Format = "\"R$\" #,##0.00";

                    // Destacar produtos em baixo estoque
                    foreach (var row in tableEstoque.DataRange.Rows())
                    {
                        if (row.Cell(7).GetString() == "BAIXO ESTOQUE")
                        {
                            row.Cell(7).Style.Font.FontColor = XLColor.Red;
                            row.Cell(7).Style.Font.Bold = true;
                        }
                        else
                        {
                            row.Cell(7).Style.Font.FontColor = XLColor.Green;
                        }
                    }

                    // Total de itens em estoque
                    tableEstoque.ShowTotalsRow = true;
                    tableEstoque.Field("QTD_ATUAL").TotalsRowFunction = XLTotalsRowFunction.Sum;
                    var totalsRowEstoque = tableEstoque.TotalsRow();
                    totalsRowEstoque.Style.Fill.BackgroundColor = XLColor.FromHtml("#222222");
                    totalsRowEstoque.Style.Font.FontColor = XLColor.White;
                    totalsRowEstoque.Style.Font.Bold = true;

                    wsEstoque.Columns().AdjustToContents();
                }

                var wsNotas = workbook.Worksheets.Add("Anotacoes");
                var notasExcel = await _context.Notes.AsNoTracking()
                    .OrderByDescending(n => n.CreatedAt)
                    .Select(n => new {
                        DATA = n.CreatedAt.ToString("dd/MM/yyyy HH:mm"),
                        CONTEUDO = n.Content
                    }).ToListAsync();

                if (notasExcel.Any())
                {
                    var tableNotas = wsNotas.Cell(1, 1).InsertTable(notasExcel);
                    tableNotas.Theme = XLTableTheme.None;
                    tableNotas.HeadersRow().Style.Fill.BackgroundColor = XLColor.FromHtml("#111111");
                    tableNotas.HeadersRow().Style.Font.FontColor = XLColor.FromHtml("#ff6600");
                    tableNotas.HeadersRow().Style.Font.Bold = true;
                    wsNotas.Columns().AdjustToContents();
                }

                // GERAÇÃO DOS ARQUIVOS HTML DAS O.S. (Backup Físico em PDF/HTML)
                var pastaOS = Path.Combine(pastaDestino, "Ordens_de_Servico");
                if (!Directory.Exists(pastaOS)) Directory.CreateDirectory(pastaOS);

                foreach (var s in ordensBanco)
                {
                    string nomeArquivoOS = $"OS_{s.Id}.html";
                    System.IO.File.WriteAllText(Path.Combine(pastaOS, nomeArquivoOS), GerarTemplateHtmlOS(s), Encoding.UTF8);
                }

                string caminhoExcel = Path.Combine(pastaDestino, $"Painel_Executivo_{DateTime.Now:ddMMyyyy_HHmm}.xlsx");
                workbook.SaveAs(caminhoExcel);

                return Ok(new { mensagem = "Relatório Executivo Excel gerado com sucesso!", caminho = caminhoExcel });
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

            sb.AppendLine($"<div class='print-field' style='top: 17.1%; left: 81.5%; font-size: 14pt; font-weight: bold;'>#{os.Id}</div>");
            sb.AppendLine($"<div class='print-field' style='top: 21.1%; left: 19.5%;'>{os.Vehicle?.CustomerName}</div>");
            if (!string.IsNullOrEmpty(os.Vehicle?.CustomerAddress))
                sb.AppendLine($"<div class='print-field' style='top: 24.1%; left: 19.5%; font-size: 9pt;'>{os.Vehicle?.CustomerAddress}</div>");
            sb.AppendLine($"<div class='print-field' style='top: 26.7%; left: 22%;'>{os.Vehicle?.Model}</div>");
            sb.AppendLine($"<div class='print-field' style='top: 34%; left: 30%;'>{os.EntryDate:dd/MM/yyyy}</div>");

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

            sb.AppendLine("<div class='print-field' style='top: 69.8%; left: 7.5%; width: 85%;'>");
            foreach (var item in os.Items.Where(i => i.ItemType == "Service"))
            {
                sb.AppendLine("<div style='position:relative; height: 14pt; margin-bottom: 2pt;'>");
                sb.AppendLine($"<span style='position:absolute; left:0;'><strong>[M.O]</strong> {item.Description}</span>");
                sb.AppendLine($"<span style='position:absolute; right:0;'>R$ {item.Price:N2}</span>");
                sb.AppendLine("</div>");
            }

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

            sb.AppendLine($"<div class='print-field' style='top: 80.2%; left: 81%; font-size: 16pt; font-weight: bold;'>R$ {os.TotalAmount:N2}</div>");
            sb.AppendLine($"<div class='print-field' style='top: 85.9%; left: 30%; font-size: 13pt; font-weight: bold;'>R$ {os.AmountPaid:N2}</div>");
            decimal falta = os.TotalAmount - os.AmountPaid;
            sb.AppendLine($"<div class='print-field' style='top: 85.9%; left: 81%; font-size: 13pt; font-weight: bold;'>R$ {(falta > 0 ? falta : 0):N2}</div>");

            sb.AppendLine("</div></body></html>");
            return sb.ToString();
        }
    }
}