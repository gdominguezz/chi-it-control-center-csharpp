using ChiIT.Data;
using Npgsql;

using OfficeOpenXml;
using OfficeOpenXml.Style;

using System.Drawing;

namespace ChiIT.Services;

// ── DTOs / Modelos ────────────────────────────────────────────────────────────

public class InventarioNFDto
{
    public string?  INV_FOLIO         { get; set; }
    public string?  EQUIPO            { get; set; }
    public string?  MARCA             { get; set; }
    public string?  MODELO            { get; set; }
    public int?     CANTIDAD          { get; set; }
    public decimal? PRECIO_UNITARIO   { get; set; }
    public decimal? PRECIO_CON_IVA    { get; set; }
    public string?  MONEDA            { get; set; }
    public string?  PROVEEDOR         { get; set; }
    public string?  PRESUPUESTO       { get; set; }
    public string?  STATUS            { get; set; }
    public int?     ANIO              { get; set; }
    public string?  OC                { get; set; }
    public string?  NUMERO_SERIE      { get; set; }
    public string?  UBICACION_ACTUAL  { get; set; }
}

public class InventarioNFFiltros
{
    public string? INV_FOLIO         { get; set; }
    public string? EQUIPO            { get; set; }
    public string? MARCA             { get; set; }
    public string? MODELO            { get; set; }
    public string? MONEDA            { get; set; }
    public string? PROVEEDOR         { get; set; }
    public string? PRESUPUESTO       { get; set; }
    public string? STATUS            { get; set; }
    public string? ANIO              { get; set; }
    public string? OC                { get; set; }
    public string? NUMERO_SERIE      { get; set; }
    public string? UBICACION_ACTUAL  { get; set; }
}

// ── Servicio ──────────────────────────────────────────────────────────────────

public class InventariosNFService
{
    private readonly DbConnectionPool _pool;

    private static readonly string[] COLS =
    [
        "INV_FOLIO","EQUIPO","MARCA","MODELO","CANTIDAD",
        "PRECIO_UNITARIO","PRECIO_CON_IVA","MONEDA","PROVEEDOR",
        "PRESUPUESTO","STATUS","ANIO","OC","NUMERO_SERIE","UBICACION_ACTUAL"
    ];

    public InventariosNFService(DbConnectionPool pool)
    {
        _pool = pool;
        _ = InicializarTablaAsync();
    }

    // ── DDL ───────────────────────────────────────────────────────────────
    private async Task InicializarTablaAsync()
    {
        await using var conn = await _pool.OpenAsync();
        await using var cmd = new NpgsqlCommand("""
            CREATE TABLE IF NOT EXISTS inventarios_nf (
                id                SERIAL PRIMARY KEY,
                inv_folio         TEXT,
                equipo            TEXT,
                marca             TEXT,
                modelo            TEXT,
                cantidad          INTEGER,
                precio_unitario   NUMERIC(12,2),
                precio_con_iva    NUMERIC(12,2),
                moneda            TEXT,
                proveedor         TEXT,
                presupuesto       TEXT,
                status            TEXT,
                anio              INTEGER,
                oc                TEXT,
                numero_serie      TEXT,
                ubicacion_actual  TEXT
            );

            CREATE TABLE IF NOT EXISTS inventarios_nf_historial (
                id                SERIAL PRIMARY KEY,
                inventario_id     INTEGER NOT NULL REFERENCES inventarios_nf(id) ON DELETE CASCADE,
                usuario           TEXT    NOT NULL,
                fecha             TIMESTAMPTZ DEFAULT NOW(),
                registro_anterior JSONB,
                registro_nuevo    JSONB
            );
            """, conn);

        await cmd.ExecuteNonQueryAsync();
    }

