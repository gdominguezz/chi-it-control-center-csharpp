using ChiIT.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;

namespace ChiIT.Controllers;

[ApiController]
public class DashboardController : ControllerBase
{
    private readonly DbConnectionPool _db;
    public DashboardController(DbConnectionPool db) => _db = db;

    private const string CTE_PLAZOS = """
        WITH cal AS (
            SELECT
                planta_key,
                periodo,
                (
                    DATE_TRUNC('week',
                        (anio_inicio || '-01-01')
                        + ((semana_inicio - 1) * 7) * INTERVAL '1 day'
                    ) + INTERVAL '4 days'
                ) AS plazo_cal
            FROM calendario_estado
            WHERE generado = 1
        ),
        planta_map AS (
            SELECT mp.id,
                   CASE mp.planta
                       WHEN 'B1'              THEN 'B1'
                       WHEN 'B2'              THEN 'B2'
                       WHEN 'PLANTA SATELITE' THEN 'SATELITE'
                       WHEN 'PLANTA MIXING'   THEN 'MIXING'
                       WHEN 'BODEGA'          THEN 'BODEGA'
                       ELSE NULL
                   END AS planta_key
            FROM mantenimientos_preventivos mp
        ),
        equipos_con_plazo AS (
            SELECT
                mp.*,
                cal_p1.plazo_cal AS plazo_cal_p1,
                cal_p2.plazo_cal AS plazo_cal_p2
            FROM mantenimientos_preventivos mp
            JOIN planta_map pm ON pm.id = mp.id
            LEFT JOIN cal cal_p1 ON cal_p1.planta_key = pm.planta_key AND cal_p1.periodo = 1
            LEFT JOIN cal cal_p2 ON cal_p2.planta_key = pm.planta_key AND cal_p2.periodo = 2
        )
        """;

