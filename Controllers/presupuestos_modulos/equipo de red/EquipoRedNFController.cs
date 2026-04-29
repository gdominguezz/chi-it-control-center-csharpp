using Microsoft.AspNetCore.Mvc;
using ChiIT.Services;

namespace ChiIT.Controllers;

[ApiController]
public class EquipoRedNFController : ControllerBase
{
    private readonly EquipoRedNFService _svc;

    public EquipoRedNFController(EquipoRedNFService svc) => _svc = svc;

    // ── GET /EQUIPOS_RED_NF  (paginado + filtros) ─────────────────────────
    [HttpGet("/EQUIPOS_RED_NF")]
    public async Task<IActionResult> Listar(
        [FromQuery] int page = 1,
        [FromQuery] int limit = 10,
        [FromQuery] string? ID_UNICO                  = null,
        [FromQuery] string? OC                        = null,
        [FromQuery] string? FOLIO_CORRECTIVO          = null,
        [FromQuery] string? FECHA_REGISTRO            = null,
        [FromQuery] string? RECIBIDO_POR              = null,
        [FromQuery] string? SUBCATEGORIA              = null,
        [FromQuery] string? NO_PARTE                  = null,
        [FromQuery] string? MARCA                     = null,
        [FromQuery] string? MODELO                    = null,
        [FromQuery] string? NUMERO_SERIE              = null,
        [FromQuery] string? MAC1                      = null,
        [FromQuery] string? MAC2                      = null,
        [FromQuery] string? MAC_ADDRESS               = null,
        [FromQuery] string? PROVEEDOR                 = null,
        [FromQuery] string? MONEDA                    = null,
        [FromQuery] string? UBICACION                 = null,
        [FromQuery] string? DESTINO                   = null,
        [FromQuery] string? ACTIVO_DTR3               = null)
    {
        var filtros = new EquipoRedNFFiltros
        {
            ID_UNICO        = ID_UNICO,
            OC              = OC,
            FOLIO_CORRECTIVO= FOLIO_CORRECTIVO,
            FECHA_REGISTRO  = FECHA_REGISTRO,
            RECIBIDO_POR    = RECIBIDO_POR,
            SUBCATEGORIA    = SUBCATEGORIA,
            NO_PARTE        = NO_PARTE,
            MARCA           = MARCA,
            MODELO          = MODELO,
            NUMERO_SERIE    = NUMERO_SERIE,
            MAC1            = MAC1,
            MAC2            = MAC2,
            MAC_ADDRESS     = MAC_ADDRESS,
            PROVEEDOR       = PROVEEDOR,
            MONEDA          = MONEDA,
            UBICACION       = UBICACION,
            DESTINO         = DESTINO,
            ACTIVO_DTR3     = ACTIVO_DTR3
        };

        var resultado = await _svc.ListarAsync(page, limit, filtros);
        return Ok(resultado);
    }

    // ── POST /EQUIPO_RED_NF  (crear) ──────────────────────────────────────
    [HttpPost("/EQUIPO_RED_NF")]
    public async Task<IActionResult> Crear([FromBody] EquipoRedNFDto dto)
    {
        var usuario = ObtenerUsuario();
        var id = await _svc.CrearAsync(dto, usuario);
        return Ok(new { id });
    }

    // ── PUT /EQUIPO_RED_NF/{id}  (editar) ─────────────────────────────────
    [HttpPut("/EQUIPO_RED_NF/{id:int}")]
    public async Task<IActionResult> Editar(int id, [FromBody] EquipoRedNFDto dto)
    {
        var usuario = ObtenerUsuario();
        var ok = await _svc.EditarAsync(id, dto, usuario);
        return ok ? Ok(new { ok = true }) : NotFound(new { error = "Registro no encontrado" });
    }

    // ── DELETE /EQUIPO_RED_NF/{id}  (eliminar) ────────────────────────────
    [HttpDelete("/EQUIPO_RED_NF/{id:int}")]
    public async Task<IActionResult> Eliminar(int id)
    {
        var ok = await _svc.EliminarAsync(id);
        return ok ? Ok(new { ok = true }) : NotFound(new { error = "Registro no encontrado" });
    }

