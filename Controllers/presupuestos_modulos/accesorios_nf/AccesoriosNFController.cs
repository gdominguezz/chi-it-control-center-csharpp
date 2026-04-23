using Microsoft.AspNetCore.Mvc;
using ChiIT.Services;

namespace ChiIT.Controllers;

[ApiController]
public class AccesoriosNFController : ControllerBase
{
    private readonly AccesoriosNFService _svc;

    public AccesoriosNFController(AccesoriosNFService svc) => _svc = svc;

    // ── GET /ACCESORIOS_NF  (paginado + filtros) ──────────────────────────
    [HttpGet("/ACCESORIOS_NF")]
    public async Task<IActionResult> Listar(
        [FromQuery] int page = 1,
        [FromQuery] int limit = 10,
        [FromQuery] string? ID_UNICO               = null,
        [FromQuery] string? OC                     = null,
        [FromQuery] string? FOLIO                  = null,
        [FromQuery] string? FECHA_ENTRADA          = null,
        [FromQuery] string? RECIBIDO_POR           = null,
        [FromQuery] string? SUBCATEGORIA           = null,
        [FromQuery] string? MARCA                  = null,
        [FromQuery] string? MODELO                 = null,
        [FromQuery] string? NO_SERIE               = null,
        [FromQuery] string? TIPO                   = null,
        [FromQuery] string? ACCESORIOS             = null,
        [FromQuery] string? PROVEEDOR              = null,
        [FromQuery] string? MONEDA                 = null,
        [FromQuery] string? DISPONIBLE             = null,
        [FromQuery] string? ASIGNADO_A             = null,
        [FromQuery] string? DESTINO_PLANTA         = null,
        [FromQuery] string? PERSONAL_IT_QUE_ASIGNA = null)
    {
        var filtros = new AccesorioNFFiltros
        {
            ID_UNICO               = ID_UNICO,
            OC                     = OC,
            FOLIO                  = FOLIO,
            FECHA_ENTRADA          = FECHA_ENTRADA,
            RECIBIDO_POR           = RECIBIDO_POR,
            SUBCATEGORIA           = SUBCATEGORIA,
            MARCA                  = MARCA,
            MODELO                 = MODELO,
            NO_SERIE               = NO_SERIE,
            TIPO                   = TIPO,
            ACCESORIOS             = ACCESORIOS,
            PROVEEDOR              = PROVEEDOR,
            MONEDA                 = MONEDA,
            DISPONIBLE             = DISPONIBLE,
            ASIGNADO_A             = ASIGNADO_A,
            DESTINO_PLANTA         = DESTINO_PLANTA,
            PERSONAL_IT_QUE_ASIGNA = PERSONAL_IT_QUE_ASIGNA
        };

        var resultado = await _svc.ListarAsync(page, limit, filtros);
        return Ok(resultado);
    }

    // ── POST /ACCESORIO_NF  (crear) ───────────────────────────────────────
    [HttpPost("/ACCESORIO_NF")]
    public async Task<IActionResult> Crear([FromBody] AccesorioNFDto dto)
    {
        var usuario = ObtenerUsuario();
        var id = await _svc.CrearAsync(dto, usuario);
        return Ok(new { id });
    }

    // ── PUT /ACCESORIO_NF/{id}  (editar) ──────────────────────────────────
    [HttpPut("/ACCESORIO_NF/{id:int}")]
    public async Task<IActionResult> Editar(int id, [FromBody] AccesorioNFDto dto)
    {
        var usuario = ObtenerUsuario();
        var ok = await _svc.EditarAsync(id, dto, usuario);
        return ok ? Ok(new { ok = true }) : NotFound(new { error = "Registro no encontrado" });
    }

    // ── DELETE /ACCESORIO_NF/{id}  (eliminar) ─────────────────────────────
    [HttpDelete("/ACCESORIO_NF/{id:int}")]
    public async Task<IActionResult> Eliminar(int id)
    {
        var ok = await _svc.EliminarAsync(id);
        return ok ? Ok(new { ok = true }) : NotFound(new { error = "Registro no encontrado" });
    }

    // ── GET /ACCESORIOS_NF/{id}/HISTORIAL ────────────────────────────────
    [HttpGet("/ACCESORIOS_NF/{id:int}/HISTORIAL")]
    public async Task<IActionResult> Historial(int id)
    {
        var historial = await _svc.HistorialAsync(id);
        return Ok(new { historial });
    }

    // ── GET /ACCESORIOS_NF/EXPORTAR  (Excel filtrado) ────────────────────
    [HttpGet("/ACCESORIOS_NF/EXPORTAR")]
    public async Task<IActionResult> ExportarFiltrado(
        [FromQuery] string? ID_UNICO               = null,
        [FromQuery] string? OC                     = null,
        [FromQuery] string? FOLIO                  = null,
        [FromQuery] string? FECHA_ENTRADA          = null,
        [FromQuery] string? RECIBIDO_POR           = null,
        [FromQuery] string? SUBCATEGORIA           = null,
        [FromQuery] string? MARCA                  = null,
        [FromQuery] string? MODELO                 = null,
        [FromQuery] string? NO_SERIE               = null,
        [FromQuery] string? TIPO                   = null,
        [FromQuery] string? ACCESORIOS             = null,
        [FromQuery] string? PROVEEDOR              = null,
        [FromQuery] string? MONEDA                 = null,
        [FromQuery] string? DISPONIBLE             = null,
        [FromQuery] string? ASIGNADO_A             = null,
        [FromQuery] string? DESTINO_PLANTA         = null,
        [FromQuery] string? PERSONAL_IT_QUE_ASIGNA = null)
    {
        var filtros = new AccesorioNFFiltros
        {
            ID_UNICO               = ID_UNICO,
            OC                     = OC,
            FOLIO                  = FOLIO,
            FECHA_ENTRADA          = FECHA_ENTRADA,
            RECIBIDO_POR           = RECIBIDO_POR,
            SUBCATEGORIA           = SUBCATEGORIA,
            MARCA                  = MARCA,
            MODELO                 = MODELO,
            NO_SERIE               = NO_SERIE,
            TIPO                   = TIPO,
            ACCESORIOS             = ACCESORIOS,
            PROVEEDOR              = PROVEEDOR,
            MONEDA                 = MONEDA,
            DISPONIBLE             = DISPONIBLE,
            ASIGNADO_A             = ASIGNADO_A,
            DESTINO_PLANTA         = DESTINO_PLANTA,
            PERSONAL_IT_QUE_ASIGNA = PERSONAL_IT_QUE_ASIGNA
        };

        var bytes = await _svc.ExportarAsync(filtros);
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                    "accesorios_nf_filtrado.xlsx");
    }

    // ── GET /ACCESORIOS_NF/EXPORTAR_TODO ─────────────────────────────────
    [HttpGet("/ACCESORIOS_NF/EXPORTAR_TODO")]
    public async Task<IActionResult> ExportarTodo()
    {
        var bytes = await _svc.ExportarAsync(new AccesorioNFFiltros());
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                    "accesorios_nf_total.xlsx");
    }

    // ── GET /ACCESORIOS_NF/EXPORTAR_ANIO ─────────────────────────────────
    [HttpGet("/ACCESORIOS_NF/EXPORTAR_ANIO")]
    public async Task<IActionResult> ExportarPorAnio([FromQuery] int anio)
    {
        var bytes = await _svc.ExportarPorAnioAsync(anio);
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                    $"accesorios_nf_{anio}.xlsx");
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
