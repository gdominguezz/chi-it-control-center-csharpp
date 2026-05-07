using ChiIT.Data;
using Npgsql;
using OfficeOpenXml;
using OfficeOpenXml.Style;
using System.Drawing;
using System.Text.Json;

namespace ChiIT.Services;

// ── DTO ───────────────────────────────────────────────────────────────────────

public class PantallaNfDto
{
    public string?  id_unico                { get; set; }
    public string?  oc                      { get; set; }
    public string?  folio                   { get; set; }
    public string?  fecha_registro          { get; set; }
    public string?  recibido_por            { get; set; }
    public string?  subcategoria            { get; set; }
    public string?  marca                   { get; set; }
    public string?  modelo                  { get; set; }
    public string?  no_serie                { get; set; }
    public int?     cantidad                { get; set; }
    public decimal? tamano_pulgadas         { get; set; }
    public string?  accesorios              { get; set; }
    public string?  mac_wifi                { get; set; }
    public string?  mac_ethernet            { get; set; }
    public string?  proveedor               { get; set; }
    public decimal? costo_usd               { get; set; }
    public int?     vida_util_meses         { get; set; }
    public string?  estado                  { get; set; }
    public bool?    disponible              { get; set; }
    public string?  fecha_salida            { get; set; }
    public string?  destino_planta          { get; set; }
    public string?  asignado_a              { get; set; }
    public string?  personal_it_que_asigna  { get; set; }
}

// ── Servicio ──────────────────────────────────────────────────────────────────

public class PantallasNfService
{
    private readonly DbConnectionPool _pool;

    private static readonly string[] COLS =
    [
        "id_unico","oc","folio","fecha_registro","recibido_por","subcategoria",
        "marca","modelo","no_serie","cantidad","tamano_pulgadas","accesorios",
        "mac_wifi","mac_ethernet","proveedor","costo_usd","vida_util_meses",
        "estado","disponible","fecha_salida","destino_planta","asignado_a",
        "personal_it_que_asigna"
    ];

    public PantallasNfService(DbConnectionPool pool)
    {
        _pool = pool;
        _ = InicializarTablaAsync();
    }

    // ── DDL ───────────────────────────────────────────────────────────────
    private async Task InicializarTablaAsync()
    {
        await using var conn = await _pool.OpenAsync();
        await using var cmd = new NpgsqlCommand("""
            CREATE TABLE IF NOT EXISTS pantallas_nf (
                id                      SERIAL PRIMARY KEY,
                id_unico                TEXT,
                oc                      TEXT,
                folio                   TEXT,
                fecha_registro          DATE,
                recibido_por            TEXT,
                subcategoria            TEXT,
                marca                   TEXT,
                modelo                  TEXT,
                no_serie                TEXT,
                cantidad                INTEGER,
                tamano_pulgadas         NUMERIC(5,2),
                accesorios              TEXT,
                mac_wifi                TEXT,
                mac_ethernet            TEXT,
                proveedor               TEXT,
                costo_usd               NUMERIC(12,2),
                vida_util_meses         INTEGER,
                estado                  TEXT,
                disponible              BOOLEAN DEFAULT TRUE,
                fecha_salida            DATE,
                destino_planta          TEXT,
                asignado_a              TEXT,
                personal_it_que_asigna  TEXT,
                fecha_creacion          TIMESTAMPTZ DEFAULT NOW()
            );

            ALTER TABLE pantallas_nf ADD COLUMN IF NOT EXISTS fecha_creacion TIMESTAMPTZ DEFAULT NOW();

            CREATE TABLE IF NOT EXISTS pantallas_nf_historial (
                id                  SERIAL PRIMARY KEY,
                pantalla_id         INTEGER NOT NULL REFERENCES pantallas_nf(id) ON DELETE CASCADE,
                usuario             TEXT NOT NULL,
                fecha               TIMESTAMPTZ DEFAULT NOW(),
                registro_anterior   JSONB,
                registro_nuevo      JSONB
            );
            """, conn);

        await cmd.ExecuteNonQueryAsync();
    }

