using ChiIT.Data;
using Npgsql;

using OfficeOpenXml;
using OfficeOpenXml.Style;

using System.Drawing;

namespace ChiIT.Services;

// ── DTOs / Modelos ────────────────────────────────────────────────────────────

public class TintaTonerRibonNFDto
{
    public string?  ID_UNICO             { get; set; }
    public string?  OC                   { get; set; }
    public string?  MODELO               { get; set; }
    public string?  RECIBIDO_POR         { get; set; }
    public string?  SUBCATEGORIA         { get; set; }
    public string?  FECHA_REGISTRO       { get; set; }
    public string?  PROVEEDOR            { get; set; }
    public int?     STOCK                { get; set; }
    public decimal? COSTO_MN             { get; set; }
    public int?     CANTIDAD_RECIBIDA    { get; set; }
    public string?  FECHA_INSTALACION    { get; set; }
    public string?  UBICACION            { get; set; }
    public string?  IMPRESORA            { get; set; }
    public string?  INSTALADO_POR        { get; set; }
}

public class TintaTonerRibonNFFiltros
{
    public string? ID_UNICO          { get; set; }
    public string? OC                { get; set; }
    public string? MODELO            { get; set; }
    public string? RECIBIDO_POR      { get; set; }
    public string? SUBCATEGORIA      { get; set; }
    public string? FECHA_REGISTRO    { get; set; }
    public string? PROVEEDOR         { get; set; }
    public string? UBICACION         { get; set; }
    public string? IMPRESORA         { get; set; }
    public string? INSTALADO_POR     { get; set; }
}

// ── Servicio ──────────────────────────────────────────────────────────────────

public class TintasTonerRibonNFService
{
    private readonly DbConnectionPool _pool;
    private readonly OrdenesDeCompraService _ordenesService;

    private static readonly string[] COLS =
    [
        "ID_UNICO","OC","MODELO","RECIBIDO_POR","SUBCATEGORIA",
        "FECHA_REGISTRO","PROVEEDOR","STOCK","COSTO_MN",
        "CANTIDAD_RECIBIDA","FECHA_INSTALACION","UBICACION","IMPRESORA","INSTALADO_POR"
    ];

    public TintasTonerRibonNFService(DbConnectionPool pool, OrdenesDeCompraService ordenesService)
    {
        _pool = pool;
        _ordenesService = ordenesService;
        _ = InicializarTablaAsync();
    }

    // ── DDL ───────────────────────────────────────────────────────────────
    private async Task InicializarTablaAsync()
    {
        await using var conn = await _pool.OpenAsync();
        await using var cmd = new NpgsqlCommand("""
            CREATE TABLE IF NOT EXISTS tintas_toner_ribon_nf (
                id                  SERIAL PRIMARY KEY,
                id_unico            TEXT,
                oc                  TEXT,
                modelo              TEXT,
                recibido_por        TEXT,
                subcategoria        TEXT,
                fecha_registro      DATE,
                proveedor           TEXT,
                stock               INTEGER,
                costo_mn            NUMERIC(12,2),
                cantidad_recibida   INTEGER,
                fecha_instalacion   DATE,
                ubicacion           TEXT,
                impresora           TEXT,
                instalado_por       TEXT
            );

            CREATE TABLE IF NOT EXISTS tintas_toner_ribon_nf_historial (
                id                SERIAL PRIMARY KEY,
                tinta_id          INTEGER NOT NULL REFERENCES tintas_toner_ribon_nf(id) ON DELETE CASCADE,
                usuario           TEXT    NOT NULL,
                fecha             TIMESTAMPTZ DEFAULT NOW(),
                registro_anterior JSONB,
                registro_nuevo    JSONB
            );
            """, conn);

        await cmd.ExecuteNonQueryAsync();
    }

