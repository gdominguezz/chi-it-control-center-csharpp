using ChiIT.Data;
using Microsoft.Data.SqlClient;

using OfficeOpenXml;
using OfficeOpenXml.Style;

using System.Drawing;

namespace ChiIT.Services;

// ── DTOs / Modelos ────────────────────────────────────────────────────────────

public class ServicioProveedorDto
{
    public string?  ID_UNICO               { get; set; }
    public string?  FOLIO_UNICO            { get; set; }
    public string?  FOLIO_COTIZACION       { get; set; }
    public string?  FOLIO_REPORTE          { get; set; }
    public string?  FECHA                  { get; set; }
    public string?  REQUISITOR             { get; set; }
    public bool?    CUENTA_CON_POLIZA      { get; set; }
    public bool?    SERVICIO_CON_COSTO     { get; set; }
    public string?  UBICACION_PLANTA       { get; set; }
    public string?  AREA                   { get; set; }
    public int?     CANTIDAD               { get; set; }
    public string?  DESCRIPCION_SERVICIO   { get; set; }
    public string?  DESCRIPCION_TRABAJO    { get; set; }
    public string?  MATERIAL_EQUIPO        { get; set; }
    public string?  OBSERVACIONES          { get; set; }
    public string?  PROVEEDORES            { get; set; }
    public string?  PANEL_FACEPLATE        { get; set; }
    public string?  SWITCH                 { get; set; }
    public string?  PERSONAL_RECIBIO       { get; set; }
    public bool?    SOLICITUD_FINALIZADA   { get; set; }
    public decimal? COSTO                  { get; set; }
}

public class ServicioProveedorFiltros
{
    public string? ID_UNICO               { get; set; }
    public string? FOLIO_UNICO            { get; set; }
    public string? FOLIO_COTIZACION       { get; set; }
    public string? FOLIO_REPORTE          { get; set; }
    public string? FECHA                  { get; set; }
    public string? REQUISITOR             { get; set; }
    public string? CUENTA_CON_POLIZA      { get; set; }
    public string? SERVICIO_CON_COSTO     { get; set; }
    public string? UBICACION_PLANTA       { get; set; }
    public string? AREA                   { get; set; }
    public string? DESCRIPCION_SERVICIO   { get; set; }
    public string? PROVEEDORES            { get; set; }
    public string? PERSONAL_RECIBIO       { get; set; }
    public string? SOLICITUD_FINALIZADA   { get; set; }
}

// ── Servicio ──────────────────────────────────────────────────────────────────

public class ServiciosProveedoresService
{
    private readonly DbConnectionPool _pool;

    private static readonly string[] COLS =
    [
        "ID_UNICO","FOLIO_UNICO","FOLIO_COTIZACION","FOLIO_REPORTE","FECHA",
        "REQUISITOR","CUENTA_CON_POLIZA","SERVICIO_CON_COSTO","UBICACION_PLANTA","AREA",
        "CANTIDAD","DESCRIPCION_SERVICIO","DESCRIPCION_TRABAJO","MATERIAL_EQUIPO","OBSERVACIONES",
        "PROVEEDORES","PANEL_FACEPLATE","SWITCH","PERSONAL_RECIBIO","SOLICITUD_FINALIZADA","COSTO"
    ];

    public ServiciosProveedoresService(DbConnectionPool pool)
    {
        _pool = pool;
        _ = InicializarTablaAsync();
    }

    // ── DDL ───────────────────────────────────────────────────────────────
    private async Task InicializarTablaAsync()
    {
        await using var conn = await _pool.OpenAsync();
        await using var cmd = new SqlCommand("""
            CREATE TABLE IF NOT EXISTS servicios_proveedores (
                id                   SERIAL PRIMARY KEY,
                id_unico             TEXT,
                folio_unico          TEXT,
                folio_cotizacion     TEXT,
                folio_reporte        TEXT,
                fecha                DATE,
                requisitor           TEXT,
                cuenta_con_poliza    BOOLEAN,
                servicio_con_costo   BOOLEAN,
                ubicacion_planta     TEXT,
                area                 TEXT,
                cantidad             INTEGER,
                descripcion_servicio TEXT,
                descripcion_trabajo  TEXT,
                material_equipo      TEXT,
                observaciones        TEXT,
                proveedores          TEXT,
                panel_faceplate      TEXT,
                switch               TEXT,
                personal_recibio     TEXT,
                solicitud_finalizada BOOLEAN,
                costo                NUMERIC(12,2)
            );

            CREATE TABLE IF NOT EXISTS servicios_proveedores_historial (
                id                SERIAL PRIMARY KEY,
                servicio_id       INTEGER NOT NULL REFERENCES servicios_proveedores(id) ON DELETE CASCADE,
                usuario           TEXT    NOT NULL,
                fecha             TIMESTAMPTZ DEFAULT GETDATE(),
                registro_anterior JSONB,
                registro_nuevo    JSONB
            );
            """, conn);

        await cmd.ExecuteNonQueryAsync();
    }

