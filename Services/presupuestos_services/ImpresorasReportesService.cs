using ChiIT.Data;
using Npgsql;

using OfficeOpenXml;
using OfficeOpenXml.Style;

using System.Drawing;

namespace ChiIT.Services;

// ════════════════════════════════════════════════════════════════════════════
//  DTOs / Modelos  ── REPORTES
// ════════════════════════════════════════════════════════════════════════════

public class ReporteImpresoraDto
{
    public string?  FOLIO                { get; set; }
    public string?  FECHA                { get; set; }
    public string?  PLANTA               { get; set; }
    public string?  IMPRESORA            { get; set; }
    public string?  AREA                 { get; set; }
    public string?  REPORTE              { get; set; }
    public string?  QUIEN_REPORTA        { get; set; }
    public string?  ESTATUS              { get; set; }
    public string?  FECHA_DE_REALIZACION { get; set; }
    public string?  COMENTARIOS          { get; set; }
}

public class ReporteImpresoraFiltros
{
    public string? FOLIO                { get; set; }
    public string? FECHA                { get; set; }
    public string? PLANTA               { get; set; }
    public string? IMPRESORA            { get; set; }
    public string? AREA                 { get; set; }
    public string? REPORTE              { get; set; }
    public string? QUIEN_REPORTA        { get; set; }
    public string? ESTATUS              { get; set; }
    public string? FECHA_DE_REALIZACION { get; set; }
    public string? COMENTARIOS          { get; set; }
}

// ════════════════════════════════════════════════════════════════════════════
//  DTOs / Modelos  ── INFO IMPRESORAS
// ════════════════════════════════════════════════════════════════════════════

public class ImpresoraInfoDto
{
    public string?  IMPRESORA         { get; set; }
    public string?  MODELO            { get; set; }
    public string?  NUMERO_DE_SERIE   { get; set; }
    public string?  IP                { get; set; }
    public string?  UBICACION         { get; set; }
    public string?  PLANTA            { get; set; }
    public string?  IDENTIFICADOR     { get; set; }
    public int?     NUMERO            { get; set; }
}

public class ImpresoraInfoFiltros
{
    public string? IMPRESORA       { get; set; }
    public string? MODELO          { get; set; }
    public string? NUMERO_DE_SERIE { get; set; }
    public string? IP              { get; set; }
    public string? UBICACION       { get; set; }
    public string? PLANTA          { get; set; }
    public string? IDENTIFICADOR   { get; set; }
}

// ════════════════════════════════════════════════════════════════════════════
//  Servicio
// ════════════════════════════════════════════════════════════════════════════

public class ImpresorasReportesService
{
    private readonly DbConnectionPool _pool;

    private static readonly string[] COLS_REPORTE =
    [
        "FOLIO","FECHA","PLANTA","IMPRESORA","AREA",
        "REPORTE","QUIEN_REPORTA","ESTATUS","FECHA_DE_REALIZACION","COMENTARIOS"
    ];

    private static readonly string[] COLS_INFO =
    [
        "IMPRESORA","MODELO","NUMERO_DE_SERIE","IP",
        "UBICACION","PLANTA","IDENTIFICADOR","NUMERO"
    ];

    public ImpresorasReportesService(DbConnectionPool pool)
    {
        _pool = pool;
        _ = InicializarTablasAsync();
    }