    [HttpGet("DASHBOARD")]
    public IActionResult ObtenerDashboard()
    {
        try
        {
            using var conn = _db.Open();

            // ── 1. KPIs generales ──────────────────────────────────────────
            using var kpiCmd = conn.CreateCommand();
            kpiCmd.CommandText = CTE_PLAZOS + """
                SELECT
                  COUNT(*)                                                                              AS total_equipos,
                  COUNT(*) FILTER (WHERE preventivo_digital    IS NOT NULL)                            AS con_pm_p1,
                  COUNT(*) FILTER (WHERE preventivo_digital    IS NULL)                                AS sin_pm_p1,
                  COUNT(*) FILTER (WHERE preventivo_digital_p2 IS NOT NULL)                           AS con_pm_p2,
                  COUNT(*) FILTER (WHERE preventivo_digital_p2 IS NULL)                               AS sin_pm_p2,
                  COUNT(*) FILTER (WHERE preventivo_digital    IS NOT NULL
                                   AND   preventivo_digital_p2 IS NOT NULL)                            AS completos,
                  COUNT(*) FILTER (WHERE plazo_cal_p1 IS NOT NULL
                                   AND   plazo_cal_p1 < CAST(GETDATE() AS DATE)
                                   AND   preventivo_digital IS NULL)                                   AS vencidos_p1,
                  COUNT(*) FILTER (WHERE plazo_cal_p2 IS NOT NULL
                                   AND   plazo_cal_p2 < CAST(GETDATE() AS DATE)
                                   AND   preventivo_digital_p2 IS NULL)                               AS vencidos_p2,
                  COUNT(*) FILTER (WHERE fecha_realizacion >= date_trunc('week', CAST(GETDATE() AS DATE))
                                   AND   fecha_realizacion  <  date_trunc('week', CAST(GETDATE() AS DATE)) + interval '7 days') AS semana_p1,
                  COUNT(*) FILTER (WHERE fecha_realizacion_p2 >= date_trunc('week', CAST(GETDATE() AS DATE))
                                   AND   fecha_realizacion_p2 <  date_trunc('week', CAST(GETDATE() AS DATE)) + interval '7 days') AS semana_p2,
                  COUNT(*) FILTER (WHERE plazo_cal_p1 IS NULL AND plazo_cal_p2 IS NULL)               AS sin_plazo_asignado
                FROM equipos_con_plazo
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
                sin_plazo_asignado = kpiR.GetInt64(10),
                total_pms_realizados = kpiR.GetInt64(1) + kpiR.GetInt64(3),
                total_vencidos = kpiR.GetInt64(6) + kpiR.GetInt64(7),
                semana_total = kpiR.GetInt64(8) + kpiR.GetInt64(9),
            };
            kpiR.Close();

            // ── 2. Por categoría de color ──────────────────────────────────
            using var colorCmd = conn.CreateCommand();
            colorCmd.CommandText = CTE_PLAZOS + """
                SELECT COALESCE(LOWER(categoria_color), 'sin categoria') AS cat,
                       COUNT(*) AS total,
                       COUNT(*) FILTER (WHERE preventivo_digital    IS NOT NULL) AS con_p1,
                       COUNT(*) FILTER (WHERE preventivo_digital_p2 IS NOT NULL) AS con_p2
                FROM equipos_con_plazo
                GROUP BY cat ORDER BY total DESC
                """;
            var porColor = new List<object>();
            using var colorR = colorCmd.ExecuteReader();
            while (colorR.Read())
                porColor.Add(new { categoria = colorR.GetString(0), total = colorR.GetInt64(1), con_p1 = colorR.GetInt64(2), con_p2 = colorR.GetInt64(3) });
            colorR.Close();

            // ── 3. Por planta ──────────────────────────────────────────────
            using var plantaCmd = conn.CreateCommand();
            plantaCmd.CommandText = CTE_PLAZOS + """
                SELECT
                    COALESCE(planta, 'Sin planta') AS planta,
                    COUNT(*) AS total,
                    COUNT(*) FILTER (WHERE preventivo_digital    IS NOT NULL) AS con_p1,
                    COUNT(*) FILTER (WHERE preventivo_digital_p2 IS NOT NULL) AS con_p2,
                    MAX(plazo_cal_p1) AS plazo_p1_cal,
                    MAX(plazo_cal_p2) AS plazo_p2_cal,
                    COUNT(*) FILTER (WHERE plazo_cal_p1 < CAST(GETDATE() AS DATE) AND preventivo_digital    IS NULL) AS vencidos_p1,
                    COUNT(*) FILTER (WHERE plazo_cal_p2 < CAST(GETDATE() AS DATE) AND preventivo_digital_p2 IS NULL) AS vencidos_p2
                FROM equipos_con_plazo
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
                    plazo_p1 = plantaR.IsDBNull(4) ? null : plantaR.GetDateTime(4).ToString("yyyy-MM-dd"),
                    plazo_p2 = plantaR.IsDBNull(5) ? null : plantaR.GetDateTime(5).ToString("yyyy-MM-dd"),
                    vencidos_p1 = plantaR.GetInt64(6),
                    vencidos_p2 = plantaR.GetInt64(7),
                });
            plantaR.Close();

