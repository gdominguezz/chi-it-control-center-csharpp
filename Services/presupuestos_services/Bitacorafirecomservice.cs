using ChiIT.Data;
using Npgsql;

using OfficeOpenXml;
using OfficeOpenXml.Style;

using System.Drawing;

namespace ChiIT.Services;

// ── DTOs / Modelos ────────────────────────────────────────────────────────────

public class BitacoraFirecomDto
{
    public string? ID_UNICO { get; set; }
    public string? OC { get; set; }
    public string? ORDEN_SERVICIO { get; set; }
    public string? FECHA { get; set; }
    public string? PERSONA_QUE_SOLICITA_REPORTA { get; set; }
    public bool? CUENTA_CON_POLIZA { get; set; }
    public bool? SERVICIO_CON_COSTO { get; set; }
    public string? UBICACION { get; set; }
    public string? AREA { get; set; }
    public int? CANTIDAD { get; set; }
    public string? DESCRIPCION_SERVICIO { get; set; }
    public string? DESCRIPCION_TRABAJO { get; set; }
    public string? MATERIAL_EQUIPO { get; set; }
    public string? OBSERVACIONES { get; set; }
    public string? PROVEEDORES { get; set; }
    public string? PANEL_FACEPLATE { get; set; }
    public string? SWITCH_RED { get; set; }
    public string? PERSONAL_QUE_RECIBIO { get; set; }
    public bool? PAGADO { get; set; }
    public string? OC2 { get; set; }
}

public class BitacoraFirecomFiltros
{
    public string? ID_UNICO { get; set; }
    public string? OC { get; set; }
    public string? ORDEN_SERVICIO { get; set; }
    public string? FECHA { get; set; }
    public string? PERSONA_QUE_SOLICITA_REPORTA { get; set; }
    public string? CUENTA_CON_POLIZA { get; set; }
    public string? SERVICIO_CON_COSTO { get; set; }
    public string? UBICACION { get; set; }
    public string? AREA { get; set; }
    public string? DESCRIPCION_SERVICIO { get; set; }
    public string? DESCRIPCION_TRABAJO { get; set; }
    public string? MATERIAL_EQUIPO { get; set; }
    public string? OBSERVACIONES { get; set; }
    public string? PROVEEDORES { get; set; }
    public string? PANEL_FACEPLATE { get; set; }
    public string? SWITCH_RED { get; set; }
    public string? PERSONAL_QUE_RECIBIO { get; set; }
    public string? PAGADO { get; set; }
    public string? OC2 { get; set; }
}

// ── Servicio ──────────────────────────────────────────────────────────────────

public class BitacoraFirecomService
{
    private readonly DbConnectionPool _pool;

    private static readonly string[] COLS =
    [
        "ID_UNICO", "OC", "ORDEN_SERVICIO", "FECHA",
        "PERSONA_QUE_SOLICITA_REPORTA", "CUENTA_CON_POLIZA", "SERVICIO_CON_COSTO",
        "UBICACION", "AREA", "CANTIDAD",
        "DESCRIPCION_SERVICIO", "DESCRIPCION_TRABAJO",
        "MATERIAL_EQUIPO", "OBSERVACIONES",
        "PROVEEDORES", "PANEL_FACEPLATE", "SWITCH_RED",
        "PERSONAL_QUE_RECIBIO", "PAGADO", "OC2"
    ];

    public BitacoraFirecomService(DbConnectionPool pool)
    {
        _pool = pool;
        _ = InicializarTablaAsync();
    }

