using ChiIT.Data;
using Microsoft.Data.SqlClient;

using OfficeOpenXml;
using OfficeOpenXml.Style;

using System.Drawing;

namespace ChiIT.Services;

// ── DTOs / Modelos ────────────────────────────────────────────────────────────

public class RemisionDto
{
    public string?  ID_OC                       { get; set; }
    public string?  ID_REMISION                 { get; set; }
    public string?  FOLIO                       { get; set; }
    public string?  SOLICITANTE                 { get; set; }
    public string?  FECHA_SOLICITUD             { get; set; }
    public string?  ACCESORIO_SOLICITADO        { get; set; }
    public string?  MODELO_SERIE_COMENTARIOS    { get; set; }
    public string?  PROVEEDOR                   { get; set; }
    public string?  PIEZA_SERVICIO              { get; set; }
    public int?     CANTIDAD                    { get; set; }
    public decimal? PRECIO_UNITARIO             { get; set; }
    public decimal? TOTAL_SIN_IVA               { get; set; }
    public string?  MONEDA                      { get; set; }
    public bool?    PAGADO                      { get; set; }
    public string?  PRESUPUESTO                 { get; set; }
    public string?  CUENTA_A_DESCONTAR          { get; set; }
    public string?  FECHA_ENTRADA_PLANTA        { get; set; }
    public string?  STATUS                      { get; set; }
    public string?  REQUISICION                 { get; set; }
    public string?  OC                          { get; set; }
}

public class RemisionFiltros
{
    public string? ID_OC                    { get; set; }
    public string? ID_REMISION              { get; set; }
    public string? FOLIO                    { get; set; }
    public string? SOLICITANTE              { get; set; }
    public string? FECHA_SOLICITUD          { get; set; }
    public string? ACCESORIO_SOLICITADO     { get; set; }
    public string? MODELO_SERIE_COMENTARIOS { get; set; }
    public string? PROVEEDOR               { get; set; }
    public string? PIEZA_SERVICIO          { get; set; }
    public string? MONEDA                  { get; set; }
    public string? PAGADO                  { get; set; }
    public string? PRESUPUESTO             { get; set; }
    public string? CUENTA_A_DESCONTAR      { get; set; }
    public string? FECHA_ENTRADA_PLANTA    { get; set; }
    public string? STATUS                  { get; set; }
    public string? REQUISICION             { get; set; }
    public string? OC                      { get; set; }
}

// ── Servicio ──────────────────────────────────────────────────────────────────

public class RemisionesService
{
    private readonly DbConnectionPool _pool;

    private static readonly string[] COLS =
    [
        "ID_OC","ID_REMISION","FOLIO","SOLICITANTE","FECHA_SOLICITUD",
        "ACCESORIO_SOLICITADO","MODELO_SERIE_COMENTARIOS","PROVEEDOR","PIEZA_SERVICIO",
        "CANTIDAD","PRECIO_UNITARIO","TOTAL_SIN_IVA","MONEDA","PAGADO",
        "PRESUPUESTO","CUENTA_A_DESCONTAR","FECHA_ENTRADA_PLANTA","STATUS","REQUISICION","OC"
    ];

    public RemisionesService(DbConnectionPool pool)
    {
        _pool = pool;
        _ = InicializarTablaAsync();
    }

    // ── DDL ───────────────────────────────────────────────────────────────
    private async Task InicializarTablaAsync()
    {
        await using var conn = await _pool.OpenAsync();
        await using var cmd = new SqlCommand("""
            CREATE TABLE IF NOT EXISTS remisiones (
                id                          SERIAL PRIMARY KEY,
                id_oc                       TEXT,
                id_remision                 TEXT,
                folio                       TEXT,
                solicitante                 TEXT,
                fecha_solicitud             DATE,
                accesorio_solicitado        TEXT,
                modelo_serie_comentarios    TEXT,
                proveedor                   TEXT,
                pieza_servicio              TEXT,
                cantidad                    INTEGER,
                precio_unitario             NUMERIC(12,2),
                total_sin_iva               NUMERIC(14,2),
                moneda                      TEXT,
                pagado                      BOOLEAN,
                presupuesto                 TEXT,
                cuenta_a_descontar          TEXT,
                fecha_entrada_planta        DATE,
                status                      TEXT,
                requisicion                 TEXT,
                oc                          TEXT
            );

            CREATE TABLE IF NOT EXISTS remisiones_historial (
                id                SERIAL PRIMARY KEY,
                remision_id       INTEGER NOT NULL REFERENCES remisiones(id) ON DELETE CASCADE,
                usuario           TEXT    NOT NULL,
                fecha             TIMESTAMPTZ DEFAULT GETDATE(),
                registro_anterior JSONB,
                registro_nuevo    JSONB
            );
            """, conn);

        await cmd.ExecuteNonQueryAsync();
    }

