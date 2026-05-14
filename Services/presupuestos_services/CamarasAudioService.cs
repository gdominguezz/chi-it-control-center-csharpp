using ChiIT.Data;
using Microsoft.Data.SqlClient;

using OfficeOpenXml;
using OfficeOpenXml.Style;

using System.Drawing;

namespace ChiIT.Services;

// ── DTOs / Modelos ────────────────────────────────────────────────────────────

public class CamaraAudioDto
{
    public string?  OC                      { get; set; }
    public string?  FOLIO_INVENTARIO        { get; set; }
    public string?  FECHA_REGISTRO          { get; set; }
    public string?  RECIBIDO_POR            { get; set; }
    public string?  SUBCATEGORIA            { get; set; }
    public string?  TIPO                    { get; set; }
    public string?  MARCA                   { get; set; }
    public string?  MODELO                  { get; set; }
    public string?  NUMERO_DE_SERIE         { get; set; }
    public string?  PROVEEDOR               { get; set; }
    public int?     CANTIDAD                { get; set; }
    public decimal? COSTO                   { get; set; }
    public string?  MONEDA                  { get; set; }
    public string?  DESTINO                 { get; set; }
    public string?  ACCESORIOS              { get; set; }
    public string?  FECHA_DE_SALIDA         { get; set; }
    public string?  PLANTA                  { get; set; }
    public string?  DESTINO2                { get; set; }
    public string?  PERSONAL_IT_QUE_ASIGNA  { get; set; }
    public string?  FOLIO_DE_SERVICIO       { get; set; }
}

public class CamaraAudioFiltros
{
    public string? OC                      { get; set; }
    public string? FOLIO_INVENTARIO        { get; set; }
    public string? FECHA_REGISTRO          { get; set; }
    public string? RECIBIDO_POR            { get; set; }
    public string? SUBCATEGORIA            { get; set; }
    public string? TIPO                    { get; set; }
    public string? MARCA                   { get; set; }
    public string? MODELO                  { get; set; }
    public string? NUMERO_DE_SERIE         { get; set; }
    public string? PROVEEDOR               { get; set; }
    public string? MONEDA                  { get; set; }
    public string? DESTINO                 { get; set; }
    public string? ACCESORIOS              { get; set; }
    public string? PLANTA                  { get; set; }
    public string? DESTINO2                { get; set; }
    public string? PERSONAL_IT_QUE_ASIGNA  { get; set; }
    public string? FOLIO_DE_SERVICIO       { get; set; }
}

// ── Servicio ──────────────────────────────────────────────────────────────────

public class CamarasAudioService
{
    private readonly DbConnectionPool _pool;
    private readonly OrdenesDeCompraService _ordenesService;

    private static readonly string[] COLS =
    [
        "OC","FOLIO_INVENTARIO","FECHA_REGISTRO","RECIBIDO_POR",
        "SUBCATEGORIA","TIPO","MARCA","MODELO","NUMERO_DE_SERIE",
        "PROVEEDOR","CANTIDAD","COSTO","MONEDA","DESTINO",
        "ACCESORIOS","FECHA_DE_SALIDA","PLANTA","DESTINO2",
        "PERSONAL_IT_QUE_ASIGNA","FOLIO_DE_SERVICIO"
    ];

    public CamarasAudioService(DbConnectionPool pool, OrdenesDeCompraService ordenesService)
    {
        _pool = pool;
        _ordenesService = ordenesService;
        _ = InicializarTablaAsync();
    }

