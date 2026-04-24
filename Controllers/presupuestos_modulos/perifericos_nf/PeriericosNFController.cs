using Microsoft.AspNetCore.Mvc;
using ChiIT.Services;

namespace ChiIT.Controllers;

[ApiController]
public class PeriericosNFController : ControllerBase
{
    private readonly PerifeicosNFService _svc;

    public PeriericosNFController(PerifeicosNFService svc) => _svc = svc;

    // ── GET /PERIFERICOS_NF  (paginado + filtros) ─────────────────────────
    [HttpGet("/PERIFERICOS_NF")]
    public async Task<IActionResult> Listar(
        [FromQuery] int page = 1,
        [FromQuery] int limit = 10,
        [FromQuery] string? ID_UNICO               = null,
        [FromQuery] string? OC                     = null,
        [FromQuery] string? FOLIO_INVENTARIO       = null,
        [FromQuery] string? FECHA_ENTRADA          = null,
        [FromQuery] string? RECIBIDO_POR           = null,
        [FromQuery] string? SUBCATEGORIA           = null,
        [FromQuery] string? TIPO                   = null,
        [FromQuery] string? MARCA                  = null,
        [FromQuery] string? MODELO                 = null,
        [FromQuery] string? NUMERO_SERIE           = null,
        [FromQuery] string? PROVEEDOR              = null,
        [FromQuery] string? ESTADO                 = null,
        [FromQuery] string? DESTINO                = null,
        [FromQuery] string? DISPONIBLE             = null,
        [FromQuery] string? DESTINO_PLANTA         = null,
        [FromQuery] string? ASIGNADO_A             = null,
        [FromQuery] string? PERSONAL_IT_QUE_ASIGNA = null)
    {
        var filtros = new PerifericoNFFiltros
        {
            ID_UNICO               = ID_UNICO,
            OC                     = OC,
            FOLIO_INVENTARIO       = FOLIO_INVENTARIO,
            FECHA_ENTRADA          = FECHA_ENTRADA,
            RECIBIDO_POR           = RECIBIDO_POR,
            SUBCATEGORIA           = SUBCATEGORIA,
            TIPO                   = TIPO,
            MARCA                  = MARCA,
            MODELO                 = MODELO,
            NUMERO_SERIE           = NUMERO_SERIE,
            PROVEEDOR              = PROVEEDOR,
            ESTADO                 = ESTADO,
            DESTINO                = DESTINO,
            DISPONIBLE             = DISPONIBLE,
            DESTINO_PLANTA         = DESTINO_PLANTA,
            ASIGNADO_A             = ASIGNADO_A,
            PERSONAL_IT_QUE_ASIGNA = PERSONAL_IT_QUE_ASIGNA
        };

        var resultado = await _svc.ListarAsync(page, limit, filtros);
        return Ok(resultado);
    }

    // ── POST /PERIFERICO_NF  (crear) ──────────────────────────────────────
    [HttpPost("/PERIFERICO_NF")]
    public async Task<IActionResult> Crear([FromBody] PerifericoNFDto dto)
    {
        var usuario = ObtenerUsuario();
        var id = await _svc.CrearAsync(dto, usuario);
        return Ok(new { id });
    }

    // ── PUT /PERIFERICO_NF/{id}  (editar) ─────────────────────────────────
    [HttpPut("/PERIFERICO_NF/{id:int}")]
    public async Task<IActionResult> Editar(int id, [FromBody] PerifericoNFDto dto)
    {
        var usuario = ObtenerUsuario();
        var ok = await _svc.EditarAsync(id, dto, usuario);
        return ok ? Ok(new { ok = true }) : NotFound(new { error = "Registro no encontrado" });
    }

    // ── DELETE /PERIFERICO_NF/{id}  (eliminar) ────────────────────────────
    [HttpDelete("/PERIFERICO_NF/{id:int}")]
    public async Task<IActionResult> Eliminar(int id)
    {
        var ok = await _svc.EliminarAsync(id);
        return ok ? Ok(new { ok = true }) : NotFound(new { error = "Registro no encontrado" });
    }

    // ── GET /PERIFERICOS_NF/{id}/HISTORIAL ───────────────────────────────
    [HttpGet("/PERIFERICOS_NF/{id:int}/HISTORIAL")]
    public async Task<IActionResult> Historial(int id)
    {
        var historial = await _svc.HistorialAsync(id);
        return Ok(new { historial });
    }

    // ── GET /PERIFERICOS_NF/EXPORTAR  (Excel filtrado) ───────────────────
    [HttpGet("/PERIFERICOS_NF/EXPORTAR")]
    public async Task<IActionResult> ExportarFiltrado(
        [FromQuery] string? ID_UNICO               = null,
        [FromQuery] string? OC                     = null,
        [FromQuery] string? FOLIO_INVENTARIO       = null,
        [FromQuery] string? FECHA_ENTRADA          = null,
        [FromQuery] string? RECIBIDO_POR           = null,
        [FromQuery] string? SUBCATEGORIA           = null,
        [FromQuery] string? TIPO                   = null,
        [FromQuery] string? MARCA                  = null,
        [FromQuery] string? MODELO                 = null,
        [FromQuery] string? NUMERO_SERIE           = null,
        [FromQuery] string? PROVEEDOR              = null,
        [FromQuery] string? ESTADO                 = null,
        [FromQuery] string? DESTINO                = null,
        [FromQuery] string? DISPONIBLE             = null,
        [FromQuery] string? DESTINO_PLANTA         = null,
        [FromQuery] string? ASIGNADO_A             = null,
        [FromQuery] string? PERSONAL_IT_QUE_ASIGNA = null)
    {
        var filtros = new PerifericoNFFiltros
        {
            ID_UNICO               = ID_UNICO,
            OC                     = OC,
            FOLIO_INVENTARIO       = FOLIO_INVENTARIO,
            FECHA_ENTRADA          = FECHA_ENTRADA,
            RECIBIDO_POR           = RECIBIDO_POR,
            SUBCATEGORIA           = SUBCATEGORIA,
            TIPO                   = TIPO,
            MARCA                  = MARCA,
            MODELO                 = MODELO,
            NUMERO_SERIE           = NUMERO_SERIE,
            PROVEEDOR              = PROVEEDOR,
            ESTADO                 = ESTADO,
            DESTINO                = DESTINO,
            DISPONIBLE             = DISPONIBLE,
            DESTINO_PLANTA         = DESTINO_PLANTA,
            ASIGNADO_A             = ASIGNADO_A,
            PERSONAL_IT_QUE_ASIGNA = PERSONAL_IT_QUE_ASIGNA
        };

        var bytes = await _svc.ExportarAsync(filtros);
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                    "perifericos_nf_filtrado.xlsx");
    }

    // ── GET /PERIFERICOS_NF/EXPORTAR_TODO ────────────────────────────────
    [HttpGet("/PERIFERICOS_NF/EXPORTAR_TODO")]
    public async Task<IActionResult> ExportarTodo()
    {
        var bytes = await _svc.ExportarAsync(new PerifericoNFFiltros());
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                    "perifericos_nf_total.xlsx");
    }

    // ── GET /PERIFERICOS_NF/EXPORTAR_ANIO ────────────────────────────────
    [HttpGet("/PERIFERICOS_NF/EXPORTAR_ANIO")]
    public async Task<IActionResult> ExportarPorAnio([FromQuery] int anio)
    {
        var bytes = await _svc.ExportarPorAnioAsync(anio);
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                    $"perifericos_nf_{anio}.xlsx");
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
