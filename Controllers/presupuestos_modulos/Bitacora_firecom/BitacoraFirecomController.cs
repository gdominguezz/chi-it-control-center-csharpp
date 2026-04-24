using Microsoft.AspNetCore.Mvc;
using ChiIT.Services;

namespace ChiIT.Controllers;

[ApiController]
public class BitacoraFirecomController : ControllerBase
{
    private readonly BitacoraFirecomService _svc;

    public BitacoraFirecomController(BitacoraFirecomService svc) => _svc = svc;

    // ── GET /BITACORA_FIRECOM  (paginado + filtros) ───────────────────────
    [HttpGet("/BITACORA_FIRECOM")]
    public async Task<IActionResult> Listar(
        [FromQuery] int page = 1,
        [FromQuery] int limit = 10,
        [FromQuery] string? ID_UNICO = null,
        [FromQuery] string? OC = null,
        [FromQuery] string? ORDEN_SERVICIO = null,
        [FromQuery] string? FECHA = null,
        [FromQuery] string? PERSONA_QUE_SOLICITA_REPORTA = null,
        [FromQuery] string? CUENTA_CON_POLIZA = null,
        [FromQuery] string? SERVICIO_CON_COSTO = null,
        [FromQuery] string? UBICACION = null,
        [FromQuery] string? AREA = null,
        [FromQuery] string? DESCRIPCION_SERVICIO = null,
        [FromQuery] string? DESCRIPCION_TRABAJO = null,
        [FromQuery] string? MATERIAL_EQUIPO = null,
        [FromQuery] string? OBSERVACIONES = null,
        [FromQuery] string? PROVEEDORES = null,
        [FromQuery] string? PANEL_FACEPLATE = null,
        [FromQuery] string? SWITCH_RED = null,
        [FromQuery] string? PERSONAL_QUE_RECIBIO = null,
        [FromQuery] string? PAGADO = null,
        [FromQuery] string? OC2 = null)
    {
        var filtros = new BitacoraFirecomFiltros
        {
            ID_UNICO = ID_UNICO,
            OC = OC,
            ORDEN_SERVICIO = ORDEN_SERVICIO,
            FECHA = FECHA,
            PERSONA_QUE_SOLICITA_REPORTA = PERSONA_QUE_SOLICITA_REPORTA,
            CUENTA_CON_POLIZA = CUENTA_CON_POLIZA,
            SERVICIO_CON_COSTO = SERVICIO_CON_COSTO,
            UBICACION = UBICACION,
            AREA = AREA,
            DESCRIPCION_SERVICIO = DESCRIPCION_SERVICIO,
            DESCRIPCION_TRABAJO = DESCRIPCION_TRABAJO,
            MATERIAL_EQUIPO = MATERIAL_EQUIPO,
            OBSERVACIONES = OBSERVACIONES,
            PROVEEDORES = PROVEEDORES,
            PANEL_FACEPLATE = PANEL_FACEPLATE,
            SWITCH_RED = SWITCH_RED,
            PERSONAL_QUE_RECIBIO = PERSONAL_QUE_RECIBIO,
            PAGADO = PAGADO,
            OC2 = OC2,
        };

        var resultado = await _svc.ListarAsync(page, limit, filtros);
        return Ok(resultado);
    }

    // ── POST /BITACORA_FIRECOM  (crear) ───────────────────────────────────
    [HttpPost("/BITACORA_FIRECOM")]
    public async Task<IActionResult> Crear([FromBody] BitacoraFirecomDto dto)
    {
        var usuario = ObtenerUsuario();
        var id = await _svc.CrearAsync(dto, usuario);
        return Ok(new { id });
    }

    // ── PUT /BITACORA_FIRECOM/{id}  (editar) ──────────────────────────────
    [HttpPut("/BITACORA_FIRECOM/{id:int}")]
    public async Task<IActionResult> Editar(int id, [FromBody] BitacoraFirecomDto dto)
    {
        var usuario = ObtenerUsuario();
        var ok = await _svc.EditarAsync(id, dto, usuario);
        return ok ? Ok(new { ok = true }) : NotFound(new { error = "Registro no encontrado" });
    }

