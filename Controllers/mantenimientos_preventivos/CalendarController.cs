using ChiIT.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System.Text.Json;
using System.Text.Json.Serialization;
namespace ChiIT.Controllers;

/// <summary>
/// CalendarioController — gestiona el estado del calendario de mantenimientos preventivos.
/// 
/// Plantas soportadas: B1, B2, SATELITE, MIXING, BODEGA
/// Periodos por planta: P1 y P2 (P2 solo se puede iniciar después de "Terminar P1" de la misma planta)
///
/// Reglas de semana mínima:
///   - SATELITE: no puede iniciarse antes de la semana 17
///   - MIXING (P1 y P2): no puede iniciarse antes de la semana 40
///
/// Activación automática: al activar SATELITE se activan MIXING y BODEGA automáticamente.
/// Distribución de equipos:
///   - B1, B2, SATELITE → total_equipos / 24 semanas
///   - MIXING → semana fija 25
///   - BODEGA  → semana fija 26
/// </summary>
[ApiController]
public class CalendarioController : ControllerBase
{
    private readonly DbConnectionPool _db;

    // ── Nombres exactos de planta en la tabla mantenimientos_preventivos ──
    private static readonly Dictionary<string, string> PlantaNombreDB = new(StringComparer.OrdinalIgnoreCase)
    {
        ["B1"] = "B1",
        ["B2"] = "B2",
        ["SATELITE"] = "PLANTA SATELITE",
        ["MIXING"] = "PLANTA MIXING",
        ["BODEGA"] = "BODEGA",
    };

    // Plantas que el usuario puede activar manualmente
    private static readonly HashSet<string> PlantasActivacionManual =
        new(StringComparer.OrdinalIgnoreCase) { "B1", "B2", "SATELITE" };

    // Plantas auto-activadas cuando se activa SATELITE
    private static readonly string[] PlantasAutoSatelite = ["MIXING", "BODEGA"];

    // Semanas mínimas de inicio
    private const int SemanaMinimaSatelite = 17;
    private const int SemanaMinimaMixing = 40;

    // Semanas fijas de MIXING y BODEGA (relativas dentro del calendario)
    private const int SemanaFijaMixing = 25;
    private const int SemanaFijaBodega = 26;

    // Total de semanas de distribución para B1/B2/SATELITE
    private const int SemanasDistribucion = 24;

    public CalendarioController(DbConnectionPool db) => _db = db;

