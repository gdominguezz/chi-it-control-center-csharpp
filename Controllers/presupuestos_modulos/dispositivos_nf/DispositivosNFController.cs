using Microsoft.AspNetCore.Mvc;
using ChiIT.Services;

namespace ChiIT.Controllers;

[ApiController]
public class DispositivosNFController : ControllerBase
{
    private readonly DispositivosNFService _svc;

    public DispositivosNFController(DispositivosNFService svc) => _svc = svc;

    // ── GET /DISPOSITIVOS_NF  (paginado + filtros) ────────────────────────
    [HttpGet("/DISPOSITIVOS_NF")]
    public async Task<IActionResult> Listar(
        [FromQuery] int page = 1,
        [FromQuery] int limit = 10,
        [FromQuery] string? ID_UNICO                    = null,
        [FromQuery] string? OC                          = null,
        [FromQuery] string? FOLIO                       = null,
        [FromQuery] string? FECHA_REGISTRO              = null,
        [FromQuery] string? RECIBIDO_POR                = null,
        [FromQuery] string? SUBCATEGORIA                = null,
        [FromQuery] string? MARCA                       = null,
        [FromQuery] string? MODELO                      = null,
        [FromQuery] string? NUMERO_SERIE                = null,
        [FromQuery] string? PROVEEDOR                   = null,
        [FromQuery] string? ACTIVO_FIJO                 = null,
        [FromQuery] string? PROCESADOR                  = null,
        [FromQuery] string? ARQUITECTURA                = null,
        [FromQuery] string? ALMACENAMIENTO              = null,
        [FromQuery] string? TIPO_DISCO_DURO             = null,
        [FromQuery] string? SISTEMA_OPERATIVO           = null,
        [FromQuery] string? LICENCIA_SISTEMA_OPERATIVO  = null,
        [FromQuery] string? MEMORIA_RAM                 = null,
        [FromQuery] string? TIPO_MEMORIA                = null,
        [FromQuery] string? UBICACION                   = null,
        [FromQuery] string? EDIFICIO                    = null,
        [FromQuery] string? DISPONIBLE                  = null,
        [FromQuery] string? ASIGNADO_A                  = null,
        [FromQuery] string? DESTINO_PLANTA              = null,
        [FromQuery] string? PERSONAL_IT_QUE_ASIGNA      = null,
        [FromQuery] string? FORMATO_DE_BAJA             = null)
    {
        var filtros = new DispositivoNFFiltros
        {
            ID_UNICO                   = ID_UNICO,
            OC                         = OC,
            FOLIO                      = FOLIO,
            FECHA_REGISTRO             = FECHA_REGISTRO,
            RECIBIDO_POR               = RECIBIDO_POR,
            SUBCATEGORIA               = SUBCATEGORIA,
            MARCA                      = MARCA,
            MODELO                     = MODELO,
            NUMERO_SERIE               = NUMERO_SERIE,
            PROVEEDOR                  = PROVEEDOR,
            ACTIVO_FIJO                = ACTIVO_FIJO,
            PROCESADOR                 = PROCESADOR,
            ARQUITECTURA               = ARQUITECTURA,
            ALMACENAMIENTO             = ALMACENAMIENTO,
            TIPO_DISCO_DURO            = TIPO_DISCO_DURO,
            SISTEMA_OPERATIVO          = SISTEMA_OPERATIVO,
            LICENCIA_SISTEMA_OPERATIVO = LICENCIA_SISTEMA_OPERATIVO,
            MEMORIA_RAM                = MEMORIA_RAM,
            TIPO_MEMORIA               = TIPO_MEMORIA,
            UBICACION                  = UBICACION,
            EDIFICIO                   = EDIFICIO,
            DISPONIBLE                 = DISPONIBLE,
            ASIGNADO_A                 = ASIGNADO_A,
            DESTINO_PLANTA             = DESTINO_PLANTA,
            PERSONAL_IT_QUE_ASIGNA     = PERSONAL_IT_QUE_ASIGNA,
            FORMATO_DE_BAJA            = FORMATO_DE_BAJA
        };

        var resultado = await _svc.ListarAsync(page, limit, filtros);
        return Ok(resultado);
    }

    // ── POST /DISPOSITIVO_NF  (crear) ─────────────────────────────────────
    [HttpPost("/DISPOSITIVO_NF")]
    public async Task<IActionResult> Crear([FromBody] DispositivoNFDto dto)
    {
        var usuario = ObtenerUsuario();
        var id = await _svc.CrearAsync(dto, usuario);
        return Ok(new { id });
    }

    // ── PUT /DISPOSITIVO_NF/{id}  (editar) ────────────────────────────────
    [HttpPut("/DISPOSITIVO_NF/{id:int}")]
    public async Task<IActionResult> Editar(int id, [FromBody] DispositivoNFDto dto)
    {
        var usuario = ObtenerUsuario();
        var ok = await _svc.EditarAsync(id, dto, usuario);
        return ok ? Ok(new { ok = true }) : NotFound(new { error = "Registro no encontrado" });
    }

