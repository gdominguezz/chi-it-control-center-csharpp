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

    // ── GET /PREVENTIVOS/DISTRIBUCION_SEMANAL ────────────
    [HttpGet("PREVENTIVOS/DISTRIBUCION_SEMANAL")]
    public IActionResult DistribucionSemanal([FromQuery] int semana)
    {
        if (semana < 1) return Ok(new { error = "Semana debe ser >= 1" });

        using var conn = _db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT planta, ubicacion, id, id_equipo, nombre_dispositivo, categoria_color,
                   CASE WHEN preventivo_digital IS NOT NULL THEN true ELSE false END AS tiene_pm_p1,
                   CASE WHEN preventivo_digital_p2 IS NOT NULL THEN true ELSE false END AS tiene_pm_p2,
                   fecha_realizacion, fecha_realizacion_p2
            FROM public.mantenimientos_preventivos
            WHERE nombre_dispositivo IN (
                'COMPUTADORA DE ESCRITORIO','LAPTOP','UPS','IMPRESORA TERMICA'
            )
            ORDER BY
              CASE planta
                WHEN 'B1'             THEN 1
                WHEN 'B2'             THEN 2
                WHEN 'PLANTA SATELITE' THEN 3
                WHEN 'PLANTA MIXING'  THEN 4
                WHEN 'BODEGA'         THEN 5
                ELSE 6
              END,
              ubicacion
            """;

        // Agrupar por planta+ubicacion respetando el orden del query
        var grupos = new List<(string planta, string ubicacion, List<object> equipos)>();
        var grupoMap = new Dictionary<string, int>(); // "planta|ubicacion" → índice

        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            var planta = r.IsDBNull(0) ? "" : r.GetString(0);
            var ubicacion = r.IsDBNull(1) ? "" : r.GetString(1);
            var key = planta + "|" + ubicacion;

            var equipo = new
            {
                id = r.GetInt64(2),
                id_equipo = r.IsDBNull(3) ? null : r.GetString(3),
                nombre_dispositivo = r.IsDBNull(4) ? null : r.GetString(4),
                categoria_color = r.IsDBNull(5) ? null : r.GetString(5),
                tiene_pm_p1 = !r.IsDBNull(6) && r.GetBoolean(6),
                tiene_pm_p2 = !r.IsDBNull(7) && r.GetBoolean(7),
                fecha_pm_p1 = r.IsDBNull(8) ? null : r.GetDateTime(8).ToString("yyyy-MM-dd"),
                fecha_pm_p2 = r.IsDBNull(9) ? null : r.GetDateTime(9).ToString("yyyy-MM-dd"),
            };

            if (!grupoMap.TryGetValue(key, out int idx))
            {
                idx = grupos.Count;
                grupoMap[key] = idx;
                grupos.Add((planta, ubicacion, new List<object>()));
            }
            grupos[idx].equipos.Add(equipo);
        }
        r.Close();

        // Distribuir en semanas de máximo 40 equipos (ubicaciones completas)
        var semanas = new List<List<(string planta, string ubicacion, List<object> equipos)>>();
        var semActual = new List<(string planta, string ubicacion, List<object> equipos)>();
        int countActual = 0;

        foreach (var g in grupos)
        {
            if (countActual + g.equipos.Count > 40 && semActual.Count > 0)
            {
                semanas.Add(semActual);
                semActual = new List<(string, string, List<object>)>();
                countActual = 0;
            }
            semActual.Add(g);
            countActual += g.equipos.Count;
        }
        if (semActual.Count > 0) semanas.Add(semActual);

        // Validar semana pedida
        if (semana > semanas.Count)
            return Ok(new
            {
                semana,
                total_semanas = semanas.Count,
                total_equipos = 0,
                ubicaciones = new List<object>(),
                error = $"Solo hay {semanas.Count} semanas en el período"
            });

        var semDatos = semanas[semana - 1];
        var resultado = semDatos.Select(g => new
        {
            planta = g.planta,
            ubicacion = g.ubicacion,
            total = g.equipos.Count,
            equipos = g.equipos
        }).ToList();

        return Ok(new
        {
            semana,
            total_semanas = semanas.Count,
            total_equipos = semDatos.Sum(g => g.equipos.Count),
            ubicaciones = resultado
        });
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
        var ub = Uri.UnescapeDataString(ubicacion).Trim();

        // Validar que la ubicación exista en la DB
        using var conn = _db.Open();
        using var chk = conn.CreateCommand();
        chk.CommandText = """
            SELECT COUNT(*) FROM public.mantenimientos_preventivos
            WHERE TRIM(LOWER(ubicacion)) = TRIM(LOWER(@u))
            """;
        chk.Parameters.AddWithValue("u", ub);
        var existe = Convert.ToInt64(chk.ExecuteScalar()!) > 0;

        if (!existe)
            return BadRequest(new { error = $"La ubicación '{ub}' no existe en la base de datos. Verifica el nombre exacto." });

        var bytes = _qr.Generar(ub);
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

    // ── QR POR EQUIPO ─────────────────────────────────────
    [HttpGet("QR_EQUIPO/{id:int}")]
    public IActionResult QrPorEquipo(int id)
    {
        try
        {
            using var conn = _db.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT id_equipo FROM public.mantenimientos_preventivos WHERE id=@id";
            cmd.Parameters.AddWithValue("id", id);

            var raw = cmd.ExecuteScalar();
            var idEquipo = (raw == null || raw == DBNull.Value || string.IsNullOrWhiteSpace(raw.ToString()))
                           ? id.ToString()
                           : raw.ToString()!.Trim();

            var bytes = _qr.GenerarPorEquipo(idEquipo);
            return File(bytes, "image/png");
        }
        catch (Exception ex) { return StatusCode(500, ex.Message); }
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
            cmd.CommandText = """
            UPDATE public.mantenimientos_preventivos
            SET preventivo_digital = NULL,
                fecha_realizacion  = NULL,
                realizado_por      = NULL,
                plazo              = NULL
            WHERE id = @id
            """;
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
    //PREVENTIVO DIGITAL DE PERIODO 2
    [HttpPost("PREVENTIVO/P2/{id:int}")]
    public IActionResult GuardarP2(int id, [FromBody] JsonElement data)
    {
        using var conn = _db.Open();
        using var cmd = conn.CreateCommand();

        cmd.CommandText = """
        UPDATE public.mantenimientos_preventivos
        SET preventivo_digital_p2 = @json,
            fecha_realizacion_p2 = NOW()
        WHERE id = @id
    """;

        cmd.Parameters.AddWithValue("json", data.ToString());
        cmd.Parameters.AddWithValue("id", id);

        cmd.ExecuteNonQuery();

        return Ok(new { ok = true });
    }

    // ── GUARDAR PM PERÍODO 2 ──────────────────────────────
    [HttpPost("PREVENTIVO/GUARDAR_PM_P2/{id:int}")]
    public IActionResult GuardarPmP2(int id, [FromBody] GuardarPmRequest data)
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
                observaciones = data.Observaciones,
                requiere_correctivo = data.RequiereCorrectivo,
                verificado_por = (string?)null  // se llena después desde el panel de auditoría
            });

            using var conn = _db.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = """
                UPDATE public.mantenimientos_preventivos
                SET fecha_realizacion_p2=@fr, plazo_p2=@pl, realizado_por_p2=@rp,
                    preventivo_digital_p2=@pd
                WHERE id=@id
                """;
            cmd.Parameters.AddWithValue("fr", fechaRea.ToDateTime(TimeOnly.MinValue));
            cmd.Parameters.AddWithValue("pl", proximoPm.ToDateTime(TimeOnly.MinValue));
            cmd.Parameters.AddWithValue("rp", data.Usuario.ToUpper());
            cmd.Parameters.Add("pd", NpgsqlTypes.NpgsqlDbType.Jsonb).Value = json;
            cmd.Parameters.AddWithValue("id", id);
            cmd.ExecuteNonQuery();

            return Ok(new { ok = true, proximo_pm = proxStr });
        }
        catch (Exception ex) { return Ok(new { ok = false, error = ex.Message }); }
    }

    // ── OBTENER PM PERÍODO 2 ──────────────────────────────
    [HttpGet("PREVENTIVO/DIGITAL_P2/{id:int}")]
    public IActionResult ObtenerDigitalP2(int id)
    {
        try
        {
            using var conn = _db.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT preventivo_digital_p2 FROM public.mantenimientos_preventivos WHERE id=@id";
            cmd.Parameters.AddWithValue("id", id);
            var raw = cmd.ExecuteScalar();
            if (raw == null || raw == DBNull.Value)
                return Ok(new { existe = false });
            var data = JsonSerializer.Deserialize<object>(raw.ToString()!);
            return Ok(new { existe = true, data });
        }
        catch (Exception ex) { return Ok(new { existe = false, error = ex.Message }); }
    }

    // ── ELIMINAR PM PERÍODO 2 ─────────────────────────────
    [HttpDelete("PREVENTIVO/ELIMINAR_DIGITAL_P2/{id:int}")]
    public IActionResult EliminarDigitalP2(int id)
    {
        try
        {
            using var conn = _db.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "UPDATE public.mantenimientos_preventivos SET preventivo_digital_p2=NULL, fecha_realizacion_p2=NULL, plazo_p2=NULL, realizado_por_p2=NULL WHERE id=@id";
            cmd.Parameters.AddWithValue("id", id);
            cmd.ExecuteNonQuery();
            return Ok(new { ok = true });
        }
        catch (Exception ex) { return Ok(new { error = ex.Message }); }
    }

    // ── RECALENDARIZACIÓN ─────────────────────────────────
    // POST /PREVENTIVO/RECALENDARIZAR
    // Mueve un dispositivo a una nueva ubicación.
    // Si la nueva ubicación está ocupada, también mueve ese dispositivo a una tercera ubicación.
    [HttpPost("PREVENTIVO/RECALENDARIZAR")]
    public IActionResult Recalendarizar([FromBody] RecalendarizarRequest data)
    {
        try
        {
            var usuario = Request.Cookies["usuario"]
                       ?? Request.Headers["X-Usuario"].FirstOrDefault()
                       ?? data.Usuario ?? "SISTEMA";

            if (data.IdDispositivo <= 0)
                return Ok(new { ok = false, error = "ID de dispositivo requerido" });
            if (string.IsNullOrWhiteSpace(data.NuevaUbicacion))
                return Ok(new { ok = false, error = "Nueva ubicación requerida" });

            using var conn = _db.Open();

            // 1. Obtener datos del dispositivo a mover
            using var sel1 = conn.CreateCommand();
            sel1.CommandText = "SELECT id, ubicacion, id_equipo, nombre_dispositivo FROM public.mantenimientos_preventivos WHERE id=@id";
            sel1.Parameters.AddWithValue("id", data.IdDispositivo);
            string ubicacionAnterior = "", idEquipo1 = "", nomDisp1 = "";
            using (var r = sel1.ExecuteReader())
            {
                if (!r.Read()) return Ok(new { ok = false, error = "Dispositivo no encontrado" });
                ubicacionAnterior = r.IsDBNull(1) ? "" : r.GetString(1);
                idEquipo1 = r.IsDBNull(2) ? "" : r.GetString(2);
                nomDisp1 = r.IsDBNull(3) ? "" : r.GetString(3);
            }

            var nuevaUbicacion = data.NuevaUbicacion.Trim();

            // No mover a la misma ubicación
            if (string.Equals(ubicacionAnterior, nuevaUbicacion, StringComparison.OrdinalIgnoreCase))
                return Ok(new { ok = false, error = "El dispositivo ya está en esa ubicación" });

            // 2. Verificar si la nueva ubicación está ocupada (buscar cualquier dispositivo allí)
            using var chk = conn.CreateCommand();
            chk.CommandText = "SELECT id, ubicacion, id_equipo, nombre_dispositivo FROM public.mantenimientos_preventivos WHERE TRIM(LOWER(ubicacion))=TRIM(LOWER(@u)) LIMIT 1";
            chk.Parameters.AddWithValue("u", nuevaUbicacion);
            int idOcupante = 0; string ubicacionOcupanteAnterior = "", idEquipo2 = "", nomDisp2 = "";
            using (var r = chk.ExecuteReader())
            {
                if (r.Read())
                {
                    idOcupante = (int)r.GetInt64(0);
                    ubicacionOcupanteAnterior = r.IsDBNull(1) ? "" : r.GetString(1);
                    idEquipo2 = r.IsDBNull(2) ? "" : r.GetString(2);
                    nomDisp2 = r.IsDBNull(3) ? "" : r.GetString(3);
                }
            }

            // Si hay ocupante, necesitamos saber a dónde va
            if (idOcupante > 0)
            {
                if (string.IsNullOrWhiteSpace(data.UbicacionOcupante))
                    return Ok(new
                    {
                        ok = false,
                        ocupada = true,
                        id_ocupante = idOcupante,
                        equipo_ocupante = idEquipo2,
                        dispositivo_ocupante = nomDisp2,
                        error = "La ubicación está ocupada. Indica dónde mover el dispositivo existente."
                    });

                var ubOcupante = data.UbicacionOcupante.Trim();

                // No se puede mover el ocupante a la ubicación original del dispositivo 1 si es distinta
                // (está permitido: el swap natural)

                // Mover el ocupante a su nueva ubicación
                using var mov2 = conn.CreateCommand();
                mov2.CommandText = "UPDATE public.mantenimientos_preventivos SET ubicacion=@u WHERE id=@id";
                mov2.Parameters.AddWithValue("u", ubOcupante);
                mov2.Parameters.AddWithValue("id", idOcupante);
                mov2.ExecuteNonQuery();

                // Registrar historial del ocupante
                RegistrarHistorialRecalendarizacion(conn, idOcupante, idEquipo2, nomDisp2,
                    ubicacionOcupanteAnterior, ubOcupante, usuario);
            }

            // 3. Mover el dispositivo principal
            using var mov1 = conn.CreateCommand();
            mov1.CommandText = "UPDATE public.mantenimientos_preventivos SET ubicacion=@u WHERE id=@id";
            mov1.Parameters.AddWithValue("u", nuevaUbicacion);
            mov1.Parameters.AddWithValue("id", data.IdDispositivo);
            mov1.ExecuteNonQuery();

            // Registrar historial del dispositivo principal
            RegistrarHistorialRecalendarizacion(conn, data.IdDispositivo, idEquipo1, nomDisp1,
                ubicacionAnterior, nuevaUbicacion, usuario);

            return Ok(new { ok = true, mensaje = "Recalendarización completada" });
        }
        catch (Exception ex) { return Ok(new { ok = false, error = ex.Message }); }
    }

    private void RegistrarHistorialRecalendarizacion(Npgsql.NpgsqlConnection conn,
        int idDispositivo, string idEquipo, string nomDisp,
        string ubAnterior, string ubNueva, string usuario)
    {
        try
        {
            var anterior = new { id_equipo = idEquipo, nombre_dispositivo = nomDisp, ubicacion = ubAnterior };
            var nuevo = new { id_equipo = idEquipo, nombre_dispositivo = nomDisp, ubicacion = ubNueva, tipo_cambio = "Recalendarización" };
            _auditoria.Registrar(idDispositivo, usuario, anterior, nuevo);
        }
        catch (Exception ex) { Console.WriteLine($"[Recalendarización historial] {ex.Message}"); }
    }

    // GET /PREVENTIVO/UBICACIONES_LIBRES — para el selector del modal de recalendarización
    [HttpGet("PREVENTIVO/UBICACIONES_TODAS")]
    public IActionResult ObtenerTodasUbicaciones()
    {
        using var conn = _db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT TRIM(ubicacion) AS ubicacion
            FROM public.mantenimientos_preventivos
            WHERE ubicacion IS NOT NULL AND TRIM(ubicacion)<>''
            GROUP BY TRIM(ubicacion)
            ORDER BY TRIM(ubicacion)
            """;
        var lista = new List<string>();
        using var r = cmd.ExecuteReader();
        while (r.Read()) lista.Add(r.GetString(0));
        return Ok(new { ubicaciones = lista });
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
                observaciones = data.Observaciones,
                requiere_correctivo = data.RequiereCorrectivo,
                verificado_por = (string?)null  // se llena después desde el panel de auditoría
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

    // ══════════════════════════════════════════════════════════════════════
    // GET /PREVENTIVOS/UBICACIONES
    // Devuelve todas las ubicaciones distintas con conteo de equipos,
    // desglose computo/laptops y progreso de PM P1.
    // ══════════════════════════════════════════════════════════════════════
    [HttpGet("PREVENTIVOS/UBICACIONES")]
    public IActionResult ObtenerUbicaciones()
    {
        using var conn = _db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT
                TRIM(ubicacion)                                          AS ubicacion,
                planta,
                COUNT(*)                                                 AS total_equipos,
                COUNT(*) FILTER (WHERE nombre_dispositivo IN (
                    'COMPUTADORA DE ESCRITORIO','UPS','IMPRESORA TERMICA')) AS total_computo,
                COUNT(*) FILTER (WHERE nombre_dispositivo = 'LAPTOP')   AS total_laptops,
                COUNT(*) FILTER (WHERE preventivo_digital IS NOT NULL)  AS con_pm_p1,
                COUNT(*) FILTER (WHERE preventivo_digital_p2 IS NOT NULL) AS con_pm_p2
            FROM public.mantenimientos_preventivos
            WHERE ubicacion IS NOT NULL AND TRIM(ubicacion) <> ''
              AND nombre_dispositivo IN (
                  'COMPUTADORA DE ESCRITORIO','LAPTOP','UPS','IMPRESORA TERMICA')
            GROUP BY TRIM(ubicacion), planta
            ORDER BY planta, TRIM(ubicacion)
            """;

        var lista = new List<object>();
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            lista.Add(new
            {
                ubicacion = r.GetString(0),
                planta = r.GetString(1),
                total_equipos = r.GetInt64(2),
                total_computo = r.GetInt64(3),
                total_laptops = r.GetInt64(4),
                con_pm_p1 = r.GetInt64(5),
                con_pm_p2 = r.GetInt64(6),
            });
        }

        return Ok(new { ubicaciones = lista });
    }

    // ══════════════════════════════════════════════════════════════════════
    // POST /PREVENTIVO/VERIFICAR/{id}
    // Marca verificado_por en el JSON del PM (P1 o P2). No requiere que
    // ya exista — si no hay PM registrado devuelve error.
    // Body: { "periodo": 1|2, "verificador": "NOMBRE" }
    // ══════════════════════════════════════════════════════════════════════
    [HttpPost("PREVENTIVO/VERIFICAR/{id:int}")]
    public IActionResult VerificarPm(int id, [FromBody] VerificarPmRequest data)
    {
        try
        {
            using var conn = _db.Open();

            // Leer el JSON actual del período solicitado
            using var sel = conn.CreateCommand();
            sel.CommandText = data.Periodo == 2
                ? "SELECT preventivo_digital_p2 FROM public.mantenimientos_preventivos WHERE id=@id"
                : "SELECT preventivo_digital   FROM public.mantenimientos_preventivos WHERE id=@id";
            sel.Parameters.AddWithValue("id", id);

            using (var selR = sel.ExecuteReader())
            {
                if (!selR.Read() || selR.IsDBNull(0))
                    return Ok(new { ok = false, error = "No hay PM registrado para este período" });
            }

            // Actualizar verificado_por dentro del JSONB con jsonb_set
            using var upd = conn.CreateCommand();
            if (data.Periodo == 2)
            {
                upd.CommandText = """
                    UPDATE public.mantenimientos_preventivos
                    SET preventivo_digital_p2 = jsonb_set(
                        preventivo_digital_p2,
                        '{verificado_por}',
                        to_jsonb(@v::text)
                    )
                    WHERE id=@id
                    """;
            }
            else
            {
                upd.CommandText = """
                    UPDATE public.mantenimientos_preventivos
                    SET preventivo_digital = jsonb_set(
                        preventivo_digital,
                        '{verificado_por}',
                        to_jsonb(@v::text)
                    )
                    WHERE id=@id
                    """;
            }
            upd.Parameters.AddWithValue("v", (data.Verificador ?? "").ToUpper().Trim());
            upd.Parameters.AddWithValue("id", id);
            upd.ExecuteNonQuery();

            return Ok(new { ok = true });
        }
        catch (Exception ex) { return Ok(new { ok = false, error = ex.Message }); }
    }

    // ══════════════════════════════════════════════════════════════════════
    // GET /PREVENTIVOS/AUDITORIA
    // Regla IATF: por cada planta+período, muestra = CEILING(total_sin_laptop * 0.01).
    // Los equipos de la muestra se eligen aleatoriamente del pool global de la planta.
    // ══════════════════════════════════════════════════════════════════════
    [HttpGet("PREVENTIVOS/AUDITORIA")]
    public IActionResult ObtenerAuditoria()
    {
        using var conn = _db.Open();

        // ── 1. Leer TODOS los equipos elegibles (con PM, sin laptops) ──────
        // Agrupamos por planta para calcular la muestra a nivel planta.
        using var cmdEq = conn.CreateCommand();
        cmdEq.CommandText = """
            SELECT
                id,
                TRIM(ubicacion)                                    AS ubicacion,
                planta,
                nombre_dispositivo,
                preventivo_digital IS NOT NULL                     AS tiene_p1,
                (preventivo_digital->>'verificado_por')            AS verificado_p1,
                preventivo_digital_p2 IS NOT NULL                  AS tiene_p2,
                (preventivo_digital_p2->>'verificado_por')         AS verificado_p2
            FROM public.mantenimientos_preventivos
            WHERE ubicacion IS NOT NULL AND TRIM(ubicacion) <> ''
              AND nombre_dispositivo IN (
                  'COMPUTADORA DE ESCRITORIO','UPS','IMPRESORA TERMICA')
              AND (preventivo_digital IS NOT NULL OR preventivo_digital_p2 IS NOT NULL)
            ORDER BY planta, TRIM(ubicacion), id
            """;

        // Registro por planta → lista de equipos elegibles
        var porPlanta = new Dictionary<string,
            List<(long id, string ub, string disp, bool tieneP1, string? vP1, bool tieneP2, string? vP2)>>(
                StringComparer.OrdinalIgnoreCase);

        // También construimos índice por ubicación para conteos
        var porUbicacion = new Dictionary<string,
            (string planta, List<(long id, string disp, bool tieneP1, string? vP1, bool tieneP2, string? vP2)> equipos)>();

        using (var rEq = cmdEq.ExecuteReader())
        {
            while (rEq.Read())
            {
                var id = rEq.GetInt64(0);
                var ub = rEq.GetString(1);
                var pl = rEq.IsDBNull(2) ? "" : rEq.GetString(2);
                var disp = rEq.IsDBNull(3) ? "" : rEq.GetString(3);
                var tp1 = rEq.GetBoolean(4);
                var vp1 = rEq.IsDBNull(5) ? null : rEq.GetString(5);
                var tp2 = rEq.GetBoolean(6);
                var vp2 = rEq.IsDBNull(7) ? null : rEq.GetString(7);

                // Índice por planta (para cálculo de muestra)
                if (!porPlanta.ContainsKey(pl)) porPlanta[pl] = new();
                porPlanta[pl].Add((id, ub, disp, tp1, vp1, tp2, vp2));

                // Índice por ubicación (para conteos de la card)
                if (!porUbicacion.ContainsKey(ub)) porUbicacion[ub] = (pl, new());
                porUbicacion[ub].equipos.Add((id, disp, tp1, vp1, tp2, vp2));
            }
        }

        // ── 2. Laptops: solo para conteos generales, nunca entran en muestra ─
        using var cmdLap = conn.CreateCommand();
        cmdLap.CommandText = """
            SELECT
                TRIM(ubicacion)                                    AS ubicacion,
                preventivo_digital IS NOT NULL                     AS tiene_p1,
                (preventivo_digital->>'verificado_por')            AS verificado_p1,
                preventivo_digital_p2 IS NOT NULL                  AS tiene_p2,
                (preventivo_digital_p2->>'verificado_por')         AS verificado_p2
            FROM public.mantenimientos_preventivos
            WHERE ubicacion IS NOT NULL AND TRIM(ubicacion) <> ''
              AND nombre_dispositivo = 'LAPTOP'
              AND (preventivo_digital IS NOT NULL OR preventivo_digital_p2 IS NOT NULL)
            """;

        var laptopConteos = new Dictionary<string, (int verP1, int pendP1, int verP2, int pendP2)>();
        using (var rLap = cmdLap.ExecuteReader())
        {
            while (rLap.Read())
            {
                var ub = rLap.GetString(0);
                var tp1 = rLap.GetBoolean(1);
                var vp1 = rLap.IsDBNull(2) ? null : rLap.GetString(2);
                var tp2 = rLap.GetBoolean(3);
                var vp2 = rLap.IsDBNull(4) ? null : rLap.GetString(4);
                if (!laptopConteos.ContainsKey(ub)) laptopConteos[ub] = (0, 0, 0, 0);
                var c = laptopConteos[ub];
                if (tp1) { if (!string.IsNullOrWhiteSpace(vp1)) c.verP1++; else c.pendP1++; }
                if (tp2) { if (!string.IsNullOrWhiteSpace(vp2)) c.verP2++; else c.pendP2++; }
                laptopConteos[ub] = c;
            }
        }

        // ── 3. Calcular muestra IATF a nivel PLANTA (no por ubicación) ─────
        // Regla: muestra = CEILING(total_sin_laptop_con_PM_en_planta * 0.01)
        // Si el resultado es < 1 (ej: 0 equipos), muestra = 0 (no hay PM).
        // Con cualquier cantidad ≥ 1 equipo, CEILING garantiza mínimo 1.
        var rng = new Random();

        // muestraPorPlanta: planta → (idsP1[], idsP2[])
        var muestraPorPlanta = new Dictionary<string, (HashSet<long> p1, HashSet<long> p2)>(
            StringComparer.OrdinalIgnoreCase);

        foreach (var (planta, equipos) in porPlanta)
        {
            // Pool P1: equipos de esta planta que tienen PM en P1
            var poolP1 = equipos.Where(e => e.tieneP1).ToList();
            var tamP1 = poolP1.Count > 0 ? (int)Math.Ceiling(poolP1.Count * 0.01) : 0;
            var selP1 = poolP1.OrderBy(_ => rng.Next()).Take(tamP1).Select(e => e.id).ToHashSet();

            // Pool P2: equipos de esta planta que tienen PM en P2
            var poolP2 = equipos.Where(e => e.tieneP2).ToList();
            var tamP2 = poolP2.Count > 0 ? (int)Math.Ceiling(poolP2.Count * 0.01) : 0;
            var selP2 = poolP2.OrderBy(_ => rng.Next()).Take(tamP2).Select(e => e.id).ToHashSet();

            muestraPorPlanta[planta] = (selP1, selP2);
        }

        // ── 4. Construir respuesta por ubicación ───────────────────────────
        // Si no hay ningún equipo con PM digital en ninguna planta → calendario no iniciado
        if (!porPlanta.Any())
            return Ok(new { sin_calendario = true, ubicaciones = Array.Empty<object>() });

        var lista = new List<object>();

        foreach (var (ub, (planta, equipos)) in porUbicacion
            .OrderBy(x => x.Value.planta).ThenBy(x => x.Key))
        {
            // IDs de la muestra de la planta que pertenecen a ESTA ubicación
            var (muestraP1Plant, muestraP2Plant) = muestraPorPlanta.TryGetValue(planta, out var m)
                ? m : (new HashSet<long>(), new HashSet<long>());

            var idsP1EnUb = equipos.Where(e => muestraP1Plant.Contains(e.id)).Select(e => e.id).ToArray();
            var idsP2EnUb = equipos.Where(e => muestraP2Plant.Contains(e.id)).Select(e => e.id).ToArray();

            // ★ Solo incluir ubicaciones donde cayó al menos un equipo de la muestra IATF.
            // Ubicaciones donde la muestra no seleccionó ningún equipo no deben aparecer.
            if (idsP1EnUb.Length == 0 && idsP2EnUb.Length == 0) continue;

            // Conteos generales sin laptops
            int verP1 = equipos.Count(e => e.tieneP1 && !string.IsNullOrWhiteSpace(e.vP1));
            int pendP1 = equipos.Count(e => e.tieneP1 && string.IsNullOrWhiteSpace(e.vP1));
            int verP2 = equipos.Count(e => e.tieneP2 && !string.IsNullOrWhiteSpace(e.vP2));
            int pendP2 = equipos.Count(e => e.tieneP2 && string.IsNullOrWhiteSpace(e.vP2));
            int totalConPm = equipos.Count;

            // Sumar laptops al conteo global
            if (laptopConteos.TryGetValue(ub, out var lc))
            {
                totalConPm += lc.verP1 + lc.pendP1 + lc.verP2 + lc.pendP2;
                verP1 += lc.verP1; pendP1 += lc.pendP1;
                verP2 += lc.verP2; pendP2 += lc.pendP2;
            }

            int mP1Ver = equipos.Count(e => muestraP1Plant.Contains(e.id) && !string.IsNullOrWhiteSpace(e.vP1));
            int mP2Ver = equipos.Count(e => muestraP2Plant.Contains(e.id) && !string.IsNullOrWhiteSpace(e.vP2));

            lista.Add(new
            {
                ubicacion = ub,
                planta,
                total_con_pm = totalConPm,
                verificados_p1 = verP1,
                pendientes_p1 = pendP1,
                verificados_p2 = verP2,
                pendientes_p2 = pendP2,
                // IDs de la muestra IATF que caen en esta ubicación
                muestra_p1_ids = idsP1EnUb,
                muestra_p1_total = muestraP1Plant.Count,   // total de la planta (informativo)
                muestra_p1_verificados = mP1Ver,
                muestra_p2_ids = idsP2EnUb,
                muestra_p2_total = muestraP2Plant.Count,   // total de la planta (informativo)
                muestra_p2_verificados = mP2Ver,
            });
        }

        // Si la lista quedó vacía (ningún equipo con PM digital) → sin calendario
        if (!lista.Any())
            return Ok(new { sin_calendario = true, ubicaciones = Array.Empty<object>() });

        return Ok(new { sin_calendario = false, ubicaciones = lista });
    }

    // ══════════════════════════════════════════════════════════════════════
    // GET /PREVENTIVOS/AUDITORIA/DETALLE?ubicacion=X&ids_p1=1,2&ids_p2=3
    // Devuelve solo los equipos de la muestra IATF para esa ubicación.
    // ids_p1 / ids_p2: IDs separados por coma (calculados en ObtenerAuditoria).
    // ══════════════════════════════════════════════════════════════════════
    [HttpGet("PREVENTIVOS/AUDITORIA/DETALLE")]
    public IActionResult ObtenerAuditoriaDetalle(
        [FromQuery] string ubicacion,
        [FromQuery(Name = "ids_p1")] string? idsP1Raw,
        [FromQuery(Name = "ids_p2")] string? idsP2Raw)
    {
        // Parsear los IDs de la muestra enviados por el frontend
        static HashSet<long> ParseIds(string? raw) =>
            (raw ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries)
                       .Select(s => long.TryParse(s.Trim(), out var n) ? n : -1L)
                       .Where(n => n > 0)
                       .ToHashSet();

        var setP1 = ParseIds(idsP1Raw);
        var setP2 = ParseIds(idsP2Raw);
        var todosIds = setP1.Union(setP2).ToList();

        if (!todosIds.Any())
            return Ok(new { equipos = Array.Empty<object>() });

        using var conn = _db.Open();
        using var cmd = conn.CreateCommand();

        // Construir cláusula IN con parámetros
        var inParams = string.Join(",", todosIds.Select((_, i) => $"@id{i}"));
        cmd.CommandText = $"""
            SELECT
                id,
                id_equipo,
                nombre_dispositivo,
                preventivo_digital IS NOT NULL                AS tiene_p1,
                preventivo_digital->>'usuario'                AS realizado_por_p1,
                preventivo_digital->>'fecha'                  AS fecha_p1,
                preventivo_digital->>'verificado_por'         AS verificado_por_p1,
                preventivo_digital_p2 IS NOT NULL             AS tiene_p2,
                preventivo_digital_p2->>'usuario'             AS realizado_por_p2,
                preventivo_digital_p2->>'fecha'               AS fecha_p2,
                preventivo_digital_p2->>'verificado_por'      AS verificado_por_p2
            FROM public.mantenimientos_preventivos
            WHERE id IN ({inParams})
              AND TRIM(LOWER(ubicacion)) = TRIM(LOWER(@u))
            ORDER BY nombre_dispositivo, id_equipo
            """;

        for (int i = 0; i < todosIds.Count; i++)
            cmd.Parameters.AddWithValue($"id{i}", todosIds[i]);
        cmd.Parameters.AddWithValue("u", (ubicacion ?? "").Trim());

        var lista = new List<object>();
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            var id = r.GetInt64(0);
            var tieneP1 = r.GetBoolean(3);
            var tieneP2 = r.GetBoolean(7);

            lista.Add(new
            {
                id,
                id_equipo = r.IsDBNull(1) ? "" : r.GetString(1),
                dispositivo = r.IsDBNull(2) ? "" : r.GetString(2),
                // P1: solo incluir si el ID está en la muestra P1
                tiene_p1 = tieneP1 && setP1.Contains(id),
                realizado_p1 = r.IsDBNull(4) ? null : r.GetString(4),
                fecha_p1 = r.IsDBNull(5) ? null : r.GetString(5),
                verificado_p1 = r.IsDBNull(6) ? null : r.GetString(6),
                // P2: solo incluir si el ID está en la muestra P2
                tiene_p2 = tieneP2 && setP2.Contains(id),
                realizado_p2 = r.IsDBNull(8) ? null : r.GetString(8),
                fecha_p2 = r.IsDBNull(9) ? null : r.GetString(9),
                verificado_p2 = r.IsDBNull(10) ? null : r.GetString(10),
                // Indicadores para el frontend
                es_muestra_p1 = setP1.Contains(id),
                es_muestra_p2 = setP2.Contains(id),
            });
        }

        return Ok(new { equipos = lista });
    }

    // ── Modelo recalendarización ──────────────────────────────────────────────
} // fin PreventivoController

public class RecalendarizarRequest
{
    public int IdDispositivo { get; set; }
    public string? NuevaUbicacion { get; set; }
    public string? UbicacionOcupante { get; set; }  // dónde va el que ya estaba allí
    public string? Usuario { get; set; }
}

public class VerificarPmRequest
{
    public int Periodo { get; set; }       // 1 o 2
    public string? Verificador { get; set; } // nombre del auditor
}