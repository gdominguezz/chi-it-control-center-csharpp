using ChiIT.Services;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("PANTALLAS_NF")]
public class PantallasNfController : ControllerBase
{
    private readonly PantallasNfService _service;

    public PantallasNfController(PantallasNfService service)
        => _service = service;

    // ── GET todos ─────────────────────────────────────────────────────────
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var registros = await _service.ListarAsync();
        return Ok(new { registros });
    }

    [ApiController]
    public class UsuarioController : ControllerBase
    {
        [HttpGet("/ME")]
        public IActionResult Me([FromHeader(Name = "X-Usuario")] string? usuario)
        {
            return Ok(new
            {
                usuario = usuario ?? "USUARIO",
                nombre = usuario ?? "USUARIO",
                rol = "USER"
            });
        }
    }

    // ── GET por ID ────────────────────────────────────────────────────────
    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        var row = await _service.ObtenerAsync(id);
        if (row == null) return NotFound();
        return Ok(row);
    }

    // ── POST ──────────────────────────────────────────────────────────────
    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] PantallaNfDto dto,
        [FromHeader(Name = "X-Usuario")] string? usuario = null)
    {
        var id = await _service.CrearAsync(dto, usuario);
        return Ok(new { id });
    }

    // ── PUT ───────────────────────────────────────────────────────────────
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(
        int id,
        [FromBody] PantallaNfDto dto,
        [FromHeader(Name = "X-Usuario")] string? usuario = null)
    {
        var ok = await _service.EditarAsync(id, dto, usuario);
        if (!ok) return NotFound();
        return Ok();
    }

    // ── DELETE ────────────────────────────────────────────────────────────
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var ok = await _service.EliminarAsync(id);
        if (!ok) return NotFound();
        return Ok();
    }

    // ── HISTORIAL ─────────────────────────────────────────────────────────
    [HttpGet("{id:int}/HISTORIAL")]
    public async Task<IActionResult> GetHistorial(int id)
    {
        var historial = await _service.ObtenerHistorialAsync(id);
        return Ok(new { historial });
    }

    // ── EXPORTAR Excel ────────────────────────────────────────────────────
    [HttpGet("EXPORTAR")]
    public async Task<IActionResult> Exportar()
    {
        var bytes = await _service.ExportarExcelAsync();
        return File(bytes,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            "PantallasNF.xlsx");
    }
}
