using ChiIT.Data;
using ChiIT.Models;
using Npgsql;

using OfficeOpenXml;
using OfficeOpenXml.Style;

using System.ComponentModel;
using System.Drawing;

namespace ChiIT.Services;

// ── DTOs / Modelos ────────────────────────────────────────────────────────────

public class BajaDto
{
    public string? FOLIO { get; set; }
    public string? ESTADO { get; set; }
    public string? PLANTA { get; set; }
    public string? FECHA { get; set; }
    public string? EQUIPO { get; set; }
    public string? MARCA { get; set; }
    public string? MODELO { get; set; }
    public string? NO_SERIE { get; set; }
    public string? ACTIVO_FIJO { get; set; }
    public string? UBICACION_PERSONA { get; set; }
    public string? MOTIVO_DE_BAJA { get; set; }
    public string? DIAGNOSTICO { get; set; }
    public string? COMENTARIOS { get; set; }
    public string? MOTIVO_DE_CANCELACION { get; set; }
}

public class BajaFiltros
{
    public string? FOLIO { get; set; }
    public string? ESTADO { get; set; }
    public string? PLANTA { get; set; }
    public string? FECHA { get; set; }
    public string? EQUIPO { get; set; }
    public string? MARCA { get; set; }
    public string? MODELO { get; set; }
    public string? NO_SERIE { get; set; }
    public string? ACTIVO_FIJO { get; set; }
    public string? UBICACION_PERSONA { get; set; }
    public string? MOTIVO_DE_BAJA { get; set; }
    public string? DIAGNOSTICO { get; set; }
    public string? COMENTARIOS { get; set; }
    public string? MOTIVO_DE_CANCELACION { get; set; }
}

// ── Servicio ──────────────────────────────────────────────────────────────────

public class BajasService
{
    private readonly DbConnectionPool _pool;
    private const string PDF_DIR = "PDF_DATABASE/BAJAS";

    // Columnas de datos (sin ID ni campos de sistema)
    private static readonly string[] COLS =
    [
        "FOLIO","ESTADO","PLANTA","FECHA","EQUIPO","MARCA","MODELO",
        "NO_SERIE","ACTIVO_FIJO","UBICACION_PERSONA",
        "MOTIVO_DE_BAJA","DIAGNOSTICO","COMENTARIOS","MOTIVO_DE_CANCELACION"
    ];

    public BajasService(DbConnectionPool pool)
    {
        _pool = pool;
        Directory.CreateDirectory(PDF_DIR);
        _ = InicializarTablaAsync();
    }

    // ── DDL: crear tablas si no existen ──────────────────────────────────
    private async Task InicializarTablaAsync()
    {
        await using var conn = await _pool.OpenAsync();
        await using var cmd = new NpgsqlCommand("""
            CREATE TABLE IF NOT EXISTS bajas_equipos (
                id                  SERIAL PRIMARY KEY,
                folio               TEXT,
                estado              TEXT,
                planta              TEXT,
                fecha               TEXT,
                equipo              TEXT,
                marca               TEXT,
                modelo              TEXT,
                no_serie            TEXT,
                activo_fijo         TEXT,
                ubicacion_persona   TEXT,
                motivo_de_baja      TEXT,
                diagnostico         TEXT,
                comentarios         TEXT,
                motivo_de_cancelacion  TEXT,
                tiene_pdf           BOOLEAN DEFAULT FALSE,
                fecha_creacion      TIMESTAMPTZ DEFAULT NOW()
            );

            -- Migracion: agrega columnas si la tabla ya existia sin ellas
            ALTER TABLE bajas_equipos ADD COLUMN IF NOT EXISTS tiene_pdf      BOOLEAN     DEFAULT FALSE;
            ALTER TABLE bajas_equipos ADD COLUMN IF NOT EXISTS fecha_creacion TIMESTAMPTZ DEFAULT NOW();

            CREATE TABLE IF NOT EXISTS bajas_historial (
                id                  SERIAL PRIMARY KEY,
                baja_id             INTEGER NOT NULL REFERENCES bajas_equipos(id) ON DELETE CASCADE,
                usuario             TEXT    NOT NULL,
                fecha               TIMESTAMPTZ DEFAULT NOW(),
                registro_anterior   JSONB,
                registro_nuevo      JSONB
            );
            """, conn);

        await cmd.ExecuteNonQueryAsync();
    }

