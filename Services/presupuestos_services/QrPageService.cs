// ═══════════════════════════════════════════════════════════════════════════
// QrPageService.cs
// Lógica de datos para la página QR de mantenimientos preventivos.
// El HTML ahora vive en wwwroot/static/qr-preventivo.html
// ═══════════════════════════════════════════════════════════════════════════
using ChiIT.Data;

namespace ChiIT.Services;

// ── DTOs de respuesta ────────────────────────────────────────────────────────

public class QrEquipoDto
{
    public long Id { get; set; }
    public string IdEquipo { get; set; } = "";
    public string Dispositivo { get; set; } = "";
    public string Planta { get; set; } = "";
    public string ColorCat { get; set; } = "";
    public string? Fecha { get; set; }
    public string? Plazo { get; set; }
    public string? PlazoP2 { get; set; }
    public string Observaciones { get; set; } = "";
    public bool TienePm { get; set; }
    public bool TienePm2 { get; set; }
    public int? Anio { get; set; }
    public string? FechaP2 { get; set; }
    // calculados en el servicio
    public string ColorHex { get; set; } = "";
    public string ColorBg { get; set; } = "";
    public string ColorLabel { get; set; } = "";
    public string Icon { get; set; } = "";
    public List<string> Actividades { get; set; } = new();
}

public class QrUbicacionDto
{
    public string Ubicacion { get; set; } = "";
    public List<QrEquipoDto> Equipos { get; set; } = new();
}

// ── Servicio ─────────────────────────────────────────────────────────────────

public class QrPageService
{
    private readonly DbConnectionPool _db;

    public QrPageService(DbConnectionPool db) => _db = db;

