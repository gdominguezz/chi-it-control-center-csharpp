using Microsoft.AspNetCore.Mvc;
using ChiIT.Services;
using ChiIT.Models;

namespace ChiIT.Controllers;

[ApiController]
public class BajasController : ControllerBase
{
    private readonly BajasService _svc;

    public BajasController(BajasService svc) => _svc = svc;

    // ── GET /BAJAS  (paginado + filtros) ──────────────────────────────────
    [HttpGet("/BAJAS")]
    public async Task<IActionResult> Listar(
        [FromQuery] int page = 1,
        [FromQuery] int limit = 10,
        [FromQuery] string? FOLIO = null,
        [FromQuery] string? ESTADO = null,
        [FromQuery] string? PLANTA = null,
        [FromQuery] string? FECHA = null,
        [FromQuery] string? EQUIPO = null,
        [FromQuery] string? MARCA = null,
        [FromQuery] string? MODELO = null,
        [FromQuery] string? NO_SERIE = null,
        [FromQuery] string? ACTIVO_FIJO = null,
        [FromQuery] string? UBICACION_PERSONA = null,
        [FromQuery] string? MOTIVO_DE_BAJA = null,
        [FromQuery] string? DIAGNOSTICO = null,
        [FromQuery] string? COMENTARIOS = null,
        [FromQuery] string? MOTIVO_DE_CANCELACION = null)
    {
        var filtros = new BajaFiltros
        {
            FOLIO = FOLIO,
            ESTADO = ESTADO,
            PLANTA = PLANTA,
            FECHA = FECHA,
            EQUIPO = EQUIPO,
            MARCA = MARCA,
            MODELO = MODELO,
            NO_SERIE = NO_SERIE,
            ACTIVO_FIJO = ACTIVO_FIJO,
            UBICACION_PERSONA = UBICACION_PERSONA,
            MOTIVO_DE_BAJA = MOTIVO_DE_BAJA,
            DIAGNOSTICO = DIAGNOSTICO,
            COMENTARIOS = COMENTARIOS,
            MOTIVO_DE_CANCELACION = MOTIVO_DE_CANCELACION
        };

        var resultado = await _svc.ListarAsync(page, limit, filtros);
        return Ok(resultado);
    }

    // ── POST /BAJA  (crear) ───────────────────────────────────────────────
    [HttpPost("/BAJA")]
    public async Task<IActionResult> Crear([FromBody] BajaDto dto)
    {
        var usuario = ObtenerUsuario();
        var id = await _svc.CrearAsync(dto, usuario);
        return Ok(new { id });
    }

    // ── PUT /BAJA/{id}  (editar) ──────────────────────────────────────────
    [HttpPut("/BAJA/{id:int}")]
    public async Task<IActionResult> Editar(int id, [FromBody] BajaDto dto)
    {
        var usuario = ObtenerUsuario();
        var ok = await _svc.EditarAsync(id, dto, usuario);
        return ok ? Ok(new { ok = true }) : NotFound(new { error = "Registro no encontrado" });
    }

    // ── DELETE /BAJA/{id}  (eliminar) ─────────────────────────────────────
    [HttpDelete("/BAJA/{id:int}")]
    public async Task<IActionResult> Eliminar(int id)
    {
        var ok = await _svc.EliminarAsync(id);
        return ok ? Ok(new { ok = true }) : NotFound(new { error = "Registro no encontrado" });
    }

    // ── GET /BAJAS/{id}/HISTORIAL ─────────────────────────────────────────
    [HttpGet("/BAJAS/{id:int}/HISTORIAL")]
    public async Task<IActionResult> Historial(int id)
    {
        var historial = await _svc.HistorialAsync(id);
        return Ok(new { historial });
    }

    // ── POST /BAJA/PDF/{id}  (subir PDF) ──────────────────────────────────
    [HttpPost("/BAJA/PDF/{id:int}")]
    public async Task<IActionResult> SubirPDF(int id, IFormFile file)
    {
        if (file == null || file.Length == 0)
            return BadRequest(new { error = "Archivo vacío" });

        await _svc.GuardarPdfAsync(id, file);
        return Ok(new { ok = true });
    }

