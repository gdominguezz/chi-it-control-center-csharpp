using Microsoft.AspNetCore.Mvc;
using ChiIT.Services;

namespace ChiIT.Controllers;

[ApiController]
public class HerramientasNFController : ControllerBase
{
    private readonly HerramientasNFService _svc;

    public HerramientasNFController(HerramientasNFService svc) => _svc = svc;

    // ── GET /HERRAMIENTAS_NF  (paginado + filtros) ────────────────────────
    [HttpGet("/HERRAMIENTAS_NF")]
    public async Task<IActionResult> Listar(
        [FromQuery] int page = 1,
        [FromQuery] int limit = 10,
        [FromQuery] string? ID_UNICO         = null,
        [FromQuery] string? OC               = null,
        [FromQuery] string? FOLIO_CORRECTIVO = null,
        [FromQuery] string? FECHA_REGISTRO   = null,
        [FromQuery] string? RECIBIDO_POR     = null,
        [FromQuery] string? SUBCATEGORIA     = null,
        [FromQuery] string? TIPO_USO         = null,
        [FromQuery] string? MARCA            = null,
        [FromQuery] string? MODELO           = null,
        [FromQuery] string? NUMERO_SERIE     = null,
        [FromQuery] string? NUM_PARTE        = null,
        [FromQuery] string? MONEDA           = null,
        [FromQuery] string? PROVEEDOR        = null,
        [FromQuery] string? UBICACION        = null)
    {
        var filtros = new HerramientaNFFiltros
        {
            ID_UNICO         = ID_UNICO,
            OC               = OC,
            FOLIO_CORRECTIVO = FOLIO_CORRECTIVO,
            FECHA_REGISTRO   = FECHA_REGISTRO,
            RECIBIDO_POR     = RECIBIDO_POR,
            SUBCATEGORIA     = SUBCATEGORIA,
            TIPO_USO         = TIPO_USO,
            MARCA            = MARCA,
            MODELO           = MODELO,
            NUMERO_SERIE     = NUMERO_SERIE,
            NUM_PARTE        = NUM_PARTE,
            MONEDA           = MONEDA,
            PROVEEDOR        = PROVEEDOR,
            UBICACION        = UBICACION
        };

        var resultado = await _svc.ListarAsync(page, limit, filtros);
        return Ok(resultado);
    }

    // ── POST /HERRAMIENTA_NF  (crear) ─────────────────────────────────────
    [HttpPost("/HERRAMIENTA_NF")]
    public async Task<IActionResult> Crear([FromBody] HerramientaNFDto dto)
    {
        var usuario = ObtenerUsuario();
        var id = await _svc.CrearAsync(dto, usuario);
        return Ok(new { id });
    }

    // ── PUT /HERRAMIENTA_NF/{id}  (editar) ────────────────────────────────
    [HttpPut("/HERRAMIENTA_NF/{id:int}")]
    public async Task<IActionResult> Editar(int id, [FromBody] HerramientaNFDto dto)
    {
        var usuario = ObtenerUsuario();
        var ok = await _svc.EditarAsync(id, dto, usuario);
        return ok ? Ok(new { ok = true }) : NotFound(new { error = "Registro no encontrado" });
    }

    // ── DELETE /HERRAMIENTA_NF/{id}  (eliminar) ───────────────────────────
    [HttpDelete("/HERRAMIENTA_NF/{id:int}")]
    public async Task<IActionResult> Eliminar(int id)
    {
        var ok = await _svc.EliminarAsync(id);
        return ok ? Ok(new { ok = true }) : NotFound(new { error = "Registro no encontrado" });
    }

    // ── GET /HERRAMIENTAS_NF/{id}/HISTORIAL ──────────────────────────────
    [HttpGet("/HERRAMIENTAS_NF/{id:int}/HISTORIAL")]
    public async Task<IActionResult> Historial(int id)
    {
        var historial = await _svc.HistorialAsync(id);
        return Ok(new { historial });
    }

    // ── GET /HERRAMIENTAS_NF/EXPORTAR  (Excel filtrado) ──────────────────
    [HttpGet("/HERRAMIENTAS_NF/EXPORTAR")]
    public async Task<IActionResult> ExportarFiltrado(
        [FromQuery] string? ID_UNICO         = null,
        [FromQuery] string? OC               = null,
        [FromQuery] string? FOLIO_CORRECTIVO = null,
        [FromQuery] string? FECHA_REGISTRO   = null,
        [FromQuery] string? RECIBIDO_POR     = null,
        [FromQuery] string? SUBCATEGORIA     = null,
        [FromQuery] string? TIPO_USO         = null,
        [FromQuery] string? MARCA            = null,
        [FromQuery] string? MODELO           = null,
        [FromQuery] string? NUMERO_SERIE     = null,
        [FromQuery] string? NUM_PARTE        = null,
        [FromQuery] string? MONEDA           = null,
        [FromQuery] string? PROVEEDOR        = null,
        [FromQuery] string? UBICACION        = null)
    {
        var filtros = new HerramientaNFFiltros
        {
            ID_UNICO         = ID_UNICO,
            OC               = OC,
            FOLIO_CORRECTIVO = FOLIO_CORRECTIVO,
            FECHA_REGISTRO   = FECHA_REGISTRO,
            RECIBIDO_POR     = RECIBIDO_POR,
            SUBCATEGORIA     = SUBCATEGORIA,
            TIPO_USO         = TIPO_USO,
            MARCA            = MARCA,
            MODELO           = MODELO,
            NUMERO_SERIE     = NUMERO_SERIE,
            NUM_PARTE        = NUM_PARTE,
            MONEDA           = MONEDA,
            PROVEEDOR        = PROVEEDOR,
            UBICACION        = UBICACION
        };

        var bytes = await _svc.ExportarAsync(filtros);
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                    "herramientas_nf_filtrado.xlsx");
    }

    // ── GET /HERRAMIENTAS_NF/EXPORTAR_TODO ───────────────────────────────
    [HttpGet("/HERRAMIENTAS_NF/EXPORTAR_TODO")]
    public async Task<IActionResult> ExportarTodo()
    {
        var bytes = await _svc.ExportarAsync(new HerramientaNFFiltros());
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                    "herramientas_nf_total.xlsx");
    }

    // ── GET /HERRAMIENTAS_NF/EXPORTAR_ANIO ───────────────────────────────
    [HttpGet("/HERRAMIENTAS_NF/EXPORTAR_ANIO")]
    public async Task<IActionResult> ExportarPorAnio([FromQuery] int anio)
    {
        var bytes = await _svc.ExportarPorAnioAsync(anio);
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                    $"herramientas_nf_{anio}.xlsx");
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