    // ── Obtener todos los equipos de una ubicación ────────────────────────
    public QrUbicacionDto ObtenerPorUbicacion(string ubicacion)
    {
        // Normalizar NBSP
        ubicacion = (ubicacion ?? "").Replace("\u00a0", " ").Trim();

        using var conn = _db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT id, id_equipo, nombre_dispositivo, planta,
                   categoria_color, fecha_realizacion, plazo, observaciones,
                   CASE WHEN preventivo_digital   IS NOT NULL THEN true ELSE false END AS tiene_pm,
                   anio_creacion,
                   CASE WHEN preventivo_digital_p2 IS NOT NULL THEN true ELSE false END AS tiene_pm2,
                   fecha_realizacion_p2, plazo_p2::text
            FROM mantenimientos_preventivos
            WHERE TRIM(LOWER(ubicacion)) = TRIM(LOWER(@u))
            ORDER BY nombre_dispositivo
            """;
        cmd.Parameters.AddWithValue("u", ubicacion);

        var rows = new List<(long id, string idEquipo, string dispositivo, string planta,
                             string colorCat, string? fecha, string? plazo, string obs,
                             bool tienePm, int? anio, bool tienePm2,
                             string? fechaP2, string? plazoP2)>();

        using (var r = cmd.ExecuteReader())
        {
            while (r.Read())
            {
                try
                {
                    rows.Add((r.GetInt64(0),
                              r.IsDBNull(1) ? "" : r.GetString(1),
                              r.IsDBNull(2) ? "" : r.GetString(2),
                              r.IsDBNull(3) ? "" : r.GetString(3),
                              r.IsDBNull(4) ? "" : r.GetString(4),
                              r.IsDBNull(5) ? null : r.GetDateTime(5).ToString("yyyy-MM-dd"),
                              r.IsDBNull(6) ? null : r.GetString(6),
                              r.IsDBNull(7) ? "" : r.GetString(7),
                              !r.IsDBNull(8) && r.GetBoolean(8),
                              r.IsDBNull(9) ? (int?)null : Convert.ToInt32(r.GetValue(9)),
                              !r.IsDBNull(10) && r.GetBoolean(10),
                              r.IsDBNull(11) ? null : r.GetDateTime(11).ToString("yyyy-MM-dd"),
                              r.IsDBNull(12) ? null : r.GetString(12)));
                }
                catch (Exception rowEx)
                {
                    Console.WriteLine("[QrPageService] Fila con error: " + rowEx.Message);
                }
            }
        }

        // ── Plazos del calendario ─────────────────────────────────────────
        var plantasPresentes = rows.Select(r => r.planta).Distinct().ToList();
        var plazosCalendario = new Dictionary<string, (string? p1, string? p2)>(StringComparer.OrdinalIgnoreCase);

        if (plantasPresentes.Count > 0)
        {
            using var calCmd = conn.CreateCommand();
            calCmd.CommandText = """
                SELECT planta_key, periodo, semana_inicio, anio_inicio, generado
                FROM calendario_estado
                WHERE generado = true
                """;
            using var calR = calCmd.ExecuteReader();
            var calRows = new List<(string key, int per, int sem, int anio)>();
            while (calR.Read())
                if (!calR.IsDBNull(2) && !calR.IsDBNull(3))
                    calRows.Add((calR.GetString(0), calR.GetInt32(1), calR.GetInt32(2), calR.GetInt32(3)));

            foreach (var plantaDB in plantasPresentes)
            {
                if (!PlantaKeyMap.TryGetValue(plantaDB, out var key)) continue;

                string? CalcPlazo(int per)
                {
                    var row = calRows.FirstOrDefault(r => r.key.Equals(key, StringComparison.OrdinalIgnoreCase) && r.per == per);
                    if (row == default) return null;
                    var lunes = LunesDeSemanaISO(row.anio, row.sem);
                    return lunes.AddDays(4).ToString("yyyy-MM-dd");
                }

                plazosCalendario[plantaDB] = (CalcPlazo(1), CalcPlazo(2));
            }
        }

        // ── Mapear a DTOs ─────────────────────────────────────────────────
        var equipos = rows.Select(row =>
        {
            var (hex, bg, label) = GetColorBadge(row.colorCat);
            plazosCalendario.TryGetValue(row.planta, out var calPlazo);

            var plazoStr = row.tienePm ? (row.plazo ?? calPlazo.p1 ?? "Sin plazo asignado") : (calPlazo.p1 ?? "Sin plazo asignado");
            var plazoP2Str = row.tienePm2 ? (row.plazoP2 ?? calPlazo.p2 ?? "Sin plazo asignado") : (calPlazo.p2 ?? "Sin plazo asignado");

            return new QrEquipoDto
            {
                Id = row.id,
                IdEquipo = row.idEquipo,
                Dispositivo = row.dispositivo,
                Planta = row.planta,
                ColorCat = row.colorCat,
                Fecha = row.fecha,
                Plazo = plazoStr,
                PlazoP2 = plazoP2Str,
                Observaciones = row.obs,
                TienePm = row.tienePm,
                TienePm2 = row.tienePm2,
                Anio = row.anio,
                FechaP2 = row.fechaP2,
                ColorHex = hex,
                ColorBg = bg,
                ColorLabel = label,
                Icon = GetDispIcon(row.dispositivo),
                Actividades = GetActividades(row.dispositivo)
            };
        }).ToList();

        return new QrUbicacionDto { Ubicacion = ubicacion, Equipos = equipos };
    }

    // ── Helpers estáticos ─────────────────────────────────────────────────

    private static readonly Dictionary<string, string> PlantaKeyMap =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["B1"] = "B1",
            ["B2"] = "B2",
            ["PLANTA SATELITE"] = "SATELITE",
            ["PLANTA MIXING"] = "MIXING",
            ["BODEGA"] = "BODEGA",
        };

    private static DateTime LunesDeSemanaISO(int anio, int semana)
    {
        var simple = new DateTime(anio, 1, 1).AddDays((semana - 1) * 7);
        int dow = (int)simple.DayOfWeek;
        int offset = dow == 0 ? -6 : 1 - dow;
        return simple.AddDays(offset);
    }

    public static (string hex, string bg, string label) GetColorBadge(string cat)
    {
        var c = (cat ?? "").ToLower();
        if (c.Contains("verde")) return ("#10B981", "#052e16", "Verde");
        if (c.Contains("amarillo")) return ("#F59E0B", "#1c1400", "Amarillo");
        if (c.Contains("rojo")) return ("#EF4444", "#1f0000", "Rojo");
        if (c.Contains("gris")) return ("#94A3B8", "#0f172a", "Gris");
        if (c.Contains("rosa")) return ("#F472B6", "#1f0011", "Rosa");
        if (c.Contains("azul")) return ("#3B82F6", "#001233", "Azul");
        return ("#64748B", "#0f172a", string.IsNullOrEmpty(cat) ? "—" : cat);
    }

    public static string GetDispIcon(string disp)
    {
        var d = (disp ?? "").ToUpper();
        if (d.Contains("COMPUTADORA") || d.Contains("CPU")) return "🖥️";
        if (d.Contains("PORTATIL") || d.Contains("LAPTOP")) return "💻";
        if (d.Contains("IMPRESORA")) return "🖨️";
        if (d.Contains("UPS")) return "🔋";
        return "🔧";
    }

    public static List<string> GetActividades(string disp)
    {
        var d = (disp ?? "").ToUpper();
        if (d.Contains("COMPUTADORA") || d.Contains("CPU"))
            return new List<string>
            {
                "Sopletear el gabinete","Limpieza de contactos de memoria RAM",
                "Sopletear fuente de poder y ventiladores","Limpieza del gabinete",
                "Limpieza del monitor o pantalla","Limpieza y sopleteado del teclado y mouse",
                "Sopleteado de ventiladores y ranuras de enfriamiento","Limpieza exterior del lector óptico",
                "Limpieza del cableado","Actualizaciones del sistema operativo",
                "Actualizaciones de Office","Eliminación de archivos temporales y vaciar reciclaje",
                "Revisión del antivirus y escaneo","Desfragmentar las unidades de disco duro",
                "Conectar todos los periféricos correspondientes","Verificar cables y conectores sin daños",
                "Encender el equipo y verificar funcionamiento","Verificar que los periféricos funcionen correctamente",
                "Verificación vida de la pila del BIOS","Cambiar Qr del Dispositivo"
            };
        if (d.Contains("PORTATIL") || d.Contains("LAPTOP"))
            return new List<string>
            {
                "Sopletear el gabinete / chasis","Limpieza de contactos de memoria RAM",
                "Sopletear fuente de poder y ventiladores","Limpieza del monitor o pantalla",
                "Limpieza y sopleteado del teclado y touchpad","Sopleteado de ventiladores y ranuras de enfriamiento",
                "Limpieza del cableado","Actualizaciones del sistema operativo",
                "Actualizaciones de Office","Eliminación de archivos temporales y vaciar reciclaje",
                "Revisión del antivirus y escaneo","Desfragmentar las unidades de disco duro",
                "Conectar todos los periféricos correspondientes","Verificar cables y conectores sin daños",
                "Encender el equipo y verificar funcionamiento","Verificar que los periféricos funcionen correctamente",
                "Cambiar Qr de la Laptop"
            };
        if (d.Contains("IMPRESORA"))
            return new List<string>
            {
                "Sopletear la impresora térmica","Limpieza de rodillos (no usar alcohol)",
                "Limpieza del cabezal de la impresora térmica","Limpieza exterior de la impresora",
                "Limpieza del cableado","Rutear cables / anclar eliminador de impresora",
                "Conectar todos los periféricos correspondientes","Verificar cables y conectores sin daños",
                "Verificar que los periféricos funcionen correctamente","Cambiar Qr del Dispositivo"
            };
        if (d.Contains("UPS"))
            return new List<string>
            {
                "Limpieza y verificación del UPS","Limpieza del cableado",
                "Conectar todos los periféricos correspondientes","Verificar cables y conectores sin daños",
                "Verificación vida de la pila del UPS","Inspección y funcionamiento del UPS",
                "Verificar que solo equipo IT esté conectado al UPS"
            };
        return new List<string> { "Inspección general", "Limpieza exterior", "Verificación de funcionamiento" };
    }
}