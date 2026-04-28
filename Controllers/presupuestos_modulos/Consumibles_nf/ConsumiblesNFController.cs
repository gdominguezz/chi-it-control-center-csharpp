using Microsoft.AspNetCore.Mvc;
using ChiIT.Services;

namespace ChiIT.Controllers;

[ApiController]
public class ConsumiblesNFController : ControllerBase
{
    private readonly ConsumiblesNFService _svc;

    public ConsumiblesNFController(ConsumiblesNFService svc) => _svc = svc;

    // ── GET /CONSUMIBLES_NF  (paginado + filtros) ─────────────────────────
    [HttpGet("/CONSUMIBLES_NF")]
    public async Task<IActionResult> Listar(
        [FromQuery] int page = 1,
        [FromQuery] int limit = 10,
        [FromQuery] string? ID_UNICO       = null,
        [FromQuery] string? OC             = null,
        [FromQuery] string? FOLIO_CANTIDAD = null,
        [FromQuery] string? FECHA_ENTRADA  = null,
        [FromQuery] string? RECIBIDO_POR   = null,
        [FromQuery] string? SUBCATEGORIA   = null,
        [FromQuery] string? MARCA          = null,
        [FromQuery] string? MODELO         = null,
        [FromQuery] string? DESCRIPCION    = null,
        [FromQuery] string? PROVEEDOR      = null,
        [FromQuery] string? MONEDA         = null,
        [FromQuery] string? PLANTA         = null,
        [FromQuery] string? UBICACION      = null,
        [FromQuery] string? DESTINO        = null)
    {
        var filtros = new ConsumibleNFFiltros
        {
            ID_UNICO       = ID_UNICO,
            OC             = OC,
            FOLIO_CANTIDAD = FOLIO_CANTIDAD,
            FECHA_ENTRADA  = FECHA_ENTRADA,
            RECIBIDO_POR   = RECIBIDO_POR,
            SUBCATEGORIA   = SUBCATEGORIA,
            MARCA          = MARCA,
            MODELO         = MODELO,
            DESCRIPCION    = DESCRIPCION,
            PROVEEDOR      = PROVEEDOR,
            MONEDA         = MONEDA,
            PLANTA         = PLANTA,
            UBICACION      = UBICACION,
            DESTINO        = DESTINO
        };

        var resultado = await _svc.ListarAsync(page, limit, filtros);
        return Ok(resultado);
    }

    // ── POST /CONSUMIBLE_NF  (crear) ──────────────────────────────────────
    [HttpPost("/CONSUMIBLE_NF")]
    public async Task<IActionResult> Crear([FromBody] ConsumibleNFDto dto)
    {
        var usuario = ObtenerUsuario();
        var id = await _svc.CrearAsync(dto, usuario);
        return Ok(new { id });
    }

    // ── PUT /CONSUMIBLE_NF/{id}  (editar) ─────────────────────────────────
    [HttpPut("/CONSUMIBLE_NF/{id:int}")]
    public async Task<IActionResult> Editar(int id, [FromBody] ConsumibleNFDto dto)
    {
        var usuario = ObtenerUsuario();
        var ok = await _svc.EditarAsync(id, dto, usuario);
        return ok ? Ok(new { ok = true }) : NotFound(new { error = "Registro no encontrado" });
    }

    // ── DELETE /CONSUMIBLE_NF/{id}  (eliminar) ────────────────────────────
    [HttpDelete("/CONSUMIBLE_NF/{id:int}")]
    public async Task<IActionResult> Eliminar(int id)
    {
        var ok = await _svc.EliminarAsync(id);
        return ok ? Ok(new { ok = true }) : NotFound(new { error = "Registro no encontrado" });
    }

    // ── GET /CONSUMIBLES_NF/{id}/HISTORIAL ───────────────────────────────
    [HttpGet("/CONSUMIBLES_NF/{id:int}/HISTORIAL")]
    public async Task<IActionResult> Historial(int id)
    {
        var historial = await _svc.HistorialAsync(id);
        return Ok(new { historial });
    }

    // ── GET /CONSUMIBLES_NF/EXPORTAR  (Excel filtrado) ───────────────────
    [HttpGet("/CONSUMIBLES_NF/EXPORTAR")]
    public async Task<IActionResult> ExportarFiltrado(
        [FromQuery] string? ID_UNICO       = null,
        [FromQuery] string? OC             = null,
        [FromQuery] string? FOLIO_CANTIDAD = null,
        [FromQuery] string? FECHA_ENTRADA  = null,
        [FromQuery] string? RECIBIDO_POR   = null,
        [FromQuery] string? SUBCATEGORIA   = null,
        [FromQuery] string? MARCA          = null,
        [FromQuery] string? MODELO         = null,
        [FromQuery] string? DESCRIPCION    = null,
        [FromQuery] string? PROVEEDOR      = null,
        [FromQuery] string? MONEDA         = null,
        [FromQuery] string? PLANTA         = null,
        [FromQuery] string? UBICACION      = null,
        [FromQuery] string? DESTINO        = null)
    {
        var filtros = new ConsumibleNFFiltros
        {
            ID_UNICO       = ID_UNICO,
            OC             = OC,
            FOLIO_CANTIDAD = FOLIO_CANTIDAD,
            FECHA_ENTRADA  = FECHA_ENTRADA,
            RECIBIDO_POR   = RECIBIDO_POR,
            SUBCATEGORIA   = SUBCATEGORIA,
            MARCA          = MARCA,
            MODELO         = MODELO,
            DESCRIPCION    = DESCRIPCION,
            PROVEEDOR      = PROVEEDOR,
            MONEDA         = MONEDA,
            PLANTA         = PLANTA,
            UBICACION      = UBICACION,
            DESTINO        = DESTINO
        };

        var bytes = await _svc.ExportarAsync(filtros);
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                    "consumibles_nf_filtrado.xlsx");
    }

    // ── GET /CONSUMIBLES_NF/EXPORTAR_TODO ────────────────────────────────
    [HttpGet("/CONSUMIBLES_NF/EXPORTAR_TODO")]
    public async Task<IActionResult> ExportarTodo()
    {
        var bytes = await _svc.ExportarAsync(new ConsumibleNFFiltros());
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                    "consumibles_nf_total.xlsx");
    }

    // ── GET /CONSUMIBLES_NF/EXPORTAR_ANIO ────────────────────────────────
    [HttpGet("/CONSUMIBLES_NF/EXPORTAR_ANIO")]
    public async Task<IActionResult> ExportarPorAnio([FromQuery] int anio)
    {
        var bytes = await _svc.ExportarPorAnioAsync(anio);
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                    $"consumibles_nf_{anio}.xlsx");
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
