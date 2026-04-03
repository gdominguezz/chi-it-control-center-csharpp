using ChiIT.Data;
using Microsoft.AspNetCore.Mvc;
using Npgsql;

namespace ChiIT.Controllers;

[ApiController]
public class DashboardController : ControllerBase
{
    private readonly DbConnectionPool _db;
    public DashboardController(DbConnectionPool db) => _db = db;

    [HttpGet("DASHBOARD")]
    public IActionResult ObtenerDashboard()
    {
        try
        {
            using var conn = _db.Open();

            // ── 1. KPIs generales ──────────────────────────────
            using var kpiCmd = conn.CreateCommand();
            kpiCmd.CommandText = """
                SELECT
                  COUNT(*)                                                                          AS total_equipos,
                  COUNT(*) FILTER (WHERE preventivo_digital    IS NOT NULL)                        AS con_pm_p1,
                  COUNT(*) FILTER (WHERE preventivo_digital    IS NULL)                            AS sin_pm_p1,
                  COUNT(*) FILTER (WHERE preventivo_digital_p2 IS NOT NULL)                        AS con_pm_p2,
                  COUNT(*) FILTER (WHERE preventivo_digital_p2 IS NULL)                            AS sin_pm_p2,
                  COUNT(*) FILTER (WHERE preventivo_digital    IS NOT NULL
                                   AND   preventivo_digital_p2 IS NOT NULL)                        AS completos,
                  COUNT(*) FILTER (WHERE plazo    IS NOT NULL
                                   AND   plazo::date < CURRENT_DATE)                               AS vencidos_p1,
                  COUNT(*) FILTER (WHERE plazo_p2 IS NOT NULL
                                   AND   plazo_p2 < CURRENT_DATE)                                  AS vencidos_p2,
                  COUNT(*) FILTER (WHERE fecha_realizacion >= date_trunc('week', CURRENT_DATE)
                                   AND   fecha_realizacion  <  date_trunc('week', CURRENT_DATE) + interval '7 days') AS semana_p1,
                  COUNT(*) FILTER (WHERE fecha_realizacion_p2 >= date_trunc('week', CURRENT_DATE)
                                   AND   fecha_realizacion_p2 <  date_trunc('week', CURRENT_DATE) + interval '7 days') AS semana_p2
                FROM public.mantenimientos_preventivos
                """;
            using var kpiR = kpiCmd.ExecuteReader();
            kpiR.Read();
            var kpis = new
            {
                total_equipos = kpiR.GetInt64(0),
                con_pm_p1 = kpiR.GetInt64(1),
                sin_pm_p1 = kpiR.GetInt64(2),
                con_pm_p2 = kpiR.GetInt64(3),
                sin_pm_p2 = kpiR.GetInt64(4),
                completos = kpiR.GetInt64(5),
                vencidos_p1 = kpiR.GetInt64(6),
                vencidos_p2 = kpiR.GetInt64(7),
                semana_p1 = kpiR.GetInt64(8),
                semana_p2 = kpiR.GetInt64(9),
                total_pms_realizados = kpiR.GetInt64(1) + kpiR.GetInt64(3),
                total_vencidos = kpiR.GetInt64(6) + kpiR.GetInt64(7),
                semana_total = kpiR.GetInt64(8) + kpiR.GetInt64(9),
            };
            kpiR.Close();

            // ── 2. Por categoría de color ──────────────────────
            using var colorCmd = conn.CreateCommand();
            colorCmd.CommandText = """
                SELECT COALESCE(LOWER(categoria_color), 'sin categoría') AS cat,
                       COUNT(*) AS total,
                       COUNT(*) FILTER (WHERE preventivo_digital    IS NOT NULL) AS con_p1,
                       COUNT(*) FILTER (WHERE preventivo_digital_p2 IS NOT NULL) AS con_p2
                FROM public.mantenimientos_preventivos
                GROUP BY cat ORDER BY total DESC
                """;
            var porColor = new List<object>();
            using var colorR = colorCmd.ExecuteReader();
            while (colorR.Read())
                porColor.Add(new
                {
                    categoria = colorR.GetString(0),
                    total = colorR.GetInt64(1),
                    con_p1 = colorR.GetInt64(2),
                    con_p2 = colorR.GetInt64(3),
                });
            colorR.Close();

            // ── 3. Por planta ──────────────────────────────────
            using var plantaCmd = conn.CreateCommand();
            plantaCmd.CommandText = """
                SELECT COALESCE(planta, 'Sin planta') AS planta,
                       COUNT(*) AS total,
                       COUNT(*) FILTER (WHERE preventivo_digital    IS NOT NULL) AS con_p1,
                       COUNT(*) FILTER (WHERE preventivo_digital_p2 IS NOT NULL) AS con_p2
                FROM public.mantenimientos_preventivos
                GROUP BY planta ORDER BY total DESC LIMIT 10
                """;
            var porPlanta = new List<object>();
            using var plantaR = plantaCmd.ExecuteReader();
            while (plantaR.Read())
                porPlanta.Add(new
                {
                    planta = plantaR.GetString(0),
                    total = plantaR.GetInt64(1),
                    con_p1 = plantaR.GetInt64(2),
                    con_p2 = plantaR.GetInt64(3),
                });
            plantaR.Close();

            // ── 4. Últimos 10 PMs realizados (P1 + P2) ────────
            using var ultimosCmd = conn.CreateCommand();
            ultimosCmd.CommandText = """
                SELECT id, id_equipo, nombre_dispositivo, ubicacion, planta,
                       fecha_realizacion AS fecha, realizado_por, 1 AS periodo
                FROM public.mantenimientos_preventivos
                WHERE fecha_realizacion IS NOT NULL

                UNION ALL

                SELECT id, id_equipo, nombre_dispositivo, ubicacion, planta,
                       fecha_realizacion_p2 AS fecha, realizado_por_p2, 2 AS periodo
                FROM public.mantenimientos_preventivos
                WHERE fecha_realizacion_p2 IS NOT NULL

                ORDER BY fecha DESC LIMIT 10
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
                    periodo = ultimosR.GetInt32(7),
                });
            ultimosR.Close();

            // ── 5. Esta semana (P1 + P2) ──────────────────────
            using var semanaCmd = conn.CreateCommand();
            semanaCmd.CommandText = """
                SELECT id, id_equipo, nombre_dispositivo, ubicacion, planta,
                       fecha_realizacion AS fecha, realizado_por, 1 AS periodo
                FROM public.mantenimientos_preventivos
                WHERE fecha_realizacion >= date_trunc('week', CURRENT_DATE)
                  AND fecha_realizacion <  date_trunc('week', CURRENT_DATE) + interval '7 days'

                UNION ALL

                SELECT id, id_equipo, nombre_dispositivo, ubicacion, planta,
                       fecha_realizacion_p2 AS fecha, realizado_por_p2, 2 AS periodo
                FROM public.mantenimientos_preventivos
                WHERE fecha_realizacion_p2 >= date_trunc('week', CURRENT_DATE)
                  AND fecha_realizacion_p2 <  date_trunc('week', CURRENT_DATE) + interval '7 days'

                ORDER BY fecha DESC
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
                    periodo = semanaR.GetInt32(7),
                });
            semanaR.Close();

