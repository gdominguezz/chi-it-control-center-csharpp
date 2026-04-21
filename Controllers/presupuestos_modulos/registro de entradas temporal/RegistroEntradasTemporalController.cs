using Microsoft.AspNetCore.Mvc;
using ClosedXML.Excel;

[ApiController]
[Route("REGISTRO_ENTRADAS_TEMPORAL")]
public class RegistroEntradasTemporalController : ControllerBase
{
    private readonly RegistroEntradasTemporalService _service;

    private static readonly string _pdfFolder =
        Path.Combine(Directory.GetCurrentDirectory(), "pdfs", "registro_entradas_temporal");

    public RegistroEntradasTemporalController(RegistroEntradasTemporalService service)
    {
        _service = service;
        Directory.CreateDirectory(_pdfFolder);
    }

    // ── GET paginado + filtros ──────────────────────────────────────────────
    [HttpGet]
    public IActionResult GetAll(
        [FromQuery] int page = 1,
        [FromQuery] int limit = 10,
        [FromQuery] string? ID_REGISTRO = null,
        [FromQuery] string? ID_OC = null,
        [FromQuery] string? SERIE = null,
        [FromQuery] string? FOLIO = null,
        [FromQuery] string? OC = null,
        [FromQuery] string? FECHA_INGRESO = null,
        [FromQuery] string? PROVEEDOR = null,
        [FromQuery] string? RECIBIDO_POR = null,
        [FromQuery] string? CATEGORIA = null,
        [FromQuery] string? SUBCATEGORIA = null,
        [FromQuery] string? TIPO = null,
        [FromQuery] string? MARCA = null,
        [FromQuery] string? MODELO = null,
        [FromQuery] string? UBICACION = null,
        [FromQuery] string? MONEDA = null,
        [FromQuery] string? STATUS_ENTRADA = null)
    {
        var (data, total) = _service.GetAll(
            page, limit,
            ID_REGISTRO, ID_OC, SERIE, FOLIO, OC, FECHA_INGRESO,
            PROVEEDOR, RECIBIDO_POR, CATEGORIA, SUBCATEGORIA,
            TIPO, MARCA, MODELO, UBICACION, MONEDA, STATUS_ENTRADA);

        return Ok(new { data, total });
    }

    // ── GET por ID ─────────────────────────────────────────────────────────
    [HttpGet("{id}")]
    public IActionResult GetById(int id)
    {
        var row = _service.GetById(id);
        if (row == null) return NotFound();
        return Ok(row);
    }

    // ── POST ───────────────────────────────────────────────────────────────
    [HttpPost]
    public IActionResult Create([FromBody] RegistroEntradaDto dto)
    {
        _service.Create(dto);
        return Ok();
    }

    // ── PUT ────────────────────────────────────────────────────────────────
    [HttpPut("{id}")]
    public IActionResult Update(int id, [FromBody] RegistroEntradaDto dto,
        [FromHeader(Name = "X-Usuario")] string? usuario = null)
    {
        _service.Update(id, dto, usuario);
        return Ok();
    }

    // ── DELETE ─────────────────────────────────────────────────────────────
    [HttpDelete("{id}")]
    public IActionResult Delete(int id)
    {
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
        [FromQuery] string? ID_REGISTRO = null,
        [FromQuery] string? ID_OC = null,
        [FromQuery] string? SERIE = null,
        [FromQuery] string? FOLIO = null,
        [FromQuery] string? OC = null,
        [FromQuery] string? FECHA_INGRESO = null,
        [FromQuery] string? PROVEEDOR = null,
        [FromQuery] string? RECIBIDO_POR = null,
        [FromQuery] string? CATEGORIA = null,
        [FromQuery] string? SUBCATEGORIA = null,
        [FromQuery] string? TIPO = null,
        [FromQuery] string? MARCA = null,
        [FromQuery] string? MODELO = null,
        [FromQuery] string? UBICACION = null,
        [FromQuery] string? MONEDA = null,
        [FromQuery] string? STATUS_ENTRADA = null)
    {
        var (data, _) = _service.GetAll(
            1, int.MaxValue,
            ID_REGISTRO, ID_OC, SERIE, FOLIO, OC, FECHA_INGRESO,
            PROVEEDOR, RECIBIDO_POR, CATEGORIA, SUBCATEGORIA,
            TIPO, MARCA, MODELO, UBICACION, MONEDA, STATUS_ENTRADA);

        var bytes = _service.GenerarExcel(data);
        return File(bytes,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            "RegistroEntradas_Filtrado.xlsx");
    }

    // ── EXPORTAR todo ──────────────────────────────────────────────────────
    [HttpGet("EXPORTAR_TODO")]
    public IActionResult ExportarTodo()
    {
        var (data, _) = _service.GetAll(1, int.MaxValue);
        var bytes = _service.GenerarExcel(data);
        return File(bytes,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            "RegistroEntradas_Todo.xlsx");
    }

    // ── EXPORTAR por año ───────────────────────────────────────────────────
    [HttpGet("EXPORTAR_ANIO")]
    public IActionResult ExportarPorAnio([FromQuery] int anio)
    {
        var data = _service.GetPorAnio(anio);
        var bytes = _service.GenerarExcel(data);
        return File(bytes,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            $"RegistroEntradas_{anio}.xlsx");
    }

    // ── HISTORIAL ──────────────────────────────────────────────────────────
    [HttpGet("{id}/HISTORIAL")]
    public IActionResult GetHistorial(int id)
    {
        var historial = _service.GetHistorial(id);
        return Ok(new { historial });
    }
}
