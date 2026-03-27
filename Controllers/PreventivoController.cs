using ChiIT.Data;
using ChiIT.Models;
using ChiIT.Services;
using Microsoft.AspNetCore.Mvc;
using Npgsql;
using System.Text.Json;

namespace ChiIT.Controllers;

[ApiController]
public class PreventivoController : ControllerBase
{
    private readonly DbConnectionPool _db;
    private readonly AuditoriaService _auditoria;
    private readonly ExcelService _excel;
    private readonly QrService _qr;
    private readonly string _pdfDir;

    public PreventivoController(DbConnectionPool db, AuditoriaService auditoria,
                                ExcelService excel, QrService qr, IConfiguration config)
    {
        _db = db;
        _auditoria = auditoria;
        _excel = excel;
        _qr = qr;
        _pdfDir = config["AppSettings:PdfDir"] ?? "PDF_DATABASE/PREVENTIVOS";
        Directory.CreateDirectory(_pdfDir);
    }

    // ── GET /PREVENTIVOS ─────────────────────────────────
    [HttpGet("PREVENTIVOS")]
    public IActionResult ObtenerPreventivos([FromQuery] FiltrosPreventivo f)
    {
        var where = "WHERE 1=1";
        var parms = new List<NpgsqlParameter>();
        int pIdx = 1;

        void Add(string clause, object val)
        {
            where += $" AND {clause} ILIKE @p{pIdx}";
            parms.Add(new NpgsqlParameter($"p{pIdx++}", $"%{val}%"));
        }

        if (!string.IsNullOrWhiteSpace(f.ID_EQUIPO)) Add("id_equipo", f.ID_EQUIPO);
        if (!string.IsNullOrWhiteSpace(f.UBICACION)) Add("ubicacion", f.UBICACION);
        if (!string.IsNullOrWhiteSpace(f.nombre_dispositivo)) Add("nombre_dispositivo", f.nombre_dispositivo);
        if (!string.IsNullOrWhiteSpace(f.PLANTA)) Add("planta", f.PLANTA);
        if (!string.IsNullOrWhiteSpace(f.CATEGORIA_COLOR)) Add("categoria_color", f.CATEGORIA_COLOR);
        if (!string.IsNullOrWhiteSpace(f.OBSERVACIONES)) Add("observaciones", f.OBSERVACIONES);

        if (!string.IsNullOrWhiteSpace(f.ANIO_CREACION) && int.TryParse(f.ANIO_CREACION, out int anio))
        {
            where += $" AND anio_creacion = @p{pIdx}";
            parms.Add(new NpgsqlParameter($"p{pIdx++}", anio));
        }

        using var conn = _db.Open();

        // Helper para clonar parámetros — Npgsql no permite reusar el mismo objeto en dos comandos
        NpgsqlParameter[] ClonarParams() =>
            parms.Select(p => new NpgsqlParameter(p.ParameterName, p.Value)).ToArray();

        // Total
        using var cntCmd = conn.CreateCommand();
        cntCmd.CommandText = $"SELECT COUNT(*) FROM public.mantenimientos_preventivos {where}";
        cntCmd.Parameters.AddRange(ClonarParams());
        var total = Convert.ToInt64(cntCmd.ExecuteScalar()!);

        // Datos paginados
        int offset = (f.Page - 1) * f.Limit;
        parms.Add(new NpgsqlParameter($"p{pIdx++}", f.Limit));
        parms.Add(new NpgsqlParameter($"p{pIdx}", offset));

        using var cmd = conn.CreateCommand();
        cmd.CommandText = $"""
            SELECT *,
              CASE WHEN pdf IS NOT NULL THEN true ELSE false END AS tiene_pdf
            FROM public.mantenimientos_preventivos
            {where}
            ORDER BY
              CASE LOWER(categoria_color)
                WHEN 'verde'    THEN 1
                WHEN 'gris'     THEN 2
                WHEN 'azul'     THEN 3
                WHEN 'rojo'     THEN 4
                WHEN 'amarillo' THEN 5
                WHEN 'rosa'     THEN 6
                ELSE 7
              END, id DESC
            LIMIT @p{pIdx - 1} OFFSET @p{pIdx}
            """;
        cmd.Parameters.AddRange(ClonarParams());

        using var reader = cmd.ExecuteReader();
        var data = new List<Dictionary<string, object?>>();
        while (reader.Read())
        {
            var row = new Dictionary<string, object?>();
            for (int i = 0; i < reader.FieldCount; i++)
                row[reader.GetName(i)] = reader.IsDBNull(i) ? null : reader.GetValue(i);
            data.Add(row);
        }

        return Ok(new { data, total, page = f.Page });
    }