    // ── GET /BAJA/PDF/{id}  (ver PDF) ─────────────────────────────────────
    [HttpGet("/BAJA/PDF/{id:int}")]
    public async Task<IActionResult> VerPDF(int id)
    {
        var stream = await _svc.ObtenerPdfAsync(id);
        if (stream == null) return NotFound(new { error = "PDF no encontrado" });
        return File(stream, "application/pdf");
    }

    // ── DELETE /BAJA/PDF/{id}  (eliminar PDF) ────────────────────────────
    [HttpDelete("/BAJA/PDF/{id:int}")]
    public async Task<IActionResult> EliminarPDF(int id)
    {
        var ok = await _svc.EliminarPdfAsync(id);
        return ok ? Ok(new { ok = true }) : NotFound(new { error = "PDF no encontrado" });
    }

    // ── GET /BAJAS/EXPORTAR  (Excel filtrado) ─────────────────────────────
    [HttpGet("/BAJAS/EXPORTAR")]
    public async Task<IActionResult> ExportarFiltrado(
        [FromQuery] string? FOLIO = null,
        [FromQuery] string? ESTADO = null,
        [FromQuery] string? PLANTA = null,
        [FromQuery] string? FECHA = null,
        [FromQuery] string? EQUIPO = null,
        [FromQuery] string? MARCA = null,
        [FromQuery] string? MODELO = null,
        [FromQuery] string? NO_SERIE = null,
        [FromQuery] string? ACTIVO_FIJO = null,
        [FromQuery] string? UBICACION_PERSONA = null,
        [FromQuery] string? MOTIVO_DE_BAJA = null,
        [FromQuery] string? DIAGNOSTICO = null,
        [FromQuery] string? COMENTARIOS = null,
        [FromQuery] string? MOTIVO_DE_CANCELACION = null)
    {
        var filtros = new BajaFiltros
        {
            FOLIO = FOLIO,
            ESTADO = ESTADO,
            PLANTA = PLANTA,
            FECHA = FECHA,
            EQUIPO = EQUIPO,
            MARCA = MARCA,
            MODELO = MODELO,
            NO_SERIE = NO_SERIE,
            ACTIVO_FIJO = ACTIVO_FIJO,
            UBICACION_PERSONA = UBICACION_PERSONA,
            MOTIVO_DE_BAJA = MOTIVO_DE_BAJA,
            DIAGNOSTICO = DIAGNOSTICO,
            COMENTARIOS = COMENTARIOS,
            MOTIVO_DE_CANCELACION = MOTIVO_DE_CANCELACION
        };

        var bytes = await _svc.ExportarAsync(filtros);
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                    "bajas_filtrado.xlsx");
    }

    // ── GET /BAJAS/EXPORTAR_TODO ──────────────────────────────────────────
    [HttpGet("/BAJAS/EXPORTAR_TODO")]
    public async Task<IActionResult> ExportarTodo()
    {
        var bytes = await _svc.ExportarAsync(new BajaFiltros());
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                    "bajas_total.xlsx");
    }

    // ── GET /BAJAS/EXPORTAR_ANIO ──────────────────────────────────────────
    [HttpGet("/BAJAS/EXPORTAR_ANIO")]
    public async Task<IActionResult> ExportarPorAnio([FromQuery] int anio)
    {
        var bytes = await _svc.ExportarPorAnioAsync(anio);
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                    $"bajas_{anio}.xlsx");
    }

    // ── GET /BAJAS/UBICACIONES  (autocomplete) ────────────────────────────
    [HttpGet("/BAJAS/UBICACIONES")]
    public async Task<IActionResult> Ubicaciones([FromQuery] string? q = null)
    {
        var lista = await _svc.ObtenerUbicacionesAsync(q);
        return Ok(lista);
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