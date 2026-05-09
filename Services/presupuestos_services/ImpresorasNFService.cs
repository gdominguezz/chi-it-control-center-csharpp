using ChiIT.Data;
using Npgsql;

using OfficeOpenXml;
using OfficeOpenXml.Style;

using System.Drawing;

namespace ChiIT.Services;

// ── DTOs / Modelos ────────────────────────────────────────────────────────────

public class ImpresoraNFDto
{
    public string?  ID_UNICO               { get; set; }
    public string?  OC                     { get; set; }
    public string?  FOLIO_INVENTARIO       { get; set; }
    public string?  FECHA_DE_ENTRADA       { get; set; }
    public string?  RECIBIDO_POR           { get; set; }
    public string?  MARCA                  { get; set; }
    public string?  MODELO                 { get; set; }
    public string?  NUMERO_DE_SERIE        { get; set; }
    public string?  TIPO                   { get; set; }
    public int?     CANTIDAD               { get; set; }
    public string?  IP                     { get; set; }
    public string?  MAC                    { get; set; }
    public string?  PROVEEDOR              { get; set; }
    public decimal? COSTO                  { get; set; }
    public string?  MONEDA                 { get; set; }
    public string?  UBICACION              { get; set; }
    public string?  ESTADO                 { get; set; }
    public string?  PLANTA                 { get; set; }
    public string?  DISPONIBLE             { get; set; }
    public string?  FECHA_DE_ASIGNACION    { get; set; }
    public string?  OBSERVACIONES          { get; set; }
    public string?  FECHA_DE_SALIDA        { get; set; }
    public string?  DESTINO_PLANTA         { get; set; }
    public string?  ASIGNADO_A             { get; set; }
    public string?  PERSONAL_IT_QUE_ASIGNA { get; set; }
    public string?  FECHA_DE_MANTENIMIENTO { get; set; }
}

public class ImpresoraNFFiltros
{
    public string? ID_UNICO               { get; set; }
    public string? OC                     { get; set; }
    public string? FOLIO_INVENTARIO       { get; set; }
    public string? FECHA_DE_ENTRADA       { get; set; }
    public string? RECIBIDO_POR           { get; set; }
    public string? MARCA                  { get; set; }
    public string? MODELO                 { get; set; }
    public string? NUMERO_DE_SERIE        { get; set; }
    public string? TIPO                   { get; set; }
    public string? IP                     { get; set; }
    public string? MAC                    { get; set; }
    public string? PROVEEDOR              { get; set; }
    public string? MONEDA                 { get; set; }
    public string? UBICACION              { get; set; }
    public string? ESTADO                 { get; set; }
    public string? PLANTA                 { get; set; }
    public string? DISPONIBLE             { get; set; }
    public string? ASIGNADO_A             { get; set; }
    public string? DESTINO_PLANTA         { get; set; }
    public string? PERSONAL_IT_QUE_ASIGNA { get; set; }
}

// ── Servicio ──────────────────────────────────────────────────────────────────

public class ImpresorasNFService
{
    private readonly DbConnectionPool _pool;
    private readonly OrdenesDeCompraService _ordenesService;

    private static readonly string[] COLS =
    [
        "ID_UNICO", "OC", "FOLIO_INVENTARIO", "FECHA_DE_ENTRADA", "RECIBIDO_POR",
        "MARCA", "MODELO", "NUMERO_DE_SERIE", "TIPO", "CANTIDAD",
        "IP", "MAC", "PROVEEDOR", "COSTO", "MONEDA",
        "UBICACION", "ESTADO", "PLANTA", "DISPONIBLE", "FECHA_DE_ASIGNACION",
        "OBSERVACIONES", "FECHA_DE_SALIDA", "DESTINO_PLANTA", "ASIGNADO_A",
        "PERSONAL_IT_QUE_ASIGNA", "FECHA_DE_MANTENIMIENTO"
    ];