    // ── Listar todos ──────────────────────────────────────────────────────
    public async Task<List<object>> ListarAsync()
    {
        await using var conn = await _pool.OpenAsync();
        await using var cmd = new NpgsqlCommand("""
        SELECT id, id_unico, oc, folio, fecha_registro, recibido_por, subcategoria,
               marca, modelo, no_serie, cantidad, tamano_pulgadas, accesorios,
               mac_wifi, mac_ethernet, proveedor, costo_usd, vida_util_meses,
               estado, disponible, fecha_salida, destino_planta, asignado_a,
               personal_it_que_asigna
        FROM pantallas_nf
        WHERE (activo IS NULL OR activo = true)
        ORDER BY id DESC
        """, conn);

        var lista = new List<object>();
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
            lista.Add(MapRow(r));

        return lista;
    }

    // ── Obtener por ID ────────────────────────────────────────────────────
    public async Task<object?> ObtenerAsync(int id)
    {
        await using var conn = await _pool.OpenAsync();
        await using var cmd = new NpgsqlCommand("""
            SELECT id, id_unico, oc, folio, fecha_registro, recibido_por, subcategoria,
                   marca, modelo, no_serie, cantidad, tamano_pulgadas, accesorios,
                   mac_wifi, mac_ethernet, proveedor, costo_usd, vida_util_meses,
                   estado, disponible, fecha_salida, destino_planta, asignado_a,
                   personal_it_que_asigna
            FROM pantallas_nf WHERE id = @id
            """, conn);
        cmd.Parameters.AddWithValue("id", id);

        await using var r = await cmd.ExecuteReaderAsync();
        if (!await r.ReadAsync()) return null;
        return MapRow(r);
    }

    // ── Crear ─────────────────────────────────────────────────────────────
    public async Task<int> CrearAsync(PantallaNfDto dto, string? usuario)
    {
        await using var conn = await _pool.OpenAsync();

        // Generar ID_UNICO = OC + FOLIO
        var idUnico = (!string.IsNullOrWhiteSpace(dto.oc) && !string.IsNullOrWhiteSpace(dto.folio))
            ? $"{dto.oc}{dto.folio}"
            : dto.id_unico;

        await using var cmd = new NpgsqlCommand("""
            INSERT INTO pantallas_nf
                (id_unico, oc, folio, fecha_registro, recibido_por, subcategoria,
                 marca, modelo, no_serie, cantidad, tamano_pulgadas, accesorios,
                 mac_wifi, mac_ethernet, proveedor, costo_usd, vida_util_meses,
                 estado, disponible, fecha_salida, destino_planta, asignado_a,
                 personal_it_que_asigna)
            VALUES
                (@id_unico, @oc, @folio, @fecha_registro, @recibido_por, @subcategoria,
                 @marca, @modelo, @no_serie, @cantidad, @tamano_pulgadas, @accesorios,
                 @mac_wifi, @mac_ethernet, @proveedor, @costo_usd, @vida_util_meses,
                 @estado, @disponible, @fecha_salida, @destino_planta, @asignado_a,
                 @personal_it_que_asigna)
            RETURNING id
            """, conn);

        AgregarParametros(cmd, dto, idUnico);
        var newId = Convert.ToInt32(await cmd.ExecuteScalarAsync());

        // Historial: registrar creación
        var snap = await SnapshotAsync(conn, newId);
        if (snap != null)
            await RegistrarHistorialAsync(conn, newId, usuario ?? "sistema",
                new Dictionary<string, object?>(), snap);

        return newId;
    }