    // ── DELETE /BITACORA_FIRECOM/{id}  (eliminar) ─────────────────────────
    [HttpDelete("/BITACORA_FIRECOM/{id:int}")]
    public async Task<IActionResult> Eliminar(int id)
    {
        var ok = await _svc.EliminarAsync(id);
        return ok ? Ok(new { ok = true }) : NotFound(new { error = "Registro no encontrado" });
    }

    // ── GET /BITACORA_FIRECOM/{id}/HISTORIAL ──────────────────────────────
    [HttpGet("/BITACORA_FIRECOM/{id:int}/HISTORIAL")]
    public async Task<IActionResult> Historial(int id)
    {
        var historial = await _svc.HistorialAsync(id);
        return Ok(new { historial });
    }

    // ── GET /BITACORA_FIRECOM/EXPORTAR  (Excel filtrado) ─────────────────
    [HttpGet("/BITACORA_FIRECOM/EXPORTAR")]
    public async Task<IActionResult> ExportarFiltrado(
        [FromQuery] string? ID_UNICO = null,
        [FromQuery] string? OC = null,
        [FromQuery] string? ORDEN_SERVICIO = null,
        [FromQuery] string? FECHA = null,
        [FromQuery] string? PERSONA_QUE_SOLICITA_REPORTA = null,
        [FromQuery] string? CUENTA_CON_POLIZA = null,
        [FromQuery] string? SERVICIO_CON_COSTO = null,
        [FromQuery] string? UBICACION = null,
        [FromQuery] string? AREA = null,
        [FromQuery] string? DESCRIPCION_SERVICIO = null,
        [FromQuery] string? DESCRIPCION_TRABAJO = null,
        [FromQuery] string? MATERIAL_EQUIPO = null,
        [FromQuery] string? OBSERVACIONES = null,
        [FromQuery] string? PROVEEDORES = null,
        [FromQuery] string? PANEL_FACEPLATE = null,
        [FromQuery] string? SWITCH_RED = null,
        [FromQuery] string? PERSONAL_QUE_RECIBIO = null,
        [FromQuery] string? PAGADO = null,
        [FromQuery] string? OC2 = null)
    {
        var filtros = new BitacoraFirecomFiltros
        {
            ID_UNICO = ID_UNICO,
            OC = OC,
            ORDEN_SERVICIO = ORDEN_SERVICIO,
            FECHA = FECHA,
            PERSONA_QUE_SOLICITA_REPORTA = PERSONA_QUE_SOLICITA_REPORTA,
            CUENTA_CON_POLIZA = CUENTA_CON_POLIZA,
            SERVICIO_CON_COSTO = SERVICIO_CON_COSTO,
            UBICACION = UBICACION,
            AREA = AREA,
            DESCRIPCION_SERVICIO = DESCRIPCION_SERVICIO,
            DESCRIPCION_TRABAJO = DESCRIPCION_TRABAJO,
            MATERIAL_EQUIPO = MATERIAL_EQUIPO,
            OBSERVACIONES = OBSERVACIONES,
            PROVEEDORES = PROVEEDORES,
            PANEL_FACEPLATE = PANEL_FACEPLATE,
            SWITCH_RED = SWITCH_RED,
            PERSONAL_QUE_RECIBIO = PERSONAL_QUE_RECIBIO,
            PAGADO = PAGADO,
            OC2 = OC2,
        };

        var bytes = await _svc.ExportarAsync(filtros);
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                    "bitacora_firecom_filtrado.xlsx");
    }

    // ── GET /BITACORA_FIRECOM/EXPORTAR_TODO ───────────────────────────────
    [HttpGet("/BITACORA_FIRECOM/EXPORTAR_TODO")]
    public async Task<IActionResult> ExportarTodo()
    {
        var bytes = await _svc.ExportarAsync(new BitacoraFirecomFiltros());
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                    "bitacora_firecom_total.xlsx");
    }

    // ── GET /BITACORA_FIRECOM/EXPORTAR_ANIO ───────────────────────────────
    [HttpGet("/BITACORA_FIRECOM/EXPORTAR_ANIO")]
    public async Task<IActionResult> ExportarPorAnio([FromQuery] int anio)
    {
        var bytes = await _svc.ExportarPorAnioAsync(anio);
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                    $"bitacora_firecom_{anio}.xlsx");
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