    // ── DDL ───────────────────────────────────────────────────────────────
    private async Task InicializarTablaAsync()
    {
        await using var conn = await _pool.OpenAsync();
        await using var cmd = new SqlCommand("""
            CREATE TABLE IF NOT EXISTS camaras_audio (
                id                      SERIAL PRIMARY KEY,
                oc                      TEXT,
                folio_inventario        TEXT,
                fecha_registro          DATE,
                recibido_por            TEXT,
                subcategoria            TEXT,
                tipo                    TEXT,
                marca                   TEXT,
                modelo                  TEXT,
                numero_de_serie         TEXT,
                proveedor               TEXT,
                cantidad                INTEGER,
                costo                   NUMERIC(12,2),
                moneda                  TEXT,
                destino                 TEXT,
                accesorios              TEXT,
                fecha_de_salida         DATE,
                planta                  TEXT,
                destino2                TEXT,
                personal_it_que_asigna  TEXT,
                folio_de_servicio       TEXT
            );

            CREATE TABLE IF NOT EXISTS camaras_audio_historial (
                id                SERIAL PRIMARY KEY,
                camara_id         INTEGER NOT NULL REFERENCES camaras_audio(id) ON DELETE CASCADE,
                usuario           TEXT    NOT NULL,
                fecha             TIMESTAMPTZ DEFAULT GETDATE(),
                registro_anterior JSONB,
                registro_nuevo    JSONB
            );
            """, conn);

        await cmd.ExecuteNonQueryAsync();
    }