    // ── DDL ───────────────────────────────────────────────────────────────
    private async Task InicializarTablaAsync()
    {
        await using var conn = await _pool.OpenAsync();
        await using var cmd = new NpgsqlCommand("""
            CREATE TABLE IF NOT EXISTS bitacora_firecom (
                id                            SERIAL PRIMARY KEY,
                id_unico                      TEXT,
                oc                            TEXT,
                orden_servicio                TEXT,
                fecha                         DATE,
                persona_que_solicita_reporta  TEXT,
                cuenta_con_poliza             BOOLEAN,
                servicio_con_costo            BOOLEAN,
                ubicacion                     TEXT,
                area                          TEXT,
                cantidad                      INTEGER,
                descripcion_servicio          TEXT,
                descripcion_trabajo           TEXT,
                material_equipo               TEXT,
                observaciones                 TEXT,
                proveedores                   TEXT,
                panel_faceplate               TEXT,
                switch_red                    TEXT,
                personal_que_recibio          TEXT,
                pagado                        BOOLEAN,
                oc2                           TEXT,
                created_at                    TIMESTAMP DEFAULT NOW()
            );

            CREATE TABLE IF NOT EXISTS bitacora_firecom_historial (
                id                SERIAL PRIMARY KEY,
                registro_id       INTEGER NOT NULL REFERENCES bitacora_firecom(id) ON DELETE CASCADE,
                usuario           TEXT    NOT NULL,
                fecha             TIMESTAMPTZ DEFAULT NOW(),
                registro_anterior JSONB,
                registro_nuevo    JSONB
            );
            """, conn);

        await cmd.ExecuteNonQueryAsync();
    }

    // ── Listar (paginado + filtros) ───────────────────────────────────────
    public async Task<object> ListarAsync(int page, int limit, BitacoraFirecomFiltros f)
    {
        await using var conn = await _pool.OpenAsync();

        var (where, parms) = ConstruirWhere(f);
        var offset = (page - 1) * limit;

        // Total
        await using var cmdCount = new NpgsqlCommand(
            $"SELECT COUNT(*) FROM bitacora_firecom {where}", conn);
        foreach (var (k, v) in parms) cmdCount.Parameters.AddWithValue(k, v ?? (object)DBNull.Value);
        var total = Convert.ToInt64(await cmdCount.ExecuteScalarAsync());

        // Datos
        await using var cmdData = new NpgsqlCommand(
            $@"SELECT id, id_unico, oc, orden_servicio, fecha,
                      persona_que_solicita_reporta, cuenta_con_poliza, servicio_con_costo,
                      ubicacion, area, cantidad,
                      descripcion_servicio, descripcion_trabajo,
                      material_equipo, observaciones,
                      proveedores, panel_faceplate, switch_red,
                      personal_que_recibio, pagado, oc2, created_at
               FROM bitacora_firecom {where}
               ORDER BY id DESC
               LIMIT @lim OFFSET @off", conn);

        foreach (var (k, v) in parms) cmdData.Parameters.AddWithValue(k, v ?? (object)DBNull.Value);
        cmdData.Parameters.AddWithValue("lim", limit);
        cmdData.Parameters.AddWithValue("off", offset);

        var lista = new List<object>();
        await using var reader = await cmdData.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            lista.Add(new
            {
                ID = reader.GetInt32(0),
                ID_UNICO = Str(reader, 1),
                OC = Str(reader, 2),
                ORDEN_SERVICIO = Str(reader, 3),
                FECHA = Str(reader, 4),
                PERSONA_QUE_SOLICITA_REPORTA = Str(reader, 5),
                CUENTA_CON_POLIZA = reader.IsDBNull(6) ? (bool?)null : reader.GetBoolean(6),
                SERVICIO_CON_COSTO = reader.IsDBNull(7) ? (bool?)null : reader.GetBoolean(7),
                UBICACION = Str(reader, 8),
                AREA = Str(reader, 9),
                CANTIDAD = reader.IsDBNull(10) ? (int?)null : reader.GetInt32(10),
                DESCRIPCION_SERVICIO = Str(reader, 11),
                DESCRIPCION_TRABAJO = Str(reader, 12),
                MATERIAL_EQUIPO = Str(reader, 13),
                OBSERVACIONES = Str(reader, 14),
                PROVEEDORES = Str(reader, 15),
                PANEL_FACEPLATE = Str(reader, 16),
                SWITCH_RED = Str(reader, 17),
                PERSONAL_QUE_RECIBIO = Str(reader, 18),
                PAGADO = reader.IsDBNull(19) ? (bool?)null : reader.GetBoolean(19),
                OC2 = Str(reader, 20),
                CREATED_AT = Str(reader, 21),
            });
        }

        return new { total, data = lista };
    }