    // ── Listar (paginado + filtros) ───────────────────────────────────────
    public async Task<object> ListarAsync(int page, int limit, RemisionFiltros f)
    {
        await using var conn = await _pool.OpenAsync();

        var (where, parms) = ConstruirWhere(f);
        var offset = (page - 1) * limit;

        // Total
        await using var cmdCount = new SqlCommand(
            $"SELECT COUNT(*) FROM remisiones {where}", conn);
        foreach (var (k, v) in parms) cmdCount.Parameters.AddWithValue(k, v ?? (object)DBNull.Value);
        var total = Convert.ToInt64(await cmdCount.ExecuteScalarAsync());

        // Datos
        await using var cmdData = new SqlCommand(
            $@"SELECT id, id_oc, id_remision, folio, solicitante,
                      fecha_solicitud, accesorio_solicitado, modelo_serie_comentarios,
                      proveedor, pieza_servicio, cantidad, precio_unitario,
                      total_sin_iva, moneda, pagado, presupuesto,
                      cuenta_a_descontar, fecha_entrada_planta, status, requisicion, oc
               FROM remisiones {where}
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
                ID                       = reader.GetInt32(0),
                ID_OC                    = Str(reader, 1),
                ID_REMISION              = Str(reader, 2),
                FOLIO                    = Str(reader, 3),
                SOLICITANTE              = Str(reader, 4),
                FECHA_SOLICITUD          = Str(reader, 5),
                ACCESORIO_SOLICITADO     = Str(reader, 6),
                MODELO_SERIE_COMENTARIOS = Str(reader, 7),
                PROVEEDOR                = Str(reader, 8),
                PIEZA_SERVICIO           = Str(reader, 9),
                CANTIDAD                 = reader.IsDBNull(10) ? (int?)null     : reader.GetInt32(10),
                PRECIO_UNITARIO          = reader.IsDBNull(11) ? (decimal?)null : reader.GetDecimal(11),
                TOTAL_SIN_IVA            = reader.IsDBNull(12) ? (decimal?)null : reader.GetDecimal(12),
                MONEDA                   = Str(reader, 13),
                PAGADO                   = reader.IsDBNull(14) ? (bool?)null    : reader.GetBoolean(14),
                PRESUPUESTO              = Str(reader, 15),
                CUENTA_A_DESCONTAR       = Str(reader, 16),
                FECHA_ENTRADA_PLANTA     = Str(reader, 17),
                STATUS                   = Str(reader, 18),
                REQUISICION              = Str(reader, 19),
                OC                       = Str(reader, 20),
            });
        }

        return new { total, page, limit, data = lista };
    }

    // ── Crear ─────────────────────────────────────────────────────────────
    public async Task<int> CrearAsync(RemisionDto dto, string usuario)
    {
        await using var conn = await _pool.OpenAsync();
        await using var cmd = new SqlCommand(
            @"INSERT INTO remisiones
                (id_oc, id_remision, folio, solicitante, fecha_solicitud,
                 accesorio_solicitado, modelo_serie_comentarios, proveedor, pieza_servicio,
                 cantidad, precio_unitario, total_sin_iva, moneda, pagado,
                 presupuesto, cuenta_a_descontar, fecha_entrada_planta, status, requisicion, oc)
              VALUES
                (@id_oc, @id_remision, @folio, @solicitante, @fecha_solicitud,
                 @accesorio_solicitado, @modelo_serie_comentarios, @proveedor, @pieza_servicio,
                 @cantidad, @precio_unitario, @total_sin_iva, @moneda, @pagado,
                 @presupuesto, @cuenta_a_descontar, @fecha_entrada_planta, @status, @requisicion, @oc)
              OUTPUT INSERTED.id", conn);

        AgregarParametros(cmd, dto);
        var newId = Convert.ToInt32(await cmd.ExecuteScalarAsync());

        // Historial: registro nuevo
        var snap = await SnapshotAsync(conn, newId);
        if (snap is not null)
            await RegistrarHistorialAsync(conn, newId, usuario,
                new Dictionary<string, object?>(), snap);

        return newId;
    }

    // ── Editar ────────────────────────────────────────────────────────────
    public async Task<bool> EditarAsync(int id, RemisionDto dto, string usuario)
    {
        await using var conn = await _pool.OpenAsync();

        var anterior = await SnapshotAsync(conn, id);
        if (anterior is null) return false;

        await using var cmd = new SqlCommand(
            @"UPDATE remisiones SET
                id_oc                    = @id_oc,
                id_remision              = @id_remision,
                folio                    = @folio,
                solicitante              = @solicitante,
                fecha_solicitud          = @fecha_solicitud,
                accesorio_solicitado     = @accesorio_solicitado,
                modelo_serie_comentarios = @modelo_serie_comentarios,
                proveedor                = @proveedor,
                pieza_servicio           = @pieza_servicio,
                cantidad                 = @cantidad,
                precio_unitario          = @precio_unitario,
                total_sin_iva            = @total_sin_iva,
                moneda                   = @moneda,
                pagado                   = @pagado,
                presupuesto              = @presupuesto,
                cuenta_a_descontar       = @cuenta_a_descontar,
                fecha_entrada_planta     = @fecha_entrada_planta,
                status                   = @status,
                requisicion              = @requisicion,
                oc                       = @oc
              WHERE id = @id", conn);

        AgregarParametros(cmd, dto);
        cmd.Parameters.AddWithValue("id", id);
        await cmd.ExecuteNonQueryAsync();

        var nuevo = await SnapshotAsync(conn, id);
        if (nuevo is not null)
            await RegistrarHistorialAsync(conn, id, usuario, anterior, nuevo);

        return true;
    }

    // ── Eliminar ──────────────────────────────────────────────────────────
    public async Task<bool> EliminarAsync(int id)
    {
        await using var conn = await _pool.OpenAsync();
        await using var cmd = new SqlCommand(
            "DELETE FROM remisiones WHERE id = @id", conn);
        cmd.Parameters.AddWithValue("id", id);
        return await cmd.ExecuteNonQueryAsync() > 0;
    }

    // ── Historial ─────────────────────────────────────────────────────────
    public async Task<List<object>> HistorialAsync(int id)
    {
        await using var conn = await _pool.OpenAsync();
        await using var cmd = new SqlCommand(
            @"SELECT id, usuario, fecha, registro_anterior, registro_nuevo
              FROM remisiones_historial
              WHERE remision_id = @id
              ORDER BY fecha DESC", conn);
        cmd.Parameters.AddWithValue("id", id);

        var lista = new List<object>();
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
        {
            lista.Add(new
            {
                ID                = r.GetInt32(0),
                USUARIO           = Str(r, 1),
                FECHA             = Str(r, 2),
                REGISTRO_ANTERIOR = Str(r, 3),
                REGISTRO_NUEVO    = Str(r, 4),
            });
        }
        return lista;
    }

    // ── Exportar (filtrado) ───────────────────────────────────────────────
    public async Task<byte[]> ExportarAsync(RemisionFiltros f)
    {
        await using var conn = await _pool.OpenAsync();
        var (where, parms) = ConstruirWhere(f);

        await using var cmd = new SqlCommand(
            $@"SELECT id, id_oc, id_remision, folio, solicitante,
                      fecha_solicitud, accesorio_solicitado, modelo_serie_comentarios,
                      proveedor, pieza_servicio, cantidad, precio_unitario,
                      total_sin_iva, moneda, pagado, presupuesto,
                      cuenta_a_descontar, fecha_entrada_planta, status, requisicion, oc
               FROM remisiones {where}
               ORDER BY id DESC", conn);

        foreach (var (k, v) in parms) cmd.Parameters.AddWithValue(k, v ?? (object)DBNull.Value);

        var rows = new List<string?[]>();
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
        {
            rows.Add(Enumerable.Range(0, 21)
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
            @"SELECT id, id_oc, id_remision, folio, solicitante,
                     fecha_solicitud, accesorio_solicitado, modelo_serie_comentarios,
                     proveedor, pieza_servicio, cantidad, precio_unitario,
                     total_sin_iva, moneda, pagado, presupuesto,
                     cuenta_a_descontar, fecha_entrada_planta, status, requisicion, oc
              FROM remisiones
              WHERE EXTRACT(YEAR FROM fecha_solicitud) = @anio
              AND (activo IS NULL OR activo = 1)
              ORDER BY id DESC", conn);
        cmd.Parameters.AddWithValue("anio", anio);

        var rows = new List<string?[]>();
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
        {
            rows.Add(Enumerable.Range(0, 21)
                .Select(i => r.IsDBNull(i) ? null : r.GetValue(i)?.ToString())
                .ToArray());
        }

        return GenerarExcel(rows);
    }

    // ── Helpers privados ──────────────────────────────────────────────────

    private static (string where, List<(string key, object? val)> parms) ConstruirWhere(RemisionFiltros f)
    {
        var conds = new List<string>();

        conds.Add("(activo IS NULL OR activo = 1)");

        var parms = new List<(string, object?)>();
        var idx = 1;

        void Add(string col, string? val)
        {
            if (string.IsNullOrWhiteSpace(val)) return;
            conds.Add($"LOWER({col}) LIKE LOWER(@p{idx})");
            parms.Add(($"p{idx}", $"%{val}%"));
            idx++;
        }

        Add("id_oc",                    f.ID_OC);
        Add("id_remision",              f.ID_REMISION);
        Add("folio",                    f.FOLIO);
        Add("solicitante",              f.SOLICITANTE);
        Add("fecha_solicitud",          f.FECHA_SOLICITUD);
        Add("accesorio_solicitado",     f.ACCESORIO_SOLICITADO);
        Add("modelo_serie_comentarios", f.MODELO_SERIE_COMENTARIOS);
        Add("proveedor",                f.PROVEEDOR);
        Add("pieza_servicio",           f.PIEZA_SERVICIO);
        Add("moneda",                   f.MONEDA);
        Add("pagado",             f.PAGADO);
        Add("presupuesto",              f.PRESUPUESTO);
        Add("cuenta_a_descontar",       f.CUENTA_A_DESCONTAR);
        Add("fecha_entrada_planta",     f.FECHA_ENTRADA_PLANTA);
        Add("status",                   f.STATUS);
        Add("requisicion",              f.REQUISICION);
        Add("oc",                       f.OC);

        var where = conds.Count > 0 ? "WHERE " + string.Join(" AND ", conds) : "";
        return (where, parms);
    }

    private static void AgregarParametros(SqlCommand cmd, RemisionDto dto)
    {
        cmd.Parameters.AddWithValue("id_oc",                    (object?)dto.ID_OC                    ?? DBNull.Value);
        cmd.Parameters.AddWithValue("id_remision",              (object?)dto.ID_REMISION              ?? DBNull.Value);
        cmd.Parameters.AddWithValue("folio",                    (object?)dto.FOLIO                    ?? DBNull.Value);
        cmd.Parameters.AddWithValue("solicitante",              (object?)dto.SOLICITANTE              ?? DBNull.Value);
        cmd.Parameters.AddWithValue("fecha_solicitud",          (object?)dto.FECHA_SOLICITUD          ?? DBNull.Value);
        cmd.Parameters.AddWithValue("accesorio_solicitado",     (object?)dto.ACCESORIO_SOLICITADO     ?? DBNull.Value);
        cmd.Parameters.AddWithValue("modelo_serie_comentarios", (object?)dto.MODELO_SERIE_COMENTARIOS ?? DBNull.Value);
        cmd.Parameters.AddWithValue("proveedor",                (object?)dto.PROVEEDOR                ?? DBNull.Value);
        cmd.Parameters.AddWithValue("pieza_servicio",           (object?)dto.PIEZA_SERVICIO           ?? DBNull.Value);
        cmd.Parameters.AddWithValue("cantidad",                 (object?)dto.CANTIDAD                 ?? DBNull.Value);
        cmd.Parameters.AddWithValue("precio_unitario",          (object?)dto.PRECIO_UNITARIO          ?? DBNull.Value);
        cmd.Parameters.AddWithValue("total_sin_iva",            (object?)dto.TOTAL_SIN_IVA            ?? DBNull.Value);
        cmd.Parameters.AddWithValue("moneda",                   (object?)dto.MONEDA                   ?? DBNull.Value);
        cmd.Parameters.AddWithValue("pagado",                   (object?)dto.PAGADO                   ?? DBNull.Value);
        cmd.Parameters.AddWithValue("presupuesto",              (object?)dto.PRESUPUESTO              ?? DBNull.Value);
        cmd.Parameters.AddWithValue("cuenta_a_descontar",       (object?)dto.CUENTA_A_DESCONTAR       ?? DBNull.Value);
        cmd.Parameters.AddWithValue("fecha_entrada_planta",     (object?)dto.FECHA_ENTRADA_PLANTA     ?? DBNull.Value);
        cmd.Parameters.AddWithValue("status",                   (object?)dto.STATUS                   ?? DBNull.Value);
        cmd.Parameters.AddWithValue("requisicion",              (object?)dto.REQUISICION              ?? DBNull.Value);
        cmd.Parameters.AddWithValue("oc",                       (object?)dto.OC                       ?? DBNull.Value);
    }

    private async Task<Dictionary<string, object?>?> SnapshotAsync(SqlConnection conn, int id)
    {
        await using var cmd = new SqlCommand(
            @"SELECT id_oc, id_remision, folio, solicitante, fecha_solicitud,
             accesorio_solicitado, modelo_serie_comentarios, proveedor, pieza_servicio,
             cantidad, precio_unitario, total_sin_iva, moneda, pagado,
             presupuesto, cuenta_a_descontar, fecha_entrada_planta, status, requisicion, oc
          FROM remisiones
          WHERE id = @id
          AND (activo IS NULL OR activo = 1)", conn);

        cmd.Parameters.AddWithValue("id", id);

        await using var r = await cmd.ExecuteReaderAsync();
        if (!await r.ReadAsync()) return null;

        var snap = new Dictionary<string, object?>();
        for (int i = 0; i < COLS.Length; i++)
            snap[COLS[i]] = r.IsDBNull(i) ? null : r.GetValue(i)?.ToString();

        return snap;
    }

    private async Task RegistrarHistorialAsync(SqlConnection conn, int remisionId,
        string usuario, Dictionary<string, object?> anterior, Dictionary<string, object?> nuevo)
    {
        var antesJson   = System.Text.Json.JsonSerializer.Serialize(anterior);
        var despuesJson = System.Text.Json.JsonSerializer.Serialize(nuevo);

        await using var cmd = new SqlCommand(
            """
            INSERT INTO remisiones_historial (remision_id, usuario, registro_anterior, registro_nuevo)
            VALUES (@rid, @usr, @ant, @nvo)
            """, conn);

        cmd.Parameters.AddWithValue("rid", remisionId);
        cmd.Parameters.AddWithValue("usr", usuario);
        cmd.Parameters.AddWithValue("ant", antesJson);
        cmd.Parameters.AddWithValue("nvo", despuesJson);

        await cmd.ExecuteNonQueryAsync();
    }

    private static byte[] GenerarExcel(List<string?[]> rows)
    {
        ExcelPackage.License.SetNonCommercialPersonal("IT Control Center");
        using var pkg = new ExcelPackage();
        var ws = pkg.Workbook.Worksheets.Add("Remisiones");

        string[] headers =
        [
            "ID","ID OC","ID REMISIÓN","FOLIO","SOLICITANTE",
            "FECHA SOLICITUD","ACCESORIO SOLICITADO","MODELO/SERIE/COMENTARIOS",
            "PROVEEDOR","PIEZA/SERVICIO","CANTIDAD","PRECIO UNITARIO",
            "TOTAL SIN IVA","MONEDA","PAGADO","PRESUPUESTO",
            "CUENTA A DESCONTAR","FECHA ENTRADA PLANTA","STATUS","REQUISICIÓN","OC"
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
