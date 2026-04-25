using Microsoft.AspNetCore.Mvc;
using ChiIT.Services;

namespace ChiIT.Controllers;

[ApiController]
public class ImpresorasNFController : ControllerBase
{
    private readonly ImpresorasNFService _svc;

    public ImpresorasNFController(ImpresorasNFService svc) => _svc = svc;

    // ── GET /IMPRESORAS_NF  (paginado + filtros) ──────────────────────────
    [HttpGet("/IMPRESORAS_NF")]
    public async Task<IActionResult> Listar(
        [FromQuery] int page = 1,
        [FromQuery] int limit = 10,
        [FromQuery] string? ID_UNICO               = null,
        [FromQuery] string? OC                     = null,
        [FromQuery] string? FOLIO_INVENTARIO       = null,
        [FromQuery] string? FECHA_DE_ENTRADA       = null,
        [FromQuery] string? RECIBIDO_POR           = null,
        [FromQuery] string? MARCA                  = null,
        [FromQuery] string? MODELO                 = null,
        [FromQuery] string? NUMERO_DE_SERIE        = null,
        [FromQuery] string? TIPO                   = null,
        [FromQuery] string? IP                     = null,
        [FromQuery] string? MAC                    = null,
        [FromQuery] string? PROVEEDOR              = null,
        [FromQuery] string? MONEDA                 = null,
        [FromQuery] string? UBICACION              = null,
        [FromQuery] string? ESTADO                 = null,
        [FromQuery] string? PLANTA                 = null,
        [FromQuery] string? DISPONIBLE             = null,
        [FromQuery] string? ASIGNADO_A             = null,
        [FromQuery] string? DESTINO_PLANTA         = null,
        [FromQuery] string? PERSONAL_IT_QUE_ASIGNA = null)
    {
        var filtros = new ImpresoraNFFiltros
        {
            ID_UNICO               = ID_UNICO,
            OC                     = OC,
            FOLIO_INVENTARIO       = FOLIO_INVENTARIO,
            FECHA_DE_ENTRADA       = FECHA_DE_ENTRADA,
            RECIBIDO_POR           = RECIBIDO_POR,
            MARCA                  = MARCA,
            MODELO                 = MODELO,
            NUMERO_DE_SERIE        = NUMERO_DE_SERIE,
            TIPO                   = TIPO,
            IP                     = IP,
            MAC                    = MAC,
            PROVEEDOR              = PROVEEDOR,
            MONEDA                 = MONEDA,
            UBICACION              = UBICACION,
            ESTADO                 = ESTADO,
            PLANTA                 = PLANTA,
            DISPONIBLE             = DISPONIBLE,
            ASIGNADO_A             = ASIGNADO_A,
            DESTINO_PLANTA         = DESTINO_PLANTA,
            PERSONAL_IT_QUE_ASIGNA = PERSONAL_IT_QUE_ASIGNA
        };

        var resultado = await _svc.ListarAsync(page, limit, filtros);
        return Ok(resultado);
    }

    // ── POST /IMPRESORA_NF  (crear) ───────────────────────────────────────
    [HttpPost("/IMPRESORA_NF")]
    public async Task<IActionResult> Crear([FromBody] ImpresoraNFDto dto)
    {
        var usuario = ObtenerUsuario();
        var id = await _svc.CrearAsync(dto, usuario);
        return Ok(new { id });
    }

    // ── PUT /IMPRESORA_NF/{id}  (editar) ──────────────────────────────────
    [HttpPut("/IMPRESORA_NF/{id:int}")]
    public async Task<IActionResult> Editar(int id, [FromBody] ImpresoraNFDto dto)
    {
        var usuario = ObtenerUsuario();
        var ok = await _svc.EditarAsync(id, dto, usuario);
        return ok ? Ok(new { ok = true }) : NotFound(new { error = "Registro no encontrado" });
    }

