using ChiIT.Data;
using Microsoft.Data.SqlClient;

namespace ChiIT.Services;

// ─────────────────────────────────────────────────────────────────────────────
//  TABLAS PERMITIDAS  (whitelist de seguridad)
// ─────────────────────────────────────────────────────────────────────────────
public static class TablasPermitidas
{
    private static readonly HashSet<string> _tablas = new(StringComparer.OrdinalIgnoreCase)
    {
        // ── Presupuestos ──
        "req_vs_oc",
        "ordenes_de_compra",
        "remisiones",
        "pantallas_nf",
        "refacciones_nf",
        "accesorios_nf",
        "dispositivos_nf",
        "inventarios_nf",
        "perifericos_nf",
        "herramientas_nf",
        "impresoras_nf",
        "consumibles_nf",
        "radios_nf",
        "tintas_toner_ribon_nf",
        "equipo_red_nf",
        "servicios_proveedores",
        "bitacora_firecom",
        "camaras_audio",
        "registro_entradas_temporal",

        // ── PREVENTIVOS ──
        "mantenimientos_preventivos",

        // ── CORRECTIVOS ──
        "mantenimientos_correctivos",

        // ── Otros módulos ──
        "bajas_equipos",
        "control_vales",
        "directorio_proveedores_nf",
        "reportes_impresoras",
        "impresoras_info",
    };

    public static bool EsValida(string tabla) => _tablas.Contains(tabla);
}

// ─────────────────────────────────────────────────────────────────────────────
//  SOFT DELETE SERVICE
// ─────────────────────────────────────────────────────────────────────────────
public class SoftDeleteService
{
    private readonly DbConnectionPool _pool;

    public SoftDeleteService(DbConnectionPool pool) => _pool = pool;

    // ── Inhabilita un registro (activo = 0) ───────────────────────────
    public async Task<(bool ok, string? error)> InhabilitarAsync(string tabla, int id)
    {
        if (!TablasPermitidas.EsValida(tabla))
            return (false, $"Tabla '{tabla}' no permitida.");

        await using var conn = await _pool.OpenAsync();
        await AsegurarColumnaActivoAsync(conn, tabla);

        await using var cmd = new SqlCommand(
            $"UPDATE [{tabla}] SET activo = 0 WHERE id = @id", conn);
        cmd.Parameters.AddWithValue("@id", id);

        var rows = await cmd.ExecuteNonQueryAsync();
        return rows > 0
            ? (true, null)
            : (false, "Registro no encontrado.");
    }

    // ── Restaura un registro (activo = 1) ─────────────────────────────
    public async Task<(bool ok, string? error)> RestaurarAsync(string tabla, int id)
    {
        if (!TablasPermitidas.EsValida(tabla))
            return (false, $"Tabla '{tabla}' no permitida.");

        await using var conn = await _pool.OpenAsync();
        await AsegurarColumnaActivoAsync(conn, tabla);

        await using var cmd = new SqlCommand(
            $"UPDATE [{tabla}] SET activo = 1 WHERE id = @id", conn);
        cmd.Parameters.AddWithValue("@id", id);

        var rows = await cmd.ExecuteNonQueryAsync();
        return rows > 0
            ? (true, null)
            : (false, "Registro no encontrado.");
    }

    // ── Lista los registros INACTIVOS de una tabla ──
    public async Task<List<Dictionary<string, object?>>> ListarInactivosAsync(string tabla)
    {
        if (!TablasPermitidas.EsValida(tabla))
            return [];

        await using var conn = await _pool.OpenAsync();
        await AsegurarColumnaActivoAsync(conn, tabla);

        await using var cmd = new SqlCommand(
            $"SELECT TOP 200 * FROM [{tabla}] WHERE activo = 0 ORDER BY id DESC", conn);

        var lista = new List<Dictionary<string, object?>>();
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
        {
            var row = new Dictionary<string, object?>();
            for (int i = 0; i < r.FieldCount; i++)
                row[r.GetName(i).ToUpper()] = r.IsDBNull(i) ? null : r.GetValue(i)?.ToString();
            lista.Add(row);
        }
        return lista;
    }

    // ── DDL: agrega columna 'activo' si no existe ──
    private static async Task AsegurarColumnaActivoAsync(SqlConnection conn, string tabla)
    {
        // En SQL Server usamos IF NOT EXISTS sobre information_schema
        await using var cmd = new SqlCommand($@"
            IF NOT EXISTS (
                SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
                WHERE TABLE_NAME = '{tabla}' AND COLUMN_NAME = 'activo'
            )
            BEGIN
                ALTER TABLE [{tabla}] ADD activo BIT NOT NULL DEFAULT 1;
                UPDATE [{tabla}] SET activo = 1 WHERE activo IS NULL;
            END", conn);
        await cmd.ExecuteNonQueryAsync();
    }
}