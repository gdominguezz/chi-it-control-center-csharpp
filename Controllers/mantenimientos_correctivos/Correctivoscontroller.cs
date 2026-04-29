using ChiIT.Data;
using ChiIT.Services;
using Microsoft.AspNetCore.Mvc;
using Npgsql;
using System.Text.Json;

namespace ChiIT.Controllers;

[ApiController]
public class CorrectivoController : ControllerBase
{
    private readonly DbConnectionPool _db;
    private readonly AuditoriaServiceCorrectivos _auditoria;
    private readonly ExcelService _excel;
    private readonly string _pdfDir;

    public CorrectivoController(DbConnectionPool db, AuditoriaServiceCorrectivos auditoria,
                                ExcelService excel, IConfiguration config)
    {
        _db = db;
        _auditoria = auditoria;
        _excel = excel;
        _pdfDir = config["AppSettings:PdfDirCorrectivos"] ?? "PDF_DATABASE/CORRECTIVOS";
        Directory.CreateDirectory(_pdfDir);
    }

    // ── Helper: usuario desde cookie o header ─────────────────────────────
    private string ObtenerUsuario() =>
        Request.Cookies["usuario"]
        ?? Request.Headers["X-Usuario"].FirstOrDefault()
        ?? "SISTEMA";

    // ══════════════════════════════════════════════════════════════════════
    // GET /CORRECTIVOS
    // Parámetros de filtro (todos opcionales, ILIKE): STATUS, FOLIO, PLANTA,
    // LINEA_PERSONA, EQUIPO, MARCA, MODELO, NUMERO_SERIE, DESCRIPCION_FALLA,
    // ACCESORIO_SOLICITADO, FECHA_SOLICITUD, REPORTE_ELABORADO_POR,
    // TIPO_OBSERVACION, VENCIMIENTO_DIAS, FECHA_CONTEO_ACTUAL,
    // FECHA_LIMITE_CIERRE, CATEGORIA_CORRECTIVO, REFACCION_ACCESORIO_COMPRA,
    // FECHA_LLEGADA_REFACCION, FECHA_REPARACION, QUIEN_REALIZO_REPARACION,
    // VALIDACION_FUNCIONAMIENTO, DESCRIPCION_REPARACION, OBSERVACIONES, OC_FACTURA
    // page, limit
    // ══════════════════════════════════════════════════════════════════════
    [HttpGet("CORRECTIVOS")]
    public IActionResult ObtenerCorrectivos([FromQuery] FiltrosCorrectivo f)
    {
        var where = "WHERE 1=1";
        var parms = new List<NpgsqlParameter>();
        int pIdx = 1;

        // Columnas que admiten filtro de texto (ILIKE)
        var filtrosTexto = new (string col, string? val)[]
        {
            ("status",                    f.STATUS),
            ("folio",                     f.FOLIO),
            ("planta",                    f.PLANTA),
            ("linea_persona",             f.LINEA_PERSONA),
            ("equipo",                    f.EQUIPO),
            ("marca",                     f.MARCA),
            ("modelo",                    f.MODELO),
            ("numero_serie",              f.NUMERO_SERIE),
            ("descripcion_falla",         f.DESCRIPCION_FALLA),
            ("accesorio_solicitado",      f.ACCESORIO_SOLICITADO),
            ("reporte_elaborado_por",     f.REPORTE_ELABORADO_POR),
            ("tipo_observacion",          f.TIPO_OBSERVACION),
            ("categoria_correctivo",      f.CATEGORIA_CORRECTIVO),
            ("refaccion_accesorio_compra",f.REFACCION_ACCESORIO_COMPRA),
            ("quien_realizo_reparacion",  f.QUIEN_REALIZO_REPARACION),
            ("validacion_funcionamiento", f.VALIDACION_FUNCIONAMIENTO),
            ("descripcion_reparacion",    f.DESCRIPCION_REPARACION),
            ("observaciones",             f.OBSERVACIONES),
            ("oc_factura",                f.OC_FACTURA),
        };

        foreach (var (col, val) in filtrosTexto)
        {
            if (!string.IsNullOrWhiteSpace(val))
            {
                where += $" AND {col}::text ILIKE @p{pIdx}";
                parms.Add(new NpgsqlParameter($"p{pIdx++}", $"%{val}%"));
            }
        }

        // Filtro entero exacto para vencimiento_dias
        if (f.VENCIMIENTO_DIAS.HasValue)
        {
            where += $" AND vencimiento_dias = @p{pIdx}";
            parms.Add(new NpgsqlParameter($"p{pIdx++}", f.VENCIMIENTO_DIAS.Value));
        }

        // Filtros de fecha (LIKE sobre el texto de la columna date)
        var filtrosFecha = new (string col, string? val)[]
        {
            ("fecha_solicitud",        f.FECHA_SOLICITUD),
            ("fecha_conteo_actual",    f.FECHA_CONTEO_ACTUAL),
            ("fecha_limite_cierre",    f.FECHA_LIMITE_CIERRE),
            ("fecha_llegada_refaccion",f.FECHA_LLEGADA_REFACCION),
            ("fecha_reparacion",       f.FECHA_REPARACION),
        };

        foreach (var (col, val) in filtrosFecha)
        {
            if (!string.IsNullOrWhiteSpace(val))
            {
                where += $" AND {col}::text ILIKE @p{pIdx}";
                parms.Add(new NpgsqlParameter($"p{pIdx++}", $"%{val}%"));
            }
        }

        using var conn = _db.Open();

        // Clonar parámetros — Npgsql no permite reusar el mismo NpgsqlParameter en dos comandos
        NpgsqlParameter[] ClonarParams() =>
            parms.Select(p => new NpgsqlParameter(p.ParameterName, p.Value)).ToArray();

        // Total
        using var cntCmd = conn.CreateCommand();
        cntCmd.CommandText = $"SELECT COUNT(*) FROM public.mantenimientos_correctivos {where}";
        cntCmd.Parameters.AddRange(ClonarParams());
        var total = Convert.ToInt64(cntCmd.ExecuteScalar()!);

        // Datos paginados
        int offset = (f.Page - 1) * f.Limit;
        parms.Add(new NpgsqlParameter($"p{pIdx++}", f.Limit));
        parms.Add(new NpgsqlParameter($"p{pIdx}", offset));

        using var cmd = conn.CreateCommand();
        cmd.CommandText = $"""
            SELECT
                id                        AS "ID",
                status                    AS "STATUS",
                folio                     AS "FOLIO",
                planta                    AS "PLANTA",
                linea_persona             AS "LINEA_PERSONA",
                equipo                    AS "EQUIPO",
                marca                     AS "MARCA",
                modelo                    AS "MODELO",
                numero_serie              AS "NUMERO_SERIE",
                descripcion_falla         AS "DESCRIPCION_FALLA",
                accesorio_solicitado      AS "ACCESORIO_SOLICITADO",
                fecha_solicitud           AS "FECHA_SOLICITUD",
                reporte_elaborado_por     AS "REPORTE_ELABORADO_POR",
                tipo_observacion          AS "TIPO_OBSERVACION",
                vencimiento_dias          AS "VENCIMIENTO_DIAS",
                fecha_conteo_actual       AS "FECHA_CONTEO_ACTUAL",
                fecha_limite_cierre       AS "FECHA_LIMITE_CIERRE",
                categoria_correctivo      AS "CATEGORIA_CORRECTIVO",
                refaccion_accesorio_compra AS "REFACCION_ACCESORIO_COMPRA",
                fecha_llegada_refaccion   AS "FECHA_LLEGADA_REFACCION",
                fecha_reparacion          AS "FECHA_REPARACION",
                quien_realizo_reparacion  AS "QUIEN_REALIZO_REPARACION",
                validacion_funcionamiento AS "VALIDACION_FUNCIONAMIENTO",
                descripcion_reparacion    AS "DESCRIPCION_REPARACION",
                observaciones             AS "OBSERVACIONES",
                oc_factura                AS "OC_FACTURA",
                CASE WHEN pdf IS NOT NULL THEN true ELSE false END AS "TIENE_PDF"
            FROM public.mantenimientos_correctivos
            {where}
            ORDER BY id DESC
            LIMIT @p{pIdx - 1} OFFSET @p{pIdx}
            """;
        cmd.Parameters.AddRange(ClonarParams());

        using var reader = cmd.ExecuteReader();
        var data = new List<Dictionary<string, object?>>();
        while (reader.Read())
        {
            var row = new Dictionary<string, object?>();
            for (int i = 0; i < reader.FieldCount; i++)
            {
                var name = reader.GetName(i);
                var val = reader.IsDBNull(i) ? null : reader.GetValue(i);

                // Serializar fechas como string corto para que el JS las muestre bien
                if (val is DateTime dt)
                    val = dt.ToString("yyyy-MM-dd");

                row[name] = val;
            }
            data.Add(row);
        }

        return Ok(new { data, total, page = f.Page });
    }

