using Microsoft.AspNetCore.Mvc;
using ChiIT.Services;

namespace ChiIT.Controllers;

[ApiController]
public class BuscarGlobalController : ControllerBase
{
    private readonly BuscarGlobalService _svc;

    public BuscarGlobalController(BuscarGlobalService svc) => _svc = svc;

    [HttpGet("/BUSCAR_GLOBAL")]
    public async Task<IActionResult> Buscar([FromQuery] string q)
    {
        if (string.IsNullOrWhiteSpace(q))
            return BadRequest(new { error = "Parámetro q requerido" });

        var resultado = await _svc.BuscarAsync(q.Trim());
        return Ok(resultado);
    }
}
