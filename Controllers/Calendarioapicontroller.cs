using ChiIT.Data;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace ChiIT.Controllers;

[ApiController]
public class CalendarioApiController : ControllerBase
{
    private readonly DbConnectionPool _db;
    public CalendarioApiController(DbConnectionPool db) => _db = db;

    // ── Clasificación de dispositivos ────────────────────────────────────
    // LAPTOPS          → LAPTOP
    // EQUIPOS DE CÓMPUTO → COMPUTADORA DE ESCRITORIO | UPS | IMPRESORA TERMICA
    private static string ClasificarTipo(string dispositivo)
    {
        var d = (dispositivo ?? "").Trim().ToUpperInvariant();
        if (d == "LAPTOP") return "laptops";
        if (d == "COMPUTADORA DE ESCRITORIO" || d == "UPS" || d == "IMPRESORA TERMICA")
            return "computo";
        return "otros";
    }

    // ═════════════════════════════════════════════════════════════════════
    // GET /CALENDARIO/API?anio=2025
    // Respuesta:
    //   {
    //     anio,
    //     config: { laptops: { p1_activo, p2_activo, ... }, computo: { ... } },
    //     laptops: { plantas_orden: [], calendario: { planta: { semana: {p1:[],p2:[]} } } },
    //     computo:  { plantas_orden: [], calendario: { ... } }
    //   }
    // ═════════════════════════════════════════════════════════════════════
    [HttpGet("CALENDARIO/API")]
    public IActionResult ObtenerCalendario([FromQuery] int? anio)
    {
        int year = anio ?? DateTime.Now.Year;

        using var conn = _db.Open();
        var config = LeerConfig(conn);

        // ── Leer todos los equipos y agrupar por planta+tipo ──
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT
                COALESCE(planta, 'Sin planta') AS planta,
                id,
                id_equipo,
                nombre_dispositivo,
                ubicacion,
                categoria_color,
                fecha_realizacion              AS fecha_p1,
                realizado_por                  AS tecnico_p1,
                fecha_realizacion_p2           AS fecha_p2,
                realizado_por_p2               AS tecnico_p2
            FROM public.mantenimientos_preventivos
            ORDER BY planta, nombre_dispositivo, id_equipo
            """;

        // planta → tipo → lista
        var datos = new Dictionary<string, Dictionary<string, List<EquipoCalendario>>>();

        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            var planta = r.GetString(0);
            var disp = r.IsDBNull(3) ? "" : r.GetString(3);
            var tipo = ClasificarTipo(disp);
            if (tipo == "otros") continue;

            if (!datos.ContainsKey(planta))
                datos[planta] = new Dictionary<string, List<EquipoCalendario>>
                { ["laptops"] = new(), ["computo"] = new() };

            datos[planta][tipo].Add(new EquipoCalendario
            {
                Id = r.GetInt64(1),
                IdEquipo = r.IsDBNull(2) ? "" : r.GetString(2),
                Dispositivo = disp,
                Ubicacion = r.IsDBNull(4) ? "" : r.GetString(4),
                Color = r.IsDBNull(5) ? "" : r.GetString(5),
                FechaP1 = r.IsDBNull(6) ? null : r.GetDateTime(6),
                TecnicoP1 = r.IsDBNull(7) ? "" : r.GetString(7),
                FechaP2 = r.IsDBNull(8) ? null : r.GetDateTime(8),
                TecnicoP2 = r.IsDBNull(9) ? "" : r.GetString(9),
            });
        }
        r.Close();

        // ── Ordenar plantas: mayor cantidad de equipos primero ──
        var plantasOrdenLaptops = datos.Keys
            .OrderByDescending(p => datos[p]["laptops"].Count)
            .ThenBy(p => p)
            .ToList();

        var plantasOrdenComputo = datos.Keys
            .OrderByDescending(p => datos[p]["computo"].Count)
            .ThenBy(p => p)
            .ToList();

        // ── Construir calendarios ──
        var calLaptops = ConstruirCalendario(datos, "laptops", config, plantasOrdenLaptops);
        var calComputo = ConstruirCalendario(datos, "computo", config, plantasOrdenComputo);

        return Ok(new
        {
            anio = year,
            config = SerializarConfig(config),
            laptops = new { plantas_orden = plantasOrdenLaptops, calendario = calLaptops },
            computo = new { plantas_orden = plantasOrdenComputo, calendario = calComputo },
        });
    }

    // ── Construir el calendario completo de un tipo ───────────────────────
    // Las plantas se distribuyen en orden (más equipos primero).
    // La distribución de semanas es CONTINUA entre plantas:
    //   B1 ocupa Plazo 8,9,10 → B2 arranca en Plazo 11 → Bodega en Plazo 14 …
    private static Dictionary<string, Dictionary<int, SemanaData>> ConstruirCalendario(
        Dictionary<string, Dictionary<string, List<EquipoCalendario>>> datos,
        string tipo,
        ConfigPeriodos config,
        List<string> plantasOrden)
    {
        var resultado = new Dictionary<string, Dictionary<int, SemanaData>>();

        DateTime? inicioP1 = tipo == "laptops" ? config.LaptopsP1 : config.ComputoP1;
        DateTime? inicioP2 = tipo == "laptops" ? config.LaptopsP2 : config.ComputoP2;

        int offsetP1 = 0; // semanas acumuladas entre plantas (P1)
        int offsetP2 = 0; // semanas acumuladas entre plantas (P2)

        foreach (var planta in plantasOrden)
        {
            if (!datos.ContainsKey(planta)) continue;

            var equipos = datos[planta].GetValueOrDefault(tipo) ?? new();
            var ordenados = equipos
                .OrderBy(e => e.Dispositivo, StringComparer.OrdinalIgnoreCase)
                .ThenBy(e => e.IdEquipo, StringComparer.OrdinalIgnoreCase)
                .ToList();

            var semanas = new Dictionary<int, SemanaData>();

            if (inicioP1 != null && ordenados.Count > 0)
                offsetP1 = DistribuirEnSemanas(ordenados, inicioP1.Value, 1, semanas, offsetP1);

            if (inicioP2 != null && ordenados.Count > 0)
                offsetP2 = DistribuirEnSemanas(ordenados, inicioP2.Value, 2, semanas, offsetP2);

            resultado[planta] = semanas;
        }

        return resultado;
    }

    // ── Distribuir equipos de UNA planta en semanas ───────────────────────
    // Devuelve el offset acumulado para que la siguiente planta continúe.
    private static int DistribuirEnSemanas(
        List<EquipoCalendario> equipos,
        DateTime fechaInicio,
        int periodo,
        Dictionary<int, SemanaData> semanas,
        int offsetInicial)
    {
        const int MIN = 24, MAX = 30;
        int total = equipos.Count;
        if (total == 0) return offsetInicial;

        // Calcular tamaño de lote (24-30 por semana)
        int semanasNec = (int)Math.Ceiling((double)total / MAX);
        int porSemana = (int)Math.Ceiling((double)total / semanasNec);
        porSemana = Math.Max(MIN, Math.Min(MAX, porSemana));

        int semanaBase = SemanaISO(fechaInicio);
        int totalSemsAnio = SemanasEnAnio(fechaInicio.Year);

        int idx = 0, offset = offsetInicial;

        while (idx < total)
        {
            int semRaw = semanaBase + offset;
            // Wraparound anual
            int semNum = ((semRaw - 1) % totalSemsAnio) + 1;

            int restantes = total - idx;
            int cant = Math.Min(porSemana, restantes);

            // Si la última tanda es < 24, la dejamos igual (excepción permitida al final)
            if (cant < MIN && restantes <= cant)
                cant = restantes;

            string plazoLabel = $"Plazo {semNum}";

            if (!semanas.ContainsKey(semNum))
                semanas[semNum] = new SemanaData();

            var lista = periodo == 1
                ? semanas[semNum].P1
                : semanas[semNum].P2;

            for (int i = 0; i < cant && idx < total; i++, idx++)
            {
                var eq = equipos[idx];
                var fechaReal = periodo == 1 ? eq.FechaP1 : eq.FechaP2;
                var tecnico = (periodo == 1 ? eq.TecnicoP1 : eq.TecnicoP2) ?? "";

                lista.Add(new EquipoSemana
                {
                    Id = eq.Id,
                    IdEquipo = eq.IdEquipo,
                    Dispositivo = eq.Dispositivo,
                    Ubicacion = eq.Ubicacion,
                    Color = eq.Color,
                    Fecha = fechaReal?.ToString("yyyy-MM-dd"),
                    Plazo = plazoLabel,
                    Tecnico = tecnico,
                    Realizado = fechaReal != null,
                });
            }

            offset++;
        }

        return offset;
    }

    // ═════════════════════════════════════════════════════════════════════
    // POST /CALENDARIO/PERIODO
    // Body: { "tipo": "laptops"|"computo", "periodo": 1|2, "fecha": "2025-02-03" }
    // Solo ADMIN
    // ═════════════════════════════════════════════════════════════════════
    [HttpPost("CALENDARIO/PERIODO")]
    public IActionResult ConfigurarPeriodo([FromBody] JsonElement body)
    {
        var usuario = ObtenerUsuarioActual();
        if (usuario == null || !usuario.EsAdmin)
            return Forbid();

        if (!body.TryGetProperty("tipo", out var tipoEl) ||
            !body.TryGetProperty("periodo", out var periodoEl) ||
            !body.TryGetProperty("fecha", out var fechaEl))
            return BadRequest(new { error = "Se requieren 'tipo', 'periodo' y 'fecha'" });

        string tipo = tipoEl.GetString() ?? "";
        int periodo = periodoEl.GetInt32();
        if (tipo != "laptops" && tipo != "computo")
            return BadRequest(new { error = "Tipo inválido" });
        if (!DateTime.TryParse(fechaEl.GetString(), out var fecha))
            return BadRequest(new { error = "Fecha inválida" });

        using var conn = _db.Open();
        GuardarConfig(conn, tipo, periodo, fecha);

        int semana = SemanaISO(fecha);
        return Ok(new { ok = true, tipo, periodo, fecha = fecha.ToString("yyyy-MM-dd"), semana });
    }

    // ── DELETE /CALENDARIO/PERIODO/{tipo}/{periodo} ───────────────────────
    [HttpDelete("CALENDARIO/PERIODO/{tipo}/{periodo}")]
    public IActionResult ResetearPeriodo(string tipo, int periodo)
    {
        var usuario = ObtenerUsuarioActual();
        if (usuario == null || !usuario.EsAdmin) return Forbid();
        if (tipo != "laptops" && tipo != "computo") return BadRequest();

        using var conn = _db.Open();
        EliminarConfig(conn, tipo, periodo);
        return Ok(new { ok = true });
    }

    // ── GET /CALENDARIO/CONFIG ────────────────────────────────────────────
    [HttpGet("CALENDARIO/CONFIG")]
    public IActionResult ObtenerConfig()
    {
        using var conn = _db.Open();
        return Ok(SerializarConfig(LeerConfig(conn)));
    }

    // ── GET /CALENDARIO/PLANTAS ───────────────────────────────────────────
    [HttpGet("CALENDARIO/PLANTAS")]
    public IActionResult ObtenerPlantas()
    {
        using var conn = _db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT DISTINCT COALESCE(planta,'Sin planta') AS planta
            FROM public.mantenimientos_preventivos ORDER BY planta
            """;
        var lista = new List<string>();
        using var r = cmd.ExecuteReader();
        while (r.Read()) lista.Add(r.GetString(0));
        return Ok(new { plantas = lista });
    }

