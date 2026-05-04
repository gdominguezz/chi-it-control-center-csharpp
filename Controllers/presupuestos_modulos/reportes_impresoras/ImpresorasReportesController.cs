using Microsoft.AspNetCore.Mvc;
using ChiIT.Services;

namespace ChiIT.Controllers;

[ApiController]
public class ImpresorasReportesController : ControllerBase
{
    private readonly ImpresorasReportesService _svc;

    public ImpresorasReportesController(ImpresorasReportesService svc) => _svc = svc;

    // ════════════════════════════════════════════════════════════════════════
    //  REPORTES_IMPRESORAS
    // ════════════════════════════════════════════════════════════════════════

    // ── GET /REPORTES_IMPRESORAS  (paginado + filtros) ────────────────────
    [HttpGet("/REPORTES_IMPRESORAS")]
    public async Task<IActionResult> ListarReportes(
        [FromQuery] int page = 1,
        [FromQuery] int limit = 10,
        [FromQuery] string? FOLIO                = null,
        [FromQuery] string? FECHA                = null,
        [FromQuery] string? PLANTA               = null,
        [FromQuery] string? IMPRESORA            = null,
        [FromQuery] string? AREA                 = null,
        [FromQuery] string? REPORTE              = null,
        [FromQuery] string? QUIEN_REPORTA        = null,
        [FromQuery] string? ESTATUS              = null,
        [FromQuery] string? FECHA_DE_REALIZACION = null,
        [FromQuery] string? COMENTARIOS          = null)
    {
        var filtros = new ReporteImpresoraFiltros
        {
            FOLIO                = FOLIO,
            FECHA                = FECHA,
            PLANTA               = PLANTA,
            IMPRESORA            = IMPRESORA,
            AREA                 = AREA,
            REPORTE              = REPORTE,
            QUIEN_REPORTA        = QUIEN_REPORTA,
            ESTATUS              = ESTATUS,
            FECHA_DE_REALIZACION = FECHA_DE_REALIZACION,
            COMENTARIOS          = COMENTARIOS
        };

        var resultado = await _svc.ListarReportesAsync(page, limit, filtros);
        return Ok(resultado);
    }

    // ── POST /REPORTE_IMPRESORA  (crear) ──────────────────────────────────
    [HttpPost("/REPORTE_IMPRESORA")]
    public async Task<IActionResult> CrearReporte([FromBody] ReporteImpresoraDto dto)
    {
        var usuario = ObtenerUsuario();
        var id = await _svc.CrearReporteAsync(dto, usuario);
        return Ok(new { id });
    }

    // ── PUT /REPORTE_IMPRESORA/{id}  (editar) ─────────────────────────────
    [HttpPut("/REPORTE_IMPRESORA/{id:int}")]
    public async Task<IActionResult> EditarReporte(int id, [FromBody] ReporteImpresoraDto dto)
    {
        var usuario = ObtenerUsuario();
        var ok = await _svc.EditarReporteAsync(id, dto, usuario);
        return ok ? Ok(new { ok = true }) : NotFound(new { error = "Registro no encontrado" });
    }

    // ── DELETE /REPORTE_IMPRESORA/{id}  (eliminar) ────────────────────────
    [HttpDelete("/REPORTE_IMPRESORA/{id:int}")]
    public async Task<IActionResult> EliminarReporte(int id)
    {
        var ok = await _svc.EliminarReporteAsync(id);
        return ok ? Ok(new { ok = true }) : NotFound(new { error = "Registro no encontrado" });
    }

    // ── GET /REPORTES_IMPRESORAS/{id}/HISTORIAL ───────────────────────────
    [HttpGet("/REPORTES_IMPRESORAS/{id:int}/HISTORIAL")]
    public async Task<IActionResult> HistorialReporte(int id)
    {
        var historial = await _svc.HistorialReporteAsync(id);
        return Ok(new { historial });
    }

