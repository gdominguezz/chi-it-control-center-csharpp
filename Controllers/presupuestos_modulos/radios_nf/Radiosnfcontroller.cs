using Microsoft.AspNetCore.Mvc;
using ChiIT.Services;

namespace ChiIT.Controllers;

[ApiController]
public class RadiosNFController : ControllerBase
{
    private readonly RadiosNFService _svc;

    public RadiosNFController(RadiosNFService svc) => _svc = svc;

    // ── GET /RADIOS_NF  (paginado + filtros) ──────────────────────────────
    [HttpGet("/RADIOS_NF")]
    public async Task<IActionResult> Listar(
        [FromQuery] int page = 1,
        [FromQuery] int limit = 10,
        [FromQuery] string? ID_UNICO = null,
        [FromQuery] string? OC = null,
        [FromQuery] string? FOLIO = null,
        [FromQuery] string? FECHA_REGISTRO = null,
        [FromQuery] string? RECIBIDO_POR = null,
        [FromQuery] string? SUBCATEGORIA = null,
        [FromQuery] string? MARCA = null,
        [FromQuery] string? MODELO = null,
        [FromQuery] string? NO_SERIE = null,
        [FromQuery] string? OBSERVACIONES = null,
        [FromQuery] string? PROVEEDOR = null,
        [FromQuery] string? MONEDA = null,
        [FromQuery] string? VIDA_UTIL = null,
        [FromQuery] string? REQUISITOR = null,
        [FromQuery] string? DISPONIBLE = null,
        [FromQuery] string? ASIGNADO_A = null,
        [FromQuery] string? DESTINO_PLANTA = null,
        [FromQuery] string? PERSONAL_IT_ASIGNA = null)
    {
        var filtros = new RadioNFFiltros
        {
            ID_UNICO = ID_UNICO,
            OC = OC,
            FOLIO = FOLIO,
            FECHA_REGISTRO = FECHA_REGISTRO,
            RECIBIDO_POR = RECIBIDO_POR,
            SUBCATEGORIA = SUBCATEGORIA,
            MARCA = MARCA,
            MODELO = MODELO,
            NO_SERIE = NO_SERIE,
            OBSERVACIONES = OBSERVACIONES,
            PROVEEDOR = PROVEEDOR,
            MONEDA = MONEDA,
            VIDA_UTIL = VIDA_UTIL,
            REQUISITOR = REQUISITOR,
            DISPONIBLE = DISPONIBLE,
            ASIGNADO_A = ASIGNADO_A,
            DESTINO_PLANTA = DESTINO_PLANTA,
            PERSONAL_IT_ASIGNA = PERSONAL_IT_ASIGNA
        };

        var resultado = await _svc.ListarAsync(page, limit, filtros);
        return Ok(resultado);
    }

    // ── POST /RADIO_NF  (crear) ───────────────────────────────────────────
    [HttpPost("/RADIO_NF")]
    public async Task<IActionResult> Crear([FromBody] RadioNFDto dto)
    {
        var usuario = ObtenerUsuario();
        var id = await _svc.CrearAsync(dto, usuario);
        return Ok(new { id });
    }

    // ── PUT /RADIO_NF/{id}  (editar) ──────────────────────────────────────
    [HttpPut("/RADIO_NF/{id:int}")]
    public async Task<IActionResult> Editar(int id, [FromBody] RadioNFDto dto)
    {
        var usuario = ObtenerUsuario();
        var ok = await _svc.EditarAsync(id, dto, usuario);
        return ok ? Ok(new { ok = true }) : NotFound(new { error = "Registro no encontrado" });
    }

    // ── DELETE /RADIO_NF/{id}  (eliminar) ────────────────────────────────
    [HttpDelete("/RADIO_NF/{id:int}")]
    public async Task<IActionResult> Eliminar(int id)
    {
        var ok = await _svc.EliminarAsync(id);
        return ok ? Ok(new { ok = true }) : NotFound(new { error = "Registro no encontrado" });
    }

    // ── GET /RADIOS_NF/{id}/HISTORIAL ─────────────────────────────────────
    [HttpGet("/RADIOS_NF/{id:int}/HISTORIAL")]
    public async Task<IActionResult> Historial(int id)
    {
        var historial = await _svc.HistorialAsync(id);
        return Ok(new { historial });
    }

    // ── GET /RADIOS_NF/EXPORTAR  (Excel filtrado) ─────────────────────────
    [HttpGet("/RADIOS_NF/EXPORTAR")]
    public async Task<IActionResult> ExportarFiltrado(
        [FromQuery] string? ID_UNICO = null,
        [FromQuery] string? OC = null,
        [FromQuery] string? FOLIO = null,
        [FromQuery] string? FECHA_REGISTRO = null,
        [FromQuery] string? RECIBIDO_POR = null,
        [FromQuery] string? SUBCATEGORIA = null,
        [FromQuery] string? MARCA = null,
        [FromQuery] string? MODELO = null,
        [FromQuery] string? NO_SERIE = null,
        [FromQuery] string? OBSERVACIONES = null,
        [FromQuery] string? PROVEEDOR = null,
        [FromQuery] string? MONEDA = null,
        [FromQuery] string? VIDA_UTIL = null,
        [FromQuery] string? REQUISITOR = null,
        [FromQuery] string? DISPONIBLE = null,
        [FromQuery] string? ASIGNADO_A = null,
        [FromQuery] string? DESTINO_PLANTA = null,
        [FromQuery] string? PERSONAL_IT_ASIGNA = null)
    {
        var filtros = new RadioNFFiltros
        {
            ID_UNICO = ID_UNICO,
            OC = OC,
            FOLIO = FOLIO,
            FECHA_REGISTRO = FECHA_REGISTRO,
            RECIBIDO_POR = RECIBIDO_POR,
            SUBCATEGORIA = SUBCATEGORIA,
            MARCA = MARCA,
            MODELO = MODELO,
            NO_SERIE = NO_SERIE,
            OBSERVACIONES = OBSERVACIONES,
            PROVEEDOR = PROVEEDOR,
            MONEDA = MONEDA,
            VIDA_UTIL = VIDA_UTIL,
            REQUISITOR = REQUISITOR,
            DISPONIBLE = DISPONIBLE,
            ASIGNADO_A = ASIGNADO_A,
            DESTINO_PLANTA = DESTINO_PLANTA,
            PERSONAL_IT_ASIGNA = PERSONAL_IT_ASIGNA
        };

        var bytes = await _svc.ExportarAsync(filtros);
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                    "radios_nf_filtrado.xlsx");
    }

    // ── GET /RADIOS_NF/EXPORTAR_TODO ──────────────────────────────────────
    [HttpGet("/RADIOS_NF/EXPORTAR_TODO")]
    public async Task<IActionResult> ExportarTodo()
    {
        var bytes = await _svc.ExportarAsync(new RadioNFFiltros());
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                    "radios_nf_total.xlsx");
    }

    // ── GET /RADIOS_NF/EXPORTAR_ANIO ──────────────────────────────────────
    [HttpGet("/RADIOS_NF/EXPORTAR_ANIO")]
    public async Task<IActionResult> ExportarPorAnio([FromQuery] int anio)
    {
        var bytes = await _svc.ExportarPorAnioAsync(anio);
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                    $"radios_nf_{anio}.xlsx");
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