    // ══════════════════════════════════════════════════════════════════════
    // GET /CORRECTIVO/{id}  — obtener un registro por ID
    // ══════════════════════════════════════════════════════════════════════
    [HttpGet("CORRECTIVO/{id:int}")]
    public IActionResult ObtenerPorId(int id)
    {
        try
        {
            using var conn = _db.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = """
                SELECT
                    id                        AS "ID",
                    status                    AS "STATUS",
                    folio                     AS "FOLIO",
                    planta                    AS "PLANTA",
                    linea_persona             AS "LINEA_PERSONA",
                    equipo                    AS "EQUIPO",
                    marca                     AS "MARCA",
                    modelo                    AS "MODELO",
                    numero_serie              AS "NUMERO_SERIE",
                    descripcion_falla         AS "DESCRIPCION_FALLA",
                    accesorio_solicitado      AS "ACCESORIO_SOLICITADO",
                    fecha_solicitud           AS "FECHA_SOLICITUD",
                    reporte_elaborado_por     AS "REPORTE_ELABORADO_POR",
                    tipo_observacion          AS "TIPO_OBSERVACION",
                    vencimiento_dias          AS "VENCIMIENTO_DIAS",
                    fecha_conteo_actual       AS "FECHA_CONTEO_ACTUAL",
                    fecha_limite_cierre       AS "FECHA_LIMITE_CIERRE",
                    categoria_correctivo      AS "CATEGORIA_CORRECTIVO",
                    refaccion_accesorio_compra AS "REFACCION_ACCESORIO_COMPRA",
                    fecha_llegada_refaccion   AS "FECHA_LLEGADA_REFACCION",
                    fecha_reparacion          AS "FECHA_REPARACION",
                    quien_realizo_reparacion  AS "QUIEN_REALIZO_REPARACION",
                    validacion_funcionamiento AS "VALIDACION_FUNCIONAMIENTO",
                    descripcion_reparacion    AS "DESCRIPCION_REPARACION",
                    observaciones             AS "OBSERVACIONES",
                    oc_factura                AS "OC_FACTURA"
                FROM public.mantenimientos_correctivos
                WHERE id = @id
                """;
            cmd.Parameters.AddWithValue("id", id);

            using var reader = cmd.ExecuteReader();
            if (!reader.Read())
                return NotFound(new { error = "Registro no encontrado" });

            var row = new Dictionary<string, object?>();
            for (int i = 0; i < reader.FieldCount; i++)
            {
                var name = reader.GetName(i);
                var val = reader.IsDBNull(i) ? null : reader.GetValue(i);
                if (val is DateTime dt) val = dt.ToString("yyyy-MM-dd");
                row[name] = val;
            }
            return Ok(row);
        }
        catch (Exception ex) { return Ok(new { error = ex.Message }); }
    }

