using ChiIT.Data;
using Npgsql;

using OfficeOpenXml;
using OfficeOpenXml.Style;

using System.Drawing;

namespace ChiIT.Services;

// ── DTOs / Modelos ────────────────────────────────────────────────────────────

public class PerifericoNFDto
{
    public string? ID_UNICO { get; set; }
    public string? OC { get; set; }
    public string? FOLIO_INVENTARIO { get; set; }
    public string? FECHA_ENTRADA { get; set; }
    public string? RECIBIDO_POR { get; set; }
    public string? SUBCATEGORIA { get; set; }
    public string? TIPO { get; set; }
    public string? MARCA { get; set; }
    public string? MODELO { get; set; }
    public int? CANTIDAD { get; set; }
    public string? NUMERO_SERIE { get; set; }
    public string? PROVEEDOR { get; set; }
    public decimal? COSTO_PESOS { get; set; }
    public string? ESTADO { get; set; }
    public string? DESTINO { get; set; }
    public bool? DISPONIBLE { get; set; }
    public string? FECHA_SALIDA { get; set; }
    public string? DESTINO_PLANTA { get; set; }
    public string? ASIGNADO_A { get; set; }
    public string? PERSONAL_IT_QUE_ASIGNA { get; set; }
}

public class PerifericoNFFiltros
{
    public string? ID_UNICO { get; set; }
    public string? OC { get; set; }
    public string? FOLIO_INVENTARIO { get; set; }
    public string? FECHA_ENTRADA { get; set; }
    public string? RECIBIDO_POR { get; set; }
    public string? SUBCATEGORIA { get; set; }
    public string? TIPO { get; set; }
    public string? MARCA { get; set; }
    public string? MODELO { get; set; }
    public string? NUMERO_SERIE { get; set; }
    public string? PROVEEDOR { get; set; }
    public string? ESTADO { get; set; }
    public string? DESTINO { get; set; }
    public string? DISPONIBLE { get; set; }
    public string? DESTINO_PLANTA { get; set; }
    public string? ASIGNADO_A { get; set; }
    public string? PERSONAL_IT_QUE_ASIGNA { get; set; }
}

// ── Servicio ──────────────────────────────────────────────────────────────────

public class PerifeicosNFService
{
    private readonly DbConnectionPool _pool;
    private readonly OrdenesDeCompraService _ordenesService;

    private static readonly string[] COLS =
    [
        "ID_UNICO","OC","FOLIO_INVENTARIO","FECHA_ENTRADA","RECIBIDO_POR",
        "SUBCATEGORIA","TIPO","MARCA","MODELO","CANTIDAD",
        "NUMERO_SERIE","PROVEEDOR","COSTO_PESOS","ESTADO","DESTINO",
        "DISPONIBLE","FECHA_SALIDA","DESTINO_PLANTA","ASIGNADO_A","PERSONAL_IT_QUE_ASIGNA"
    ];

    public PerifeicosNFService(DbConnectionPool pool, OrdenesDeCompraService ordenesService)
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
            CREATE TABLE IF NOT EXISTS perifericos_nf (
                id                      SERIAL PRIMARY KEY,
                id_unico                TEXT,
                oc                      TEXT,
                folio_inventario        TEXT,
                fecha_entrada           DATE,
                recibido_por            TEXT,
                subcategoria            TEXT,
                tipo                    TEXT,
                marca                   TEXT,
                modelo                  TEXT,
                cantidad                INTEGER,
                numero_serie            TEXT,
                proveedor               TEXT,
                costo_pesos             NUMERIC(12,2),
                estado                  TEXT,
                destino                 TEXT,
                disponible              BOOLEAN,
                fecha_salida            DATE,
                destino_planta          TEXT,
                asignado_a              TEXT,
                personal_it_que_asigna  TEXT,
                created_at              TIMESTAMP DEFAULT NOW()
            );