    // ── Editar ────────────────────────────────────────────────────────────
    public async Task<bool> EditarAsync(int id, PantallaNfDto dto, string? usuario)
    {
        await using var conn = await _pool.OpenAsync();

        // Obtener registro anterior para historial
        var anterior = await SnapshotAsync(conn, id);
        if (anterior == null) return false;

        // Recalcular ID_UNICO = OC + FOLIO
        var idUnico = (!string.IsNullOrWhiteSpace(dto.oc) && !string.IsNullOrWhiteSpace(dto.folio))
            ? $"{dto.oc}{dto.folio}"
            : dto.id_unico;

        await using var cmd = new NpgsqlCommand("""
        UPDATE pantallas_nf SET
            id_unico                = @id_unico,
            oc                      = @oc,
            folio                   = @folio,
            fecha_registro          = @fecha_registro,
            recibido_por            = @recibido_por,
            subcategoria            = @subcategoria,
            marca                   = @marca,
            modelo                  = @modelo,
            no_serie                = @no_serie,
            cantidad                = @cantidad,
            tamano_pulgadas         = @tamano_pulgadas,
            accesorios              = @accesorios,
            mac_wifi                = @mac_wifi,
            mac_ethernet            = @mac_ethernet,
            proveedor               = @proveedor,
            costo_usd               = @costo_usd,
            vida_util_meses         = @vida_util_meses,
            estado                  = @estado,
            disponible              = @disponible,
            fecha_salida            = @fecha_salida,
            destino_planta          = @destino_planta,
            asignado_a              = @asignado_a,
            personal_it_que_asigna  = @personal_it_que_asigna
        WHERE id = @id
        """, conn);

        // Agregar parámetros
        AgregarParametros(cmd, dto, idUnico);
        cmd.Parameters.AddWithValue("id", id);

        await cmd.ExecuteNonQueryAsync();

        // Obtener registro nuevo para historial
        var nuevo = await SnapshotAsync(conn, id);
        if (nuevo != null)
            await RegistrarHistorialAsync(conn, id, usuario ?? "sistema", anterior, nuevo);

        return true;
    }

    // ── Eliminar ──────────────────────────────────────────────────────────
    public async Task<bool> EliminarAsync(int id)
    {
        await using var conn = await _pool.OpenAsync();
        await using var cmd = new NpgsqlCommand(
            "DELETE FROM pantallas_nf WHERE id = @id", conn);
        cmd.Parameters.AddWithValue("id", id);
        return await cmd.ExecuteNonQueryAsync() > 0;
    }

    // ── Historial ─────────────────────────────────────────────────────────
    public async Task<List<object>> ObtenerHistorialAsync(int id)
    {
        await using var conn = await _pool.OpenAsync();
        await using var cmd = new NpgsqlCommand("""
            SELECT id, pantalla_id, usuario, fecha, registro_anterior, registro_nuevo
            FROM pantallas_nf_historial
            WHERE pantalla_id = @id
            ORDER BY fecha DESC
            """, conn);
        cmd.Parameters.AddWithValue("id", id);

        var lista = new List<object>();
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
        {
            lista.Add(new
            {
                id               = r.GetInt32(0),
                pantalla_id      = r.GetInt32(1),
                usuario          = Str(r, 2),
                fecha            = r.IsDBNull(3) ? null : r.GetDateTime(3).ToString("o"),
                registro_anterior = r.IsDBNull(4) ? null : r.GetString(4),
                registro_nuevo    = r.IsDBNull(5) ? null : r.GetString(5),
            });
        }
        return lista;
    }

    // ── Exportar Excel ────────────────────────────────────────────────────
    public async Task<byte[]> ExportarExcelAsync()
    {
        await using var conn = await _pool.OpenAsync();
        await using var cmd = new NpgsqlCommand("""
            SELECT id, id_unico, oc, folio, fecha_registro, recibido_por, subcategoria,
                   marca, modelo, no_serie, cantidad, tamano_pulgadas, accesorios,
                   mac_wifi, mac_ethernet, proveedor, costo_usd, vida_util_meses,
                   estado, disponible, fecha_salida, destino_planta, asignado_a,
                   personal_it_que_asigna
            FROM pantallas_nf ORDER BY id DESC
            """, conn);

        var rows = new List<string?[]>();
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
            rows.Add(Enumerable.Range(0, 24)
                .Select(i => r.IsDBNull(i) ? null : r.GetValue(i)?.ToString())
                .ToArray());

        return GenerarExcel(rows);
    }

