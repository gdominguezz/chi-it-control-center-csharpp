using Microsoft.AspNetCore.Mvc;
using ChiIT.Services;

namespace ChiIT.Controllers;

[ApiController]
public class CamarasAudioController : ControllerBase
{
    private readonly CamarasAudioService _svc;

    public CamarasAudioController(CamarasAudioService svc) => _svc = svc;

    // ── GET /CAMARAS_AUDIO  (paginado + filtros) ──────────────────────────
    [HttpGet("/CAMARAS_AUDIO")]
    public async Task<IActionResult> Listar(
        [FromQuery] int page = 1,
        [FromQuery] int limit = 10,
        [FromQuery] string? OC                      = null,
        [FromQuery] string? FOLIO_INVENTARIO        = null,
        [FromQuery] string? FECHA_REGISTRO          = null,
        [FromQuery] string? RECIBIDO_POR            = null,
        [FromQuery] string? SUBCATEGORIA            = null,
        [FromQuery] string? TIPO                    = null,
        [FromQuery] string? MARCA                   = null,
        [FromQuery] string? MODELO                  = null,
        [FromQuery] string? NUMERO_DE_SERIE         = null,
        [FromQuery] string? PROVEEDOR               = null,
        [FromQuery] string? MONEDA                  = null,
        [FromQuery] string? DESTINO                 = null,
        [FromQuery] string? ACCESORIOS              = null,
        [FromQuery] string? PLANTA                  = null,
        [FromQuery] string? DESTINO2                = null,
        [FromQuery] string? PERSONAL_IT_QUE_ASIGNA  = null,
        [FromQuery] string? FOLIO_DE_SERVICIO       = null)
    {
        var filtros = new CamaraAudioFiltros
        {
            OC                     = OC,
            FOLIO_INVENTARIO       = FOLIO_INVENTARIO,
            FECHA_REGISTRO         = FECHA_REGISTRO,
            RECIBIDO_POR           = RECIBIDO_POR,
            SUBCATEGORIA           = SUBCATEGORIA,
            TIPO                   = TIPO,
            MARCA                  = MARCA,
            MODELO                 = MODELO,
            NUMERO_DE_SERIE        = NUMERO_DE_SERIE,
            PROVEEDOR              = PROVEEDOR,
            MONEDA                 = MONEDA,
            DESTINO                = DESTINO,
            ACCESORIOS             = ACCESORIOS,
            PLANTA                 = PLANTA,
            DESTINO2               = DESTINO2,
            PERSONAL_IT_QUE_ASIGNA = PERSONAL_IT_QUE_ASIGNA,
            FOLIO_DE_SERVICIO      = FOLIO_DE_SERVICIO
        };

        var resultado = await _svc.ListarAsync(page, limit, filtros);
        return Ok(resultado);
    }

    // ── POST /CAMARA_AUDIO  (crear) ───────────────────────────────────────
    [HttpPost("/CAMARA_AUDIO")]
    public async Task<IActionResult> Crear([FromBody] CamaraAudioDto dto)
    {
        var usuario = ObtenerUsuario();
        var id = await _svc.CrearAsync(dto, usuario);
        return Ok(new { id });
    }

    // ── PUT /CAMARA_AUDIO/{id}  (editar) ──────────────────────────────────
    [HttpPut("/CAMARA_AUDIO/{id:int}")]
    public async Task<IActionResult> Editar(int id, [FromBody] CamaraAudioDto dto)
    {
        var usuario = ObtenerUsuario();
        var ok = await _svc.EditarAsync(id, dto, usuario);
        return ok ? Ok(new { ok = true }) : NotFound(new { error = "Registro no encontrado" });
    }

    // ── DELETE /CAMARA_AUDIO/{id}  (eliminar) ─────────────────────────────
    [HttpDelete("/CAMARA_AUDIO/{id:int}")]
    public async Task<IActionResult> Eliminar(int id)
    {
        var ok = await _svc.EliminarAsync(id);
        return ok ? Ok(new { ok = true }) : NotFound(new { error = "Registro no encontrado" });
    }

    // ── GET /CAMARAS_AUDIO/{id}/HISTORIAL ─────────────────────────────────
    [HttpGet("/CAMARAS_AUDIO/{id:int}/HISTORIAL")]
    public async Task<IActionResult> Historial(int id)
    {
        var historial = await _svc.HistorialAsync(id);
        return Ok(new { historial });
    }

    // ── GET /CAMARAS_AUDIO/EXPORTAR  (Excel filtrado) ─────────────────────
    [HttpGet("/CAMARAS_AUDIO/EXPORTAR")]
    public async Task<IActionResult> ExportarFiltrado(
        [FromQuery] string? OC                      = null,
        [FromQuery] string? FOLIO_INVENTARIO        = null,
        [FromQuery] string? FECHA_REGISTRO          = null,
        [FromQuery] string? RECIBIDO_POR            = null,
        [FromQuery] string? SUBCATEGORIA            = null,
        [FromQuery] string? TIPO                    = null,
        [FromQuery] string? MARCA                   = null,
        [FromQuery] string? MODELO                  = null,
        [FromQuery] string? NUMERO_DE_SERIE         = null,
        [FromQuery] string? PROVEEDOR               = null,
        [FromQuery] string? MONEDA                  = null,
        [FromQuery] string? DESTINO                 = null,
        [FromQuery] string? ACCESORIOS              = null,
        [FromQuery] string? PLANTA                  = null,
        [FromQuery] string? DESTINO2                = null,
        [FromQuery] string? PERSONAL_IT_QUE_ASIGNA  = null,
        [FromQuery] string? FOLIO_DE_SERVICIO       = null)
    {
        var filtros = new CamaraAudioFiltros
        {
            OC                     = OC,
            FOLIO_INVENTARIO       = FOLIO_INVENTARIO,
            FECHA_REGISTRO         = FECHA_REGISTRO,
            RECIBIDO_POR           = RECIBIDO_POR,
            SUBCATEGORIA           = SUBCATEGORIA,
            TIPO                   = TIPO,
            MARCA                  = MARCA,
            MODELO                 = MODELO,
            NUMERO_DE_SERIE        = NUMERO_DE_SERIE,
            PROVEEDOR              = PROVEEDOR,
            MONEDA                 = MONEDA,
            DESTINO                = DESTINO,
            ACCESORIOS             = ACCESORIOS,
            PLANTA                 = PLANTA,
            DESTINO2               = DESTINO2,
            PERSONAL_IT_QUE_ASIGNA = PERSONAL_IT_QUE_ASIGNA,
            FOLIO_DE_SERVICIO      = FOLIO_DE_SERVICIO
        };

        var bytes = await _svc.ExportarAsync(filtros);
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                    "camaras_audio_filtrado.xlsx");
    }

    // ── GET /CAMARAS_AUDIO/EXPORTAR_TODO ──────────────────────────────────
    [HttpGet("/CAMARAS_AUDIO/EXPORTAR_TODO")]
    public async Task<IActionResult> ExportarTodo()
    {
        var bytes = await _svc.ExportarAsync(new CamaraAudioFiltros());
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                    "camaras_audio_total.xlsx");
    }

    // ── GET /CAMARAS_AUDIO/EXPORTAR_ANIO ──────────────────────────────────
    [HttpGet("/CAMARAS_AUDIO/EXPORTAR_ANIO")]
    public async Task<IActionResult> ExportarPorAnio([FromQuery] int anio)
    {
        var bytes = await _svc.ExportarPorAnioAsync(anio);
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                    $"camaras_audio_{anio}.xlsx");
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