            // ── 6. Próximos PM del mes actual (P1 + P2) ───────
            // plazo es TEXT, plazo_p2 es DATE — se normaliza todo a TEXT para el UNION
            using var mesCmd = conn.CreateCommand();
            mesCmd.CommandText = """
                SELECT id, id_equipo, nombre_dispositivo, ubicacion, planta,
                       plazo AS plazo_fecha, categoria_color, 1 AS periodo
                FROM public.mantenimientos_preventivos
                WHERE plazo IS NOT NULL
                  AND plazo::date >= date_trunc('month', CURRENT_DATE)
                  AND plazo::date <  date_trunc('month', CURRENT_DATE) + interval '1 month'

                UNION ALL

                SELECT id, id_equipo, nombre_dispositivo, ubicacion, planta,
                       TO_CHAR(plazo_p2, 'YYYY-MM-DD') AS plazo_fecha, categoria_color, 2 AS periodo
                FROM public.mantenimientos_preventivos
                WHERE plazo_p2 IS NOT NULL
                  AND plazo_p2 >= date_trunc('month', CURRENT_DATE)
                  AND plazo_p2 <  date_trunc('month', CURRENT_DATE) + interval '1 month'

                ORDER BY plazo_fecha ASC
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
                    periodo = mesR.GetInt32(7),
                });
            mesR.Close();

            // ── 7. Vencidos P1 + P2 ───────────────────────────
            using var vencidosCmd = conn.CreateCommand();
            vencidosCmd.CommandText = """
                SELECT id, id_equipo, nombre_dispositivo, ubicacion, planta,
                       plazo AS plazo_fecha, fecha_realizacion AS ultimo_pm, categoria_color, 1 AS periodo
                FROM public.mantenimientos_preventivos
                WHERE plazo IS NOT NULL AND plazo::date < CURRENT_DATE
                LIMIT 25

