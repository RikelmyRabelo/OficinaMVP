using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OficinaAPI.Data;

namespace OficinaAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class BackupController : ControllerBase
    {
        private readonly OficinaContext _context;

        public BackupController(OficinaContext context)
        {
            _context = context;
        }

        [HttpPost("gerar")]
        public IActionResult GerarBackup()
        {
            try
            {
                // ATENÇÃO: Altere para o caminho da sua pasta do Google Drive
                string pastaBackup = @"D:\Backups_Oficina";

                if (!Directory.Exists(pastaBackup))
                    Directory.CreateDirectory(pastaBackup);

                string nomeArquivo = $"OficinaDB_{DateTime.Now:yyyyMMdd_HHmm}.bak";
                string caminhoCompleto = Path.Combine(pastaBackup, nomeArquivo);

                // Comando SQL que gera o backup do LocalDB
                string sqlBackup = $@"BACKUP DATABASE [OficinaMVP] TO DISK = '{caminhoCompleto}'";

                _context.Database.ExecuteSqlRaw(sqlBackup);

                return Ok(new { mensagem = "Sucesso", caminho = caminhoCompleto });
            }
            catch (Exception ex)
            {
                return BadRequest(new { erro = ex.Message });
            }
        }
    }
}