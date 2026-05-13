// ═══════════════════════════════════════════════════════════════════════════
// QrPageController.cs
// Solo endpoints de datos. El HTML vive en wwwroot/static/qr-preventivo.html
//
// MIGRACIÓN SQL REQUERIDA — ejecutar una sola vez en la base de datos:
//   ALTER TABLE public.mantenimientos_preventivos
//     ADD COLUMN IF NOT EXISTS recalendarizado_por               TEXT,
//     ADD COLUMN IF NOT EXISTS observaciones_de_recalendarizacion TEXT;
// ═══════════════════════════════════════════════════════════════════════════
using ChiIT.Data;
using ChiIT.Services;
using Microsoft.AspNetCore.Mvc;

namespace ChiIT.Controllers;

[ApiController]
public class QrPageController : ControllerBase
{
    private readonly DbConnectionPool _db;
    private readonly QrPageService _svc;

    public QrPageController(DbConnectionPool db, QrPageService svc)
    {
        _db = db;
        _svc = svc;
    }

    // ── GET /preventivos/qr/{ubicacion} ─────────────────────────────────────
    // Antes devolvía HTML embebido.
    // Ahora redirige al HTML estático pasando la ubicación como query param.
    [HttpGet("preventivos/qr/{ubicacion}")]
    public IActionResult VerQrPreventivo([FromRoute] string ubicacion)
    {
        var ub = (ubicacion ?? "").Replace("\u00a0", " ").Trim();
        return Redirect($"/static/qr-preventivo.html?ubicacion={Uri.EscapeDataString(ub)}");
    }

    // ── GET /PREVENTIVO/QR_DATA/{ubicacion} ─────────────────────────────────
    // API de datos que consume qr-preventivo.html
    [HttpGet("PREVENTIVO/QR_DATA/{ubicacion}")]
    public IActionResult ObtenerDatosQr([FromRoute] string ubicacion)
    {
        try
        {
            var ub = (ubicacion ?? "").Replace("\u00a0", " ").Trim();
            var result = _svc.ObtenerPorUbicacion(ub);
            return Ok(result);
        }
        catch (Exception ex)
        {
            Console.WriteLine("[QrPageController] Error: " + ex);
            return StatusCode(500, new { error = ex.Message });
        }
    }

    // ── GET /PREVENTIVO/UBICACIONES_TODAS ────────────────────────────────────
    [HttpGet("PREVENTIVO/UBICACIONES_TODAS")]
    public IActionResult ObtenerUbicacionesTodas()
    {
        try
        {
            using var conn = _db.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = """
                SELECT DISTINCT TRIM(ubicacion)
                FROM public.mantenimientos_preventivos
                WHERE ubicacion IS NOT NULL AND TRIM(ubicacion) <> ''
                ORDER BY 1
                """;
            var lista = new List<string>();
            using var r = cmd.ExecuteReader();
            while (r.Read()) lista.Add(r.GetString(0));
            return Ok(new { ubicaciones = lista });
        }
        catch (Exception ex) { return Ok(new { ubicaciones = new List<string>(), error = ex.Message }); }
    }

