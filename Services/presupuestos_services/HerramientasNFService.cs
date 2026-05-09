using ChiIT.Data;
using Npgsql;

using OfficeOpenXml;
using OfficeOpenXml.Style;

using System.Drawing;

namespace ChiIT.Services;

// ── DTOs / Modelos ────────────────────────────────────────────────────────────

public class HerramientaNFDto
{
    public string?  ID_UNICO          { get; set; }
    public string?  OC                { get; set; }
    public string?  FOLIO_CORRECTIVO  { get; set; }
    public string?  FECHA_REGISTRO    { get; set; }
    public string?  RECIBIDO_POR      { get; set; }
    public string?  SUBCATEGORIA      { get; set; }
    public string?  TIPO_USO          { get; set; }
    public string?  MARCA             { get; set; }
    public string?  MODELO            { get; set; }
    public int?     CANTIDAD          { get; set; }
    public string?  NUMERO_SERIE      { get; set; }
    public string?  NUM_PARTE         { get; set; }
    public decimal? COSTO             { get; set; }
    public string?  MONEDA            { get; set; }
    public string?  PROVEEDOR         { get; set; }
    public string?  UBICACION         { get; set; }
    public string?  COMENTARIOS       { get; set; }
}

public class HerramientaNFFiltros
{
    public string? ID_UNICO         { get; set; }
    public string? OC               { get; set; }
    public string? FOLIO_CORRECTIVO { get; set; }
    public string? FECHA_REGISTRO   { get; set; }
    public string? RECIBIDO_POR     { get; set; }
    public string? SUBCATEGORIA     { get; set; }
    public string? TIPO_USO         { get; set; }
    public string? MARCA            { get; set; }
    public string? MODELO           { get; set; }
    public string? NUMERO_SERIE     { get; set; }
    public string? NUM_PARTE        { get; set; }
    public string? MONEDA           { get; set; }
    public string? PROVEEDOR        { get; set; }
    public string? UBICACION        { get; set; }
}

// ── Servicio ──────────────────────────────────────────────────────────────────

public class HerramientasNFService
{
    private readonly DbConnectionPool _pool;
    private readonly OrdenesDeCompraService _ordenesService;

    private static readonly string[] COLS =
    [
        "ID_UNICO","OC","FOLIO_CORRECTIVO","FECHA_REGISTRO","RECIBIDO_POR",
        "SUBCATEGORIA","TIPO_USO","MARCA","MODELO","CANTIDAD",
        "NUMERO_SERIE","NUM_PARTE","COSTO","MONEDA",
        "PROVEEDOR","UBICACION","COMENTARIOS"
    ];

    public HerramientasNFService(DbConnectionPool pool, OrdenesDeCompraService ordenesService)
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
            CREATE TABLE IF NOT EXISTS herramientas_nf (
                id                SERIAL PRIMARY KEY,
                id_unico          TEXT,
                oc                TEXT,
                folio_correctivo  TEXT,
                fecha_registro    DATE,
                recibido_por      TEXT,
                subcategoria      TEXT,
                tipo_uso          TEXT,
                marca             TEXT,
                modelo            TEXT,
                cantidad          INTEGER,
                numero_serie      TEXT,
                num_parte         TEXT,
                costo             NUMERIC(12,2),
                moneda            TEXT,
                proveedor         TEXT,
                ubicacion         TEXT,
                comentarios       TEXT
            );

