using ChiIT.Data;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace ChiIT.Controllers;

[ApiController]
public class CalendarioApiController : ControllerBase
{
    private readonly DbConnectionPool _db;
    public CalendarioApiController(DbConnectionPool db) => _db = db;

    // ── GET /CALENDARIO/API?anio=2025 ─────────────────────────────────────
    // Devuelve para cada planta los equipos distribuidos semana a semana
    // según las fechas de inicio de P1 y P2 configuradas.
    // Distribución: orden alfabético, 24-30 equipos por semana obligatorio.
    [HttpGet("CALENDARIO/API")]
    public IActionResult ObtenerCalendario([FromQuery] int? anio)
    {
        int year = anio ?? DateTime.Now.Year;

        using var conn = _db.Open();

        // ── Leer configuración de períodos ──
        var config = LeerConfigPeriodos(conn);

        // ── Leer todos los equipos ──
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT
                COALESCE(planta, 'Sin planta')      AS planta,
                id,
                id_equipo,
                nombre_dispositivo,
                ubicacion,
                categoria_color,
                fecha_realizacion                   AS fecha_p1,
                realizado_por                       AS tecnico_p1,
                fecha_realizacion_p2                AS fecha_p2,
                realizado_por_p2                    AS tecnico_p2
            FROM public.mantenimientos_preventivos
            ORDER BY planta, nombre_dispositivo, id_equipo
            """;

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
                TecnicoP1 = r.IsDBNull(7) ? "" : r.GetString(7),
                FechaP2 = r.IsDBNull(8) ? (DateTime?)null : r.GetDateTime(8),
                TecnicoP2 = r.IsDBNull(9) ? "" : r.GetString(9),
            };

            if (!plantasDict.ContainsKey(planta))
                plantasDict[planta] = new List<EquipoCalendario>();
            plantasDict[planta].Add(eq);
        }
        r.Close();

        // ── Distribuir equipos por semana (24-30 por semana, orden alfabético) ──
        var result = new Dictionary<string, object>();

        foreach (var (planta, equipos) in plantasDict)
        {
            // Ordenar alfabéticamente por nombre_dispositivo luego id_equipo
            var equiposOrdenados = equipos
                .OrderBy(e => e.Dispositivo, StringComparer.OrdinalIgnoreCase)
                .ThenBy(e => e.IdEquipo, StringComparer.OrdinalIgnoreCase)
                .ToList();

            var semanas = new Dictionary<int, object>();

            // ── Distribución P1 ──
            if (config.InicioP1 != null)
            {
                var inicio1 = config.InicioP1.Value;
                DistribuirEnSemanas(equiposOrdenados, inicio1, year, 1, semanas, config);
            }

            // ── Distribución P2 ──
            if (config.InicioP2 != null)
            {
                var inicio2 = config.InicioP2.Value;
                DistribuirEnSemanas(equiposOrdenados, inicio2, year, 2, semanas, config);
            }

            result[planta] = semanas;
        }

        var plantas = plantasDict.Keys.OrderBy(p => p).ToList();

        return Ok(new
        {
            anio = year,
            plantas,
            calendario = result,
            config = new
            {
                p1_activo = config.InicioP1 != null,
                p2_activo = config.InicioP2 != null,
                inicio_p1 = config.InicioP1?.ToString("yyyy-MM-dd"),
                inicio_p2 = config.InicioP2?.ToString("yyyy-MM-dd"),
                semana_inicio_p1 = config.InicioP1 != null ? SemanaISO(config.InicioP1.Value) : (int?)null,
                semana_inicio_p2 = config.InicioP2 != null ? SemanaISO(config.InicioP2.Value) : (int?)null,
            }
        });
    }

    // ── POST /CALENDARIO/PERIODO ──────────────────────────────────────────
    // Body: { "periodo": 1, "fecha": "2025-02-03" }
    // Solo ADMIN puede configurar P2.
    [HttpPost("CALENDARIO/PERIODO")]
    public IActionResult ConfigurarPeriodo([FromBody] JsonElement body)
    {
        if (!body.TryGetProperty("periodo", out var periodoEl) ||
            !body.TryGetProperty("fecha", out var fechaEl))
            return BadRequest(new { error = "Se requieren campos 'periodo' y 'fecha'" });

        int periodo = periodoEl.GetInt32();
        if (!DateTime.TryParse(fechaEl.GetString(), out var fecha))
            return BadRequest(new { error = "Fecha inválida" });

        // Para P2, verificar que el usuario sea ADMIN
        if (periodo == 2)
        {
            // Obtener usuario de la sesión (cookie/header)
            var usuario = ObtenerUsuarioActual();
            if (usuario == null || !usuario.EsAdmin)
                return Forbid();
        }

        using var conn = _db.Open();
        GuardarConfigPeriodo(conn, periodo, fecha);

        var semanaNum = SemanaISO(fecha);
        return Ok(new
        {
            ok = true,
            periodo,
            fecha = fecha.ToString("yyyy-MM-dd"),
            semana = semanaNum,
            mensaje = $"Período {periodo} iniciará en la semana {semanaNum} ({fecha:dd/MM/yyyy})"
        });
    }

    // ── DELETE /CALENDARIO/PERIODO/{n} ────────────────────────────────────
    [HttpDelete("CALENDARIO/PERIODO/{periodo}")]
    public IActionResult ResetearPeriodo(int periodo)
    {
        if (periodo == 2)
        {
            var usuario = ObtenerUsuarioActual();
            if (usuario == null || !usuario.EsAdmin) return Forbid();
        }
        using var conn = _db.Open();
        EliminarConfigPeriodo(conn, periodo);
        return Ok(new { ok = true });
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

    // ── GET /CALENDARIO/CONFIG ────────────────────────────────────────────
    [HttpGet("CALENDARIO/CONFIG")]
    public IActionResult ObtenerConfig()
    {
        using var conn = _db.Open();
        var cfg = LeerConfigPeriodos(conn);
        var semanaP1 = cfg.InicioP1 != null ? SemanaISO(cfg.InicioP1.Value) : (int?)null;
        var semanaP2 = cfg.InicioP2 != null ? SemanaISO(cfg.InicioP2.Value) : (int?)null;
        return Ok(new
        {
            p1_activo = cfg.InicioP1 != null,
            p2_activo = cfg.InicioP2 != null,
            inicio_p1 = cfg.InicioP1?.ToString("yyyy-MM-dd"),
            inicio_p2 = cfg.InicioP2?.ToString("yyyy-MM-dd"),
            semana_inicio_p1 = semanaP1,
            semana_inicio_p2 = semanaP2,
        });
    }

    // ── Distribución de equipos en semanas ───────────────────────────────
    private static void DistribuirEnSemanas(
        List<EquipoCalendario> equipos,
        DateTime fechaInicio,
        int year,
        int periodo,
        Dictionary<int, object> semanas,
        ConfigPeriodos config)
    {
        int semanaInicio = SemanaISO(fechaInicio);
        int totalEquipos = equipos.Count;
        if (totalEquipos == 0) return;

        // Calcular cuántas semanas necesitamos y cuántos equipos por semana
        // Regla: 24-30 equipos por semana obligatorio
        const int MIN_POR_SEMANA = 24;
        const int MAX_POR_SEMANA = 30;

        // Número óptimo de semanas
        int semanasNecesarias = (int)Math.Ceiling((double)totalEquipos / MAX_POR_SEMANA);
        int porSemana = (int)Math.Ceiling((double)totalEquipos / semanasNecesarias);

        // Ajustar para que quede entre 24 y 30
        if (porSemana < MIN_POR_SEMANA) porSemana = MIN_POR_SEMANA;
        if (porSemana > MAX_POR_SEMANA) porSemana = MAX_POR_SEMANA;

        int idx = 0;
        int semanaOffset = 0;

        while (idx < totalEquipos)
        {
            int semanaNum = semanaInicio + semanaOffset;
            // Wrap si pasa de 52/53
            int totalSemanasAnio = SemanasEnAnio(fechaInicio.Year);
            if (semanaNum > totalSemanasAnio)
            {
                semanaNum = semanaNum - totalSemanasAnio;
            }

            // Calcular equipos para esta semana (últimas semanas pueden tener menos)
            int restantes = totalEquipos - idx;
            int cantEsta = Math.Min(porSemana, restantes);

            // Si quedan pocos en la última semana, los unimos a la anterior si son < 24
            // Pero solo si ya hay equipos asignados en la semana anterior
            if (cantEsta < MIN_POR_SEMANA && semanaOffset > 0 && restantes < MIN_POR_SEMANA)
            {
                // Distribuir los restantes entre las semanas anteriores no supera el máximo
                // Si no es posible, los ponemos igual (excepción: última semana puede tener menos)
                cantEsta = restantes;
            }

            // Número de plazo = número de semana ISO
            string plazo = $"Plazo {semanaNum}";

            // Crear entradas para esta semana en el diccionario
            if (!semanas.ContainsKey(semanaNum))
            {
                semanas[semanaNum] = new SemanaData();
            }

            var semObj = (SemanaData)semanas[semanaNum];
            var lista = periodo == 1 ? semObj.P1 : semObj.P2;

            for (int i = 0; i < cantEsta && idx < totalEquipos; i++, idx++)
            {
                var eq = equipos[idx];
                DateTime? fechaReal = periodo == 1 ? eq.FechaP1 : eq.FechaP2;
                string? tecnico = periodo == 1 ? eq.TecnicoP1 : eq.TecnicoP2;

                lista.Add(new EquipoSemana
                {
                    Id = eq.Id,
                    IdEquipo = eq.IdEquipo,
                    Dispositivo = eq.Dispositivo,
                    Ubicacion = eq.Ubicacion,
                    Color = eq.Color,
                    Fecha = fechaReal?.ToString("yyyy-MM-dd"),
                    Plazo = plazo,
                    Tecnico = tecnico ?? "",
                    Realizado = fechaReal != null,
                });
            }

            semanaOffset++;
        }
    }

    // ── Config BD helpers ─────────────────────────────────────────────────
    private static ConfigPeriodos LeerConfigPeriodos(Npgsql.NpgsqlConnection conn)
    {
        var cfg = new ConfigPeriodos();
        try
        {
            // Crear tabla si no existe
            using var create = conn.CreateCommand();
            create.CommandText = """
                CREATE TABLE IF NOT EXISTS public.calendario_config (
                    clave VARCHAR(50) PRIMARY KEY,
                    valor TEXT NOT NULL,
                    actualizado_en TIMESTAMPTZ DEFAULT NOW()
                )
                """;
            create.ExecuteNonQuery();

            using var sel = conn.CreateCommand();
            sel.CommandText = "SELECT clave, valor FROM public.calendario_config WHERE clave IN ('inicio_p1','inicio_p2')";
            using var r = sel.ExecuteReader();
            while (r.Read())
            {
                var clave = r.GetString(0);
                var valor = r.GetString(1);
                if (DateTime.TryParse(valor, out var fecha))
                {
                    if (clave == "inicio_p1") cfg.InicioP1 = fecha;
                    else if (clave == "inicio_p2") cfg.InicioP2 = fecha;
                }
            }
        }
        catch { /* Si falla, períodos no configurados */ }
        return cfg;
    }

    private static void GuardarConfigPeriodo(Npgsql.NpgsqlConnection conn, int periodo, DateTime fecha)
    {
        // Asegurar que la tabla existe
        using var create = conn.CreateCommand();
        create.CommandText = """
            CREATE TABLE IF NOT EXISTS public.calendario_config (
                clave VARCHAR(50) PRIMARY KEY,
                valor TEXT NOT NULL,
                actualizado_en TIMESTAMPTZ DEFAULT NOW()
            )
            """;
        create.ExecuteNonQuery();

        string clave = periodo == 1 ? "inicio_p1" : "inicio_p2";
        using var upsert = conn.CreateCommand();
        upsert.CommandText = """
            INSERT INTO public.calendario_config (clave, valor, actualizado_en)
            VALUES (@c, @v, NOW())
            ON CONFLICT (clave) DO UPDATE SET valor = EXCLUDED.valor, actualizado_en = NOW()
            """;
        upsert.Parameters.AddWithValue("@c", clave);
        upsert.Parameters.AddWithValue("@v", fecha.ToString("yyyy-MM-dd"));
        upsert.ExecuteNonQuery();
    }

    private static void EliminarConfigPeriodo(Npgsql.NpgsqlConnection conn, int periodo)
    {
        string clave = periodo == 1 ? "inicio_p1" : "inicio_p2";
        using var del = conn.CreateCommand();
        del.CommandText = "DELETE FROM public.calendario_config WHERE clave = @c";
        del.Parameters.AddWithValue("@c", clave);
        del.ExecuteNonQuery();
    }

    // ── Helpers ───────────────────────────────────────────────────────────
    private static int SemanaISO(DateTime fecha)
    {
        var cal = System.Globalization.CultureInfo.InvariantCulture.Calendar;
        return cal.GetWeekOfYear(fecha,
            System.Globalization.CalendarWeekRule.FirstFourDayWeek,
            DayOfWeek.Monday);
    }

    private static int SemanasEnAnio(int year)
    {
        var d28 = new DateTime(year, 12, 28);
        return SemanaISO(d28) == 1 ? 52 : SemanaISO(d28);
    }

    // Stub: reemplazar con la lógica real de sesión del proyecto
    private UsuarioSesion? ObtenerUsuarioActual()
    {
        // Ejemplo: leer el claim de rol del JWT/cookie de sesión
        // Adaptar según cómo maneja ChiIT la autenticación
        var rolHeader = HttpContext.Request.Headers["X-User-Rol"].FirstOrDefault()
                        ?? HttpContext.Request.Cookies["user_rol"];
        var nombre = HttpContext.Request.Headers["X-User-Name"].FirstOrDefault()
                     ?? HttpContext.Request.Cookies["user_name"]
                     ?? "SISTEMA";
        if (string.IsNullOrEmpty(rolHeader)) return null;
        return new UsuarioSesion
        {
            Nombre = nombre,
            EsAdmin = rolHeader.Equals("ADMIN", StringComparison.OrdinalIgnoreCase)
        };
    }
}

// ── Modelos internos ──────────────────────────────────────────────────────
internal class EquipoCalendario
{
    public long Id { get; set; }
    public string IdEquipo { get; set; } = "";
    public string Dispositivo { get; set; } = "";
    public string Ubicacion { get; set; } = "";
    public string Color { get; set; } = "";
    public DateTime? FechaP1 { get; set; }
    public string TecnicoP1 { get; set; } = "";
    public DateTime? FechaP2 { get; set; }
    public string TecnicoP2 { get; set; } = "";
}

internal class EquipoSemana
{
    public long Id { get; set; }
    public string IdEquipo { get; set; } = "";
    public string Dispositivo { get; set; } = "";
    public string Ubicacion { get; set; } = "";
    public string Color { get; set; } = "";
    public string? Fecha { get; set; }
    public string? Plazo { get; set; }
    public string Tecnico { get; set; } = "";
    public bool Realizado { get; set; }
}

internal class SemanaData
{
    public List<EquipoSemana> P1 { get; set; } = new();
    public List<EquipoSemana> P2 { get; set; } = new();
}

internal class ConfigPeriodos
{
    public DateTime? InicioP1 { get; set; }
    public DateTime? InicioP2 { get; set; }
}

internal class UsuarioSesion
{
    public string Nombre { get; set; } = "";
    public bool EsAdmin { get; set; }
}