    // ── Crear ─────────────────────────────────────────────────────────────
    public async Task<int> CrearAsync(BitacoraFirecomDto dto, string usuario)
    {
        await using var conn = await _pool.OpenAsync();

        await using var cmd = new NpgsqlCommand("""
            INSERT INTO bitacora_firecom (
                id_unico, oc, orden_servicio, fecha,
                persona_que_solicita_reporta, cuenta_con_poliza, servicio_con_costo,
                ubicacion, area, cantidad,
                descripcion_servicio, descripcion_trabajo,
                material_equipo, observaciones,
                proveedores, panel_faceplate, switch_red,
                personal_que_recibio, pagado, oc2
            ) VALUES (
                @id_unico, @oc, @orden_servicio, @fecha,
                @persona_que_solicita_reporta, @cuenta_con_poliza, @servicio_con_costo,
                @ubicacion, @area, @cantidad,
                @descripcion_servicio, @descripcion_trabajo,
                @material_equipo, @observaciones,
                @proveedores, @panel_faceplate, @switch_red,
                @personal_que_recibio, @pagado, @oc2
            ) RETURNING id
            """, conn);

        AgregarParametros(cmd, dto);
        var id = Convert.ToInt32(await cmd.ExecuteScalarAsync());

        // Historial: inserción inicial
        var snap = await SnapshotAsync(conn, id);
        if (snap is not null)
            await RegistrarHistorialAsync(conn, id, usuario,
                new Dictionary<string, object?>(), snap);

        return id;
    }

    // ── Editar ────────────────────────────────────────────────────────────
    public async Task<bool> EditarAsync(int id, BitacoraFirecomDto dto, string usuario)
    {
        await using var conn = await _pool.OpenAsync();

        var anterior = await SnapshotAsync(conn, id);
        if (anterior is null) return false;

        await using var cmd = new NpgsqlCommand("""
            UPDATE bitacora_firecom SET
                id_unico                     = @id_unico,
                oc                           = @oc,
                orden_servicio               = @orden_servicio,
                fecha                        = @fecha,
                persona_que_solicita_reporta = @persona_que_solicita_reporta,
                cuenta_con_poliza            = @cuenta_con_poliza,
                servicio_con_costo           = @servicio_con_costo,
                ubicacion                    = @ubicacion,
                area                         = @area,
                cantidad                     = @cantidad,
                descripcion_servicio         = @descripcion_servicio,
                descripcion_trabajo          = @descripcion_trabajo,
                material_equipo              = @material_equipo,
                observaciones                = @observaciones,
                proveedores                  = @proveedores,
                panel_faceplate              = @panel_faceplate,
                switch_red                   = @switch_red,
                personal_que_recibio         = @personal_que_recibio,
                pagado                       = @pagado,
                oc2                          = @oc2
            WHERE id = @id
            """, conn);

        AgregarParametros(cmd, dto);
        cmd.Parameters.AddWithValue("id", id);
        var filas = await cmd.ExecuteNonQueryAsync();
        if (filas == 0) return false;

        var nuevo = await SnapshotAsync(conn, id);
        if (nuevo is not null)
            await RegistrarHistorialAsync(conn, id, usuario, anterior, nuevo);

        return true;
    }

    // ── Eliminar ──────────────────────────────────────────────────────────
    public async Task<bool> EliminarAsync(int id)
    {
        await using var conn = await _pool.OpenAsync();
        await using var cmd = new NpgsqlCommand(
            "DELETE FROM bitacora_firecom WHERE id = @id", conn);
        cmd.Parameters.AddWithValue("id", id);
        return await cmd.ExecuteNonQueryAsync() > 0;
    }