            CREATE TABLE IF NOT EXISTS herramientas_nf_historial (
                id                SERIAL PRIMARY KEY,
                herramienta_id    INTEGER NOT NULL REFERENCES herramientas_nf(id) ON DELETE CASCADE,
                usuario           TEXT    NOT NULL,
                fecha             TIMESTAMPTZ DEFAULT NOW(),
                registro_anterior JSONB,
                registro_nuevo    JSONB
            );
            """, conn);

        await cmd.ExecuteNonQueryAsync();
    }

    // ── Listar (paginado + filtros) ───────────────────────────────────────
    public async Task<object> ListarAsync(int page, int limit, HerramientaNFFiltros f)
    {
        await using var conn = await _pool.OpenAsync();

        var (where, parms) = ConstruirWhere(f);
        var offset = (page - 1) * limit;

        // Total
        await using var cmdCount = new NpgsqlCommand(
            $"SELECT COUNT(*) FROM herramientas_nf {where}", conn);
        foreach (var (k, v) in parms) cmdCount.Parameters.AddWithValue(k, v ?? (object)DBNull.Value);
        var total = Convert.ToInt64(await cmdCount.ExecuteScalarAsync());

        // Datos
        await using var cmdData = new NpgsqlCommand(
            $@"SELECT id, id_unico, oc, folio_correctivo, fecha_registro,
                      recibido_por, subcategoria, tipo_uso, marca, modelo,
                      cantidad, numero_serie, num_parte, costo,
                      moneda, proveedor, ubicacion, comentarios
               FROM herramientas_nf {where}
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
                ID_UNICO         = Str(reader, 1),
                OC               = Str(reader, 2),
                FOLIO_CORRECTIVO = Str(reader, 3),
                FECHA_REGISTRO   = Str(reader, 4),
                RECIBIDO_POR     = Str(reader, 5),
                SUBCATEGORIA     = Str(reader, 6),
                TIPO_USO         = Str(reader, 7),
                MARCA            = Str(reader, 8),
                MODELO           = Str(reader, 9),
                CANTIDAD         = reader.IsDBNull(10) ? (int?)null     : reader.GetInt32(10),
                NUMERO_SERIE     = Str(reader, 11),
                NUM_PARTE        = Str(reader, 12),
                COSTO            = reader.IsDBNull(13) ? (decimal?)null : reader.GetDecimal(13),
                MONEDA           = Str(reader, 14),
                PROVEEDOR        = Str(reader, 15),
                UBICACION        = Str(reader, 16),
                COMENTARIOS      = Str(reader, 17)
            });
        }

        return new { total, data = lista };
    }

    // ── Crear ─────────────────────────────────────────────────────────────
    public async Task<int> CrearAsync(HerramientaNFDto dto, string usuario)
    {
        await using var conn = await _pool.OpenAsync();

        await using var cmd = new NpgsqlCommand("""
            INSERT INTO herramientas_nf
                (id_unico, oc, folio_correctivo, fecha_registro, recibido_por,
                 subcategoria, tipo_uso, marca, modelo, cantidad,
                 numero_serie, num_parte, costo, moneda,
                 proveedor, ubicacion, comentarios)
            VALUES
                (@id_unico, @oc, @folio_correctivo, @fecha_registro::date, @recibido_por,
                 @subcategoria, @tipo_uso, @marca, @modelo, @cantidad,
                 @numero_serie, @num_parte, @costo, @moneda,
                 @proveedor, @ubicacion, @comentarios)
            RETURNING id
            """, conn);

        AgregarParametros(cmd, dto);
        var id = Convert.ToInt32(await cmd.ExecuteScalarAsync());
        await conn.CloseAsync();
        _ordenesService.RecalcularPorCambioEnHija("HERRAMIENTAS NF", dto.OC, dto.FOLIO_CORRECTIVO);
        return id;
    }

    // ── Editar ────────────────────────────────────────────────────────────
    public async Task<bool> EditarAsync(int id, HerramientaNFDto dto, string usuario)
    {
        await using var conn = await _pool.OpenAsync();

        var anterior = await SnapshotAsync(conn, id);
        if (anterior is null) return false;

        await using var cmd = new NpgsqlCommand("""
            UPDATE herramientas_nf SET
                id_unico         = @id_unico,
                oc               = @oc,
                folio_correctivo = @folio_correctivo,
                fecha_registro   = @fecha_registro::date,
                recibido_por     = @recibido_por,
                subcategoria     = @subcategoria,
                tipo_uso         = @tipo_uso,
                marca            = @marca,
                modelo           = @modelo,
                cantidad         = @cantidad,
                numero_serie     = @numero_serie,
                num_parte        = @num_parte,
                costo            = @costo,
                moneda           = @moneda,
                proveedor        = @proveedor,
                ubicacion        = @ubicacion,
                comentarios      = @comentarios
            WHERE id = @id
            """, conn);

        AgregarParametros(cmd, dto);
        cmd.Parameters.AddWithValue("id", id);
        var rows = await cmd.ExecuteNonQueryAsync();
        if (rows == 0) return false;

        var nuevo = await SnapshotAsync(conn, id);
        await RegistrarHistorialAsync(conn, id, usuario, anterior, nuevo!);
        await conn.CloseAsync();
        _ordenesService.RecalcularPorCambioEnHija("HERRAMIENTAS NF", dto.OC, dto.FOLIO_CORRECTIVO);
        return true;
    }

    // ── Eliminar ──────────────────────────────────────────────────────────
    public async Task<bool> EliminarAsync(int id)
    {
        await using var conn = await _pool.OpenAsync();

        string? ocVal = null, folioVal = null;
        await using (var qSnap = new NpgsqlCommand(
            "SELECT oc, folio_correctivo FROM herramientas_nf WHERE id = @id", conn))
        {
            qSnap.Parameters.AddWithValue("id", id);
            await using var r = await qSnap.ExecuteReaderAsync();
            if (await r.ReadAsync())
            {
                ocVal = r.IsDBNull(0) ? null : r.GetString(0);
                folioVal = r.IsDBNull(1) ? null : r.GetString(1);
            }
        }

        await using var cmd = new NpgsqlCommand(
            "DELETE FROM herramientas_nf WHERE id = @id", conn);
        cmd.Parameters.AddWithValue("id", id);
        var deleted = await cmd.ExecuteNonQueryAsync() > 0;

        await conn.CloseAsync();

        if (deleted)
            _ordenesService.RecalcularPorCambioEnHija("HERRAMIENTAS NF", ocVal, folioVal);

        return deleted;
    }

    // ── Historial ─────────────────────────────────────────────────────────
    public async Task<List<object>> HistorialAsync(int id)
    {
        await using var conn = await _pool.OpenAsync();
        await using var cmd = new NpgsqlCommand(
            @"SELECT id, usuario, fecha, registro_anterior, registro_nuevo
              FROM herramientas_nf_historial
              WHERE herramienta_id = @id
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
    public async Task<byte[]> ExportarAsync(HerramientaNFFiltros f)
    {
        await using var conn = await _pool.OpenAsync();

        var (where, parms) = ConstruirWhere(f);

        await using var cmd = new NpgsqlCommand(
            $@"SELECT id, id_unico, oc, folio_correctivo, fecha_registro,
                      recibido_por, subcategoria, tipo_uso, marca, modelo,
                      cantidad, numero_serie, num_parte, costo,
                      moneda, proveedor, ubicacion, comentarios
               FROM herramientas_nf {where} ORDER BY id DESC", conn);

        foreach (var (k, v) in parms) cmd.Parameters.AddWithValue(k, v ?? (object)DBNull.Value);

        var rows = new List<string?[]>();
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
        {
            rows.Add(Enumerable.Range(0, 18)
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
            @"SELECT id, id_unico, oc, folio_correctivo, fecha_registro,
                     recibido_por, subcategoria, tipo_uso, marca, modelo,
                     cantidad, numero_serie, num_parte, costo,
                     moneda, proveedor, ubicacion, comentarios
              FROM herramientas_nf
              WHERE EXTRACT(YEAR FROM fecha_registro) = @anio
              ORDER BY id DESC", conn);
        cmd.Parameters.AddWithValue("anio", anio);

        var rows = new List<string?[]>();
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
        {
            rows.Add(Enumerable.Range(0, 18)
                .Select(i => r.IsDBNull(i) ? null : r.GetValue(i)?.ToString())
                .ToArray());
        }

        return GenerarExcel(rows);
    }

    // ── Helpers privados ──────────────────────────────────────────────────

    private static (string where, List<(string key, object? val)> parms) ConstruirWhere(HerramientaNFFiltros f)
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

        Add("id_unico",         f.ID_UNICO);
        Add("oc",               f.OC);
        Add("folio_correctivo", f.FOLIO_CORRECTIVO);
        Add("fecha_registro",   f.FECHA_REGISTRO);
        Add("recibido_por",     f.RECIBIDO_POR);
        Add("subcategoria",     f.SUBCATEGORIA);
        Add("tipo_uso",         f.TIPO_USO);
        Add("marca",            f.MARCA);
        Add("modelo",           f.MODELO);
        Add("numero_serie",     f.NUMERO_SERIE);
        Add("num_parte",        f.NUM_PARTE);
        Add("moneda",           f.MONEDA);
        Add("proveedor",        f.PROVEEDOR);
        Add("ubicacion",        f.UBICACION);

        var where = conds.Count > 0 ? "WHERE " + string.Join(" AND ", conds) : "";
        return (where, parms);
    }

    private static void AgregarParametros(NpgsqlCommand cmd, HerramientaNFDto dto)
    {
        cmd.Parameters.AddWithValue("id_unico",         (object?)dto.ID_UNICO         ?? DBNull.Value);
        cmd.Parameters.AddWithValue("oc",               (object?)dto.OC               ?? DBNull.Value);
        cmd.Parameters.AddWithValue("folio_correctivo", (object?)dto.FOLIO_CORRECTIVO ?? DBNull.Value);
        cmd.Parameters.AddWithValue("fecha_registro",   (object?)dto.FECHA_REGISTRO   ?? DBNull.Value);
        cmd.Parameters.AddWithValue("recibido_por",     (object?)dto.RECIBIDO_POR     ?? DBNull.Value);
        cmd.Parameters.AddWithValue("subcategoria",     (object?)dto.SUBCATEGORIA     ?? DBNull.Value);
        cmd.Parameters.AddWithValue("tipo_uso",         (object?)dto.TIPO_USO         ?? DBNull.Value);
        cmd.Parameters.AddWithValue("marca",            (object?)dto.MARCA            ?? DBNull.Value);
        cmd.Parameters.AddWithValue("modelo",           (object?)dto.MODELO           ?? DBNull.Value);
        cmd.Parameters.AddWithValue("cantidad",         (object?)dto.CANTIDAD         ?? DBNull.Value);
        cmd.Parameters.AddWithValue("numero_serie",     (object?)dto.NUMERO_SERIE     ?? DBNull.Value);
        cmd.Parameters.AddWithValue("num_parte",        (object?)dto.NUM_PARTE        ?? DBNull.Value);
        cmd.Parameters.AddWithValue("costo",            (object?)dto.COSTO            ?? DBNull.Value);
        cmd.Parameters.AddWithValue("moneda",           (object?)dto.MONEDA           ?? DBNull.Value);
        cmd.Parameters.AddWithValue("proveedor",        (object?)dto.PROVEEDOR        ?? DBNull.Value);
        cmd.Parameters.AddWithValue("ubicacion",        (object?)dto.UBICACION        ?? DBNull.Value);
        cmd.Parameters.AddWithValue("comentarios",      (object?)dto.COMENTARIOS      ?? DBNull.Value);
    }

    private async Task<Dictionary<string, object?>?> SnapshotAsync(NpgsqlConnection conn, int id)
    {
        await using var cmd = new NpgsqlCommand(
            @"SELECT id_unico, oc, folio_correctivo, fecha_registro, recibido_por,
                     subcategoria, tipo_uso, marca, modelo, cantidad,
                     numero_serie, num_parte, costo, moneda,
                     proveedor, ubicacion, comentarios
              FROM herramientas_nf WHERE id=@id", conn);
        cmd.Parameters.AddWithValue("id", id);

        await using var r = await cmd.ExecuteReaderAsync();
        if (!await r.ReadAsync()) return null;

        var snap = new Dictionary<string, object?>();
        for (int i = 0; i < COLS.Length; i++)
            snap[COLS[i]] = r.IsDBNull(i) ? null : r.GetValue(i)?.ToString();

        return snap;
    }

    private async Task RegistrarHistorialAsync(NpgsqlConnection conn, int herramientaId,
        string usuario, Dictionary<string, object?> anterior, Dictionary<string, object?> nuevo)
    {
        var antesJson   = System.Text.Json.JsonSerializer.Serialize(anterior);
        var despuesJson = System.Text.Json.JsonSerializer.Serialize(nuevo);

        await using var cmd = new NpgsqlCommand(
            """
            INSERT INTO herramientas_nf_historial (herramienta_id, usuario, registro_anterior, registro_nuevo)
            VALUES (@hid, @usr, @ant::jsonb, @nvo::jsonb)
            """, conn);

        cmd.Parameters.AddWithValue("hid", herramientaId);
        cmd.Parameters.AddWithValue("usr", usuario);
        cmd.Parameters.AddWithValue("ant", antesJson);
        cmd.Parameters.AddWithValue("nvo", despuesJson);

        await cmd.ExecuteNonQueryAsync();
    }

    private static byte[] GenerarExcel(List<string?[]> rows)
    {
        ExcelPackage.License.SetNonCommercialPersonal("IT Control Center");
        using var pkg = new ExcelPackage();
        var ws = pkg.Workbook.Worksheets.Add("Herramientas NF");

        string[] headers =
        [
            "ID","ID ÚNICO","OC","FOLIO CORRECTIVO","FECHA REGISTRO",
            "RECIBIDO POR","SUBCATEGORÍA","TIPO USO","MARCA","MODELO",
            "CANTIDAD","NÚMERO SERIE","NUM PARTE","COSTO",
            "MONEDA","PROVEEDOR","UBICACIÓN","COMENTARIOS"
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
