using ChiIT.Data;
using Npgsql;

using OfficeOpenXml;
using OfficeOpenXml.Style;

using System.Drawing;

namespace ChiIT.Services;

// ── DTOs / Modelos ────────────────────────────────────────────────────────────

public class ConsumibleNFDto
{
    public string?  ID_UNICO        { get; set; }
    public string?  OC              { get; set; }
    public string?  FOLIO_CANTIDAD  { get; set; }
    public string?  FECHA_ENTRADA   { get; set; }
    public string?  RECIBIDO_POR    { get; set; }
    public string?  SUBCATEGORIA    { get; set; }
    public string?  MARCA           { get; set; }
    public string?  MODELO          { get; set; }
    public string?  DESCRIPCION     { get; set; }
    public int?     CANTIDAD        { get; set; }
    public string?  PROVEEDOR       { get; set; }
    public decimal? COSTO           { get; set; }
    public string?  MONEDA          { get; set; }
    public string?  PLANTA          { get; set; }
    public string?  UBICACION       { get; set; }
    public string?  DESTINO         { get; set; }
}

public class ConsumibleNFFiltros
{
    public string? ID_UNICO       { get; set; }
    public string? OC             { get; set; }
    public string? FOLIO_CANTIDAD { get; set; }
    public string? FECHA_ENTRADA  { get; set; }
    public string? RECIBIDO_POR   { get; set; }
    public string? SUBCATEGORIA   { get; set; }
    public string? MARCA          { get; set; }
    public string? MODELO         { get; set; }
    public string? DESCRIPCION    { get; set; }
    public string? PROVEEDOR      { get; set; }
    public string? MONEDA         { get; set; }
    public string? PLANTA         { get; set; }
    public string? UBICACION      { get; set; }
    public string? DESTINO        { get; set; }
}

// ── Servicio ──────────────────────────────────────────────────────────────────

public class ConsumiblesNFService
{
    private readonly DbConnectionPool _pool;

    private static readonly string[] COLS =
    [
        "ID_UNICO","OC","FOLIO_CANTIDAD","FECHA_ENTRADA","RECIBIDO_POR",
        "SUBCATEGORIA","MARCA","MODELO","DESCRIPCION","CANTIDAD",
        "PROVEEDOR","COSTO","MONEDA","PLANTA","UBICACION","DESTINO"
    ];

    public ConsumiblesNFService(DbConnectionPool pool)
    {
        _pool = pool;
        _ = InicializarTablaAsync();
    }

    // ── DDL ───────────────────────────────────────────────────────────────
    private async Task InicializarTablaAsync()
    {
        await using var conn = await _pool.OpenAsync();
        await using var cmd = new NpgsqlCommand("""
            CREATE TABLE IF NOT EXISTS consumibles_nf (
                id              SERIAL PRIMARY KEY,
                id_unico        TEXT,
                oc              TEXT,
                folio_cantidad  TEXT,
                fecha_entrada   DATE,
                recibido_por    TEXT,
                subcategoria    TEXT,
                marca           TEXT,
                modelo          TEXT,
                descripcion     TEXT,
                cantidad        INTEGER,
                proveedor       TEXT,
                costo           NUMERIC(12,2),
                moneda          TEXT,
                planta          TEXT,
                ubicacion       TEXT,
                destino         TEXT
            );

            CREATE TABLE IF NOT EXISTS consumibles_nf_historial (
                id                SERIAL PRIMARY KEY,
                consumible_id     INTEGER NOT NULL REFERENCES consumibles_nf(id) ON DELETE CASCADE,
                usuario           TEXT    NOT NULL,
                fecha             TIMESTAMPTZ DEFAULT NOW(),
                registro_anterior JSONB,
                registro_nuevo    JSONB
            );
            """, conn);

        await cmd.ExecuteNonQueryAsync();
    }