            CREATE TABLE IF NOT EXISTS perifericos_nf_historial (
                id                SERIAL PRIMARY KEY,
                periferico_id     INTEGER NOT NULL REFERENCES perifericos_nf(id) ON DELETE CASCADE,
                usuario           TEXT    NOT NULL,
                fecha             TIMESTAMPTZ DEFAULT NOW(),
                registro_anterior JSONB,
                registro_nuevo    JSONB
            );
            """, conn);

        await cmd.ExecuteNonQueryAsync();
    }

    // ── Listar (paginado + filtros) ───────────────────────────────────────
    public async Task<object> ListarAsync(int page, int limit, PerifericoNFFiltros f)
    {
        await using var conn = await _pool.OpenAsync();

        var (where, parms) = ConstruirWhere(f);
        var offset = (page - 1) * limit;

        // Total
        await using var cmdCount = new NpgsqlCommand(
            $"SELECT COUNT(*) FROM perifericos_nf {where}", conn);
        foreach (var (k, v) in parms) cmdCount.Parameters.AddWithValue(k, v ?? (object)DBNull.Value);
        var total = Convert.ToInt64(await cmdCount.ExecuteScalarAsync());

        // Datos
        await using var cmdData = new NpgsqlCommand(
            $@"SELECT id, id_unico, oc, folio_inventario, fecha_entrada,
                      recibido_por, subcategoria, tipo, marca, modelo,
                      cantidad, numero_serie, proveedor, costo_pesos,
                      estado, destino, disponible, fecha_salida,
                      destino_planta, asignado_a, personal_it_que_asigna, created_at
               FROM perifericos_nf {where}
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
                ID = reader.GetInt32(0),
                ID_UNICO = Str(reader, 1),
                OC = Str(reader, 2),
                FOLIO_INVENTARIO = Str(reader, 3),
                FECHA_ENTRADA = Str(reader, 4),
                RECIBIDO_POR = Str(reader, 5),
                SUBCATEGORIA = Str(reader, 6),
                TIPO = Str(reader, 7),
                MARCA = Str(reader, 8),
                MODELO = Str(reader, 9),
                CANTIDAD = reader.IsDBNull(10) ? (int?)null : reader.GetInt32(10),
                NUMERO_SERIE = Str(reader, 11),
                PROVEEDOR = Str(reader, 12),
                COSTO_PESOS = reader.IsDBNull(13) ? (decimal?)null : reader.GetDecimal(13),
                ESTADO = Str(reader, 14),
                DESTINO = Str(reader, 15),
                DISPONIBLE = reader.IsDBNull(16) ? (bool?)null : reader.GetBoolean(16),
                FECHA_SALIDA = Str(reader, 17),
                DESTINO_PLANTA = Str(reader, 18),
                ASIGNADO_A = Str(reader, 19),
                PERSONAL_IT_QUE_ASIGNA = Str(reader, 20),
                CREATED_AT = Str(reader, 21),
            });
        }

        return new { total, page, limit, data = lista };
    }

    // ── Crear ─────────────────────────────────────────────────────────────
    public async Task<int> CrearAsync(PerifericoNFDto dto, string usuario)
    {
        await using var conn = await _pool.OpenAsync();

        await using var cmd = new NpgsqlCommand(
            @"INSERT INTO perifericos_nf
                (id_unico, oc, folio_inventario, fecha_entrada, recibido_por,
                 subcategoria, tipo, marca, modelo, cantidad,
                 numero_serie, proveedor, costo_pesos, estado, destino,
                 disponible, fecha_salida, destino_planta, asignado_a, personal_it_que_asigna)
              VALUES
                (@id_unico, @oc, @folio_inventario, @fecha_entrada, @recibido_por,
                 @subcategoria, @tipo, @marca, @modelo, @cantidad,
                 @numero_serie, @proveedor, @costo_pesos, @estado, @destino,
                 @disponible, @fecha_salida, @destino_planta, @asignado_a, @personal_it_que_asigna)
              RETURNING id", conn);

        AgregarParametros(cmd, dto);
        var id = Convert.ToInt32(await cmd.ExecuteScalarAsync());

        
        await conn.CloseAsync();
        Console.WriteLine("[PeriericosNF] RecalcularPorCambioEnHija PERIFERICOS NF " + dto.OC, dto.FOLIO_INVENTARIO);
        try { _ordenesService.RecalcularPorCambioEnHija("PERIFERICOS NF", dto.OC, dto.FOLIO_INVENTARIO); }
        catch (Exception ex) { Console.WriteLine("[PeriericosNF] ERROR RecalcularPorCambioEnHija: " + ex.Message); }

        var snap = await SnapshotAsync(conn, id);
        if (snap is not null)
        {
            var vacio = COLS.ToDictionary(c => c, _ => (object?)null);
            await RegistrarHistorialAsync(conn, id, usuario, vacio, snap);
        }

        return id;
    }

    // ── Editar ────────────────────────────────────────────────────────────
    public async Task<bool> EditarAsync(int id, PerifericoNFDto dto, string usuario)
    {
        await using var conn = await _pool.OpenAsync();

        var anterior = await SnapshotAsync(conn, id);
        if (anterior is null) return false;

        await using var cmd = new NpgsqlCommand(
            @"UPDATE perifericos_nf SET
                id_unico               = @id_unico,
                oc                     = @oc,
                folio_inventario       = @folio_inventario,
                fecha_entrada          = @fecha_entrada,
                recibido_por           = @recibido_por,
                subcategoria           = @subcategoria,
                tipo                   = @tipo,
                marca                  = @marca,
                modelo                 = @modelo,
                cantidad               = @cantidad,
                numero_serie           = @numero_serie,
                proveedor              = @proveedor,
                costo_pesos            = @costo_pesos,
                estado                 = @estado,
                destino                = @destino,
                disponible             = @disponible,
                fecha_salida           = @fecha_salida,
                destino_planta         = @destino_planta,
                asignado_a             = @asignado_a,
                personal_it_que_asigna = @personal_it_que_asigna
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

        await conn.CloseAsync();
        Console.WriteLine("[PeriericosNF] RecalcularPorCambioEnHija PERIFERICOS NF " + dto.OC, dto.FOLIO_INVENTARIO);
        try { _ordenesService.RecalcularPorCambioEnHija("PERIFERICOS NF", dto.OC, dto.FOLIO_INVENTARIO); }
        catch (Exception ex) { Console.WriteLine("[PeriericosNF] ERROR RecalcularPorCambioEnHija: " + ex.Message); }

        return rows > 0;
    }

    // ── Eliminar ──────────────────────────────────────────────────────────
    public async Task<bool> EliminarAsync(int id)
    {
        await using var conn = await _pool.OpenAsync();

        string? ocVal = null, folioVal = null;
        await using (var qSnap = new NpgsqlCommand(
            "SELECT oc, folio_inventario FROM perifericos_nf WHERE id = @id", conn))
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
            "DELETE FROM perifericos_nf WHERE id = @id", conn);
        cmd.Parameters.AddWithValue("id", id);
        var deleted = await cmd.ExecuteNonQueryAsync() > 0;

        await conn.CloseAsync();

        if (deleted)
            Console.WriteLine("[PeriericosNF] RecalcularPorCambioEnHija PERIFERICOS NF " + ocVal, folioVal);
            try { _ordenesService.RecalcularPorCambioEnHija("PERIFERICOS NF", ocVal, folioVal); }
            catch (Exception ex) { Console.WriteLine("[PeriericosNF] ERROR RecalcularPorCambioEnHija: " + ex.Message); }

        return deleted;
    }

    // ── Historial ─────────────────────────────────────────────────────────
    public async Task<List<object>> HistorialAsync(int id)
    {
        await using var conn = await _pool.OpenAsync();
        await using var cmd = new NpgsqlCommand(
            @"SELECT id, usuario, fecha, registro_anterior, registro_nuevo
              FROM perifericos_nf_historial
              WHERE periferico_id = @id
              ORDER BY fecha DESC", conn);
        cmd.Parameters.AddWithValue("id", id);

        var lista = new List<object>();
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
        {
            lista.Add(new
            {
                ID = r.GetInt32(0),
                USUARIO = r.GetString(1),
                FECHA = r.GetValue(2)?.ToString(),
                REGISTRO_ANTERIOR = r.IsDBNull(3) ? null : r.GetString(3),
                REGISTRO_NUEVO = r.IsDBNull(4) ? null : r.GetString(4),
            });
        }
        return lista;
    }

    // ── Exportar (filtrado) ───────────────────────────────────────────────
    public async Task<byte[]> ExportarAsync(PerifericoNFFiltros f)
    {
        await using var conn = await _pool.OpenAsync();
        var (where, parms) = ConstruirWhere(f);

        await using var cmd = new NpgsqlCommand(
            $@"SELECT id, id_unico, oc, folio_inventario, fecha_entrada,
                      recibido_por, subcategoria, tipo, marca, modelo,
                      cantidad, numero_serie, proveedor, costo_pesos,
                      estado, destino, disponible, fecha_salida,
                      destino_planta, asignado_a, personal_it_que_asigna, created_at
               FROM perifericos_nf {where}
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
        await using var cmd = new NpgsqlCommand(
            @"SELECT id, id_unico, oc, folio_inventario, fecha_entrada,
                     recibido_por, subcategoria, tipo, marca, modelo,
                     cantidad, numero_serie, proveedor, costo_pesos,
                     estado, destino, disponible, fecha_salida,
                     destino_planta, asignado_a, personal_it_que_asigna, created_at
              FROM perifericos_nf
              WHERE EXTRACT(YEAR FROM fecha_entrada) = @anio
              AND (activo IS NULL OR activo = true)
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

    private static (string where, List<(string key, object? val)> parms) ConstruirWhere(PerifericoNFFiltros f)
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

        Add("id_unico", f.ID_UNICO);
        Add("oc", f.OC);
        Add("folio_inventario", f.FOLIO_INVENTARIO);
        Add("fecha_entrada", f.FECHA_ENTRADA);
        Add("recibido_por", f.RECIBIDO_POR);
        Add("subcategoria", f.SUBCATEGORIA);
        Add("tipo", f.TIPO);
        Add("marca", f.MARCA);
        Add("modelo", f.MODELO);
        Add("numero_serie", f.NUMERO_SERIE);
        Add("proveedor", f.PROVEEDOR);
        Add("estado", f.ESTADO);
        Add("destino", f.DESTINO);
        Add("disponible::TEXT", f.DISPONIBLE);
        Add("destino_planta", f.DESTINO_PLANTA);
        Add("asignado_a", f.ASIGNADO_A);
        Add("personal_it_que_asigna", f.PERSONAL_IT_QUE_ASIGNA);

        var where = conds.Count > 0 ? "WHERE " + string.Join(" AND ", conds) : "";
        return (where, parms);
    }

    private static void AgregarParametros(NpgsqlCommand cmd, PerifericoNFDto dto)
    {
        cmd.Parameters.AddWithValue("id_unico", (object?)dto.ID_UNICO ?? DBNull.Value);
        cmd.Parameters.AddWithValue("oc", (object?)dto.OC ?? DBNull.Value);
        cmd.Parameters.AddWithValue("folio_inventario", (object?)dto.FOLIO_INVENTARIO ?? DBNull.Value);
        cmd.Parameters.AddWithValue("fecha_entrada", ParseDate(dto.FECHA_ENTRADA));
        cmd.Parameters.AddWithValue("recibido_por", (object?)dto.RECIBIDO_POR ?? DBNull.Value);
        cmd.Parameters.AddWithValue("subcategoria", (object?)dto.SUBCATEGORIA ?? DBNull.Value);
        cmd.Parameters.AddWithValue("tipo", (object?)dto.TIPO ?? DBNull.Value);
        cmd.Parameters.AddWithValue("marca", (object?)dto.MARCA ?? DBNull.Value);
        cmd.Parameters.AddWithValue("modelo", (object?)dto.MODELO ?? DBNull.Value);
        cmd.Parameters.AddWithValue("cantidad", (object?)dto.CANTIDAD ?? DBNull.Value);
        cmd.Parameters.AddWithValue("numero_serie", (object?)dto.NUMERO_SERIE ?? DBNull.Value);
        cmd.Parameters.AddWithValue("proveedor", (object?)dto.PROVEEDOR ?? DBNull.Value);
        cmd.Parameters.AddWithValue("costo_pesos", (object?)dto.COSTO_PESOS ?? DBNull.Value);
        cmd.Parameters.AddWithValue("estado", (object?)dto.ESTADO ?? DBNull.Value);
        cmd.Parameters.AddWithValue("destino", (object?)dto.DESTINO ?? DBNull.Value);
        cmd.Parameters.AddWithValue("disponible", (object?)dto.DISPONIBLE ?? DBNull.Value);
        cmd.Parameters.AddWithValue("fecha_salida", ParseDate(dto.FECHA_SALIDA));
        cmd.Parameters.AddWithValue("destino_planta", (object?)dto.DESTINO_PLANTA ?? DBNull.Value);
        cmd.Parameters.AddWithValue("asignado_a", (object?)dto.ASIGNADO_A ?? DBNull.Value);
        cmd.Parameters.AddWithValue("personal_it_que_asigna", (object?)dto.PERSONAL_IT_QUE_ASIGNA ?? DBNull.Value);
    }

    // Convierte string "yyyy-MM-dd" (o null/vacío) a DateOnly para Npgsql.
    // Acepta también formatos con hora ("yyyy-MM-ddTHH:mm:ss") por si el frontend manda datetime.
    private static object ParseDate(string? val)
    {
        if (string.IsNullOrWhiteSpace(val)) return DBNull.Value;
        // Intentar parsear solo la parte de fecha si viene con hora
        var part = val.Length > 10 ? val[..10] : val;
        return DateOnly.TryParse(part, out var d) ? (object)d : DBNull.Value;
    }

    private async Task<Dictionary<string, object?>?> SnapshotAsync(NpgsqlConnection conn, int id)
    {
        await using var cmd = new NpgsqlCommand(
            @"SELECT id_unico, oc, folio_inventario, fecha_entrada, recibido_por,
                     subcategoria, tipo, marca, modelo, cantidad,
                     numero_serie, proveedor, costo_pesos, estado, destino,
                     disponible, fecha_salida, destino_planta, asignado_a, personal_it_que_asigna
              FROM perifericos_nf WHERE id=@id", conn);
        cmd.Parameters.AddWithValue("id", id);

        await using var r = await cmd.ExecuteReaderAsync();
        if (!await r.ReadAsync()) return null;

        var snap = new Dictionary<string, object?>();
        for (int i = 0; i < COLS.Length; i++)
            snap[COLS[i]] = r.IsDBNull(i) ? null : r.GetValue(i)?.ToString();

        return snap;
    }

    private async Task RegistrarHistorialAsync(NpgsqlConnection conn, int perifericoId,
        string usuario, Dictionary<string, object?> anterior, Dictionary<string, object?> nuevo)
    {
        var antesJson = System.Text.Json.JsonSerializer.Serialize(anterior);
        var despuesJson = System.Text.Json.JsonSerializer.Serialize(nuevo);

        await using var cmd = new NpgsqlCommand(
            """
            INSERT INTO perifericos_nf_historial (periferico_id, usuario, registro_anterior, registro_nuevo)
            VALUES (@pid, @usr, @ant::jsonb, @nvo::jsonb)
            """, conn);

        cmd.Parameters.AddWithValue("pid", perifericoId);
        cmd.Parameters.AddWithValue("usr", usuario);
        cmd.Parameters.AddWithValue("ant", antesJson);
        cmd.Parameters.AddWithValue("nvo", despuesJson);

        await cmd.ExecuteNonQueryAsync();
    }

    private static byte[] GenerarExcel(List<string?[]> rows)
    {
        ExcelPackage.License.SetNonCommercialPersonal("IT Control Center");
        using var pkg = new ExcelPackage();
        var ws = pkg.Workbook.Worksheets.Add("Periféricos NF");

        string[] headers =
        [
            "ID","ID ÚNICO","OC","FOLIO INVENTARIO","FECHA ENTRADA",
            "RECIBIDO POR","SUBCATEGORÍA","TIPO","MARCA","MODELO",
            "CANTIDAD","NÚMERO SERIE","PROVEEDOR","COSTO PESOS",
            "ESTADO","DESTINO","DISPONIBLE","FECHA SALIDA",
            "DESTINO PLANTA","ASIGNADO A","PERSONAL IT QUE ASIGNA","FECHA REGISTRO"
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