    // ── Listar (paginado + filtros) ───────────────────────────────────────
    public async Task<object> ListarAsync(int page, int limit, BajaFiltros f)
    {
        await using var conn = await _pool.OpenAsync();

        var (where, parms) = ConstruirWhere(f);
        var offset = (page - 1) * limit;

        // Total
        await using var cmdCount = new NpgsqlCommand(
            $"SELECT COUNT(*) FROM bajas_equipos {where}", conn);
        foreach (var (k, v) in parms) cmdCount.Parameters.AddWithValue(k, v ?? (object)DBNull.Value);
        var total = Convert.ToInt64(await cmdCount.ExecuteScalarAsync());

        // Datos
        await using var cmdData = new NpgsqlCommand(
            $@"SELECT id, folio, estado, planta, fecha, equipo, marca, modelo,
                      no_serie, activo_fijo, ubicacion_persona,
                      MOTIVO_DE_BAJA, diagnostico, comentarios, motivo_de_cancelacion, tiene_pdf
               FROM bajas_equipos {where}
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
                FOLIO = Str(reader, 1),
                ESTADO = Str(reader, 2),
                PLANTA = Str(reader, 3),
                FECHA = Str(reader, 4),
                EQUIPO = Str(reader, 5),
                MARCA = Str(reader, 6),
                MODELO = Str(reader, 7),
                NO_SERIE = Str(reader, 8),
                ACTIVO_FIJO = Str(reader, 9),
                UBICACION_PERSONA = Str(reader, 10),
                MOTIVO_DE_BAJA = Str(reader, 11),
                DIAGNOSTICO = Str(reader, 12),
                COMENTARIOS = Str(reader, 13),
                MOTIVO_DE_CANCELACION = Str(reader, 14),
                TIENE_PDF = !reader.IsDBNull(15) && reader.GetBoolean(15)
            });
        }

        return new { total, data = lista };
    }

    // ── Crear ─────────────────────────────────────────────────────────────
    public async Task<int> CrearAsync(BajaDto dto, string usuario)
    {
        await using var conn = await _pool.OpenAsync();

        await using var cmd = new NpgsqlCommand("""
            INSERT INTO bajas_equipos
                (folio,estado,planta,fecha,equipo,marca,modelo,
                 no_serie,activo_fijo,ubicacion_persona,
                 MOTIVO_DE_BAJA,diagnostico,comentarios,motivo_de_cancelacion)
            VALUES
                (@folio,@estado,@planta,@fecha,@equipo,@marca,@modelo,
                 @no_serie,@activo_fijo,@ubicacion_persona,
                 @motivo_de_baja,@diagnostico,@comentarios,@motivo_de_cancelacion)
            RETURNING id
            """, conn);

        AgregarParametros(cmd, dto);
        return Convert.ToInt32(await cmd.ExecuteScalarAsync());
    }

    // ── Editar ────────────────────────────────────────────────────────────
    public async Task<bool> EditarAsync(int id, BajaDto dto, string usuario)
    {
        await using var conn = await _pool.OpenAsync();

        // Snapshot anterior
        var anterior = await SnapshotAsync(conn, id);
        if (anterior == null) return false;

        await using var cmd = new NpgsqlCommand("""
            UPDATE bajas_equipos SET
                folio=@folio, estado=@estado, planta=@planta, fecha=@fecha,
                equipo=@equipo, marca=@marca, modelo=@modelo, no_serie=@no_serie,
                activo_fijo=@activo_fijo, ubicacion_persona=@ubicacion_persona,
                motivo_de_baja=@motivo_de_baja, diagnostico=@diagnostico,
                comentarios=@comentarios, motivo_de_cancelacion=@motivo_de_cancelacion
            WHERE id=@id
            """, conn);

        AgregarParametros(cmd, dto);
        cmd.Parameters.AddWithValue("id", id);
        var rows = await cmd.ExecuteNonQueryAsync();
        if (rows == 0) return false;

        // Snapshot nuevo
        var nuevo = await SnapshotAsync(conn, id);

        // Historial
        await RegistrarHistorialAsync(conn, id, usuario, anterior, nuevo!);
        return true;
    }

    // ── Eliminar ──────────────────────────────────────────────────────────
    public async Task<bool> EliminarAsync(int id)
    {
        await using var conn = await _pool.OpenAsync();
        await using var cmd = new NpgsqlCommand(
            "DELETE FROM bajas_equipos WHERE id=@id", conn);
        cmd.Parameters.AddWithValue("id", id);
        return await cmd.ExecuteNonQueryAsync() > 0;
    }

    // ── Historial ─────────────────────────────────────────────────────────
    public async Task<List<object>> HistorialAsync(int bajaId)
    {
        await using var conn = await _pool.OpenAsync();
        await using var cmd = new NpgsqlCommand(
            """
                SELECT id, usuario, fecha, registro_anterior, registro_nuevo
                FROM bajas_historial
                WHERE baja_id = @id
                ORDER BY fecha DESC
                """,
            conn
        );

        cmd.Parameters.AddWithValue("id", bajaId);

        var lista = new List<object>();
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
        {
            lista.Add(new
            {
                id = r.GetInt32(0),
                usuario = Str(r, 1),
                fecha = r.IsDBNull(2) ? null : r.GetDateTime(2).ToString("o"),
                registro_anterior = Str(r, 3),
                registro_nuevo = Str(r, 4)
            });
        }
        return lista;
    }

    // ── PDF ───────────────────────────────────────────────────────────────
    public async Task GuardarPdfAsync(int id, IFormFile file)
    {
        var path = PdfPath(id);
        await using var fs = System.IO.File.Create(path);
        await file.CopyToAsync(fs);

        await using var conn = await _pool.OpenAsync();
        await using var cmd = new NpgsqlCommand(
            "UPDATE bajas_equipos SET tiene_pdf=TRUE WHERE id=@id", conn);
        cmd.Parameters.AddWithValue("id", id);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task<Stream?> ObtenerPdfAsync(int id)
    {
        var path = PdfPath(id);
        if (!System.IO.File.Exists(path)) return null;
        return await Task.FromResult(System.IO.File.OpenRead(path));
    }

    public async Task<bool> EliminarPdfAsync(int id)
    {
        var path = PdfPath(id);
        if (!System.IO.File.Exists(path)) return false;
        System.IO.File.Delete(path);

        await using var conn = await _pool.OpenAsync();
        await using var cmd = new NpgsqlCommand(
            "UPDATE bajas_equipos SET tiene_pdf=FALSE WHERE id=@id", conn);
        cmd.Parameters.AddWithValue("id", id);
        await cmd.ExecuteNonQueryAsync();
        return true;
    }

    // ── Exportar Excel ────────────────────────────────────────────────────
    public async Task<byte[]> ExportarAsync(BajaFiltros f)
    {
        await using var conn = await _pool.OpenAsync();
        var (where, parms) = ConstruirWhere(f);

        await using var cmd = new NpgsqlCommand(
            $@"SELECT id, folio, estado, planta, fecha, equipo, marca, modelo,
                      no_serie, activo_fijo, ubicacion_persona,
                      MOTIVO_DE_BAJA, diagnostico, comentarios, motivo_de_cancelacion
               FROM bajas_equipos {where} ORDER BY id DESC", conn);

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

    public async Task<byte[]> ExportarPorAnioAsync(int anio)
    {
        await using var conn = await _pool.OpenAsync();
        await using var cmd = new NpgsqlCommand(
            @"SELECT id, folio, estado, planta, fecha, equipo, marca, modelo,
                     no_serie, activo_fijo, ubicacion_persona,
                     MOTIVO_DE_BAJA, diagnostico, comentarios, motivo_de_cancelacion
              FROM bajas_equipos
              WHERE EXTRACT(YEAR FROM fecha_creacion) = @anio
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

    private static (string where, List<(string key, object? val)> parms) ConstruirWhere(BajaFiltros f)
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

        Add("folio", f.FOLIO);
        Add("estado", f.ESTADO);
        Add("planta", f.PLANTA);
        Add("fecha", f.FECHA);
        Add("equipo", f.EQUIPO);
        Add("marca", f.MARCA);
        Add("modelo", f.MODELO);
        Add("no_serie", f.NO_SERIE);
        Add("activo_fijo", f.ACTIVO_FIJO);
        Add("ubicacion_persona", f.UBICACION_PERSONA);
        Add("motivo_de_baja", f.MOTIVO_DE_BAJA);
        Add("diagnostico", f.DIAGNOSTICO);
        Add("comentarios", f.COMENTARIOS);
        Add("motivo_de_cancelacion", f.MOTIVO_DE_CANCELACION);

        var where = conds.Count > 0 ? "WHERE " + string.Join(" AND ", conds) : "";
        return (where, parms);
    }

    private static void AgregarParametros(NpgsqlCommand cmd, BajaDto dto)
    {
        cmd.Parameters.AddWithValue("folio", (object?)dto.FOLIO ?? DBNull.Value);
        cmd.Parameters.AddWithValue("estado", (object?)dto.ESTADO ?? DBNull.Value);
        cmd.Parameters.AddWithValue("planta", (object?)dto.PLANTA ?? DBNull.Value);
        cmd.Parameters.AddWithValue("fecha", NpgsqlTypes.NpgsqlDbType.Text,
            (object?)dto.FECHA ?? DBNull.Value);
        cmd.Parameters.AddWithValue("equipo", (object?)dto.EQUIPO ?? DBNull.Value);
        cmd.Parameters.AddWithValue("marca", (object?)dto.MARCA ?? DBNull.Value);
        cmd.Parameters.AddWithValue("modelo", (object?)dto.MODELO ?? DBNull.Value);
        cmd.Parameters.AddWithValue("no_serie", (object?)dto.NO_SERIE ?? DBNull.Value);
        cmd.Parameters.AddWithValue("activo_fijo", (object?)dto.ACTIVO_FIJO ?? DBNull.Value);
        cmd.Parameters.AddWithValue("ubicacion_persona", (object?)dto.UBICACION_PERSONA ?? DBNull.Value);
        cmd.Parameters.AddWithValue("motivo_de_baja", (object?)dto.MOTIVO_DE_BAJA ?? DBNull.Value);
        cmd.Parameters.AddWithValue("diagnostico", (object?)dto.DIAGNOSTICO ?? DBNull.Value);
        cmd.Parameters.AddWithValue("comentarios", (object?)dto.COMENTARIOS ?? DBNull.Value);
        cmd.Parameters.AddWithValue("motivo_de_cancelacion", (object?)dto.MOTIVO_DE_CANCELACION ?? DBNull.Value);
    }

    private async Task<Dictionary<string, object?>?> SnapshotAsync(NpgsqlConnection conn, int id)
    {
        await using var cmd = new NpgsqlCommand(
            @"SELECT folio,estado,planta,fecha,equipo,marca,modelo,
                     no_serie,activo_fijo,ubicacion_persona,
                     MOTIVO_DE_BAJA,diagnostico,comentarios,motivo_de_cancelacion
              FROM bajas_equipos WHERE id=@id", conn);
        cmd.Parameters.AddWithValue("id", id);

        await using var r = await cmd.ExecuteReaderAsync();
        if (!await r.ReadAsync()) return null;

        var snap = new Dictionary<string, object?>();
        for (int i = 0; i < COLS.Length; i++)
            snap[COLS[i]] = r.IsDBNull(i) ? null : r.GetValue(i)?.ToString();

        return snap;
    }

    private async Task RegistrarHistorialAsync(NpgsqlConnection conn, int bajaId,
        string usuario, Dictionary<string, object?> anterior, Dictionary<string, object?> nuevo)
    {
        var antesJson = System.Text.Json.JsonSerializer.Serialize(anterior);
        var despuesJson = System.Text.Json.JsonSerializer.Serialize(nuevo);

        await using var cmd = new NpgsqlCommand(
            """
                INSERT INTO bajas_historial (baja_id, usuario, registro_anterior, registro_nuevo)
                VALUES (@bid, @usr, @ant::jsonb, @nvo::jsonb)
                """,
            conn
        );

        cmd.Parameters.AddWithValue("bid", bajaId);
        cmd.Parameters.AddWithValue("usr", usuario);
        cmd.Parameters.AddWithValue("ant", antesJson);
        cmd.Parameters.AddWithValue("nvo", despuesJson);

        await cmd.ExecuteNonQueryAsync();
    }

    private static byte[] GenerarExcel(List<string?[]> rows)
    {
        ExcelPackage.License.SetNonCommercialPersonal("IT Control Center");
        using var pkg = new ExcelPackage();
        var ws = pkg.Workbook.Worksheets.Add("Bajas de Equipos");

        // Encabezados
        string[] headers = ["ID","FOLIO","ESTADO","PLANTA","FECHA","EQUIPO","MARCA","MODELO",
                             "NO SERIE","ACTIVO FIJO","UBICACION / PERSONA",
                             "MOTIVO DE BAJA","DIAGNOSTICO","COMENTARIOS","MOTIVO DE CANCELACION"];

        for (int c = 0; c < headers.Length; c++)
        {
            ws.Cells[1, c + 1].Value = headers[c];
            ws.Cells[1, c + 1].Style.Font.Bold = true;
            ws.Cells[1, c + 1].Style.Fill.PatternType = ExcelFillStyle.Solid;
            ws.Cells[1, c + 1].Style.Fill.BackgroundColor.SetColor(Color.FromArgb(30, 41, 59));
            ws.Cells[1, c + 1].Style.Font.Color.SetColor(Color.White);
            ws.Cells[1, c + 1].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
        }

        // Datos
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

    private static string PdfPath(int id) => Path.Combine(PDF_DIR, $"baja_{id}.pdf");
}