    // ── Listar (paginado + filtros) ───────────────────────────────────────
    public async Task<object> ListarAsync(int page, int limit, ConsumibleNFFiltros f)
    {
        await using var conn = await _pool.OpenAsync();

        var (where, parms) = ConstruirWhere(f);
        var offset = (page - 1) * limit;

        // Total
        await using var cmdCount = new NpgsqlCommand(
            $"SELECT COUNT(*) FROM consumibles_nf {where}", conn);
        foreach (var (k, v) in parms) cmdCount.Parameters.AddWithValue(k, v ?? (object)DBNull.Value);
        var total = Convert.ToInt64(await cmdCount.ExecuteScalarAsync());

        // Datos
        await using var cmdData = new NpgsqlCommand(
            $@"SELECT id, id_unico, oc, folio_cantidad, fecha_entrada,
                      recibido_por, subcategoria, marca, modelo, descripcion,
                      cantidad, proveedor, costo, moneda, planta, ubicacion, destino
               FROM consumibles_nf {where}
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
                ID             = reader.GetInt32(0),
                ID_UNICO       = Str(reader, 1),
                OC             = Str(reader, 2),
                FOLIO_CANTIDAD = Str(reader, 3),
                FECHA_ENTRADA  = Str(reader, 4),
                RECIBIDO_POR   = Str(reader, 5),
                SUBCATEGORIA   = Str(reader, 6),
                MARCA          = Str(reader, 7),
                MODELO         = Str(reader, 8),
                DESCRIPCION    = Str(reader, 9),
                CANTIDAD       = reader.IsDBNull(10) ? (int?)null     : reader.GetInt32(10),
                PROVEEDOR      = Str(reader, 11),
                COSTO          = reader.IsDBNull(12) ? (decimal?)null : reader.GetDecimal(12),
                MONEDA         = Str(reader, 13),
                PLANTA         = Str(reader, 14),
                UBICACION      = Str(reader, 15),
                DESTINO        = Str(reader, 16),
            });
        }

        return new
        {
            total,
            page,
            limit,
            pages   = (int)Math.Ceiling((double)total / limit),
            data    = lista
        };
    }

    // ── Crear ─────────────────────────────────────────────────────────────
    public async Task<int> CrearAsync(ConsumibleNFDto dto, string usuario)
    {
        await using var conn = await _pool.OpenAsync();
        await using var cmd = new NpgsqlCommand(
            @"INSERT INTO consumibles_nf
                (id_unico, oc, folio_cantidad, fecha_entrada, recibido_por,
                 subcategoria, marca, modelo, descripcion, cantidad,
                 proveedor, costo, moneda, planta, ubicacion, destino)
              VALUES
                (@id_unico, @oc, @folio_cantidad, @fecha_entrada, @recibido_por,
                 @subcategoria, @marca, @modelo, @descripcion, @cantidad,
                 @proveedor, @costo, @moneda, @planta, @ubicacion, @destino)
              RETURNING id", conn);

        AgregarParametros(cmd, dto);
        var id = Convert.ToInt32(await cmd.ExecuteScalarAsync());

        // Historial: registro nuevo
        var snap = await SnapshotAsync(conn, id);
        if (snap != null)
            await RegistrarHistorialAsync(conn, id, usuario,
                new Dictionary<string, object?>(), snap);

        return id;
    }

    // ── Editar ────────────────────────────────────────────────────────────
    public async Task<bool> EditarAsync(int id, ConsumibleNFDto dto, string usuario)
    {
        await using var conn = await _pool.OpenAsync();

        var anterior = await SnapshotAsync(conn, id);
        if (anterior == null) return false;

        await using var cmd = new NpgsqlCommand(
            @"UPDATE consumibles_nf SET
                id_unico       = @id_unico,
                oc             = @oc,
                folio_cantidad = @folio_cantidad,
                fecha_entrada  = @fecha_entrada,
                recibido_por   = @recibido_por,
                subcategoria   = @subcategoria,
                marca          = @marca,
                modelo         = @modelo,
                descripcion    = @descripcion,
                cantidad       = @cantidad,
                proveedor      = @proveedor,
                costo          = @costo,
                moneda         = @moneda,
                planta         = @planta,
                ubicacion      = @ubicacion,
                destino        = @destino
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
        await using var cmd = new NpgsqlCommand(
            "DELETE FROM consumibles_nf WHERE id = @id", conn);
        cmd.Parameters.AddWithValue("id", id);
        return await cmd.ExecuteNonQueryAsync() > 0;
    }

    // ── Historial ─────────────────────────────────────────────────────────
    public async Task<List<object>> HistorialAsync(int id)
    {
        await using var conn = await _pool.OpenAsync();
        await using var cmd = new NpgsqlCommand(
            @"SELECT id, usuario, fecha, registro_anterior, registro_nuevo
              FROM consumibles_nf_historial
              WHERE consumible_id = @id
              ORDER BY fecha DESC", conn);
        cmd.Parameters.AddWithValue("id", id);

        var lista = new List<object>();
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
        {
            lista.Add(new
            {
                ID                 = r.GetInt32(0),
                USUARIO            = Str(r, 1),
                FECHA              = Str(r, 2),
                REGISTRO_ANTERIOR  = Str(r, 3),
                REGISTRO_NUEVO     = Str(r, 4)
            });
        }
        return lista;
    }

    // ── Exportar (filtrado) ───────────────────────────────────────────────
    public async Task<byte[]> ExportarAsync(ConsumibleNFFiltros f)
    {
        await using var conn = await _pool.OpenAsync();
        var (where, parms) = ConstruirWhere(f);

        await using var cmd = new NpgsqlCommand(
            $@"SELECT id, id_unico, oc, folio_cantidad, fecha_entrada,
                      recibido_por, subcategoria, marca, modelo, descripcion,
                      cantidad, proveedor, costo, moneda, planta, ubicacion, destino
               FROM consumibles_nf {where}
               ORDER BY id DESC", conn);

        foreach (var (k, v) in parms) cmd.Parameters.AddWithValue(k, v ?? (object)DBNull.Value);

        var rows = new List<string?[]>();
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
        {
            rows.Add(Enumerable.Range(0, 17)
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
            @"SELECT id, id_unico, oc, folio_cantidad, fecha_entrada,
                     recibido_por, subcategoria, marca, modelo, descripcion,
                     cantidad, proveedor, costo, moneda, planta, ubicacion, destino
              FROM consumibles_nf
              WHERE EXTRACT(YEAR FROM fecha_entrada) = @anio
              ORDER BY id DESC", conn);
        cmd.Parameters.AddWithValue("anio", anio);

        var rows = new List<string?[]>();
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
        {
            rows.Add(Enumerable.Range(0, 17)
                .Select(i => r.IsDBNull(i) ? null : r.GetValue(i)?.ToString())
                .ToArray());
        }

        return GenerarExcel(rows);
    }

    // ── Helpers privados ──────────────────────────────────────────────────

    private static (string where, List<(string key, object? val)> parms) ConstruirWhere(ConsumibleNFFiltros f)
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

        Add("id_unico",       f.ID_UNICO);
        Add("oc",             f.OC);
        Add("folio_cantidad", f.FOLIO_CANTIDAD);
        Add("fecha_entrada",  f.FECHA_ENTRADA);
        Add("recibido_por",   f.RECIBIDO_POR);
        Add("subcategoria",   f.SUBCATEGORIA);
        Add("marca",          f.MARCA);
        Add("modelo",         f.MODELO);
        Add("descripcion",    f.DESCRIPCION);
        Add("proveedor",      f.PROVEEDOR);
        Add("moneda",         f.MONEDA);
        Add("planta",         f.PLANTA);
        Add("ubicacion",      f.UBICACION);
        Add("destino",        f.DESTINO);

        var where = conds.Count > 0 ? "WHERE " + string.Join(" AND ", conds) : "";
        return (where, parms);
    }

    private static void AgregarParametros(NpgsqlCommand cmd, ConsumibleNFDto dto)
    {
        cmd.Parameters.AddWithValue("id_unico",       (object?)dto.ID_UNICO       ?? DBNull.Value);
        cmd.Parameters.AddWithValue("oc",             (object?)dto.OC             ?? DBNull.Value);
        cmd.Parameters.AddWithValue("folio_cantidad", (object?)dto.FOLIO_CANTIDAD ?? DBNull.Value);
        cmd.Parameters.AddWithValue("fecha_entrada",  (object?)dto.FECHA_ENTRADA  ?? DBNull.Value);
        cmd.Parameters.AddWithValue("recibido_por",   (object?)dto.RECIBIDO_POR   ?? DBNull.Value);
        cmd.Parameters.AddWithValue("subcategoria",   (object?)dto.SUBCATEGORIA   ?? DBNull.Value);
        cmd.Parameters.AddWithValue("marca",          (object?)dto.MARCA          ?? DBNull.Value);
        cmd.Parameters.AddWithValue("modelo",         (object?)dto.MODELO         ?? DBNull.Value);
        cmd.Parameters.AddWithValue("descripcion",    (object?)dto.DESCRIPCION    ?? DBNull.Value);
        cmd.Parameters.AddWithValue("cantidad",       (object?)dto.CANTIDAD       ?? DBNull.Value);
        cmd.Parameters.AddWithValue("proveedor",      (object?)dto.PROVEEDOR      ?? DBNull.Value);
        cmd.Parameters.AddWithValue("costo",          (object?)dto.COSTO          ?? DBNull.Value);
        cmd.Parameters.AddWithValue("moneda",         (object?)dto.MONEDA         ?? DBNull.Value);
        cmd.Parameters.AddWithValue("planta",         (object?)dto.PLANTA         ?? DBNull.Value);
        cmd.Parameters.AddWithValue("ubicacion",      (object?)dto.UBICACION      ?? DBNull.Value);
        cmd.Parameters.AddWithValue("destino",        (object?)dto.DESTINO        ?? DBNull.Value);
    }

    private async Task<Dictionary<string, object?>?> SnapshotAsync(NpgsqlConnection conn, int id)
    {
        await using var cmd = new NpgsqlCommand(
            @"SELECT id_unico, oc, folio_cantidad, fecha_entrada, recibido_por,
                     subcategoria, marca, modelo, descripcion, cantidad,
                     proveedor, costo, moneda, planta, ubicacion, destino
              FROM consumibles_nf WHERE id=@id", conn);
        cmd.Parameters.AddWithValue("id", id);

        await using var r = await cmd.ExecuteReaderAsync();
        if (!await r.ReadAsync()) return null;

        var snap = new Dictionary<string, object?>();
        for (int i = 0; i < COLS.Length; i++)
            snap[COLS[i]] = r.IsDBNull(i) ? null : r.GetValue(i)?.ToString();

        return snap;
    }

    private async Task RegistrarHistorialAsync(NpgsqlConnection conn, int consumibleId,
        string usuario, Dictionary<string, object?> anterior, Dictionary<string, object?> nuevo)
    {
        var antesJson   = System.Text.Json.JsonSerializer.Serialize(anterior);
        var despuesJson = System.Text.Json.JsonSerializer.Serialize(nuevo);

        await using var cmd = new NpgsqlCommand(
            """
            INSERT INTO consumibles_nf_historial (consumible_id, usuario, registro_anterior, registro_nuevo)
            VALUES (@cid, @usr, @ant::jsonb, @nvo::jsonb)
            """, conn);

        cmd.Parameters.AddWithValue("cid", consumibleId);
        cmd.Parameters.AddWithValue("usr", usuario);
        cmd.Parameters.AddWithValue("ant", antesJson);
        cmd.Parameters.AddWithValue("nvo", despuesJson);

        await cmd.ExecuteNonQueryAsync();
    }

    private static byte[] GenerarExcel(List<string?[]> rows)
    {
        ExcelPackage.License.SetNonCommercialPersonal("IT Control Center");
        using var pkg = new ExcelPackage();
        var ws = pkg.Workbook.Worksheets.Add("Consumibles NF");

        string[] headers =
        [
            "ID","ID ÚNICO","OC","FOLIO/CANTIDAD","FECHA ENTRADA",
            "RECIBIDO POR","SUBCATEGORÍA","MARCA","MODELO","DESCRIPCIÓN",
            "CANTIDAD","PROVEEDOR","COSTO","MONEDA","PLANTA","UBICACIÓN","DESTINO"
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