    // ── GET /REPORTES_IMPRESORAS/EXPORTAR  (Excel filtrado) ───────────────
    [HttpGet("/REPORTES_IMPRESORAS/EXPORTAR")]
    public async Task<IActionResult> ExportarReportes(
        [FromQuery] string? FOLIO                = null,
        [FromQuery] string? FECHA                = null,
        [FromQuery] string? PLANTA               = null,
        [FromQuery] string? IMPRESORA            = null,
        [FromQuery] string? AREA                 = null,
        [FromQuery] string? REPORTE              = null,
        [FromQuery] string? QUIEN_REPORTA        = null,
        [FromQuery] string? ESTATUS              = null,
        [FromQuery] string? FECHA_DE_REALIZACION = null,
        [FromQuery] string? COMENTARIOS          = null)
    {
        var filtros = new ReporteImpresoraFiltros
        {
            FOLIO                = FOLIO,
            FECHA                = FECHA,
            PLANTA               = PLANTA,
            IMPRESORA            = IMPRESORA,
            AREA                 = AREA,
            REPORTE              = REPORTE,
            QUIEN_REPORTA        = QUIEN_REPORTA,
            ESTATUS              = ESTATUS,
            FECHA_DE_REALIZACION = FECHA_DE_REALIZACION,
            COMENTARIOS          = COMENTARIOS
        };

        var bytes = await _svc.ExportarReportesAsync(filtros);
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                    "reportes_impresoras_filtrado.xlsx");
    }

    // ── GET /REPORTES_IMPRESORAS/EXPORTAR_TODO ────────────────────────────
    [HttpGet("/REPORTES_IMPRESORAS/EXPORTAR_TODO")]
    public async Task<IActionResult> ExportarReportesTodo()
    {
        var bytes = await _svc.ExportarReportesAsync(new ReporteImpresoraFiltros());
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                    "reportes_impresoras_total.xlsx");
    }

    // ── GET /REPORTES_IMPRESORAS/EXPORTAR_ANIO ────────────────────────────
    [HttpGet("/REPORTES_IMPRESORAS/EXPORTAR_ANIO")]
    public async Task<IActionResult> ExportarReportesPorAnio([FromQuery] int anio)
    {
        var bytes = await _svc.ExportarReportesPorAnioAsync(anio);
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                    $"reportes_impresoras_{anio}.xlsx");
    }

    // ════════════════════════════════════════════════════════════════════════
    //  IMPRESORAS_INFO
    // ════════════════════════════════════════════════════════════════════════

    // ── GET /IMPRESORAS_INFO  (paginado + filtros) ────────────────────────
    [HttpGet("/IMPRESORAS_INFO")]
    public async Task<IActionResult> ListarInfo(
        [FromQuery] int page = 1,
        [FromQuery] int limit = 10,
        [FromQuery] string? IMPRESORA       = null,
        [FromQuery] string? MODELO          = null,
        [FromQuery] string? NUMERO_DE_SERIE = null,
        [FromQuery] string? IP              = null,
        [FromQuery] string? UBICACION       = null,
        [FromQuery] string? PLANTA          = null,
        [FromQuery] string? IDENTIFICADOR   = null)
    {
        var filtros = new ImpresoraInfoFiltros
        {
            IMPRESORA       = IMPRESORA,
            MODELO          = MODELO,
            NUMERO_DE_SERIE = NUMERO_DE_SERIE,
            IP              = IP,
            UBICACION       = UBICACION,
            PLANTA          = PLANTA,
            IDENTIFICADOR   = IDENTIFICADOR
        };

        var resultado = await _svc.ListarInfoAsync(page, limit, filtros);
        return Ok(resultado);
    }

    // ── POST /IMPRESORA_INFO  (crear) ─────────────────────────────────────
    [HttpPost("/IMPRESORA_INFO")]
    public async Task<IActionResult> CrearInfo([FromBody] ImpresoraInfoDto dto)
    {
        var usuario = ObtenerUsuario();
        var id = await _svc.CrearInfoAsync(dto, usuario);
        return Ok(new { id });
    }

    // ── PUT /IMPRESORA_INFO/{id}  (editar) ────────────────────────────────
    [HttpPut("/IMPRESORA_INFO/{id:int}")]
    public async Task<IActionResult> EditarInfo(int id, [FromBody] ImpresoraInfoDto dto)
    {
        var usuario = ObtenerUsuario();
        var ok = await _svc.EditarInfoAsync(id, dto, usuario);
        return ok ? Ok(new { ok = true }) : NotFound(new { error = "Registro no encontrado" });
    }

    // ── DELETE /IMPRESORA_INFO/{id}  (eliminar) ───────────────────────────
    [HttpDelete("/IMPRESORA_INFO/{id:int}")]
    public async Task<IActionResult> EliminarInfo(int id)
    {
        var ok = await _svc.EliminarInfoAsync(id);
        return ok ? Ok(new { ok = true }) : NotFound(new { error = "Registro no encontrado" });
    }

    // ── GET /IMPRESORAS_INFO/{id}/HISTORIAL ───────────────────────────────
    [HttpGet("/IMPRESORAS_INFO/{id:int}/HISTORIAL")]
    public async Task<IActionResult> HistorialInfo(int id)
    {
        var historial = await _svc.HistorialInfoAsync(id);
        return Ok(new { historial });
    }

    // ── GET /IMPRESORAS_INFO/EXPORTAR  (Excel filtrado) ───────────────────
    [HttpGet("/IMPRESORAS_INFO/EXPORTAR")]
    public async Task<IActionResult> ExportarInfo(
        [FromQuery] string? IMPRESORA       = null,
        [FromQuery] string? MODELO          = null,
        [FromQuery] string? NUMERO_DE_SERIE = null,
        [FromQuery] string? IP              = null,
        [FromQuery] string? UBICACION       = null,
        [FromQuery] string? PLANTA          = null,
        [FromQuery] string? IDENTIFICADOR   = null)
    {
        var filtros = new ImpresoraInfoFiltros
        {
            IMPRESORA       = IMPRESORA,
            MODELO          = MODELO,
            NUMERO_DE_SERIE = NUMERO_DE_SERIE,
            IP              = IP,
            UBICACION       = UBICACION,
            PLANTA          = PLANTA,
            IDENTIFICADOR   = IDENTIFICADOR
        };

        var bytes = await _svc.ExportarInfoAsync(filtros);
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                    "impresoras_info_filtrado.xlsx");
    }

    // ── GET /IMPRESORAS_INFO/EXPORTAR_TODO ────────────────────────────────
    [HttpGet("/IMPRESORAS_INFO/EXPORTAR_TODO")]
    public async Task<IActionResult> ExportarInfoTodo()
    {
        var bytes = await _svc.ExportarInfoAsync(new ImpresoraInfoFiltros());
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                    "impresoras_info_total.xlsx");
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
