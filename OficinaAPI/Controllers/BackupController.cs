using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OficinaAPI.Data;
using System.Text.Json;

namespace OficinaAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class BackupController : ControllerBase
    {
        private readonly OficinaContext _context;
        private readonly string _pastaBackup = @"D:\Backups_Oficina";

        public BackupController(OficinaContext context)
        {
            _context = context;

            if (!Directory.Exists(_pastaBackup))
                Directory.CreateDirectory(_pastaBackup);
        }

        [HttpPost("gerar")]
        public IActionResult GerarBackup()
        {
            try
            {
                string nomeArquivo = $"OficinaDB_{DateTime.Now:yyyyMMdd_HHmm}.bak";
                string caminhoCompleto = Path.Combine(_pastaBackup, nomeArquivo);

                string sqlBackup = $@"BACKUP DATABASE [OficinaMVP] TO DISK = '{caminhoCompleto}'";
                _context.Database.ExecuteSqlRaw(sqlBackup);

                return Ok(new { mensagem = "Sucesso", caminho = caminhoCompleto });
            }
            catch (Exception ex)
            {
                return BadRequest(new { erro = ex.Message });
            }
        }

        [HttpPost("exportar-legivel")]
        public async Task<IActionResult> ExportarDadosLegiveis()
        {
            try
            {
                var dadosExportacao = new
                {
                    DataGeracao = DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss"),
                    Funcionarios = await _context.Employees.ToListAsync(),
                    Produtos = await _context.Products.ToListAsync(),
                    // Corrigido para .Include(s => s.Items) conforme o seu Model
                    OrdensServico = await _context.ServiceOrders
                        .Include(s => s.Items)
                        .ToListAsync()
                };

                string nomeArquivo = $"Relatorio_Oficina_{DateTime.Now:yyyyMMdd_HHmm}.json";
                string caminhoCompleto = Path.Combine(_pastaBackup, nomeArquivo);

                var opcoes = new JsonSerializerOptions
                {
                    WriteIndented = true,
                    ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles
                };

                string jsonTexto = JsonSerializer.Serialize(dadosExportacao, opcoes);
                await System.IO.File.WriteAllTextAsync(caminhoCompleto, jsonTexto);

                return Ok(new { mensagem = "Relatório legível gerado!", caminho = caminhoCompleto });
            }
            catch (Exception ex)
            {
                return BadRequest(new { erro = ex.Message });
            }
        }
    }
}