    // ── DDL ───────────────────────────────────────────────────────────────
    private async Task InicializarTablasAsync()
    {
        await using var conn = await _pool.OpenAsync();
        await using var cmd = new NpgsqlCommand("""
            CREATE TABLE IF NOT EXISTS reportes_impresoras (
                id                   SERIAL PRIMARY KEY,
                folio                TEXT,
                fecha                DATE,
                planta               TEXT,
                impresora            TEXT,
                area                 TEXT,
                reporte              TEXT,
                quien_reporta        TEXT,
                estatus              TEXT,
                fecha_de_realizacion DATE,
                comentarios          TEXT
            );

            CREATE TABLE IF NOT EXISTS reportes_impresoras_historial (
                id                SERIAL PRIMARY KEY,
                reporte_id        INTEGER NOT NULL REFERENCES reportes_impresoras(id) ON DELETE CASCADE,
                usuario           TEXT    NOT NULL,
                fecha             TIMESTAMPTZ DEFAULT NOW(),
                registro_anterior JSONB,
                registro_nuevo    JSONB
            );

            CREATE TABLE IF NOT EXISTS impresoras_info (
                id               SERIAL PRIMARY KEY,
                impresora        TEXT,
                modelo           TEXT,
                numero_de_serie  TEXT,
                ip               TEXT,
                ubicacion        TEXT,
                planta           TEXT,
                identificador    TEXT,
                numero           INTEGER
            );

            CREATE TABLE IF NOT EXISTS impresoras_info_historial (
                id                SERIAL PRIMARY KEY,
                impresora_id      INTEGER NOT NULL REFERENCES impresoras_info(id) ON DELETE CASCADE,
                usuario           TEXT    NOT NULL,
                fecha             TIMESTAMPTZ DEFAULT NOW(),
                registro_anterior JSONB,
                registro_nuevo    JSONB
            );
            """, conn);

        await cmd.ExecuteNonQueryAsync();
    }

    // ══════════════════════════════════════════════════════════════════════
    //  REPORTES_IMPRESORAS  ─ CRUD + Historial + Exportar
    // ══════════════════════════════════════════════════════════════════════