    // ══════════════════════════════════════════════════════════════════════
    // GET /CALENDARIO/ESTADO
    // Devuelve el estado completo del calendario (todas las plantas, P1 y P2).
    // ══════════════════════════════════════════════════════════════════════
    [HttpGet("CALENDARIO/ESTADO")]
    public IActionResult ObtenerEstado()
    {
        using var conn = _db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT planta_key, periodo, semana_inicio, anio_inicio, terminado,
                   generado, creado_en, terminado_en
            FROM calendario_estado
            ORDER BY planta_key, periodo
            """;

        var estado = new Dictionary<string, object>();
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            var key = r.GetString(0);  // e.g. "B1", "SATELITE"
            var per = r.GetInt32(1);   // 1 o 2
            var pkey = $"{key}_P{per}";
            estado[pkey] = new
            {
                planta = key,
                periodo = per,
                semana_inicio = r.IsDBNull(2) ? (int?)null : r.GetInt32(2),
                anio_inicio = r.IsDBNull(3) ? (int?)null : r.GetInt32(3),
                terminado = !r.IsDBNull(4) && r.GetBoolean(4),
                generado = !r.IsDBNull(5) && r.GetBoolean(5),
                creado_en = r.IsDBNull(6) ? null : r.GetDateTime(6).ToString("o"),
                terminado_en = r.IsDBNull(7) ? null : r.GetDateTime(7).ToString("o"),
            };
        }
        return Ok(new { estado });
    }

    // ══════════════════════════════════════════════════════════════════════
    // POST /CALENDARIO/GENERAR
    // Genera (o regenera) el calendario de una planta y periodo.
    // Body: { planta, periodo, semana_inicio, anio_inicio }
    // ══════════════════════════════════════════════════════════════════════
    [HttpPost("CALENDARIO/GENERAR")]
    public IActionResult GenerarCalendario([FromBody] GenerarCalendarioRequest req)
    {
        var planta = (req.Planta ?? "").Trim().ToUpper();
        var periodo = req.Periodo;

        // ── Validaciones básicas ──────────────────────────────────────────
        if (!PlantaNombreDB.ContainsKey(planta))
            return Ok(new { ok = 0, error = $"Planta desconocida: {req.Planta}" });

        if (periodo != 1 && periodo != 2)
            return Ok(new { ok = 0, error = "Periodo debe ser 1 o 2" });

        if (req.SemanaInicio < 1 || req.SemanaInicio > 52)
            return Ok(new { ok = 0, error = "Semana de inicio debe estar entre 1 y 52" });

        // ── Plantas de activación manual ──────────────────────────────────
        if (!PlantasActivacionManual.Contains(planta))
            return Ok(new { ok = 0, error = $"La planta {planta} se activa automáticamente" });

        // ── Restricción semana SATELITE ───────────────────────────────────
        if (planta == "SATELITE" && req.SemanaInicio < SemanaMinimaSatelite)
            return Ok(new { ok = 0, error = $"Planta Satélite solo puede iniciar desde la semana {SemanaMinimaSatelite}." });

        // ── Restricción semana MIXING (aplica también si se activa via SATELITE) ──
        // (Para MIXING manual no procede porque es auto-activada, pero validamos por si acaso)
        if (planta == "MIXING" && req.SemanaInicio < SemanaMinimaMixing)
            return Ok(new { ok = 0, error = $"Planta Mixing solo puede iniciar desde la semana {SemanaMinimaMixing}." });

        // ── Si es P2, verificar que P1 de la MISMA planta esté terminado ─
        if (periodo == 2)
        {
            var p1Terminado = ObtenerEstadoPlanta(planta, 1);
            if (p1Terminado == null || !p1Terminado.Terminado)
                return Ok(new { ok = 0, error = $"Debes terminar el Período 1 de {planta} antes de generar el Período 2." });
        }

        using var conn = _db.Open();

        // ── Calcular distribución de equipos ──────────────────────────────
        var distribucion = CalcularDistribucion(conn, planta, periodo, req.SemanaInicio, req.AnioInicio);

        // ── Guardar/actualizar estado en BD ───────────────────────────────
        UpsertEstado(conn, planta, periodo, req.SemanaInicio, req.AnioInicio, generado: true, terminado: false);

        // ── Si es SATELITE, activar MIXING y BODEGA automáticamente ───────
        List<object> autoActivadas = new();
        if (planta == "SATELITE")
        {
            // MIXING: semana de inicio = semana de SATELITE + offset hasta semana 40 como mínimo
            var semMixing = Math.Max(SemanaMinimaMixing, req.SemanaInicio);
            var semBodega = semMixing + 1; // BODEGA va la semana siguiente a MIXING
            if (semBodega > 52) semBodega = semBodega % 52;

            var distMixing = CalcularDistribucionFija(conn, "MIXING", periodo, semMixing, req.AnioInicio);
            var distBodega = CalcularDistribucionFija(conn, "BODEGA", periodo, semBodega, req.AnioInicio);

            UpsertEstado(conn, "MIXING", periodo, semMixing, req.AnioInicio, generado: true, terminado: false);
            UpsertEstado(conn, "BODEGA", periodo, semBodega, req.AnioInicio, generado: true, terminado: false);

            autoActivadas.Add(new { planta = "MIXING", distribucion = distMixing });
            autoActivadas.Add(new { planta = "BODEGA", distribucion = distBodega });
        }

        return Ok(new
        {
            ok = 1,
            planta,
            periodo,
            semana_inicio = req.SemanaInicio,
            anio_inicio = req.AnioInicio,
            distribucion,
            auto_activadas = autoActivadas,
        });
    }

    // ══════════════════════════════════════════════════════════════════════
    // POST /CALENDARIO/TERMINAR_P1
    // Marca el P1 de una planta como terminado y habilita P2.
    // Body: { planta }
    // ══════════════════════════════════════════════════════════════════════
    [HttpPost("CALENDARIO/TERMINAR_P1")]
    public IActionResult TerminarP1([FromBody] PlantaRequest req)
    {
        var planta = (req.Planta ?? "").Trim().ToUpper();

        if (!PlantaNombreDB.ContainsKey(planta))
            return Ok(new { ok = 0, error = "Planta desconocida" });

        var estado = ObtenerEstadoPlanta(planta, 1);
        if (estado == null || !estado.Generado)
            return Ok(new { ok = 0, error = "El Período 1 aún no ha sido generado." });
        if (estado.Terminado)
            return Ok(new { ok = 0, error = "El Período 1 ya está marcado como terminado." });

        using var conn = _db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            UPDATE calendario_estado
            SET terminado=1, terminado_en=GETDATE()
            WHERE planta_key=@p AND periodo=1
            """;
        cmd.Parameters.AddWithValue("p", planta);
        cmd.ExecuteNonQuery();

        // Si es SATELITE, también terminar MIXING y BODEGA si están activos
        if (planta == "SATELITE")
        {
            foreach (var auto in PlantasAutoSatelite)
            {
                var est = ObtenerEstadoPlanta(auto, 1);
                if (est != null && est.Generado && !est.Terminado)
                {
                    using var cmd2 = conn.CreateCommand();
                    cmd2.CommandText = "UPDATE calendario_estado SET terminado=1,terminado_en=GETDATE() WHERE planta_key=@p AND periodo=1";
                    cmd2.Parameters.AddWithValue("p", auto);
                    cmd2.ExecuteNonQuery();
                }
            }
        }

        return Ok(new { ok = 1, planta, mensaje = $"Período 1 de {planta} terminado. Período 2 habilitado." });
    }

