using ChiIT.Data;
using Microsoft.AspNetCore.Mvc;
using Npgsql;

namespace ChiIT.Controllers;

[ApiController]
public class DashboardController : ControllerBase
{
    private readonly DbConnectionPool _db;
    public DashboardController(DbConnectionPool db) => _db = db;

    /// <summary>
    /// GET /DASHBOARD — Devuelve todos los datos para el dashboard
    /// </summary>
    [HttpGet("DASHBOARD")]
    public IActionResult ObtenerDashboard()
    {
        using var conn = _db.Open();

        // ── 1. KPIs generales ──────────────────────────────
        using var kpiCmd = conn.CreateCommand();
        kpiCmd.CommandText = """
            SELECT
              COUNT(*)                                                          AS total,
              COUNT(*) FILTER (WHERE preventivo_digital IS NOT NULL)            AS con_pm,
              COUNT(*) FILTER (WHERE preventivo_digital IS NULL)                AS sin_pm,
              COUNT(*) FILTER (WHERE fecha_realizacion IS NOT NULL
                               AND plazo IS NOT NULL
                               AND plazo::date < CURRENT_DATE)                  AS vencidos,
              COUNT(*) FILTER (WHERE fecha_realizacion >= date_trunc('week', CURRENT_DATE)
                               AND fecha_realizacion <  date_trunc('week', CURRENT_DATE) + interval '7 days') AS semana
            FROM public.mantenimientos_preventivos
            """;
        using var kpiR = kpiCmd.ExecuteReader();
        kpiR.Read();
        var kpis = new
        {
            total = kpiR.GetInt64(0),
            con_pm = kpiR.GetInt64(1),
            sin_pm = kpiR.GetInt64(2),
            vencidos = kpiR.GetInt64(3),
            semana = kpiR.GetInt64(4),
        };
        kpiR.Close();

        // ── 2. Por categoría de color ──────────────────────
        using var colorCmd = conn.CreateCommand();
        colorCmd.CommandText = """
            SELECT COALESCE(LOWER(categoria_color), 'sin categoría') AS cat, COUNT(*) AS total
            FROM public.mantenimientos_preventivos
            GROUP BY cat ORDER BY total DESC
            """;
        var porColor = new List<object>();
        using var colorR = colorCmd.ExecuteReader();
        while (colorR.Read())
            porColor.Add(new { categoria = colorR.GetString(0), total = colorR.GetInt64(1) });
        colorR.Close();

        // ── 3. Por planta ──────────────────────────────────
        using var plantaCmd = conn.CreateCommand();
        plantaCmd.CommandText = """
            SELECT COALESCE(planta, 'Sin planta') AS planta, COUNT(*) AS total
            FROM public.mantenimientos_preventivos
            GROUP BY planta ORDER BY total DESC LIMIT 10
            """;
        var porPlanta = new List<object>();
        using var plantaR = plantaCmd.ExecuteReader();
        while (plantaR.Read())
            porPlanta.Add(new { planta = plantaR.GetString(0), total = plantaR.GetInt64(1) });
        plantaR.Close();

        // ── 4. Últimos 10 mantenimientos realizados ────────
        using var ultimosCmd = conn.CreateCommand();
        ultimosCmd.CommandText = """
            SELECT id, id_equipo, nombre_dispositivo, ubicacion, planta,
                   fecha_realizacion, realizado_por
            FROM public.mantenimientos_preventivos
            WHERE fecha_realizacion IS NOT NULL
            ORDER BY fecha_realizacion DESC
            LIMIT 10
            """;
        var ultimos = new List<object>();
        using var ultimosR = ultimosCmd.ExecuteReader();
        while (ultimosR.Read())
            ultimos.Add(new
            {
                id = ultimosR.GetInt64(0),
                id_equipo = ultimosR.IsDBNull(1) ? "" : ultimosR.GetString(1),
                dispositivo = ultimosR.IsDBNull(2) ? "" : ultimosR.GetString(2),
                ubicacion = ultimosR.IsDBNull(3) ? "" : ultimosR.GetString(3),
                planta = ultimosR.IsDBNull(4) ? "" : ultimosR.GetString(4),
                fecha = ultimosR.IsDBNull(5) ? "" : ultimosR.GetDateTime(5).ToString("yyyy-MM-dd"),
                realizado_por = ultimosR.IsDBNull(6) ? "" : ultimosR.GetString(6),
            });
        ultimosR.Close();

        // ── 5. Mantenimientos de esta semana ───────────────
        using var semanaCmd = conn.CreateCommand();
        semanaCmd.CommandText = """
            SELECT id, id_equipo, nombre_dispositivo, ubicacion, planta,
                   fecha_realizacion, realizado_por
            FROM public.mantenimientos_preventivos
            WHERE fecha_realizacion >= date_trunc('week', CURRENT_DATE)
              AND fecha_realizacion <  date_trunc('week', CURRENT_DATE) + interval '7 days'
            ORDER BY fecha_realizacion DESC
            """;
        var estaSemana = new List<object>();
        using var semanaR = semanaCmd.ExecuteReader();
        while (semanaR.Read())
            estaSemana.Add(new
            {
                id = semanaR.GetInt64(0),
                id_equipo = semanaR.IsDBNull(1) ? "" : semanaR.GetString(1),
                dispositivo = semanaR.IsDBNull(2) ? "" : semanaR.GetString(2),
                ubicacion = semanaR.IsDBNull(3) ? "" : semanaR.GetString(3),
                planta = semanaR.IsDBNull(4) ? "" : semanaR.GetString(4),
                fecha = semanaR.IsDBNull(5) ? "" : semanaR.GetDateTime(5).ToString("yyyy-MM-dd"),
                realizado_por = semanaR.IsDBNull(6) ? "" : semanaR.GetString(6),
            });
        semanaR.Close();

        // ── 6. Próximos PM del mes actual (por plazo) ─────
        using var mesCmd = conn.CreateCommand();
        mesCmd.CommandText = """
            SELECT id, id_equipo, nombre_dispositivo, ubicacion, planta,
                   plazo, categoria_color
            FROM public.mantenimientos_preventivos
            WHERE plazo IS NOT NULL
              AND plazo::date >= date_trunc('month', CURRENT_DATE)
              AND plazo::date <  date_trunc('month', CURRENT_DATE) + interval '1 month'
            ORDER BY plazo::date ASC
            """;
        var proximos = new List<object>();
        using var mesR = mesCmd.ExecuteReader();
        while (mesR.Read())
            proximos.Add(new
            {
                id = mesR.GetInt64(0),
                id_equipo = mesR.IsDBNull(1) ? "" : mesR.GetString(1),
                dispositivo = mesR.IsDBNull(2) ? "" : mesR.GetString(2),
                ubicacion = mesR.IsDBNull(3) ? "" : mesR.GetString(3),
                planta = mesR.IsDBNull(4) ? "" : mesR.GetString(4),
                plazo = mesR.IsDBNull(5) ? "" : mesR.GetString(5),
                color = mesR.IsDBNull(6) ? "" : mesR.GetString(6),
            });
        mesR.Close();

        // ── 7. Vencidos (plazo pasado sin PM reciente) ────
        using var vencidosCmd = conn.CreateCommand();
        vencidosCmd.CommandText = """
            SELECT id, id_equipo, nombre_dispositivo, ubicacion, planta,
                   plazo, fecha_realizacion, categoria_color
            FROM public.mantenimientos_preventivos
            WHERE plazo IS NOT NULL
              AND plazo::date < CURRENT_DATE
            ORDER BY plazo::date ASC
            LIMIT 50
            """;
        var vencidosList = new List<object>();
        using var vencidosR = vencidosCmd.ExecuteReader();
        while (vencidosR.Read())
            vencidosList.Add(new
            {
                id = vencidosR.GetInt64(0),
                id_equipo = vencidosR.IsDBNull(1) ? "" : vencidosR.GetString(1),
                dispositivo = vencidosR.IsDBNull(2) ? "" : vencidosR.GetString(2),
                ubicacion = vencidosR.IsDBNull(3) ? "" : vencidosR.GetString(3),
                planta = vencidosR.IsDBNull(4) ? "" : vencidosR.GetString(4),
                plazo = vencidosR.IsDBNull(5) ? "" : vencidosR.GetString(5),
                ultimo_pm = vencidosR.IsDBNull(6) ? "" : vencidosR.GetDateTime(6).ToString("yyyy-MM-dd"),
                color = vencidosR.IsDBNull(7) ? "" : vencidosR.GetString(7),
            });
        vencidosR.Close();

        return Ok(new
        {
            kpis,
            por_color = porColor,
            por_planta = porPlanta,
            ultimos,
            esta_semana = estaSemana,
            proximos_mes = proximos,
            vencidos = vencidosList,
        });
    }
}