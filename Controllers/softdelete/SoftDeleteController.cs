using Microsoft.AspNetCore.Mvc;
using ChiIT.Services;

namespace ChiIT.Controllers;

[ApiController]
public class SoftDeleteController : ControllerBase
{
    private readonly SoftDeleteService _svc;

    public SoftDeleteController(SoftDeleteService svc) => _svc = svc;

    // ── DELETE /SOFT_DELETE/{tabla}/{id}  →  inhabilita el registro ───────
    [HttpDelete("/SOFT_DELETE/{tabla}/{id:int}")]
    public async Task<IActionResult> Inhabilitar(string tabla, int id)
    {
        var (ok, error) = await _svc.InhabilitarAsync(tabla, id);
        return ok
            ? Ok(new { ok = true, mensaje = "Registro inhabilitado correctamente." })
            : BadRequest(new { ok = false, error });
    }

    // ── PUT /SOFT_DELETE/{tabla}/{id}/RESTAURAR  →  reactiva el registro ──
    [HttpPut("/SOFT_DELETE/{tabla}/{id:int}/RESTAURAR")]
    public async Task<IActionResult> Restaurar(string tabla, int id)
    {
        var (ok, error) = await _svc.RestaurarAsync(tabla, id);
        return ok
            ? Ok(new { ok = true, mensaje = "Registro restaurado correctamente." })
            : BadRequest(new { ok = false, error });
    }

    // ── GET /SOFT_DELETE/{tabla}/INACTIVOS  →  lista registros inactivos ──
    [HttpGet("/SOFT_DELETE/{tabla}/INACTIVOS")]
    public async Task<IActionResult> ListarInactivos(string tabla)
    {
        if (!TablasPermitidas.EsValida(tabla))
            return BadRequest(new { error = $"Tabla '{tabla}' no permitida." });

        var lista = await _svc.ListarInactivosAsync(tabla);
        return Ok(new { total = lista.Count, data = lista });
    }
}