    // ── Serializar config para el frontend ───────────────────────────────
    private static object SerializarConfig(ConfigPeriodos cfg) => new
    {
        laptops = new
        {
            p1_activo = cfg.LaptopsP1 != null,
            p2_activo = cfg.LaptopsP2 != null,
            inicio_p1 = cfg.LaptopsP1?.ToString("yyyy-MM-dd"),
            inicio_p2 = cfg.LaptopsP2?.ToString("yyyy-MM-dd"),
            semana_inicio_p1 = cfg.LaptopsP1 != null ? SemanaISO(cfg.LaptopsP1.Value) : (int?)null,
            semana_inicio_p2 = cfg.LaptopsP2 != null ? SemanaISO(cfg.LaptopsP2.Value) : (int?)null,
        },
        computo = new
        {
            p1_activo = cfg.ComputoP1 != null,
            p2_activo = cfg.ComputoP2 != null,
            inicio_p1 = cfg.ComputoP1?.ToString("yyyy-MM-dd"),
            inicio_p2 = cfg.ComputoP2?.ToString("yyyy-MM-dd"),
            semana_inicio_p1 = cfg.ComputoP1 != null ? SemanaISO(cfg.ComputoP1.Value) : (int?)null,
            semana_inicio_p2 = cfg.ComputoP2 != null ? SemanaISO(cfg.ComputoP2.Value) : (int?)null,
        }
    };