    // ══════════════════════════════════════════════════════════════════════
    // POST /CALENDARIO/TERMINAR_P2
    // Body: { planta }
    // ══════════════════════════════════════════════════════════════════════
    [HttpPost("CALENDARIO/TERMINAR_P2")]
    public IActionResult TerminarP2([FromBody] PlantaRequest req)
    {
        var planta = (req.Planta ?? "").Trim().ToUpper();

        if (!PlantaNombreDB.ContainsKey(planta))
            return Ok(new { ok = 0, error = "Planta desconocida" });

        var estado = ObtenerEstadoPlanta(planta, 2);
        if (estado == null || !estado.Generado)
            return Ok(new { ok = 0, error = "El Período 2 aún no ha sido generado." });

        using var conn = _db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "UPDATE calendario_estado SET terminado=1,terminado_en=GETDATE() WHERE planta_key=@p AND periodo=2";
        cmd.Parameters.AddWithValue("p", planta);
        cmd.ExecuteNonQuery();

        return Ok(new { ok = 1, planta });
    }

    // ══════════════════════════════════════════════════════════════════════
    // POST /CALENDARIO/RESET
    // Elimina el estado de una planta (solo ADMIN). Body: { planta }
    // ══════════════════════════════════════════════════════════════════════
    [HttpPost("CALENDARIO/RESET")]
    public IActionResult Reset([FromBody] PlantaRequest req)
    {
        if (!EsAdmin()) return Ok(new { ok = 0, error = "No autorizado" });

        var planta = (req.Planta ?? "ALL").Trim().ToUpper();
        using var conn = _db.Open();
        using var cmd = conn.CreateCommand();

        if (planta == "ALL")
            cmd.CommandText = "DELETE FROM calendario_estado";
        else
        {
            if (!PlantaNombreDB.ContainsKey(planta))
                return Ok(new { ok = 0, error = "Planta desconocida" });
            cmd.CommandText = "DELETE FROM calendario_estado WHERE planta_key=@p";
            cmd.Parameters.AddWithValue("p", planta);
        }

        cmd.ExecuteNonQuery();
        return Ok(new { ok = 1 });
    }