    // ── Listar (paginado + filtros) ───────────────────────────────────────
    public async Task<object> ListarAsync(int page, int limit, TintaTonerRibonNFFiltros f)
    {
        await using var conn = await _pool.OpenAsync();

        var (where, parms) = ConstruirWhere(f);
        if (string.IsNullOrWhiteSpace(where))
            where = "WHERE (activo IS NULL OR activo = true)";
        else
            where += " AND (activo IS NULL OR activo = true)";
        var offset = (page - 1) * limit;

        // Total
        await using var cmdCount = new NpgsqlCommand(
            $"SELECT COUNT(*) FROM tintas_toner_ribon_nf {where}", conn);
        foreach (var (k, v) in parms) cmdCount.Parameters.AddWithValue(k, v ?? (object)DBNull.Value);
        var total = Convert.ToInt64(await cmdCount.ExecuteScalarAsync());

        // Datos
        await using var cmdData = new NpgsqlCommand(
            $@"SELECT id, id_unico, oc, modelo, recibido_por,
                      subcategoria, fecha_registro, proveedor, stock,
                      costo_mn, cantidad_recibida, fecha_instalacion,
                      ubicacion, impresora, instalado_por
               FROM tintas_toner_ribon_nf {where}
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
                ID                = reader.GetInt32(0),
                ID_UNICO          = Str(reader, 1),
                OC                = Str(reader, 2),
                MODELO            = Str(reader, 3),
                RECIBIDO_POR      = Str(reader, 4),
                SUBCATEGORIA      = Str(reader, 5),
                FECHA_REGISTRO    = Str(reader, 6),
                PROVEEDOR         = Str(reader, 7),
                STOCK             = reader.IsDBNull(8)  ? (int?)null     : reader.GetInt32(8),
                COSTO_MN          = reader.IsDBNull(9)  ? (decimal?)null : reader.GetDecimal(9),
                CANTIDAD_RECIBIDA = reader.IsDBNull(10) ? (int?)null     : reader.GetInt32(10),
                FECHA_INSTALACION = Str(reader, 11),
                UBICACION         = Str(reader, 12),
                IMPRESORA         = Str(reader, 13),
                INSTALADO_POR     = Str(reader, 14)
            });
        }

        return new { total, data = lista };
    }

    // ── Crear ─────────────────────────────────────────────────────────────
    public async Task<int> CrearAsync(TintaTonerRibonNFDto dto, string usuario)
    {
        await using var conn = await _pool.OpenAsync();

        await using var cmd = new NpgsqlCommand("""
            INSERT INTO tintas_toner_ribon_nf
                (id_unico, oc, modelo, recibido_por, subcategoria,
                 fecha_registro, proveedor, stock, costo_mn,
                 cantidad_recibida, fecha_instalacion, ubicacion, impresora, instalado_por)
            VALUES
                (@id_unico, @oc, @modelo, @recibido_por, @subcategoria,
                 @fecha_registro::date, @proveedor, @stock, @costo_mn,
                 @cantidad_recibida, @fecha_instalacion::date, @ubicacion, @impresora, @instalado_por)
            RETURNING id
            """, conn);

        AgregarParametros(cmd, dto);
        var id = Convert.ToInt32(await cmd.ExecuteScalarAsync());
        await conn.CloseAsync();
        _ordenesService.RecalcularPorCambioEnHija("Tintas,Toner,Ribon NF", dto.OC, null);
        return id;
    }

    // ── Editar ────────────────────────────────────────────────────────────
    public async Task<bool> EditarAsync(int id, TintaTonerRibonNFDto dto, string usuario)
    {
        await using var conn = await _pool.OpenAsync();

        var anterior = await SnapshotAsync(conn, id);
        if (anterior is null) return false;

        await using var cmd = new NpgsqlCommand("""
            UPDATE tintas_toner_ribon_nf SET
                id_unico          = @id_unico,
                oc                = @oc,
                modelo            = @modelo,
                recibido_por      = @recibido_por,
                subcategoria      = @subcategoria,
                fecha_registro    = @fecha_registro::date,
                proveedor         = @proveedor,
                stock             = @stock,
                costo_mn          = @costo_mn,
                cantidad_recibida = @cantidad_recibida,
                fecha_instalacion = @fecha_instalacion::date,
                ubicacion         = @ubicacion,
                impresora         = @impresora,
                instalado_por     = @instalado_por
            WHERE id = @id
            """, conn);

        AgregarParametros(cmd, dto);
        cmd.Parameters.AddWithValue("id", id);
        var rows = await cmd.ExecuteNonQueryAsync();
        if (rows == 0) return false;

        var nuevo = await SnapshotAsync(conn, id);
        await RegistrarHistorialAsync(conn, id, usuario, anterior, nuevo!);
        await conn.CloseAsync();
        _ordenesService.RecalcularPorCambioEnHija("Tintas,Toner,Ribon NF", dto.OC, null);
        return true;
    }

    // ── Eliminar ──────────────────────────────────────────────────────────
    public async Task<bool> EliminarAsync(int id)
    {
        await using var conn = await _pool.OpenAsync();

        string? ocVal = null, folioVal = null;
        await using (var qSnap = new NpgsqlCommand(
            "SELECT oc FROM tintas_toner_ribon_nf WHERE id = @id", conn))
        {
            qSnap.Parameters.AddWithValue("id", id);
            await using var r = await qSnap.ExecuteReaderAsync();
            if (await r.ReadAsync())
            {
                ocVal = r.IsDBNull(0) ? null : r.GetString(0);
            }
        }

        await using var cmd = new NpgsqlCommand(
            "DELETE FROM tintas_toner_ribon_nf WHERE id = @id", conn);
        cmd.Parameters.AddWithValue("id", id);
        var deleted = await cmd.ExecuteNonQueryAsync() > 0;

        await conn.CloseAsync();

        if (deleted)
            _ordenesService.RecalcularPorCambioEnHija("Tintas,Toner,Ribon NF", ocVal, null);

        return deleted;
    }

    // ── Historial ─────────────────────────────────────────────────────────
    public async Task<List<object>> HistorialAsync(int id)
    {
        await using var conn = await _pool.OpenAsync();
        await using var cmd = new NpgsqlCommand(
            @"SELECT id, usuario, fecha, registro_anterior, registro_nuevo
              FROM tintas_toner_ribon_nf_historial
              WHERE tinta_id = @id
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

    // ── Exportar (filtrado) ───────────────────────────────────────────────
    public async Task<byte[]> ExportarAsync(TintaTonerRibonNFFiltros f)
    {
        await using var conn = await _pool.OpenAsync();

        var (where, parms) = ConstruirWhere(f);

        if (string.IsNullOrWhiteSpace(where))
            where = "WHERE (activo IS NULL OR activo = true)";
        else
            where += " AND (activo IS NULL OR activo = true)";

        await using var cmd = new NpgsqlCommand(
            $@"SELECT id, id_unico, oc, modelo, recibido_por,
                      subcategoria, fecha_registro, proveedor, stock,
                      costo_mn, cantidad_recibida, fecha_instalacion,
                      ubicacion, impresora, instalado_por
               FROM tintas_toner_ribon_nf {where} ORDER BY id DESC", conn);

        foreach (var (k, v) in parms) cmd.Parameters.AddWithValue(k, v ?? (object)DBNull.Value);

        var rows = new List<string?[]>();
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
        {
            rows.Add(Enumerable.Range(0, 15)
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
            @"SELECT id, id_unico, oc, modelo, recibido_por,
                 subcategoria, fecha_registro, proveedor, stock,
                 costo_mn, cantidad_recibida, fecha_instalacion,
                 ubicacion, impresora, instalado_por
          FROM tintas_toner_ribon_nf
          WHERE EXTRACT(YEAR FROM fecha_registro) = @anio
          AND (activo IS NULL OR activo = true)
          ORDER BY id DESC", conn);

        cmd.Parameters.AddWithValue("anio", anio);

        var rows = new List<string?[]>();
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
        {
            rows.Add(Enumerable.Range(0, 15)
                .Select(i => r.IsDBNull(i) ? null : r.GetValue(i)?.ToString())
                .ToArray());
        }

        return GenerarExcel(rows);
    }

    // ── Helpers privados ──────────────────────────────────────────────────

    private static (string where, List<(string key, object? val)> parms) ConstruirWhere(TintaTonerRibonNFFiltros f)
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

        Add("id_unico",         f.ID_UNICO);
        Add("oc",               f.OC);
        Add("modelo",           f.MODELO);
        Add("recibido_por",     f.RECIBIDO_POR);
        Add("subcategoria",     f.SUBCATEGORIA);
        Add("fecha_registro",   f.FECHA_REGISTRO);
        Add("proveedor",        f.PROVEEDOR);
        Add("ubicacion",        f.UBICACION);
        Add("impresora",        f.IMPRESORA);
        Add("instalado_por",    f.INSTALADO_POR);

        var where = conds.Count > 0 ? "WHERE " + string.Join(" AND ", conds) : "";
        return (where, parms);
    }

    private static void AgregarParametros(NpgsqlCommand cmd, TintaTonerRibonNFDto dto)
    {
        cmd.Parameters.AddWithValue("id_unico",          (object?)dto.ID_UNICO          ?? DBNull.Value);
        cmd.Parameters.AddWithValue("oc",                (object?)dto.OC                ?? DBNull.Value);
        cmd.Parameters.AddWithValue("modelo",            (object?)dto.MODELO            ?? DBNull.Value);
        cmd.Parameters.AddWithValue("recibido_por",      (object?)dto.RECIBIDO_POR      ?? DBNull.Value);
        cmd.Parameters.AddWithValue("subcategoria",      (object?)dto.SUBCATEGORIA      ?? DBNull.Value);
        cmd.Parameters.AddWithValue("fecha_registro",    (object?)dto.FECHA_REGISTRO    ?? DBNull.Value);
        cmd.Parameters.AddWithValue("proveedor",         (object?)dto.PROVEEDOR         ?? DBNull.Value);
        cmd.Parameters.AddWithValue("stock",             (object?)dto.STOCK             ?? DBNull.Value);
        cmd.Parameters.AddWithValue("costo_mn",          (object?)dto.COSTO_MN          ?? DBNull.Value);
        cmd.Parameters.AddWithValue("cantidad_recibida", (object?)dto.CANTIDAD_RECIBIDA ?? DBNull.Value);
        cmd.Parameters.AddWithValue("fecha_instalacion", (object?)dto.FECHA_INSTALACION ?? DBNull.Value);
        cmd.Parameters.AddWithValue("ubicacion",         (object?)dto.UBICACION         ?? DBNull.Value);
        cmd.Parameters.AddWithValue("impresora",         (object?)dto.IMPRESORA         ?? DBNull.Value);
        cmd.Parameters.AddWithValue("instalado_por",     (object?)dto.INSTALADO_POR     ?? DBNull.Value);
    }

    private async Task<Dictionary<string, object?>?> SnapshotAsync(NpgsqlConnection conn, int id)
    {
        await using var cmd = new NpgsqlCommand(
            @"SELECT id_unico, oc, modelo, recibido_por, subcategoria,
                 fecha_registro, proveedor, stock, costo_mn,
                 cantidad_recibida, fecha_instalacion, ubicacion, impresora, instalado_por
          FROM tintas_toner_ribon_nf
          WHERE id=@id
          AND (activo IS NULL OR activo = true)", conn);

        cmd.Parameters.AddWithValue("id", id);

        await using var r = await cmd.ExecuteReaderAsync();
        if (!await r.ReadAsync()) return null;

        var snap = new Dictionary<string, object?>();
        for (int i = 0; i < COLS.Length; i++)
            snap[COLS[i]] = r.IsDBNull(i) ? null : r.GetValue(i)?.ToString();

        return snap;
    }

    private async Task RegistrarHistorialAsync(NpgsqlConnection conn, int tintaId,
        string usuario, Dictionary<string, object?> anterior, Dictionary<string, object?> nuevo)
    {
        var antesJson   = System.Text.Json.JsonSerializer.Serialize(anterior);
        var despuesJson = System.Text.Json.JsonSerializer.Serialize(nuevo);

        await using var cmd = new NpgsqlCommand(
            """
            INSERT INTO tintas_toner_ribon_nf_historial (tinta_id, usuario, registro_anterior, registro_nuevo)
            VALUES (@tid, @usr, @ant::jsonb, @nvo::jsonb)
            """, conn);

        cmd.Parameters.AddWithValue("tid", tintaId);
        cmd.Parameters.AddWithValue("usr", usuario);
        cmd.Parameters.AddWithValue("ant", antesJson);
        cmd.Parameters.AddWithValue("nvo", despuesJson);

        await cmd.ExecuteNonQueryAsync();
    }

    private static byte[] GenerarExcel(List<string?[]> rows)
    {
        ExcelPackage.License.SetNonCommercialPersonal("IT Control Center");
        using var pkg = new ExcelPackage();
        var ws = pkg.Workbook.Worksheets.Add("Tintas Toner Ribon NF");

        string[] headers =
        [
            "ID","ID ÚNICO","OC","MODELO","RECIBIDO POR",
            "SUBCATEGORÍA","FECHA REGISTRO","PROVEEDOR","STOCK",
            "COSTO MN","CANTIDAD RECIBIDA","FECHA INSTALACIÓN",
            "UBICACIÓN","IMPRESORA","INSTALADO POR"
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
