using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OficinaAPI.Data;

// Verifique se o namespace abaixo está correto de acordo com a pasta onde está o seu OficinaContext
using OficinaAPI.Models;

[Route("api/[controller]")]
[ApiController]
public class SettingsController : ControllerBase
{
    private readonly OficinaContext _context; // Alterado de ApplicationDbContext para OficinaContext

    public SettingsController(OficinaContext context) => _context = context;

    [HttpGet]
    public async Task<ActionResult<SystemSettings>> GetSettings()
    {
        var settings = await _context.SystemSettings.FirstOrDefaultAsync();

        if (settings == null)
        {
            settings = new SystemSettings { ActiveMonth = DateTime.Now.Month, ActiveYear = DateTime.Now.Year };
            _context.SystemSettings.Add(settings);
            await _context.SaveChangesAsync();
        }

        return settings;
    }

    [HttpPost("close-period")]
    public async Task<IActionResult> ClosePeriod()
    {
        var settings = await _context.SystemSettings.FirstOrDefaultAsync();
        if (settings == null) return NotFound();

        if (settings.ActiveMonth == 12)
        {
            settings.ActiveMonth = 1;
            settings.ActiveYear++;
        }
        else
        {
            settings.ActiveMonth++;
        }

        await _context.SaveChangesAsync();
        return Ok();
    }
}