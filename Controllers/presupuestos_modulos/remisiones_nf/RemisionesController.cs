using Microsoft.AspNetCore.Mvc;
using ChiIT.Services;

namespace ChiIT.Controllers;

[ApiController]
public class RemisionesController : ControllerBase
{
    private readonly RemisionesService _svc;

    public RemisionesController(RemisionesService svc) => _svc = svc;

    // ── GET /REMISIONES  (paginado + filtros) ─────────────────────────────
    [HttpGet("/REMISIONES")]
    public async Task<IActionResult> Listar(
        [FromQuery] int page = 1,
        [FromQuery] int limit = 10,
        [FromQuery] string? ID_OC                    = null,
        [FromQuery] string? ID_REMISION              = null,
        [FromQuery] string? FOLIO                    = null,
        [FromQuery] string? SOLICITANTE              = null,
        [FromQuery] string? FECHA_SOLICITUD          = null,
        [FromQuery] string? ACCESORIO_SOLICITADO     = null,
        [FromQuery] string? MODELO_SERIE_COMENTARIOS = null,
        [FromQuery] string? PROVEEDOR                = null,
        [FromQuery] string? PIEZA_SERVICIO           = null,
        [FromQuery] string? MONEDA                   = null,
        [FromQuery] string? PAGADO                   = null,
        [FromQuery] string? PRESUPUESTO              = null,
        [FromQuery] string? CUENTA_A_DESCONTAR       = null,
        [FromQuery] string? FECHA_ENTRADA_PLANTA     = null,
        [FromQuery] string? STATUS                   = null,
        [FromQuery] string? REQUISICION              = null,
        [FromQuery] string? OC                       = null)
    {
        var filtros = new RemisionFiltros
        {
            ID_OC                    = ID_OC,
            ID_REMISION              = ID_REMISION,
            FOLIO                    = FOLIO,
            SOLICITANTE              = SOLICITANTE,
            FECHA_SOLICITUD          = FECHA_SOLICITUD,
            ACCESORIO_SOLICITADO     = ACCESORIO_SOLICITADO,
            MODELO_SERIE_COMENTARIOS = MODELO_SERIE_COMENTARIOS,
            PROVEEDOR                = PROVEEDOR,
            PIEZA_SERVICIO           = PIEZA_SERVICIO,
            MONEDA                   = MONEDA,
            PAGADO                   = PAGADO,
            PRESUPUESTO              = PRESUPUESTO,
            CUENTA_A_DESCONTAR       = CUENTA_A_DESCONTAR,
            FECHA_ENTRADA_PLANTA     = FECHA_ENTRADA_PLANTA,
            STATUS                   = STATUS,
            REQUISICION              = REQUISICION,
            OC                       = OC
        };

        var resultado = await _svc.ListarAsync(page, limit, filtros);
        return Ok(resultado);
    }

    // ── POST /REMISION  (crear) ───────────────────────────────────────────
    [HttpPost("/REMISION")]
    public async Task<IActionResult> Crear([FromBody] RemisionDto dto)
    {
        var usuario = ObtenerUsuario();
        var id = await _svc.CrearAsync(dto, usuario);
        return Ok(new { id });
    }

    // ── PUT /REMISION/{id}  (editar) ──────────────────────────────────────
    [HttpPut("/REMISION/{id:int}")]
    public async Task<IActionResult> Editar(int id, [FromBody] RemisionDto dto)
    {
        var usuario = ObtenerUsuario();
        var ok = await _svc.EditarAsync(id, dto, usuario);
        return ok ? Ok(new { ok = true }) : NotFound(new { error = "Registro no encontrado" });
    }

    // ── DELETE /REMISION/{id}  (eliminar) ─────────────────────────────────
    [HttpDelete("/REMISION/{id:int}")]
    public async Task<IActionResult> Eliminar(int id)
    {
        var ok = await _svc.EliminarAsync(id);
        return ok ? Ok(new { ok = true }) : NotFound(new { error = "Registro no encontrado" });
    }

    // ── GET /REMISIONES/{id}/HISTORIAL ───────────────────────────────────
    [HttpGet("/REMISIONES/{id:int}/HISTORIAL")]
    public async Task<IActionResult> Historial(int id)
    {
        var historial = await _svc.HistorialAsync(id);
        return Ok(new { historial });
    }

    // ── GET /REMISIONES/EXPORTAR  (Excel filtrado) ────────────────────────
    [HttpGet("/REMISIONES/EXPORTAR")]
    public async Task<IActionResult> ExportarFiltrado(
        [FromQuery] string? ID_OC                    = null,
        [FromQuery] string? ID_REMISION              = null,
        [FromQuery] string? FOLIO                    = null,
        [FromQuery] string? SOLICITANTE              = null,
        [FromQuery] string? FECHA_SOLICITUD          = null,
        [FromQuery] string? ACCESORIO_SOLICITADO     = null,
        [FromQuery] string? MODELO_SERIE_COMENTARIOS = null,
        [FromQuery] string? PROVEEDOR                = null,
        [FromQuery] string? PIEZA_SERVICIO           = null,
        [FromQuery] string? MONEDA                   = null,
        [FromQuery] string? PAGADO                   = null,
        [FromQuery] string? PRESUPUESTO              = null,
        [FromQuery] string? CUENTA_A_DESCONTAR       = null,
        [FromQuery] string? FECHA_ENTRADA_PLANTA     = null,
        [FromQuery] string? STATUS                   = null,
        [FromQuery] string? REQUISICION              = null,
        [FromQuery] string? OC                       = null)
    {
        var filtros = new RemisionFiltros
        {
            ID_OC                    = ID_OC,
            ID_REMISION              = ID_REMISION,
            FOLIO                    = FOLIO,
            SOLICITANTE              = SOLICITANTE,
            FECHA_SOLICITUD          = FECHA_SOLICITUD,
            ACCESORIO_SOLICITADO     = ACCESORIO_SOLICITADO,
            MODELO_SERIE_COMENTARIOS = MODELO_SERIE_COMENTARIOS,
            PROVEEDOR                = PROVEEDOR,
            PIEZA_SERVICIO           = PIEZA_SERVICIO,
            MONEDA                   = MONEDA,
            PAGADO                   = PAGADO,
            PRESUPUESTO              = PRESUPUESTO,
            CUENTA_A_DESCONTAR       = CUENTA_A_DESCONTAR,
            FECHA_ENTRADA_PLANTA     = FECHA_ENTRADA_PLANTA,
            STATUS                   = STATUS,
            REQUISICION              = REQUISICION,
            OC                       = OC
        };

        var bytes = await _svc.ExportarAsync(filtros);
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                    "remisiones_filtrado.xlsx");
    }

    // ── GET /REMISIONES/EXPORTAR_TODO ─────────────────────────────────────
    [HttpGet("/REMISIONES/EXPORTAR_TODO")]
    public async Task<IActionResult> ExportarTodo()
    {
        var bytes = await _svc.ExportarAsync(new RemisionFiltros());
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                    "remisiones_total.xlsx");
    }

    // ── GET /REMISIONES/EXPORTAR_ANIO ─────────────────────────────────────
    [HttpGet("/REMISIONES/EXPORTAR_ANIO")]
    public async Task<IActionResult> ExportarPorAnio([FromQuery] int anio)
    {
        var bytes = await _svc.ExportarPorAnioAsync(anio);
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                    $"remisiones_{anio}.xlsx");
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