    public ImpresorasNFService(DbConnectionPool pool, OrdenesDeCompraService ordenesService)
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
            CREATE TABLE IF NOT EXISTS impresoras_nf (
                id                      SERIAL PRIMARY KEY,
                id_unico                TEXT,
                oc                      TEXT,
                folio_inventario        TEXT,
                fecha_de_entrada        DATE,
                recibido_por            TEXT,
                marca                   TEXT,
                modelo                  TEXT,
                numero_de_serie         TEXT,
                tipo                    TEXT,
                cantidad                INTEGER,
                ip                      TEXT,
                mac                     TEXT,
                proveedor               TEXT,
                costo                   NUMERIC(12,2),
                moneda                  TEXT,
                ubicacion               TEXT,
                estado                  TEXT,
                planta                  TEXT,
                disponible              TEXT,
                fecha_de_asignacion     DATE,
                observaciones           TEXT,
                fecha_de_salida         DATE,
                destino_planta          TEXT,
                asignado_a              TEXT,
                personal_it_que_asigna  TEXT,
                fecha_de_mantenimiento  DATE
            );

            CREATE TABLE IF NOT EXISTS impresoras_nf_historial (
                id                SERIAL PRIMARY KEY,
                impresora_id      INTEGER NOT NULL REFERENCES impresoras_nf(id) ON DELETE CASCADE,
                usuario           TEXT    NOT NULL,
                fecha             TIMESTAMPTZ DEFAULT NOW(),
                registro_anterior JSONB,
                registro_nuevo    JSONB
            );
            """, conn);

        await cmd.ExecuteNonQueryAsync();
    }

    // ── Listar (paginado + filtros) ───────────────────────────────────────
    public async Task<object> ListarAsync(int page, int limit, ImpresoraNFFiltros f)
    {
        await using var conn = await _pool.OpenAsync();

        var (where, parms) = ConstruirWhere(f);
        var offset = (page - 1) * limit;

        // Total
        await using var cmdCount = new NpgsqlCommand(
            $"SELECT COUNT(*) FROM impresoras_nf {where}", conn);
        foreach (var (k, v) in parms) cmdCount.Parameters.AddWithValue(k, v ?? (object)DBNull.Value);
        var total = Convert.ToInt64(await cmdCount.ExecuteScalarAsync());

        // Datos
        await using var cmdData = new NpgsqlCommand(
            $@"SELECT id,
                      id_unico, oc, folio_inventario, fecha_de_entrada, recibido_por,
                      marca, modelo, numero_de_serie, tipo, cantidad,
                      ip, mac, proveedor, costo, moneda,
                      ubicacion, estado, planta, disponible, fecha_de_asignacion,
                      observaciones, fecha_de_salida, destino_planta, asignado_a,
                      personal_it_que_asigna, fecha_de_mantenimiento
               FROM impresoras_nf {where}
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
                ID_UNICO               = Str(reader, 1),
                OC                     = Str(reader, 2),
                FOLIO_INVENTARIO       = Str(reader, 3),
                FECHA_DE_ENTRADA       = Str(reader, 4),
                RECIBIDO_POR           = Str(reader, 5),
                MARCA                  = Str(reader, 6),
                MODELO                 = Str(reader, 7),
                NUMERO_DE_SERIE        = Str(reader, 8),
                TIPO                   = Str(reader, 9),
                CANTIDAD               = reader.IsDBNull(10) ? (int?)null     : reader.GetInt32(10),
                IP                     = Str(reader, 11),
                MAC                    = Str(reader, 12),
                PROVEEDOR              = Str(reader, 13),
                COSTO                  = reader.IsDBNull(14) ? (decimal?)null : reader.GetDecimal(14),
                MONEDA                 = Str(reader, 15),
                UBICACION              = Str(reader, 16),
                ESTADO                 = Str(reader, 17),
                PLANTA                 = Str(reader, 18),
                DISPONIBLE             = Str(reader, 19),
                FECHA_DE_ASIGNACION    = Str(reader, 20),
                OBSERVACIONES          = Str(reader, 21),
                FECHA_DE_SALIDA        = Str(reader, 22),
                DESTINO_PLANTA         = Str(reader, 23),
                ASIGNADO_A             = Str(reader, 24),
                PERSONAL_IT_QUE_ASIGNA = Str(reader, 25),
                FECHA_DE_MANTENIMIENTO = Str(reader, 26)
            });
        }

        return new { total, data = lista };
    }

    // ── Crear ─────────────────────────────────────────────────────────────
    public async Task<int> CrearAsync(ImpresoraNFDto dto, string usuario)
    {
        await using var conn = await _pool.OpenAsync();

        await using var cmd = new NpgsqlCommand("""
            INSERT INTO impresoras_nf
                (id_unico, oc, folio_inventario, fecha_de_entrada, recibido_por,
                 marca, modelo, numero_de_serie, tipo, cantidad,
                 ip, mac, proveedor, costo, moneda,
                 ubicacion, estado, planta, disponible, fecha_de_asignacion,
                 observaciones, fecha_de_salida, destino_planta, asignado_a,
                 personal_it_que_asigna, fecha_de_mantenimiento)
            VALUES
                (@id_unico, @oc, @folio_inventario, @fecha_de_entrada::date, @recibido_por,
                 @marca, @modelo, @numero_de_serie, @tipo, @cantidad,
                 @ip, @mac, @proveedor, @costo, @moneda,
                 @ubicacion, @estado, @planta, @disponible, @fecha_de_asignacion::date,
                 @observaciones, @fecha_de_salida::date, @destino_planta, @asignado_a,
                 @personal_it_que_asigna, @fecha_de_mantenimiento::date)
            RETURNING id
            """, conn);

        AgregarParametros(cmd, dto);
        var id = Convert.ToInt32(await cmd.ExecuteScalarAsync());
        await conn.CloseAsync();
        Console.WriteLine("[ImpresorasNF] RecalcularPorCambioEnHija IMPRESORAS NF " + dto.OC, dto.FOLIO_INVENTARIO);
        try { _ordenesService.RecalcularPorCambioEnHija("IMPRESORAS NF", dto.OC, dto.FOLIO_INVENTARIO); }
        catch (Exception ex) { Console.WriteLine("[ImpresorasNF] ERROR RecalcularPorCambioEnHija: " + ex.Message); }
        return id;
    }

    // ── Editar ────────────────────────────────────────────────────────────
    public async Task<bool> EditarAsync(int id, ImpresoraNFDto dto, string usuario)
    {
        await using var conn = await _pool.OpenAsync();

        var anterior = await SnapshotAsync(conn, id);
        if (anterior is null) return false;

        await using var cmd = new NpgsqlCommand("""
            UPDATE impresoras_nf SET
                id_unico               = @id_unico,
                oc                     = @oc,
                folio_inventario       = @folio_inventario,
                fecha_de_entrada       = @fecha_de_entrada::date,
                recibido_por           = @recibido_por,
                marca                  = @marca,
                modelo                 = @modelo,
                numero_de_serie        = @numero_de_serie,
                tipo                   = @tipo,
                cantidad               = @cantidad,
                ip                     = @ip,
                mac                    = @mac,
                proveedor              = @proveedor,
                costo                  = @costo,
                moneda                 = @moneda,
                ubicacion              = @ubicacion,
                estado                 = @estado,
                planta                 = @planta,
                disponible             = @disponible,
                fecha_de_asignacion    = @fecha_de_asignacion::date,
                observaciones          = @observaciones,
                fecha_de_salida        = @fecha_de_salida::date,
                destino_planta         = @destino_planta,
                asignado_a             = @asignado_a,
                personal_it_que_asigna = @personal_it_que_asigna,
                fecha_de_mantenimiento = @fecha_de_mantenimiento::date
            WHERE id = @id
            """, conn);

        AgregarParametros(cmd, dto);
        cmd.Parameters.AddWithValue("id", id);
        var rows = await cmd.ExecuteNonQueryAsync();
        if (rows == 0) return false;

        var nuevo = await SnapshotAsync(conn, id);
        await RegistrarHistorialAsync(conn, id, usuario, anterior, nuevo!);
        await conn.CloseAsync();
        Console.WriteLine("[ImpresorasNF] RecalcularPorCambioEnHija IMPRESORAS NF " + dto.OC, dto.FOLIO_INVENTARIO);
        try { _ordenesService.RecalcularPorCambioEnHija("IMPRESORAS NF", dto.OC, dto.FOLIO_INVENTARIO); }
        catch (Exception ex) { Console.WriteLine("[ImpresorasNF] ERROR RecalcularPorCambioEnHija: " + ex.Message); }
        return true;
    }

    // ── Eliminar ──────────────────────────────────────────────────────────
    public async Task<bool> EliminarAsync(int id)
    {
        await using var conn = await _pool.OpenAsync();

        string? ocVal = null, folioVal = null;
        await using (var qSnap = new NpgsqlCommand(
            "SELECT oc, folio_inventario FROM impresoras_nf WHERE id = @id", conn))
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
            "DELETE FROM impresoras_nf WHERE id = @id", conn);
        cmd.Parameters.AddWithValue("id", id);
        var deleted = await cmd.ExecuteNonQueryAsync() > 0;

        await conn.CloseAsync();

        if (deleted)
            Console.WriteLine("[ImpresorasNF] RecalcularPorCambioEnHija IMPRESORAS NF " + ocVal, folioVal);
            try { _ordenesService.RecalcularPorCambioEnHija("IMPRESORAS NF", ocVal, folioVal); }
            catch (Exception ex) { Console.WriteLine("[ImpresorasNF] ERROR RecalcularPorCambioEnHija: " + ex.Message); }

        return deleted;
    }

    // ── Historial ─────────────────────────────────────────────────────────
    public async Task<List<object>> HistorialAsync(int id)
    {
        await using var conn = await _pool.OpenAsync();
        await using var cmd = new NpgsqlCommand(
            @"SELECT id, usuario, fecha, registro_anterior, registro_nuevo
              FROM impresoras_nf_historial
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

    // ── Exportar (filtrado) ───────────────────────────────────────────────
    public async Task<byte[]> ExportarAsync(ImpresoraNFFiltros f)
    {
        await using var conn = await _pool.OpenAsync();

        var (where, parms) = ConstruirWhere(f);

        await using var cmd = new NpgsqlCommand(
            $@"SELECT id,
                      id_unico, oc, folio_inventario, fecha_de_entrada, recibido_por,
                      marca, modelo, numero_de_serie, tipo, cantidad,
                      ip, mac, proveedor, costo, moneda,
                      ubicacion, estado, planta, disponible, fecha_de_asignacion,
                      observaciones, fecha_de_salida, destino_planta, asignado_a,
                      personal_it_que_asigna, fecha_de_mantenimiento
               FROM impresoras_nf {where} ORDER BY id DESC", conn);

        foreach (var (k, v) in parms) cmd.Parameters.AddWithValue(k, v ?? (object)DBNull.Value);

        var rows = new List<string?[]>();
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
        {
            rows.Add(Enumerable.Range(0, 27)
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
            @"SELECT id,
                     id_unico, oc, folio_inventario, fecha_de_entrada, recibido_por,
                     marca, modelo, numero_de_serie, tipo, cantidad,
                     ip, mac, proveedor, costo, moneda,
                     ubicacion, estado, planta, disponible, fecha_de_asignacion,
                     observaciones, fecha_de_salida, destino_planta, asignado_a,
                     personal_it_que_asigna, fecha_de_mantenimiento
              FROM impresoras_nf
              WHERE EXTRACT(YEAR FROM fecha_de_entrada) = @anio
              ORDER BY id DESC", conn);
        cmd.Parameters.AddWithValue("anio", anio);

        var rows = new List<string?[]>();
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
        {
            rows.Add(Enumerable.Range(0, 27)
                .Select(i => r.IsDBNull(i) ? null : r.GetValue(i)?.ToString())
                .ToArray());
        }

        return GenerarExcel(rows);
    }

    // ── Helpers privados ──────────────────────────────────────────────────

    private static (string where, List<(string key, object? val)> parms) ConstruirWhere(ImpresoraNFFiltros f)
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

        Add("id_unico",               f.ID_UNICO);
        Add("oc",                     f.OC);
        Add("folio_inventario",       f.FOLIO_INVENTARIO);
        Add("fecha_de_entrada",       f.FECHA_DE_ENTRADA);
        Add("recibido_por",           f.RECIBIDO_POR);
        Add("marca",                  f.MARCA);
        Add("modelo",                 f.MODELO);
        Add("numero_de_serie",        f.NUMERO_DE_SERIE);
        Add("tipo",                   f.TIPO);
        Add("ip",                     f.IP);
        Add("mac",                    f.MAC);
        Add("proveedor",              f.PROVEEDOR);
        Add("moneda",                 f.MONEDA);
        Add("ubicacion",              f.UBICACION);
        Add("estado",                 f.ESTADO);
        Add("planta",                 f.PLANTA);
        Add("disponible",             f.DISPONIBLE);
        Add("asignado_a",             f.ASIGNADO_A);
        Add("destino_planta",         f.DESTINO_PLANTA);
        Add("personal_it_que_asigna", f.PERSONAL_IT_QUE_ASIGNA);

        var where = conds.Count > 0 ? "WHERE " + string.Join(" AND ", conds) : "";
        return (where, parms);
    }

    private static void AgregarParametros(NpgsqlCommand cmd, ImpresoraNFDto dto)
    {
        cmd.Parameters.AddWithValue("id_unico",               (object?)dto.ID_UNICO               ?? DBNull.Value);
        cmd.Parameters.AddWithValue("oc",                     (object?)dto.OC                     ?? DBNull.Value);
        cmd.Parameters.AddWithValue("folio_inventario",       (object?)dto.FOLIO_INVENTARIO        ?? DBNull.Value);
        cmd.Parameters.AddWithValue("fecha_de_entrada",       (object?)dto.FECHA_DE_ENTRADA        ?? DBNull.Value);
        cmd.Parameters.AddWithValue("recibido_por",           (object?)dto.RECIBIDO_POR            ?? DBNull.Value);
        cmd.Parameters.AddWithValue("marca",                  (object?)dto.MARCA                  ?? DBNull.Value);
        cmd.Parameters.AddWithValue("modelo",                 (object?)dto.MODELO                 ?? DBNull.Value);
        cmd.Parameters.AddWithValue("numero_de_serie",        (object?)dto.NUMERO_DE_SERIE         ?? DBNull.Value);
        cmd.Parameters.AddWithValue("tipo",                   (object?)dto.TIPO                   ?? DBNull.Value);
        cmd.Parameters.AddWithValue("cantidad",               (object?)dto.CANTIDAD               ?? DBNull.Value);
        cmd.Parameters.AddWithValue("ip",                     (object?)dto.IP                     ?? DBNull.Value);
        cmd.Parameters.AddWithValue("mac",                    (object?)dto.MAC                    ?? DBNull.Value);
        cmd.Parameters.AddWithValue("proveedor",              (object?)dto.PROVEEDOR              ?? DBNull.Value);
        cmd.Parameters.AddWithValue("costo",                  (object?)dto.COSTO                  ?? DBNull.Value);
        cmd.Parameters.AddWithValue("moneda",                 (object?)dto.MONEDA                 ?? DBNull.Value);
        cmd.Parameters.AddWithValue("ubicacion",              (object?)dto.UBICACION              ?? DBNull.Value);
        cmd.Parameters.AddWithValue("estado",                 (object?)dto.ESTADO                 ?? DBNull.Value);
        cmd.Parameters.AddWithValue("planta",                 (object?)dto.PLANTA                 ?? DBNull.Value);
        cmd.Parameters.AddWithValue("disponible",             (object?)dto.DISPONIBLE             ?? DBNull.Value);
        cmd.Parameters.AddWithValue("fecha_de_asignacion",    (object?)dto.FECHA_DE_ASIGNACION     ?? DBNull.Value);
        cmd.Parameters.AddWithValue("observaciones",          (object?)dto.OBSERVACIONES          ?? DBNull.Value);
        cmd.Parameters.AddWithValue("fecha_de_salida",        (object?)dto.FECHA_DE_SALIDA         ?? DBNull.Value);
        cmd.Parameters.AddWithValue("destino_planta",         (object?)dto.DESTINO_PLANTA          ?? DBNull.Value);
        cmd.Parameters.AddWithValue("asignado_a",             (object?)dto.ASIGNADO_A             ?? DBNull.Value);
        cmd.Parameters.AddWithValue("personal_it_que_asigna", (object?)dto.PERSONAL_IT_QUE_ASIGNA ?? DBNull.Value);
        cmd.Parameters.AddWithValue("fecha_de_mantenimiento", (object?)dto.FECHA_DE_MANTENIMIENTO  ?? DBNull.Value);
    }

    private async Task<Dictionary<string, object?>?> SnapshotAsync(NpgsqlConnection conn, int id)
    {
        await using var cmd = new NpgsqlCommand(
            @"SELECT id_unico, oc, folio_inventario, fecha_de_entrada, recibido_por,
                     marca, modelo, numero_de_serie, tipo, cantidad,
                     ip, mac, proveedor, costo, moneda,
                     ubicacion, estado, planta, disponible, fecha_de_asignacion,
                     observaciones, fecha_de_salida, destino_planta, asignado_a,
                     personal_it_que_asigna, fecha_de_mantenimiento
              FROM impresoras_nf WHERE id=@id", conn);
        cmd.Parameters.AddWithValue("id", id);

        await using var r = await cmd.ExecuteReaderAsync();
        if (!await r.ReadAsync()) return null;

        var snap = new Dictionary<string, object?>();
        for (int i = 0; i < COLS.Length; i++)
            snap[COLS[i]] = r.IsDBNull(i) ? null : r.GetValue(i)?.ToString();

        return snap;
    }

    private async Task RegistrarHistorialAsync(NpgsqlConnection conn, int impresoraId,
        string usuario, Dictionary<string, object?> anterior, Dictionary<string, object?> nuevo)
    {
        var antesJson   = System.Text.Json.JsonSerializer.Serialize(anterior);
        var despuesJson = System.Text.Json.JsonSerializer.Serialize(nuevo);

        await using var cmd = new NpgsqlCommand(
            """
            INSERT INTO impresoras_nf_historial (impresora_id, usuario, registro_anterior, registro_nuevo)
            VALUES (@iid, @usr, @ant::jsonb, @nvo::jsonb)
            """, conn);

        cmd.Parameters.AddWithValue("iid", impresoraId);
        cmd.Parameters.AddWithValue("usr", usuario);
        cmd.Parameters.AddWithValue("ant", antesJson);
        cmd.Parameters.AddWithValue("nvo", despuesJson);

        await cmd.ExecuteNonQueryAsync();
    }

    private static byte[] GenerarExcel(List<string?[]> rows)
    {
        ExcelPackage.License.SetNonCommercialPersonal("IT Control Center");
        using var pkg = new ExcelPackage();
        var ws = pkg.Workbook.Worksheets.Add("Impresoras NF");

        string[] headers =
        [
            "ID", "ID ÚNICO", "OC", "FOLIO INVENTARIO", "FECHA DE ENTRADA",
            "RECIBIDO POR", "MARCA", "MODELO", "NÚMERO DE SERIE", "TIPO",
            "CANTIDAD", "IP", "MAC", "PROVEEDOR", "COSTO",
            "MONEDA", "UBICACIÓN", "ESTADO", "PLANTA", "DISPONIBLE",
            "FECHA DE ASIGNACIÓN", "OBSERVACIONES", "FECHA DE SALIDA", "DESTINO PLANTA",
            "ASIGNADO A", "PERSONAL IT QUE ASIGNA", "FECHA DE MANTENIMIENTO"
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
