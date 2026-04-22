using Microsoft.AspNetCore.Mvc;
using ChiIT.Services;

namespace ChiIT.Controllers;

[ApiController]
public class RefaccionesNFController : ControllerBase
{
    private readonly RefaccionesNFService _svc;

    public RefaccionesNFController(RefaccionesNFService svc) => _svc = svc;

    // ── GET /REFACCIONES_NF  (paginado + filtros) ─────────────────────────
    [HttpGet("/REFACCIONES_NF")]
    public async Task<IActionResult> Listar(
        [FromQuery] int page = 1,
        [FromQuery] int limit = 10,
        [FromQuery] string? ID_UNICO         = null,
        [FromQuery] string? OC               = null,
        [FromQuery] string? FOLIO_CORRECTIVO = null,
        [FromQuery] string? FECHA_REGISTRO   = null,
        [FromQuery] string? RECIBIDO_POR     = null,
        [FromQuery] string? SUBCATEGORIA     = null,
        [FromQuery] string? MARCA            = null,
        [FromQuery] string? MODELO           = null,
        [FromQuery] string? SERIE            = null,
        [FromQuery] string? NUM_PARTE        = null,
        [FromQuery] string? MONEDA           = null,
        [FromQuery] string? PROVEEDOR        = null,
        [FromQuery] string? DISPONIBLE       = null,
        [FromQuery] string? COMENTARIOS      = null)
    {
        var filtros = new RefaccionNFFiltros
        {
            ID_UNICO         = ID_UNICO,
            OC               = OC,
            FOLIO_CORRECTIVO = FOLIO_CORRECTIVO,
            FECHA_REGISTRO   = FECHA_REGISTRO,
            RECIBIDO_POR     = RECIBIDO_POR,
            SUBCATEGORIA     = SUBCATEGORIA,
            MARCA            = MARCA,
            MODELO           = MODELO,
            SERIE            = SERIE,
            NUM_PARTE        = NUM_PARTE,
            MONEDA           = MONEDA,
            PROVEEDOR        = PROVEEDOR,
            DISPONIBLE       = DISPONIBLE,
            COMENTARIOS      = COMENTARIOS
        };

        var resultado = await _svc.ListarAsync(page, limit, filtros);
        return Ok(resultado);
    }

    // ── POST /REFACCION_NF  (crear) ───────────────────────────────────────
    [HttpPost("/REFACCION_NF")]
    public async Task<IActionResult> Crear([FromBody] RefaccionNFDto dto)
    {
        var usuario = ObtenerUsuario();
        var id = await _svc.CrearAsync(dto, usuario);
        return Ok(new { id });
    }

    // ── PUT /REFACCION_NF/{id}  (editar) ──────────────────────────────────
    [HttpPut("/REFACCION_NF/{id:int}")]
    public async Task<IActionResult> Editar(int id, [FromBody] RefaccionNFDto dto)
    {
        var usuario = ObtenerUsuario();
        var ok = await _svc.EditarAsync(id, dto, usuario);
        return ok ? Ok(new { ok = true }) : NotFound(new { error = "Registro no encontrado" });
    }

    // ── DELETE /REFACCION_NF/{id}  (eliminar) ─────────────────────────────
    [HttpDelete("/REFACCION_NF/{id:int}")]
    public async Task<IActionResult> Eliminar(int id)
    {
        var ok = await _svc.EliminarAsync(id);
        return ok ? Ok(new { ok = true }) : NotFound(new { error = "Registro no encontrado" });
    }

    // ── GET /REFACCIONES_NF/{id}/HISTORIAL ───────────────────────────────
    [HttpGet("/REFACCIONES_NF/{id:int}/HISTORIAL")]
    public async Task<IActionResult> Historial(int id)
    {
        var historial = await _svc.HistorialAsync(id);
        return Ok(new { historial });
    }

    // ── GET /REFACCIONES_NF/EXPORTAR  (Excel filtrado) ───────────────────
    [HttpGet("/REFACCIONES_NF/EXPORTAR")]
    public async Task<IActionResult> ExportarFiltrado(
        [FromQuery] string? ID_UNICO         = null,
        [FromQuery] string? OC               = null,
        [FromQuery] string? FOLIO_CORRECTIVO = null,
        [FromQuery] string? FECHA_REGISTRO   = null,
        [FromQuery] string? RECIBIDO_POR     = null,
        [FromQuery] string? SUBCATEGORIA     = null,
        [FromQuery] string? MARCA            = null,
        [FromQuery] string? MODELO           = null,
        [FromQuery] string? SERIE            = null,
        [FromQuery] string? NUM_PARTE        = null,
        [FromQuery] string? MONEDA           = null,
        [FromQuery] string? PROVEEDOR        = null,
        [FromQuery] string? DISPONIBLE       = null,
        [FromQuery] string? COMENTARIOS      = null)
    {
        var filtros = new RefaccionNFFiltros
        {
            ID_UNICO         = ID_UNICO,
            OC               = OC,
            FOLIO_CORRECTIVO = FOLIO_CORRECTIVO,
            FECHA_REGISTRO   = FECHA_REGISTRO,
            RECIBIDO_POR     = RECIBIDO_POR,
            SUBCATEGORIA     = SUBCATEGORIA,
            MARCA            = MARCA,
            MODELO           = MODELO,
            SERIE            = SERIE,
            NUM_PARTE        = NUM_PARTE,
            MONEDA           = MONEDA,
            PROVEEDOR        = PROVEEDOR,
            DISPONIBLE       = DISPONIBLE,
            COMENTARIOS      = COMENTARIOS
        };

        var bytes = await _svc.ExportarAsync(filtros);
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                    "refacciones_nf_filtrado.xlsx");
    }

    // ── GET /REFACCIONES_NF/EXPORTAR_TODO ────────────────────────────────
    [HttpGet("/REFACCIONES_NF/EXPORTAR_TODO")]
    public async Task<IActionResult> ExportarTodo()
    {
        var bytes = await _svc.ExportarAsync(new RefaccionNFFiltros());
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                    "refacciones_nf_total.xlsx");
    }

    // ── GET /REFACCIONES_NF/EXPORTAR_ANIO ────────────────────────────────
    [HttpGet("/REFACCIONES_NF/EXPORTAR_ANIO")]
    public async Task<IActionResult> ExportarPorAnio([FromQuery] int anio)
    {
        var bytes = await _svc.ExportarPorAnioAsync(anio);
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                    $"refacciones_nf_{anio}.xlsx");
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