    // ── Listar (paginado + filtros) ───────────────────────────────────────
    public async Task<object> ListarAsync(int page, int limit, InventarioNFFiltros f)
    {
        await using var conn = await _pool.OpenAsync();

        var (where, parms) = ConstruirWhere(f);
        var offset = (page - 1) * limit;

        // Total
        await using var cmdCount = new NpgsqlCommand(
            $"SELECT COUNT(*) FROM inventarios_nf {where}", conn);
        foreach (var (k, v) in parms) cmdCount.Parameters.AddWithValue(k, v ?? (object)DBNull.Value);
        var total = Convert.ToInt64(await cmdCount.ExecuteScalarAsync());

        // Datos
        await using var cmdData = new NpgsqlCommand(
            $@"SELECT id, inv_folio, equipo, marca, modelo,
                      cantidad, precio_unitario, precio_con_iva, moneda,
                      proveedor, presupuesto, status, anio, oc,
                      numero_serie, ubicacion_actual
               FROM inventarios_nf {where}
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
                ID               = reader.GetInt32(0),
                INV_FOLIO        = Str(reader, 1),
                EQUIPO           = Str(reader, 2),
                MARCA            = Str(reader, 3),
                MODELO           = Str(reader, 4),
                CANTIDAD         = reader.IsDBNull(5)  ? (int?)null     : reader.GetInt32(5),
                PRECIO_UNITARIO  = reader.IsDBNull(6)  ? (decimal?)null : reader.GetDecimal(6),
                PRECIO_CON_IVA   = reader.IsDBNull(7)  ? (decimal?)null : reader.GetDecimal(7),
                MONEDA           = Str(reader, 8),
                PROVEEDOR        = Str(reader, 9),
                PRESUPUESTO      = Str(reader, 10),
                STATUS           = Str(reader, 11),
                ANIO             = reader.IsDBNull(12) ? (int?)null     : reader.GetInt32(12),
                OC               = Str(reader, 13),
                NUMERO_SERIE     = Str(reader, 14),
                UBICACION_ACTUAL = Str(reader, 15),
            });
        }

        return new { total, page, limit, data = lista };
    }

    // ── Crear ─────────────────────────────────────────────────────────────
    public async Task<int> CrearAsync(InventarioNFDto dto, string usuario)
    {
        await using var conn = await _pool.OpenAsync();

        await using var cmd = new NpgsqlCommand(
            @"INSERT INTO inventarios_nf
                (inv_folio, equipo, marca, modelo, cantidad,
                 precio_unitario, precio_con_iva, moneda, proveedor,
                 presupuesto, status, anio, oc, numero_serie, ubicacion_actual)
              VALUES
                (@inv_folio, @equipo, @marca, @modelo, @cantidad,
                 @precio_unitario, @precio_con_iva, @moneda, @proveedor,
                 @presupuesto, @status, @anio, @oc, @numero_serie, @ubicacion_actual)
              RETURNING id", conn);

        AgregarParametros(cmd, dto);
        var id = Convert.ToInt32(await cmd.ExecuteScalarAsync());

        // Historial: snapshot nuevo
        var snap = await SnapshotAsync(conn, id);
        if (snap is not null)
        {
            var vacio = COLS.ToDictionary(c => c, _ => (object?)null);
            await RegistrarHistorialAsync(conn, id, usuario, vacio, snap);
        }

        return id;
    }

    // ── Editar ────────────────────────────────────────────────────────────
    public async Task<bool> EditarAsync(int id, InventarioNFDto dto, string usuario)
    {
        await using var conn = await _pool.OpenAsync();

        var anterior = await SnapshotAsync(conn, id);
        if (anterior is null) return false;

        await using var cmd = new NpgsqlCommand(
            @"UPDATE inventarios_nf SET
                inv_folio        = @inv_folio,
                equipo           = @equipo,
                marca            = @marca,
                modelo           = @modelo,
                cantidad         = @cantidad,
                precio_unitario  = @precio_unitario,
                precio_con_iva   = @precio_con_iva,
                moneda           = @moneda,
                proveedor        = @proveedor,
                presupuesto      = @presupuesto,
                status           = @status,
                anio             = @anio,
                oc               = @oc,
                numero_serie     = @numero_serie,
                ubicacion_actual = @ubicacion_actual
              WHERE id = @id", conn);

        AgregarParametros(cmd, dto);
        cmd.Parameters.AddWithValue("id", id);
        var rows = await cmd.ExecuteNonQueryAsync();

        if (rows > 0)
        {
            var nuevo = await SnapshotAsync(conn, id);
            if (nuevo is not null)
                await RegistrarHistorialAsync(conn, id, usuario, anterior, nuevo);
        }

        return rows > 0;
    }

    // ── Eliminar ──────────────────────────────────────────────────────────
    public async Task<bool> EliminarAsync(int id)
    {
        await using var conn = await _pool.OpenAsync();
        await using var cmd = new NpgsqlCommand(
            "DELETE FROM inventarios_nf WHERE id = @id", conn);
        cmd.Parameters.AddWithValue("id", id);
        return await cmd.ExecuteNonQueryAsync() > 0;
    }

    // ── Historial ─────────────────────────────────────────────────────────
    public async Task<List<object>> HistorialAsync(int id)
    {
        await using var conn = await _pool.OpenAsync();
        await using var cmd = new NpgsqlCommand(
            @"SELECT id, usuario, fecha, registro_anterior, registro_nuevo
              FROM inventarios_nf_historial
              WHERE inventario_id = @id
              ORDER BY fecha DESC", conn);
        cmd.Parameters.AddWithValue("id", id);

        var lista = new List<object>();
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
        {
            lista.Add(new
            {
                ID                 = r.GetInt32(0),
                USUARIO            = r.GetString(1),
                FECHA              = r.GetValue(2)?.ToString(),
                REGISTRO_ANTERIOR  = r.IsDBNull(3) ? null : r.GetString(3),
                REGISTRO_NUEVO     = r.IsDBNull(4) ? null : r.GetString(4),
            });
        }
        return lista;
    }

    // ── Exportar (filtrado) ───────────────────────────────────────────────
    public async Task<byte[]> ExportarAsync(InventarioNFFiltros f)
    {
        await using var conn = await _pool.OpenAsync();
        var (where, parms) = ConstruirWhere(f);

        await using var cmd = new NpgsqlCommand(
            $@"SELECT id, inv_folio, equipo, marca, modelo,
                      cantidad, precio_unitario, precio_con_iva, moneda,
                      proveedor, presupuesto, status, anio, oc,
                      numero_serie, ubicacion_actual
               FROM inventarios_nf {where}
               ORDER BY id DESC", conn);

        foreach (var (k, v) in parms) cmd.Parameters.AddWithValue(k, v ?? (object)DBNull.Value);

        var rows = new List<string?[]>();
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
        {
            rows.Add(Enumerable.Range(0, 16)
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
            @"SELECT id, inv_folio, equipo, marca, modelo,
                     cantidad, precio_unitario, precio_con_iva, moneda,
                     proveedor, presupuesto, status, anio, oc,
                     numero_serie, ubicacion_actual
              FROM inventarios_nf
              WHERE anio = @anio
              ORDER BY id DESC", conn);
        cmd.Parameters.AddWithValue("anio", anio);

        var rows = new List<string?[]>();
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
        {
            rows.Add(Enumerable.Range(0, 16)
                .Select(i => r.IsDBNull(i) ? null : r.GetValue(i)?.ToString())
                .ToArray());
        }

        return GenerarExcel(rows);
    }

    // ── Helpers privados ──────────────────────────────────────────────────

    private static (string where, List<(string key, object? val)> parms) ConstruirWhere(InventarioNFFiltros f)
    {
        var conds = new List<string>();

        conds.Add("(activo IS NULL OR activo = true)");

        var parms = new List<(string, object?)>();
        var idx   = 1;

        void Add(string col, string? val)
        {
            if (string.IsNullOrWhiteSpace(val)) return;
            conds.Add($"LOWER({col}::TEXT) LIKE LOWER(@p{idx})");
            parms.Add(($"p{idx}", $"%{val}%"));
            idx++;
        }

        Add("inv_folio",        f.INV_FOLIO);
        Add("equipo",           f.EQUIPO);
        Add("marca",            f.MARCA);
        Add("modelo",           f.MODELO);
        Add("moneda",           f.MONEDA);
        Add("proveedor",        f.PROVEEDOR);
        Add("presupuesto",      f.PRESUPUESTO);
        Add("status",           f.STATUS);
        Add("anio",             f.ANIO);
        Add("oc",               f.OC);
        Add("numero_serie",     f.NUMERO_SERIE);
        Add("ubicacion_actual", f.UBICACION_ACTUAL);

        var where = conds.Count > 0 ? "WHERE " + string.Join(" AND ", conds) : "";
        return (where, parms);
    }

    private static void AgregarParametros(NpgsqlCommand cmd, InventarioNFDto dto)
    {
        cmd.Parameters.AddWithValue("inv_folio",        (object?)dto.INV_FOLIO        ?? DBNull.Value);
        cmd.Parameters.AddWithValue("equipo",           (object?)dto.EQUIPO           ?? DBNull.Value);
        cmd.Parameters.AddWithValue("marca",            (object?)dto.MARCA            ?? DBNull.Value);
        cmd.Parameters.AddWithValue("modelo",           (object?)dto.MODELO           ?? DBNull.Value);
        cmd.Parameters.AddWithValue("cantidad",         (object?)dto.CANTIDAD         ?? DBNull.Value);
        cmd.Parameters.AddWithValue("precio_unitario",  (object?)dto.PRECIO_UNITARIO  ?? DBNull.Value);
        cmd.Parameters.AddWithValue("precio_con_iva",   (object?)dto.PRECIO_CON_IVA   ?? DBNull.Value);
        cmd.Parameters.AddWithValue("moneda",           (object?)dto.MONEDA           ?? DBNull.Value);
        cmd.Parameters.AddWithValue("proveedor",        (object?)dto.PROVEEDOR        ?? DBNull.Value);
        cmd.Parameters.AddWithValue("presupuesto",      (object?)dto.PRESUPUESTO      ?? DBNull.Value);
        cmd.Parameters.AddWithValue("status",           (object?)dto.STATUS           ?? DBNull.Value);
        cmd.Parameters.AddWithValue("anio",             (object?)dto.ANIO             ?? DBNull.Value);
        cmd.Parameters.AddWithValue("oc",               (object?)dto.OC               ?? DBNull.Value);
        cmd.Parameters.AddWithValue("numero_serie",     (object?)dto.NUMERO_SERIE     ?? DBNull.Value);
        cmd.Parameters.AddWithValue("ubicacion_actual", (object?)dto.UBICACION_ACTUAL ?? DBNull.Value);
    }

    private async Task<Dictionary<string, object?>?> SnapshotAsync(NpgsqlConnection conn, int id)
    {
        await using var cmd = new NpgsqlCommand(
            @"SELECT inv_folio, equipo, marca, modelo, cantidad,
                     precio_unitario, precio_con_iva, moneda, proveedor,
                     presupuesto, status, anio, oc, numero_serie, ubicacion_actual
              FROM inventarios_nf WHERE id=@id", conn);
        cmd.Parameters.AddWithValue("id", id);

        await using var r = await cmd.ExecuteReaderAsync();
        if (!await r.ReadAsync()) return null;

        var snap = new Dictionary<string, object?>();
        for (int i = 0; i < COLS.Length; i++)
            snap[COLS[i]] = r.IsDBNull(i) ? null : r.GetValue(i)?.ToString();

        return snap;
    }

    private async Task RegistrarHistorialAsync(NpgsqlConnection conn, int inventarioId,
        string usuario, Dictionary<string, object?> anterior, Dictionary<string, object?> nuevo)
    {
        var antesJson   = System.Text.Json.JsonSerializer.Serialize(anterior);
        var despuesJson = System.Text.Json.JsonSerializer.Serialize(nuevo);

        await using var cmd = new NpgsqlCommand(
            """
            INSERT INTO inventarios_nf_historial (inventario_id, usuario, registro_anterior, registro_nuevo)
            VALUES (@iid, @usr, @ant::jsonb, @nvo::jsonb)
            """, conn);

        cmd.Parameters.AddWithValue("iid", inventarioId);
        cmd.Parameters.AddWithValue("usr", usuario);
        cmd.Parameters.AddWithValue("ant", antesJson);
        cmd.Parameters.AddWithValue("nvo", despuesJson);

        await cmd.ExecuteNonQueryAsync();
    }

    private static byte[] GenerarExcel(List<string?[]> rows)
    {
        ExcelPackage.License.SetNonCommercialPersonal("IT Control Center");
        using var pkg = new ExcelPackage();
        var ws = pkg.Workbook.Worksheets.Add("Inventarios NF");

        string[] headers =
        [
            "ID","FOLIO INV","EQUIPO","MARCA","MODELO",
            "CANTIDAD","PRECIO UNITARIO","PRECIO CON IVA","MONEDA",
            "PROVEEDOR","PRESUPUESTO","STATUS","AÑO","OC",
            "NÚMERO SERIE","UBICACIÓN ACTUAL"
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