    // ══════════════════════════════════════════════════════════════════════
    // POST /CORRECTIVO  — crear nuevo registro
    // ══════════════════════════════════════════════════════════════════════
    [HttpPost("CORRECTIVO")]
    public IActionResult Crear([FromBody] CorrectivoRequest data)
    {
        try
        {
            using var conn = _db.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = """
                INSERT INTO public.mantenimientos_correctivos
                (
                    status, folio, planta, linea_persona, equipo, marca, modelo,
                    numero_serie, descripcion_falla, accesorio_solicitado,
                    fecha_solicitud, reporte_elaborado_por, tipo_observacion,
                    vencimiento_dias, fecha_conteo_actual,
                    fecha_limite_cierre, categoria_correctivo,
                    refaccion_accesorio_compra, fecha_llegada_refaccion,
                    fecha_reparacion, quien_realizo_reparacion,
                    validacion_funcionamiento, descripcion_reparacion,
                    observaciones, oc_factura
                )
                VALUES
                (
                    @status, @folio, @planta, @linea, @equipo, @marca, @modelo,
                    @serie, @falla, @accesorio,
                    @fsol, @reporte, @tobservacion,
                    @vencimiento, @fconteo,
                    @flimite, @categoria,
                    @refaccion, @fllegada,
                    @freparacion, @quien,
                    @validacion, @dreparacion,
                    @observaciones, @oc
                )
                RETURNING id
                """;

            AgregarParametros(cmd, data);

            var newId = cmd.ExecuteScalar();
            return Ok(new { id = newId });
        }
        catch (Exception ex) { return Ok(new { error = ex.Message }); }
    }

    // ══════════════════════════════════════════════════════════════════════
    // PUT /CORRECTIVO/{id}  — editar registro existente + auditoría
    // ══════════════════════════════════════════════════════════════════════
    [HttpPut("CORRECTIVO/{id:int}")]
    public IActionResult Editar(int id, [FromBody] CorrectivoRequest data, [FromQuery] string? usuario)
    {
        try
        {
            var usr = Request.Cookies["usuario"]
                   ?? Request.Headers["X-Usuario"].FirstOrDefault()
                   ?? usuario ?? "SISTEMA";

            using var conn = _db.Open();

            // ── Leer registro anterior ──
            var anterior = LeerRegistro(conn, id);
            if (anterior == null) return Ok(new { error = "Registro no encontrado" });

            // ── Actualizar ──
            using var upd = conn.CreateCommand();
            upd.CommandText = """
                UPDATE public.mantenimientos_correctivos SET
                    status                     = @status,
                    folio                      = @folio,
                    planta                     = @planta,
                    linea_persona              = @linea,
                    equipo                     = @equipo,
                    marca                      = @marca,
                    modelo                     = @modelo,
                    numero_serie               = @serie,
                    descripcion_falla          = @falla,
                    accesorio_solicitado       = @accesorio,
                    fecha_solicitud            = @fsol,
                    reporte_elaborado_por      = @reporte,
                    tipo_observacion           = @tobservacion,
                    vencimiento_dias           = @vencimiento,
                    fecha_conteo_actual        = @fconteo,
                    fecha_limite_cierre        = @flimite,
                    categoria_correctivo       = @categoria,
                    refaccion_accesorio_compra = @refaccion,
                    fecha_llegada_refaccion    = @fllegada,
                    fecha_reparacion           = @freparacion,
                    quien_realizo_reparacion   = @quien,
                    validacion_funcionamiento  = @validacion,
                    descripcion_reparacion     = @dreparacion,
                    observaciones              = @observaciones,
                    oc_factura                 = @oc
                WHERE id = @id
                """;
            AgregarParametros(upd, data);
            upd.Parameters.AddWithValue("id", id);
            upd.ExecuteNonQuery();

            // ── Leer estado REAL post-UPDATE (igual que PreventivoController) ──
            var nuevo = LeerRegistro(conn, id)!;

            // ── Registrar auditoría mediante AuditoriaService ──
            _auditoria.RegistrarCorrectivo((long)id, usr, anterior, nuevo);

            return Ok(new { mensaje = "ACTUALIZADO" });
        }
        catch (Exception ex) { return Ok(new { error = ex.Message }); }
    }

    // ══════════════════════════════════════════════════════════════════════
    // DELETE /CORRECTIVO/{id}
    // ══════════════════════════════════════════════════════════════════════
    [HttpDelete("CORRECTIVO/{id:int}")]
    public IActionResult Eliminar(int id)
    {
        try
        {
            var usuario = ObtenerUsuario();

            using var conn = _db.Open();

            // Solo ADMIN puede eliminar
            using var chk = conn.CreateCommand();
            chk.CommandText = "SELECT rol FROM public.usuarios WHERE usuario=@u";
            chk.Parameters.AddWithValue("u", usuario);
            var rol = chk.ExecuteScalar()?.ToString();
            if (rol != "ADMIN")
                return Ok(new { error = "No tienes permiso para eliminar" });

            using var del = conn.CreateCommand();
            del.CommandText = "DELETE FROM public.mantenimientos_correctivos WHERE id=@id";
            del.Parameters.AddWithValue("id", id);
            del.ExecuteNonQuery();

            return Ok(new { ok = true });
        }
        catch (Exception ex) { return Ok(new { error = ex.Message }); }
    }

