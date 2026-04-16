
using Microsoft.AspNetCore.Mvc;
using ClosedXML.Excel;


[ApiController]
[Route("PRESUPUESTOS_REQ_VS_OC")]
public class PresupuestosReqVsOcController : ControllerBase
{
    private readonly PresupuestosReqVsOcService _service;
    public PresupuestosReqVsOcController(PresupuestosReqVsOcService service)
        => _service = service;

    // ── GET paginado + filtros ──────────────────────────────────────────────
    [HttpGet]
    public IActionResult GetAll(
        [FromQuery] int page = 1,
        [FromQuery] int limit = 10,
        [FromQuery] string? NO_REQUISICION   = null,
        [FromQuery] string? ORDEN_COMPRA     = null,
        [FromQuery] string? FECHA_COMPRA     = null,
        [FromQuery] string? PO_SUBTOTAL      = null,
        [FromQuery] string? MONEDA           = null,
        [FromQuery] string? OC_SUBTOTAL      = null,
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
        _service.Delete(id);
        return Ok();
    }

    // ── PDF: subir ─────────────────────────────────────────────────────────
    [HttpPost("PDF/{id}")]
    public async Task<IActionResult> SubirPDF(int id, IFormFile file)
    {
        if (file == null || file.Length == 0) return BadRequest("Sin archivo.");
        using var ms = new MemoryStream();
        await file.CopyToAsync(ms);
        _service.GuardarPDF(id, ms.ToArray());
        return Ok();
    }

    // ── PDF: ver ───────────────────────────────────────────────────────────
    [HttpGet("PDF/{id}")]
    public IActionResult VerPDF(int id)
    {
        var pdf = _service.ObtenerPDF(id);
        if (pdf == null) return NotFound();
        return File(pdf, "application/pdf");
    }

    // ── PDF: eliminar ──────────────────────────────────────────────────────
    [HttpDelete("PDF/{id}")]
    public IActionResult EliminarPDF(int id)
    {
        _service.EliminarPDF(id);
        return Ok();
    }

    // ── EXPORTAR filtrado ──────────────────────────────────────────────────
    [HttpGet("EXPORTAR")]
    public IActionResult ExportarFiltrado(
        [FromQuery] string? NO_REQUISICION   = null,
        [FromQuery] string? ORDEN_COMPRA     = null,
        [FromQuery] string? FECHA_COMPRA     = null,
        [FromQuery] string? PO_SUBTOTAL      = null,
        [FromQuery] string? MONEDA           = null,
        [FromQuery] string? OC_SUBTOTAL      = null,
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
