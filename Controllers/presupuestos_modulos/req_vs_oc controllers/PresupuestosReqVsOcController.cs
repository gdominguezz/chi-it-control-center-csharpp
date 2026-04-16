using Microsoft.AspNetCore.Mvc;
using ClosedXML.Excel;

[ApiController]
[Route("PRESUPUESTOS_REQ_VS_OC")]
public class PresupuestosReqVsOcController : ControllerBase
{
    private readonly PresupuestosReqVsOcService _service;

    // Carpeta donde se guardarán los PDFs en disco
    private static readonly string _pdfFolder =
        Path.Combine(Directory.GetCurrentDirectory(), "pdfs", "req_vs_oc");

    public PresupuestosReqVsOcController(PresupuestosReqVsOcService service)
    {
        _service = service;
        // Crear la carpeta si no existe
        Directory.CreateDirectory(_pdfFolder);
    }

    // ── GET paginado + filtros ──────────────────────────────────────────────
    [HttpGet]
    public IActionResult GetAll(
        [FromQuery] int page = 1,
        [FromQuery] int limit = 10,
        [FromQuery] string? NO_REQUISICION = null,
        [FromQuery] string? ORDEN_COMPRA = null,
        [FromQuery] string? FECHA_COMPRA = null,
        [FromQuery] string? PO_SUBTOTAL = null,
        [FromQuery] string? MONEDA = null,
        [FromQuery] string? OC_SUBTOTAL = null,
        [FromQuery] string? REGISTRADA_EN_OC = null)
    {
        var (data, total) = _service.GetAll(
            page, limit,
            NO_REQUISICION, ORDEN_COMPRA, FECHA_COMPRA,
            PO_SUBTOTAL, MONEDA, OC_SUBTOTAL, REGISTRADA_EN_OC);

        return Ok(new { data, total });
    }

    // ── POST ───────────────────────────────────────────────────────────────
    [HttpPost]
    public IActionResult Create([FromBody] ReqVsOcDto dto)
    {
        _service.Create(dto);
        return Ok();
    }

    // ── PUT ────────────────────────────────────────────────────────────────
    [HttpPut("{id}")]
    public IActionResult Update(int id, [FromBody] ReqVsOcDto dto)
    {
        _service.Update(id, dto);
        return Ok();
    }

    // ── DELETE ─────────────────────────────────────────────────────────────
    [HttpDelete("{id}")]
    public IActionResult Delete(int id)
    {
        // Si tiene PDF en disco, eliminarlo también
        var ruta = _service.ObtenerRutaPDF(id);
        if (ruta != null && System.IO.File.Exists(ruta))
            System.IO.File.Delete(ruta);

        _service.Delete(id);
        return Ok();
    }

    // ── PDF: subir ─────────────────────────────────────────────────────────
    [HttpPost("PDF/{id}")]
    public async Task<IActionResult> SubirPDF(int id, IFormFile file)
    {
        if (file == null || file.Length == 0)
            return BadRequest("Sin archivo.");

        // Si ya tenía un PDF anterior, eliminarlo del disco
        var rutaAnterior = _service.ObtenerRutaPDF(id);
        if (rutaAnterior != null && System.IO.File.Exists(rutaAnterior))
            System.IO.File.Delete(rutaAnterior);

        // Guardar el nuevo archivo con nombre único
        var nombreArchivo = $"{id}_{Guid.NewGuid():N}.pdf";
        var rutaNueva = Path.Combine(_pdfFolder, nombreArchivo);

        using (var fs = System.IO.File.Create(rutaNueva))
            await file.CopyToAsync(fs);

        // Guardar solo la ruta en la BD
        _service.GuardarRutaPDF(id, rutaNueva);

        return Ok();
    }

    // ── PDF: ver ───────────────────────────────────────────────────────────
    [HttpGet("PDF/{id}")]
    public IActionResult VerPDF(int id)
    {
        var ruta = _service.ObtenerRutaPDF(id);
        if (ruta == null || !System.IO.File.Exists(ruta))
            return NotFound();

        var bytes = System.IO.File.ReadAllBytes(ruta);
        return File(bytes, "application/pdf");
    }

    // ── PDF: eliminar ──────────────────────────────────────────────────────
    [HttpDelete("PDF/{id}")]
    public IActionResult EliminarPDF(int id)
    {
        var ruta = _service.ObtenerRutaPDF(id);
        if (ruta != null && System.IO.File.Exists(ruta))
            System.IO.File.Delete(ruta);

        _service.EliminarPDF(id);
        return Ok();
    }

    // ── EXPORTAR filtrado ──────────────────────────────────────────────────
    [HttpGet("EXPORTAR")]
    public IActionResult ExportarFiltrado(
        [FromQuery] string? NO_REQUISICION = null,
        [FromQuery] string? ORDEN_COMPRA = null,
        [FromQuery] string? FECHA_COMPRA = null,
        [FromQuery] string? PO_SUBTOTAL = null,
        [FromQuery] string? MONEDA = null,
        [FromQuery] string? OC_SUBTOTAL = null,
        [FromQuery] string? REGISTRADA_EN_OC = null)
    {
        var (data, _) = _service.GetAll(
            1, int.MaxValue,
            NO_REQUISICION, ORDEN_COMPRA, FECHA_COMPRA,
            PO_SUBTOTAL, MONEDA, OC_SUBTOTAL, REGISTRADA_EN_OC);

        var bytes = _service.GenerarExcel(data);
        return File(bytes,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            "REQ_VS_OC_Filtrado.xlsx");
    }

    // ── EXPORTAR todo ──────────────────────────────────────────────────────
    [HttpGet("EXPORTAR_TODO")]
    public IActionResult ExportarTodo()
    {
        var (data, _) = _service.GetAll(1, int.MaxValue);
        var bytes = _service.GenerarExcel(data);
        return File(bytes,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            "REQ_VS_OC_Todo.xlsx");
    }

    // ── EXPORTAR por año ───────────────────────────────────────────────────
    [HttpGet("EXPORTAR_ANIO")]
    public IActionResult ExportarPorAnio([FromQuery] int anio)
    {
        var data = _service.GetPorAnio(anio);
        var bytes = _service.GenerarExcel(data);
        return File(bytes,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            $"REQ_VS_OC_{anio}.xlsx");
    }
}