    // ══════════════════════════════════════════════════════════════════════
    // GET /CALENDARIO/DISTRIBUCION_PLANTA?planta=B1&periodo=1
    // Devuelve la distribución semanal de equipos de una planta/periodo,
    // enriquecida con el estado de PM realizados de la BD.
    // ══════════════════════════════════════════════════════════════════════
    [HttpGet("CALENDARIO/DISTRIBUCION_PLANTA")]
    public IActionResult DistribucionPlanta([FromQuery] string planta, [FromQuery] int periodo = 1)
    {
        planta = (planta ?? "").Trim().ToUpper();

        if (!PlantaNombreDB.ContainsKey(planta))
            return Ok(new { error = "Planta desconocida" });

        var estado = ObtenerEstadoPlanta(planta, periodo);
        if (estado == null || !estado.Generado)
            return Ok(new { error = "Calendario no generado para esta planta/periodo" });

        using var conn = _db.Open();
        var dist = planta is "MIXING" or "BODEGA"
            ? CalcularDistribucionFija(conn, planta, periodo, estado.SemanaInicio!.Value, estado.AnioInicio!.Value)
            : CalcularDistribucion(conn, planta, periodo, estado.SemanaInicio!.Value, estado.AnioInicio!.Value);

        return Ok(new { planta, periodo, distribucion = dist });
    }

