using ChiIT.Data;
using Microsoft.Data.SqlClient;

using OfficeOpenXml;
using OfficeOpenXml.Style;

using System.Drawing;

namespace ChiIT.Services;

// ── DTOs / Modelos ────────────────────────────────────────────────────────────

public class EquipoRedNFDto
{
    public string?  ID_UNICO                     { get; set; }
    public string?  OC                           { get; set; }
    public string?  FOLIO_CORRECTIVO             { get; set; }
    public string?  FECHA_REGISTRO               { get; set; }
    public string?  RECIBIDO_POR                 { get; set; }
    public string?  SUBCATEGORIA                 { get; set; }
    public string?  NO_PARTE                     { get; set; }
    public string?  MARCA                        { get; set; }
    public string?  MODELO                       { get; set; }
    public string?  NUMERO_SERIE                 { get; set; }
    public string?  MAC1                         { get; set; }
    public string?  MAC2                         { get; set; }
    public string?  MAC_ADDRESS                  { get; set; }
    public int?     CANTIDAD                     { get; set; }
    public string?  PROVEEDOR                    { get; set; }
    public string?  COSTO                        { get; set; }
    public string?  MONEDA                       { get; set; }
    public string?  UBICACION                    { get; set; }
    public string?  OBSERVACIONES_COMENTARIOS    { get; set; }
    public string?  DESTINO                      { get; set; }
    public string?  OBSERVACIONES                { get; set; }
    public string?  ACTIVO_DTR3                  { get; set; }
}

public class EquipoRedNFFiltros
{
    public string? ID_UNICO                  { get; set; }
    public string? OC                        { get; set; }
    public string? FOLIO_CORRECTIVO          { get; set; }
    public string? FECHA_REGISTRO            { get; set; }
    public string? RECIBIDO_POR              { get; set; }
    public string? SUBCATEGORIA              { get; set; }
    public string? NO_PARTE                  { get; set; }
    public string? MARCA                     { get; set; }
    public string? MODELO                    { get; set; }
    public string? NUMERO_SERIE              { get; set; }
    public string? MAC1                      { get; set; }
    public string? MAC2                      { get; set; }
    public string? MAC_ADDRESS               { get; set; }
    public string? PROVEEDOR                 { get; set; }
    public string? MONEDA                    { get; set; }
    public string? UBICACION                 { get; set; }
    public string? DESTINO                   { get; set; }
    public string? ACTIVO_DTR3               { get; set; }
}

// ── Servicio ──────────────────────────────────────────────────────────────────

public class EquipoRedNFService
{
    private readonly DbConnectionPool _pool;
    private readonly OrdenesDeCompraService _ordenesService;

    private static readonly string[] COLS =
    [
        "ID_UNICO","OC","FOLIO_CORRECTIVO","FECHA_REGISTRO","RECIBIDO_POR",
        "SUBCATEGORIA","NO_PARTE","MARCA","MODELO","NUMERO_SERIE",
        "MAC1","MAC2","MAC_ADDRESS","CANTIDAD","PROVEEDOR",
        "COSTO","MONEDA","UBICACION","OBSERVACIONES_COMENTARIOS",
        "DESTINO","OBSERVACIONES","ACTIVO_DTR3"
    ];

    public EquipoRedNFService(DbConnectionPool pool, OrdenesDeCompraService ordenesService)
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
            CREATE TABLE IF NOT EXISTS equipo_red_nf (
                id                          SERIAL PRIMARY KEY,
                id_unico                    TEXT,
                oc                          TEXT,
                folio_correctivo            TEXT,
                fecha_registro              DATE,
                recibido_por                TEXT,
                subcategoria                TEXT,
                no_parte                    TEXT,
                marca                       TEXT,
                modelo                      TEXT,
                numero_serie                TEXT,
                mac1                        TEXT,
                mac2                        TEXT,
                mac_address                 TEXT,
                cantidad                    INTEGER,
                proveedor                   TEXT,
                costo                       TEXT,
                moneda                      TEXT,
                ubicacion                   TEXT,
                observaciones_comentarios   TEXT,
                destino                     TEXT,
                observaciones               TEXT,
                activo_dtr3                 TEXT
            );

