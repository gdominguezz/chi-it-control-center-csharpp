using Microsoft.AspNetCore.Mvc;
using ClosedXML.Excel;

[ApiController]
[Route("ORDENES_DE_COMPRA")]
public class OrdenesDeCompraController : ControllerBase
{
    private readonly OrdenesDeCompraService _service;

    private static readonly string _pdfFolder =
        Path.Combine(Directory.GetCurrentDirectory(), "pdfs", "ordenes_de_compra");

    public OrdenesDeCompraController(OrdenesDeCompraService service)
    {
        _service = service;
        Directory.CreateDirectory(_pdfFolder);
    }

    // ── GET paginado + filtros ─────────────────────────────────────────────
    [HttpGet]
    public IActionResult GetAll(
        [FromQuery] int page = 1,
        [FromQuery] int limit = 10,
        [FromQuery] string? ORDEN_DE_COMPRA = null,
        [FromQuery] string? FOLIO = null,
        [FromQuery] string? SOLICITANTE = null,
        [FromQuery] string? PRESUPUESTO_MES = null,
        [FromQuery] string? SERIE_UBICACION_NO_EMPLEADO = null,
        [FromQuery] string? ACCESORIO_SOLICITADO = null,
        [FromQuery] string? PROVEEDOR_ELEGIDO = null,
        [FromQuery] string? PIEZA_SERVICIO = null,
        [FromQuery] string? MONEDA = null,
        [FromQuery] string? REQUISICION = null,
        [FromQuery] string? OC = null,
        [FromQuery] string? ESTATUS_OC = null)
    {
        var (data, total) = _service.GetAll(
            page, limit,
            ORDEN_DE_COMPRA, FOLIO, SOLICITANTE, PRESUPUESTO_MES,
            SERIE_UBICACION_NO_EMPLEADO, ACCESORIO_SOLICITADO,
            PROVEEDOR_ELEGIDO, PIEZA_SERVICIO, MONEDA,
            REQUISICION, OC, ESTATUS_OC);

        return Ok(new { data, total });
    }

    // ── GET solicitantes únicos ────────────────────────────────────────────
    [HttpGet("SOLICITANTES")]
    public IActionResult GetSolicitantes()
    {
        var solicitantes = _service.GetSolicitantesUnicos();
        return Ok(new { solicitantes });
    }

    // ── GET por ID ────────────────────────────────────────────────────────
    [HttpGet("{id}")]
    public IActionResult GetById(int id)
    {
        var row = _service.GetById(id);
        if (row == null) return NotFound();
        return Ok(row);
    }

    // ── POST ──────────────────────────────────────────────────────────────
    [HttpPost]
    public IActionResult Create([FromBody] OrdenDeCompraDto dto,
        [FromHeader(Name = "X-Usuario")] string? usuario = null)
    {
        _service.Create(dto, usuario);
        return Ok();
    }

    // ── PUT ───────────────────────────────────────────────────────────────
    [HttpPut("{id}")]
    public IActionResult Update(int id, [FromBody] OrdenDeCompraDto dto,
        [FromHeader(Name = "X-Usuario")] string? usuario = null)
    {
        _service.Update(id, dto, usuario);
        return Ok();
    }

    // ── DELETE ────────────────────────────────────────────────────────────
    [HttpDelete("{id}")]
    public IActionResult Delete(int id)
    {
        var ruta = _service.ObtenerRutaPDF(id);
        if (ruta != null && System.IO.File.Exists(ruta))
            System.IO.File.Delete(ruta);

        _service.Delete(id);
        return Ok();
    }

