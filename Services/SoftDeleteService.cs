using ChiIT.Data;
using Npgsql;

namespace ChiIT.Services;

// ─────────────────────────────────────────────────────────────────────────────
//  TABLAS PERMITIDAS  (whitelist de seguridad — nunca aceptar tabla libre del cliente)
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

    // ── Inhabilita un registro (ACTIVO = false) ───────────────────────────
    public async Task<(bool ok, string? error)> InhabilitarAsync(string tabla, int id)
    {
        if (!TablasPermitidas.EsValida(tabla))
            return (false, $"Tabla '{tabla}' no permitida.");

        await using var conn = await _pool.OpenAsync();

        // Asegura que la columna exista (idempotente)
        await AsegurarColumnaActivoAsync(conn, tabla);

        await using var cmd = new NpgsqlCommand(
            $"UPDATE {tabla} SET activo = false WHERE id = @id", conn);
        cmd.Parameters.AddWithValue("id", id);

        var rows = await cmd.ExecuteNonQueryAsync();
        return rows > 0
            ? (true, null)
            : (false, "Registro no encontrado.");
    }

    // ── Restaura un registro (ACTIVO = true) ─────────────────────────────
    public async Task<(bool ok, string? error)> RestaurarAsync(string tabla, int id)
    {
        if (!TablasPermitidas.EsValida(tabla))
            return (false, $"Tabla '{tabla}' no permitida.");

        await using var conn = await _pool.OpenAsync();
        await AsegurarColumnaActivoAsync(conn, tabla);

        await using var cmd = new NpgsqlCommand(
            $"UPDATE {tabla} SET activo = true WHERE id = @id", conn);
        cmd.Parameters.AddWithValue("id", id);

        var rows = await cmd.ExecuteNonQueryAsync();
        return rows > 0
            ? (true, null)
            : (false, "Registro no encontrado.");
    }

    // ── Lista los registros INACTIVOS de una tabla (para el panel de restauración) ──
    public async Task<List<Dictionary<string, object?>>> ListarInactivosAsync(string tabla)
    {
        if (!TablasPermitidas.EsValida(tabla))
            return [];

        await using var conn = await _pool.OpenAsync();
        await AsegurarColumnaActivoAsync(conn, tabla);

        await using var cmd = new NpgsqlCommand(
            $"SELECT * FROM {tabla} WHERE activo = false ORDER BY id DESC LIMIT 200", conn);

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

    // ── DDL: agrega columna 'activo' si no existe y activa todos los actuales ──
    private static async Task AsegurarColumnaActivoAsync(NpgsqlConnection conn, string tabla)
    {
        await using var cmd = new NpgsqlCommand($"""
            DO $$
            BEGIN
                IF NOT EXISTS (
                    SELECT 1 FROM information_schema.columns
                    WHERE table_name = '{tabla}' AND column_name = 'activo'
                ) THEN
                    ALTER TABLE {tabla} ADD COLUMN activo BOOLEAN NOT NULL DEFAULT true;
                    UPDATE {tabla} SET activo = true WHERE activo IS NULL;
                END IF;
            END
            $$;
            """, conn);
        await cmd.ExecuteNonQueryAsync();
    }
}