    // ── DELETE /DISPOSITIVO_NF/{id}  (eliminar) ───────────────────────────
    [HttpDelete("/DISPOSITIVO_NF/{id:int}")]
    public async Task<IActionResult> Eliminar(int id)
    {
        var ok = await _svc.EliminarAsync(id);
        return ok ? Ok(new { ok = true }) : NotFound(new { error = "Registro no encontrado" });
    }

    // ── GET /DISPOSITIVOS_NF/{id}/HISTORIAL ───────────────────────────────
    [HttpGet("/DISPOSITIVOS_NF/{id:int}/HISTORIAL")]
    public async Task<IActionResult> Historial(int id)
    {
        var historial = await _svc.HistorialAsync(id);
        return Ok(new { historial });
    }

    // ── GET /DISPOSITIVOS_NF/EXPORTAR  (Excel filtrado) ───────────────────
    [HttpGet("/DISPOSITIVOS_NF/EXPORTAR")]
    public async Task<IActionResult> ExportarFiltrado(
        [FromQuery] string? ID_UNICO                    = null,
        [FromQuery] string? OC                          = null,
        [FromQuery] string? FOLIO                       = null,
        [FromQuery] string? FECHA_REGISTRO              = null,
        [FromQuery] string? RECIBIDO_POR                = null,
        [FromQuery] string? SUBCATEGORIA                = null,
        [FromQuery] string? MARCA                       = null,
        [FromQuery] string? MODELO                      = null,
        [FromQuery] string? NUMERO_SERIE                = null,
        [FromQuery] string? PROVEEDOR                   = null,
        [FromQuery] string? ACTIVO_FIJO                 = null,
        [FromQuery] string? PROCESADOR                  = null,
        [FromQuery] string? ARQUITECTURA                = null,
        [FromQuery] string? ALMACENAMIENTO              = null,
        [FromQuery] string? TIPO_DISCO_DURO             = null,
        [FromQuery] string? SISTEMA_OPERATIVO           = null,
        [FromQuery] string? LICENCIA_SISTEMA_OPERATIVO  = null,
        [FromQuery] string? MEMORIA_RAM                 = null,
        [FromQuery] string? TIPO_MEMORIA                = null,
        [FromQuery] string? UBICACION                   = null,
        [FromQuery] string? EDIFICIO                    = null,
        [FromQuery] string? DISPONIBLE                  = null,
        [FromQuery] string? ASIGNADO_A                  = null,
        [FromQuery] string? DESTINO_PLANTA              = null,
        [FromQuery] string? PERSONAL_IT_QUE_ASIGNA      = null,
        [FromQuery] string? FORMATO_DE_BAJA             = null)
    {
        var filtros = new DispositivoNFFiltros
        {
            ID_UNICO                   = ID_UNICO,
            OC                         = OC,
            FOLIO                      = FOLIO,
            FECHA_REGISTRO             = FECHA_REGISTRO,
            RECIBIDO_POR               = RECIBIDO_POR,
            SUBCATEGORIA               = SUBCATEGORIA,
            MARCA                      = MARCA,
            MODELO                     = MODELO,
            NUMERO_SERIE               = NUMERO_SERIE,
            PROVEEDOR                  = PROVEEDOR,
            ACTIVO_FIJO                = ACTIVO_FIJO,
            PROCESADOR                 = PROCESADOR,
            ARQUITECTURA               = ARQUITECTURA,
            ALMACENAMIENTO             = ALMACENAMIENTO,
            TIPO_DISCO_DURO            = TIPO_DISCO_DURO,
            SISTEMA_OPERATIVO          = SISTEMA_OPERATIVO,
            LICENCIA_SISTEMA_OPERATIVO = LICENCIA_SISTEMA_OPERATIVO,
            MEMORIA_RAM                = MEMORIA_RAM,
            TIPO_MEMORIA               = TIPO_MEMORIA,
            UBICACION                  = UBICACION,
            EDIFICIO                   = EDIFICIO,
            DISPONIBLE                 = DISPONIBLE,
            ASIGNADO_A                 = ASIGNADO_A,
            DESTINO_PLANTA             = DESTINO_PLANTA,
            PERSONAL_IT_QUE_ASIGNA     = PERSONAL_IT_QUE_ASIGNA,
            FORMATO_DE_BAJA            = FORMATO_DE_BAJA
        };

        var bytes = await _svc.ExportarAsync(filtros);
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                    "dispositivos_nf_filtrado.xlsx");
    }

    // ── GET /DISPOSITIVOS_NF/EXPORTAR_TODO ────────────────────────────────
    [HttpGet("/DISPOSITIVOS_NF/EXPORTAR_TODO")]
    public async Task<IActionResult> ExportarTodo()
    {
        var bytes = await _svc.ExportarAsync(new DispositivoNFFiltros());
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                    "dispositivos_nf_total.xlsx");
    }

    // ── GET /DISPOSITIVOS_NF/EXPORTAR_ANIO ───────────────────────────────
    [HttpGet("/DISPOSITIVOS_NF/EXPORTAR_ANIO")]
    public async Task<IActionResult> ExportarPorAnio([FromQuery] int anio)
    {
        var bytes = await _svc.ExportarPorAnioAsync(anio);
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                    $"dispositivos_nf_{anio}.xlsx");
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