    // ── GET /PREVENTIVO/DATOS/{id} ───────────────────────
    [HttpGet("PREVENTIVO/DATOS/{id:int}")]
    public IActionResult ObtenerDatos(int id, [FromQuery] string? usuario)
    {
        using var conn = _db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT ubicacion, planta FROM public.mantenimientos_preventivos WHERE id=@id";
        cmd.Parameters.AddWithValue("id", id);

        using var r = cmd.ExecuteReader();
        if (!r.Read()) return Ok(new { error = "Registro no encontrado" });
        var ubicacion = r.GetString(0);
        var planta = r.IsDBNull(1) ? "" : r.GetString(1);
        r.Close();

        // Equipos en la misma ubicación
        using var cmd2 = conn.CreateCommand();
        cmd2.CommandText = "SELECT id, id_equipo, nombre_dispositivo FROM public.mantenimientos_preventivos WHERE ubicacion=@u";
        cmd2.Parameters.AddWithValue("u", ubicacion);

        string pc = "", impresora = "", ups = "", portatil = "";
        int? idPc = null, idPortatil = null, idImpresora = null, idUps = null;

        using var r2 = cmd2.ExecuteReader();
        while (r2.Read())
        {
            var regId = r2.GetInt64(0);
            var equipo = r2.IsDBNull(1) ? "" : r2.GetString(1);
            var disp = (r2.IsDBNull(2) ? "" : r2.GetString(2)).ToUpper();

            if (disp.Contains("PORTATIL") || disp.Contains("LAPTOP"))
            { portatil = equipo; idPortatil = (int)regId; }
            else if (disp.Contains("COMPUTADORA") || disp.Contains("CPU"))
            { pc = equipo; idPc = (int)regId; }

            if (disp.Contains("IMPRESORA"))
            { impresora = equipo; idImpresora = (int)regId; }
            if (disp.Contains("UPS"))
            { ups = equipo; idUps = (int)regId; }
        }
        r2.Close();

        // Nombre del usuario
        var nombreUsuario = "";
        if (!string.IsNullOrWhiteSpace(usuario))
        {
            using var cu = conn.CreateCommand();
            cu.CommandText = "SELECT nombre FROM public.usuarios WHERE usuario=@u";
            cu.Parameters.AddWithValue("u", usuario.ToUpper());
            nombreUsuario = cu.ExecuteScalar()?.ToString() ?? "";
        }

        return Ok(new
        {
            planta,
            ubicacion,
            usuario = nombreUsuario,
            portatil,
            pc,
            impresora,
            ups,
            id_portatil = idPortatil,
            id_pc = idPc,
            id_impresora = idImpresora,
            id_ups = idUps
        });
    }