                UNION ALL

                SELECT id, id_equipo, nombre_dispositivo, ubicacion, planta,
                       TO_CHAR(plazo_p2, 'YYYY-MM-DD') AS plazo_fecha, fecha_realizacion_p2 AS ultimo_pm, categoria_color, 2 AS periodo
                FROM public.mantenimientos_preventivos
                WHERE plazo_p2 IS NOT NULL AND plazo_p2 < CURRENT_DATE
                LIMIT 25

                ORDER BY plazo_fecha ASC
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
                    periodo = vencidosR.GetInt32(8),
                });
            vencidosR.Close();

            // ── 8. PM por mes — últimos 12 meses (P1 + P2) ────
            using var mesHistCmd = conn.CreateCommand();
            mesHistCmd.CommandText = """
                SELECT mes, SUM(p1) AS p1, SUM(p2) AS p2, SUM(p1)+SUM(p2) AS total
                FROM (
                    SELECT TO_CHAR(fecha_realizacion, 'YYYY-MM') AS mes, 1 AS p1, 0 AS p2
                    FROM public.mantenimientos_preventivos
                    WHERE fecha_realizacion IS NOT NULL
                      AND fecha_realizacion >= NOW() - INTERVAL '12 months'

                    UNION ALL

                    SELECT TO_CHAR(fecha_realizacion_p2, 'YYYY-MM') AS mes, 0 AS p1, 1 AS p2
                    FROM public.mantenimientos_preventivos
                    WHERE fecha_realizacion_p2 IS NOT NULL
                      AND fecha_realizacion_p2 >= NOW() - INTERVAL '12 months'
                ) sub
                GROUP BY mes ORDER BY mes ASC
                """;
            var pmPorMes = new List<object>();
            using var mesHistR = mesHistCmd.ExecuteReader();
            while (mesHistR.Read())
                pmPorMes.Add(new
                {
                    mes = mesHistR.GetString(0),
                    p1 = mesHistR.GetInt64(1),
                    p2 = mesHistR.GetInt64(2),
                    total = mesHistR.GetInt64(3),
                });
            mesHistR.Close();

            // ── 9. Estado digital P1 vs P2 ────────────────────
            using var digitalCmd = conn.CreateCommand();
            digitalCmd.CommandText = """
                SELECT
                  COUNT(*) FILTER (WHERE preventivo_digital    IS NOT NULL) AS con_digital_p1,
                  COUNT(*) FILTER (WHERE preventivo_digital    IS NULL)     AS sin_digital_p1,
                  COUNT(*) FILTER (WHERE preventivo_digital_p2 IS NOT NULL) AS con_digital_p2,
                  COUNT(*) FILTER (WHERE preventivo_digital_p2 IS NULL)     AS sin_digital_p2
                FROM public.mantenimientos_preventivos
                """;
            using var digitalR = digitalCmd.ExecuteReader();
            digitalR.Read();
            var pmDigital = new
            {
                con_digital_p1 = digitalR.GetInt64(0),
                sin_digital_p1 = digitalR.GetInt64(1),
                con_digital_p2 = digitalR.GetInt64(2),
                sin_digital_p2 = digitalR.GetInt64(3),
            };
            digitalR.Close();

            // ── 10. Salud del Parque ───────────────────────────
            // plazo es TEXT, plazo_p2 es DATE — se castea plazo a date para comparar
            // peor_plazo = el más próximo a vencer de los dos períodos
            using var saludCmd = conn.CreateCommand();
            saludCmd.CommandText = """
                WITH plazos AS (
                    SELECT
                        id,
                        LEAST(
                            CASE WHEN plazo    IS NOT NULL THEN plazo::date ELSE NULL END,
                            CASE WHEN plazo_p2 IS NOT NULL THEN plazo_p2    ELSE NULL END
                        ) AS peor_plazo
                    FROM public.mantenimientos_preventivos
                    WHERE plazo IS NOT NULL OR plazo_p2 IS NOT NULL
                )
                SELECT
                    COUNT(*)                                                                             AS total_con_plazo,
                    COUNT(*) FILTER (WHERE peor_plazo < CURRENT_DATE)                                   AS vencidos,
                    COUNT(*) FILTER (WHERE peor_plazo >= CURRENT_DATE
                                     AND   peor_plazo <  date_trunc('month', CURRENT_DATE) + interval '1 month') AS por_vencer_mes,
                    COUNT(*) FILTER (WHERE peor_plazo >= date_trunc('month', CURRENT_DATE) + interval '1 month') AS al_corriente
                FROM plazos
                """;
            using var saludR = saludCmd.ExecuteReader();
            saludR.Read();

            var totalConPlazo = saludR.GetInt64(0);
            var saludVencidos = saludR.GetInt64(1);
            var porVencerMes = saludR.GetInt64(2);
            var alCorriente = saludR.GetInt64(3);
            saludR.Close();

            double indice = 100.0;
            if (totalConPlazo > 0)
            {
                double pctVencidos = (double)saludVencidos / totalConPlazo;
                double pctPorVencer = (double)porVencerMes / totalConPlazo;
                indice = Math.Max(0, Math.Round(100.0 - (pctVencidos * 100.0) - (pctPorVencer * 30.0), 1));
            }

            string emoji = indice >= 90 ? "😊" : indice >= 70 ? "😐" : indice >= 50 ? "😟" : "🚨";

            var saludParque = new
            {
                total_con_plazo = totalConPlazo,
                al_corriente = alCorriente,
                por_vencer_mes = porVencerMes,
                vencidos = saludVencidos,
                pct_al_corriente = totalConPlazo > 0 ? Math.Round((double)alCorriente / totalConPlazo * 100, 1) : 0.0,
                pct_por_vencer = totalConPlazo > 0 ? Math.Round((double)porVencerMes / totalConPlazo * 100, 1) : 0.0,
                pct_vencidos = totalConPlazo > 0 ? Math.Round((double)saludVencidos / totalConPlazo * 100, 1) : 0.0,
                indice_salud = indice,
                emoji,
            };

            return Ok(new
            {
                kpis,
                por_color = porColor,
                por_planta = porPlanta,
                ultimos,
                esta_semana = estaSemana,
                proximos_mes = proximos,
                vencidos = vencidosList,
                pm_por_mes = pmPorMes,
                pm_digital = pmDigital,
                salud_parque = saludParque,
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message, detalle = ex.InnerException?.Message });
        }
    }
}