    public async Task<object> ListarReportesAsync(int page, int limit, ReporteImpresoraFiltros f)
    {
        await using var conn = await _pool.OpenAsync();

        var (where, parms) = ConstruirWhereReporte(f);
        var offset = (page - 1) * limit;

        await using var cmdCount = new NpgsqlCommand(
            $"SELECT COUNT(*) FROM reportes_impresoras {where}", conn);
        foreach (var (k, v) in parms) cmdCount.Parameters.AddWithValue(k, v ?? (object)DBNull.Value);
        var total = Convert.ToInt64(await cmdCount.ExecuteScalarAsync());

        await using var cmdData = new NpgsqlCommand(
            $@"SELECT id, folio, fecha, planta, impresora,
                      area, reporte, quien_reporta, estatus,
                      fecha_de_realizacion, comentarios
               FROM reportes_impresoras {where}
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
                FOLIO                = Str(reader, 1),
                FECHA                = Str(reader, 2),
                PLANTA               = Str(reader, 3),
                IMPRESORA            = Str(reader, 4),
                AREA                 = Str(reader, 5),
                REPORTE              = Str(reader, 6),
                QUIEN_REPORTA        = Str(reader, 7),
                ESTATUS              = Str(reader, 8),
                FECHA_DE_REALIZACION = Str(reader, 9),
                COMENTARIOS          = Str(reader, 10)
            });
        }

        return new { total, data = lista };
    }

    public async Task<int> CrearReporteAsync(ReporteImpresoraDto dto, string usuario)
    {
        await using var conn = await _pool.OpenAsync();
        await using var cmd = new NpgsqlCommand("""
            INSERT INTO reportes_impresoras
                (folio, fecha, planta, impresora, area,
                 reporte, quien_reporta, estatus, fecha_de_realizacion, comentarios)
            VALUES
                (@folio, @fecha::date, @planta, @impresora, @area,
                 @reporte, @quien_reporta, @estatus, @fecha_de_realizacion::date, @comentarios)
            RETURNING id
            """, conn);

        AgregarParametrosReporte(cmd, dto);
        return Convert.ToInt32(await cmd.ExecuteScalarAsync());
    }

    public async Task<bool> EditarReporteAsync(int id, ReporteImpresoraDto dto, string usuario)
    {
        await using var conn = await _pool.OpenAsync();

        var anterior = await SnapshotReporteAsync(conn, id);
        if (anterior is null) return false;

        await using var cmd = new NpgsqlCommand("""
            UPDATE reportes_impresoras SET
                folio                = @folio,
                fecha                = @fecha::date,
                planta               = @planta,
                impresora            = @impresora,
                area                 = @area,
                reporte              = @reporte,
                quien_reporta        = @quien_reporta,
                estatus              = @estatus,
                fecha_de_realizacion = @fecha_de_realizacion::date,
                comentarios          = @comentarios
            WHERE id = @id
            """, conn);

        AgregarParametrosReporte(cmd, dto);
        cmd.Parameters.AddWithValue("id", id);
        var rows = await cmd.ExecuteNonQueryAsync();
        if (rows == 0) return false;

        var nuevo = await SnapshotReporteAsync(conn, id);
        await RegistrarHistorialReporteAsync(conn, id, usuario, anterior, nuevo!);
        return true;
    }

    public async Task<bool> EliminarReporteAsync(int id)
    {
        await using var conn = await _pool.OpenAsync();
        await using var cmd = new NpgsqlCommand(
            "DELETE FROM reportes_impresoras WHERE id = @id", conn);
        cmd.Parameters.AddWithValue("id", id);
        return await cmd.ExecuteNonQueryAsync() > 0;
    }

    public async Task<List<object>> HistorialReporteAsync(int id)
    {
        await using var conn = await _pool.OpenAsync();
        await using var cmd = new NpgsqlCommand(
            @"SELECT id, usuario, fecha, registro_anterior, registro_nuevo
              FROM reportes_impresoras_historial
              WHERE reporte_id = @id
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
                FECHA             = r.GetDateTime(2).ToString("yyyy-MM-dd HH:mm:ss"),
                REGISTRO_ANTERIOR = r.IsDBNull(3) ? null : r.GetString(3),
                REGISTRO_NUEVO    = r.IsDBNull(4) ? null : r.GetString(4)
            });
        }
        return lista;
    }

    public async Task<byte[]> ExportarReportesAsync(ReporteImpresoraFiltros f)
    {
        await using var conn = await _pool.OpenAsync();
        var (where, parms) = ConstruirWhereReporte(f);

        await using var cmd = new NpgsqlCommand(
            $@"SELECT id, folio, fecha, planta, impresora,
                      area, reporte, quien_reporta, estatus,
                      fecha_de_realizacion, comentarios
               FROM reportes_impresoras {where} ORDER BY id DESC", conn);

        foreach (var (k, v) in parms) cmd.Parameters.AddWithValue(k, v ?? (object)DBNull.Value);

        var rows = new List<string?[]>();
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
            rows.Add(Enumerable.Range(0, 11).Select(i => r.IsDBNull(i) ? null : r.GetValue(i)?.ToString()).ToArray());

        return GenerarExcelReportes(rows);
    }

    public async Task<byte[]> ExportarReportesPorAnioAsync(int anio)
    {
        await using var conn = await _pool.OpenAsync();
        await using var cmd = new NpgsqlCommand(
            @"SELECT id, folio, fecha, planta, impresora,
                     area, reporte, quien_reporta, estatus,
                     fecha_de_realizacion, comentarios
              FROM reportes_impresoras
              WHERE EXTRACT(YEAR FROM fecha) = @anio
              ORDER BY id DESC", conn);
        cmd.Parameters.AddWithValue("anio", anio);

        var rows = new List<string?[]>();
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
            rows.Add(Enumerable.Range(0, 11).Select(i => r.IsDBNull(i) ? null : r.GetValue(i)?.ToString()).ToArray());

        return GenerarExcelReportes(rows);
    }

    // ══════════════════════════════════════════════════════════════════════
    //  IMPRESORAS_INFO  ─ CRUD + Historial + Exportar
    // ══════════════════════════════════════════════════════════════════════

    public async Task<object> ListarInfoAsync(int page, int limit, ImpresoraInfoFiltros f)
    {
        await using var conn = await _pool.OpenAsync();

        var (where, parms) = ConstruirWhereInfo(f);
        var offset = (page - 1) * limit;

        await using var cmdCount = new NpgsqlCommand(
            $"SELECT COUNT(*) FROM impresoras_info {where}", conn);
        foreach (var (k, v) in parms) cmdCount.Parameters.AddWithValue(k, v ?? (object)DBNull.Value);
        var total = Convert.ToInt64(await cmdCount.ExecuteScalarAsync());

        await using var cmdData = new NpgsqlCommand(
            $@"SELECT id, impresora, modelo, numero_de_serie, ip,
                      ubicacion, planta, identificador, numero
               FROM impresoras_info {where}
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
                ID              = reader.GetInt32(0),
                IMPRESORA       = Str(reader, 1),
                MODELO          = Str(reader, 2),
                NUMERO_DE_SERIE = Str(reader, 3),
                IP              = Str(reader, 4),
                UBICACION       = Str(reader, 5),
                PLANTA          = Str(reader, 6),
                IDENTIFICADOR   = Str(reader, 7),
                NUMERO          = reader.IsDBNull(8) ? (int?)null : reader.GetInt32(8)
            });
        }

        return new { total, data = lista };
    }

    public async Task<int> CrearInfoAsync(ImpresoraInfoDto dto, string usuario)
    {
        await using var conn = await _pool.OpenAsync();
        await using var cmd = new NpgsqlCommand("""
            INSERT INTO impresoras_info
                (impresora, modelo, numero_de_serie, ip,
                 ubicacion, planta, identificador, numero)
            VALUES
                (@impresora, @modelo, @numero_de_serie, @ip,
                 @ubicacion, @planta, @identificador, @numero)
            RETURNING id
            """, conn);

        AgregarParametrosInfo(cmd, dto);
        return Convert.ToInt32(await cmd.ExecuteScalarAsync());
    }

    public async Task<bool> EditarInfoAsync(int id, ImpresoraInfoDto dto, string usuario)
    {
        await using var conn = await _pool.OpenAsync();

        var anterior = await SnapshotInfoAsync(conn, id);
        if (anterior is null) return false;

        await using var cmd = new NpgsqlCommand("""
            UPDATE impresoras_info SET
                impresora       = @impresora,
                modelo          = @modelo,
                numero_de_serie = @numero_de_serie,
                ip              = @ip,
                ubicacion       = @ubicacion,
                planta          = @planta,
                identificador   = @identificador,
                numero          = @numero
            WHERE id = @id
            """, conn);

        AgregarParametrosInfo(cmd, dto);
        cmd.Parameters.AddWithValue("id", id);
        var rows = await cmd.ExecuteNonQueryAsync();
        if (rows == 0) return false;

        var nuevo = await SnapshotInfoAsync(conn, id);
        await RegistrarHistorialInfoAsync(conn, id, usuario, anterior, nuevo!);
        return true;
    }

    public async Task<bool> EliminarInfoAsync(int id)
    {
        await using var conn = await _pool.OpenAsync();
        await using var cmd = new NpgsqlCommand(
            "DELETE FROM impresoras_info WHERE id = @id", conn);
        cmd.Parameters.AddWithValue("id", id);
        return await cmd.ExecuteNonQueryAsync() > 0;
    }

    public async Task<List<object>> HistorialInfoAsync(int id)
    {
        await using var conn = await _pool.OpenAsync();
        await using var cmd = new NpgsqlCommand(
            @"SELECT id, usuario, fecha, registro_anterior, registro_nuevo
              FROM impresoras_info_historial
              WHERE impresora_id = @id
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
                FECHA             = r.GetDateTime(2).ToString("yyyy-MM-dd HH:mm:ss"),
                REGISTRO_ANTERIOR = r.IsDBNull(3) ? null : r.GetString(3),
                REGISTRO_NUEVO    = r.IsDBNull(4) ? null : r.GetString(4)
            });
        }
        return lista;
    }

    public async Task<byte[]> ExportarInfoAsync(ImpresoraInfoFiltros f)
    {
        await using var conn = await _pool.OpenAsync();
        var (where, parms) = ConstruirWhereInfo(f);

        await using var cmd = new NpgsqlCommand(
            $@"SELECT id, impresora, modelo, numero_de_serie, ip,
                      ubicacion, planta, identificador, numero
               FROM impresoras_info {where} ORDER BY id DESC", conn);

        foreach (var (k, v) in parms) cmd.Parameters.AddWithValue(k, v ?? (object)DBNull.Value);

        var rows = new List<string?[]>();
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
            rows.Add(Enumerable.Range(0, 9).Select(i => r.IsDBNull(i) ? null : r.GetValue(i)?.ToString()).ToArray());

        return GenerarExcelInfo(rows);
    }

    // ── Helpers privados ── WHERE builders ───────────────────────────────

    private static (string where, List<(string key, object? val)> parms) ConstruirWhereReporte(ReporteImpresoraFiltros f)
    {
        var conds = new List<string>();

        conds.Add("(activo IS NULL OR activo = true)");

        var parms = new List<(string, object?)>();
        var idx = 1;

        void Add(string col, string? val)
        {
            if (string.IsNullOrWhiteSpace(val)) return;
            conds.Add($"LOWER({col}::TEXT) LIKE LOWER(@p{idx})");
            parms.Add(($"p{idx}", $"%{val}%"));
            idx++;
        }

        Add("folio",                f.FOLIO);
        Add("fecha",                f.FECHA);
        Add("planta",               f.PLANTA);
        Add("impresora",            f.IMPRESORA);
        Add("area",                 f.AREA);
        Add("reporte",              f.REPORTE);
        Add("quien_reporta",        f.QUIEN_REPORTA);
        Add("estatus",              f.ESTATUS);
        Add("fecha_de_realizacion", f.FECHA_DE_REALIZACION);
        Add("comentarios",          f.COMENTARIOS);

        var where = conds.Count > 0 ? "WHERE " + string.Join(" AND ", conds) : "";
        return (where, parms);
    }

    private static (string where, List<(string key, object? val)> parms) ConstruirWhereInfo(ImpresoraInfoFiltros f)
    {
        var conds = new List<string>();

        conds.Add("(activo IS NULL OR activo = true)");

        var parms = new List<(string, object?)>();
        var idx = 1;

        void Add(string col, string? val)
        {
            if (string.IsNullOrWhiteSpace(val)) return;
            conds.Add($"LOWER({col}::TEXT) LIKE LOWER(@p{idx})");
            parms.Add(($"p{idx}", $"%{val}%"));
            idx++;
        }

        Add("impresora",       f.IMPRESORA);
        Add("modelo",          f.MODELO);
        Add("numero_de_serie", f.NUMERO_DE_SERIE);
        Add("ip",              f.IP);
        Add("ubicacion",       f.UBICACION);
        Add("planta",          f.PLANTA);
        Add("identificador",   f.IDENTIFICADOR);

        var where = conds.Count > 0 ? "WHERE " + string.Join(" AND ", conds) : "";
        return (where, parms);
    }

    // ── Helpers privados ── AddParameters ────────────────────────────────

    private static void AgregarParametrosReporte(NpgsqlCommand cmd, ReporteImpresoraDto dto)
    {
        cmd.Parameters.AddWithValue("folio",                (object?)dto.FOLIO                ?? DBNull.Value);
        cmd.Parameters.AddWithValue("fecha",                (object?)dto.FECHA                ?? DBNull.Value);
        cmd.Parameters.AddWithValue("planta",               (object?)dto.PLANTA               ?? DBNull.Value);
        cmd.Parameters.AddWithValue("impresora",            (object?)dto.IMPRESORA            ?? DBNull.Value);
        cmd.Parameters.AddWithValue("area",                 (object?)dto.AREA                 ?? DBNull.Value);
        cmd.Parameters.AddWithValue("reporte",              (object?)dto.REPORTE              ?? DBNull.Value);
        cmd.Parameters.AddWithValue("quien_reporta",        (object?)dto.QUIEN_REPORTA        ?? DBNull.Value);
        cmd.Parameters.AddWithValue("estatus",              (object?)dto.ESTATUS              ?? DBNull.Value);
        cmd.Parameters.AddWithValue("fecha_de_realizacion", (object?)dto.FECHA_DE_REALIZACION ?? DBNull.Value);
        cmd.Parameters.AddWithValue("comentarios",          (object?)dto.COMENTARIOS          ?? DBNull.Value);
    }

    private static void AgregarParametrosInfo(NpgsqlCommand cmd, ImpresoraInfoDto dto)
    {
        cmd.Parameters.AddWithValue("impresora",       (object?)dto.IMPRESORA       ?? DBNull.Value);
        cmd.Parameters.AddWithValue("modelo",          (object?)dto.MODELO          ?? DBNull.Value);
        cmd.Parameters.AddWithValue("numero_de_serie", (object?)dto.NUMERO_DE_SERIE ?? DBNull.Value);
        cmd.Parameters.AddWithValue("ip",              (object?)dto.IP              ?? DBNull.Value);
        cmd.Parameters.AddWithValue("ubicacion",       (object?)dto.UBICACION       ?? DBNull.Value);
        cmd.Parameters.AddWithValue("planta",          (object?)dto.PLANTA          ?? DBNull.Value);
        cmd.Parameters.AddWithValue("identificador",   (object?)dto.IDENTIFICADOR   ?? DBNull.Value);
        cmd.Parameters.AddWithValue("numero",          (object?)dto.NUMERO          ?? DBNull.Value);
    }

    // ── Helpers privados ── Snapshots ─────────────────────────────────────

    private async Task<Dictionary<string, object?>?> SnapshotReporteAsync(NpgsqlConnection conn, int id)
    {
        await using var cmd = new NpgsqlCommand(
            @"SELECT folio, fecha, planta, impresora, area,
                     reporte, quien_reporta, estatus, fecha_de_realizacion, comentarios
              FROM reportes_impresoras WHERE id=@id", conn);
        cmd.Parameters.AddWithValue("id", id);

        await using var r = await cmd.ExecuteReaderAsync();
        if (!await r.ReadAsync()) return null;

        var snap = new Dictionary<string, object?>();
        for (int i = 0; i < COLS_REPORTE.Length; i++)
            snap[COLS_REPORTE[i]] = r.IsDBNull(i) ? null : r.GetValue(i)?.ToString();
        return snap;
    }

    private async Task<Dictionary<string, object?>?> SnapshotInfoAsync(NpgsqlConnection conn, int id)
    {
        await using var cmd = new NpgsqlCommand(
            @"SELECT impresora, modelo, numero_de_serie, ip,
                     ubicacion, planta, identificador, numero
              FROM impresoras_info WHERE id=@id", conn);
        cmd.Parameters.AddWithValue("id", id);

        await using var r = await cmd.ExecuteReaderAsync();
        if (!await r.ReadAsync()) return null;

        var snap = new Dictionary<string, object?>();
        for (int i = 0; i < COLS_INFO.Length; i++)
            snap[COLS_INFO[i]] = r.IsDBNull(i) ? null : r.GetValue(i)?.ToString();
        return snap;
    }

    // ── Helpers privados ── Historial writers ─────────────────────────────

    private async Task RegistrarHistorialReporteAsync(NpgsqlConnection conn, int reporteId,
        string usuario, Dictionary<string, object?> anterior, Dictionary<string, object?> nuevo)
    {
        var antesJson   = System.Text.Json.JsonSerializer.Serialize(anterior);
        var despuesJson = System.Text.Json.JsonSerializer.Serialize(nuevo);

        await using var cmd = new NpgsqlCommand("""
            INSERT INTO reportes_impresoras_historial (reporte_id, usuario, registro_anterior, registro_nuevo)
            VALUES (@rid, @usr, @ant::jsonb, @nvo::jsonb)
            """, conn);

        cmd.Parameters.AddWithValue("rid", reporteId);
        cmd.Parameters.AddWithValue("usr", usuario);
        cmd.Parameters.AddWithValue("ant", antesJson);
        cmd.Parameters.AddWithValue("nvo", despuesJson);

        await cmd.ExecuteNonQueryAsync();
    }

    private async Task RegistrarHistorialInfoAsync(NpgsqlConnection conn, int impresoraId,
        string usuario, Dictionary<string, object?> anterior, Dictionary<string, object?> nuevo)
    {
        var antesJson   = System.Text.Json.JsonSerializer.Serialize(anterior);
        var despuesJson = System.Text.Json.JsonSerializer.Serialize(nuevo);

        await using var cmd = new NpgsqlCommand("""
            INSERT INTO impresoras_info_historial (impresora_id, usuario, registro_anterior, registro_nuevo)
            VALUES (@iid, @usr, @ant::jsonb, @nvo::jsonb)
            """, conn);

        cmd.Parameters.AddWithValue("iid", impresoraId);
        cmd.Parameters.AddWithValue("usr", usuario);
        cmd.Parameters.AddWithValue("ant", antesJson);
        cmd.Parameters.AddWithValue("nvo", despuesJson);

        await cmd.ExecuteNonQueryAsync();
    }

    // ── Helpers privados ── Excel generators ──────────────────────────────

    private static byte[] GenerarExcelReportes(List<string?[]> rows)
    {
        ExcelPackage.License.SetNonCommercialPersonal("IT Control Center");
        using var pkg = new ExcelPackage();
        var ws = pkg.Workbook.Worksheets.Add("Reportes Impresoras");

        string[] headers =
        [
            "ID","FOLIO","FECHA","PLANTA","IMPRESORA",
            "ÁREA","REPORTE","QUIEN REPORTA","ESTATUS",
            "FECHA DE REALIZACIÓN","COMENTARIOS"
        ];

        EstilizarHeaders(ws, headers);

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

    private static byte[] GenerarExcelInfo(List<string?[]> rows)
    {
        ExcelPackage.License.SetNonCommercialPersonal("IT Control Center");
        using var pkg = new ExcelPackage();
        var ws = pkg.Workbook.Worksheets.Add("Impresoras Info");

        string[] headers =
        [
            "ID","IMPRESORA","MODELO","NO. SERIE","IP",
            "UBICACIÓN","PLANTA","IDENTIFICADOR","NÚMERO"
        ];

        EstilizarHeaders(ws, headers);

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

    private static void EstilizarHeaders(ExcelWorksheet ws, string[] headers)
    {
        for (int c = 0; c < headers.Length; c++)
        {
            ws.Cells[1, c + 1].Value = headers[c];
            ws.Cells[1, c + 1].Style.Font.Bold = true;
            ws.Cells[1, c + 1].Style.Fill.PatternType = ExcelFillStyle.Solid;
            ws.Cells[1, c + 1].Style.Fill.BackgroundColor.SetColor(Color.FromArgb(30, 41, 59));
            ws.Cells[1, c + 1].Style.Font.Color.SetColor(Color.White);
            ws.Cells[1, c + 1].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
        }
    }

    private static string? Str(NpgsqlDataReader r, int i)
        => r.IsDBNull(i) ? null : r.GetValue(i)?.ToString();
}