    // ── GET /PREVENTIVOS/{id}/HISTORIAL ──────────────────
    [HttpGet("PREVENTIVOS/{id:int}/HISTORIAL")]
    public IActionResult ObtenerHistorial(int id)
    {
        using var conn = _db.Open();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT id,id_equipo,ubicacion,plazo,realizado_por,
                   fecha_realizacion,observaciones,nombre_dispositivo,planta,categoria_color,anio_creacion
            FROM public.mantenimientos_preventivos WHERE id=@id
            """;
        cmd.Parameters.AddWithValue("id", id);

        using var r = cmd.ExecuteReader();
        if (!r.Read()) return Ok(new { success = false, error = "Registro no encontrado" });

        var actual = new Dictionary<string, object?>();
        for (int i = 0; i < r.FieldCount; i++)
            actual[r.GetName(i)] = r.IsDBNull(i) ? null : r.GetValue(i);
        r.Close();

        using var cmd2 = conn.CreateCommand();
        cmd2.CommandText = """
            SELECT id, fecha_cambio, usuario, registro_anterior, registro_nuevo
            FROM public.auditoria_preventivos
            WHERE registro_id=@id ORDER BY fecha_cambio DESC
            """;
        cmd2.Parameters.AddWithValue("id", id);

        var historial = new List<object>();
        using var r2 = cmd2.ExecuteReader();
        while (r2.Read())
        {
            historial.Add(new
            {
                id = r2.GetInt32(0),
                fecha = r2.IsDBNull(1) ? null : r2.GetDateTime(1).ToString("o"),
                usuario = r2.IsDBNull(2) ? null : r2.GetString(2),
                registro_anterior = r2.IsDBNull(3) ? (object)new { } : JsonSerializer.Deserialize<object>(r2.GetString(3))!,
                registro_nuevo = r2.IsDBNull(4) ? (object)new { } : JsonSerializer.Deserialize<object>(r2.GetString(4))!,
            });
        }

        return Ok(new { success = true, registro_actual = actual, historial });
    }

    // ── POST /PREVENTIVO ─────────────────────────────────
    [HttpPost("PREVENTIVO")]
    public IActionResult Crear([FromBody] PreventivoRequest data)
    {
        try
        {
            using var conn = _db.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = """
                INSERT INTO public.mantenimientos_preventivos
                (id_equipo,ubicacion,plazo,realizado_por,fecha_realizacion,
                 observaciones,nombre_dispositivo,planta,categoria_color,anio_creacion)
                VALUES (@e,@u,@p,@rp,@fr,@o,@nd,@pl,@cc,@ac) RETURNING id
                """;
            cmd.Parameters.AddWithValue("e", (object?)data.ID_EQUIPO ?? DBNull.Value);
            cmd.Parameters.AddWithValue("u", (object?)data.UBICACION ?? DBNull.Value);
            cmd.Parameters.AddWithValue("p", (object?)data.PLAZO ?? DBNull.Value);
            cmd.Parameters.AddWithValue("rp", (object?)data.REALIZADO_POR ?? DBNull.Value);
            cmd.Parameters.AddWithValue("fr", (object?)data.FECHA_REALIZACION ?? DBNull.Value);
            cmd.Parameters.AddWithValue("o", (object?)data.OBSERVACIONES ?? DBNull.Value);
            cmd.Parameters.AddWithValue("nd", (object?)data.nombre_dispositivo ?? DBNull.Value);
            cmd.Parameters.AddWithValue("pl", (object?)data.PLANTA ?? DBNull.Value);
            cmd.Parameters.AddWithValue("cc", (object?)data.CATEGORIA_COLOR ?? DBNull.Value);
            cmd.Parameters.AddWithValue("ac", (object?)data.ANIO_CREACION ?? DBNull.Value);

            var newId = cmd.ExecuteScalar();
            return Ok(new { id = newId });
        }
        catch (Exception ex) { return Ok(new { error = ex.Message }); }
    }

    // ── PUT /PREVENTIVO/{id} ──────────────────────────────
    [HttpPut("PREVENTIVO/{id:int}")]
    public IActionResult Editar(int id, [FromBody] PreventivoRequest data, [FromQuery] string? usuario)
    {
        try
        {
            var usr = Request.Cookies["usuario"]
                   ?? Request.Headers["X-Usuario"].FirstOrDefault()
                   ?? usuario ?? "SISTEMA";

            using var conn = _db.Open();

            // Registro anterior
            using var sel = conn.CreateCommand();
            sel.CommandText = """
                SELECT id_equipo,ubicacion,plazo,realizado_por,fecha_realizacion,
                       observaciones,nombre_dispositivo,planta,categoria_color,anio_creacion
                FROM public.mantenimientos_preventivos WHERE id=@id
                """;
            sel.Parameters.AddWithValue("id", id);

            Dictionary<string, object?> anterior = new();
            using (var r = sel.ExecuteReader())
            {
                if (r.Read())
                    for (int i = 0; i < r.FieldCount; i++)
                        anterior[r.GetName(i)] = r.IsDBNull(i) ? null : r.GetValue(i);
            }

            // Actualizar — COALESCE preserva plazo/realizado_por/fecha_realizacion
            // si llegan null (ej: edición desde página QR que no envía esos campos)
            using var upd = conn.CreateCommand();
            upd.CommandText = """
                UPDATE public.mantenimientos_preventivos SET
                id_equipo          = @e,
                ubicacion          = @u,
                plazo              = COALESCE(@p,  plazo),
                realizado_por      = COALESCE(@rp, realizado_por),
                fecha_realizacion  = COALESCE(@fr, fecha_realizacion),
                observaciones      = @o,
                nombre_dispositivo = @nd,
                planta             = @pl,
                categoria_color    = @cc,
                anio_creacion      = @ac
                WHERE id=@id
                """;
            upd.Parameters.AddWithValue("e", (object?)data.ID_EQUIPO ?? DBNull.Value);
            upd.Parameters.AddWithValue("u", (object?)data.UBICACION ?? DBNull.Value);
            upd.Parameters.AddWithValue("p", (object?)data.PLAZO ?? DBNull.Value);
            upd.Parameters.AddWithValue("rp", (object?)data.REALIZADO_POR ?? DBNull.Value);
            upd.Parameters.AddWithValue("fr", (object?)data.FECHA_REALIZACION ?? DBNull.Value);
            upd.Parameters.AddWithValue("o", (object?)data.OBSERVACIONES ?? DBNull.Value);
            upd.Parameters.AddWithValue("nd", (object?)data.nombre_dispositivo ?? DBNull.Value);
            upd.Parameters.AddWithValue("pl", (object?)data.PLANTA ?? DBNull.Value);
            upd.Parameters.AddWithValue("cc", (object?)data.CATEGORIA_COLOR ?? DBNull.Value);
            upd.Parameters.AddWithValue("ac", (object?)data.ANIO_CREACION ?? DBNull.Value);
            upd.Parameters.AddWithValue("id", id);
            upd.ExecuteNonQuery();

            // Leer estado REAL post-UPDATE para que registro_nuevo en auditoría
            // refleje los valores definitivos (incluyendo los preservados por COALESCE)
            Dictionary<string, object?> nuevo = new();
            using var post = conn.CreateCommand();
            post.CommandText = """
                SELECT id_equipo,ubicacion,plazo,realizado_por,fecha_realizacion,
                       observaciones,nombre_dispositivo,planta,categoria_color,anio_creacion
                FROM public.mantenimientos_preventivos WHERE id=@id
                """;
            post.Parameters.AddWithValue("id", id);
            using (var rPost = post.ExecuteReader())
            {
                if (rPost.Read())
                    for (int i = 0; i < rPost.FieldCount; i++)
                        nuevo[rPost.GetName(i)] = rPost.IsDBNull(i) ? null : rPost.GetValue(i);
            }

            _auditoria.Registrar(id, usr, anterior, nuevo);

            return Ok(new { mensaje = "ACTUALIZADO" });
        }
        catch (Exception ex) { return Ok(new { error = ex.Message }); }
    }

    // ── DELETE /PREVENTIVO/{id} ───────────────────────────
    [HttpDelete("PREVENTIVO/{id:int}")]
    public IActionResult Eliminar(int id)
    {
        var usuario = Request.Cookies["usuario"];
        try
        {
            using var conn = _db.Open();

            using var chk = conn.CreateCommand();
            chk.CommandText = "SELECT rol FROM public.usuarios WHERE usuario=@u";
            chk.Parameters.AddWithValue("u", usuario ?? "");
            var rol = chk.ExecuteScalar()?.ToString();
            if (rol != "ADMIN") return Ok(new { error = "No tienes permiso para eliminar" });

            using var del = conn.CreateCommand();
            del.CommandText = "DELETE FROM public.mantenimientos_preventivos WHERE id=@id";
            del.Parameters.AddWithValue("id", id);
            del.ExecuteNonQuery();

            return Ok(new { ok = true });
        }
        catch (Exception ex) { return Ok(new { error = ex.Message }); }
    }

    // ── PDF ───────────────────────────────────────────────
    [HttpPost("PREVENTIVO/PDF/{id:int}")]
    public async Task<IActionResult> SubirPdf(int id, IFormFile file)
    {
        try
        {
            var path = Path.Combine(_pdfDir, $"{id}.pdf");
            await using var fs = System.IO.File.Create(path);
            await file.CopyToAsync(fs);

            using var conn = _db.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "UPDATE public.mantenimientos_preventivos SET pdf=@p WHERE id=@id";
            cmd.Parameters.AddWithValue("p", path);
            cmd.Parameters.AddWithValue("id", id);
            cmd.ExecuteNonQuery();

            return Ok(new { mensaje = "PDF subido" });
        }
        catch (Exception ex) { return Ok(new { error = ex.Message }); }
    }

    [HttpGet("PREVENTIVO/PDF/{id:int}")]
    public IActionResult ObtenerPdf(int id)
    {
        var path = Path.Combine(_pdfDir, $"{id}.pdf");
        if (!System.IO.File.Exists(path)) return Ok(new { error = "PDF no encontrado" });
        return PhysicalFile(Path.GetFullPath(path), "application/pdf");
    }

    [HttpDelete("PREVENTIVO/PDF/{id:int}")]
    public IActionResult EliminarPdf(int id)
    {
        try
        {
            var path = Path.Combine(_pdfDir, $"{id}.pdf");
            if (System.IO.File.Exists(path)) System.IO.File.Delete(path);

            using var conn = _db.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "UPDATE public.mantenimientos_preventivos SET pdf=NULL WHERE id=@id";
            cmd.Parameters.AddWithValue("id", id);
            cmd.ExecuteNonQuery();

            return Ok(new { mensaje = "PDF eliminado" });
        }
        catch (Exception ex) { return Ok(new { error = ex.Message }); }
    }

    // ── EXPORTAR EXCEL ────────────────────────────────────
    [HttpGet("PREVENTIVOS/EXPORTAR_TODO")]
    public IActionResult ExportarTodo()
    {
        using var conn = _db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT * FROM public.mantenimientos_preventivos ORDER BY id DESC";
        using var reader = cmd.ExecuteReader();
        var bytes = _excel.GenerarExcel(reader, "Preventivos");
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                    "preventivos_todo.xlsx");
    }

    [HttpGet("PREVENTIVOS/EXPORTAR_FILTRADO")]
    public IActionResult ExportarFiltrado([FromQuery] FiltrosPreventivo f)
    {
        var where = "WHERE 1=1";
        var parms = new List<NpgsqlParameter>();
        int pIdx = 1;

        void Add(string col, string? val)
        {
            if (string.IsNullOrWhiteSpace(val)) return;
            where += $" AND {col} ILIKE @p{pIdx}";
            parms.Add(new NpgsqlParameter($"p{pIdx++}", $"%{val}%"));
        }

        Add("id_equipo", f.ID_EQUIPO);
        Add("ubicacion", f.UBICACION);
        Add("nombre_dispositivo", f.nombre_dispositivo);
        Add("planta", f.PLANTA);
        Add("categoria_color", f.CATEGORIA_COLOR);
        Add("observaciones", f.OBSERVACIONES);

        using var conn = _db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $"""
            SELECT id,id_equipo,ubicacion,nombre_dispositivo,planta,categoria_color,observaciones
            FROM public.mantenimientos_preventivos {where} ORDER BY id DESC
            """;
        cmd.Parameters.AddRange(parms.ToArray());
        using var reader = cmd.ExecuteReader();
        var bytes = _excel.GenerarExcel(reader, "Preventivos");
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                    "preventivos_filtrados.xlsx");
    }

    [HttpGet("PREVENTIVOS/EXPORTAR_ANIO")]
    public IActionResult ExportarAnio([FromQuery] int anio)
    {
        using var conn = _db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT * FROM public.mantenimientos_preventivos WHERE anio_creacion=@a ORDER BY id DESC";
        cmd.Parameters.AddWithValue("a", anio);
        using var reader = cmd.ExecuteReader();
        var bytes = _excel.GenerarExcel(reader, "Preventivos");
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                    $"preventivos_{anio}.xlsx");
    }

    // ── QR ────────────────────────────────────────────────
    [HttpGet("QR_MESA_GENERAR/{ubicacion}")]
    public IActionResult GenerarQr(string ubicacion)
    {
        var bytes = _qr.Generar(Uri.UnescapeDataString(ubicacion));
        return File(bytes, "image/png");
    }

    [HttpGet("QR_GENERAR_TODOS")]
    public IActionResult GenerarTodosQr()
    {
        using var conn = _db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT DISTINCT ubicacion FROM public.mantenimientos_preventivos WHERE ubicacion IS NOT NULL";

        var ubicaciones = new List<string>();
        using var r = cmd.ExecuteReader();
        while (r.Read()) ubicaciones.Add(r.GetString(0));
        r.Close();

        using var ms = new MemoryStream();
        using var zip = new System.IO.Compression.ZipArchive(ms, System.IO.Compression.ZipArchiveMode.Create, true);

        foreach (var ub in ubicaciones)
        {
            try
            {
                var bytes = _qr.Generar(ub);
                var entry = zip.CreateEntry($"{ub}.png");
                using var es = entry.Open();
                es.Write(bytes);
            }
            catch (Exception ex) { Console.WriteLine($"QR error {ub}: {ex.Message}"); }
        }

        zip.Dispose();
        ms.Position = 0;
        return File(ms.ToArray(), "application/zip", "QRS_PREVENTIVOS.zip");
    }

    [HttpGet("QR_REIMPRIMIR/{ubicacion}")]
    public IActionResult ReimprimirQr(string ubicacion)
    {
        var ub = Uri.UnescapeDataString(ubicacion);
        var path = Path.Combine("QR_CODES/MESAS", $"{ub}.png");
        if (!System.IO.File.Exists(path))
            return Ok(new { success = false });
        return Ok(new { success = true, qr_url = $"/QR_CODES/MESAS/{Uri.EscapeDataString(ub)}.png" });
    }

    // ── PREVENTIVO DIGITAL ────────────────────────────────
    [HttpPost("PREVENTIVO/GUARDAR_DIGITAL/{id:int}")]
    public IActionResult GuardarDigital(int id, [FromBody] GuardarDigitalRequest data)
    {
        try
        {
            using var conn = _db.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "UPDATE public.mantenimientos_preventivos SET preventivo_digital=@d WHERE id=@id";
            cmd.Parameters.Add("d", NpgsqlTypes.NpgsqlDbType.Jsonb).Value = JsonSerializer.Serialize(data);
            cmd.Parameters.AddWithValue("id", id);
            cmd.ExecuteNonQuery();

            var idsValidos = data.Ids_con_check.Select(x => (int)x).ToList();

            if (idsValidos.Any())
            {
                var placeholders = string.Join(",", idsValidos.Select((_, i) => $"@i{i}"));
                using var upd = conn.CreateCommand();
                upd.CommandText = $"UPDATE public.mantenimientos_preventivos SET fecha_realizacion=NOW() WHERE id IN ({placeholders})";
                for (int i = 0; i < idsValidos.Count; i++)
                    upd.Parameters.AddWithValue($"i{i}", idsValidos[i]);
                upd.ExecuteNonQuery();
            }
            else
            {
                using var upd = conn.CreateCommand();
                upd.CommandText = "UPDATE public.mantenimientos_preventivos SET fecha_realizacion=NOW() WHERE id=@id";
                upd.Parameters.AddWithValue("id", id);
                upd.ExecuteNonQuery();
            }

            return Ok(new { ok = true });
        }
        catch (Exception ex) { return Ok(new { error = ex.Message }); }
    }

    [HttpGet("PREVENTIVO/DIGITAL/{id:int}")]
    public IActionResult ObtenerDigital(int id)
    {
        try
        {
            using var conn = _db.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT preventivo_digital FROM public.mantenimientos_preventivos WHERE id=@id";
            cmd.Parameters.AddWithValue("id", id);
            var raw = cmd.ExecuteScalar();
            if (raw == null || raw == DBNull.Value)
                return Ok(new { existe = false });
            var data = JsonSerializer.Deserialize<object>(raw.ToString()!);
            return Ok(new { existe = true, data });
        }
        catch (Exception ex) { return Ok(new { existe = false, error = ex.Message }); }
    }

    [HttpDelete("PREVENTIVO/ELIMINAR_DIGITAL/{id:int}")]
    public IActionResult EliminarDigital(int id)
    {
        try
        {
            using var conn = _db.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "UPDATE public.mantenimientos_preventivos SET preventivo_digital=NULL WHERE id=@id";
            cmd.Parameters.AddWithValue("id", id);
            cmd.ExecuteNonQuery();
            return Ok(new { ok = true });
        }
        catch (Exception ex) { return Ok(new { error = ex.Message }); }
    }

    // ── VERIFICAR USUARIO ─────────────────────────────────
    [HttpGet("PREVENTIVO/VERIFICAR_USUARIO")]
    public IActionResult VerificarUsuario([FromQuery] string usuario)
    {
        try
        {
            using var conn = _db.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT nombre FROM public.usuarios WHERE usuario=@u AND activo=true";
            cmd.Parameters.AddWithValue("u", usuario.ToUpper());
            var nombre = cmd.ExecuteScalar()?.ToString();
            if (nombre != null) return Ok(new { existe = true, nombre });
            return Ok(new { existe = false });
        }
        catch (Exception ex) { return Ok(new { existe = false, error = ex.Message }); }
    }

    // ── GUARDAR PM INDIVIDUAL ─────────────────────────────
    [HttpPost("PREVENTIVO/GUARDAR_PM/{id:int}")]
    public IActionResult GuardarPmIndividual(int id, [FromBody] GuardarPmRequest data)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(data.Fecha))
                return Ok(new { ok = false, error = "Fecha requerida" });

            var fechaRea = DateOnly.Parse(data.Fecha);
            var proximoPm = fechaRea.AddMonths(6);
            var proxStr = proximoPm.ToString("yyyy-MM-dd");

            var json = JsonSerializer.Serialize(new
            {
                usuario = data.Usuario,
                fecha = data.Fecha,
                proximo_pm = proxStr,
                checks = data.Checks,
                observaciones = data.Observaciones
            });

            using var conn = _db.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = """
                UPDATE public.mantenimientos_preventivos
                SET fecha_realizacion=@fr, plazo=@pl, realizado_por=@rp,
                    observaciones=@o, preventivo_digital=@pd
                WHERE id=@id
                """;
            cmd.Parameters.AddWithValue("fr", fechaRea.ToDateTime(TimeOnly.MinValue));
            cmd.Parameters.AddWithValue("pl", proxStr);
            cmd.Parameters.AddWithValue("rp", data.Usuario.ToUpper());
            cmd.Parameters.AddWithValue("o", data.Observaciones);
            cmd.Parameters.Add("pd", NpgsqlTypes.NpgsqlDbType.Jsonb).Value = json;
            cmd.Parameters.AddWithValue("id", id);
            cmd.ExecuteNonQuery();

            return Ok(new { ok = true, proximo_pm = proxStr });
        }
        catch (Exception ex) { return Ok(new { ok = false, error = ex.Message }); }
    }
}