    // ── GET /EQUIPOS_RED_NF/{id}/HISTORIAL ───────────────────────────────
    [HttpGet("/EQUIPOS_RED_NF/{id:int}/HISTORIAL")]
    public async Task<IActionResult> Historial(int id)
    {
        var historial = await _svc.HistorialAsync(id);
        return Ok(new { historial });
    }

    // ── GET /EQUIPOS_RED_NF/EXPORTAR  (Excel filtrado) ───────────────────
    [HttpGet("/EQUIPOS_RED_NF/EXPORTAR")]
    public async Task<IActionResult> ExportarFiltrado(
        [FromQuery] string? ID_UNICO        = null,
        [FromQuery] string? OC              = null,
        [FromQuery] string? FOLIO_CORRECTIVO= null,
        [FromQuery] string? FECHA_REGISTRO  = null,
        [FromQuery] string? RECIBIDO_POR    = null,
        [FromQuery] string? SUBCATEGORIA    = null,
        [FromQuery] string? NO_PARTE        = null,
        [FromQuery] string? MARCA           = null,
        [FromQuery] string? MODELO          = null,
        [FromQuery] string? NUMERO_SERIE    = null,
        [FromQuery] string? MAC1            = null,
        [FromQuery] string? MAC2            = null,
        [FromQuery] string? MAC_ADDRESS     = null,
        [FromQuery] string? PROVEEDOR       = null,
        [FromQuery] string? MONEDA          = null,
        [FromQuery] string? UBICACION       = null,
        [FromQuery] string? DESTINO         = null,
        [FromQuery] string? ACTIVO_DTR3     = null)
    {
        var filtros = new EquipoRedNFFiltros
        {
            ID_UNICO        = ID_UNICO,
            OC              = OC,
            FOLIO_CORRECTIVO= FOLIO_CORRECTIVO,
            FECHA_REGISTRO  = FECHA_REGISTRO,
            RECIBIDO_POR    = RECIBIDO_POR,
            SUBCATEGORIA    = SUBCATEGORIA,
            NO_PARTE        = NO_PARTE,
            MARCA           = MARCA,
            MODELO          = MODELO,
            NUMERO_SERIE    = NUMERO_SERIE,
            MAC1            = MAC1,
            MAC2            = MAC2,
            MAC_ADDRESS     = MAC_ADDRESS,
            PROVEEDOR       = PROVEEDOR,
            MONEDA          = MONEDA,
            UBICACION       = UBICACION,
            DESTINO         = DESTINO,
            ACTIVO_DTR3     = ACTIVO_DTR3
        };

        var bytes = await _svc.ExportarAsync(filtros);
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                    "equipo_red_nf_filtrado.xlsx");
    }

    // ── GET /EQUIPOS_RED_NF/EXPORTAR_TODO ────────────────────────────────
    [HttpGet("/EQUIPOS_RED_NF/EXPORTAR_TODO")]
    public async Task<IActionResult> ExportarTodo()
    {
        var bytes = await _svc.ExportarAsync(new EquipoRedNFFiltros());
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                    "equipo_red_nf_total.xlsx");
    }

    // ── GET /EQUIPOS_RED_NF/EXPORTAR_ANIO ────────────────────────────────
    [HttpGet("/EQUIPOS_RED_NF/EXPORTAR_ANIO")]
    public async Task<IActionResult> ExportarPorAnio([FromQuery] int anio)
    {
        var bytes = await _svc.ExportarPorAnioAsync(anio);
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                    $"equipo_red_nf_{anio}.xlsx");
    }

    // ── Helper: leer usuario del header o query ───────────────────────────
    private string ObtenerUsuario()
    {
        if (Request.Headers.TryGetValue("X-Usuario", out var h) && !string.IsNullOrWhiteSpace(h))
            return h!;
        if (Request.Query.TryGetValue("usuario", out var q) && !string.IsNullOrWhiteSpace(q))
            return q!;
        return "SISTEMA";
    }
}