    // ── Listar (paginado + filtros) ───────────────────────────────────────
    public async Task<object> ListarAsync(int page, int limit, CamaraAudioFiltros f)
    {
        await using var conn = await _pool.OpenAsync();

        var (where, parms) = ConstruirWhere(f);
        var offset = (page - 1) * limit;

        // Total
        await using var cmdCount = new SqlCommand(
            $"SELECT COUNT(*) FROM camaras_audio {where}", conn);
        foreach (var (k, v) in parms) cmdCount.Parameters.AddWithValue(k, v ?? (object)DBNull.Value);
        var total = Convert.ToInt64(await cmdCount.ExecuteScalarAsync());

        // Datos
        await using var cmdData = new SqlCommand(
            $@"SELECT id, oc, folio_inventario, fecha_registro, recibido_por,
                      subcategoria, tipo, marca, modelo, numero_de_serie,
                      proveedor, cantidad, costo, moneda, destino,
                      accesorios, fecha_de_salida, planta, destino2,
                      personal_it_que_asigna, folio_de_servicio
               FROM camaras_audio {where}
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
                ID                     = reader.GetInt32(0),
                OC                     = Str(reader, 1),
                FOLIO_INVENTARIO       = Str(reader, 2),
                FECHA_REGISTRO         = Str(reader, 3),
                RECIBIDO_POR           = Str(reader, 4),
                SUBCATEGORIA           = Str(reader, 5),
                TIPO                   = Str(reader, 6),
                MARCA                  = Str(reader, 7),
                MODELO                 = Str(reader, 8),
                NUMERO_DE_SERIE        = Str(reader, 9),
                PROVEEDOR              = Str(reader, 10),
                CANTIDAD               = reader.IsDBNull(11) ? (int?)null     : reader.GetInt32(11),
                COSTO                  = reader.IsDBNull(12) ? (decimal?)null : reader.GetDecimal(12),
                MONEDA                 = Str(reader, 13),
                DESTINO                = Str(reader, 14),
                ACCESORIOS             = Str(reader, 15),
                FECHA_DE_SALIDA        = Str(reader, 16),
                PLANTA                 = Str(reader, 17),
                DESTINO2               = Str(reader, 18),
                PERSONAL_IT_QUE_ASIGNA = Str(reader, 19),
                FOLIO_DE_SERVICIO      = Str(reader, 20)
            });
        }

        return new { total, data = lista };
    }

    // ── Crear ─────────────────────────────────────────────────────────────
    public async Task<int> CrearAsync(CamaraAudioDto dto, string usuario)
    {
        await using var conn = await _pool.OpenAsync();

        await using var cmd = new SqlCommand("""
            INSERT INTO camaras_audio
                (oc, folio_inventario, fecha_registro, recibido_por,
                 subcategoria, tipo, marca, modelo, numero_de_serie,
                 proveedor, cantidad, costo, moneda, destino,
                 accesorios, fecha_de_salida, planta, destino2,
                 personal_it_que_asigna, folio_de_servicio)
            VALUES
                (@oc, @folio_inventario, @fecha_registro, @recibido_por,
                 @subcategoria, @tipo, @marca, @modelo, @numero_de_serie,
                 @proveedor, @cantidad, @costo, @moneda, @destino,
                 @accesorios, @fecha_de_salida, @planta, @destino2,
                 @personal_it_que_asigna, @folio_de_servicio)
            OUTPUT INSERTED.id
            """, conn);

        AgregarParametros(cmd, dto);
        var id = Convert.ToInt32(await cmd.ExecuteScalarAsync());
        await conn.CloseAsync();
        Console.WriteLine("[CamarasAudio] RecalcularPorCambioEnHija CAMARAS AUDIO " + dto.OC, dto.FOLIO_INVENTARIO);
        try { _ordenesService.RecalcularPorCambioEnHija("CAMARAS AUDIO", dto.OC, dto.FOLIO_INVENTARIO); }
        catch (Exception ex) { Console.WriteLine("[CamarasAudio] ERROR RecalcularPorCambioEnHija: " + ex.Message); }
        return id;
    }

    // ── Editar ────────────────────────────────────────────────────────────
    public async Task<bool> EditarAsync(int id, CamaraAudioDto dto, string usuario)
    {
        await using var conn = await _pool.OpenAsync();

        var anterior = await SnapshotAsync(conn, id);
        if (anterior is null) return false;

        await using var cmd = new SqlCommand("""
            UPDATE camaras_audio SET
                oc                     = @oc,
                folio_inventario       = @folio_inventario,
                fecha_registro         = @fecha_registro,
                recibido_por           = @recibido_por,
                subcategoria           = @subcategoria,
                tipo                   = @tipo,
                marca                  = @marca,
                modelo                 = @modelo,
                numero_de_serie        = @numero_de_serie,
                proveedor              = @proveedor,
                cantidad               = @cantidad,
                costo                  = @costo,
                moneda                 = @moneda,
                destino                = @destino,
                accesorios             = @accesorios,
                fecha_de_salida        = @fecha_de_salida,
                planta                 = @planta,
                destino2               = @destino2,
                personal_it_que_asigna = @personal_it_que_asigna,
                folio_de_servicio      = @folio_de_servicio
            WHERE id = @id
            """, conn);

        AgregarParametros(cmd, dto);
        cmd.Parameters.AddWithValue("id", id);
        var rows = await cmd.ExecuteNonQueryAsync();
        if (rows == 0) return false;

        var nuevo = await SnapshotAsync(conn, id);
        await RegistrarHistorialAsync(conn, id, usuario, anterior, nuevo!);
        await conn.CloseAsync();
        Console.WriteLine("[CamarasAudio] RecalcularPorCambioEnHija CAMARAS AUDIO " + dto.OC, dto.FOLIO_INVENTARIO);
        try { _ordenesService.RecalcularPorCambioEnHija("CAMARAS AUDIO", dto.OC, dto.FOLIO_INVENTARIO); }
        catch (Exception ex) { Console.WriteLine("[CamarasAudio] ERROR RecalcularPorCambioEnHija: " + ex.Message); }
        return true;
    }

    // ── Eliminar ──────────────────────────────────────────────────────────
    public async Task<bool> EliminarAsync(int id)
    {
        await using var conn = await _pool.OpenAsync();

        string? ocVal = null, folioVal = null;
        await using (var qSnap = new SqlCommand(
            "SELECT oc, folio_inventario FROM camaras_audio WHERE id = @id", conn))
        {
            qSnap.Parameters.AddWithValue("id", id);
            await using var r = await qSnap.ExecuteReaderAsync();
            if (await r.ReadAsync())
            {
                ocVal = r.IsDBNull(0) ? null : r.GetString(0);
                folioVal = r.IsDBNull(1) ? null : r.GetString(1);
            }
        }

        await using var cmd = new SqlCommand(
            "DELETE FROM camaras_audio WHERE id = @id", conn);
        cmd.Parameters.AddWithValue("id", id);
        var deleted = await cmd.ExecuteNonQueryAsync() > 0;

        await conn.CloseAsync();

        if (deleted)
            Console.WriteLine("[CamarasAudio] RecalcularPorCambioEnHija CAMARAS AUDIO " + ocVal, folioVal);
            try { _ordenesService.RecalcularPorCambioEnHija("CAMARAS AUDIO", ocVal, folioVal); }
            catch (Exception ex) { Console.WriteLine("[CamarasAudio] ERROR RecalcularPorCambioEnHija: " + ex.Message); }

        return deleted;
    }

    // ── Historial ─────────────────────────────────────────────────────────
    public async Task<List<object>> HistorialAsync(int id)
    {
        await using var conn = await _pool.OpenAsync();
        await using var cmd = new SqlCommand(
            @"SELECT id, usuario, fecha, registro_anterior, registro_nuevo
              FROM camaras_audio_historial
              WHERE camara_id = @id
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
    public async Task<byte[]> ExportarAsync(CamaraAudioFiltros f)
    {
        await using var conn = await _pool.OpenAsync();

        var (where, parms) = ConstruirWhere(f);

        await using var cmd = new SqlCommand(
            $@"SELECT id, oc, folio_inventario, fecha_registro, recibido_por,
                      subcategoria, tipo, marca, modelo, numero_de_serie,
                      proveedor, cantidad, costo, moneda, destino,
                      accesorios, fecha_de_salida, planta, destino2,
                      personal_it_que_asigna, folio_de_servicio
               FROM camaras_audio {where} ORDER BY id DESC", conn);

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
            @"SELECT id, oc, folio_inventario, fecha_registro, recibido_por,
                     subcategoria, tipo, marca, modelo, numero_de_serie,
                     proveedor, cantidad, costo, moneda, destino,
                     accesorios, fecha_de_salida, planta, destino2,
                     personal_it_que_asigna, folio_de_servicio
              FROM camaras_audio
              WHERE EXTRACT(YEAR FROM fecha_registro) = @anio
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

    private static (string where, List<(string key, object? val)> parms) ConstruirWhere(CamaraAudioFiltros f)
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

        Add("oc",                     f.OC);
        Add("folio_inventario",       f.FOLIO_INVENTARIO);
        Add("fecha_registro",         f.FECHA_REGISTRO);
        Add("recibido_por",           f.RECIBIDO_POR);
        Add("subcategoria",           f.SUBCATEGORIA);
        Add("tipo",                   f.TIPO);
        Add("marca",                  f.MARCA);
        Add("modelo",                 f.MODELO);
        Add("numero_de_serie",        f.NUMERO_DE_SERIE);
        Add("proveedor",              f.PROVEEDOR);
        Add("moneda",                 f.MONEDA);
        Add("destino",                f.DESTINO);
        Add("accesorios",             f.ACCESORIOS);
        Add("planta",                 f.PLANTA);
        Add("destino2",               f.DESTINO2);
        Add("personal_it_que_asigna", f.PERSONAL_IT_QUE_ASIGNA);
        Add("folio_de_servicio",      f.FOLIO_DE_SERVICIO);

        var where = conds.Count > 0 ? "WHERE " + string.Join(" AND ", conds) : "";
        return (where, parms);
    }

    private static void AgregarParametros(SqlCommand cmd, CamaraAudioDto dto)
    {
        cmd.Parameters.AddWithValue("oc",                     (object?)dto.OC                     ?? DBNull.Value);
        cmd.Parameters.AddWithValue("folio_inventario",       (object?)dto.FOLIO_INVENTARIO        ?? DBNull.Value);
        cmd.Parameters.AddWithValue("fecha_registro",         (object?)dto.FECHA_REGISTRO          ?? DBNull.Value);
        cmd.Parameters.AddWithValue("recibido_por",           (object?)dto.RECIBIDO_POR            ?? DBNull.Value);
        cmd.Parameters.AddWithValue("subcategoria",           (object?)dto.SUBCATEGORIA            ?? DBNull.Value);
        cmd.Parameters.AddWithValue("tipo",                   (object?)dto.TIPO                    ?? DBNull.Value);
        cmd.Parameters.AddWithValue("marca",                  (object?)dto.MARCA                   ?? DBNull.Value);
        cmd.Parameters.AddWithValue("modelo",                 (object?)dto.MODELO                  ?? DBNull.Value);
        cmd.Parameters.AddWithValue("numero_de_serie",        (object?)dto.NUMERO_DE_SERIE         ?? DBNull.Value);
        cmd.Parameters.AddWithValue("proveedor",              (object?)dto.PROVEEDOR               ?? DBNull.Value);
        cmd.Parameters.AddWithValue("cantidad",               (object?)dto.CANTIDAD                ?? DBNull.Value);
        cmd.Parameters.AddWithValue("costo",                  (object?)dto.COSTO                   ?? DBNull.Value);
        cmd.Parameters.AddWithValue("moneda",                 (object?)dto.MONEDA                  ?? DBNull.Value);
        cmd.Parameters.AddWithValue("destino",                (object?)dto.DESTINO                 ?? DBNull.Value);
        cmd.Parameters.AddWithValue("accesorios",             (object?)dto.ACCESORIOS              ?? DBNull.Value);
        cmd.Parameters.AddWithValue("fecha_de_salida",        (object?)dto.FECHA_DE_SALIDA         ?? DBNull.Value);
        cmd.Parameters.AddWithValue("planta",                 (object?)dto.PLANTA                  ?? DBNull.Value);
        cmd.Parameters.AddWithValue("destino2",               (object?)dto.DESTINO2                ?? DBNull.Value);
        cmd.Parameters.AddWithValue("personal_it_que_asigna", (object?)dto.PERSONAL_IT_QUE_ASIGNA  ?? DBNull.Value);
        cmd.Parameters.AddWithValue("folio_de_servicio",      (object?)dto.FOLIO_DE_SERVICIO       ?? DBNull.Value);
    }

    private async Task<Dictionary<string, object?>?> SnapshotAsync(SqlConnection conn, int id)
    {
        await using var cmd = new SqlCommand(
            @"SELECT oc, folio_inventario, fecha_registro, recibido_por,
                     subcategoria, tipo, marca, modelo, numero_de_serie,
                     proveedor, cantidad, costo, moneda, destino,
                     accesorios, fecha_de_salida, planta, destino2,
                     personal_it_que_asigna, folio_de_servicio
              FROM camaras_audio WHERE id=@id", conn);
        cmd.Parameters.AddWithValue("id", id);

        await using var r = await cmd.ExecuteReaderAsync();
        if (!await r.ReadAsync()) return null;

        var snap = new Dictionary<string, object?>();
        for (int i = 0; i < COLS.Length; i++)
            snap[COLS[i]] = r.IsDBNull(i) ? null : r.GetValue(i)?.ToString();

        return snap;
    }

    private async Task RegistrarHistorialAsync(SqlConnection conn, int camaraId,
        string usuario, Dictionary<string, object?> anterior, Dictionary<string, object?> nuevo)
    {
        var antesJson   = System.Text.Json.JsonSerializer.Serialize(anterior);
        var despuesJson = System.Text.Json.JsonSerializer.Serialize(nuevo);

        await using var cmd = new SqlCommand(
            """
            INSERT INTO camaras_audio_historial (camara_id, usuario, registro_anterior, registro_nuevo)
            VALUES (@cid, @usr, @ant, @nvo)
            """, conn);

        cmd.Parameters.AddWithValue("cid", camaraId);
        cmd.Parameters.AddWithValue("usr", usuario);
        cmd.Parameters.AddWithValue("ant", antesJson);
        cmd.Parameters.AddWithValue("nvo", despuesJson);

        await cmd.ExecuteNonQueryAsync();
    }

    private static byte[] GenerarExcel(List<string?[]> rows)
    {
        ExcelPackage.License.SetNonCommercialPersonal("IT Control Center");
        using var pkg = new ExcelPackage();
        var ws = pkg.Workbook.Worksheets.Add("Camaras Audio");

        string[] headers =
        [
            "ID","OC","FOLIO INVENTARIO","FECHA REGISTRO","RECIBIDO POR",
            "SUBCATEGORÍA","TIPO","MARCA","MODELO","NÚMERO DE SERIE",
            "PROVEEDOR","CANTIDAD","COSTO","MONEDA","DESTINO",
            "ACCESORIOS","FECHA DE SALIDA","PLANTA","DESTINO 2",
            "PERSONAL IT QUE ASIGNA","FOLIO DE SERVICIO"
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

    private static string? Str(SqlDataReader r, int i)
        => r.IsDBNull(i) ? null : r.GetValue(i)?.ToString();
}