    // ── BD helpers ────────────────────────────────────────────────────────
    private static void AsegurarTabla(Npgsql.NpgsqlConnection conn)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS public.calendario_config (
                clave          VARCHAR(50) PRIMARY KEY,
                valor          TEXT        NOT NULL,
                actualizado_en TIMESTAMPTZ DEFAULT NOW()
            )
            """;
        cmd.ExecuteNonQuery();
    }

    private static ConfigPeriodos LeerConfig(Npgsql.NpgsqlConnection conn)
    {
        var cfg = new ConfigPeriodos();
        try
        {
            AsegurarTabla(conn);
            using var sel = conn.CreateCommand();
            sel.CommandText = """
                SELECT clave, valor FROM public.calendario_config
                WHERE clave IN ('laptops_p1','laptops_p2','computo_p1','computo_p2')
                """;
            using var r = sel.ExecuteReader();
            while (r.Read())
            {
                if (!DateTime.TryParse(r.GetString(1), out var d)) continue;
                switch (r.GetString(0))
                {
                    case "laptops_p1": cfg.LaptopsP1 = d; break;
                    case "laptops_p2": cfg.LaptopsP2 = d; break;
                    case "computo_p1": cfg.ComputoP1 = d; break;
                    case "computo_p2": cfg.ComputoP2 = d; break;
                }
            }
        }
        catch { /* tabla aún no existe o sin datos */ }
        return cfg;
    }

    private static void GuardarConfig(Npgsql.NpgsqlConnection conn, string tipo, int periodo, DateTime fecha)
    {
        AsegurarTabla(conn);
        using var upsert = conn.CreateCommand();
        upsert.CommandText = """
            INSERT INTO public.calendario_config (clave, valor, actualizado_en)
            VALUES (@c, @v, NOW())
            ON CONFLICT (clave) DO UPDATE SET valor = EXCLUDED.valor, actualizado_en = NOW()
            """;
        upsert.Parameters.AddWithValue("@c", $"{tipo}_p{periodo}");
        upsert.Parameters.AddWithValue("@v", fecha.ToString("yyyy-MM-dd"));
        upsert.ExecuteNonQuery();
    }

    private static void EliminarConfig(Npgsql.NpgsqlConnection conn, string tipo, int periodo)
    {
        using var del = conn.CreateCommand();
        del.CommandText = "DELETE FROM public.calendario_config WHERE clave = @c";
        del.Parameters.AddWithValue("@c", $"{tipo}_p{periodo}");
        del.ExecuteNonQuery();
    }

    // ── Semana ISO ────────────────────────────────────────────────────────
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

    // ── Sesión ────────────────────────────────────────────────────────────
    private UsuarioSesion? ObtenerUsuarioActual()
    {
        var rol = HttpContext.Request.Headers["X-User-Rol"].FirstOrDefault()
               ?? HttpContext.Request.Cookies["user_rol"];
        var nombre = HttpContext.Request.Headers["X-User-Name"].FirstOrDefault()
                  ?? HttpContext.Request.Cookies["user_name"]
                  ?? "SISTEMA";
        if (string.IsNullOrEmpty(rol)) return null;
        return new UsuarioSesion
        {
            Nombre = nombre,
            EsAdmin = rol.Equals("ADMIN", StringComparison.OrdinalIgnoreCase)
        };
    }
}

// ── Modelos ───────────────────────────────────────────────────────────────
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
    public DateTime? LaptopsP1 { get; set; }
    public DateTime? LaptopsP2 { get; set; }
    public DateTime? ComputoP1 { get; set; }
    public DateTime? ComputoP2 { get; set; }
}

internal class UsuarioSesion
{
    public string Nombre { get; set; } = "";
    public bool EsAdmin { get; set; }
}