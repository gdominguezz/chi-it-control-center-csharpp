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
    private static string ClasificarTipo(string dispositivo)
    {
        var d = (dispositivo ?? "").Trim().ToUpperInvariant();
        if (d == "LAPTOP") return "laptops";
        if (d == "COMPUTADORA DE ESCRITORIO" || d == "UPS" || d == "IMPRESORA TERMICA")
            return "computo";
        return "otros";
    }

    // ── Plantas principales que comparten plazos proporcionalmente ────────
    // B1 y B2 se mezclan semana a semana en proporción a su tamaño.
    // El resto se asigna al final en bloques.
    private static readonly HashSet<string> PlantasPrincipales =
        new(StringComparer.OrdinalIgnoreCase) { "B1", "B2" };

    // ═════════════════════════════════════════════════════════════════════
    // GET /CALENDARIO/API?anio=2025
    // ═════════════════════════════════════════════════════════════════════
    [HttpGet("CALENDARIO/API")]
    public IActionResult ObtenerCalendario([FromQuery] int? anio)
    {
        int year = anio ?? DateTime.Now.Year;

        using var conn = _db.Open();
        var config = LeerConfig(conn);

        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT
                COALESCE(planta, 'Sin planta') AS planta,
                id, id_equipo, nombre_dispositivo, ubicacion, categoria_color,
                fecha_realizacion   AS fecha_p1,
                realizado_por       AS tecnico_p1,
                fecha_realizacion_p2 AS fecha_p2,
                realizado_por_p2    AS tecnico_p2,
                plazo               AS plazo_p1,
                plazo_p2            AS plazo_p2
            FROM public.mantenimientos_preventivos
            ORDER BY planta, nombre_dispositivo, id_equipo
            """;

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
                PlazoP1 = r.IsDBNull(10) ? null : r.GetString(10),
                PlazoP2 = r.IsDBNull(11) ? null : r.GetString(11),
            });
        }
        r.Close();

        // Ordenar plantas: principales primero (B1, B2), resto por cantidad desc
        List<string> OrdenarPlantas(string tipo) =>
            datos.Keys
                .OrderByDescending(p => PlantasPrincipales.Contains(p) ? 1 : 0)
                .ThenByDescending(p => datos[p][tipo].Count)
                .ThenBy(p => p)
                .ToList();

        var plantasLaptops = OrdenarPlantas("laptops");
        var plantasComputo = OrdenarPlantas("computo");

        var calLaptops = ConstruirCalendario(datos, "laptops", config, plantasLaptops);
        var calComputo = ConstruirCalendario(datos, "computo", config, plantasComputo);

        return Ok(new
        {
            anio = year,
            config = SerializarConfig(config),
            laptops = new { plantas_orden = plantasLaptops, calendario = calLaptops },
            computo = new { plantas_orden = plantasComputo, calendario = calComputo },
        });
    }

    // ═════════════════════════════════════════════════════════════════════
    // Construir calendario para un tipo (laptops | computo)
    //
    // Regla de distribución:
    //   1. B1 y B2 se mezclan proporcionalmente semana a semana.
    //      Cada semana recibe: round(30 * proporcion_planta) equipos de cada una.
    //   2. Las demás plantas van al final, una tras otra, en bloques de ~30.
    //
    // Resultado: dict planta → semana → { p1:[], p2:[] }
    //   Cada equipo tiene el plazo (número de semana) que le fue asignado.
    // ═════════════════════════════════════════════════════════════════════
    private static Dictionary<string, Dictionary<int, SemanaData>> ConstruirCalendario(
        Dictionary<string, Dictionary<string, List<EquipoCalendario>>> datos,
        string tipo,
        ConfigPeriodos config,
        List<string> plantasOrden)
    {
        DateTime? inicioP1 = tipo == "laptops" ? config.LaptopsP1 : config.ComputoP1;
        DateTime? inicioP2 = tipo == "laptops" ? config.LaptopsP2 : config.ComputoP2;

        // resultado final: planta → semana → SemanaData
        var resultado = new Dictionary<string, Dictionary<int, SemanaData>>();
        foreach (var p in plantasOrden) resultado[p] = new Dictionary<int, SemanaData>();

        foreach (int periodo in new[] { 1, 2 })
        {
            DateTime? inicio = periodo == 1 ? inicioP1 : inicioP2;
            if (inicio == null) continue;

            int semanaBase = SemanaISO(inicio.Value);
            int totalSemsAnio = SemanasEnAnio(inicio.Value.Year);
            const int POR_SEMANA = 30; // total equipos por semana entre todas las principales

            // Separar plantas principales de las demás
            var principales = plantasOrden
                .Where(p => PlantasPrincipales.Contains(p) && datos.ContainsKey(p))
                .ToList();
            var secundarias = plantasOrden
                .Where(p => !PlantasPrincipales.Contains(p) && datos.ContainsKey(p))
                .ToList();

            // Ordenar equipos de cada planta
            var equiposPorPlanta = new Dictionary<string, List<EquipoCalendario>>();
            foreach (var planta in plantasOrden)
            {
                if (!datos.ContainsKey(planta)) continue;
                equiposPorPlanta[planta] = datos[planta]
                    .GetValueOrDefault(tipo, new())
                    .OrderBy(e => e.Dispositivo, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(e => e.IdEquipo, StringComparer.OrdinalIgnoreCase)
                    .ToList();
            }

            // ── FASE 1: Distribuir B1 y B2 proporcionalmente ──────────────
            // Calcular cuántos equipos de cada principal por semana (proporcional)
            int totalPrincipales = principales.Sum(p =>
                equiposPorPlanta.GetValueOrDefault(p)?.Count ?? 0);

            // Índice de avance por planta
            var idx = new Dictionary<string, int>();
            foreach (var p in principales) idx[p] = 0;

            // Cuántos equipos por semana le tocan a cada planta principal
            var porSemana = new Dictionary<string, int>();
            foreach (var p in principales)
            {
                int cnt = equiposPorPlanta.GetValueOrDefault(p)?.Count ?? 0;
                porSemana[p] = totalPrincipales > 0
                    ? (int)Math.Round((double)cnt / totalPrincipales * POR_SEMANA)
                    : 0;
                // Mínimo 1 si tiene equipos
                if (porSemana[p] == 0 && cnt > 0) porSemana[p] = 1;
            }

            int semOffset = 0;

            // Seguir hasta que todas las principales terminen
            bool HayPendientes() => principales.Any(p =>
                idx.GetValueOrDefault(p) < (equiposPorPlanta.GetValueOrDefault(p)?.Count ?? 0));

            while (HayPendientes())
            {
                int semRaw = semanaBase + semOffset;
                int semNum = ((semRaw - 1) % totalSemsAnio) + 1;
                semOffset++;

                foreach (var planta in principales)
                {
                    var lista = equiposPorPlanta.GetValueOrDefault(planta);
                    if (lista == null || lista.Count == 0) continue;

                    int pSem = porSemana[planta];
                    int start = idx[planta];
                    int end = Math.Min(start + pSem, lista.Count);
                    if (start >= lista.Count) continue;

                    if (!resultado[planta].ContainsKey(semNum))
                        resultado[planta][semNum] = new SemanaData();

                    var semData = resultado[planta][semNum];
                    var destLista = periodo == 1 ? semData.P1 : semData.P2;

                    for (int i = start; i < end; i++)
                    {
                        var eq = lista[i];
                        var fechaReal = periodo == 1 ? eq.FechaP1 : eq.FechaP2;
                        var tecnico = (periodo == 1 ? eq.TecnicoP1 : eq.TecnicoP2) ?? "";
                        destLista.Add(new EquipoSemana
                        {
                            Id = eq.Id,
                            IdEquipo = eq.IdEquipo,
                            Dispositivo = eq.Dispositivo,
                            Ubicacion = eq.Ubicacion,
                            Color = eq.Color,
                            Planta = planta,
                            Fecha = fechaReal?.ToString("yyyy-MM-dd"),
                            PlazoSemana = semNum,
                            Tecnico = tecnico,
                            Realizado = fechaReal != null,
                        });
                    }

                    idx[planta] = end;
                }
            }

            // ── FASE 2: Plantas secundarias al final en bloques de ~30 ────
            foreach (var planta in secundarias)
            {
                var lista = equiposPorPlanta.GetValueOrDefault(planta);
                if (lista == null || lista.Count == 0) continue;

                int i = 0;
                while (i < lista.Count)
                {
                    int semRaw = semanaBase + semOffset;
                    int semNum = ((semRaw - 1) % totalSemsAnio) + 1;

                    if (!resultado[planta].ContainsKey(semNum))
                        resultado[planta][semNum] = new SemanaData();

                    var semData = resultado[planta][semNum];
                    var destLista = periodo == 1 ? semData.P1 : semData.P2;

                    int cant = Math.Min(POR_SEMANA, lista.Count - i);
                    for (int j = 0; j < cant; j++, i++)
                    {
                        var eq = lista[i];
                        var fechaReal = periodo == 1 ? eq.FechaP1 : eq.FechaP2;
                        var tecnico = (periodo == 1 ? eq.TecnicoP1 : eq.TecnicoP2) ?? "";
                        destLista.Add(new EquipoSemana
                        {
                            Id = eq.Id,
                            IdEquipo = eq.IdEquipo,
                            Dispositivo = eq.Dispositivo,
                            Ubicacion = eq.Ubicacion,
                            Color = eq.Color,
                            Planta = planta,
                            Fecha = fechaReal?.ToString("yyyy-MM-dd"),
                            PlazoSemana = semNum,
                            Tecnico = tecnico,
                            Realizado = fechaReal != null,
                        });
                    }
                    semOffset++;
                }
            }
        }

        return resultado;
    }

    // ═════════════════════════════════════════════════════════════════════
    // POST /CALENDARIO/PERIODO
    // Body: { "tipo": "laptops"|"computo", "periodo": 1|2, "fecha": "2025-01-06" }
    // Guarda la fecha de inicio Y escribe el campo plazo/plazo_p2 en cada
    // equipo de la BD con la semana que le fue asignada.
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

        // Validar que P2 empiece en semana 27 o posterior (segundo semestre)
        if (periodo == 2)
        {
            int semanaFecha = SemanaISO(fecha);
            if (semanaFecha < 27)
                return BadRequest(new
                {
                    error = $"El Período 2 debe iniciar en semana 27 o posterior (julio en adelante). " +
                            $"La fecha seleccionada cae en semana {semanaFecha}."
                });
        }

        using var conn = _db.Open();
        GuardarConfig(conn, tipo, periodo, fecha);

        // Leer todos los equipos del tipo para calcular y escribir plazos
        var config = LeerConfig(conn);

        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT
                COALESCE(planta,'Sin planta') AS planta,
                id, id_equipo, nombre_dispositivo, ubicacion, categoria_color,
                fecha_realizacion, realizado_por,
                fecha_realizacion_p2, realizado_por_p2,
                plazo, plazo_p2
            FROM public.mantenimientos_preventivos
            ORDER BY planta, nombre_dispositivo, id_equipo
            """;

        var datos = new Dictionary<string, Dictionary<string, List<EquipoCalendario>>>();
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            var planta = r.GetString(0);
            var disp = r.IsDBNull(3) ? "" : r.GetString(3);
            var t = ClasificarTipo(disp);
            if (t == "otros") continue;

            if (!datos.ContainsKey(planta))
                datos[planta] = new() { ["laptops"] = new(), ["computo"] = new() };

            datos[planta][t].Add(new EquipoCalendario
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
                PlazoP1 = r.IsDBNull(10) ? null : r.GetString(10),
                PlazoP2 = r.IsDBNull(11) ? null : r.GetString(11),
            });
        }
        r.Close();

        List<string> OrdenarPlantas() =>
            datos.Keys
                .OrderByDescending(p => PlantasPrincipales.Contains(p) ? 1 : 0)
                .ThenByDescending(p => datos[p][tipo].Count)
                .ThenBy(p => p)
                .ToList();

        var calendarioPorPlanta = ConstruirCalendario(datos, tipo, config, OrdenarPlantas());

        // Escribir el plazo calculado de vuelta a cada equipo en la BD
        string campoFecha = periodo == 1 ? "plazo" : "plazo_p2";
        int actualizados = 0;

        foreach (var (planta, semanas) in calendarioPorPlanta)
        {
            foreach (var (semNum, semData) in semanas)
            {
                var listaEquipos = periodo == 1 ? semData.P1 : semData.P2;
                foreach (var eq in listaEquipos)
                {
                    // Calcular fecha del lunes de esa semana como valor de plazo
                    var fechaLunes = FechaLunesDeSemana(fecha.Year, semNum);
                    string plazoStr = fechaLunes.ToString("yyyy-MM-dd");

                    using var upd = conn.CreateCommand();
                    upd.CommandText = $"""
                        UPDATE public.mantenimientos_preventivos
                        SET {campoFecha} = @p
                        WHERE id = @id
                        """;
                    upd.Parameters.AddWithValue("@p", plazoStr);
                    upd.Parameters.AddWithValue("@id", eq.Id);
                    upd.ExecuteNonQuery();
                    actualizados++;
                }
            }
        }

        int semana = SemanaISO(fecha);
        return Ok(new
        {
            ok = true,
            tipo,
            periodo,
            fecha = fecha.ToString("yyyy-MM-dd"),
            semana,
            actualizados,
        });
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

        // Limpiar el campo plazo de los equipos del tipo
        string campo = periodo == 1 ? "plazo" : "plazo_p2";
        using var cmd = conn.CreateCommand();
        // Solo equipos del tipo indicado
        string filtro = tipo == "laptops"
            ? "nombre_dispositivo ILIKE 'LAPTOP'"
            : "nombre_dispositivo NOT ILIKE 'LAPTOP'";
        cmd.CommandText = $"UPDATE public.mantenimientos_preventivos SET {campo} = NULL WHERE {filtro}";
        cmd.ExecuteNonQuery();

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

    // ── Serializar config ─────────────────────────────────────────────────
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
        catch { }
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

    // ── Fecha del lunes de una semana ISO dada ────────────────────────────
    private static DateTime FechaLunesDeSemana(int anio, int semana)
    {
        // ISO 8601: semana 1 es la que contiene el primer jueves del año
        var ene4 = new DateTime(anio, 1, 4);
        int diaSemana = (int)ene4.DayOfWeek;
        if (diaSemana == 0) diaSemana = 7; // domingo = 7
        var lunesSem1 = ene4.AddDays(1 - diaSemana);
        return lunesSem1.AddDays((semana - 1) * 7);
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
        var rol = Request.Cookies["rol"]
               ?? Request.Headers["X-Rol"].FirstOrDefault()
               ?? Request.Headers["X-User-Rol"].FirstOrDefault();

        var nombre = Request.Cookies["usuario"]
                  ?? Request.Headers["X-Usuario"].FirstOrDefault()
                  ?? Request.Headers["X-User-Name"].FirstOrDefault()
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
    public string? PlazoP1 { get; set; }
    public string? PlazoP2 { get; set; }
}

internal class EquipoSemana
{
    public long Id { get; set; }
    public string IdEquipo { get; set; } = "";
    public string Dispositivo { get; set; } = "";
    public string Ubicacion { get; set; } = "";
    public string Color { get; set; } = "";
    public string Planta { get; set; } = "";
    public string? Fecha { get; set; }
    public int PlazoSemana { get; set; }  // número de semana ISO asignado
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