    // ══════════════════════════════════════════════════════════════════════
    // GET /CALENDARIO/EQUIPOS_SEMANA?planta=B1&periodo=1&semana_rel=3
    // Devuelve los equipos asignados a una semana relativa específica,
    // con su estado de PM (realizado/pendiente).
    // ══════════════════════════════════════════════════════════════════════
    [HttpGet("CALENDARIO/EQUIPOS_SEMANA")]
    public IActionResult EquiposSemana([FromQuery] string planta, [FromQuery] int periodo, [FromQuery] int semana_rel)
    {
        planta = (planta ?? "").Trim().ToUpper();

        if (!PlantaNombreDB.ContainsKey(planta))
            return Ok(new { error = "Planta desconocida" });

        var estado = ObtenerEstadoPlanta(planta, periodo);
        if (estado == null || !estado.Generado)
            return Ok(new { error = "Calendario no generado" });

        using var conn = _db.Open();
        var nombreDB = PlantaNombreDB[planta];

        // ── Contar cómputo y laptops por separado ────────────────────────
        // Cada tipo tiene su propio calendario independiente de 24 semanas.
        using var cntComp = conn.CreateCommand();
        cntComp.CommandText = """
            SELECT COUNT(*) FROM mantenimientos_preventivos
            WHERE planta = @p
              AND nombre_dispositivo IN ('COMPUTADORA DE ESCRITORIO','UPS','IMPRESORA TERMICA')
              AND (categoria_color IS NULL OR (categoria_color NOT LIKE '%rojo%' AND categoria_color NOT LIKE '%rosa%'))
            """;
        cntComp.Parameters.AddWithValue("p", nombreDB);
        var totalComputo = Convert.ToInt64(cntComp.ExecuteScalar()!);

        using var cntLap = conn.CreateCommand();
        cntLap.CommandText = """
            SELECT COUNT(*) FROM mantenimientos_preventivos
            WHERE planta = @p AND nombre_dispositivo = 'LAPTOP'
              AND (categoria_color IS NULL OR (categoria_color NOT LIKE '%rojo%' AND categoria_color NOT LIKE '%rosa%'))
            """;
        cntLap.Parameters.AddWithValue("p", nombreDB);
        var totalLaptops = Convert.ToInt64(cntLap.ExecuteScalar() ?? 0);
        int totalSemanas;
        int computoPorSemana;
        int laptopsPorSemana;

        if (planta is "MIXING" or "BODEGA")
        {
            // Semana fija: todos los equipos en una sola semana
            totalSemanas = 1;
            computoPorSemana = (int)totalComputo;
            laptopsPorSemana = (int)totalLaptops;
        }
        else
        {
            totalSemanas = SemanasDistribucion;
            // Cada tipo distribuye su propio total en 24 semanas independientemente
            computoPorSemana = totalComputo > 0 ? (int)Math.Ceiling((double)totalComputo / totalSemanas) : 0;
            laptopsPorSemana = totalLaptops > 0 ? (int)Math.Ceiling((double)totalLaptops / totalSemanas) : 0;
        }

        if (semana_rel < 1 || semana_rel > totalSemanas)
            return Ok(new { error = $"Semana relativa fuera de rango (1–{totalSemanas})" });

        int offsetComp = (semana_rel - 1) * computoPorSemana;
        int offsetLap = (semana_rel - 1) * laptopsPorSemana;

        // ── Cómputo: paginación independiente ────────────────────────────
        using var cmdComp = conn.CreateCommand();
        cmdComp.CommandText = $"""
            SELECT id, id_equipo, nombre_dispositivo, ubicacion, categoria_color,
                   CASE WHEN preventivo_digital    IS NOT NULL THEN true ELSE false END AS tiene_pm_p1,
                   CASE WHEN preventivo_digital_p2 IS NOT NULL THEN true ELSE false END AS tiene_pm_p2,
                   fecha_realizacion, fecha_realizacion_p2
            FROM mantenimientos_preventivos
            WHERE planta = @p
              AND nombre_dispositivo IN ('COMPUTADORA DE ESCRITORIO','UPS','IMPRESORA TERMICA')
              AND (categoria_color IS NULL OR (categoria_color NOT LIKE '%rojo%' AND categoria_color NOT LIKE '%rosa%'))
            ORDER BY ubicacion, id
            LIMIT @lim OFFSET @off
            """;
        cmdComp.Parameters.AddWithValue("p", nombreDB);
        cmdComp.Parameters.AddWithValue("lim", computoPorSemana);
        cmdComp.Parameters.AddWithValue("off", offsetComp);

        var equipos = new List<object>();
        using (var r = cmdComp.ExecuteReader())
        {
            while (r.Read())
                equipos.Add(new
                {
                    id = r.GetInt64(0),
                    id_equipo = r.IsDBNull(1) ? null : r.GetString(1),
                    nombre_dispositivo = r.IsDBNull(2) ? null : r.GetString(2),
                    ubicacion = r.IsDBNull(3) ? null : r.GetString(3),
                    categoria_color = r.IsDBNull(4) ? null : r.GetString(4),
                    tiene_pm_p1 = !r.IsDBNull(5) && r.GetBoolean(5),
                    tiene_pm_p2 = !r.IsDBNull(6) && r.GetBoolean(6),
                    fecha_pm_p1 = r.IsDBNull(7) ? null : r.GetDateTime(7).ToString("yyyy-MM-dd"),
                    fecha_pm_p2 = r.IsDBNull(8) ? null : r.GetDateTime(8).ToString("yyyy-MM-dd"),
                    tipo_calendario = "computo",
                });
        }

        // ── Laptops: paginación independiente ────────────────────────────
        using var cmdLap = conn.CreateCommand();
        cmdLap.CommandText = $"""
            SELECT id, id_equipo, nombre_dispositivo, ubicacion, categoria_color,
                   CASE WHEN preventivo_digital    IS NOT NULL THEN true ELSE false END AS tiene_pm_p1,
                   CASE WHEN preventivo_digital_p2 IS NOT NULL THEN true ELSE false END AS tiene_pm_p2,
                   fecha_realizacion, fecha_realizacion_p2
            FROM mantenimientos_preventivos
            WHERE planta = @p
              AND nombre_dispositivo = 'LAPTOP'
              AND (categoria_color IS NULL OR (categoria_color NOT LIKE '%rojo%' AND categoria_color NOT LIKE '%rosa%'))
            ORDER BY ubicacion, id
            LIMIT @lim OFFSET @off
            """;
        cmdLap.Parameters.AddWithValue("p", nombreDB);
        cmdLap.Parameters.AddWithValue("lim", laptopsPorSemana);
        cmdLap.Parameters.AddWithValue("off", offsetLap);

        using (var r = cmdLap.ExecuteReader())
        {
            while (r.Read())
                equipos.Add(new
                {
                    id = r.GetInt64(0),
                    id_equipo = r.IsDBNull(1) ? null : r.GetString(1),
                    nombre_dispositivo = r.IsDBNull(2) ? null : r.GetString(2),
                    ubicacion = r.IsDBNull(3) ? null : r.GetString(3),
                    categoria_color = r.IsDBNull(4) ? null : r.GetString(4),
                    tiene_pm_p1 = !r.IsDBNull(5) && r.GetBoolean(5),
                    tiene_pm_p2 = !r.IsDBNull(6) && r.GetBoolean(6),
                    fecha_pm_p1 = r.IsDBNull(7) ? null : r.GetDateTime(7).ToString("yyyy-MM-dd"),
                    fecha_pm_p2 = r.IsDBNull(8) ? null : r.GetDateTime(8).ToString("yyyy-MM-dd"),
                    tipo_calendario = "laptop",
                });
        }

        return Ok(new
        {
            planta,
            periodo,
            semana_rel,
            total_semanas = totalSemanas,
            total_equipos = equipos.Count,
            computo_por_semana = computoPorSemana,
            laptops_por_semana = laptopsPorSemana,
            equipos,
        });
    }