    // ── Helpers privados ──────────────────────────────────────────────────

    private static object MapRow(NpgsqlDataReader r) => new
    {
        id                      = r.GetInt32(0),
        id_unico                = Str(r, 1),
        oc                      = Str(r, 2),
        folio                   = Str(r, 3),
        fecha_registro          = r.IsDBNull(4)  ? null : ((DateTime)r.GetValue(4)).ToString("yyyy-MM-dd"),
        recibido_por            = Str(r, 5),
        subcategoria            = Str(r, 6),
        marca                   = Str(r, 7),
        modelo                  = Str(r, 8),
        no_serie                = Str(r, 9),
        cantidad                = r.IsDBNull(10) ? (int?)null    : r.GetInt32(10),
        tamano_pulgadas         = r.IsDBNull(11) ? (decimal?)null: r.GetDecimal(11),
        accesorios              = Str(r, 12),
        mac_wifi                = Str(r, 13),
        mac_ethernet            = Str(r, 14),
        proveedor               = Str(r, 15),
        costo_usd               = r.IsDBNull(16) ? (decimal?)null: r.GetDecimal(16),
        vida_util_meses         = r.IsDBNull(17) ? (int?)null    : r.GetInt32(17),
        estado                  = Str(r, 18),
        disponible              = r.IsDBNull(19) ? (bool?)null   : r.GetBoolean(19),
        fecha_salida            = r.IsDBNull(20) ? null : ((DateTime)r.GetValue(20)).ToString("yyyy-MM-dd"),
        destino_planta          = Str(r, 21),
        asignado_a              = Str(r, 22),
        personal_it_que_asigna  = Str(r, 23),
    };

    private static void AgregarParametros(NpgsqlCommand cmd, PantallaNfDto dto, string? idUnico)
    {
        DateTime? fr = DateTime.TryParse(dto.fecha_registro, out var frTmp) ? frTmp : null;
        DateTime? fs = DateTime.TryParse(dto.fecha_salida, out var fsTmp) ? fsTmp : null;

        cmd.Parameters.AddWithValue("id_unico", (object?)idUnico ?? DBNull.Value);
        cmd.Parameters.AddWithValue("oc", (object?)dto.oc ?? DBNull.Value);
        cmd.Parameters.AddWithValue("folio", (object?)dto.folio ?? DBNull.Value);
        cmd.Parameters.AddWithValue("fecha_registro", (object?)fr ?? DBNull.Value);
        cmd.Parameters.AddWithValue("recibido_por", (object?)dto.recibido_por ?? DBNull.Value);
        cmd.Parameters.AddWithValue("subcategoria", (object?)dto.subcategoria ?? DBNull.Value);
        cmd.Parameters.AddWithValue("marca", (object?)dto.marca ?? DBNull.Value);
        cmd.Parameters.AddWithValue("modelo", (object?)dto.modelo ?? DBNull.Value);
        cmd.Parameters.AddWithValue("no_serie", (object?)dto.no_serie ?? DBNull.Value);
        cmd.Parameters.AddWithValue("cantidad", (object?)dto.cantidad ?? DBNull.Value);
        cmd.Parameters.AddWithValue("tamano_pulgadas", (object?)dto.tamano_pulgadas ?? DBNull.Value);
        cmd.Parameters.AddWithValue("accesorios", (object?)dto.accesorios ?? DBNull.Value);
        cmd.Parameters.AddWithValue("mac_wifi", (object?)dto.mac_wifi ?? DBNull.Value);
        cmd.Parameters.AddWithValue("mac_ethernet", (object?)dto.mac_ethernet ?? DBNull.Value);
        cmd.Parameters.AddWithValue("proveedor", (object?)dto.proveedor ?? DBNull.Value);
        cmd.Parameters.AddWithValue("costo_usd", (object?)dto.costo_usd ?? DBNull.Value);
        cmd.Parameters.AddWithValue("vida_util_meses", (object?)dto.vida_util_meses ?? DBNull.Value);
        cmd.Parameters.AddWithValue("estado", (object?)dto.estado ?? DBNull.Value);
        cmd.Parameters.AddWithValue("disponible", (object?)dto.disponible ?? DBNull.Value);
        cmd.Parameters.AddWithValue("fecha_salida", (object?)fs ?? DBNull.Value);
        cmd.Parameters.AddWithValue("destino_planta", (object?)dto.destino_planta ?? DBNull.Value);
        cmd.Parameters.AddWithValue("asignado_a", (object?)dto.asignado_a ?? DBNull.Value);
        cmd.Parameters.AddWithValue("personal_it_que_asigna", (object?)dto.personal_it_que_asigna ?? DBNull.Value);
    }