            // ── 4. Últimos 10 PMs realizados ──────────────────────────────
            using var ultimosCmd = conn.CreateCommand();
            ultimosCmd.CommandText = """
                SELECT id, id_equipo, nombre_dispositivo, ubicacion, planta,
                       fecha_realizacion AS fecha, realizado_por, 1 AS periodo
                FROM mantenimientos_preventivos WHERE fecha_realizacion IS NOT NULL
                UNION ALL
                SELECT id, id_equipo, nombre_dispositivo, ubicacion, planta,
                       fecha_realizacion_p2, realizado_por_p2, 2
                FROM mantenimientos_preventivos WHERE fecha_realizacion_p2 IS NOT NULL
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

            // ── 5. Esta semana ─────────────────────────────────────────────
            using var semanaCmd = conn.CreateCommand();
            semanaCmd.CommandText = """
                SELECT id, id_equipo, nombre_dispositivo, ubicacion, planta,
                       fecha_realizacion, realizado_por, 1
                FROM mantenimientos_preventivos
                WHERE fecha_realizacion >= date_trunc('week', CAST(GETDATE() AS DATE))
                  AND fecha_realizacion <  date_trunc('week', CAST(GETDATE() AS DATE)) + interval '7 days'
                UNION ALL
                SELECT id, id_equipo, nombre_dispositivo, ubicacion, planta,
                       fecha_realizacion_p2, realizado_por_p2, 2
                FROM mantenimientos_preventivos
                WHERE fecha_realizacion_p2 >= date_trunc('week', CAST(GETDATE() AS DATE))
                  AND fecha_realizacion_p2 <  date_trunc('week', CAST(GETDATE() AS DATE)) + interval '7 days'
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
                    periodo = semanaR.GetInt32(7),
                });
            semanaR.Close();

            // ── 6. Próximos del mes (sin PM, plazo calendario este mes) ────
            using var mesCmd = conn.CreateCommand();
            mesCmd.CommandText = CTE_PLAZOS + """
                SELECT id, id_equipo, nombre_dispositivo, ubicacion, planta,
                       plazo_cal_p1, categoria_color, 1
                FROM equipos_con_plazo
                WHERE plazo_cal_p1 IS NOT NULL AND preventivo_digital IS NULL
                  AND plazo_cal_p1 >= date_trunc('month', CAST(GETDATE() AS DATE))
                  AND plazo_cal_p1 <  date_trunc('month', CAST(GETDATE() AS DATE)) + interval '1 month'
                UNION ALL
                SELECT id, id_equipo, nombre_dispositivo, ubicacion, planta,
                       plazo_cal_p2, categoria_color, 2
                FROM equipos_con_plazo
                WHERE plazo_cal_p2 IS NOT NULL AND preventivo_digital_p2 IS NULL
                  AND plazo_cal_p2 >= date_trunc('month', CAST(GETDATE() AS DATE))
                  AND plazo_cal_p2 <  date_trunc('month', CAST(GETDATE() AS DATE)) + interval '1 month'
                ORDER BY plazo_cal_p1 ASC
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

            // ── 7. Vencidos (sin PM, plazo calendario ya pasó) ────────────
            using var vencidosCmd = conn.CreateCommand();
            vencidosCmd.CommandText = CTE_PLAZOS + """
                SELECT id, id_equipo, nombre_dispositivo, ubicacion, planta,
                       plazo_cal_p1, fecha_realizacion, categoria_color, 1
                FROM equipos_con_plazo
                WHERE plazo_cal_p1 IS NOT NULL AND plazo_cal_p1 < CAST(GETDATE() AS DATE)
                  AND preventivo_digital IS NULL
                UNION ALL
                SELECT id, id_equipo, nombre_dispositivo, ubicacion, planta,
                       plazo_cal_p2, fecha_realizacion_p2, categoria_color, 2
                FROM equipos_con_plazo
                WHERE plazo_cal_p2 IS NOT NULL AND plazo_cal_p2 < CAST(GETDATE() AS DATE)
                  AND preventivo_digital_p2 IS NULL
                ORDER BY plazo_cal_p1 ASC LIMIT 50
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

            // ── 8. PM por mes — últimos 12 meses ──────────────────────────
            using var mesHistCmd = conn.CreateCommand();
            mesHistCmd.CommandText = """
                SELECT mes, SUM(p1), SUM(p2), SUM(p1)+SUM(p2)
                FROM (
                    SELECT TO_CHAR(fecha_realizacion,    'YYYY-MM') AS mes, 1 AS p1, 0 AS p2
                    FROM mantenimientos_preventivos
                    WHERE fecha_realizacion    IS NOT NULL AND fecha_realizacion    >= GETDATE() - INTERVAL '12 months'
                    UNION ALL
                    SELECT TO_CHAR(fecha_realizacion_p2, 'YYYY-MM'),               0,         1
                    FROM mantenimientos_preventivos
                    WHERE fecha_realizacion_p2 IS NOT NULL AND fecha_realizacion_p2 >= GETDATE() - INTERVAL '12 months'
                ) sub
                GROUP BY mes ORDER BY mes ASC
                """;
            var pmPorMes = new List<object>();
            using var mesHistR = mesHistCmd.ExecuteReader();
            while (mesHistR.Read())
                pmPorMes.Add(new { mes = mesHistR.GetString(0), p1 = mesHistR.GetInt64(1), p2 = mesHistR.GetInt64(2), total = mesHistR.GetInt64(3) });
            mesHistR.Close();

            // ── 9. Estado digital ─────────────────────────────────────────
            using var digitalCmd = conn.CreateCommand();
            digitalCmd.CommandText = """
                SELECT
                  COUNT(*) FILTER (WHERE preventivo_digital    IS NOT NULL) AS con_digital_p1,
                  COUNT(*) FILTER (WHERE preventivo_digital    IS NULL)     AS sin_digital_p1,
                  COUNT(*) FILTER (WHERE preventivo_digital_p2 IS NOT NULL) AS con_digital_p2,
                  COUNT(*) FILTER (WHERE preventivo_digital_p2 IS NULL)     AS sin_digital_p2
                FROM mantenimientos_preventivos
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

            // ── 10. Salud del parque ───────────────────────────────────────
            using var saludCmd = conn.CreateCommand();
            saludCmd.CommandText = CTE_PLAZOS + """
                SELECT
                    COUNT(*) FILTER (WHERE plazo_cal_p1 IS NOT NULL OR plazo_cal_p2 IS NOT NULL) AS total_con_plazo,
                    COUNT(*) FILTER (WHERE (plazo_cal_p1 < CAST(GETDATE() AS DATE) AND preventivo_digital    IS NULL)
                                        OR (plazo_cal_p2 < CAST(GETDATE() AS DATE) AND preventivo_digital_p2 IS NULL)) AS vencidos,
                    COUNT(*) FILTER (WHERE (plazo_cal_p1 >= CAST(GETDATE() AS DATE)
                                        AND plazo_cal_p1 < date_trunc('month', CAST(GETDATE() AS DATE)) + interval '1 month'
                                        AND preventivo_digital IS NULL)
                                       OR  (plazo_cal_p2 >= CAST(GETDATE() AS DATE)
                                        AND plazo_cal_p2 < date_trunc('month', CAST(GETDATE() AS DATE)) + interval '1 month'
                                        AND preventivo_digital_p2 IS NULL)) AS por_vencer_mes,
                    COUNT(*) FILTER (WHERE (preventivo_digital    IS NOT NULL OR plazo_cal_p1 IS NULL
                                            OR plazo_cal_p1 >= date_trunc('month', CAST(GETDATE() AS DATE)) + interval '1 month')
                                      AND  (preventivo_digital_p2 IS NOT NULL OR plazo_cal_p2 IS NULL
                                            OR plazo_cal_p2 >= date_trunc('month', CAST(GETDATE() AS DATE)) + interval '1 month')) AS al_corriente
                FROM equipos_con_plazo
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

            // ── 11. Estado del calendario por planta ──────────────────────
            using var calCmd = conn.CreateCommand();
            calCmd.CommandText = """
                SELECT planta_key, periodo, semana_inicio, anio_inicio, generado, terminado,
                       (
                           DATE_TRUNC('week',
                               (anio_inicio || '-01-01')
                               + ((semana_inicio - 1) * 7) * INTERVAL '1 day'
                           ) + INTERVAL '4 days'
                       ) AS plazo_viernes
                FROM calendario_estado
                ORDER BY planta_key, periodo
                """;
            var calEstado = new List<object>();
            using var calR = calCmd.ExecuteReader();
            while (calR.Read())
                calEstado.Add(new
                {
                    planta = calR.GetString(0),
                    periodo = calR.GetInt32(1),
                    semana_inicio = calR.IsDBNull(2) ? (int?)null : calR.GetInt32(2),
                    anio_inicio = calR.IsDBNull(3) ? (int?)null : calR.GetInt32(3),
                    generado = calR.GetBoolean(4),
                    terminado = calR.GetBoolean(5),
                    plazo_viernes = calR.IsDBNull(6) ? null : calR.GetDateTime(6).ToString("yyyy-MM-dd"),
                });
            calR.Close();

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
                calendario = calEstado,
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message, detalle = ex.InnerException?.Message });
        }
    }
}