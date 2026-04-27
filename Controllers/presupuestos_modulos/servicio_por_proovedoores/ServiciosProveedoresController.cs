using Microsoft.AspNetCore.Mvc;
using ChiIT.Services;

namespace ChiIT.Controllers;

[ApiController]
public class ServiciosProveedoresController : ControllerBase
{
    private readonly ServiciosProveedoresService _svc;

    public ServiciosProveedoresController(ServiciosProveedoresService svc) => _svc = svc;

    // ── GET /SERVICIOS_PROVEEDORES  (paginado + filtros) ──────────────────
    [HttpGet("/SERVICIOS_PROVEEDORES")]
    public async Task<IActionResult> Listar(
        [FromQuery] int page = 1,
        [FromQuery] int limit = 10,
        [FromQuery] string? ID_UNICO               = null,
        [FromQuery] string? FOLIO_UNICO            = null,
        [FromQuery] string? FOLIO_COTIZACION       = null,
        [FromQuery] string? FOLIO_REPORTE          = null,
        [FromQuery] string? FECHA                  = null,
        [FromQuery] string? REQUISITOR             = null,
        [FromQuery] string? CUENTA_CON_POLIZA      = null,
        [FromQuery] string? SERVICIO_CON_COSTO     = null,
        [FromQuery] string? UBICACION_PLANTA       = null,
        [FromQuery] string? AREA                   = null,
        [FromQuery] string? DESCRIPCION_SERVICIO   = null,
        [FromQuery] string? PROVEEDORES            = null,
        [FromQuery] string? PERSONAL_RECIBIO       = null,
        [FromQuery] string? SOLICITUD_FINALIZADA   = null)
    {
        var filtros = new ServicioProveedorFiltros
        {
            ID_UNICO               = ID_UNICO,
            FOLIO_UNICO            = FOLIO_UNICO,
            FOLIO_COTIZACION       = FOLIO_COTIZACION,
            FOLIO_REPORTE          = FOLIO_REPORTE,
            FECHA                  = FECHA,
            REQUISITOR             = REQUISITOR,
            CUENTA_CON_POLIZA      = CUENTA_CON_POLIZA,
            SERVICIO_CON_COSTO     = SERVICIO_CON_COSTO,
            UBICACION_PLANTA       = UBICACION_PLANTA,
            AREA                   = AREA,
            DESCRIPCION_SERVICIO   = DESCRIPCION_SERVICIO,
            PROVEEDORES            = PROVEEDORES,
            PERSONAL_RECIBIO       = PERSONAL_RECIBIO,
            SOLICITUD_FINALIZADA   = SOLICITUD_FINALIZADA
        };

        var resultado = await _svc.ListarAsync(page, limit, filtros);
        return Ok(resultado);
    }

    // ── POST /SERVICIO_PROVEEDOR  (crear) ─────────────────────────────────
    [HttpPost("/SERVICIO_PROVEEDOR")]
    public async Task<IActionResult> Crear([FromBody] ServicioProveedorDto dto)
    {
        var usuario = ObtenerUsuario();
        var id = await _svc.CrearAsync(dto, usuario);
        return Ok(new { id });
    }

    // ── PUT /SERVICIO_PROVEEDOR/{id}  (editar) ────────────────────────────
    [HttpPut("/SERVICIO_PROVEEDOR/{id:int}")]
    public async Task<IActionResult> Editar(int id, [FromBody] ServicioProveedorDto dto)
    {
        var usuario = ObtenerUsuario();
        var ok = await _svc.EditarAsync(id, dto, usuario);
        return ok ? Ok(new { ok = true }) : NotFound(new { error = "Registro no encontrado" });
    }

    // ── DELETE /SERVICIO_PROVEEDOR/{id}  (eliminar) ───────────────────────
    [HttpDelete("/SERVICIO_PROVEEDOR/{id:int}")]
    public async Task<IActionResult> Eliminar(int id)
    {
        var ok = await _svc.EliminarAsync(id);
        return ok ? Ok(new { ok = true }) : NotFound(new { error = "Registro no encontrado" });
    }

    // ── GET /SERVICIOS_PROVEEDORES/{id}/HISTORIAL ─────────────────────────
    [HttpGet("/SERVICIOS_PROVEEDORES/{id:int}/HISTORIAL")]
    public async Task<IActionResult> Historial(int id)
    {
        var historial = await _svc.HistorialAsync(id);
        return Ok(new { historial });
    }

    // ── GET /SERVICIOS_PROVEEDORES/EXPORTAR  (Excel filtrado) ────────────
    [HttpGet("/SERVICIOS_PROVEEDORES/EXPORTAR")]
    public async Task<IActionResult> ExportarFiltrado(
        [FromQuery] string? ID_UNICO               = null,
        [FromQuery] string? FOLIO_UNICO            = null,
        [FromQuery] string? FOLIO_COTIZACION       = null,
        [FromQuery] string? FOLIO_REPORTE          = null,
        [FromQuery] string? FECHA                  = null,
        [FromQuery] string? REQUISITOR             = null,
        [FromQuery] string? CUENTA_CON_POLIZA      = null,
        [FromQuery] string? SERVICIO_CON_COSTO     = null,
        [FromQuery] string? UBICACION_PLANTA       = null,
        [FromQuery] string? AREA                   = null,
        [FromQuery] string? DESCRIPCION_SERVICIO   = null,
        [FromQuery] string? PROVEEDORES            = null,
        [FromQuery] string? PERSONAL_RECIBIO       = null,
        [FromQuery] string? SOLICITUD_FINALIZADA   = null)
    {
        var filtros = new ServicioProveedorFiltros
        {
            ID_UNICO               = ID_UNICO,
            FOLIO_UNICO            = FOLIO_UNICO,
            FOLIO_COTIZACION       = FOLIO_COTIZACION,
            FOLIO_REPORTE          = FOLIO_REPORTE,
            FECHA                  = FECHA,
            REQUISITOR             = REQUISITOR,
            CUENTA_CON_POLIZA      = CUENTA_CON_POLIZA,
            SERVICIO_CON_COSTO     = SERVICIO_CON_COSTO,
            UBICACION_PLANTA       = UBICACION_PLANTA,
            AREA                   = AREA,
            DESCRIPCION_SERVICIO   = DESCRIPCION_SERVICIO,
            PROVEEDORES            = PROVEEDORES,
            PERSONAL_RECIBIO       = PERSONAL_RECIBIO,
            SOLICITUD_FINALIZADA   = SOLICITUD_FINALIZADA
        };

        var bytes = await _svc.ExportarAsync(filtros);
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                    "servicios_proveedores_filtrado.xlsx");
    }

    // ── GET /SERVICIOS_PROVEEDORES/EXPORTAR_TODO ──────────────────────────
    [HttpGet("/SERVICIOS_PROVEEDORES/EXPORTAR_TODO")]
    public async Task<IActionResult> ExportarTodo()
    {
        var bytes = await _svc.ExportarAsync(new ServicioProveedorFiltros());
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                    "servicios_proveedores_total.xlsx");
    }

    // ── GET /SERVICIOS_PROVEEDORES/EXPORTAR_ANIO ──────────────────────────
    [HttpGet("/SERVICIOS_PROVEEDORES/EXPORTAR_ANIO")]
    public async Task<IActionResult> ExportarPorAnio([FromQuery] int anio)
    {
        var bytes = await _svc.ExportarPorAnioAsync(anio);
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                    $"servicios_proveedores_{anio}.xlsx");
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
