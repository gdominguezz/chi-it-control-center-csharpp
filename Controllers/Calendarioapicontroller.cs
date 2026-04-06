using ChiIT.Data;
using Microsoft.AspNetCore.Mvc;

namespace ChiIT.Controllers;

[ApiController]
public class CalendarioApiController : ControllerBase
{
    private readonly DbConnectionPool _db;
    public CalendarioApiController(DbConnectionPool db) => _db = db;

    // ── GET /CALENDARIO/API?anio=2025 ─────────────────────────────────────
    // Devuelve para cada planta, para cada semana (1-53), los equipos
    // con su fecha de PM P1 y P2 (realizadas o próximas).
    [HttpGet("CALENDARIO/API")]
    public IActionResult ObtenerCalendario([FromQuery] int? anio)
    {
        int year = anio ?? DateTime.Now.Year;

        using var conn = _db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT
                COALESCE(planta, 'Sin planta')      AS planta,
                id,
                id_equipo,
                nombre_dispositivo,
                ubicacion,
                categoria_color,
                -- P1
                fecha_realizacion                   AS fecha_p1,
                plazo                               AS plazo_p1,
                realizado_por                       AS tecnico_p1,
                -- P2
                fecha_realizacion_p2                AS fecha_p2,
                plazo_p2                            AS plazo_p2,
                realizado_por_p2                    AS tecnico_p2
            FROM public.mantenimientos_preventivos
            ORDER BY planta, ubicacion, nombre_dispositivo
            """;

        // Construir estructura: planta -> semana -> lista de equipos
        // Usamos el plazo para ubicar en qué semana cae el mantenimiento.
        // Si no hay plazo, usamos fecha_realizacion como referencia.

        var plantasDict = new Dictionary<string, List<EquipoCalendario>>();

        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            var planta = r.GetString(0);
            var eq = new EquipoCalendario
            {
                Id = r.GetInt64(1),
                IdEquipo = r.IsDBNull(2) ? "" : r.GetString(2),
                Dispositivo = r.IsDBNull(3) ? "" : r.GetString(3),
                Ubicacion = r.IsDBNull(4) ? "" : r.GetString(4),
                Color = r.IsDBNull(5) ? "" : r.GetString(5),
                FechaP1 = r.IsDBNull(6) ? (DateTime?)null : r.GetDateTime(6),
                PlazoP1Raw = r.IsDBNull(7) ? null : r.GetString(7),
                TecnicoP1 = r.IsDBNull(8) ? "" : r.GetString(8),
                FechaP2 = r.IsDBNull(9) ? (DateTime?)null : r.GetDateTime(9),
                PlazoP2Raw = r.IsDBNull(10) ? null : r.GetString(10),
                TecnicoP2 = r.IsDBNull(11) ? "" : r.GetString(11),
            };

            if (!plantasDict.ContainsKey(planta))
                plantasDict[planta] = new List<EquipoCalendario>();
            plantasDict[planta].Add(eq);
        }
        r.Close();

        // Convertir a respuesta con semanas
        var result = new Dictionary<string, object>();

        foreach (var (planta, equipos) in plantasDict)
        {
            // semana -> periodo -> lista
            var semanas = new Dictionary<int, object>();

            foreach (var eq in equipos)
            {
                // Calcular semana ISO de P1 (plazo_p1 primero, si no fecha_p1)
                var semanaP1 = SemanaISO(ParseFecha(eq.PlazoP1Raw) ?? eq.FechaP1, year);
                var semanaP2 = SemanaISO(ParseFecha(eq.PlazoP2Raw) ?? eq.FechaP2, year);

                void AgregarASemana(int? semana, int periodo)
                {
                    if (semana == null) return;
                    int s = semana.Value;
                    if (!semanas.ContainsKey(s))
                        semanas[s] = new { p1 = new List<object>(), p2 = new List<object>() };

                    var semObj = (dynamic)semanas[s];
                    var lista = periodo == 1 ? (List<object>)semObj.p1 : (List<object>)semObj.p2;

                    lista.Add(new
                    {
                        id = eq.Id,
                        id_equipo = eq.IdEquipo,
                        dispositivo = eq.Dispositivo,
                        ubicacion = eq.Ubicacion,
                        color = eq.Color,
                        fecha = periodo == 1
                                        ? eq.FechaP1?.ToString("yyyy-MM-dd")
                                        : eq.FechaP2?.ToString("yyyy-MM-dd"),
                        plazo = periodo == 1 ? eq.PlazoP1Raw : eq.PlazoP2Raw,
                        tecnico = periodo == 1 ? eq.TecnicoP1 : eq.TecnicoP2,
                        realizado = periodo == 1 ? eq.FechaP1 != null : eq.FechaP2 != null,
                    });
                }

                AgregarASemana(semanaP1, 1);
                AgregarASemana(semanaP2, 2);
            }

            result[planta] = semanas;
        }

        // Lista de plantas disponibles
        var plantas = plantasDict.Keys.OrderBy(p => p).ToList();

        return Ok(new { anio = year, plantas, calendario = result });
    }

    // ── GET /CALENDARIO/PLANTAS ───────────────────────────────────────────
    [HttpGet("CALENDARIO/PLANTAS")]
    public IActionResult ObtenerPlantas()
    {
        using var conn = _db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT DISTINCT COALESCE(planta,'Sin planta') AS planta
            FROM public.mantenimientos_preventivos
            ORDER BY planta
            """;
        var lista = new List<string>();
        using var r = cmd.ExecuteReader();
        while (r.Read()) lista.Add(r.GetString(0));
        return Ok(new { plantas = lista });
    }

    // ── Helpers ───────────────────────────────────────────────────────────
    private static DateTime? ParseFecha(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        return DateTime.TryParse(raw, out var d) ? d : null;
    }

    private static int? SemanaISO(DateTime? fecha, int year)
    {
        if (fecha == null) return null;
        var d = fecha.Value;
        // Solo mostrar si cae en el año solicitado (±1 semana de margen en dic/ene)
        if (d.Year < year - 1 || d.Year > year + 1) return null;
        // ISO 8601: semana empieza el lunes
        var cal = System.Globalization.CultureInfo.InvariantCulture.Calendar;
        int week = cal.GetWeekOfYear(d,
            System.Globalization.CalendarWeekRule.FirstFourDayWeek,
            DayOfWeek.Monday);
        // Normalizar: si la fecha es de dic pero semana 1 -> pertenece al año siguiente
        if (d.Month == 12 && week == 1) return null;
        // Si la fecha es de ene pero semana 52/53 -> pertenece al año anterior
        if (d.Month == 1 && week >= 52) return null;
        return week;
    }
}

internal class EquipoCalendario
{
    public long Id { get; set; }
    public string IdEquipo { get; set; } = "";
    public string Dispositivo { get; set; } = "";
    public string Ubicacion { get; set; } = "";
    public string Color { get; set; } = "";
    public DateTime? FechaP1 { get; set; }
    public string? PlazoP1Raw { get; set; }
    public string TecnicoP1 { get; set; } = "";
    public DateTime? FechaP2 { get; set; }
    public string? PlazoP2Raw { get; set; }
    public string TecnicoP2 { get; set; } = "";
}