    // ── DELETE /IMPRESORA_NF/{id}  (eliminar) ─────────────────────────────
    [HttpDelete("/IMPRESORA_NF/{id:int}")]
    public async Task<IActionResult> Eliminar(int id)
    {
        var ok = await _svc.EliminarAsync(id);
        return ok ? Ok(new { ok = true }) : NotFound(new { error = "Registro no encontrado" });
    }

    // ── GET /IMPRESORAS_NF/{id}/HISTORIAL ────────────────────────────────
    [HttpGet("/IMPRESORAS_NF/{id:int}/HISTORIAL")]
    public async Task<IActionResult> Historial(int id)
    {
        var historial = await _svc.HistorialAsync(id);
        return Ok(new { historial });
    }

    // ── GET /IMPRESORAS_NF/EXPORTAR  (Excel filtrado) ────────────────────
    [HttpGet("/IMPRESORAS_NF/EXPORTAR")]
    public async Task<IActionResult> ExportarFiltrado(
        [FromQuery] string? ID_UNICO               = null,
        [FromQuery] string? OC                     = null,
        [FromQuery] string? FOLIO_INVENTARIO       = null,
        [FromQuery] string? FECHA_DE_ENTRADA       = null,
        [FromQuery] string? RECIBIDO_POR           = null,
        [FromQuery] string? MARCA                  = null,
        [FromQuery] string? MODELO                 = null,
        [FromQuery] string? NUMERO_DE_SERIE        = null,
        [FromQuery] string? TIPO                   = null,
        [FromQuery] string? IP                     = null,
        [FromQuery] string? MAC                    = null,
        [FromQuery] string? PROVEEDOR              = null,
        [FromQuery] string? MONEDA                 = null,
        [FromQuery] string? UBICACION              = null,
        [FromQuery] string? ESTADO                 = null,
        [FromQuery] string? PLANTA                 = null,
        [FromQuery] string? DISPONIBLE             = null,
        [FromQuery] string? ASIGNADO_A             = null,
        [FromQuery] string? DESTINO_PLANTA         = null,
        [FromQuery] string? PERSONAL_IT_QUE_ASIGNA = null)
    {
        var filtros = new ImpresoraNFFiltros
        {
            ID_UNICO               = ID_UNICO,
            OC                     = OC,
            FOLIO_INVENTARIO       = FOLIO_INVENTARIO,
            FECHA_DE_ENTRADA       = FECHA_DE_ENTRADA,
            RECIBIDO_POR           = RECIBIDO_POR,
            MARCA                  = MARCA,
            MODELO                 = MODELO,
            NUMERO_DE_SERIE        = NUMERO_DE_SERIE,
            TIPO                   = TIPO,
            IP                     = IP,
            MAC                    = MAC,
            PROVEEDOR              = PROVEEDOR,
            MONEDA                 = MONEDA,
            UBICACION              = UBICACION,
            ESTADO                 = ESTADO,
            PLANTA                 = PLANTA,
            DISPONIBLE             = DISPONIBLE,
            ASIGNADO_A             = ASIGNADO_A,
            DESTINO_PLANTA         = DESTINO_PLANTA,
            PERSONAL_IT_QUE_ASIGNA = PERSONAL_IT_QUE_ASIGNA
        };

        var bytes = await _svc.ExportarAsync(filtros);
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                    "impresoras_nf_filtrado.xlsx");
    }

    // ── GET /IMPRESORAS_NF/EXPORTAR_TODO ─────────────────────────────────
    [HttpGet("/IMPRESORAS_NF/EXPORTAR_TODO")]
    public async Task<IActionResult> ExportarTodo()
    {
        var bytes = await _svc.ExportarAsync(new ImpresoraNFFiltros());
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                    "impresoras_nf_total.xlsx");
    }

    // ── GET /IMPRESORAS_NF/EXPORTAR_ANIO ─────────────────────────────────
    [HttpGet("/IMPRESORAS_NF/EXPORTAR_ANIO")]
    public async Task<IActionResult> ExportarPorAnio([FromQuery] int anio)
    {
        var bytes = await _svc.ExportarPorAnioAsync(anio);
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                    $"impresoras_nf_{anio}.xlsx");
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
