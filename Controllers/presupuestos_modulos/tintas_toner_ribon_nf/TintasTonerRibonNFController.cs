using Microsoft.AspNetCore.Mvc;
using ChiIT.Services;

namespace ChiIT.Controllers;

[ApiController]
public class TintasTonerRibonNFController : ControllerBase
{
    private readonly TintasTonerRibonNFService _svc;

    public TintasTonerRibonNFController(TintasTonerRibonNFService svc) => _svc = svc;

    // ── GET /TINTAS_TONER_RIBON_NF  (paginado + filtros) ─────────────────
    [HttpGet("/TINTAS_TONER_RIBON_NF")]
    public async Task<IActionResult> Listar(
        [FromQuery] int page = 1,
        [FromQuery] int limit = 10,
        [FromQuery] string? ID_UNICO          = null,
        [FromQuery] string? OC                = null,
        [FromQuery] string? MODELO            = null,
        [FromQuery] string? RECIBIDO_POR      = null,
        [FromQuery] string? SUBCATEGORIA      = null,
        [FromQuery] string? FECHA_REGISTRO    = null,
        [FromQuery] string? PROVEEDOR         = null,
        [FromQuery] string? UBICACION         = null,
        [FromQuery] string? IMPRESORA         = null,
        [FromQuery] string? INSTALADO_POR     = null)
    {
        var filtros = new TintaTonerRibonNFFiltros
        {
            ID_UNICO       = ID_UNICO,
            OC             = OC,
            MODELO         = MODELO,
            RECIBIDO_POR   = RECIBIDO_POR,
            SUBCATEGORIA   = SUBCATEGORIA,
            FECHA_REGISTRO = FECHA_REGISTRO,
            PROVEEDOR      = PROVEEDOR,
            UBICACION      = UBICACION,
            IMPRESORA      = IMPRESORA,
            INSTALADO_POR  = INSTALADO_POR
        };

        var resultado = await _svc.ListarAsync(page, limit, filtros);
        return Ok(resultado);
    }

    // ── POST /TINTA_TONER_RIBON_NF  (crear) ──────────────────────────────
    [HttpPost("/TINTA_TONER_RIBON_NF")]
    public async Task<IActionResult> Crear([FromBody] TintaTonerRibonNFDto dto)
    {
        var usuario = ObtenerUsuario();
        var id = await _svc.CrearAsync(dto, usuario);
        return Ok(new { id });
    }

    // ── PUT /TINTA_TONER_RIBON_NF/{id}  (editar) ─────────────────────────
    [HttpPut("/TINTA_TONER_RIBON_NF/{id:int}")]
    public async Task<IActionResult> Editar(int id, [FromBody] TintaTonerRibonNFDto dto)
    {
        var usuario = ObtenerUsuario();
        var ok = await _svc.EditarAsync(id, dto, usuario);
        return ok ? Ok(new { ok = true }) : NotFound(new { error = "Registro no encontrado" });
    }

    // ── DELETE /TINTA_TONER_RIBON_NF/{id}  (eliminar) ────────────────────
    [HttpDelete("/TINTA_TONER_RIBON_NF/{id:int}")]
    public async Task<IActionResult> Eliminar(int id)
    {
        var ok = await _svc.EliminarAsync(id);
        return ok ? Ok(new { ok = true }) : NotFound(new { error = "Registro no encontrado" });
    }

    // ── GET /TINTAS_TONER_RIBON_NF/{id}/HISTORIAL ────────────────────────
    [HttpGet("/TINTAS_TONER_RIBON_NF/{id:int}/HISTORIAL")]
    public async Task<IActionResult> Historial(int id)
    {
        var historial = await _svc.HistorialAsync(id);
        return Ok(new { historial });
    }

    // ── GET /TINTAS_TONER_RIBON_NF/EXPORTAR  (Excel filtrado) ────────────
    [HttpGet("/TINTAS_TONER_RIBON_NF/EXPORTAR")]
    public async Task<IActionResult> ExportarFiltrado(
        [FromQuery] string? ID_UNICO       = null,
        [FromQuery] string? OC             = null,
        [FromQuery] string? MODELO         = null,
        [FromQuery] string? RECIBIDO_POR   = null,
        [FromQuery] string? SUBCATEGORIA   = null,
        [FromQuery] string? FECHA_REGISTRO = null,
        [FromQuery] string? PROVEEDOR      = null,
        [FromQuery] string? UBICACION      = null,
        [FromQuery] string? IMPRESORA      = null,
        [FromQuery] string? INSTALADO_POR  = null)
    {
        var filtros = new TintaTonerRibonNFFiltros
        {
            ID_UNICO       = ID_UNICO,
            OC             = OC,
            MODELO         = MODELO,
            RECIBIDO_POR   = RECIBIDO_POR,
            SUBCATEGORIA   = SUBCATEGORIA,
            FECHA_REGISTRO = FECHA_REGISTRO,
            PROVEEDOR      = PROVEEDOR,
            UBICACION      = UBICACION,
            IMPRESORA      = IMPRESORA,
            INSTALADO_POR  = INSTALADO_POR
        };

        var bytes = await _svc.ExportarAsync(filtros);
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                    "tintas_toner_ribon_nf_filtrado.xlsx");
    }

    // ── GET /TINTAS_TONER_RIBON_NF/EXPORTAR_TODO ─────────────────────────
    [HttpGet("/TINTAS_TONER_RIBON_NF/EXPORTAR_TODO")]
    public async Task<IActionResult> ExportarTodo()
    {
        var bytes = await _svc.ExportarAsync(new TintaTonerRibonNFFiltros());
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                    "tintas_toner_ribon_nf_total.xlsx");
    }

    // ── GET /TINTAS_TONER_RIBON_NF/EXPORTAR_ANIO ─────────────────────────
    [HttpGet("/TINTAS_TONER_RIBON_NF/EXPORTAR_ANIO")]
    public async Task<IActionResult> ExportarPorAnio([FromQuery] int anio)
    {
        var bytes = await _svc.ExportarPorAnioAsync(anio);
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                    $"tintas_toner_ribon_nf_{anio}.xlsx");
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