            CREATE TABLE IF NOT EXISTS equipo_red_nf_historial (
                id                SERIAL PRIMARY KEY,
                equipo_red_id     INTEGER NOT NULL REFERENCES equipo_red_nf(id) ON DELETE CASCADE,
                usuario           TEXT    NOT NULL,
                fecha             TIMESTAMPTZ DEFAULT GETDATE(),
                registro_anterior JSONB,
                registro_nuevo    JSONB
            );
            """, conn);

        await cmd.ExecuteNonQueryAsync();
    }

    // ── Listar (paginado + filtros) ───────────────────────────────────────
    public async Task<object> ListarAsync(int page, int limit, EquipoRedNFFiltros f)
    {
        await using var conn = await _pool.OpenAsync();

        var (where, parms) = ConstruirWhere(f);
        var offset = (page - 1) * limit;

        // Total
        await using var cmdCount = new SqlCommand(
            $"SELECT COUNT(*) FROM equipo_red_nf {where}", conn);
        foreach (var (k, v) in parms) cmdCount.Parameters.AddWithValue(k, v ?? (object)DBNull.Value);
        var total = Convert.ToInt64(await cmdCount.ExecuteScalarAsync());

        // Datos
        await using var cmdData = new SqlCommand(
            $@"SELECT id, id_unico, oc, folio_correctivo, fecha_registro,
                      recibido_por, subcategoria, no_parte, marca, modelo,
                      numero_serie, mac1, mac2, mac_address, cantidad,
                      proveedor, costo, moneda, ubicacion,
                      observaciones_comentarios, destino, observaciones, activo_dtr3
               FROM equipo_red_nf {where}
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
                ID                          = reader.GetInt32(0),
                ID_UNICO                    = Str(reader, 1),
                OC                          = Str(reader, 2),
                FOLIO_CORRECTIVO            = Str(reader, 3),
                FECHA_REGISTRO              = Str(reader, 4),
                RECIBIDO_POR                = Str(reader, 5),
                SUBCATEGORIA                = Str(reader, 6),
                NO_PARTE                    = Str(reader, 7),
                MARCA                       = Str(reader, 8),
                MODELO                      = Str(reader, 9),
                NUMERO_SERIE                = Str(reader, 10),
                MAC1                        = Str(reader, 11),
                MAC2                        = Str(reader, 12),
                MAC_ADDRESS                 = Str(reader, 13),
                CANTIDAD                    = reader.IsDBNull(14) ? (int?)null : reader.GetInt32(14),
                PROVEEDOR                   = Str(reader, 15),
                COSTO                       = Str(reader, 16),
                MONEDA                      = Str(reader, 17),
                UBICACION                   = Str(reader, 18),
                OBSERVACIONES_COMENTARIOS   = Str(reader, 19),
                DESTINO                     = Str(reader, 20),
                OBSERVACIONES               = Str(reader, 21),
                ACTIVO_DTR3                 = Str(reader, 22)
            });
        }

        return new { total, data = lista };
    }

    // ── Crear ─────────────────────────────────────────────────────────────
    public async Task<int> CrearAsync(EquipoRedNFDto dto, string usuario)
    {
        await using var conn = await _pool.OpenAsync();

        await using var cmd = new SqlCommand("""
            INSERT INTO equipo_red_nf
                (id_unico, oc, folio_correctivo, fecha_registro, recibido_por,
                 subcategoria, no_parte, marca, modelo, numero_serie,
                 mac1, mac2, mac_address, cantidad, proveedor,
                 costo, moneda, ubicacion, observaciones_comentarios,
                 destino, observaciones, activo_dtr3)
            VALUES
                (@id_unico, @oc, @folio_correctivo, @fecha_registro, @recibido_por,
                 @subcategoria, @no_parte, @marca, @modelo, @numero_serie,
                 @mac1, @mac2, @mac_address, @cantidad, @proveedor,
                 @costo, @moneda, @ubicacion, @observaciones_comentarios,
                 @destino, @observaciones, @activo_dtr3)
            OUTPUT INSERTED.id
            """, conn);

        AgregarParametros(cmd, dto);
        var id = Convert.ToInt32(await cmd.ExecuteScalarAsync());
        await conn.CloseAsync();
        Console.WriteLine("[EquipoRedNF] RecalcularPorCambioEnHija EQUIPO DE RED " + dto.OC, dto.FOLIO_CORRECTIVO);
        try { _ordenesService.RecalcularPorCambioEnHija("EQUIPO DE RED", dto.OC, dto.FOLIO_CORRECTIVO); }
        catch (Exception ex) { Console.WriteLine("[EquipoRedNF] ERROR RecalcularPorCambioEnHija: " + ex.Message); }
        return id;
    }

    // ── Editar ────────────────────────────────────────────────────────────
    public async Task<bool> EditarAsync(int id, EquipoRedNFDto dto, string usuario)
    {
        await using var conn = await _pool.OpenAsync();

        var anterior = await SnapshotAsync(conn, id);
        if (anterior is null) return false;

        await using var cmd = new SqlCommand("""
            UPDATE equipo_red_nf SET
                id_unico                  = @id_unico,
                oc                        = @oc,
                folio_correctivo          = @folio_correctivo,
                fecha_registro            = @fecha_registro,
                recibido_por              = @recibido_por,
                subcategoria              = @subcategoria,
                no_parte                  = @no_parte,
                marca                     = @marca,
                modelo                    = @modelo,
                numero_serie              = @numero_serie,
                mac1                      = @mac1,
                mac2                      = @mac2,
                mac_address               = @mac_address,
                cantidad                  = @cantidad,
                proveedor                 = @proveedor,
                costo                     = @costo,
                moneda                    = @moneda,
                ubicacion                 = @ubicacion,
                observaciones_comentarios = @observaciones_comentarios,
                destino                   = @destino,
                observaciones             = @observaciones,
                activo_dtr3               = @activo_dtr3
            WHERE id = @id
            """, conn);

        AgregarParametros(cmd, dto);
        cmd.Parameters.AddWithValue("id", id);
        var rows = await cmd.ExecuteNonQueryAsync();
        if (rows == 0) return false;

        var nuevo = await SnapshotAsync(conn, id);
        await RegistrarHistorialAsync(conn, id, usuario, anterior, nuevo!);
        await conn.CloseAsync();
        Console.WriteLine("[EquipoRedNF] RecalcularPorCambioEnHija EQUIPO DE RED " + dto.OC, dto.FOLIO_CORRECTIVO);
        try { _ordenesService.RecalcularPorCambioEnHija("EQUIPO DE RED", dto.OC, dto.FOLIO_CORRECTIVO); }
        catch (Exception ex) { Console.WriteLine("[EquipoRedNF] ERROR RecalcularPorCambioEnHija: " + ex.Message); }
        return true;
    }

    // ── Eliminar ──────────────────────────────────────────────────────────
    public async Task<bool> EliminarAsync(int id)
    {
        await using var conn = await _pool.OpenAsync();

        string? ocVal = null, folioVal = null;
        await using (var qSnap = new SqlCommand(
            "SELECT oc, folio_correctivo FROM equipo_red_nf WHERE id = @id", conn))
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
            "DELETE FROM equipo_red_nf WHERE id = @id", conn);
        cmd.Parameters.AddWithValue("id", id);
        var deleted = await cmd.ExecuteNonQueryAsync() > 0;

        await conn.CloseAsync();

        if (deleted)
            Console.WriteLine("[EquipoRedNF] RecalcularPorCambioEnHija EQUIPO DE RED " + ocVal, folioVal);
            try { _ordenesService.RecalcularPorCambioEnHija("EQUIPO DE RED", ocVal, folioVal); }
            catch (Exception ex) { Console.WriteLine("[EquipoRedNF] ERROR RecalcularPorCambioEnHija: " + ex.Message); }

        return deleted;
    }

    // ── Historial ─────────────────────────────────────────────────────────
    public async Task<List<object>> HistorialAsync(int id)
    {
        await using var conn = await _pool.OpenAsync();
        await using var cmd = new SqlCommand(
            @"SELECT id, usuario, fecha, registro_anterior, registro_nuevo
              FROM equipo_red_nf_historial
              WHERE equipo_red_id = @id
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
    public async Task<byte[]> ExportarAsync(EquipoRedNFFiltros f)
    {
        await using var conn = await _pool.OpenAsync();

        var (where, parms) = ConstruirWhere(f);

        await using var cmd = new SqlCommand(
            $@"SELECT id, id_unico, oc, folio_correctivo, fecha_registro,
                      recibido_por, subcategoria, no_parte, marca, modelo,
                      numero_serie, mac1, mac2, mac_address, cantidad,
                      proveedor, costo, moneda, ubicacion,
                      observaciones_comentarios, destino, observaciones, activo_dtr3
               FROM equipo_red_nf {where} ORDER BY id DESC", conn);

        foreach (var (k, v) in parms) cmd.Parameters.AddWithValue(k, v ?? (object)DBNull.Value);

        var rows = new List<string?[]>();
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
        {
            rows.Add(Enumerable.Range(0, 23)
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
            @"SELECT id, id_unico, oc, folio_correctivo, fecha_registro,
                     recibido_por, subcategoria, no_parte, marca, modelo,
                     numero_serie, mac1, mac2, mac_address, cantidad,
                     proveedor, costo, moneda, ubicacion,
                     observaciones_comentarios, destino, observaciones, activo_dtr3
              FROM equipo_red_nf
              WHERE EXTRACT(YEAR FROM fecha_registro) = @anio
              ORDER BY id DESC", conn);
        cmd.Parameters.AddWithValue("anio", anio);

        var rows = new List<string?[]>();
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
        {
            rows.Add(Enumerable.Range(0, 23)
                .Select(i => r.IsDBNull(i) ? null : r.GetValue(i)?.ToString())
                .ToArray());
        }

        return GenerarExcel(rows);
    }

    // ── Helpers privados ──────────────────────────────────────────────────

    private static (string where, List<(string key, object? val)> parms) ConstruirWhere(EquipoRedNFFiltros f)
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

        Add("id_unico",                 f.ID_UNICO);
        Add("oc",                       f.OC);
        Add("folio_correctivo",         f.FOLIO_CORRECTIVO);
        Add("fecha_registro",           f.FECHA_REGISTRO);
        Add("recibido_por",             f.RECIBIDO_POR);
        Add("subcategoria",             f.SUBCATEGORIA);
        Add("no_parte",                 f.NO_PARTE);
        Add("marca",                    f.MARCA);
        Add("modelo",                   f.MODELO);
        Add("numero_serie",             f.NUMERO_SERIE);
        Add("mac1",                     f.MAC1);
        Add("mac2",                     f.MAC2);
        Add("mac_address",              f.MAC_ADDRESS);
        Add("proveedor",                f.PROVEEDOR);
        Add("moneda",                   f.MONEDA);
        Add("ubicacion",                f.UBICACION);
        Add("destino",                  f.DESTINO);
        Add("activo_dtr3",              f.ACTIVO_DTR3);

        var where = conds.Count > 0 ? "WHERE " + string.Join(" AND ", conds) : "";
        return (where, parms);
    }

    private static void AgregarParametros(SqlCommand cmd, EquipoRedNFDto dto)
    {
        cmd.Parameters.AddWithValue("id_unico",                 (object?)dto.ID_UNICO                   ?? DBNull.Value);
        cmd.Parameters.AddWithValue("oc",                       (object?)dto.OC                         ?? DBNull.Value);
        cmd.Parameters.AddWithValue("folio_correctivo",         (object?)dto.FOLIO_CORRECTIVO           ?? DBNull.Value);
        cmd.Parameters.AddWithValue("fecha_registro",           (object?)dto.FECHA_REGISTRO             ?? DBNull.Value);
        cmd.Parameters.AddWithValue("recibido_por",             (object?)dto.RECIBIDO_POR               ?? DBNull.Value);
        cmd.Parameters.AddWithValue("subcategoria",             (object?)dto.SUBCATEGORIA               ?? DBNull.Value);
        cmd.Parameters.AddWithValue("no_parte",                 (object?)dto.NO_PARTE                   ?? DBNull.Value);
        cmd.Parameters.AddWithValue("marca",                    (object?)dto.MARCA                      ?? DBNull.Value);
        cmd.Parameters.AddWithValue("modelo",                   (object?)dto.MODELO                     ?? DBNull.Value);
        cmd.Parameters.AddWithValue("numero_serie",             (object?)dto.NUMERO_SERIE               ?? DBNull.Value);
        cmd.Parameters.AddWithValue("mac1",                     (object?)dto.MAC1                       ?? DBNull.Value);
        cmd.Parameters.AddWithValue("mac2",                     (object?)dto.MAC2                       ?? DBNull.Value);
        cmd.Parameters.AddWithValue("mac_address",              (object?)dto.MAC_ADDRESS                ?? DBNull.Value);
        cmd.Parameters.AddWithValue("cantidad",                 (object?)dto.CANTIDAD                   ?? DBNull.Value);
        cmd.Parameters.AddWithValue("proveedor",                (object?)dto.PROVEEDOR                  ?? DBNull.Value);
        cmd.Parameters.AddWithValue("costo",                    (object?)dto.COSTO                      ?? DBNull.Value);
        cmd.Parameters.AddWithValue("moneda",                   (object?)dto.MONEDA                     ?? DBNull.Value);
        cmd.Parameters.AddWithValue("ubicacion",                (object?)dto.UBICACION                  ?? DBNull.Value);
        cmd.Parameters.AddWithValue("observaciones_comentarios",(object?)dto.OBSERVACIONES_COMENTARIOS  ?? DBNull.Value);
        cmd.Parameters.AddWithValue("destino",                  (object?)dto.DESTINO                    ?? DBNull.Value);
        cmd.Parameters.AddWithValue("observaciones",            (object?)dto.OBSERVACIONES              ?? DBNull.Value);
        cmd.Parameters.AddWithValue("activo_dtr3",              (object?)dto.ACTIVO_DTR3                ?? DBNull.Value);
    }

    private async Task<Dictionary<string, object?>?> SnapshotAsync(SqlConnection conn, int id)
    {
        await using var cmd = new SqlCommand(
            @"SELECT id_unico, oc, folio_correctivo, fecha_registro, recibido_por,
                     subcategoria, no_parte, marca, modelo, numero_serie,
                     mac1, mac2, mac_address, cantidad, proveedor,
                     costo, moneda, ubicacion, observaciones_comentarios,
                     destino, observaciones, activo_dtr3
              FROM equipo_red_nf WHERE id=@id", conn);
        cmd.Parameters.AddWithValue("id", id);

        await using var r = await cmd.ExecuteReaderAsync();
        if (!await r.ReadAsync()) return null;

        var snap = new Dictionary<string, object?>();
        for (int i = 0; i < COLS.Length; i++)
            snap[COLS[i]] = r.IsDBNull(i) ? null : r.GetValue(i)?.ToString();

        return snap;
    }

    private async Task RegistrarHistorialAsync(SqlConnection conn, int equipoRedId,
        string usuario, Dictionary<string, object?> anterior, Dictionary<string, object?> nuevo)
    {
        var antesJson   = System.Text.Json.JsonSerializer.Serialize(anterior);
        var despuesJson = System.Text.Json.JsonSerializer.Serialize(nuevo);

        await using var cmd = new SqlCommand(
            """
            INSERT INTO equipo_red_nf_historial (equipo_red_id, usuario, registro_anterior, registro_nuevo)
            VALUES (@eid, @usr, @ant, @nvo)
            """, conn);

        cmd.Parameters.AddWithValue("eid", equipoRedId);
        cmd.Parameters.AddWithValue("usr", usuario);
        cmd.Parameters.AddWithValue("ant", antesJson);
        cmd.Parameters.AddWithValue("nvo", despuesJson);

        await cmd.ExecuteNonQueryAsync();
    }

    private static byte[] GenerarExcel(List<string?[]> rows)
    {
        ExcelPackage.License.SetNonCommercialPersonal("IT Control Center");
        using var pkg = new ExcelPackage();
        var ws = pkg.Workbook.Worksheets.Add("Equipo Red NF");

        string[] headers =
        [
            "ID","ID ÚNICO","OC","FOLIO CORRECTIVO","FECHA REGISTRO",
            "RECIBIDO POR","SUBCATEGORÍA","NO PARTE","MARCA","MODELO",
            "NÚMERO SERIE","MAC1","MAC2","MAC ADDRESS","CANTIDAD",
            "PROVEEDOR","COSTO","MONEDA","UBICACIÓN",
            "OBSERVACIONES COMENTARIOS","DESTINO","OBSERVACIONES","ACTIVO DTR3"
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