    // ── POST /PREVENTIVO/RECALENDARIZAR ──────────────────────────────────────
    [HttpPost("PREVENTIVO/RECALENDARIZAR")]
    public IActionResult Recalendarizar([FromBody] RecalRequest data)
    {
        try
        {
            var usuario = Request.Cookies["usuario"]
                       ?? Request.Headers["X-Usuario"].FirstOrDefault()
                       ?? data.Usuario ?? "SISTEMA";

            if (string.IsNullOrWhiteSpace(data.NuevaUbicacion))
                return Ok(new { ok = false, error = "La nueva ubicación es obligatoria" });
            if (string.IsNullOrWhiteSpace(data.ObservacionesRecal))
                return Ok(new { ok = false, error = "Las observaciones son obligatorias" });

            using var conn = _db.Open();

            // ── Verificar si la nueva ubicación ya está ocupada ──────────
            using var chk = conn.CreateCommand();
            chk.CommandText = """
                SELECT id, id_equipo, nombre_dispositivo
                FROM public.mantenimientos_preventivos
                WHERE TRIM(LOWER(ubicacion)) = TRIM(LOWER(@u))
                  AND id <> @id
                  AND (activo IS NULL OR activo = true)
                LIMIT 1
                """;
            chk.Parameters.AddWithValue("u", data.NuevaUbicacion.Trim());
            chk.Parameters.AddWithValue("id", data.IdDispositivo);

            long? idOcupante = null;
            string? dispOcupante = null;
            string? eqOcupante = null;

            using (var ro = chk.ExecuteReader())
            {
                if (ro.Read())
                {
                    idOcupante = ro.GetInt64(0);
                    eqOcupante = ro.IsDBNull(1) ? null : ro.GetString(1);
                    dispOcupante = ro.IsDBNull(2) ? null : ro.GetString(2);
                }
            }

            // Si está ocupada y no se indicó dónde mover al ocupante → pedir confirmación
            if (idOcupante.HasValue && string.IsNullOrWhiteSpace(data.UbicacionOcupante))
                return Ok(new
                {
                    ok = false,
                    ocupada = true,
                    id_ocupante = idOcupante,
                    equipo_ocupante = eqOcupante,
                    dispositivo_ocupante = dispOcupante
                });

            // ── Mover ocupante si aplica ──────────────────────────────────
            if (idOcupante.HasValue && !string.IsNullOrWhiteSpace(data.UbicacionOcupante))
            {
                using var mvOcup = conn.CreateCommand();
                mvOcup.CommandText = """
                    UPDATE public.mantenimientos_preventivos
                    SET ubicacion = @nu,
                        recalendarizado_por = @rp,
                        observaciones_de_recalendarizacion = @obs
                    WHERE id = @id
                    """;
                mvOcup.Parameters.AddWithValue("nu", data.UbicacionOcupante.Trim());
                mvOcup.Parameters.AddWithValue("rp", usuario.ToUpper());
                mvOcup.Parameters.AddWithValue("obs", $"Desplazado por recalendarización de equipo {data.IdDispositivo}");
                mvOcup.Parameters.AddWithValue("id", idOcupante.Value);
                mvOcup.ExecuteNonQuery();
            }

            // ── Mover el dispositivo solicitado ───────────────────────────
            using var upd = conn.CreateCommand();
            upd.CommandText = """
                UPDATE public.mantenimientos_preventivos
                SET ubicacion = @nu,
                    recalendarizado_por = @rp,
                    observaciones_de_recalendarizacion = @obs
                WHERE id = @id
                """;
            upd.Parameters.AddWithValue("nu", data.NuevaUbicacion.Trim());
            upd.Parameters.AddWithValue("rp", usuario.ToUpper());
            upd.Parameters.AddWithValue("obs", data.ObservacionesRecal);
            upd.Parameters.AddWithValue("id", data.IdDispositivo);
            upd.ExecuteNonQuery();

            // ── Calcular si el equipo quedó solo en la ubicación ──────────
            using var cnt = conn.CreateCommand();
            cnt.CommandText = """
                SELECT COUNT(*) FROM public.mantenimientos_preventivos
                WHERE TRIM(LOWER(ubicacion)) = TRIM(LOWER(@u))
                  AND (activo IS NULL OR activo = true)
                """;
            cnt.Parameters.AddWithValue("u", data.NuevaUbicacion.Trim());
            var totalEnUb = Convert.ToInt64(cnt.ExecuteScalar()!);

            return Ok(new
            {
                ok = true,
                nueva_ubicacion = data.NuevaUbicacion.Trim(),
                es_solo_en_ubicacion = totalEnUb == 1
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine("[Recalendarizar] " + ex);
            return Ok(new { ok = false, error = ex.Message });
        }
    }

    // ── POST /PREVENTIVO/GUARDAR_RECAL_META ──────────────────────────────────
    [HttpPost("PREVENTIVO/GUARDAR_RECAL_META")]
    public IActionResult GuardarRecalMeta([FromBody] RecalMetaRequest data)
    {
        try
        {
            using var conn = _db.Open();
            using var upd = conn.CreateCommand();
            upd.CommandText = """
                UPDATE public.mantenimientos_preventivos
                SET recalendarizado_por = @rp,
                    observaciones_de_recalendarizacion = @obs
                WHERE id = @id
                """;
            upd.Parameters.AddWithValue("rp", (object?)data.RecalendarizadoPor ?? DBNull.Value);
            upd.Parameters.AddWithValue("obs", (object?)data.ObservacionesRecal ?? DBNull.Value);
            upd.Parameters.AddWithValue("id", data.IdDispositivo);
            upd.ExecuteNonQuery();
            return Ok(new { ok = true });
        }
        catch (Exception ex) { return Ok(new { ok = false, error = ex.Message }); }
    }

    // ── POST /PREVENTIVO/STOCK ───────────────────────────────────────────────
    [HttpPost("/PREVENTIVO/STOCK")]
    public IActionResult MoverAStock([FromBody] StockRequest data)
    {
        try
        {
            var usuario = Request.Cookies["usuario"]
                       ?? Request.Headers["X-Usuario"].FirstOrDefault()
                       ?? data.Usuario ?? "SISTEMA";

            if (data.IdDispositivo <= 0)
                return Ok(new { ok = false, error = "ID de dispositivo requerido" });
            if (data.LineaSigueSiendoExistente && string.IsNullOrWhiteSpace(data.IdReemplazo))
                return Ok(new { ok = false, error = "ID de reemplazo obligatorio cuando la línea sigue existiendo" });
            if (string.IsNullOrWhiteSpace(data.Rack) || string.IsNullOrWhiteSpace(data.Espacio))
                return Ok(new { ok = false, error = "Rack y espacio son requeridos" });

            var nuevaUbicacion = $"Soporte Site ({data.Rack.Trim()}) {data.Espacio.Trim()}";

            using var conn = _db.Open();

            // Leer datos del dispositivo original
            using var sel = conn.CreateCommand();
            sel.CommandText = """
                SELECT id_equipo, ubicacion, plazo, realizado_por, fecha_realizacion,
                       observaciones, nombre_dispositivo, planta, categoria_color, anio_creacion
                FROM public.mantenimientos_preventivos WHERE id = @id
                """;
            sel.Parameters.AddWithValue("id", data.IdDispositivo);

            string? idEquipoOrig = null, ubicOrig = null, plazoOrig = null, realPorOrig = null,
                      obsOrig = null, nomDispOrig = null, plantaOrig = null, colorOrig = null;
            DateTime? fechaOrig = null;
            int? anioOrig = null;

            using (var r = sel.ExecuteReader())
            {
                if (!r.Read()) return Ok(new { ok = false, error = "Dispositivo no encontrado" });
                idEquipoOrig = r.IsDBNull(0) ? null : r.GetString(0);
                ubicOrig = r.IsDBNull(1) ? null : r.GetString(1);
                plazoOrig = r.IsDBNull(2) ? null : r.GetString(2);
                realPorOrig = r.IsDBNull(3) ? null : r.GetString(3);
                fechaOrig = r.IsDBNull(4) ? (DateTime?)null : r.GetDateTime(4);
                obsOrig = r.IsDBNull(5) ? null : r.GetString(5);
                nomDispOrig = r.IsDBNull(6) ? null : r.GetString(6);
                plantaOrig = r.IsDBNull(7) ? null : r.GetString(7);
                colorOrig = r.IsDBNull(8) ? null : r.GetString(8);
                anioOrig = r.IsDBNull(9) ? (int?)null : Convert.ToInt32(r.GetValue(9));
            }

            // Crear registro de reemplazo (solo rama Sí)
            object? nuevoId = null;
            if (data.LineaSigueSiendoExistente && !string.IsNullOrWhiteSpace(data.IdReemplazo))
            {
                using var ins = conn.CreateCommand();
                ins.CommandText = """
                    INSERT INTO public.mantenimientos_preventivos
                    (id_equipo, ubicacion, plazo, realizado_por, fecha_realizacion,
                     observaciones, nombre_dispositivo, planta, categoria_color, anio_creacion)
                    VALUES (@e,@u,@p,@rp,@fr,@o,@nd,@pl,@cc,@ac)
                    RETURNING id
                    """;
                ins.Parameters.AddWithValue("e", data.IdReemplazo.Trim());
                ins.Parameters.AddWithValue("u", (object?)ubicOrig ?? DBNull.Value);
                ins.Parameters.AddWithValue("p", (object?)plazoOrig ?? DBNull.Value);
                ins.Parameters.AddWithValue("rp", (object?)realPorOrig ?? DBNull.Value);
                ins.Parameters.AddWithValue("fr", (object?)fechaOrig ?? DBNull.Value);
                ins.Parameters.AddWithValue("o", (object?)(data.Observaciones ?? obsOrig) ?? DBNull.Value);
                ins.Parameters.AddWithValue("nd", (object?)nomDispOrig ?? DBNull.Value);
                ins.Parameters.AddWithValue("pl", (object?)plantaOrig ?? DBNull.Value);
                ins.Parameters.AddWithValue("cc", (object?)colorOrig ?? DBNull.Value);
                ins.Parameters.AddWithValue("ac", (object?)anioOrig ?? DBNull.Value);
                nuevoId = ins.ExecuteScalar();
            }

            // Actualizar dispositivo original: nueva ubicación + color ROSA
            using var upd = conn.CreateCommand();
            upd.CommandText = """
                UPDATE public.mantenimientos_preventivos
                SET ubicacion = @u, categoria_color = 'ROSA',
                    observaciones = COALESCE(@obs, observaciones)
                WHERE id = @id
                """;
            upd.Parameters.AddWithValue("u", nuevaUbicacion);
            upd.Parameters.AddWithValue("obs", (object?)data.Observaciones ?? DBNull.Value);
            upd.Parameters.AddWithValue("id", data.IdDispositivo);
            upd.ExecuteNonQuery();

            return Ok(new
            {
                ok = true,
                nueva_ubicacion = nuevaUbicacion,
                id_nuevo_registro = nuevoId,
                linea_sigue = data.LineaSigueSiendoExistente
            });
        }
        catch (Exception ex) { return Ok(new { ok = false, error = ex.Message }); }
    }

    // ── POST /PREVENTIVO/CAMBIO_PLANTA ───────────────────────────────────────
    [HttpPost("/PREVENTIVO/CAMBIO_PLANTA")]
    public IActionResult CambiarPlanta([FromBody] CambioPlantaRequest data)
    {
        try
        {
            if (data.IdDispositivo <= 0)
                return Ok(new { ok = false, error = "ID de dispositivo requerido" });
            if (string.IsNullOrWhiteSpace(data.Planta))
                return Ok(new { ok = false, error = "Debes seleccionar una planta" });

            using var conn = _db.Open();
            using var upd = conn.CreateCommand();
            upd.CommandText = "UPDATE public.mantenimientos_preventivos SET planta=@p WHERE id=@id";
            upd.Parameters.AddWithValue("p", data.Planta.Trim());
            upd.Parameters.AddWithValue("id", data.IdDispositivo);
            int rows = upd.ExecuteNonQuery();

            if (rows == 0) return Ok(new { ok = false, error = "Dispositivo no encontrado" });
            return Ok(new { ok = true, planta = data.Planta.Trim() });
        }
        catch (Exception ex) { return Ok(new { ok = false, error = ex.Message }); }
    }

    // ── POST /PREVENTIVO/RECAL_REPARACION ────────────────────────────────────
    [HttpPost("/PREVENTIVO/RECAL_REPARACION")]
    public IActionResult RecalReparacion([FromBody] RecalReparacionRequest data)
    {
        try
        {
            if (data.IdDispositivo <= 0)
                return Ok(new { ok = false, error = "ID de dispositivo requerido" });
            if (string.IsNullOrWhiteSpace(data.Rack))
                return Ok(new { ok = false, error = "El rack es obligatorio" });
            if (string.IsNullOrWhiteSpace(data.Espacio))
                return Ok(new { ok = false, error = "El espacio es obligatorio" });
            if (string.IsNullOrWhiteSpace(data.IdDispositivoPrestamo))
                return Ok(new { ok = false, error = "El ID del dispositivo de préstamo es obligatorio" });

            var usuario = Request.Cookies["usuario"]
                              ?? Request.Headers["X-Usuario"].FirstOrDefault()
                              ?? data.Usuario ?? "SISTEMA";
            var nuevaUbicacion = $"Soporte Site Reparacion ({data.Rack.Trim()} espacio {data.Espacio.Trim()})";

            using var conn = _db.Open();

            using var sel = conn.CreateCommand();
            sel.CommandText = """
                SELECT id_equipo, ubicacion, plazo, realizado_por, fecha_realizacion,
                       observaciones, nombre_dispositivo, planta, categoria_color, anio_creacion
                FROM public.mantenimientos_preventivos WHERE id = @id
                """;
            sel.Parameters.AddWithValue("id", data.IdDispositivo);

            string? idEquipoOrig = null, ubicOrig = null, plazoOrig = null, realPorOrig = null,
                      obsOrig = null, nomDispOrig = null, plantaOrig = null, colorOrig = null;
            DateTime? fechaOrig = null;
            int? anioOrig = null;

            using (var r = sel.ExecuteReader())
            {
                if (!r.Read()) return Ok(new { ok = false, error = "Dispositivo no encontrado" });
                idEquipoOrig = r.IsDBNull(0) ? null : r.GetString(0);
                ubicOrig = r.IsDBNull(1) ? null : r.GetString(1);
                plazoOrig = r.IsDBNull(2) ? null : r.GetString(2);
                realPorOrig = r.IsDBNull(3) ? null : r.GetString(3);
                fechaOrig = r.IsDBNull(4) ? (DateTime?)null : r.GetDateTime(4);
                obsOrig = r.IsDBNull(5) ? null : r.GetString(5);
                nomDispOrig = r.IsDBNull(6) ? null : r.GetString(6);
                plantaOrig = r.IsDBNull(7) ? null : r.GetString(7);
                colorOrig = r.IsDBNull(8) ? null : r.GetString(8);
                anioOrig = r.IsDBNull(9) ? (int?)null : Convert.ToInt32(r.GetValue(9));
            }

            // Duplicar tarjeta con dispositivo de préstamo
            using var ins = conn.CreateCommand();
            ins.CommandText = """
                INSERT INTO public.mantenimientos_preventivos
                (id_equipo, ubicacion, plazo, realizado_por, fecha_realizacion,
                 observaciones, nombre_dispositivo, planta, categoria_color, anio_creacion)
                VALUES (@e,@u,@p,@rp,@fr,@o,@nd,@pl,@cc,@ac)
                """;
            ins.Parameters.AddWithValue("e", data.IdDispositivoPrestamo.Trim());
            ins.Parameters.AddWithValue("u", (object?)ubicOrig ?? DBNull.Value);
            ins.Parameters.AddWithValue("p", (object?)plazoOrig ?? DBNull.Value);
            ins.Parameters.AddWithValue("rp", (object?)realPorOrig ?? DBNull.Value);
            ins.Parameters.AddWithValue("fr", (object?)fechaOrig ?? DBNull.Value);
            ins.Parameters.AddWithValue("o", (object?)obsOrig ?? DBNull.Value);
            ins.Parameters.AddWithValue("nd", (object?)nomDispOrig ?? DBNull.Value);
            ins.Parameters.AddWithValue("pl", (object?)plantaOrig ?? DBNull.Value);
            ins.Parameters.AddWithValue("cc", (object?)colorOrig ?? DBNull.Value);
            ins.Parameters.AddWithValue("ac", (object?)anioOrig ?? DBNull.Value);
            ins.ExecuteNonQuery();

            // Actualizar original: nueva ubicación + color ROJO
            using var upd = conn.CreateCommand();
            upd.CommandText = """
                UPDATE public.mantenimientos_preventivos
                SET ubicacion = @u, categoria_color = 'ROJO'
                WHERE id = @id
                """;
            upd.Parameters.AddWithValue("u", nuevaUbicacion);
            upd.Parameters.AddWithValue("id", data.IdDispositivo);
            upd.ExecuteNonQuery();

            // Insertar correctivo automático
            using var corrCmd = conn.CreateCommand();
            corrCmd.CommandText = """
                INSERT INTO public.mantenimientos_correctivos
                    (status, planta, linea_persona, equipo, descripcion_falla,
                     fecha_solicitud, reporte_elaborado_por, observaciones)
                VALUES ('PENDIENTE',@planta,@linea,@equipo,@falla,CURRENT_DATE,@reporte,@obs)
                """;
            corrCmd.Parameters.AddWithValue("planta", (object?)plantaOrig ?? DBNull.Value);
            corrCmd.Parameters.AddWithValue("linea", (object?)ubicOrig ?? DBNull.Value);
            corrCmd.Parameters.AddWithValue("equipo", (object?)idEquipoOrig ?? DBNull.Value);
            corrCmd.Parameters.AddWithValue("falla", $"Equipo en reparación — enviado a {nuevaUbicacion}");
            corrCmd.Parameters.AddWithValue("reporte", usuario.ToUpper());
            corrCmd.Parameters.AddWithValue("obs", $"Recalendarización por reparación. Equipo préstamo: {data.IdDispositivoPrestamo.Trim()}");
            corrCmd.ExecuteNonQuery();

            return Ok(new { ok = true, nueva_ubicacion = nuevaUbicacion });
        }
        catch (Exception ex) { return Ok(new { ok = false, error = ex.Message }); }
    }

    // ── POST /PREVENTIVO/REGISTRAR_BAJA ──────────────────────────────────────
    [HttpPost("/PREVENTIVO/REGISTRAR_BAJA")]
    public async Task<IActionResult> RegistrarBaja([FromBody] RegistrarBajaRequest req)
    {
        if (req?.BajaDto == null)
            return BadRequest(new { error = "Payload inválido" });
        if (string.IsNullOrWhiteSpace(req.IdEquipoReemplazo))
            return BadRequest(new { error = "El ID Equipo de Reemplazo es obligatorio" });

        req.BajaDto.ESTADO = "PENDIENTE";

        try
        {
            await using var conn = await _db.OpenAsync();

            await using var cmdBaja = new Npgsql.NpgsqlCommand("""
                INSERT INTO bajas_equipos
                    (folio,estado,planta,fecha,equipo,marca,modelo,
                     no_serie,activo_fijo,ubicacion_persona,
                     motivo_de_baja,diagnostico,comentarios,motivo_de_cancelacion)
                VALUES
                    (@folio,@estado,@planta,@fecha::date,@equipo,@marca,@modelo,
                     @no_serie,@activo_fijo,@ubicacion_persona,
                     @motivo_de_baja,@diagnostico,@comentarios,@motivo_de_cancelacion)
                RETURNING id
                """, conn);

            cmdBaja.Parameters.AddWithValue("folio", (object?)req.BajaDto.FOLIO ?? DBNull.Value);
            cmdBaja.Parameters.AddWithValue("estado", "PENDIENTE");
            cmdBaja.Parameters.AddWithValue("planta", (object?)req.BajaDto.PLANTA ?? DBNull.Value);
            cmdBaja.Parameters.AddWithValue("fecha", (object?)req.BajaDto.FECHA ?? DBNull.Value);
            cmdBaja.Parameters.AddWithValue("equipo", (object?)req.BajaDto.EQUIPO ?? DBNull.Value);
            cmdBaja.Parameters.AddWithValue("marca", (object?)req.BajaDto.MARCA ?? DBNull.Value);
            cmdBaja.Parameters.AddWithValue("modelo", (object?)req.BajaDto.MODELO ?? DBNull.Value);
            cmdBaja.Parameters.AddWithValue("no_serie", (object?)req.BajaDto.NO_SERIE ?? DBNull.Value);
            cmdBaja.Parameters.AddWithValue("activo_fijo", (object?)req.BajaDto.ACTIVO_FIJO ?? DBNull.Value);
            cmdBaja.Parameters.AddWithValue("ubicacion_persona", (object?)req.BajaDto.UBICACION_PERSONA ?? DBNull.Value);
            cmdBaja.Parameters.AddWithValue("motivo_de_baja", (object?)req.BajaDto.MOTIVO_DE_BAJA ?? DBNull.Value);
            cmdBaja.Parameters.AddWithValue("diagnostico", (object?)req.BajaDto.DIAGNOSTICO ?? DBNull.Value);
            cmdBaja.Parameters.AddWithValue("comentarios", (object?)req.BajaDto.COMENTARIOS ?? DBNull.Value);
            cmdBaja.Parameters.AddWithValue("motivo_de_cancelacion", (object?)req.BajaDto.MOTIVO_DE_CANCELACION ?? DBNull.Value);

            var bajaId = Convert.ToInt32(await cmdBaja.ExecuteScalarAsync());
            var fechaHoy = DateOnly.FromDateTime(DateTime.Today);
            var proxStr = fechaHoy.AddMonths(6).ToString("yyyy-MM-dd");
            var obsMsg = $"Antes: {req.BajaDto.EQUIPO ?? "?"}, se dio de baja, ahora: {req.IdEquipoReemplazo}";
            var usuario = req.Usuario ?? "SISTEMA";

            var pmJson = System.Text.Json.JsonSerializer.Serialize(new
            {
                usuario = usuario,
                fecha = fechaHoy.ToString("yyyy-MM-dd"),
                proximo_pm = proxStr,
                checks = Array.Empty<int>(),
                observaciones = obsMsg,
                requiere_correctivo = false,
                verificado_por = (string?)null
            });

            string updSql = req.Periodo == 2
                ? """
                  UPDATE public.mantenimientos_preventivos
                  SET id_equipo=@nuevo_id, fecha_realizacion_p2=@fr,
                      plazo_p2=@pl, realizado_por_p2=@rp, preventivo_digital_p2=@pd::jsonb
                  WHERE id=@prev_id
                  """
                : """
                  UPDATE public.mantenimientos_preventivos
                  SET id_equipo=@nuevo_id, fecha_realizacion=@fr,
                      plazo=@pl, realizado_por=@rp,
                      observaciones=@o, preventivo_digital=@pd::jsonb
                  WHERE id=@prev_id
                  """;

            await using var cmdUpd = new Npgsql.NpgsqlCommand(updSql, conn);
            cmdUpd.Parameters.AddWithValue("nuevo_id", req.IdEquipoReemplazo);
            cmdUpd.Parameters.AddWithValue("fr", fechaHoy.ToDateTime(TimeOnly.MinValue));
            cmdUpd.Parameters.AddWithValue("pl", proxStr);
            cmdUpd.Parameters.AddWithValue("rp", usuario.ToUpper());
            cmdUpd.Parameters.AddWithValue("pd", pmJson);
            cmdUpd.Parameters.AddWithValue("prev_id", req.IdPreventivoDb);
            if (req.Periodo != 2) cmdUpd.Parameters.AddWithValue("o", obsMsg);
            await cmdUpd.ExecuteNonQueryAsync();

            return Ok(new { ok = true, bajaId, idEquipoReemplazo = req.IdEquipoReemplazo, proximo_pm = proxStr });
        }
        catch (Exception ex)
        {
            Console.WriteLine("[RegistrarBaja] " + ex);
            return StatusCode(500, new { error = "Error interno: " + ex.Message });
        }
    }

    // ── DTOs ─────────────────────────────────────────────────────────────────

    public class RecalRequest
    {
        public long IdDispositivo { get; set; }
        public string NuevaUbicacion { get; set; } = "";
        public string? UbicacionOcupante { get; set; }
        public string? Usuario { get; set; }
        public string ObservacionesRecal { get; set; } = "";
    }

    public class RecalMetaRequest
    {
        public long IdDispositivo { get; set; }
        public string? RecalendarizadoPor { get; set; }
        public string? ObservacionesRecal { get; set; }
    }

    public class StockRequest
    {
        public long IdDispositivo { get; set; }
        public string IdReemplazo { get; set; } = "";
        public string Rack { get; set; } = "";
        public string Espacio { get; set; } = "";
        public string NuevaUbicacion { get; set; } = "";
        public string? Usuario { get; set; }
        public string? Observaciones { get; set; }
        public bool LineaSigueSiendoExistente { get; set; } = true;
    }

    public class CambioPlantaRequest
    {
        public long IdDispositivo { get; set; }
        public string Planta { get; set; } = "";
        public string? Usuario { get; set; }
    }

    public class RecalReparacionRequest
    {
        public long IdDispositivo { get; set; }
        public string Rack { get; set; } = "";
        public string Espacio { get; set; } = "";
        public string IdDispositivoPrestamo { get; set; } = "";
        public string? Usuario { get; set; }
    }
}

// ── DTO Baja (fuera del controller porque lo comparte con otros) ──────────────
public class RegistrarBajaRequest
{
    public BajaDto? BajaDto { get; set; }
    public int IdPreventivoDb { get; set; }
    public string? IdEquipoReemplazo { get; set; }
    public int Periodo { get; set; }
    public string? Usuario { get; set; }
}