    // ══════════════════════════════════════════════════════════════════════
    // HELPERS PRIVADOS
    // ══════════════════════════════════════════════════════════════════════

    private bool EsAdmin()
    {
        var usr = Request.Cookies["usuario"]
               ?? Request.Headers["X-Usuario"].FirstOrDefault()
               ?? "";
        if (string.IsNullOrWhiteSpace(usr)) return false;
        using var conn = _db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT rol FROM usuarios WHERE usuario=@u AND activo=1";
        cmd.Parameters.AddWithValue("u", usr.ToUpper());
        return cmd.ExecuteScalar()?.ToString() == "ADMIN";
    }

    private CalendarioEstadoRow? ObtenerEstadoPlanta(string planta, int periodo)
    {
        using var conn = _db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT semana_inicio, anio_inicio, terminado, generado
            FROM calendario_estado
            WHERE planta_key=@p AND periodo=@per
            """;
        cmd.Parameters.AddWithValue("p", planta.ToUpper());
        cmd.Parameters.AddWithValue("per", periodo);
        using var r = cmd.ExecuteReader();
        if (!r.Read()) return null;
        return new CalendarioEstadoRow
        {
            SemanaInicio = r.IsDBNull(0) ? null : r.GetInt32(0),
            AnioInicio = r.IsDBNull(1) ? null : r.GetInt32(1),
            Terminado = !r.IsDBNull(2) && r.GetBoolean(2),
            Generado = !r.IsDBNull(3) && r.GetBoolean(3),
        };
    }

    private void UpsertEstado(SqlConnection conn,
        string planta, int periodo, int semana, int anio, bool generado, bool terminado)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO calendario_estado
                (planta_key, periodo, semana_inicio, anio_inicio, generado, terminado, creado_en)
            VALUES (@p, @per, @sem, @anio, @gen, @term, GETDATE())
            ON CONFLICT (planta_key, periodo) DO UPDATE
            SET semana_inicio = @sem,
                anio_inicio   = @anio,
                generado      = @gen,
                terminado     = CASE WHEN EXCLUDED.terminado THEN EXCLUDED.terminado ELSE calendario_estado.terminado END,
                creado_en     = CASE WHEN calendario_estado.creado_en IS NULL THEN GETDATE() ELSE calendario_estado.creado_en END
            """;
        cmd.Parameters.AddWithValue("p", planta.ToUpper());
        cmd.Parameters.AddWithValue("per", periodo);
        cmd.Parameters.AddWithValue("sem", semana);
        cmd.Parameters.AddWithValue("anio", anio);
        cmd.Parameters.AddWithValue("gen", generado);
        cmd.Parameters.AddWithValue("term", terminado);
        cmd.ExecuteNonQuery();
    }