    // ── PDF: subir ────────────────────────────────────────────────────────
    [HttpPost("PDF/{id}")]
    public async Task<IActionResult> SubirPDF(int id, IFormFile file)
    {
        if (file == null || file.Length == 0)
            return BadRequest("Sin archivo.");

        var rutaAnterior = _service.ObtenerRutaPDF(id);
        if (rutaAnterior != null && System.IO.File.Exists(rutaAnterior))
            System.IO.File.Delete(rutaAnterior);

        var nombreArchivo = $"{id}_{Guid.NewGuid():N}.pdf";
        var rutaNueva = Path.Combine(_pdfFolder, nombreArchivo);

        using (var fs = System.IO.File.Create(rutaNueva))
            await file.CopyToAsync(fs);

        _service.GuardarRutaPDF(id, rutaNueva);
        return Ok();
    }

    // ── PDF: ver ──────────────────────────────────────────────────────────
    [HttpGet("PDF/{id}")]
    public IActionResult VerPDF(int id)
    {
        var ruta = _service.ObtenerRutaPDF(id);
        if (ruta == null || !System.IO.File.Exists(ruta))
            return NotFound();

        var bytes = System.IO.File.ReadAllBytes(ruta);
        return File(bytes, "application/pdf");
    }

    // ── PDF: eliminar ─────────────────────────────────────────────────────
    [HttpDelete("PDF/{id}")]
    public IActionResult EliminarPDF(int id)
    {
        var ruta = _service.ObtenerRutaPDF(id);
        if (ruta != null && System.IO.File.Exists(ruta))
            System.IO.File.Delete(ruta);

        _service.EliminarPDF(id);
        return Ok();
    }

    // ── EXPORTAR filtrado ─────────────────────────────────────────────────
    [HttpGet("EXPORTAR")]
    public IActionResult ExportarFiltrado(
        [FromQuery] string? ORDEN_DE_COMPRA = null,
        [FromQuery] string? FOLIO = null,
        [FromQuery] string? SOLICITANTE = null,
        [FromQuery] string? PRESUPUESTO_MES = null,
        [FromQuery] string? SERIE_UBICACION_NO_EMPLEADO = null,
        [FromQuery] string? ACCESORIO_SOLICITADO = null,
        [FromQuery] string? PROVEEDOR_ELEGIDO = null,
        [FromQuery] string? PIEZA_SERVICIO = null,
        [FromQuery] string? MONEDA = null,
        [FromQuery] string? REQUISICION = null,
        [FromQuery] string? OC = null,
        [FromQuery] string? ESTATUS_OC = null)
    {
        var (data, _) = _service.GetAll(
            1, int.MaxValue,
            ORDEN_DE_COMPRA, FOLIO, SOLICITANTE, PRESUPUESTO_MES,
            SERIE_UBICACION_NO_EMPLEADO, ACCESORIO_SOLICITADO,
            PROVEEDOR_ELEGIDO, PIEZA_SERVICIO, MONEDA,
            REQUISICION, OC, ESTATUS_OC);

        var bytes = _service.GenerarExcel(data);
        return File(bytes,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            "OrdenesDeCompra_Filtrado.xlsx");
    }

    // ── EXPORTAR todo ─────────────────────────────────────────────────────
    [HttpGet("EXPORTAR_TODO")]
    public IActionResult ExportarTodo()
    {
        var (data, _) = _service.GetAll(1, int.MaxValue);
        var bytes = _service.GenerarExcel(data);
        return File(bytes,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            "OrdenesDeCompra_Todo.xlsx");
    }

    // ── EXPORTAR por año ──────────────────────────────────────────────────
    [HttpGet("EXPORTAR_ANIO")]
    public IActionResult ExportarPorAnio([FromQuery] int anio)
    {
        var data = _service.GetPorAnio(anio);
        var bytes = _service.GenerarExcel(data);
        return File(bytes,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            $"OrdenesDeCompra_{anio}.xlsx");
    }

    // ── HISTORIAL ─────────────────────────────────────────────────────────
    [HttpGet("{id}/HISTORIAL")]
    public IActionResult GetHistorial(int id)
    {
        var historial = _service.GetHistorial(id);
        return Ok(new { historial });
    }
}