    // ── Listar (paginado + filtros) ───────────────────────────────────────
    public async Task<object> ListarAsync(int page, int limit, ServicioProveedorFiltros f)
    {
        await using var conn = await _pool.OpenAsync();

        var (where, parms) = ConstruirWhere(f);
        if (string.IsNullOrWhiteSpace(where))
            where = "WHERE (activo IS NULL OR activo = 1)";
        else
            where += " AND (activo IS NULL OR activo = 1)";
        var offset = (page - 1) * limit;

        // Total
        await using var cmdCount = new SqlCommand(
            $"SELECT COUNT(*) FROM servicios_proveedores {where}", conn);
        foreach (var (k, v) in parms) cmdCount.Parameters.AddWithValue(k, v ?? (object)DBNull.Value);
        var total = Convert.ToInt64(await cmdCount.ExecuteScalarAsync());

        // Datos
        await using var cmdData = new SqlCommand(
            $@"SELECT id, id_unico, folio_unico, folio_cotizacion, folio_reporte,
                      fecha, requisitor, cuenta_con_poliza, servicio_con_costo,
                      ubicacion_planta, area, cantidad, descripcion_servicio,
                      descripcion_trabajo, material_equipo, observaciones,
                      proveedores, panel_faceplate, switch, personal_recibio,
                      solicitud_finalizada, costo
               FROM servicios_proveedores {where}
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
                ID                   = reader.GetInt32(0),
                ID_UNICO             = Str(reader, 1),
                FOLIO_UNICO          = Str(reader, 2),
                FOLIO_COTIZACION     = Str(reader, 3),
                FOLIO_REPORTE        = Str(reader, 4),
                FECHA                = Str(reader, 5),
                REQUISITOR           = Str(reader, 6),
                CUENTA_CON_POLIZA    = reader.IsDBNull(7)  ? (bool?)null    : reader.GetBoolean(7),
                SERVICIO_CON_COSTO   = reader.IsDBNull(8)  ? (bool?)null    : reader.GetBoolean(8),
                UBICACION_PLANTA     = Str(reader, 9),
                AREA                 = Str(reader, 10),
                CANTIDAD             = reader.IsDBNull(11) ? (int?)null     : reader.GetInt32(11),
                DESCRIPCION_SERVICIO = Str(reader, 12),
                DESCRIPCION_TRABAJO  = Str(reader, 13),
                MATERIAL_EQUIPO      = Str(reader, 14),
                OBSERVACIONES        = Str(reader, 15),
                PROVEEDORES          = Str(reader, 16),
                PANEL_FACEPLATE      = Str(reader, 17),
                SWITCH               = Str(reader, 18),
                PERSONAL_RECIBIO     = Str(reader, 19),
                SOLICITUD_FINALIZADA = reader.IsDBNull(20) ? (bool?)null    : reader.GetBoolean(20),
                COSTO                = reader.IsDBNull(21) ? (decimal?)null : reader.GetDecimal(21)
            });
        }

        return new
        {
            total,
            page,
            limit,
            pages = (int)Math.Ceiling((double)total / limit),
            data  = lista
        };
    }

    // ── Crear ─────────────────────────────────────────────────────────────
    public async Task<int> CrearAsync(ServicioProveedorDto dto, string usuario)
    {
        await using var conn = await _pool.OpenAsync();
        await using var cmd = new SqlCommand(
            @"INSERT INTO servicios_proveedores (
                id_unico, folio_unico, folio_cotizacion, folio_reporte, fecha,
                requisitor, cuenta_con_poliza, servicio_con_costo, ubicacion_planta, area,
                cantidad, descripcion_servicio, descripcion_trabajo, material_equipo, observaciones,
                proveedores, panel_faceplate, switch, personal_recibio, solicitud_finalizada, costo
              ) VALUES (
                @id_unico, @folio_unico, @folio_cotizacion, @folio_reporte, @fecha,
                @requisitor, @cuenta_con_poliza, @servicio_con_costo, @ubicacion_planta, @area,
                @cantidad, @descripcion_servicio, @descripcion_trabajo, @material_equipo, @observaciones,
                @proveedores, @panel_faceplate, @switch, @personal_recibio, @solicitud_finalizada, @costo
              ) OUTPUT INSERTED.id", conn);

        AgregarParametros(cmd, dto);
        var id = Convert.ToInt32(await cmd.ExecuteScalarAsync());

        // Historial de creación
        var snap = await SnapshotAsync(conn, id);
        if (snap != null)
            await RegistrarHistorialAsync(conn, id, usuario,
                new Dictionary<string, object?>(), snap);

        return id;
    }

    // ── Editar ────────────────────────────────────────────────────────────
    public async Task<bool> EditarAsync(int id, ServicioProveedorDto dto, string usuario)
    {
        await using var conn = await _pool.OpenAsync();

        var anterior = await SnapshotAsync(conn, id);
        if (anterior == null) return false;

        await using var cmd = new SqlCommand(
            @"UPDATE servicios_proveedores SET
                id_unico             = @id_unico,
                folio_unico          = @folio_unico,
                folio_cotizacion     = @folio_cotizacion,
                folio_reporte        = @folio_reporte,
                fecha                = @fecha,
                requisitor           = @requisitor,
                cuenta_con_poliza    = @cuenta_con_poliza,
                servicio_con_costo   = @servicio_con_costo,
                ubicacion_planta     = @ubicacion_planta,
                area                 = @area,
                cantidad             = @cantidad,
                descripcion_servicio = @descripcion_servicio,
                descripcion_trabajo  = @descripcion_trabajo,
                material_equipo      = @material_equipo,
                observaciones        = @observaciones,
                proveedores          = @proveedores,
                panel_faceplate      = @panel_faceplate,
                switch               = @switch,
                personal_recibio     = @personal_recibio,
                solicitud_finalizada = @solicitud_finalizada,
                costo                = @costo
              WHERE id = @id", conn);

        AgregarParametros(cmd, dto);
        cmd.Parameters.AddWithValue("id", id);

        var rows = await cmd.ExecuteNonQueryAsync();
        if (rows == 0) return false;

        var nuevo = await SnapshotAsync(conn, id);
        if (nuevo != null)
            await RegistrarHistorialAsync(conn, id, usuario, anterior, nuevo);

        return true;
    }

    // ── Eliminar ──────────────────────────────────────────────────────────
    public async Task<bool> EliminarAsync(int id)
    {
        await using var conn = await _pool.OpenAsync();
        await using var cmd = new SqlCommand(
            "DELETE FROM servicios_proveedores WHERE id = @id", conn);
        cmd.Parameters.AddWithValue("id", id);
        return await cmd.ExecuteNonQueryAsync() > 0;
    }

    // ── Historial ─────────────────────────────────────────────────────────
    public async Task<List<object>> HistorialAsync(int id)
    {
        await using var conn = await _pool.OpenAsync();
        await using var cmd = new SqlCommand(
            @"SELECT id, usuario, fecha, registro_anterior, registro_nuevo
              FROM servicios_proveedores_historial
              WHERE servicio_id = @id
              ORDER BY fecha DESC", conn);
        cmd.Parameters.AddWithValue("id", id);

        var lista = new List<object>();
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
        {
            lista.Add(new
            {
                ID                = r.GetInt32(0),
                USUARIO           = r.GetString(1),
                FECHA             = r.GetValue(2)?.ToString(),
                REGISTRO_ANTERIOR = r.IsDBNull(3) ? null : r.GetString(3),
                REGISTRO_NUEVO    = r.IsDBNull(4) ? null : r.GetString(4)
            });
        }

        return lista;
    }

    // ── Exportar filtrado ─────────────────────────────────────────────────
    public async Task<byte[]> ExportarAsync(ServicioProveedorFiltros f)
    {
        await using var conn = await _pool.OpenAsync();
        var (where, parms) = ConstruirWhere(f);
        if (string.IsNullOrWhiteSpace(where))
            where = "WHERE (activo IS NULL OR activo = 1)";
        else
            where += " AND (activo IS NULL OR activo = 1)";
        await using var cmd = new SqlCommand(
            $@"SELECT id, id_unico, folio_unico, folio_cotizacion, folio_reporte,
                      fecha, requisitor, cuenta_con_poliza, servicio_con_costo,
                      ubicacion_planta, area, cantidad, descripcion_servicio,
                      descripcion_trabajo, material_equipo, observaciones,
                      proveedores, panel_faceplate, switch, personal_recibio,
                      solicitud_finalizada, costo
               FROM servicios_proveedores {where}
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
        await using var cmd = new SqlCommand(
            @"SELECT id, id_unico, folio_unico, folio_cotizacion, folio_reporte,
                     fecha, requisitor, cuenta_con_poliza, servicio_con_costo,
                     ubicacion_planta, area, cantidad, descripcion_servicio,
                     descripcion_trabajo, material_equipo, observaciones,
                     proveedores, panel_faceplate, switch, personal_recibio,
                     solicitud_finalizada, costo
              FROM servicios_proveedores
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

    private static (string where, List<(string key, object? val)> parms) ConstruirWhere(ServicioProveedorFiltros f)
    {
        var conds = new List<string>();
        var parms = new List<(string, object?)>();
        var idx = 1;

        void Add(string col, string? val)
        {
            if (string.IsNullOrWhiteSpace(val)) return;
            conds.Add($"LOWER({col}) LIKE LOWER(@p{idx})");
            parms.Add(($"p{idx}", $"%{val}%"));
            idx++;
        }

        Add("id_unico",             f.ID_UNICO);
        Add("folio_unico",          f.FOLIO_UNICO);
        Add("folio_cotizacion",     f.FOLIO_COTIZACION);
        Add("folio_reporte",        f.FOLIO_REPORTE);
        Add("fecha",                f.FECHA);
        Add("requisitor",           f.REQUISITOR);
        Add("cuenta_con_poliza",    f.CUENTA_CON_POLIZA);
        Add("servicio_con_costo",   f.SERVICIO_CON_COSTO);
        Add("ubicacion_planta",     f.UBICACION_PLANTA);
        Add("area",                 f.AREA);
        Add("descripcion_servicio", f.DESCRIPCION_SERVICIO);
        Add("proveedores",          f.PROVEEDORES);
        Add("personal_recibio",     f.PERSONAL_RECIBIO);
        Add("solicitud_finalizada", f.SOLICITUD_FINALIZADA);

        var where = conds.Count > 0 ? "WHERE " + string.Join(" AND ", conds) : "";
        return (where, parms);
    }

    private static void AgregarParametros(SqlCommand cmd, ServicioProveedorDto dto)
    {
        cmd.Parameters.AddWithValue("id_unico",             (object?)dto.ID_UNICO             ?? DBNull.Value);
        cmd.Parameters.AddWithValue("folio_unico",          (object?)dto.FOLIO_UNICO          ?? DBNull.Value);
        cmd.Parameters.AddWithValue("folio_cotizacion",     (object?)dto.FOLIO_COTIZACION     ?? DBNull.Value);
        cmd.Parameters.AddWithValue("folio_reporte",        (object?)dto.FOLIO_REPORTE        ?? DBNull.Value);
        cmd.Parameters.AddWithValue("fecha",                (object?)dto.FECHA                ?? DBNull.Value);
        cmd.Parameters.AddWithValue("requisitor",           (object?)dto.REQUISITOR           ?? DBNull.Value);
        cmd.Parameters.AddWithValue("cuenta_con_poliza",    (object?)dto.CUENTA_CON_POLIZA    ?? DBNull.Value);
        cmd.Parameters.AddWithValue("servicio_con_costo",   (object?)dto.SERVICIO_CON_COSTO   ?? DBNull.Value);
        cmd.Parameters.AddWithValue("ubicacion_planta",     (object?)dto.UBICACION_PLANTA     ?? DBNull.Value);
        cmd.Parameters.AddWithValue("area",                 (object?)dto.AREA                 ?? DBNull.Value);
        cmd.Parameters.AddWithValue("cantidad",             (object?)dto.CANTIDAD             ?? DBNull.Value);
        cmd.Parameters.AddWithValue("descripcion_servicio", (object?)dto.DESCRIPCION_SERVICIO ?? DBNull.Value);
        cmd.Parameters.AddWithValue("descripcion_trabajo",  (object?)dto.DESCRIPCION_TRABAJO  ?? DBNull.Value);
        cmd.Parameters.AddWithValue("material_equipo",      (object?)dto.MATERIAL_EQUIPO      ?? DBNull.Value);
        cmd.Parameters.AddWithValue("observaciones",        (object?)dto.OBSERVACIONES        ?? DBNull.Value);
        cmd.Parameters.AddWithValue("proveedores",          (object?)dto.PROVEEDORES          ?? DBNull.Value);
        cmd.Parameters.AddWithValue("panel_faceplate",      (object?)dto.PANEL_FACEPLATE      ?? DBNull.Value);
        cmd.Parameters.AddWithValue("switch",               (object?)dto.SWITCH               ?? DBNull.Value);
        cmd.Parameters.AddWithValue("personal_recibio",     (object?)dto.PERSONAL_RECIBIO     ?? DBNull.Value);
        cmd.Parameters.AddWithValue("solicitud_finalizada", (object?)dto.SOLICITUD_FINALIZADA ?? DBNull.Value);
        cmd.Parameters.AddWithValue("costo",                (object?)dto.COSTO                ?? DBNull.Value);
    }

    private async Task<Dictionary<string, object?>?> SnapshotAsync(SqlConnection conn, int id)
    {
        await using var cmd = new SqlCommand(
            @"SELECT id_unico, folio_unico, folio_cotizacion, folio_reporte, fecha,
                 requisitor, cuenta_con_poliza, servicio_con_costo, ubicacion_planta, area,
                 cantidad, descripcion_servicio, descripcion_trabajo, material_equipo, observaciones,
                 proveedores, panel_faceplate, switch, personal_recibio, solicitud_finalizada, costo
          FROM servicios_proveedores
          WHERE id=@id
          AND (activo IS NULL OR activo = 1)", conn);

        cmd.Parameters.AddWithValue("id", id);

        await using var r = await cmd.ExecuteReaderAsync();
        if (!await r.ReadAsync()) return null;

        var snap = new Dictionary<string, object?>();
        for (int i = 0; i < COLS.Length; i++)
            snap[COLS[i]] = r.IsDBNull(i) ? null : r.GetValue(i)?.ToString();

        return snap;
    }

    private async Task RegistrarHistorialAsync(SqlConnection conn, int servicioId,
        string usuario, Dictionary<string, object?> anterior, Dictionary<string, object?> nuevo)
    {
        var antesJson   = System.Text.Json.JsonSerializer.Serialize(anterior);
        var despuesJson = System.Text.Json.JsonSerializer.Serialize(nuevo);

        await using var cmd = new SqlCommand(
            """
            INSERT INTO servicios_proveedores_historial (servicio_id, usuario, registro_anterior, registro_nuevo)
            VALUES (@sid, @usr, @ant, @nvo)
            """, conn);

        cmd.Parameters.AddWithValue("sid", servicioId);
        cmd.Parameters.AddWithValue("usr", usuario);
        cmd.Parameters.AddWithValue("ant", antesJson);
        cmd.Parameters.AddWithValue("nvo", despuesJson);

        await cmd.ExecuteNonQueryAsync();
    }

    private static byte[] GenerarExcel(List<string?[]> rows)
    {
        ExcelPackage.License.SetNonCommercialPersonal("IT Control Center");
        using var pkg = new ExcelPackage();
        var ws = pkg.Workbook.Worksheets.Add("Servicios Proveedores");

        string[] headers =
        [
            "ID","ID ÚNICO","FOLIO ÚNICO","FOLIO COTIZACIÓN","FOLIO REPORTE",
            "FECHA","REQUISITOR","CUENTA CON PÓLIZA","SERVICIO CON COSTO",
            "UBICACIÓN PLANTA","ÁREA","CANTIDAD","DESCRIPCIÓN SERVICIO",
            "DESCRIPCIÓN TRABAJO","MATERIAL/EQUIPO","OBSERVACIONES",
            "PROVEEDORES","PANEL/FACEPLATE","SWITCH","PERSONAL QUE RECIBIÓ",
            "SOLICITUD FINALIZADA","COSTO"
        ];

        for (int c = 0; c < headers.Length; c++)
        {
            ws.Cells[1, c + 1].Value = headers[c];
            ws.Cells[1, c + 1].Style.Font.Bold = 1;
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

    private static string? Str(SqlDataReader r, int i)
        => r.IsDBNull(i) ? null : r.GetValue(i)?.ToString();
}