    // ══════════════════════════════════════════════════════════════════════
    // GET /CORRECTIVOS/{id}/HISTORIAL
    // ══════════════════════════════════════════════════════════════════════
    [HttpGet("CORRECTIVOS/{id:int}/HISTORIAL")]
    public IActionResult ObtenerHistorial(int id)
    {
        try
        {
            using var conn = _db.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = """
            SELECT id, fecha_cambio, usuario, registro_anterior, registro_nuevo
            FROM public.auditoria_correctivos
            WHERE registro_id = @id
            ORDER BY fecha_cambio DESC
            """;
            cmd.Parameters.AddWithValue("id", (long)id);  // ← cast a long para match con bigint

            static object SafeJson(string? raw)
            {
                if (string.IsNullOrWhiteSpace(raw)) return new { };
                try { return JsonSerializer.Deserialize<object>(raw)!; }
                catch { return new { _raw = raw }; }
            }

            var historial = new List<object>();
            using var r = cmd.ExecuteReader();
            while (r.Read())
            {
                historial.Add(new
                {
                    id = r.GetInt64(0),   // ← era GetInt32
                    fecha = r.IsDBNull(1) ? null : r.GetDateTime(1).ToString("o"),
                    usuario = r.IsDBNull(2) ? null : r.GetString(2),
                    registro_anterior = SafeJson(r.IsDBNull(3) ? null : r.GetString(3)),
                    registro_nuevo = SafeJson(r.IsDBNull(4) ? null : r.GetString(4)),
                });
            }
            return Ok(new { historial });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message, tipo = ex.GetType().Name });
        }
    }
    // ══════════════════════════════════════════════════════════════════════
    // PDF  — subir / ver / eliminar
    // ══════════════════════════════════════════════════════════════════════
    [HttpPost("CORRECTIVO/PDF/{id:int}")]
    public async Task<IActionResult> SubirPdf(int id, IFormFile file)
    {
        try
        {
            var path = Path.Combine(_pdfDir, $"{id}.pdf");
            await using var fs = System.IO.File.Create(path);
            await file.CopyToAsync(fs);

            using var conn = _db.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "UPDATE public.mantenimientos_correctivos SET pdf=@p WHERE id=@id";
            cmd.Parameters.AddWithValue("p", path);
            cmd.Parameters.AddWithValue("id", id);
            cmd.ExecuteNonQuery();

            return Ok(new { mensaje = "PDF subido" });
        }
        catch (Exception ex) { return Ok(new { error = ex.Message }); }
    }

    [HttpGet("CORRECTIVO/PDF/{id:int}")]
    public IActionResult ObtenerPdf(int id)
    {
        var path = Path.Combine(_pdfDir, $"{id}.pdf");
        if (!System.IO.File.Exists(path))
            return NotFound(new { error = "PDF no encontrado" });
        return PhysicalFile(Path.GetFullPath(path), "application/pdf");
    }

    [HttpDelete("CORRECTIVO/PDF/{id:int}")]
    public IActionResult EliminarPdf(int id)
    {
        try
        {
            var path = Path.Combine(_pdfDir, $"{id}.pdf");
            if (System.IO.File.Exists(path)) System.IO.File.Delete(path);

            using var conn = _db.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "UPDATE public.mantenimientos_correctivos SET pdf=NULL WHERE id=@id";
            cmd.Parameters.AddWithValue("id", id);
            cmd.ExecuteNonQuery();

            return Ok(new { mensaje = "PDF eliminado" });
        }
        catch (Exception ex) { return Ok(new { error = ex.Message }); }
    }

    // ══════════════════════════════════════════════════════════════════════
    // EXPORTAR EXCEL
    // ══════════════════════════════════════════════════════════════════════

    // GET /CORRECTIVOS/EXPORTAR?<filtros>  — solo columnas visibles, filtrado
    [HttpGet("CORRECTIVOS/EXPORTAR")]
    public IActionResult ExportarFiltrado([FromQuery] FiltrosCorrectivo f)
    {
        var (where, parms) = ConstruirWhere(f);

        using var conn = _db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $"""
            SELECT
                id, status, folio, planta, linea_persona, equipo, marca, modelo,
                numero_serie, descripcion_falla, accesorio_solicitado,
                fecha_solicitud, reporte_elaborado_por, tipo_observacion,
                vencimiento_dias, fecha_conteo_actual,
                fecha_limite_cierre, categoria_correctivo,
                refaccion_accesorio_compra, fecha_llegada_refaccion,
                fecha_reparacion, quien_realizo_reparacion,
                validacion_funcionamiento, descripcion_reparacion,
                observaciones, oc_factura
            FROM public.mantenimientos_correctivos
            {where}
            ORDER BY id DESC
            """;
        cmd.Parameters.AddRange(parms.ToArray());
        using var reader = cmd.ExecuteReader();
        var bytes = _excel.GenerarExcel(reader, "Correctivos");
        return File(bytes,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            "correctivos_filtrado.xlsx");
    }

    // GET /CORRECTIVOS/EXPORTAR_TODO
    [HttpGet("CORRECTIVOS/EXPORTAR_TODO")]
    public IActionResult ExportarTodo()
    {
        using var conn = _db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT
                id, status, folio, planta, linea_persona, equipo, marca, modelo,
                numero_serie, descripcion_falla, accesorio_solicitado,
                fecha_solicitud, reporte_elaborado_por, tipo_observacion,
                vencimiento_dias, fecha_conteo_actual,
                fecha_limite_cierre, categoria_correctivo,
                refaccion_accesorio_compra, fecha_llegada_refaccion,
                fecha_reparacion, quien_realizo_reparacion,
                validacion_funcionamiento, descripcion_reparacion,
                observaciones, oc_factura
            FROM public.mantenimientos_correctivos
            ORDER BY id DESC
            """;
        using var reader = cmd.ExecuteReader();
        var bytes = _excel.GenerarExcel(reader, "Correctivos");
        return File(bytes,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            "correctivos_todo.xlsx");
    }

    // GET /CORRECTIVOS/EXPORTAR_ANIO?anio=2026
    [HttpGet("CORRECTIVOS/EXPORTAR_ANIO")]
    public IActionResult ExportarAnio([FromQuery] int anio)
    {
        using var conn = _db.Open();
        using var cmd = conn.CreateCommand();
        // Filtra por el año de fecha_solicitud (o de creación del registro)
        cmd.CommandText = """
            SELECT
                id, status, folio, planta, linea_persona, equipo, marca, modelo,
                numero_serie, descripcion_falla, accesorio_solicitado,
                fecha_solicitud, reporte_elaborado_por, tipo_observacion,
                vencimiento_dias, fecha_conteo_actual,
                fecha_limite_cierre, categoria_correctivo,
                refaccion_accesorio_compra, fecha_llegada_refaccion,
                fecha_reparacion, quien_realizo_reparacion,
                validacion_funcionamiento, descripcion_reparacion,
                observaciones, oc_factura
            FROM public.mantenimientos_correctivos
            WHERE EXTRACT(YEAR FROM fecha_solicitud) = @anio
            ORDER BY id DESC
            """;
        cmd.Parameters.AddWithValue("anio", anio);
        using var reader = cmd.ExecuteReader();
        var bytes = _excel.GenerarExcel(reader, "Correctivos");
        return File(bytes,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            $"correctivos_{anio}.xlsx");
    }

    // ══════════════════════════════════════════════════════════════════════
    // HELPERS PRIVADOS
    // ══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Agrega todos los parámetros de datos al comando (INSERT y UPDATE comparten la misma lista).
    /// </summary>
    private static void AgregarParametros(NpgsqlCommand cmd, CorrectivoRequest d)
    {
        static object N(string? v) => string.IsNullOrWhiteSpace(v) ? DBNull.Value : v.Trim();
        static object NDate(string? v) =>
            DateOnly.TryParse(v, out var dd) ? (object)dd : DBNull.Value;
        static object NInt(int? v) => v.HasValue ? (object)v.Value : DBNull.Value;

        cmd.Parameters.AddWithValue("status", N(d.STATUS));
        cmd.Parameters.AddWithValue("folio", N(d.FOLIO));
        cmd.Parameters.AddWithValue("planta", N(d.PLANTA));
        cmd.Parameters.AddWithValue("linea", N(d.LINEA_PERSONA));
        cmd.Parameters.AddWithValue("equipo", N(d.EQUIPO));
        cmd.Parameters.AddWithValue("marca", N(d.MARCA));
        cmd.Parameters.AddWithValue("modelo", N(d.MODELO));
        cmd.Parameters.AddWithValue("serie", N(d.NUMERO_SERIE));
        cmd.Parameters.AddWithValue("falla", N(d.DESCRIPCION_FALLA));
        cmd.Parameters.AddWithValue("accesorio", N(d.ACCESORIO_SOLICITADO));
        cmd.Parameters.AddWithValue("fsol", NDate(d.FECHA_SOLICITUD));
        cmd.Parameters.AddWithValue("reporte", N(d.REPORTE_ELABORADO_POR));
        cmd.Parameters.AddWithValue("tobservacion", N(d.TIPO_OBSERVACION));
        cmd.Parameters.AddWithValue("vencimiento", NInt(d.VENCIMIENTO_DIAS));   // INTEGER
        cmd.Parameters.AddWithValue("fconteo", NDate(d.FECHA_CONTEO_ACTUAL));
        cmd.Parameters.AddWithValue("flimite", NDate(d.FECHA_LIMITE_CIERRE));
        cmd.Parameters.AddWithValue("categoria", N(d.CATEGORIA_CORRECTIVO));
        cmd.Parameters.AddWithValue("refaccion", N(d.REFACCION_ACCESORIO_COMPRA));
        cmd.Parameters.AddWithValue("fllegada", NDate(d.FECHA_LLEGADA_REFACCION));
        cmd.Parameters.AddWithValue("freparacion", NDate(d.FECHA_REPARACION));
        cmd.Parameters.AddWithValue("quien", N(d.QUIEN_REALIZO_REPARACION));
        cmd.Parameters.AddWithValue("validacion", N(d.VALIDACION_FUNCIONAMIENTO));
        cmd.Parameters.AddWithValue("dreparacion", N(d.DESCRIPCION_REPARACION));
        cmd.Parameters.AddWithValue("observaciones", N(d.OBSERVACIONES));
        cmd.Parameters.AddWithValue("oc", N(d.OC_FACTURA));
    }

    /// <summary>
    /// Lee todas las columnas de negocio de un registro por ID.
    /// Devuelve null si no existe.
    /// </summary>
    private static Dictionary<string, object?>? LeerRegistro(NpgsqlConnection conn, int id)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT
                status, folio, planta, linea_persona, equipo, marca, modelo,
                numero_serie, descripcion_falla, accesorio_solicitado,
                fecha_solicitud, reporte_elaborado_por, tipo_observacion,
                vencimiento_dias, fecha_conteo_actual,
                fecha_limite_cierre, categoria_correctivo,
                refaccion_accesorio_compra, fecha_llegada_refaccion,
                fecha_reparacion, quien_realizo_reparacion,
                validacion_funcionamiento, descripcion_reparacion,
                observaciones, oc_factura
            FROM public.mantenimientos_correctivos
            WHERE id = @id
            """;
        cmd.Parameters.AddWithValue("id", id);

        using var r = cmd.ExecuteReader();
        if (!r.Read()) return null;

        var dict = new Dictionary<string, object?>();
        for (int i = 0; i < r.FieldCount; i++)
            dict[r.GetName(i)] = r.IsDBNull(i) ? null : r.GetValue(i);
        return dict;
    }

    /// <summary>
    /// Construye la cláusula WHERE y la lista de parámetros para las exportaciones filtradas.
    /// </summary>
    private static (string where, List<NpgsqlParameter> parms) ConstruirWhere(FiltrosCorrectivo f)
    {
        var where = "WHERE 1=1";
        var parms = new List<NpgsqlParameter>();
        int pIdx = 1;

        void Add(string col, string? val)
        {
            if (string.IsNullOrWhiteSpace(val)) return;
            where += $" AND {col}::text ILIKE @p{pIdx}";
            parms.Add(new NpgsqlParameter($"p{pIdx++}", $"%{val}%"));
        }

        Add("status", f.STATUS);
        Add("folio", f.FOLIO);
        Add("planta", f.PLANTA);
        Add("linea_persona", f.LINEA_PERSONA);
        Add("equipo", f.EQUIPO);
        Add("marca", f.MARCA);
        Add("modelo", f.MODELO);
        Add("numero_serie", f.NUMERO_SERIE);
        Add("descripcion_falla", f.DESCRIPCION_FALLA);
        Add("accesorio_solicitado", f.ACCESORIO_SOLICITADO);
        Add("fecha_solicitud", f.FECHA_SOLICITUD);
        Add("reporte_elaborado_por", f.REPORTE_ELABORADO_POR);
        Add("tipo_observacion", f.TIPO_OBSERVACION);
        // vencimiento_dias es INTEGER — filtro exacto, no ILIKE
        if (f.VENCIMIENTO_DIAS.HasValue)
        {
            where += $" AND vencimiento_dias = @p{pIdx}";
            parms.Add(new NpgsqlParameter($"p{pIdx++}", f.VENCIMIENTO_DIAS.Value));
        }
        Add("categoria_correctivo", f.CATEGORIA_CORRECTIVO);
        Add("refaccion_accesorio_compra", f.REFACCION_ACCESORIO_COMPRA);
        Add("fecha_llegada_refaccion", f.FECHA_LLEGADA_REFACCION);
        Add("fecha_reparacion", f.FECHA_REPARACION);
        Add("quien_realizo_reparacion", f.QUIEN_REALIZO_REPARACION);
        Add("validacion_funcionamiento", f.VALIDACION_FUNCIONAMIENTO);
        Add("descripcion_reparacion", f.DESCRIPCION_REPARACION);
        Add("observaciones", f.OBSERVACIONES);
        Add("oc_factura", f.OC_FACTURA);

        return (where, parms);
    }

    // ══════════════════════════════════════════════════════════════════════
    // GET /CORRECTIVOS/UBICACIONES
    // ══════════════════════════════════════════════════════════════════════
    [HttpGet("CORRECTIVOS/UBICACIONES")]
    public IActionResult ObtenerUbicaciones()
    {
        using var conn = _db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT DISTINCT TRIM(ubicacion)
            FROM public.mantenimientos_preventivos
            WHERE ubicacion IS NOT NULL AND TRIM(ubicacion) <> ''
            ORDER BY TRIM(ubicacion)
            """;

        var lista = new List<string>();
        using var r = cmd.ExecuteReader();
        while (r.Read()) lista.Add(r.GetString(0));

        return Ok(new { ubicaciones = lista });
    }






    //endpoint que guarda los datos del modal de qrpage controller "requiere correctivo"
    [HttpPost("MANTENIMIENTOS_CORRECTIVOS")]
    public IActionResult CrearCorrectivo([FromBody] CorrectivoQR dto)
    {
        try
        {
            using var conn = _db.Open();

            var sql = @"
        INSERT INTO mantenimientos_correctivos
        (
            status,
            planta,
            linea_persona,
            equipo,
            marca,
            modelo,
            numero_serie,
            descripcion_falla,
            accesorio_solicitado,
            fecha_solicitud,
            reporte_elaborado_por
        )
        VALUES
        (
            'PENDIENTE',
            @planta,
            @linea_persona,
            @equipo,
            @marca,
            @modelo,
            @numero_serie,
            @descripcion_falla,
            @accesorio_solicitado,
            @fecha_solicitud,
            @reporte_elaborado_por
        )";

            using var cmd = conn.CreateCommand();
            cmd.CommandText = sql;

            cmd.Parameters.AddWithValue("planta", dto.planta ?? "");
            cmd.Parameters.AddWithValue("linea_persona", dto.linea_persona ?? "");
            cmd.Parameters.AddWithValue("equipo", dto.equipo ?? "");
            cmd.Parameters.AddWithValue("marca", dto.marca ?? "");
            cmd.Parameters.AddWithValue("modelo", dto.modelo ?? "");
            cmd.Parameters.AddWithValue("numero_serie", dto.numero_serie ?? "");
            cmd.Parameters.AddWithValue("descripcion_falla", dto.descripcion_falla ?? "");
            cmd.Parameters.AddWithValue("accesorio_solicitado", dto.accesorio_solicitado ?? "");
            cmd.Parameters.AddWithValue(
                "fecha_solicitud",
                DateTime.TryParse(dto.fecha_solicitud, out var f) ? f : DateTime.Now
            );
            cmd.Parameters.AddWithValue("reporte_elaborado_por", dto.reporte_elaborado_por ?? "");

            cmd.ExecuteNonQuery();

            return Ok(new { ok = true, mensaje = "INSERT EJECUTADO" });
        }
        catch (Exception ex)
        {
            return Ok(new { error = ex.Message });
        }
    }



    // end point correctivos pendientes 
    [HttpGet("CORRECTIVOS/PENDIENTES")]
    public IActionResult ObtenerPendientes()
    {
        try
        {
            using var conn = _db.Open();
            using var cmd = conn.CreateCommand();

            cmd.CommandText = @"
        SELECT
            id,
            planta,
            linea_persona,
            equipo,
            descripcion_falla,
            fecha_solicitud,
            reporte_elaborado_por
        FROM mantenimientos_correctivos
        WHERE folio IS NULL
        ORDER BY id DESC";

            var lista = new List<object>();

            using var r = cmd.ExecuteReader();

            while (r.Read())
            {
                lista.Add(new
                {
                    id = r.GetInt32(0),
                    planta = r.IsDBNull(1) ? "" : r.GetString(1),
                    linea_persona = r.IsDBNull(2) ? "" : r.GetString(2),
                    equipo = r.IsDBNull(3) ? "" : r.GetString(3),
                    descripcion_falla = r.IsDBNull(4) ? "" : r.GetString(4),

                    fecha_solicitud = r.IsDBNull(5)
                        ? ""
                        : r.GetDateTime(5).ToString("yyyy-MM-dd"),

                    reporte_elaborado_por = r.IsDBNull(6) ? "" : r.GetString(6)
                });
            }

            return Ok(lista);
        }
        catch (Exception ex)
        {
            return StatusCode(500, ex.Message);
        }


    }
    [HttpGet("CORRECTIVOS/{id}")]
    public IActionResult ObtenerCorrectivo(int id)
    {
        using var conn = _db.Open();
        using var cmd = conn.CreateCommand();

        cmd.CommandText = @"
                SELECT
                    id,
                    status,
                    folio,
                    planta,
                    linea_persona,
                    equipo,
                    marca,
                    modelo,
                    numero_serie,
                    descripcion_falla,
                    accesorio_solicitado,
                    fecha_solicitud,
                    reporte_elaborado_por,
                    tipo_observacion,
                    vencimiento_dias,
                    fecha_conteo_actual,
                    fecha_limite_cierre,
                    categoria_correctivo,
                    refaccion_accesorio_compra,
                    fecha_llegada_refaccion,
                    fecha_reparacion,
                    quien_realizo_reparacion,
                    validacion_funcionamiento,
                    descripcion_reparacion,
                    observaciones,
                    oc_factura
                FROM mantenimientos_correctivos
                WHERE id = @id
                ";

        cmd.Parameters.AddWithValue("@id", id);


        using var r = cmd.ExecuteReader();

        if (!r.Read())
            return NotFound();

        var data = new
        {
            ID = r.GetInt32(0),
            STATUS = r.IsDBNull(1) ? "" : r.GetString(1),
            FOLIO = r.IsDBNull(2) ? "" : r.GetString(2),
            PLANTA = r.IsDBNull(3) ? "" : r.GetString(3),
            LINEA_PERSONA = r.IsDBNull(4) ? "" : r.GetString(4),
            EQUIPO = r.IsDBNull(5) ? "" : r.GetString(5),
            MARCA = r.IsDBNull(6) ? "" : r.GetString(6),
            MODELO = r.IsDBNull(7) ? "" : r.GetString(7),
            NUMERO_SERIE = r.IsDBNull(8) ? "" : r.GetString(8),
            DESCRIPCION_FALLA = r.IsDBNull(9) ? "" : r.GetString(9),
            ACCESORIO_SOLICITADO = r.IsDBNull(10) ? "" : r.GetString(10),
            FECHA_SOLICITUD = r.IsDBNull(11) ? "" : r.GetDateTime(11).ToString("yyyy-MM-dd"),
            REPORTE_ELABORADO_POR = r.IsDBNull(12) ? "" : r.GetString(12),
            TIPO_OBSERVACION = r.IsDBNull(13) ? "" : r.GetString(13),
            VENCIMIENTO_DIAS = r.IsDBNull(14) ? 0 : r.GetInt32(14),
            FECHA_CONTEO_ACTUAL = r.IsDBNull(15) ? "" : r.GetDateTime(15).ToString("yyyy-MM-dd"),
            FECHA_LIMITE_CIERRE = r.IsDBNull(16) ? "" : r.GetDateTime(16).ToString("yyyy-MM-dd"),
            CATEGORIA_CORRECTIVO = r.IsDBNull(17) ? "" : r.GetString(17),
            REFACCION_ACCESORIO_COMPRA = r.IsDBNull(18) ? "" : r.GetString(18),
            FECHA_LLEGADA_REFACCION = r.IsDBNull(19) ? "" : r.GetDateTime(19).ToString("yyyy-MM-dd"),
            FECHA_REPARACION = r.IsDBNull(20) ? "" : r.GetDateTime(20).ToString("yyyy-MM-dd"),
            QUIEN_REALIZO_REPARACION = r.IsDBNull(21) ? "" : r.GetString(21),
            VALIDACION_FUNCIONAMIENTO = r.IsDBNull(22) ? "" : r.GetString(22),
            DESCRIPCION_REPARACION = r.IsDBNull(23) ? "" : r.GetString(23),
            OBSERVACIONES = r.IsDBNull(24) ? "" : r.GetString(24),
            OC_FACTURA = r.IsDBNull(25) ? "" : r.GetString(25)
        };

        return Ok(data);
    }
    [HttpGet("CORRECTIVOS/EQUIPOS_UNICOS")]
    public IActionResult ObtenerEquiposUnicos([FromQuery] string? q)
    {
        using var conn = _db.Open();
        using var cmd = conn.CreateCommand();

        cmd.CommandText = @"
        SELECT DISTINCT TRIM(equipo)
        FROM mantenimientos_correctivos
        WHERE equipo IS NOT NULL
          AND TRIM(equipo) <> ''
          AND (@q IS NULL OR equipo ILIKE '%' || @q || '%')
        ORDER BY TRIM(equipo)
        LIMIT 20
    ";

        cmd.Parameters.AddWithValue("q", (object?)q ?? DBNull.Value);

        var lista = new List<string>();

        using var r = cmd.ExecuteReader();
        while (r.Read())
            lista.Add(r.GetString(0));

        return Ok(lista);
    }
    // ══════════════════════════════════════════════════════════════════════════
    // MODELOS
    // ══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Parámetros de filtro + paginación para GET /CORRECTIVOS.
    /// Todos los campos de filtro son opcionales.
    /// </summary>
    public class FiltrosCorrectivo
    {
        // Paginación
        public int Page { get; set; } = 1;
        public int Limit { get; set; } = 10;

        // Filtros de texto
        public string? STATUS { get; set; }
        public string? FOLIO { get; set; }
        public string? PLANTA { get; set; }
        public string? LINEA_PERSONA { get; set; }
        public string? EQUIPO { get; set; }
        public string? MARCA { get; set; }
        public string? MODELO { get; set; }
        public string? NUMERO_SERIE { get; set; }
        public string? DESCRIPCION_FALLA { get; set; }
        public string? ACCESORIO_SOLICITADO { get; set; }
        public string? REPORTE_ELABORADO_POR { get; set; }
        public string? TIPO_OBSERVACION { get; set; }
        public int? VENCIMIENTO_DIAS { get; set; }   // INTEGER en la BD
        public string? CATEGORIA_CORRECTIVO { get; set; }
        public string? REFACCION_ACCESORIO_COMPRA { get; set; }
        public string? QUIEN_REALIZO_REPARACION { get; set; }
        public string? VALIDACION_FUNCIONAMIENTO { get; set; }
        public string? DESCRIPCION_REPARACION { get; set; }
        public string? OBSERVACIONES { get; set; }
        public string? OC_FACTURA { get; set; }

        // Filtros de fecha (enviados como string "yyyy-MM-dd" desde el HTML)
        public string? FECHA_SOLICITUD { get; set; }
        public string? FECHA_CONTEO_ACTUAL { get; set; }
        public string? FECHA_LIMITE_CIERRE { get; set; }
        public string? FECHA_LLEGADA_REFACCION { get; set; }
        public string? FECHA_REPARACION { get; set; }
    }

    /// <summary>
    /// Cuerpo del POST (nuevo) y PUT (edición) de un correctivo.
    /// </summary>
    public class CorrectivoRequest
    {
        public string? STATUS { get; set; }
        public string? FOLIO { get; set; }
        public string? PLANTA { get; set; }
        public string? LINEA_PERSONA { get; set; }
        public string? EQUIPO { get; set; }
        public string? MARCA { get; set; }
        public string? MODELO { get; set; }
        public string? NUMERO_SERIE { get; set; }
        public string? DESCRIPCION_FALLA { get; set; }
        public string? ACCESORIO_SOLICITADO { get; set; }
        public string? FECHA_SOLICITUD { get; set; }
        public string? REPORTE_ELABORADO_POR { get; set; }
        public string? TIPO_OBSERVACION { get; set; }
        public int? VENCIMIENTO_DIAS { get; set; }   // INTEGER en la BD
        public string? FECHA_CONTEO_ACTUAL { get; set; }
        public string? FECHA_LIMITE_CIERRE { get; set; }
        public string? CATEGORIA_CORRECTIVO { get; set; }
        public string? REFACCION_ACCESORIO_COMPRA { get; set; }
        public string? FECHA_LLEGADA_REFACCION { get; set; }
        public string? FECHA_REPARACION { get; set; }
        public string? QUIEN_REALIZO_REPARACION { get; set; }
        public string? VALIDACION_FUNCIONAMIENTO { get; set; }
        public string? DESCRIPCION_REPARACION { get; set; }
        public string? OBSERVACIONES { get; set; }
        public string? OC_FACTURA { get; set; }
    }
    //modelo del modal "requiere correctivo"

    public class CorrectivoQR
    {
        public string planta { get; set; } = "";
        public string linea_persona { get; set; } = "";
        public string equipo { get; set; } = "";
        public string marca { get; set; } = "";
        public string modelo { get; set; } = "";
        public string numero_serie { get; set; } = "";
        public string descripcion_falla { get; set; } = "";
        public string accesorio_solicitado { get; set; } = "";
        public string fecha_solicitud { get; set; } = "";
        public string reporte_elaborado_por { get; set; } = "";
    }
}