    // ── Historial ─────────────────────────────────────────────────────────
    public async Task<List<object>> HistorialAsync(int id)
    {
        await using var conn = await _pool.OpenAsync();
        await using var cmd = new NpgsqlCommand("""
            SELECT id, usuario, fecha, registro_anterior, registro_nuevo
            FROM bitacora_firecom_historial
            WHERE registro_id = @id
            ORDER BY fecha DESC
            """, conn);
        cmd.Parameters.AddWithValue("id", id);

        var lista = new List<object>();
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
        {
            lista.Add(new
            {
                ID = r.GetInt32(0),
                USUARIO = r.GetString(1),
                FECHA = r.GetDateTime(2).ToString("o"),
                REGISTRO_ANTERIOR = r.IsDBNull(3) ? null : r.GetString(3),
                REGISTRO_NUEVO = r.IsDBNull(4) ? null : r.GetString(4),
            });
        }
        return lista;
    }

    // ── Exportar (filtrado o todo) ─────────────────────────────────────────
    public async Task<byte[]> ExportarAsync(BitacoraFirecomFiltros f)
    {
        await using var conn = await _pool.OpenAsync();

        var (where, parms) = ConstruirWhere(f);

        await using var cmd = new NpgsqlCommand(
            $@"SELECT id, id_unico, oc, orden_servicio, fecha,
                      persona_que_solicita_reporta, cuenta_con_poliza, servicio_con_costo,
                      ubicacion, area, cantidad,
                      descripcion_servicio, descripcion_trabajo,
                      material_equipo, observaciones,
                      proveedores, panel_faceplate, switch_red,
                      personal_que_recibio, pagado, oc2, created_at
               FROM bitacora_firecom {where}
               ORDER BY id DESC", conn);

        foreach (var (k, v) in parms) cmd.Parameters.AddWithValue(k, v ?? (object)DBNull.Value);

        var rows = new List<string?[]>();
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
        {
            rows.Add(Enumerable.Range(0, 22)
                .Select(i => r.IsDBNull(i) ? null : r.GetValue(i)?.ToString())
                .ToArray());
        }

        return GenerarExcel(rows);
    }

    // ── Exportar por año ──────────────────────────────────────────────────
    public async Task<byte[]> ExportarPorAnioAsync(int anio)
    {
        await using var conn = await _pool.OpenAsync();
        await using var cmd = new NpgsqlCommand(
            @"SELECT id, id_unico, oc, orden_servicio, fecha,
                     persona_que_solicita_reporta, cuenta_con_poliza, servicio_con_costo,
                     ubicacion, area, cantidad,
                     descripcion_servicio, descripcion_trabajo,
                     material_equipo, observaciones,
                     proveedores, panel_faceplate, switch_red,
                     personal_que_recibio, pagado, oc2, created_at
              FROM bitacora_firecom
              WHERE EXTRACT(YEAR FROM fecha) = @anio
              ORDER BY id DESC", conn);
        cmd.Parameters.AddWithValue("anio", anio);

        var rows = new List<string?[]>();
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
        {
            rows.Add(Enumerable.Range(0, 22)
                .Select(i => r.IsDBNull(i) ? null : r.GetValue(i)?.ToString())
                .ToArray());
        }

        return GenerarExcel(rows);
    }

    // ── Helpers privados ──────────────────────────────────────────────────

    private static (string where, List<(string key, object? val)> parms) ConstruirWhere(BitacoraFirecomFiltros f)
    {
        var conds = new List<string>();
        var parms = new List<(string, object?)>();
        var idx = 1;

        void Add(string col, string? val)
        {
            if (string.IsNullOrWhiteSpace(val)) return;
            conds.Add($"LOWER({col}::TEXT) LIKE LOWER(@p{idx})");
            parms.Add(($"p{idx}", $"%{val}%"));
            idx++;
        }

        Add("id_unico", f.ID_UNICO);
        Add("oc", f.OC);
        Add("orden_servicio", f.ORDEN_SERVICIO);
        Add("fecha", f.FECHA);
        Add("persona_que_solicita_reporta", f.PERSONA_QUE_SOLICITA_REPORTA);
        Add("cuenta_con_poliza::TEXT", f.CUENTA_CON_POLIZA);
        Add("servicio_con_costo::TEXT", f.SERVICIO_CON_COSTO);
        Add("ubicacion", f.UBICACION);
        Add("area", f.AREA);
        Add("descripcion_servicio", f.DESCRIPCION_SERVICIO);
        Add("descripcion_trabajo", f.DESCRIPCION_TRABAJO);
        Add("material_equipo", f.MATERIAL_EQUIPO);
        Add("observaciones", f.OBSERVACIONES);
        Add("proveedores", f.PROVEEDORES);
        Add("panel_faceplate", f.PANEL_FACEPLATE);
        Add("switch_red", f.SWITCH_RED);
        Add("personal_que_recibio", f.PERSONAL_QUE_RECIBIO);
        Add("pagado::TEXT", f.PAGADO);
        Add("oc2", f.OC2);

        var where = conds.Count > 0 ? "WHERE " + string.Join(" AND ", conds) : "";
        return (where, parms);
    }

    private static void AgregarParametros(NpgsqlCommand cmd, BitacoraFirecomDto dto)
    {
        cmd.Parameters.AddWithValue("id_unico", (object?)dto.ID_UNICO ?? DBNull.Value);
        cmd.Parameters.AddWithValue("oc", (object?)dto.OC ?? DBNull.Value);
        cmd.Parameters.AddWithValue("orden_servicio", (object?)dto.ORDEN_SERVICIO ?? DBNull.Value);
        cmd.Parameters.AddWithValue("fecha", (object?)dto.FECHA ?? DBNull.Value);
        cmd.Parameters.AddWithValue("persona_que_solicita_reporta", (object?)dto.PERSONA_QUE_SOLICITA_REPORTA ?? DBNull.Value);
        cmd.Parameters.AddWithValue("cuenta_con_poliza", (object?)dto.CUENTA_CON_POLIZA ?? DBNull.Value);
        cmd.Parameters.AddWithValue("servicio_con_costo", (object?)dto.SERVICIO_CON_COSTO ?? DBNull.Value);
        cmd.Parameters.AddWithValue("ubicacion", (object?)dto.UBICACION ?? DBNull.Value);
        cmd.Parameters.AddWithValue("area", (object?)dto.AREA ?? DBNull.Value);
        cmd.Parameters.AddWithValue("cantidad", (object?)dto.CANTIDAD ?? DBNull.Value);
        cmd.Parameters.AddWithValue("descripcion_servicio", (object?)dto.DESCRIPCION_SERVICIO ?? DBNull.Value);
        cmd.Parameters.AddWithValue("descripcion_trabajo", (object?)dto.DESCRIPCION_TRABAJO ?? DBNull.Value);
        cmd.Parameters.AddWithValue("material_equipo", (object?)dto.MATERIAL_EQUIPO ?? DBNull.Value);
        cmd.Parameters.AddWithValue("observaciones", (object?)dto.OBSERVACIONES ?? DBNull.Value);
        cmd.Parameters.AddWithValue("proveedores", (object?)dto.PROVEEDORES ?? DBNull.Value);
        cmd.Parameters.AddWithValue("panel_faceplate", (object?)dto.PANEL_FACEPLATE ?? DBNull.Value);
        cmd.Parameters.AddWithValue("switch_red", (object?)dto.SWITCH_RED ?? DBNull.Value);
        cmd.Parameters.AddWithValue("personal_que_recibio", (object?)dto.PERSONAL_QUE_RECIBIO ?? DBNull.Value);
        cmd.Parameters.AddWithValue("pagado", (object?)dto.PAGADO ?? DBNull.Value);
        cmd.Parameters.AddWithValue("oc2", (object?)dto.OC2 ?? DBNull.Value);
    }

    private async Task<Dictionary<string, object?>?> SnapshotAsync(NpgsqlConnection conn, int id)
    {
        await using var cmd = new NpgsqlCommand(
            @"SELECT id_unico, oc, orden_servicio, fecha,
                     persona_que_solicita_reporta, cuenta_con_poliza, servicio_con_costo,
                     ubicacion, area, cantidad,
                     descripcion_servicio, descripcion_trabajo,
                     material_equipo, observaciones,
                     proveedores, panel_faceplate, switch_red,
                     personal_que_recibio, pagado, oc2
              FROM bitacora_firecom WHERE id = @id", conn);
        cmd.Parameters.AddWithValue("id", id);

        await using var r = await cmd.ExecuteReaderAsync();
        if (!await r.ReadAsync()) return null;

        var snap = new Dictionary<string, object?>();
        for (int i = 0; i < COLS.Length; i++)
            snap[COLS[i]] = r.IsDBNull(i) ? null : r.GetValue(i)?.ToString();

        return snap;
    }

    private async Task RegistrarHistorialAsync(NpgsqlConnection conn, int registroId,
        string usuario, Dictionary<string, object?> anterior, Dictionary<string, object?> nuevo)
    {
        var antesJson = System.Text.Json.JsonSerializer.Serialize(anterior);
        var despuesJson = System.Text.Json.JsonSerializer.Serialize(nuevo);

        await using var cmd = new NpgsqlCommand("""
            INSERT INTO bitacora_firecom_historial (registro_id, usuario, registro_anterior, registro_nuevo)
            VALUES (@rid, @usr, @ant::jsonb, @nvo::jsonb)
            """, conn);

        cmd.Parameters.AddWithValue("rid", registroId);
        cmd.Parameters.AddWithValue("usr", usuario);
        cmd.Parameters.AddWithValue("ant", antesJson);
        cmd.Parameters.AddWithValue("nvo", despuesJson);

        await cmd.ExecuteNonQueryAsync();
    }

    private static byte[] GenerarExcel(List<string?[]> rows)
    {
        ExcelPackage.License.SetNonCommercialPersonal("IT Control Center");
        using var pkg = new ExcelPackage();
        var ws = pkg.Workbook.Worksheets.Add("Bitácora Firecom");

        string[] headers =
        [
            "ID", "ID ÚNICO", "OC", "ORDEN SERVICIO", "FECHA",
            "PERSONA QUE SOLICITA/REPORTA", "CUENTA CON PÓLIZA", "SERVICIO CON COSTO",
            "UBICACIÓN", "ÁREA", "CANTIDAD",
            "DESCRIPCIÓN SERVICIO", "DESCRIPCIÓN TRABAJO",
            "MATERIAL/EQUIPO", "OBSERVACIONES",
            "PROVEEDORES", "PANEL/FACEPLATE", "SWITCH RED",
            "PERSONAL QUE RECIBIÓ", "PAGADO", "OC2", "FECHA REGISTRO"
        ];

        for (int c = 0; c < headers.Length; c++)
        {
            ws.Cells[1, c + 1].Value = headers[c];
            ws.Cells[1, c + 1].Style.Font.Bold = true;
            ws.Cells[1, c + 1].Style.Fill.PatternType = ExcelFillStyle.Solid;
            ws.Cells[1, c + 1].Style.Fill.BackgroundColor.SetColor(Color.FromArgb(30, 41, 59));
            ws.Cells[1, c + 1].Style.Font.Color.SetColor(Color.White);
            ws.Cells[1, c + 1].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
        }

        for (int r = 0; r < rows.Count; r++)
        {
            for (int c = 0; c < rows[r].Length; c++)
                ws.Cells[r + 2, c + 1].Value = rows[r][c];

            if (r % 2 == 0)
            {
                using var range = ws.Cells[r + 2, 1, r + 2, headers.Length];
                range.Style.Fill.PatternType = ExcelFillStyle.Solid;
                range.Style.Fill.BackgroundColor.SetColor(Color.FromArgb(241, 245, 249));
            }
        }

        ws.Cells.AutoFitColumns();
        return pkg.GetAsByteArray();
    }

    private static string? Str(NpgsqlDataReader r, int i)
        => r.IsDBNull(i) ? null : r.GetValue(i)?.ToString();
}