    /// <summary>
    /// Calcula la distribución semanal de equipos para B1, B2 o SATELITE.
    /// Divide el total de equipos de la planta en 24 semanas iguales.
    /// Enriquece con estado PM desde la BD.
    /// </summary>
    private List<SemanaDistribucion> CalcularDistribucion(
        SqlConnection conn,
        string planta, int periodo, int semIni, int anio)
    {
        var nombreDB = PlantaNombreDB[planta];

        // ── Contar laptops (calendario independiente) ─────────────────────
        using var cntLap = conn.CreateCommand();
        cntLap.CommandText = """
            SELECT COUNT(*) FROM mantenimientos_preventivos
            WHERE planta = @p AND nombre_dispositivo = 'LAPTOP'
              AND (categoria_color IS NULL OR (categoria_color NOT LIKE '%rojo%' AND categoria_color NOT LIKE '%rosa%'))
            """;
        cntLap.Parameters.AddWithValue("p", nombreDB);
        var totalLaptops = Convert.ToInt64(cntLap.ExecuteScalar()!);

        // ── Contar cómputo (calendario independiente) ─────────────────────
        using var cntComp = conn.CreateCommand();
        cntComp.CommandText = """
            SELECT COUNT(*) FROM mantenimientos_preventivos
            WHERE planta = @p
              AND nombre_dispositivo IN ('COMPUTADORA DE ESCRITORIO','UPS','IMPRESORA TERMICA')
              AND (categoria_color IS NULL OR (categoria_color NOT LIKE '%rojo%' AND categoria_color NOT LIKE '%rosa%'))
            """;
        cntComp.Parameters.AddWithValue("p", nombreDB);
        var totalComputo = Convert.ToInt64(cntComp.ExecuteScalar() ?? 0);

        // Laptops y cómputo tienen su propia distribución independiente en 24 semanas.
        // TotalEquipos = suma de ambos para mostrar en la UI, pero cada tipo
        // sigue su propio ritmo sin mezclarse.
        int laptopsPorSemana = totalLaptops > 0 ? (int)Math.Ceiling((double)totalLaptops / SemanasDistribucion) : 0;
        int computoPorSemana = totalComputo > 0 ? (int)Math.Ceiling((double)totalComputo / SemanasDistribucion) : 0;

        var result = new List<SemanaDistribucion>();
        for (int rel = 1; rel <= SemanasDistribucion; rel++)
        {
            var (semReal, anioReal, lunes, viernes) = FechasDeSemana(anio, semIni, rel);

            // Cada tipo se distribuye de forma independiente sobre sus propios 24 slots
            int laptopsSemana = (int)Math.Max(0, Math.Min(laptopsPorSemana, totalLaptops - (long)(rel - 1) * laptopsPorSemana));
            int computoSemana = (int)Math.Max(0, Math.Min(computoPorSemana, totalComputo - (long)(rel - 1) * computoPorSemana));

            result.Add(new SemanaDistribucion
            {
                SemanaRelativa = rel,
                SemanaReal = semReal,
                AnioReal = anioReal,
                LunesISO = lunes,
                ViernesISO = viernes,
                TotalEquipos = laptopsSemana + computoSemana,
                TotalComputo = computoSemana,
                TotalLaptops = laptopsSemana,
                Periodo = periodo,
            });
        }
        return result;
    }

