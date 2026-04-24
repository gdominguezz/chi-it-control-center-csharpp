using Microsoft.AspNetCore.Mvc;
using ChiIT.Services;

namespace ChiIT.Controllers;

[ApiController]
public class InventariosNFController : ControllerBase
{
    private readonly InventariosNFService _svc;

    public InventariosNFController(InventariosNFService svc) => _svc = svc;

    // ── GET /INVENTARIOS_NF  (paginado + filtros) ─────────────────────────
    [HttpGet("/INVENTARIOS_NF")]
    public async Task<IActionResult> Listar(
        [FromQuery] int page = 1,
        [FromQuery] int limit = 10,
        [FromQuery] string? INV_FOLIO        = null,
        [FromQuery] string? EQUIPO           = null,
        [FromQuery] string? MARCA            = null,
        [FromQuery] string? MODELO           = null,
        [FromQuery] string? MONEDA           = null,
        [FromQuery] string? PROVEEDOR        = null,
        [FromQuery] string? PRESUPUESTO      = null,
        [FromQuery] string? STATUS           = null,
        [FromQuery] string? ANIO             = null,
        [FromQuery] string? OC               = null,
        [FromQuery] string? NUMERO_SERIE     = null,
        [FromQuery] string? UBICACION_ACTUAL = null)
    {
        var filtros = new InventarioNFFiltros
        {
            INV_FOLIO        = INV_FOLIO,
            EQUIPO           = EQUIPO,
            MARCA            = MARCA,
            MODELO           = MODELO,
            MONEDA           = MONEDA,
            PROVEEDOR        = PROVEEDOR,
            PRESUPUESTO      = PRESUPUESTO,
            STATUS           = STATUS,
            ANIO             = ANIO,
            OC               = OC,
            NUMERO_SERIE     = NUMERO_SERIE,
            UBICACION_ACTUAL = UBICACION_ACTUAL
        };

        var resultado = await _svc.ListarAsync(page, limit, filtros);
        return Ok(resultado);
    }

    // ── POST /INVENTARIO_NF  (crear) ──────────────────────────────────────
    [HttpPost("/INVENTARIO_NF")]
    public async Task<IActionResult> Crear([FromBody] InventarioNFDto dto)
    {
        var usuario = ObtenerUsuario();
        var id = await _svc.CrearAsync(dto, usuario);
        return Ok(new { id });
    }

    // ── PUT /INVENTARIO_NF/{id}  (editar) ─────────────────────────────────
    [HttpPut("/INVENTARIO_NF/{id:int}")]
    public async Task<IActionResult> Editar(int id, [FromBody] InventarioNFDto dto)
    {
        var usuario = ObtenerUsuario();
        var ok = await _svc.EditarAsync(id, dto, usuario);
        return ok ? Ok(new { ok = true }) : NotFound(new { error = "Registro no encontrado" });
    }

    // ── DELETE /INVENTARIO_NF/{id}  (eliminar) ────────────────────────────
    [HttpDelete("/INVENTARIO_NF/{id:int}")]
    public async Task<IActionResult> Eliminar(int id)
    {
        var ok = await _svc.EliminarAsync(id);
        return ok ? Ok(new { ok = true }) : NotFound(new { error = "Registro no encontrado" });
    }

    // ── GET /INVENTARIOS_NF/{id}/HISTORIAL ───────────────────────────────
    [HttpGet("/INVENTARIOS_NF/{id:int}/HISTORIAL")]
    public async Task<IActionResult> Historial(int id)
    {
        var historial = await _svc.HistorialAsync(id);
        return Ok(new { historial });
    }

    // ── GET /INVENTARIOS_NF/EXPORTAR  (Excel filtrado) ───────────────────
    [HttpGet("/INVENTARIOS_NF/EXPORTAR")]
    public async Task<IActionResult> ExportarFiltrado(
        [FromQuery] string? INV_FOLIO        = null,
        [FromQuery] string? EQUIPO           = null,
        [FromQuery] string? MARCA            = null,
        [FromQuery] string? MODELO           = null,
        [FromQuery] string? MONEDA           = null,
        [FromQuery] string? PROVEEDOR        = null,
        [FromQuery] string? PRESUPUESTO      = null,
        [FromQuery] string? STATUS           = null,
        [FromQuery] string? ANIO             = null,
        [FromQuery] string? OC               = null,
        [FromQuery] string? NUMERO_SERIE     = null,
        [FromQuery] string? UBICACION_ACTUAL = null)
    {
        var filtros = new InventarioNFFiltros
        {
            INV_FOLIO        = INV_FOLIO,
            EQUIPO           = EQUIPO,
            MARCA            = MARCA,
            MODELO           = MODELO,
            MONEDA           = MONEDA,
            PROVEEDOR        = PROVEEDOR,
            PRESUPUESTO      = PRESUPUESTO,
            STATUS           = STATUS,
            ANIO             = ANIO,
            OC               = OC,
            NUMERO_SERIE     = NUMERO_SERIE,
            UBICACION_ACTUAL = UBICACION_ACTUAL
        };

        var bytes = await _svc.ExportarAsync(filtros);
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                    "inventarios_nf_filtrado.xlsx");
    }

    // ── GET /INVENTARIOS_NF/EXPORTAR_TODO ────────────────────────────────
    [HttpGet("/INVENTARIOS_NF/EXPORTAR_TODO")]
    public async Task<IActionResult> ExportarTodo()
    {
        var bytes = await _svc.ExportarAsync(new InventarioNFFiltros());
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                    "inventarios_nf_total.xlsx");
    }

    // ── GET /INVENTARIOS_NF/EXPORTAR_ANIO ────────────────────────────────
    [HttpGet("/INVENTARIOS_NF/EXPORTAR_ANIO")]
    public async Task<IActionResult> ExportarPorAnio([FromQuery] int anio)
    {
        var bytes = await _svc.ExportarPorAnioAsync(anio);
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                    $"inventarios_nf_{anio}.xlsx");
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