    private async Task<Dictionary<string, object?>?> SnapshotAsync(NpgsqlConnection conn, int id)
    {
        await using var cmd = new NpgsqlCommand("""
            SELECT id_unico, oc, folio, fecha_registro, recibido_por, subcategoria,
                   marca, modelo, no_serie, cantidad, tamano_pulgadas, accesorios,
                   mac_wifi, mac_ethernet, proveedor, costo_usd, vida_util_meses,
                   estado, disponible, fecha_salida, destino_planta, asignado_a,
                   personal_it_que_asigna
            FROM pantallas_nf WHERE id = @id
            """, conn);
        cmd.Parameters.AddWithValue("id", id);

        await using var r = await cmd.ExecuteReaderAsync();
        if (!await r.ReadAsync()) return null;

        var snap = new Dictionary<string, object?>();
        for (int i = 0; i < COLS.Length; i++)
            snap[COLS[i]] = r.IsDBNull(i) ? null : r.GetValue(i)?.ToString();

        return snap;
    }

    private async Task RegistrarHistorialAsync(NpgsqlConnection conn, int pantallaId,
        string usuario, Dictionary<string, object?> anterior, Dictionary<string, object?> nuevo)
    {
        await using var cmd = new NpgsqlCommand("""
            INSERT INTO pantallas_nf_historial
                (pantalla_id, usuario, registro_anterior, registro_nuevo)
            VALUES (@pid, @usr, @ant::jsonb, @nvo::jsonb)
            """, conn);

        cmd.Parameters.AddWithValue("pid", pantallaId);
        cmd.Parameters.AddWithValue("usr", usuario);
        cmd.Parameters.AddWithValue("ant", JsonSerializer.Serialize(anterior));
        cmd.Parameters.AddWithValue("nvo", JsonSerializer.Serialize(nuevo));
        await cmd.ExecuteNonQueryAsync();
    }

    private static byte[] GenerarExcel(List<string?[]> rows)
    {
        ExcelPackage.License.SetNonCommercialPersonal("IT Control Center");
        using var pkg = new ExcelPackage();
        var ws = pkg.Workbook.Worksheets.Add("Pantallas NF");

        string[] headers =
        [
            "ID","ID ÚNICO","OC","FOLIO","FECHA REGISTRO","RECIBIDO POR","SUBCATEGORÍA",
            "MARCA","MODELO","NO. SERIE","CANTIDAD","TAMAÑO (\")",
            "ACCESORIOS","MAC WIFI","MAC ETHERNET","PROVEEDOR","COSTO USD",
            "VIDA ÚTIL (m)","ESTADO","DISPONIBLE","FECHA SALIDA",
            "DESTINO / PLANTA","ASIGNADO A","PERSONAL IT"
        ];

        for (int c = 0; c < headers.Length; c++)
        {
            var cell = ws.Cells[1, c + 1];
            cell.Value = headers[c];
            cell.Style.Font.Bold = true;
            cell.Style.Fill.PatternType = ExcelFillStyle.Solid;
            cell.Style.Fill.BackgroundColor.SetColor(Color.FromArgb(30, 41, 59));
            cell.Style.Font.Color.SetColor(Color.White);
            cell.Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
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