    /// <summary>
    /// Distribución fija para MIXING (semana 25) y BODEGA (semana 26).
    /// Todos los equipos en una sola semana.
    /// </summary>
    private List<SemanaDistribucion> CalcularDistribucionFija(
        SqlConnection conn,
        string planta, int periodo, int semIni, int anio)
    {
        var nombreDB = PlantaNombreDB[planta];

        // Laptops: calendario independiente (semana fija, todos en una semana)
        using var cntLap = conn.CreateCommand();
        cntLap.CommandText = """
            SELECT COUNT(*) FROM mantenimientos_preventivos
            WHERE planta = @p AND nombre_dispositivo = 'LAPTOP'
              AND (categoria_color IS NULL OR (categoria_color NOT LIKE '%rojo%' AND categoria_color NOT LIKE '%rosa%'))
            """;
        cntLap.Parameters.AddWithValue("p", nombreDB);
        var totalLaptops = Convert.ToInt32(cntLap.ExecuteScalar() ?? 0);

        // Cómputo: calendario independiente (semana fija, todos en una semana)
        using var cntComp = conn.CreateCommand();
        cntComp.CommandText = """
            SELECT COUNT(*) FROM mantenimientos_preventivos
            WHERE planta = @p
              AND nombre_dispositivo IN ('COMPUTADORA DE ESCRITORIO','UPS','IMPRESORA TERMICA')
              AND (categoria_color IS NULL OR (categoria_color NOT LIKE '%rojo%' AND categoria_color NOT LIKE '%rosa%'))
            """;
        cntComp.Parameters.AddWithValue("p", nombreDB);
        var totalComputo = Convert.ToInt32(cntComp.ExecuteScalar() ?? 0);

        var (semReal, anioReal, lunes, viernes) = FechasDeSemana(anio, semIni, 1);

        // Ambos tipos caen en la misma semana fija pero son calendarios independientes
        return new List<SemanaDistribucion>
        {
            new()
            {
                SemanaRelativa = 1,
                SemanaReal     = semReal,
                AnioReal       = anioReal,
                LunesISO       = lunes,
                ViernesISO     = viernes,
                TotalEquipos   = totalLaptops + totalComputo,
                TotalComputo   = totalComputo,   // calendario cómputo independiente
                TotalLaptops   = totalLaptops,   // calendario laptops independiente
                Periodo        = periodo,
            }
        };
    }

    /// <summary>
    /// Calcula la semana ISO real y fechas de lunes/viernes dado el inicio y semana relativa.
    /// </summary>
    private static (int semReal, int anioReal, string lunes, string viernes) FechasDeSemana(
        int anio, int semIni, int rel)
    {
        int totalOffset = semIni - 1 + (rel - 1);
        int anioReal = anio + totalOffset / 52;
        int semReal = (totalOffset % 52) + 1;

        var lunesDate = LunesDeSemanaISO(anioReal, semReal);
        var viernesDate = lunesDate.AddDays(4);

        return (semReal, anioReal,
                lunesDate.ToString("yyyy-MM-dd"),
                viernesDate.ToString("yyyy-MM-dd"));
    }

    private static DateTime LunesDeSemanaISO(int anio, int semana)
    {
        var simple = new DateTime(anio, 1, 1).AddDays((semana - 1) * 7);
        int dow = (int)simple.DayOfWeek; // 0=dom,1=lun,...,6=sab
        int offset = dow == 0 ? -6 : 1 - dow; // mover a lunes
        return simple.AddDays(offset);
    }
}

// ══════════════════════════════════════════════════════════════════════════
// Modelos de request / respuesta
// ══════════════════════════════════════════════════════════════════════════

public class GenerarCalendarioRequest
{
    public string? Planta { get; set; }
    public int Periodo { get; set; } = 1;
    public int SemanaInicio { get; set; }
    public int AnioInicio { get; set; }
}

public class PlantaRequest
{
    public string? Planta { get; set; }
}

public class CalendarioEstadoRow
{
    public int? SemanaInicio { get; set; }
    public int? AnioInicio { get; set; }
    public bool Terminado { get; set; }
    public bool Generado { get; set; }
}

public class SemanaDistribucion
{
    [JsonPropertyName("semana_rel")]
    public int SemanaRelativa { get; set; }

    [JsonPropertyName("semana_real")]
    public int SemanaReal { get; set; }

    [JsonPropertyName("anio_real")]
    public int AnioReal { get; set; }

    [JsonPropertyName("lunes_iso")]
    public string LunesISO { get; set; } = "";

    [JsonPropertyName("viernes_iso")]
    public string ViernesISO { get; set; } = "";

    [JsonPropertyName("total_equipos")]
    public int TotalEquipos { get; set; }

    [JsonPropertyName("total_computo")]
    public int TotalComputo { get; set; }

    [JsonPropertyName("total_laptops")]
    public int TotalLaptops { get; set; }

    [JsonPropertyName("periodo")]
    public int Periodo { get; set; }
}