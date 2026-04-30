using ChiIT.Data;
using ChiIT.Services;
using Microsoft.AspNetCore.Mvc;
using System.Text;

namespace ChiIT.Controllers;

[ApiController]
public class QrPageController : ControllerBase
{
    private readonly DbConnectionPool _db;
    public QrPageController(DbConnectionPool db) => _db = db;

    // Mapeo nombre DB → clave del calendario_estado
    private static readonly Dictionary<string, string> PlantaKeyMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["B1"] = "B1",
        ["B2"] = "B2",
        ["PLANTA SATELITE"] = "SATELITE",
        ["PLANTA MIXING"] = "MIXING",
        ["BODEGA"] = "BODEGA",
    };

    [HttpGet("preventivos/qr/{ubicacion}")]
    public ContentResult VerQrPreventivo([FromRoute] string ubicacion)
    {
        // Limpiar espacio no-separable (NBSP U+00A0) que puede venir de la BD
        ubicacion = (ubicacion ?? "").Replace(" ", " ").Trim();
        try
        {
            using var conn = _db.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = """
            SELECT id, id_equipo, nombre_dispositivo, planta,
                   categoria_color, fecha_realizacion, plazo, observaciones,
                   CASE WHEN preventivo_digital IS NOT NULL THEN true ELSE false END AS tiene_pm,
                   anio_creacion,
                   CASE WHEN preventivo_digital_p2 IS NOT NULL THEN true ELSE false END AS tiene_pm2,
                   fecha_realizacion_p2, plazo_p2::text
            FROM public.mantenimientos_preventivos
            WHERE TRIM(LOWER(ubicacion)) = TRIM(LOWER(@u))
            ORDER BY nombre_dispositivo
            """;
            cmd.Parameters.AddWithValue("u", ubicacion);

            var rows = new List<(long id, string idEquipo, string dispositivo, string planta,
                                 string colorCat, string? fecha, string? plazo, string obs, bool tienePm, int? anio, bool tienePm2, string? fechaP2, string? plazoP2)>();

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
                        Console.WriteLine("[QR] Fila con error: " + rowEx.Message);
                    }
                }
            } // reader cerrado aquí — conexión libre para siguiente query

            // ── Obtener plazos del calendario para las plantas presentes ──────────
            // Mapeo: planta_nombre_db → (plazoP1, plazoP2)
            // Si hay calendario generado, el plazo = viernes de la semana de inicio.
            // Si no hay calendario → "Sin plazo asignado".
            var plantasPresentes = rows.Select(r => r.planta).Distinct().ToList();
            var plazosCalendario = new Dictionary<string, (string? p1, string? p2)>(StringComparer.OrdinalIgnoreCase);

            if (plantasPresentes.Count > 0)
            {
                using var calCmd = conn.CreateCommand();
                calCmd.CommandText = """
                SELECT planta_key, periodo, semana_inicio, anio_inicio, generado
                FROM public.calendario_estado
                WHERE generado = true
                """;
                using var calR = calCmd.ExecuteReader();
                var calRows = new List<(string key, int per, int sem, int anio)>();
                while (calR.Read())
                {
                    if (!calR.IsDBNull(2) && !calR.IsDBNull(3))
                        calRows.Add((calR.GetString(0), calR.GetInt32(1), calR.GetInt32(2), calR.GetInt32(3)));
                }

                // Para cada planta presente, calcular el viernes de la semana de inicio
                foreach (var plantaDB in plantasPresentes)
                {
                    if (!PlantaKeyMap.TryGetValue(plantaDB, out var key)) continue;

                    string? CalcPlazo(int per)
                    {
                        var row = calRows.FirstOrDefault(r => r.key.Equals(key, StringComparison.OrdinalIgnoreCase) && r.per == per);
                        if (row == default) return null;
                        var lunes = LunesDeSemanaISO(row.anio, row.sem);
                        return lunes.AddDays(4).ToString("yyyy-MM-dd"); // viernes
                    }

                    plazosCalendario[plantaDB] = (CalcPlazo(1), CalcPlazo(2));
                }
            }

            var cards = new StringBuilder();
            foreach (var row in rows)
            {
                var (badgeColor, badgeBg, badgeLabel) = ColorBadge(row.colorCat);
                var icon = DispIcon(row.dispositivo);
                var fechaStr = row.fecha ?? "Sin registro";

                // Plazo: si tiene PM registrado → mostrar el plazo del PM,
                // si no → usar el plazo del calendario, si tampoco → "Sin plazo asignado"
                plazosCalendario.TryGetValue(row.planta, out var calPlazo);

                var plazoStr = row.tienePm
                    ? (row.plazo ?? calPlazo.p1 ?? "Sin plazo asignado")
                    : (calPlazo.p1 ?? "Sin plazo asignado");

                var plazoP2Str = row.tienePm2
                    ? (row.plazoP2 ?? calPlazo.p2 ?? "Sin plazo asignado")
                    : (calPlazo.p2 ?? "Sin plazo asignado");
                var actsHtml = ActsHtml(row.dispositivo);

                // Siempre generar los botones PM organizados en filas por período
                string btnPm =
                    "<div class=\"pm-section\" id=\"pm_section_" + row.id + "\" style=\"display:none\">" +
                      // ── Período 1 ──
                      "<div class=\"pm-row\">" +
                        "<span class=\"pm-row-label\">Período 1</span>" +
                        "<button class=\"pm-btn btn-pm btn-p1 btn btn-purple\" id=\"btn_hacer1_" + row.id + "\" onclick=\"abrirForm(" + row.id + ",1)\">📋 Registrar</button>" +
                        "<button class=\"pm-btn btn-ver btn-ver1 btn btn-cyan\" id=\"btn_ver1_" + row.id + "\" onclick=\"verPM(" + row.id + ",1)\" style=\"display:none\">👁 Ver</button>" +
                        "<button class=\"pm-btn btn-edit btn-edit1 btn btn-amber\" id=\"btn_edit1_" + row.id + "\" onclick=\"abrirEditarPM(" + row.id + ",1)\" style=\"display:none\">✏️ Editar</button>" +
                        "<button class=\"pm-btn btn-del btn-del1 btn btn-danger\" id=\"btn_del1_" + row.id + "\" onclick=\"eliminarPreventivo(" + row.id + ",1)\" style=\"display:none\">🗑</button>" +
                      "</div>" +
                      // ── Período 2 ──
                      "<div class=\"pm-row\">" +
                        "<span class=\"pm-row-label\">Período 2</span>" +
                        "<button class=\"pm-btn btn-pm btn-p2 btn btn-purple\" id=\"btn_hacer2_" + row.id + "\" onclick=\"abrirForm(" + row.id + ",2)\">📋 Registrar</button>" +
                        "<button class=\"pm-btn btn-ver btn-ver2 btn btn-cyan\" id=\"btn_ver2_" + row.id + "\" onclick=\"verPM(" + row.id + ",2)\" style=\"display:none\">👁 Ver</button>" +
                        "<button class=\"pm-btn btn-edit btn-edit2 btn btn-amber\" id=\"btn_edit2_" + row.id + "\" onclick=\"abrirEditarPM(" + row.id + ",2)\" style=\"display:none\">✏️ Editar</button>" +
                        "<button class=\"pm-btn btn-del btn-del2 btn btn-danger\" id=\"btn_del2_" + row.id + "\" onclick=\"eliminarPreventivo(" + row.id + ",2)\" style=\"display:none\">🗑</button>" +
                      "</div>" +
                    "</div>";
                bool tienePmFlag = row.tienePm;

                cards.Append("<div class=\"card\" data-id=\"" + row.id + "\" data-tiene-pm=\"" + (row.tienePm ? "true" : "false") + "\" data-tiene-pm2=\"" + (row.tienePm2 ? "true" : "false") + "\">\n");
                cards.Append("  <input type=\"hidden\" id=\"ubicacion_" + row.id + "\" value=\"" + Esc(ubicacion) + "\">\n");
                cards.Append("  <input type=\"hidden\" id=\"anio_" + row.id + "\" value=\"" + (row.anio?.ToString() ?? "") + "\">\n");  // kept for legacy
                cards.Append("  <div class=\"card-top\">\n");
                cards.Append("    <div class=\"dev-icon\">" + icon + "</div>\n");
                cards.Append("    <div class=\"dev-name\"><h3>" + Esc(row.dispositivo) + "</h3><span>" + Esc(row.idEquipo) + "</span></div>\n");
                cards.Append("    <span class=\"color-badge\" style=\"background:" + badgeBg + ";color:" + badgeColor + ";border:1px solid " + badgeColor + "40\">" + badgeLabel + "</span>\n");
                cards.Append("    <span class=\"edit-mode-badge\">✏️ Editando</span>\n");
                cards.Append("  </div>\n");
                cards.Append("  <div class=\"card-body\">\n");
                cards.Append("    <div class=\"info-row\">\n");
                cards.Append("      <div class=\"info-item\"><label>ID Equipo</label><input id=\"equipo_" + row.id + "\" value=\"" + Esc(row.idEquipo) + "\" disabled></div>\n");
                cards.Append("      <div class=\"info-item\"><label>Dispositivo</label>" +
                    "<select id=\"disp_" + row.id + "\" disabled>" +
                    "<option value=\"COMPUTADORA DE ESCRITORIO\"" + (row.dispositivo == "COMPUTADORA DE ESCRITORIO" ? " selected" : "") + ">🖥️ Computadora de Escritorio</option>" +
                    "<option value=\"LAPTOP\"" + (row.dispositivo == "LAPTOP" ? " selected" : "") + ">💻 Laptop</option>" +
                    "<option value=\"IMPRESORA TERMICA\"" + (row.dispositivo == "IMPRESORA TERMICA" ? " selected" : "") + ">🖨️ Impresora Térmica</option>" +
                    "<option value=\"UPS\"" + (row.dispositivo == "UPS" ? " selected" : "") + ">🔋 UPS</option>" +
                    "</select></div>\n");
                cards.Append("      <div class=\"info-item\"><label>Planta</label>" +
                    "<select id=\"planta_" + row.id + "\" disabled>" +
                    "<option value=\"B1\"" + (row.planta == "B1" ? " selected" : "") + ">B1</option>" +
                    "<option value=\"B2\"" + (row.planta == "B2" ? " selected" : "") + ">B2</option>" +
                    "<option value=\"PLANTA SATELITE\"" + (row.planta == "PLANTA SATELITE" ? " selected" : "") + ">Planta Satélite</option>" +
                    "<option value=\"BODEGA\"" + (row.planta == "BODEGA" ? " selected" : "") + ">Bodega</option>" +
                    "<option value=\"PLANTA MIXING\"" + (row.planta == "PLANTA MIXING" ? " selected" : "") + ">Planta Mixing</option>" +
                    "</select></div>\n");
                cards.Append("      <div class=\"info-item\"><label>Color</label>" +
                    "<select id=\"color_" + row.id + "\" disabled>" +
                    "<option value=\"Verde\"" + (row.colorCat.ToLower().Contains("verde") ? " selected" : "") + ">🟢 Verde</option>" +
                    "<option value=\"Gris\"" + (row.colorCat.ToLower().Contains("gris") ? " selected" : "") + ">⚫ Gris</option>" +
                    "<option value=\"Azul\"" + (row.colorCat.ToLower().Contains("azul") ? " selected" : "") + ">🔵 Azul</option>" +
                    "<option value=\"Rojo\"" + (row.colorCat.ToLower().Contains("rojo") ? " selected" : "") + ">🔴 Rojo</option>" +
                    "<option value=\"Amarillo\"" + (row.colorCat.ToLower().Contains("amarillo") ? " selected" : "") + ">🟡 Amarillo</option>" +
                    "<option value=\"Rosa\"" + (row.colorCat.ToLower().Contains("rosa") ? " selected" : "") + ">🩷 Rosa</option>" +
                    "</select></div>\n");
                cards.Append("      <div class=\"info-item\"><label>Año Creación</label>" +
                    "<select id=\"anio_vis_" + row.id + "\" disabled>" +
                    "<option value=\"\">--</option>" +
                    "<option value=\"2022\"" + (row.anio == 2022 ? " selected" : "") + ">2022</option>" +
                    "<option value=\"2023\"" + (row.anio == 2023 ? " selected" : "") + ">2023</option>" +
                    "<option value=\"2024\"" + (row.anio == 2024 ? " selected" : "") + ">2024</option>" +
                    "<option value=\"2025\"" + (row.anio == 2025 ? " selected" : "") + ">2025</option>" +
                    "<option value=\"2026\"" + (row.anio == 2026 ? " selected" : "") + ">2026</option>" +
                    "<option value=\"2027\"" + (row.anio == 2027 ? " selected" : "") + ">2027</option>" +
                    "</select></div>\n");
                cards.Append("    </div>\n");
                cards.Append("    <div class=\"status-row\">\n");
                cards.Append("      <span style=\"font-family:'DM Mono',monospace;font-size:10px;color:#64748B;font-weight:600;text-transform:uppercase;letter-spacing:.06em\">Próximo PM</span>\n");
                cards.Append("      <span style=\"margin-left:auto;font-family:'DM Mono',monospace;font-size:10px;color:#475569;display:flex;flex-direction:column;align-items:flex-end;gap:2px\">" +
                    "<span>P1: <span id=\"plazo_" + row.id + "\">" + plazoStr + "</span></span>" +
                    "<span>P2: <span id=\"plazo_p2_" + row.id + "\">" + plazoP2Str + "</span></span>" +
                    "</span>\n");
                cards.Append("    </div>\n");
                cards.Append("    <div class=\"periodos-estado\">\n");
                cards.Append("      <span class=\"periodo-badge " + (row.tienePm ? "periodo-ok" : "periodo-pend") + "\" id=\"pbadge1_" + row.id + "\">📋 P1: " + (row.tienePm ? "✅ Registrado" : "⏳ Pendiente") + "</span>\n");
                cards.Append("      <span class=\"periodo-badge " + (row.tienePm2 ? "periodo-ok" : "periodo-pend") + "\" id=\"pbadge2_" + row.id + "\">📋 P2: " + (row.tienePm2 ? "✅ Registrado" : "⏳ Pendiente") + "</span>\n");
                cards.Append("    </div>\n");
                cards.Append("    <div id=\"obswrap_" + row.id + "\" style=\"display:block;margin-top:10px\">\n");
                cards.Append("      <div class=\"obs-label\">Observaciones</div>\n");
                cards.Append("      <textarea class=\"obs-edit-field\" id=\"obs_" + row.id + "\" disabled>" + Esc(row.obs) + "</textarea>\n");
                cards.Append("    </div>\n");
                cards.Append("    <div class=\"mini-form\" id=\"form1_" + row.id + "\" style=\"display:none\">\n");
                cards.Append("      <div class=\"form-sep\" style=\"margin-top:12px\">📋 Período 1 — Actividades</div>\n");
                cards.Append("      <div class=\"acts-list\">" + actsHtml + "</div>\n");
                cards.Append(
                "<label class=\"act-item act-correctivo\">" +
                "<button class=\"btn btn-danger btn-sm\" " +
                "onclick=\"abrirModalCorrectivo(" +
                row.id + ",'" +
                Esc(row.planta) + "','" +
                Esc(row.idEquipo) + "','" +
                Esc(ubicacion) +
                "')\">" +
                "⚠️ Requiere Correctivo" +
                "</button>" +
                "</label>\n");
                cards.Append("      <div class=\"form-sep\" style=\"margin-top:10px\">📅 Fecha</div>\n");
                cards.Append("      <input type=\"date\" class=\"date-input\" id=\"fecha1_" + row.id + "\">\n");
                cards.Append("      <div class=\"form-sep\" style=\"margin-top:8px\">📝 Observaciones</div>\n");
                cards.Append("      <textarea class=\"date-input\" style=\"min-height:52px;resize:vertical;\" id=\"obs_pm1_" + row.id + "\" placeholder=\"Observaciones P1...\"></textarea>\n");
                cards.Append("      <div class=\"form-actions\" style=\"margin-top:8px\">\n");
                cards.Append("        <button class=\"btn btn-success\" onclick=\"guardarPreventivo(" + row.id + ",1)\">💾 Guardar P1</button>\n");
                cards.Append("        <button class=\"btn btn-baja\" onclick=\"abrirBaja(" + row.id + ",1,'" + Esc(row.idEquipo) + "','" + Esc(ubicacion) + "','" + Esc(row.planta) + "')\">📤 Baja de Equipo</button>\n");
                cards.Append("      </div>\n    </div>\n");
                cards.Append("    <div class=\"mini-form\" id=\"ver1_" + row.id + "\" style=\"display:none\">\n");
                cards.Append("      <div class=\"form-sep\" style=\"margin-top:12px;color:#06B6D4\">👁 Período 1 — Registrado</div>\n");
                cards.Append("      <div class=\"acts-list\" id=\"ver_acts1_" + row.id + "\" style=\"max-height:180px;overflow-y:auto\"></div>\n");
                cards.Append("      <div style=\"margin-top:10px;font-size:11px;color:var(--muted2);display:flex;flex-wrap:wrap;gap:8px\">\n");
                cards.Append("        <span>👤 <b id=\"ver_usuario1_" + row.id + "\"></b></span>\n");
                cards.Append("        <span>📅 <b id=\"ver_fecha1_" + row.id + "\"></b></span>\n");
                cards.Append("        <span>⏭ <b id=\"ver_proximo1_" + row.id + "\"></b></span>\n");
                cards.Append("      </div>\n");
                cards.Append("      <div style=\"margin-top:6px;font-size:11px;color:var(--muted2)\" id=\"ver_obs1_" + row.id + "\"></div>\n");
                cards.Append("      <div id=\"ver_correctivo1_" + row.id + "\" style=\"display:none;margin-top:8px;padding:8px 12px;border-radius:8px;background:rgba(239,68,68,.12);border:1px solid rgba(239,68,68,.4);font-size:12px;font-weight:700;color:#fca5a5;\">⚠️ Requiere Correctivo</div>\n");
                cards.Append("      <div class=\"form-actions\" style=\"margin-top:8px\">\n");
                cards.Append("      </div>\n    </div>\n");
                cards.Append("    <div class=\"mini-form\" id=\"edit_pm1_" + row.id + "\" style=\"display:none\">\n");
                cards.Append("      <div class=\"form-sep\" style=\"margin-top:12px;color:var(--amber)\">✏️ Editar Período 1</div>\n");
                cards.Append("      <div class=\"acts-list\" id=\"edit_acts1_" + row.id + "\">" + actsHtml + "</div>\n");
                cards.Append("<button class=\"btn btn-danger btn-sm\" onclick=\"abrirModalCorrectivo(" + row.id + ",'" + row.planta + "','" + row.idEquipo + "','" + Esc(ubicacion) + "')\">⚠️ Requiere Correctivo</button>");
                cards.Append("      <div class=\"form-sep\" style=\"margin-top:10px\">📅 Fecha</div>\n");
                cards.Append("      <input type=\"date\" class=\"date-input\" id=\"edit_fecha1_" + row.id + "\">\n");
                cards.Append("      <div class=\"form-sep\" style=\"margin-top:8px\">📝 Observaciones</div>\n");
                cards.Append("      <textarea class=\"date-input\" style=\"min-height:52px;resize:vertical;\" id=\"edit_obs_pm1_" + row.id + "\" placeholder=\"Observaciones...\"></textarea>\n");
                cards.Append("      <div class=\"form-actions\" style=\"margin-top:8px\">\n");
                cards.Append("        <button class=\"btn btn-amber\" onclick=\"guardarEditarPM(" + row.id + ",1)\">💾 Guardar P1</button>\n");
                cards.Append("      </div>\n    </div>\n");
                cards.Append("    <div class=\"mini-form\" id=\"form2_" + row.id + "\" style=\"display:none\">\n");
                cards.Append("      <div class=\"form-sep\" style=\"margin-top:12px\">📋 Período 2 — Actividades</div>\n");
                cards.Append("      <div class=\"acts-list\">" + actsHtml + "</div>\n");
                cards.Append("<button class=\"btn btn-danger btn-sm\" onclick=\"abrirModalCorrectivo(" + row.id + ",'" + row.planta + "','" + row.idEquipo + "','" + Esc(ubicacion) + "')\">⚠️ Requiere Correctivo</button>");
                cards.Append("      <div class=\"form-sep\" style=\"margin-top:10px\">📅 Fecha</div>\n");
                cards.Append("      <input type=\"date\" class=\"date-input\" id=\"fecha2_" + row.id + "\">\n");
                cards.Append("      <div class=\"form-sep\" style=\"margin-top:8px\">📝 Observaciones</div>\n");
                cards.Append("      <textarea class=\"date-input\" style=\"min-height:52px;resize:vertical;\" id=\"obs_pm2_" + row.id + "\" placeholder=\"Observaciones P2...\"></textarea>\n");
                cards.Append("      <div class=\"form-actions\" style=\"margin-top:8px\">\n");
                cards.Append("        <button class=\"btn btn-success\" onclick=\"guardarPreventivo(" + row.id + ",2)\">💾 Guardar P2</button>\n");
                cards.Append("        <button class=\"btn btn-baja\" onclick=\"abrirBaja(" + row.id + ",2,'" + Esc(row.idEquipo) + "','" + Esc(ubicacion) + "','" + Esc(row.planta) + "')\">📤 Baja de Equipo</button>\n");
                cards.Append("      </div>\n    </div>\n");
                cards.Append("    <div class=\"mini-form\" id=\"ver2_" + row.id + "\" style=\"display:none\">\n");
                cards.Append("      <div class=\"form-sep\" style=\"margin-top:12px;color:#06B6D4\">👁 Período 2 — Registrado</div>\n");
                cards.Append("      <div class=\"acts-list\" id=\"ver_acts2_" + row.id + "\" style=\"max-height:180px;overflow-y:auto\"></div>\n");
                cards.Append("      <div style=\"margin-top:10px;font-size:11px;color:var(--muted2);display:flex;flex-wrap:wrap;gap:8px\">\n");
                cards.Append("        <span>👤 <b id=\"ver_usuario2_" + row.id + "\"></b></span>\n");
                cards.Append("        <span>📅 <b id=\"ver_fecha2_" + row.id + "\"></b></span>\n");
                cards.Append("        <span>⏭ <b id=\"ver_proximo2_" + row.id + "\"></b></span>\n");
                cards.Append("      </div>\n");
                cards.Append("      <div style=\"margin-top:6px;font-size:11px;color:var(--muted2)\" id=\"ver_obs2_" + row.id + "\"></div>\n");
                cards.Append("      <div id=\"ver_correctivo2_" + row.id + "\" style=\"display:none;margin-top:8px;padding:8px 12px;border-radius:8px;background:rgba(239,68,68,.12);border:1px solid rgba(239,68,68,.4);font-size:12px;font-weight:700;color:#fca5a5;\">⚠️ Requiere Correctivo</div>\n");
                cards.Append("      <div class=\"form-actions\" style=\"margin-top:8px\">\n");
                cards.Append("      </div>\n    </div>\n");
                cards.Append("    <div class=\"mini-form\" id=\"edit_pm2_" + row.id + "\" style=\"display:none\">\n");
                cards.Append("      <div class=\"form-sep\" style=\"margin-top:12px;color:var(--amber)\">✏️ Editar Período 2</div>\n");
                cards.Append("      <div class=\"acts-list\" id=\"edit_acts2_" + row.id + "\">" + actsHtml + "</div>\n");
                cards.Append("      <label class=\"act-item act-correctivo\"><input type=\"checkbox\" id=\"req_correctivo_edit2_" + row.id + "\"><span class=\"act-check\"></span><span class=\"act-text\">⚠️ Requiere Correctivo</span></label>\n");
                cards.Append("      <div class=\"form-sep\" style=\"margin-top:10px\">📅 Fecha</div>\n");
                cards.Append("      <input type=\"date\" class=\"date-input\" id=\"edit_fecha2_" + row.id + "\">\n");
                cards.Append("      <div class=\"form-sep\" style=\"margin-top:8px\">📝 Observaciones</div>\n");
                cards.Append("      <textarea class=\"date-input\" style=\"min-height:52px;resize:vertical;\" id=\"edit_obs_pm2_" + row.id + "\" placeholder=\"Observaciones...\"></textarea>\n");
                cards.Append("      <div class=\"form-actions\" style=\"margin-top:8px\">\n");
                cards.Append("        <button class=\"btn btn-amber\" onclick=\"guardarEditarPM(" + row.id + ",2)\">💾 Guardar P2</button>\n");
                cards.Append("      </div>\n    </div>\n");
                cards.Append("  </div>\n");
                cards.Append("  <div class=\"card-actions\">\n");
                cards.Append("    <div class=\"ca-edit-row\">\n");
                cards.Append("      <button class=\"btn btn-blue\" onclick=\"abrirEditar(" + row.id + ")\">✏️ Editar</button>\n");
                cards.Append("      <button class=\"btn btn-green\" onclick=\"guardarCambios(" + row.id + ")\">💾 Guardar</button>\n");
                cards.Append("      <button class=\"btn btn-ghost\" onclick=\"cancelarTodo(" + row.id + ")\">↩ Cancelar</button>\n");
                cards.Append("      <button class=\"btn btn-ghost\" onclick=\"window.close()\">✕ Salir</button>\n");
                cards.Append("      <button class=\"btn btn-recal\" id=\"btn_recal_" + row.id + "\" style=\"display:none\" " +
                    "onclick=\"abrirRecal(" + row.id + ",'" + Esc(row.idEquipo) + "','" + Esc(ubicacion) + "','" + Esc(row.planta) + "','" + Esc(row.dispositivo) + "')\">📍 Recalendarización</button>\n");
                cards.Append("    </div>\n");
                cards.Append("    " + btnPm + "\n");
                // ── Sección QR del dispositivo ──
                cards.Append("    <div class=\"qr-section\">\n");
                cards.Append("      <div class=\"form-sep\" style=\"margin:10px 0 6px\">📷 QR del Dispositivo</div>\n");
                cards.Append("      <div style=\"display:flex;align-items:center;gap:14px;\">\n");
                cards.Append("        <div id=\"qr_" + row.id + "\" data-equipo=\"" + Esc(row.idEquipo) + "\" style=\"background:white;padding:8px;border-radius:8px;width:90px;height:90px;flex-shrink:0;\"></div>\n");
                cards.Append("        <span style=\"font-size:11px;font-family:'DM Mono',monospace;color:var(--muted2);\">" + Esc(row.idEquipo) + "</span>\n");
                cards.Append("      </div>\n");
                cards.Append("    </div>\n");
                cards.Append("  </div>\n</div>\n");
            }

            var html = HtmlPage(Esc(ubicacion), cards.ToString());
            return Content(html, "text/html; charset=utf-8");
        }
        catch (Exception ex)
        {
            Console.WriteLine("[QR] 500 en ubicacion=" + ubicacion + ": " + ex);
            var errHtml = "<html><body><h2>Error: " + ex.GetType().Name + "</h2><pre>" + ex.Message + "</pre></body></html>";
            return Content(errHtml, "text/html; charset=utf-8");
        }
    }

    // ── POST /PREVENTIVO/REGISTRAR_BAJA ──────────────────────────────────────
    // Inserta en bajas_equipos (ESTADO=PENDIENTE forzado) y actualiza
    // el id_equipo del preventivo con el equipo de reemplazo.
    [HttpPost("/PREVENTIVO/REGISTRAR_BAJA")]
    public async Task<IActionResult> RegistrarBaja([FromBody] RegistrarBajaRequest req)
    {
        if (req?.BajaDto == null)
            return BadRequest(new { error = "Payload inválido" });

        if (string.IsNullOrWhiteSpace(req.IdEquipoReemplazo))
            return BadRequest(new { error = "El ID Equipo de Reemplazo es obligatorio" });

        // Forzar ESTADO = PENDIENTE siempre
        req.BajaDto.ESTADO = "PENDIENTE";

        try
        {
            await using var conn = await _db.OpenAsync();

            // 1. Insertar en bajas_equipos
            await using var cmdBaja = new Npgsql.NpgsqlCommand("""
                INSERT INTO bajas_equipos
                    (folio, estado, planta, fecha, equipo, marca, modelo,
                     no_serie, activo_fijo, ubicacion_persona,
                     motivo_de_baja, diagnostico, comentarios, motivo_de_cancelacion)
                VALUES
                    (@folio, @estado, @planta, @fecha::date, @equipo, @marca, @modelo,
                     @no_serie, @activo_fijo, @ubicacion_persona,
                     @motivo_de_baja, @diagnostico, @comentarios, @motivo_de_cancelacion)
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

            // 2. Marcar el PM del período como completado (checks vacíos, observaciones de baja)
            //    y actualizar id_equipo con el equipo de reemplazo.
            var fechaHoy = DateOnly.FromDateTime(DateTime.Today);
            var proximoPm = fechaHoy.AddMonths(6);
            var proxStr = proximoPm.ToString("yyyy-MM-dd");
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
                  SET id_equipo            = @nuevo_id,
                      fecha_realizacion_p2 = @fr,
                      plazo_p2             = @pl,
                      realizado_por_p2     = @rp,
                      preventivo_digital_p2 = @pd::jsonb
                  WHERE id = @prev_id
                  """
                : """
                  UPDATE public.mantenimientos_preventivos
                  SET id_equipo        = @nuevo_id,
                      fecha_realizacion = @fr,
                      plazo            = @pl,
                      realizado_por    = @rp,
                      observaciones    = @o,
                      preventivo_digital = @pd::jsonb
                  WHERE id = @prev_id
                  """;

            await using var cmdUpd = new Npgsql.NpgsqlCommand(updSql, conn);
            cmdUpd.Parameters.AddWithValue("nuevo_id", req.IdEquipoReemplazo);
            cmdUpd.Parameters.AddWithValue("fr", fechaHoy.ToDateTime(TimeOnly.MinValue));
            cmdUpd.Parameters.AddWithValue("pl", proxStr);
            cmdUpd.Parameters.AddWithValue("rp", usuario.ToUpper());
            cmdUpd.Parameters.AddWithValue("pd", pmJson);
            cmdUpd.Parameters.AddWithValue("prev_id", req.IdPreventivoDb);
            if (req.Periodo != 2)
                cmdUpd.Parameters.AddWithValue("o", obsMsg);

            await cmdUpd.ExecuteNonQueryAsync();

            return Ok(new { ok = true, bajaId, idEquipoReemplazo = req.IdEquipoReemplazo, proximo_pm = proxStr });
        }
        catch (Exception ex)
        {
            Console.WriteLine("[RegistrarBaja] Error: " + ex);
            return StatusCode(500, new { error = "Error interno: " + ex.Message });
        }
    }


    private static string HtmlPage(string ubicacion, string cardsHtml)
    {
        var sb = new StringBuilder();
        sb.AppendLine("<!DOCTYPE html>");
        sb.AppendLine("<html lang=\"es\">");
        sb.AppendLine("<head>");
        sb.AppendLine("<meta charset=\"UTF-8\">");
        sb.AppendLine("<meta name=\"viewport\" content=\"width=device-width,initial-scale=1\">");
        sb.AppendLine("<title>PM — " + ubicacion + "</title>");
        sb.AppendLine("<link href=\"https://fonts.googleapis.com/css2?family=DM+Sans:wght@300;400;500;600;700&family=DM+Mono:wght@400;500&display=swap\" rel=\"stylesheet\">");
        sb.AppendLine("<script src=\"https://cdnjs.cloudflare.com/ajax/libs/qrcodejs/1.0.0/qrcode.min.js\"></script>");
        sb.AppendLine("<style>");
        sb.AppendLine(":root{--bg:#0B0F1A;--surface:#111827;--surface2:#1a2235;--border:rgba(255,255,255,0.07);--border2:rgba(255,255,255,0.12);--accent:#3B82F6;--text:#F1F5F9;--muted:#64748B;--muted2:#94A3B8;--green:#10B981;--red:#EF4444;--amber:#F59E0B;--radius:14px;}");
        sb.AppendLine("*{box-sizing:border-box;margin:0;padding:0;}");
        sb.AppendLine("body{font-family:'DM Sans',sans-serif;background:var(--bg);color:var(--text);min-height:100vh;padding-bottom:40px;}");
        sb.AppendLine(".top-bar{background:linear-gradient(135deg,#0f1e35,#0B0F1A);border-bottom:1px solid var(--border2);padding:14px 20px;display:flex;align-items:center;gap:12px;position:sticky;top:0;z-index:100;}");
        sb.AppendLine(".top-icon{width:42px;height:42px;border-radius:10px;background:linear-gradient(135deg,#1D4ED8,#3B82F6);display:flex;align-items:center;justify-content:center;font-size:20px;flex-shrink:0;}");
        sb.AppendLine(".top-title{flex:1;}.top-title h1{font-size:15px;font-weight:700;}.top-title p{font-size:11px;color:var(--muted2);margin-top:2px;}");
        sb.AppendLine(".user-chip{display:none;align-items:center;gap:8px;padding:7px 14px;border-radius:999px;background:rgba(16,185,129,.15);border:1px solid rgba(16,185,129,.3);font-size:12px;font-weight:600;color:#6ee7b7;}");
        sb.AppendLine("@keyframes pop{0%{transform:scale(1)}30%{transform:scale(.88)}65%{transform:scale(1.08)}100%{transform:scale(1)}}@keyframes ripple{0%{transform:translate(-50%,-50%) scale(0);opacity:.5}100%{transform:translate(-50%,-50%) scale(4);opacity:0}}.btn{display:inline-flex;align-items:center;gap:6px;padding:9px 16px;border:none;border-radius:8px;font-family:'DM Sans',sans-serif;font-size:13px;font-weight:600;cursor:pointer;transition:transform .15s,filter .15s;position:relative;overflow:hidden;}.btn.animating{animation:pop .35s cubic-bezier(.36,.07,.19,.97) forwards;}.btn-ripple{position:absolute;width:40px;height:40px;border-radius:50%;background:rgba(255,255,255,.4);pointer-events:none;animation:ripple .5s ease forwards;}.btn:hover{transform:translateY(-2px);filter:brightness(1.1);}");
        sb.AppendLine(".btn-primary{background:var(--accent);color:white;}.btn-success{background:var(--green);color:white;}");
        sb.AppendLine(".btn-ghost{background:var(--surface2);color:var(--muted2);border:1px solid var(--border2);}");
        sb.AppendLine(".btn-blue{background:#3B82F6;color:white;}.btn-green{background:#10B981;color:white;}");
        sb.AppendLine(".btn-amber{background:var(--amber);color:#1c1400;}.btn-danger{background:var(--red);color:white;}");
        sb.AppendLine(".btn-cyan{background:#06B6D4;color:#001a1f;}.btn-purple{background:#8B5CF6;color:white;}");
        sb.AppendLine(".btn-recal{background:linear-gradient(135deg,#0f766e,#14b8a6);color:white;}");
        sb.AppendLine(".btn-login{background:linear-gradient(135deg,#1D4ED8,#3B82F6);color:white;}");
        sb.AppendLine(".btn-print-all{background:linear-gradient(135deg,#065f46,#10B981);color:white;font-size:12px;padding:8px 14px;}");
        sb.AppendLine(".grid{display:grid;grid-template-columns:repeat(auto-fill,minmax(320px,1fr));gap:14px;padding:16px 20px;}");
        sb.AppendLine(".card{background:var(--surface);border:1px solid var(--border);border-radius:var(--radius);overflow:hidden;transition:border-color .2s;}");
        sb.AppendLine(".card-top{display:flex;align-items:center;gap:12px;padding:14px 16px;border-bottom:1px solid var(--border);background:var(--surface2);}");
        sb.AppendLine(".dev-icon{width:38px;height:38px;border-radius:9px;background:rgba(59,130,246,.12);border:1px solid rgba(59,130,246,.2);display:flex;align-items:center;justify-content:center;font-size:18px;flex-shrink:0;}");
        sb.AppendLine(".dev-name{flex:1;}.dev-name h3{font-size:13px;font-weight:700;}.dev-name span{font-size:11px;color:var(--muted2);}");
        sb.AppendLine(".color-badge{padding:4px 10px;border-radius:999px;font-size:10px;font-weight:700;text-transform:uppercase;}");
        sb.AppendLine(".card-body{padding:14px 16px;}");
        sb.AppendLine(".info-row{display:grid;grid-template-columns:1fr 1fr 1fr;gap:10px;margin-bottom:12px;}");
        sb.AppendLine(".info-item label{font-size:9px;font-weight:600;text-transform:uppercase;letter-spacing:.1em;color:var(--muted);display:block;margin-bottom:4px;}");
        sb.AppendLine(".info-item input,.info-item select{width:100%;background:var(--surface2);border:1px solid var(--border2);border-radius:6px;padding:7px 9px;font-size:12px;color:var(--text);opacity:.6;font-family:'DM Sans',sans-serif;}\n"
            + ".info-item select{cursor:pointer;appearance:none;padding-right:22px;background-image:url(\"data:image/svg+xml,%3Csvg xmlns='http://www.w3.org/2000/svg' width='10' height='10' viewBox='0 0 10 10'%3E%3Cpath fill='%2364748B' d='M5 7L0 2h10z'/%3E%3C/svg%3E\");background-repeat:no-repeat;background-position:right 7px center;}\n"
            + ".info-item select option{background:#1a2235;color:#F1F5F9;}\n"
            + ".card.editing .info-item select:not([disabled]){border-color:rgba(245,158,11,.5);background:rgba(245,158,11,.06);opacity:1;}");
        sb.AppendLine(".status-row{display:flex;align-items:center;gap:8px;padding:9px 12px;background:rgba(255,255,255,.03);border:1px solid var(--border);border-radius:8px;margin-bottom:12px;font-size:11px;color:var(--muted2);}");
        sb.AppendLine(".status-dot{width:7px;height:7px;border-radius:50%;flex-shrink:0;}");
        sb.AppendLine(".dot-ok{background:var(--green);box-shadow:0 0 6px var(--green);}.dot-warn{background:var(--amber);box-shadow:0 0 6px var(--amber);}");
        sb.AppendLine(".pm-btn{font-size:12px !important;padding:7px 13px !important;}");
        sb.AppendLine(".card-actions{padding:12px 16px;border-top:1px solid var(--border);background:rgba(0,0,0,.15);display:flex;flex-direction:column;gap:10px;}");
        sb.AppendLine(".ca-edit-row{display:flex;flex-wrap:wrap;gap:6px;}");
        sb.AppendLine(".ca-edit-row .btn{font-size:11px;padding:7px 12px;}");
        sb.AppendLine(".pm-section{display:flex;flex-direction:column;gap:6px;border-top:1px solid var(--border);padding-top:10px;}");
        sb.AppendLine(".pm-row{display:flex;align-items:center;gap:6px;}");
        sb.AppendLine(".pm-row-label{font-size:10px;font-weight:700;text-transform:uppercase;letter-spacing:.08em;color:var(--muted);min-width:60px;flex-shrink:0;}");
        sb.AppendLine(".pm-row .btn{font-size:11px;padding:7px 12px;flex-shrink:0;}");
        sb.AppendLine(".mini-form{border-top:1px solid var(--border);padding-top:12px;margin-top:4px;}");
        sb.AppendLine(".form-sep{font-size:10px;font-weight:700;text-transform:uppercase;letter-spacing:.1em;color:var(--accent);margin-bottom:8px;}");
        sb.AppendLine(".acts-list{display:flex;flex-direction:column;gap:3px;margin-bottom:4px;max-height:220px;overflow-y:auto;}");
        sb.AppendLine(".act-item{display:flex;align-items:flex-start;gap:8px;padding:5px 8px;border-radius:6px;cursor:pointer;transition:background .15s;font-size:12px;color:var(--muted2);}");
        sb.AppendLine(".act-item input[type=checkbox]{display:none;}");
        sb.AppendLine(".act-check{width:16px;height:16px;border-radius:4px;border:1.5px solid rgba(59,130,246,.3);background:var(--surface2);flex-shrink:0;margin-top:2px;display:flex;align-items:center;justify-content:center;font-size:10px;color:transparent;transition:all .15s;}");
        sb.AppendLine(".act-item input:checked ~ .act-check{background:var(--accent);border-color:var(--accent);color:white;}");
        sb.AppendLine(".act-item input:checked ~ .act-check::after{content:'\\2713';}");
        sb.AppendLine(".act-item input:checked ~ .act-text{color:var(--text);}");
        sb.AppendLine(".date-input{width:100%;background:var(--surface2);border:1px solid var(--border2);border-radius:6px;padding:8px 10px;font-size:12px;color:var(--text);color-scheme:dark;margin-bottom:8px;}");
        sb.AppendLine(".obs-label{font-size:9px;font-weight:600;text-transform:uppercase;letter-spacing:.1em;color:var(--muted);margin-bottom:4px;}");
        sb.AppendLine(".obs-edit-field{width:100%;background:var(--surface2);border:1px solid var(--border2);border-radius:6px;padding:8px 10px;font-size:12px;color:var(--text);resize:vertical;min-height:52px;}");
        sb.AppendLine("/* ── Modo edición ── */");
        sb.AppendLine(".card.editing{border-color:rgba(245,158,11,.55);box-shadow:0 0 0 2px rgba(245,158,11,.18);}");
        sb.AppendLine(".card.editing .card-top{background:rgba(245,158,11,.08);}");
        sb.AppendLine(".edit-mode-badge{display:none;align-items:center;gap:5px;padding:3px 10px;border-radius:999px;background:rgba(245,158,11,.15);border:1px solid rgba(245,158,11,.4);font-size:10px;font-weight:700;color:var(--amber);text-transform:uppercase;letter-spacing:.07em;animation:badgePulse 2s ease-in-out infinite;}");
        sb.AppendLine(".card.editing .edit-mode-badge{display:inline-flex;}");
        sb.AppendLine(".card.editing .info-item input:not([disabled]),.card.editing textarea:not([disabled]){border-color:rgba(245,158,11,.5);background:rgba(245,158,11,.06);opacity:1;}");
        sb.AppendLine("@keyframes badgePulse{0%,100%{opacity:1;box-shadow:0 0 0 0 rgba(245,158,11,.3)}50%{opacity:.8;box-shadow:0 0 0 5px rgba(245,158,11,0)}}");
        sb.AppendLine(".form-actions{display:flex;gap:8px;justify-content:flex-end;}");
        sb.AppendLine(".modal{display:none;position:fixed;inset:0;background:rgba(0,0,0,.7);backdrop-filter:blur(4px);justify-content:center;align-items:center;z-index:9999;}");
        sb.AppendLine(".modal.show{display:flex;}");
        sb.AppendLine(".modal-box{background:var(--surface);border:1px solid var(--border2);border-radius:16px;padding:28px;width:min(360px,95vw);}");
        sb.AppendLine(".modal-box h3{font-size:16px;font-weight:700;margin-bottom:6px;}.modal-box p{font-size:12px;color:var(--muted2);margin-bottom:20px;}");
        sb.AppendLine(".modal-field{margin-bottom:16px;}.modal-field label{font-size:10px;font-weight:600;text-transform:uppercase;color:var(--muted);display:block;margin-bottom:5px;}");
        sb.AppendLine(".modal-field input{width:100%;background:var(--surface2);border:1px solid var(--border2);border-radius:7px;padding:10px 12px;font-size:14px;color:var(--text);}");
        sb.AppendLine(".modal-field select{width:100%;background:var(--surface2);border:1px solid var(--border2);border-radius:7px;padding:10px 12px;font-size:14px;color:var(--text);}");
        sb.AppendLine(".modal-field-row{display:flex;gap:8px;align-items:flex-end;}");
        sb.AppendLine(".modal-field-row .modal-field{flex:1;margin-bottom:0;}");
        sb.AppendLine(".modal-footer{display:flex;gap:8px;justify-content:flex-end;}");
        sb.AppendLine(".toast{position:fixed;bottom:24px;right:24px;padding:12px 20px;border-radius:10px;font-size:13px;font-weight:600;z-index:99999;pointer-events:none;}");
        sb.AppendLine(".toast-ok{background:#052e16;border:1px solid #10B981;color:#6ee7b7;}");
        sb.AppendLine(".toast-err{background:#1f0000;border:1px solid #EF4444;color:#fca5a5;}");
        sb.AppendLine(".periodos-estado{display:flex;gap:8px;margin:8px 0;flex-wrap:wrap;}");
        sb.AppendLine(".periodo-badge{padding:4px 12px;border-radius:999px;font-size:11px;font-weight:600;font-family:'DM Mono',monospace;}");
        sb.AppendLine(".periodo-ok{background:rgba(16,185,129,.12);border:1px solid rgba(16,185,129,.4);color:#6ee7b7;}");
        sb.AppendLine(".periodo-pend{background:rgba(245,158,11,.10);border:1px solid rgba(245,158,11,.35);color:#fcd34d;}");
        // Dropdown de usuario
        sb.AppendLine(".user-chip{display:none;align-items:center;gap:8px;padding:7px 14px;border-radius:999px;background:rgba(16,185,129,.15);border:1px solid rgba(16,185,129,.3);font-size:12px;font-weight:600;color:#6ee7b7;cursor:pointer;position:relative;user-select:none;}");
        sb.AppendLine(".chip-arrow{font-size:9px;color:#6ee7b7;transition:transform .2s;}");
        sb.AppendLine(".user-chip.open .chip-arrow{transform:rotate(180deg);}");
        sb.AppendLine(".user-dropdown{display:none;position:absolute;top:calc(100% + 10px);right:0;min-width:190px;background:#111827;border:1px solid rgba(255,255,255,.13);border-radius:12px;box-shadow:0 16px 48px rgba(0,0,0,.6);padding:8px;z-index:999;}");
        sb.AppendLine(".user-chip.open .user-dropdown{display:block;}");
        sb.AppendLine(".drop-item{display:flex;align-items:center;gap:10px;padding:9px 12px;border-radius:8px;font-size:12px;font-weight:500;cursor:pointer;color:#94A3B8;background:none;border:none;width:100%;text-align:left;font-family:'DM Sans',sans-serif;transition:background .15s,color .15s;}");
        sb.AppendLine(".drop-item:hover{background:rgba(255,255,255,.06);color:#F1F5F9;}");
        sb.AppendLine(".drop-item.danger{color:#FCA5A5;}");
        sb.AppendLine(".drop-item.danger:hover{background:rgba(239,68,68,.1);color:#fff;}");
        sb.AppendLine(".drop-sep{height:1px;background:rgba(255,255,255,.07);margin:6px 0;}");
        sb.AppendLine("/* ── Requiere Correctivo ── */");
        sb.AppendLine(".act-correctivo{margin-top:10px;padding:10px 12px;border-radius:8px;background:rgba(239,68,68,.08);border:1px solid rgba(239,68,68,.35);transition:background .15s;}");
        sb.AppendLine(".act-correctivo:hover{background:rgba(239,68,68,.14);}");
        sb.AppendLine(".act-correctivo .act-text{color:#FCA5A5 !important;font-weight:700 !important;font-size:13px !important;}");
        sb.AppendLine(".act-correctivo .act-check{border-color:rgba(239,68,68,.5) !important;}");
        sb.AppendLine(".act-correctivo input:checked ~ .act-check{background:#EF4444 !important;border-color:#EF4444 !important;}");
        sb.AppendLine(".act-correctivo input:checked ~ .act-text{color:#ff8080 !important;}");
        sb.AppendLine(".qr-section{border-top:1px solid var(--border);padding-top:10px;margin-top:4px;}");
        sb.AppendLine(".qr-section .form-sep{font-size:10px;font-weight:700;text-transform:uppercase;letter-spacing:.1em;color:var(--accent);margin-bottom:6px;}");
        sb.AppendLine(".auto-tag{font-size:9px;font-weight:700;padding:1px 6px;border-radius:4px;background:rgba(59,130,246,.18);color:#60a5fa;letter-spacing:.06em;vertical-align:middle;margin-left:4px;}");
        sb.AppendLine(".input-auto{background:rgba(59,130,246,.06)!important;border-color:rgba(59,130,246,.3)!important;color:var(--muted2)!important;cursor:not-allowed!important;}");
        sb.AppendLine(".btn-baja{background:linear-gradient(135deg,#7c2d12,#ea580c);color:white;}");
        sb.AppendLine(".modal-baja-box{background:var(--surface);border:1px solid var(--border2);border-radius:16px;padding:28px;width:min(560px,95vw);max-height:90vh;overflow-y:auto;}");
        sb.AppendLine(".baja-grid{display:grid;grid-template-columns:1fr 1fr;gap:12px;margin-bottom:14px;}");
        sb.AppendLine(".baja-field{display:flex;flex-direction:column;gap:4px;}");
        sb.AppendLine(".baja-field label{font-size:9px;font-weight:600;text-transform:uppercase;letter-spacing:.1em;color:var(--muted);}");
        sb.AppendLine(".baja-field input,.baja-field select,.baja-field textarea{width:100%;background:var(--surface2);border:1px solid var(--border2);border-radius:7px;padding:8px 10px;font-size:13px;color:var(--text);font-family:'DM Sans',sans-serif;}");
        sb.AppendLine(".baja-field textarea{min-height:60px;resize:vertical;}");
        sb.AppendLine(".baja-field.full{grid-column:1/-1;}");
        sb.AppendLine(".baja-field input.auto-fill{background:rgba(59,130,246,.06);border-color:rgba(59,130,246,.3);color:var(--muted2);}");
        sb.AppendLine("</style></head><body>");
        sb.AppendLine("<div class=\"top-bar\">");
        sb.AppendLine("  <div class=\"top-icon\">🔧</div>");
        sb.AppendLine("  <div class=\"top-title\"><h1>Mantenimiento Preventivo</h1><p>📍 " + ubicacion + "</p></div>");
        sb.AppendLine("  <div class=\"user-chip\" id=\"userChip\" onclick=\"toggleChipQr()\">");
        sb.AppendLine("    👤 <span id=\"userNombre\"></span> <span class=\"chip-arrow\">▼</span>");
        sb.AppendLine("    <div class=\"user-dropdown\">");
        sb.AppendLine("      <button class=\"drop-item\" onclick=\"window.location.href='https://chi-it-control-center-csharpp.onrender.com/static/menu.html'\">🏠 &nbsp;Menu principal</button>");
        sb.AppendLine("      <div class=\"drop-sep\"></div>");
        sb.AppendLine("      <button class=\"drop-item danger\" onclick=\"cerrarSesionQr()\">⏻ &nbsp;Cerrar sesión</button>");
        sb.AppendLine("    </div>");
        sb.AppendLine("  </div>");
        sb.AppendLine("  <button class=\"btn btn-login\" id=\"btnLogin\" onclick=\"abrirLogin()\">🔑 Iniciar Sesión</button>");
        sb.AppendLine("  <button class=\"btn btn-print-all\" onclick=\"imprimirTodosQr()\">🖨️ Imprimir QR</button>");
        sb.AppendLine("  <button class=\"btn\" style=\"background:linear-gradient(135deg,#1e3a5f,#2563eb);color:white;font-size:12px;padding:8px 14px\" onclick=\"descargarFormatoPM()\">📥 Descargar Formato PM</button>");

        sb.AppendLine("</div>");
        sb.AppendLine("<div class=\"grid\">");
        sb.AppendLine(cardsHtml);
        sb.AppendLine("</div>");
        sb.AppendLine("<div class=\"modal\" id=\"modalLogin\">");
        sb.AppendLine("  <div class=\"modal-box\">");
        sb.AppendLine("    <h3>🔑 Iniciar Sesión</h3>");
        sb.AppendLine("    <p>Ingresa tus credenciales para habilitar el registro de preventivos</p>");
        sb.AppendLine("    <div class=\"modal-field\"><label>Usuario</label>");
        sb.AppendLine("    <input id=\"inputUsuario\" type=\"text\" placeholder=\"Ej: DOMINGUEZG\" autocomplete=\"off\" onkeydown=\"if(event.key===\'Enter\') document.getElementById(\'inputPassword\').focus()\"></div>");
        sb.AppendLine("    <div class=\"modal-field\"><label>Contraseña</label>");
        sb.AppendLine("    <input id=\"inputPassword\" type=\"password\" placeholder=\"Contraseña\" autocomplete=\"off\" onkeydown=\"if(event.key===\'Enter\') confirmarLogin()\"></div>");
        sb.AppendLine("    <div id=\"loginError\" style=\"color:#fca5a5;font-size:12px;margin-bottom:10px;display:none;\"></div>");
        sb.AppendLine("    <div class=\"modal-footer\">");
        sb.AppendLine("      <button class=\"btn btn-ghost\" onclick=\"cerrarLogin()\">Cancelar</button>");
        sb.AppendLine("      <button class=\"btn btn-primary\" id=\"btnConfirmar\" onclick=\"confirmarLogin()\">Entrar →</button>");
        sb.AppendLine("    </div>");
        sb.AppendLine("  </div>");
        sb.AppendLine("</div>");
        sb.AppendLine("<div class=\"modal\" id=\"modalRecal\">");
        sb.AppendLine("  <div class=\"modal-box\" style=\"width:min(820px,97vw);max-height:92vh;overflow-y:auto;\">");
        sb.AppendLine("    <h3>📍 Recalendarización</h3>");
        sb.AppendLine("    <p>Selecciona el tipo de recalendarización o mueve el dispositivo</p>");
        // ── Info del dispositivo ─────────────────────────────────────────────
        sb.AppendLine("    <div style=\"background:var(--surface2);border:1px solid var(--border2);border-radius:10px;padding:12px 16px;margin-bottom:16px;font-size:12px;\">");
        sb.AppendLine("      <div style=\"display:grid;grid-template-columns:repeat(4,1fr);gap:8px;\">");
        sb.AppendLine("        <div><span style=\"color:var(--muted);font-size:10px;text-transform:uppercase;letter-spacing:.06em;\">ID Equipo</span><br><b id=\"recal-id-equipo\" style=\"font-family:'DM Mono',monospace;color:var(--accent);\"></b></div>");
        sb.AppendLine("        <div><span style=\"color:var(--muted);font-size:10px;text-transform:uppercase;letter-spacing:.06em;\">Dispositivo</span><br><b id=\"recal-dispositivo\"></b></div>");
        sb.AppendLine("        <div><span style=\"color:var(--muted);font-size:10px;text-transform:uppercase;letter-spacing:.06em;\">Ubicación actual</span><br><b id=\"recal-ub-actual\" style=\"color:var(--amber);\"></b></div>");
        sb.AppendLine("        <div><span style=\"color:var(--muted);font-size:10px;text-transform:uppercase;letter-spacing:.06em;\">Planta</span><br><b id=\"recal-planta\"></b></div>");
        sb.AppendLine("      </div>");
        sb.AppendLine("    </div>");
        // ── Botones de tipo (3 columnas fijas, sin encimarse) ────────────────
        sb.AppendLine("    <div style=\"display:grid;grid-template-columns:repeat(3,1fr);gap:10px;margin-bottom:16px;\">");
        sb.AppendLine("      <button class=\"btn\" style=\"padding:12px 10px;font-size:12px;background:rgba(244,114,182,.15);border:1px solid rgba(244,114,182,.4);color:#f472b6;border-radius:8px;\" onclick=\"abrirStockModal()\">🗄️ Soporte Site (stock)</button>");
        sb.AppendLine("      <button class=\"btn\" style=\"padding:12px 10px;font-size:12px;background:rgba(99,102,241,.15);border:1px solid rgba(99,102,241,.4);color:#a5b4fc;border-radius:8px;\" onclick=\"abrirCambioPlantaModal()\">🏭 Cambio de planta</button>");
        sb.AppendLine("      <button class=\"btn\" id=\"btn-tipo-reparacion\" style=\"padding:12px 10px;font-size:12px;background:rgba(239,68,68,.15);border:1px solid rgba(239,68,68,.4);color:#fca5a5;border-radius:8px;\" onclick=\"toggleFormReparacion()\">🔧 Recalendarización por Reparación</button>");
        sb.AppendLine("    </div>");
        // ── Sub-formulario de Reparación ─────────────────────────────────────
        sb.AppendLine("    <div id=\"form-reparacion\" style=\"display:none;background:rgba(239,68,68,.06);border:1px solid rgba(239,68,68,.35);border-radius:10px;padding:16px;margin-bottom:16px;\">");
        sb.AppendLine("      <div style=\"font-size:13px;font-weight:700;color:#fca5a5;margin-bottom:12px;\">🔧 Datos de Reparación</div>");
        sb.AppendLine("      <div style=\"display:grid;grid-template-columns:1fr 1fr 1fr;gap:12px;margin-bottom:12px;\">");
        // Rack
        sb.AppendLine("        <div><label style=\"font-size:10px;font-weight:600;text-transform:uppercase;color:var(--muted);display:block;margin-bottom:4px;\">Rack <span style=\"color:#ef4444;\">*</span></label>");
        sb.AppendLine("          <select id=\"rep-rack\" style=\"width:100%;background:var(--surface2);border:1px solid var(--border2);border-radius:7px;padding:9px 10px;font-size:13px;color:var(--text);\" onchange=\"actualizarPreviewRep()\">");
        sb.AppendLine("            <option value=\"\">-- Rack --</option>");
        sb.AppendLine("            <optgroup label=\"Planta 1\"><option value=\"A\">A — Planta 1</option><option value=\"T\">T — Planta 1</option><option value=\"C\">C — Planta 1</option><option value=\"D\">D — Planta 1</option></optgroup>");
        sb.AppendLine("            <optgroup label=\"Planta 2\"><option value=\"E\">E — Planta 2</option></optgroup>");
        sb.AppendLine("            <optgroup label=\"Planta Satélite\"><option value=\"F\">F — Planta Satélite</option></optgroup>");
        sb.AppendLine("            <optgroup label=\"Planta Mixing\"><option value=\"G\">G — Planta Mixing</option></optgroup>");
        sb.AppendLine("          </select></div>");
        // Espacio
        sb.AppendLine("        <div><label style=\"font-size:10px;font-weight:600;text-transform:uppercase;color:var(--muted);display:block;margin-bottom:4px;\">Espacio <span style=\"color:#ef4444;\">*</span></label>");
        sb.AppendLine("          <select id=\"rep-espacio\" style=\"width:100%;background:var(--surface2);border:1px solid var(--border2);border-radius:7px;padding:9px 10px;font-size:13px;color:var(--text);\" onchange=\"actualizarPreviewRep()\">");
        sb.AppendLine("            <option value=\"\">-- Espacio --</option>");
        sb.AppendLine("            <option value=\"1\">1</option><option value=\"2\">2</option><option value=\"3\">3</option><option value=\"4\">4</option><option value=\"5\">5</option><option value=\"6\">6</option>");
        sb.AppendLine("          </select></div>");
        // ID Préstamo
        sb.AppendLine("        <div><label style=\"font-size:10px;font-weight:600;text-transform:uppercase;color:var(--muted);display:block;margin-bottom:4px;\">ID Dispositivo Préstamo <span style=\"color:#ef4444;\">*</span></label>");
        sb.AppendLine("          <input id=\"rep-id-prestamo\" type=\"text\" placeholder=\"ID del equipo de préstamo\" style=\"width:100%;background:var(--surface2);border:1px solid var(--border2);border-radius:7px;padding:9px 10px;font-size:13px;color:var(--text);\">");
        sb.AppendLine("          <span style=\"font-size:10px;color:var(--muted2);\">Se duplica la tarjeta con este ID</span></div>");
        sb.AppendLine("      </div>");
        // Preview ubicación
        sb.AppendLine("      <div id=\"rep-preview\" style=\"display:none;background:rgba(245,158,11,.08);border:1px solid rgba(245,158,11,.3);border-radius:7px;padding:8px 12px;margin-bottom:10px;font-size:12px;color:var(--amber);\">📍 Ubicación generada: <b id=\"rep-preview-txt\"></b></div>");
        sb.AppendLine("      <div id=\"rep-error\" style=\"display:none;color:#fca5a5;font-size:12px;margin-bottom:8px;\"></div>");
        sb.AppendLine("      <div style=\"display:flex;gap:8px;justify-content:flex-end;\">");
        sb.AppendLine("        <button class=\"btn btn-ghost\" onclick=\"cerrarFormReparacion()\">✕ Cancelar</button>");
        sb.AppendLine("        <button class=\"btn btn-danger\" id=\"btnGuardarRep\" onclick=\"guardarReparacion()\">💾 Guardar Reparación</button>");
        sb.AppendLine("      </div>");
        sb.AppendLine("    </div>");
        // ── Campo de nueva ubicación libre ───────────────────────────────────
        sb.AppendLine("    <div class=\"modal-field\">");
        sb.AppendLine("      <label>📍 Nueva ubicación (libre)</label>");
        sb.AppendLine("      <div class=\"modal-field-row\" style=\"margin-bottom:0;\">");
        sb.AppendLine("        <div class=\"modal-field\" style=\"flex:1;margin-bottom:0;\">");
        sb.AppendLine("          <input id=\"recal-nueva-ub\" list=\"recal-ub-list\" type=\"text\" placeholder=\"Escribe o selecciona la ubicación\" autocomplete=\"off\">");
        sb.AppendLine("          <datalist id=\"recal-ub-list\"></datalist>");
        sb.AppendLine("        </div>");
        sb.AppendLine("      </div>");
        sb.AppendLine("    </div>");
        sb.AppendLine("    <!-- Paso 2: si está ocupada -->");
        sb.AppendLine("    <div id=\"recal-paso2\" style=\"display:none;margin-bottom:14px;padding:12px;border-radius:10px;background:rgba(239,68,68,.08);border:1px solid rgba(239,68,68,.3);\">");
        sb.AppendLine("      <div style=\"font-size:12px;font-weight:700;color:#fca5a5;margin-bottom:8px;\">⚠️ Esa ubicación ya tiene un dispositivo asignado</div>");
        sb.AppendLine("      <div style=\"font-size:11px;color:var(--muted2);margin-bottom:10px;\">Dispositivo existente: <b id=\"recal-ocupante-info\" style=\"color:var(--text);\"></b></div>");
        sb.AppendLine("      <label style=\"font-size:10px;font-weight:600;text-transform:uppercase;color:var(--muted);display:block;margin-bottom:5px;\">📍 ¿A dónde mover el dispositivo existente?</label>");
        sb.AppendLine("      <input id=\"recal-ub-ocupante\" list=\"recal-ub-list2\" type=\"text\" placeholder=\"Nueva ubicación del dispositivo existente\" autocomplete=\"off\" style=\"width:100%;background:var(--surface2);border:1px solid var(--border2);border-radius:7px;padding:10px 12px;font-size:14px;color:var(--text);\">");
        sb.AppendLine("      <datalist id=\"recal-ub-list2\"></datalist>");
        sb.AppendLine("    </div>");
        sb.AppendLine("    <div id=\"recal-error\" style=\"color:#fca5a5;font-size:12px;margin-bottom:10px;display:none;\"></div>");
        sb.AppendLine("    <div class=\"modal-footer\">");
        sb.AppendLine("      <button class=\"btn btn-ghost\" onclick=\"cerrarRecal()\">Cerrar</button>");
        sb.AppendLine("      <button class=\"btn btn-primary\" id=\"btnRecalConfirmar\" onclick=\"confirmarRecal()\">Confirmar ubicación →</button>");
        sb.AppendLine("    </div>");
        sb.AppendLine("  </div>");
        sb.AppendLine("<!-- Modal: Cambio de planta -->");
        sb.AppendLine("<div class=\"modal\" id=\"modalCambioPlanta\">");
        sb.AppendLine("  <div class=\"modal-box\" style=\"width:min(360px,95vw);\">");
        sb.AppendLine("    <h3>🏭 Cambio de planta</h3>");
        sb.AppendLine("    <p>Selecciona la nueva planta para este dispositivo</p>");
        sb.AppendLine("    <div class=\"modal-field\">");
        sb.AppendLine("      <label>Planta</label>");
        sb.AppendLine("      <select id=\"cp-planta\">");
        sb.AppendLine("        <option value=\"\">-- Selecciona --</option>");
        sb.AppendLine("        <option value=\"B1\">B1</option>");
        sb.AppendLine("        <option value=\"B2\">B2</option>");
        sb.AppendLine("        <option value=\"Planta Satelite\">Planta Satelite</option>");
        sb.AppendLine("        <option value=\"Planta Mixing\">Planta Mixing</option>");
        sb.AppendLine("        <option value=\"Bodega\">Bodega</option>");
        sb.AppendLine("      </select>");
        sb.AppendLine("    </div>");
        sb.AppendLine("    <div id=\"cp-error\" style=\"color:#fca5a5;font-size:12px;margin-bottom:10px;display:none;\"></div>");
        sb.AppendLine("    <div class=\"modal-footer\">");
        sb.AppendLine("      <button class=\"btn btn-ghost\" onclick=\"cerrarCambioPlantaModal()\">Cancelar</button>");
        sb.AppendLine("      <button class=\"btn btn-primary\" id=\"btnCpGuardar\" onclick=\"guardarCambioPlanta()\">💾 Guardar</button>");
        sb.AppendLine("    </div>");
        sb.AppendLine("  </div>");
        sb.AppendLine("</div>");
        sb.AppendLine("<!-- Modal: Stock Soporte Site -->");
        sb.AppendLine("<div class=\"modal\" id=\"modalStock\">");
        sb.AppendLine("  <div class=\"modal-box\" style=\"width:min(400px,95vw);\">");
        sb.AppendLine("    <h3>🗄️ Mover a Soporte Site (stock)</h3>");
        sb.AppendLine("    <p>Selecciona el rack y el espacio donde se colocará el equipo</p>");
        sb.AppendLine("    <div class=\"modal-field\">");
        sb.AppendLine("      <label>Rack</label>");
        sb.AppendLine("      <select id=\"stock-rack\">");
        sb.AppendLine("        <option value=\"A\">A (Planta 1)</option>");
        sb.AppendLine("        <option value=\"T\">T (Planta 1)</option>");
        sb.AppendLine("        <option value=\"C\">C (Planta 1)</option>");
        sb.AppendLine("        <option value=\"D\">D (Planta 1)</option>");
        sb.AppendLine("        <option value=\"E\">E (Planta 2)</option>");
        sb.AppendLine("        <option value=\"F\">F (Planta satelite)</option>");
        sb.AppendLine("        <option value=\"G\">G (Planta mixing)</option>");
        sb.AppendLine("      </select>");
        sb.AppendLine("    </div>");
        sb.AppendLine("    <div class=\"modal-field\">");
        sb.AppendLine("      <label>Espacio</label>");
        sb.AppendLine("      <select id=\"stock-espacio\">");
        sb.AppendLine("        <option value=\"1\">1</option>");
        sb.AppendLine("        <option value=\"2\">2</option>");
        sb.AppendLine("        <option value=\"3\">3</option>");
        sb.AppendLine("        <option value=\"4\">4</option>");
        sb.AppendLine("        <option value=\"5\">5</option>");
        sb.AppendLine("        <option value=\"6\">6</option>");
        sb.AppendLine("      </select>");
        sb.AppendLine("    </div>");
        sb.AppendLine("    <div id=\"stock-error\" style=\"color:#fca5a5;font-size:12px;margin-bottom:10px;display:none;\"></div>");
        sb.AppendLine("    <div class=\"modal-footer\">");
        sb.AppendLine("      <button class=\"btn btn-ghost\" onclick=\"cerrarStockModal()\">Cancelar</button>");
        sb.AppendLine("      <button class=\"btn btn-primary\" id=\"btnStockGuardar\" onclick=\"guardarStock()\">💾 Guardar</button>");
        sb.AppendLine("    </div>");
        sb.AppendLine("  </div>");
        sb.AppendLine("</div>");
        sb.AppendLine("</div>");
        sb.AppendLine("<script>");
        sb.AppendLine("let usuarioActual=null,nombreActual=null,usuarioTarjeta={};");
        sb.AppendLine("function toggleChipQr(){document.getElementById('userChip').classList.toggle('open');}");
        sb.AppendLine("document.addEventListener('click',e=>{const c=document.getElementById('userChip');if(c&&!c.contains(e.target))c.classList.remove('open');});");
        sb.AppendLine("async function cerrarSesionQr(){try{await fetch('/LOGOUT',{method:'POST',credentials:'include'});}catch(e){}usuarioActual=null;nombreActual=null;document.getElementById('userChip').style.display='none';document.getElementById('btnLogin').style.display='inline-flex';document.querySelectorAll('.pm-section').forEach(s=>s.style.display='none');toast('Sesión cerrada',true);}");
        sb.AppendLine("function abrirLogin(){document.getElementById('modalLogin').classList.add('show');setTimeout(()=>document.getElementById('inputUsuario').focus(),100);}");
        sb.AppendLine("function cerrarLogin(){");
        sb.AppendLine("  document.getElementById('modalLogin').classList.remove('show');");
        sb.AppendLine("  document.getElementById('inputUsuario').value='';");
        sb.AppendLine("  document.getElementById('inputPassword').value='';");
        sb.AppendLine("  document.getElementById('loginError').style.display='none';");
        sb.AppendLine("}");
        sb.AppendLine("async function confirmarLogin(){");
        sb.AppendLine("  const usr=document.getElementById('inputUsuario').value.trim().toUpperCase();");
        sb.AppendLine("  const pwd=document.getElementById('inputPassword').value;");
        sb.AppendLine("  const errEl=document.getElementById('loginError');");
        sb.AppendLine("  errEl.style.display='none';");
        sb.AppendLine("  if(!usr||!pwd){errEl.textContent='Ingresa usuario y contraseña';errEl.style.display='block';return;}");
        sb.AppendLine("  const btn=document.getElementById('btnConfirmar');");
        sb.AppendLine("  btn.disabled=true;btn.textContent='Verificando...';");
        sb.AppendLine("  const res=await fetch('/LOGIN',{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify({usuario:usr,password:pwd})});");
        sb.AppendLine("  const data=await res.json();");
        sb.AppendLine("  btn.disabled=false;btn.textContent='Entrar →';");
        sb.AppendLine("  if(!data.ok){errEl.textContent=data.mensaje||'Credenciales incorrectas';errEl.style.display='block';return;}");
        sb.AppendLine("  usuarioActual=data.usuario;nombreActual=data.nombre||data.usuario;");
        sb.AppendLine("  const esAdmin=data.rol==='ADMIN';");
        sb.AppendLine("  document.getElementById('userNombre').textContent=nombreActual;");
        sb.AppendLine("  document.getElementById('userChip').style.display='flex';");
        sb.AppendLine("  document.getElementById('btnLogin').style.display='none';");
        sb.AppendLine("  document.querySelectorAll('.card').forEach(card=>{");
        sb.AppendLine("    const p1=card.dataset.tienePm==='true';const p2=card.dataset.tienePm2==='true';");
        sb.AppendLine("    const sec=card.querySelector('.pm-section');if(sec)sec.style.display='flex';");
        sb.AppendLine("    const b1hacer=card.querySelector('.btn-p1');const b2hacer=card.querySelector('.btn-p2');");
        sb.AppendLine("    const b1ver=card.querySelector('.btn-ver1');const b1edit=card.querySelector('.btn-edit1');const b1del=card.querySelector('.btn-del1');");
        sb.AppendLine("    const b2ver=card.querySelector('.btn-ver2');const b2edit=card.querySelector('.btn-edit2');const b2del=card.querySelector('.btn-del2');");
        sb.AppendLine("    if(b1hacer)b1hacer.style.display=p1?'none':'inline-flex';");
        sb.AppendLine("    if(b1ver)b1ver.style.display=p1?'inline-flex':'none';if(b1edit)b1edit.style.display=p1?'inline-flex':'none';");
        sb.AppendLine("    if(b1del)b1del.style.display=(p1&&esAdmin)?'inline-flex':'none';");
        sb.AppendLine("    if(b2hacer)b2hacer.style.display=p2?'none':'inline-flex';");
        sb.AppendLine("    if(b2ver)b2ver.style.display=p2?'inline-flex':'none';if(b2edit)b2edit.style.display=p2?'inline-flex':'none';");
        sb.AppendLine("    if(b2del)b2del.style.display=(p2&&esAdmin)?'inline-flex':'none';");
        sb.AppendLine("  });");
        sb.AppendLine("  cerrarLogin();toast('Sesión iniciada — '+nombreActual,true);");
        sb.AppendLine("  // Mostrar botones de recalendarización");
        sb.AppendLine("  document.querySelectorAll('[id^=\"btn_recal_\"]').forEach(b=>b.style.display='inline-flex');");
        sb.AppendLine("}");
        sb.AppendLine("function abrirForm(id,p){if(!usuarioActual){abrirLogin();return;}document.getElementById('form'+p+'_'+id).style.display='block';document.getElementById('fecha'+p+'_'+id).value=new Date().toISOString().split('T')[0];}");
        sb.AppendLine("function cancelarForm(id,p){document.getElementById('form'+p+'_'+id).style.display='none';document.querySelectorAll('#form'+p+'_'+id+' input[type=checkbox]').forEach(cb=>cb.checked=false);}");
        sb.AppendLine("async function guardarPreventivo(id,p){");
        sb.AppendLine("  const fecha=document.getElementById('fecha'+p+'_'+id).value;");
        sb.AppendLine("  if(!fecha){toast('Selecciona la fecha',false);return;}");
        sb.AppendLine("  const cbs=document.querySelectorAll('#form'+p+'_'+id+' .acts-list input[type=checkbox]');");
        sb.AppendLine("  const checks=[];cbs.forEach((cb,i)=>{if(cb.checked)checks.push(i);});");
        sb.AppendLine("  if(!checks.length){toast('Marca al menos una actividad',false);return;}");
        sb.AppendLine("  const obs=document.getElementById('obs_pm'+p+'_'+id)?.value||'';");
        sb.AppendLine("  const reqCorr=document.getElementById('req_correctivo'+p+'_'+id)?.checked||false;");
        sb.AppendLine("  const btn=document.querySelector('#form'+p+'_'+id+' .btn-success');");
        sb.AppendLine("  btn.disabled=true;btn.textContent='Guardando...';");
        sb.AppendLine("  try{");
        sb.AppendLine("    const endpoint=p===2?'/PREVENTIVO/GUARDAR_PM_P2/'+id:'/PREVENTIVO/GUARDAR_PM/'+id;");
        sb.AppendLine("    const res=await fetch(endpoint,{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify({usuario:usuarioActual,fecha,checks,observaciones:obs,requiere_correctivo:reqCorr})});");
        sb.AppendLine("    const data=await res.json();");
        sb.AppendLine("    if(data.ok){");
        sb.AppendLine("      toast('P'+p+' guardado. Próximo: '+data.proximo_pm,true);");
        sb.AppendLine("      const card=document.getElementById('form'+p+'_'+id).closest('.card');");
        sb.AppendLine("      cancelarForm(id,p);");
        sb.AppendLine("      const badge=document.getElementById('pbadge'+p+'_'+id);");
        sb.AppendLine("      if(badge){badge.textContent='📋 P'+p+': ✅ Registrado';badge.className='periodo-badge periodo-ok';}");
        sb.AppendLine("      const plazoEl=document.getElementById(p===2?'plazo_p2_'+id:'plazo_'+id);if(plazoEl&&data.proximo_pm){plazoEl.textContent=data.proximo_pm;}");
        sb.AppendLine("      if(p===1){card.dataset.tienePm='true';card.querySelector('.btn-p1').style.display='none';card.querySelector('.btn-ver1').style.display='inline-flex';card.querySelector('.btn-edit1').style.display='inline-flex';card.querySelector('.btn-del1').style.display='inline-flex';}");
        sb.AppendLine("      else{card.dataset.tienePm2='true';card.querySelector('.btn-p2').style.display='none';card.querySelector('.btn-ver2').style.display='inline-flex';card.querySelector('.btn-edit2').style.display='inline-flex';card.querySelector('.btn-del2').style.display='inline-flex';}");
        sb.AppendLine("    }else{toast('Error: '+(data.error||'desconocido'),false);}");
        sb.AppendLine("  }catch(err){toast('Error de conexión. Intenta de nuevo.',false);}");
        sb.AppendLine("  finally{btn.disabled=false;btn.textContent='💾 Guardar P'+p;}");
        sb.AppendLine("}");
        sb.AppendLine("async function verPM(id,p){");
        sb.AppendLine("  const endpoint=p===2?'/PREVENTIVO/DIGITAL_P2/'+id:'/PREVENTIVO/DIGITAL/'+id;const res=await fetch(endpoint);const data=await res.json();");
        sb.AppendLine("  if(!data.existe){toast('No hay PM de P'+p+' guardado',false);return;}");
        sb.AppendLine("  const pm=data.data;");
        sb.AppendLine("  const actsEl=document.getElementById('ver_acts'+p+'_'+id);");
        sb.AppendLine("  const allActs=Array.from(document.querySelectorAll('#edit_acts'+p+'_'+id+' .act-text')).map(e=>e.textContent);");
        sb.AppendLine("  actsEl.innerHTML='';");
        sb.AppendLine("  allActs.forEach((act,i)=>{const m=pm.checks&&pm.checks.includes(i);actsEl.innerHTML+='<div class=\"act-item\" style=\"opacity:'+(m?1:.35)+'\"><span class=\"act-check\" style=\"'+(m?'background:var(--accent);border-color:var(--accent);color:white':'')+'\">'+( m?'\\u2713':'')+' </span><span class=\"act-text\">'+act+'</span></div>';});");
        sb.AppendLine("  document.getElementById('ver_usuario'+p+'_'+id).textContent=pm.usuario||'—';");
        sb.AppendLine("  document.getElementById('ver_fecha'+p+'_'+id).textContent=pm.fecha||'—';");
        sb.AppendLine("  document.getElementById('ver_proximo'+p+'_'+id).textContent=pm.proximo_pm||'—';");
        sb.AppendLine("  const o=document.getElementById('ver_obs'+p+'_'+id);if(o)o.textContent=pm.observaciones?'📝 '+pm.observaciones:'';");
        sb.AppendLine("  const rc=document.getElementById('ver_correctivo'+p+'_'+id);if(rc){rc.style.display=pm.requiere_correctivo?'block':'none';}");
        sb.AppendLine("  document.getElementById('ver'+p+'_'+id).style.display='block';");
        sb.AppendLine("}");
        sb.AppendLine("function cerrarVer(id,p){document.getElementById('ver'+p+'_'+id).style.display='none';}");
        sb.AppendLine("async function abrirEditarPM(id,p){");
        sb.AppendLine("  if(!usuarioActual){abrirLogin();return;}");
        sb.AppendLine("  const endpoint=p===2?'/PREVENTIVO/DIGITAL_P2/'+id:'/PREVENTIVO/DIGITAL/'+id;const res=await fetch(endpoint);const data=await res.json();");
        sb.AppendLine("  const cbs=document.querySelectorAll('#edit_acts'+p+'_'+id+' input[type=checkbox]');");
        sb.AppendLine("  cbs.forEach(cb=>cb.checked=false);");
        sb.AppendLine("  if(data.existe&&data.data.checks)data.data.checks.forEach(i=>{if(cbs[i])cbs[i].checked=true;});");
        sb.AppendLine("  const f=document.getElementById('edit_fecha'+p+'_'+id);if(f)f.value=data.existe?(data.data.fecha||''):'';");
        sb.AppendLine("  const o=document.getElementById('edit_obs_pm'+p+'_'+id);if(o)o.value=data.existe?(data.data.observaciones||''):'';");
        sb.AppendLine("  const rc=document.getElementById('req_correctivo_edit'+p+'_'+id);if(rc)rc.checked=data.existe?(data.data.requiere_correctivo||false):false;");
        sb.AppendLine("  document.getElementById('edit_pm'+p+'_'+id).style.display='block';");
        sb.AppendLine("}");
        sb.AppendLine("function cerrarEditarPM(id,p){document.getElementById('edit_pm'+p+'_'+id).style.display='none';}");
        sb.AppendLine("async function guardarEditarPM(id,p){");
        sb.AppendLine("  const fecha=document.getElementById('edit_fecha'+p+'_'+id).value;");
        sb.AppendLine("  if(!fecha){toast('Selecciona la fecha',false);return;}");
        sb.AppendLine("  const cbs=document.querySelectorAll('#edit_acts'+p+'_'+id+' input[type=checkbox]');");
        sb.AppendLine("  const checks=[];cbs.forEach((cb,i)=>{if(cb.checked)checks.push(i);});");
        sb.AppendLine("  if(!checks.length){toast('Marca al menos una actividad',false);return;}");
        sb.AppendLine("  const obs=document.getElementById('edit_obs_pm'+p+'_'+id).value;");
        sb.AppendLine("  const reqCorr=document.getElementById('req_correctivo_edit'+p+'_'+id)?.checked||false;");
        sb.AppendLine("  const btn=document.querySelector('#edit_pm'+p+'_'+id+' .btn-amber');");
        sb.AppendLine("  btn.disabled=true;btn.textContent='Guardando...';");
        sb.AppendLine("  try{");
        sb.AppendLine("    const endpoint=p===2?'/PREVENTIVO/GUARDAR_PM_P2/'+id:'/PREVENTIVO/GUARDAR_PM/'+id;");
        sb.AppendLine("    const res=await fetch(endpoint,{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify({usuario:usuarioActual,fecha,checks,observaciones:obs,requiere_correctivo:reqCorr})});");
        sb.AppendLine("    const data=await res.json();");
        sb.AppendLine("    if(data.ok){");
        sb.AppendLine("      toast('P'+p+' actualizado. Próximo: '+data.proximo_pm,true);");
        sb.AppendLine("      const badge=document.getElementById('pbadge'+p+'_'+id);if(badge){badge.textContent='📋 P'+p+': ✅ Registrado';badge.className='periodo-badge periodo-ok';}");
        sb.AppendLine("      const plazoEl=document.getElementById(p===2?'plazo_p2_'+id:'plazo_'+id);if(plazoEl&&data.proximo_pm){plazoEl.textContent=data.proximo_pm;}");
        sb.AppendLine("      cerrarEditarPM(id,p);");
        sb.AppendLine("    }else{toast('Error: '+(data.error||'desconocido'),false);}");
        sb.AppendLine("  }catch(err){toast('Error de conexión. Intenta de nuevo.',false);}");
        sb.AppendLine("  finally{btn.disabled=false;btn.textContent='💾 Guardar P'+p;}");
        sb.AppendLine("}");
        sb.AppendLine("async function eliminarPreventivo(id,p){");
        sb.AppendLine("  if(!confirm('Eliminar el PM de Período '+p+'?'))return;");
        sb.AppendLine("  const endpoint=p===2?'/PREVENTIVO/ELIMINAR_DIGITAL_P2/'+id:'/PREVENTIVO/ELIMINAR_DIGITAL/'+id;");
        sb.AppendLine("  const res=await fetch(endpoint,{method:'DELETE'});const data=await res.json();");
        sb.AppendLine("  if(data.ok){");
        sb.AppendLine("    toast('PM P'+p+' eliminado',true);");
        sb.AppendLine("    document.getElementById('ver'+p+'_'+id).style.display='none';");
        sb.AppendLine("    document.getElementById('edit_pm'+p+'_'+id).style.display='none';");
        sb.AppendLine("    const badge=document.getElementById('pbadge'+p+'_'+id);if(badge){badge.textContent='📋 P'+p+': ⏳ Pendiente';badge.className='periodo-badge periodo-pend';}");
        sb.AppendLine("    const plazoEl=document.getElementById(p===2?'plazo_p2_'+id:'plazo_'+id);if(plazoEl)plazoEl.textContent='Sin PM';");
        sb.AppendLine("    const card=document.getElementById('btn_del'+p+'_'+id).closest('.card');");
        sb.AppendLine("    if(p===1){card.dataset.tienePm='false';card.querySelector('.btn-p1').style.display='inline-flex';card.querySelector('.btn-ver1').style.display='none';card.querySelector('.btn-edit1').style.display='none';card.querySelector('.btn-del1').style.display='none';}");
        sb.AppendLine("    else{card.dataset.tienePm2='false';card.querySelector('.btn-p2').style.display='inline-flex';card.querySelector('.btn-ver2').style.display='none';card.querySelector('.btn-edit2').style.display='none';card.querySelector('.btn-del2').style.display='none';}");
        sb.AppendLine("  }else toast('Error al eliminar',false);");
        sb.AppendLine("}");
        sb.AppendLine("function abrirEditar(id){if(!usuarioActual){abrirLogin();return;}usuarioTarjeta[id]=usuarioActual;['equipo_','disp_','planta_','color_','anio_vis_'].forEach(p=>{const el=document.getElementById(p+id);if(el)el.disabled=false;});document.getElementById('obs_'+id).disabled=false;const card=document.getElementById('btn_hacer_'+id)?.closest('.card')||document.getElementById('equipo_'+id)?.closest('.card');if(card)card.classList.add('editing');}");
        sb.AppendLine("function cancelarEditar(id){['equipo_','disp_','planta_','color_','anio_vis_'].forEach(p=>{const el=document.getElementById(p+id);if(el)el.disabled=true;});document.getElementById('obs_'+id).disabled=true;const card=document.getElementById('equipo_'+id)?.closest('.card');if(card)card.classList.remove('editing');}");
        sb.AppendLine("function cancelarTodo(id){");
        sb.AppendLine("  // Cerrar formularios de registro");
        sb.AppendLine("  [1,2].forEach(p=>{");
        sb.AppendLine("    const f=document.getElementById('form'+p+'_'+id);if(f&&f.style.display!=='none'){f.style.display='none';f.querySelectorAll('input[type=checkbox]').forEach(cb=>cb.checked=false);}");
        sb.AppendLine("    const v=document.getElementById('ver'+p+'_'+id);if(v)v.style.display='none';");
        sb.AppendLine("    const e=document.getElementById('edit_pm'+p+'_'+id);if(e)e.style.display='none';");
        sb.AppendLine("  });");
        sb.AppendLine("  // Cancelar modo edición si está activo");
        sb.AppendLine("  cancelarEditar(id);");
        sb.AppendLine("}");
        // (guardarCorrectivo is defined below in the main script block)
        sb.AppendLine("async function guardarCambios(id){");
        sb.AppendLine("  const anioVal=document.getElementById('anio_vis_'+id)?.value;  const datos={ID_EQUIPO:document.getElementById('equipo_'+id).value,UBICACION:document.getElementById('ubicacion_'+id).value,nombre_dispositivo:document.getElementById('disp_'+id).value,PLANTA:document.getElementById('planta_'+id).value,CATEGORIA_COLOR:document.getElementById('color_'+id).value,OBSERVACIONES:document.getElementById('obs_'+id).value,ANIO_CREACION:anioVal?parseInt(anioVal):null};");
        sb.AppendLine("  const usuario=usuarioTarjeta[id]||usuarioActual||'SISTEMA';");
        sb.AppendLine("  const res=await fetch('/PREVENTIVO/'+id+'?usuario='+encodeURIComponent(usuario),{method:'PUT',headers:{'Content-Type':'application/json','X-Usuario':usuario},body:JSON.stringify(datos)});");
        sb.AppendLine("  const data=await res.json();");
        sb.AppendLine("  if(data.mensaje){");
        sb.AppendLine("    toast('Cambios guardados',true);");
        sb.AppendLine("    // Actualizar el encabezado de la tarjeta en tiempo real");
        sb.AppendLine("    const card=document.getElementById('equipo_'+id).closest('.card');");
        sb.AppendLine("    const h3=card.querySelector('.dev-name h3');if(h3)h3.textContent=datos.nombre_dispositivo;");
        sb.AppendLine("    const idSpan=card.querySelector('.dev-name span');if(idSpan)idSpan.textContent=datos.ID_EQUIPO;");
        sb.AppendLine("    const colorMap={'verde':['#10B981','#052e16','Verde'],'amarillo':['#F59E0B','#1c1400','Amarillo'],'rojo':['#EF4444','#1f0000','Rojo'],'gris':['#94A3B8','#0f172a','Gris'],'rosa':['#F472B6','#1f0011','Rosa'],'azul':['#3B82F6','#001233','Azul']};");
        sb.AppendLine("    const cb=card.querySelector('.color-badge');if(cb){const cm=colorMap[datos.CATEGORIA_COLOR.toLowerCase()];if(cm){cb.style.background=cm[1];cb.style.color=cm[0];cb.style.borderColor=cm[0]+'40';cb.textContent=cm[2];}}");
        sb.AppendLine("    cancelarEditar(id);");
        sb.AppendLine("  }else toast('Error al guardar',false);");
        sb.AppendLine("}");

        // ── Descarga de formato IT-FO-002 embebido ───────────────────────────
        sb.AppendLine(@"
function descargarFormatoPM(){
  var b64 = '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AJSBrq6uCQCAYP4GzcxItA0KZW5kc3RyZWFtDQplbmRvYmoNCjEyIDAgb2JqDQo8PC9UeXBlL1hPYmplY3QvU3VidHlwZS9JbWFnZS9XaWR0aCAyOTEvSGVpZ2h0IDIyMC9Db2xvclNwYWNlL0RldmljZUdyYXkvTWF0dGVbIDAgMCAwXSAvQml0c1BlckNvbXBvbmVudCA4L0ludGVycG9sYXRlIGZhbHNlL0ZpbHRlci9GbGF0ZURlY29kZS9MZW5ndGggNDEzPj4NCnN0cmVhbQ0KeJzt0IEAACAQADHmfJ4gtCR6hBPYEHYu5RMUJUVJUVKUFCVFSVFSlBQlRUlRUpQUJUVJUVKUFCVFSVFSlBQlRUlRUpQUJUVJUVKUFCVFSVFSlBQlRUlRUpQUJUVJUVKUFCVFSVFSlBQlRUlRUpQUJUVJUVKUFCVFSVFSlBQlRUlRUpQUJUVJUVKUFCVFSVFSlBQlRUlRUpQUJUVJUVKUFCVFSVFSlBQlRUlRUpQUJUVJUVKUFCVFSVFSlBQlRUlRUpQUJUVJUVKUFCVFSVFSlBQlRUlRUpQUJUVJUVKUFCVFSVFSlBQlRUlRUpQUJUVJUVKUFCVFSVFSlBQlRUlRUpQUJUVJUVKUFCVFSVFSlBQlRUlRUpQUJUVJUVKUFCVFSVFSlBQlRUlRUpQUJUVJUVKUFCVFSVFSlBQlRUlRUpQUJUVJUVKUFCVFSVFSlBQlRUlRUpQUJUVJUVKUFCVFSVFSlBQlRUlRUpQUJUVJUVKUFCVFSVFSlBQlRUlRUpQUJUVJUVKUFCVFSVFSlBQlRUlRUpQUpXmEBRddZw4NCmVuZHN0cmVhbQ0KZW5kb2JqDQoxMyAwIG9iag0KPDwvVHlwZS9YT2JqZWN0L1N1YnR5cGUvSW1hZ2UvV2lkdGggODQ0L0hlaWdodCAxMjkvQ29sb3JTcGFjZS9EZXZpY2VSR0IvQml0c1BlckNvbXBvbmVudCA4L0ludGVycG9sYXRlIGZhbHNlL1NNYXNrIDE0IDAgUi9GaWx0ZXIvRmxhdGVEZWNvZGUvTGVuZ3RoIDMzOD4+DQpzdHJlYW0NCnic7cEBAQAAAIIg/69uSEABAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA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DQplbmRvYmoNCjgzIDAgb2JqDQo8PC9PL0xpc3QvTGlzdE51bWJlcmluZy9EZWNpbWFsPj4NCmVuZG9iag0KMTAwIDAgb2JqDQo8PC9PL0xpc3QvTGlzdE51bWJlcmluZy9EZWNpbWFsPj4NCmVuZG9iag0KMTE2IDAgb2JqDQo8PC9PL0xpc3QvTGlzdE51bWJlcmluZy9EZWNpbWFsPj4NCmVuZG9iag0KMTMyIDAgb2JqDQo8PC9PL0xpc3QvTGlzdE51bWJlcmluZy9EZWNpbWFsPj4NCmVuZG9iag0KMTQ4IDAgb2JqDQo8PC9PL0xpc3QvTGlzdE51bWJlcmluZy9EZWNpbWFsPj4NCmVuZG9iag0KMTY0IDAgb2JqDQo8PC9PL0xpc3QvTGlzdE51bWJlcmluZy9EZWNpbWFsPj4NCmVuZG9iag0KMTgxIDAgb2JqDQo8PC9PL0xpc3QvTGlzdE51bWJlcmluZy9EZWNpbWFsPj4NCmVuZG9iag0KMTk3IDAgb2JqDQo8PC9PL0xpc3QvTGlzdE51bWJlcmluZy9EZWNpbWFsPj4NCmVuZG9iag0KMjEzIDAgb2JqDQo8PC9PL0xpc3QvTGlzdE51bWJlcmluZy9EZWNpbWFsPj4NCmVuZG9iag0KMjI5IDAgb2JqDQo8PC9PL0xpc3QvTGlzdE51bWJlcmluZy9EZWNpbWFsPj4NCmVuZG9iag0KMjQ1IDAgb2JqDQo8PC9PL0xpc3QvTGlzdE51bWJlcmluZy9EZWNpbWFsPj4NCmVuZG9iag0KMjYxIDAgb2JqDQo8PC9PL0xpc3QvTGlzdE51bWJlcmluZy9EZWNpbWFsPj4NCmVuZG9iag0KMjc3IDAgb2JqDQo8PC9PL0xpc3QvTGlzdE51bWJlcmluZy9EZWNpbWFsPj4NCmVuZG9iag0KMjkzIDAgb2JqDQo8PC9PL0xpc3QvTGlzdE51bWJlcmluZy9EZWNpbWFsPj4NCmVuZG9iag0KMzA5IDAgb2JqDQo8PC9PL0xpc3QvTGlzdE51bWJlcmluZy9EZWNpbWFsPj4NCmVuZG9iag0KMzI1IDAgb2JqDQo8PC9PL0xpc3QvTGlzdE51bWJlcmluZy9EZWNpbWFsPj4NCmVuZG9iag0KMzQyIDAgb2JqDQo8PC9PL0xpc3QvTGlzdE51bWJlcmluZy9EZWNpbWFsPj4NCmVuZG9iag0KMzU4IDAgb2JqDQo8PC9PL0xpc3QvTGlzdE51bWJlcmluZy9EZWNpbWFsPj4NCmVuZG9iag0KMzc0IDAgb2JqDQo8PC9PL0xpc3QvTGlzdE51bWJlcmluZy9EZWNpbWFsPj4NCmVuZG9iag0KMzkwIDAgb2JqDQo8PC9PL0xpc3QvTGlzdE51bWJlcmluZy9EZWNpbWFsPj4NCmVuZG9iag0KNDA2IDAgb2JqDQo8PC9PL0xpc3QvTGlzdE51bWJlcmluZy9EZWNpbWFsPj4NCmVuZG9iag0KNDIzIDAgb2JqDQo8PC9PL0xpc3QvTGlzdE51bWJlcmluZy9EZWNpbWFsPj4NCmVuZG9iag0KNDM5IDAgb2JqDQo8PC9PL0xpc3QvTGlzdE51bWJlcmluZy9EZWNpbWFsPj4NCmVuZG9iag0KNDU1IDAgb2JqDQo8PC9PL0xpc3QvTGlzdE51bWJlcmluZy9EZWNpbWFsPj4NCmVuZG9iag0KNDcyIDAgb2JqDQo8PC9PL0xpc3QvTGlzdE51bWJlcmluZy9EZWNpbWFsPj4NCmVuZG9iag0KNDg4IDAgb2JqDQo8PC9PL0xpc3QvTGlzdE51bWJlcmluZy9EZWNpbWFsPj4NCmVuZG9iag0KNTA0IDAgb2JqDQo8PC9PL0xpc3QvTGlzdE51bWJlcmluZy9EZWNpbWFsPj4NCmVuZG9iag0KNTIwIDAgb2JqDQo8PC9PL0xpc3QvTGlzdE51bWJlcmluZy9EZWNpbWFsPj4NCmVuZG9iag0KNTM2IDAgb2JqDQo8PC9PL0xpc3QvTGlzdE51bWJlcmluZy9EZWNpbWFsPj4NCmVuZG9iag0KNTUyIDAgb2JqDQo8PC9PL0xpc3QvTGlzdE51bWJlcmluZy9EZWNpbWFsPj4NCmVuZG9iag0KNTY4IDAgb2JqDQo8PC9PL0xheW91dC9QbGFjZW1lbnQvQmxvY2svQkJveCA1NjkgMCBSPj4NCmVuZG9iag0KNTY5IDAgb2JqDQpbIDQ4LjIgMTE3LjM1IDU2NC4wNSAxNDAuMDVdIA0KZW5kb2JqDQo1NzQgMCBvYmoNCjw8L08vTGF5b3V0L1BsYWNlbWVudC9CbG9jay9CQm94IDU3NSAwIFI+Pg0KZW5k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L1hNTC9MZW5ndGggMzE4Mj4+DQpzdHJlYW0NCjw/eHBhY2tldCBiZWdpbj0i77u/IiBpZD0iVzVNME1wQ2VoaUh6cmVTek5UY3prYzlkIj8+PHg6eG1wbWV0YSB4bWxuczp4PSJhZG9iZTpuczptZXRhLyIgeDp4bXB0az0iMy4xLTcwMSI+CjxyZGY6UkRGIHhtbG5zOnJkZj0iaHR0cDovL3d3dy53My5vcmcvMTk5OS8wMi8yMi1yZGYtc3ludGF4LW5zIyI+CjxyZGY6RGVzY3JpcHRpb24gcmRmOmFib3V0PSIiICB4bWxuczpwZGY9Imh0dHA6Ly9ucy5hZG9iZS5jb20vcGRmLzEuMy8iPgo8cGRmOlByb2R1Y2VyPk1pY3Jvc29mdMKuIFdvcmQgcGFyYSBNaWNyb3NvZnQgMzY1PC9wZGY6UHJvZHVjZXI+PC9yZGY6RGVzY3JpcHRpb24+CjxyZGY6RGVzY3JpcHRpb24gcmRmOmFib3V0PSIiICB4bWxuczpkYz0iaHR0cDovL3B1cmwub3JnL2RjL2VsZW1lbnRzLzEuMS8iPgo8ZGM6dGl0bGU+PHJkZjpBbHQ+PHJkZjpsaSB4bWw6bGFuZz0ieC1kZWZhdWx0Ij5JVC1GTy0wMDI8L3JkZjpsaT48L3JkZjpBbHQ+PC9kYzp0aXRsZT48ZGM6Y3JlYXRvcj48cmRmOlNlcT48cmRmOmxpPnBnYXJjaWFAZHRybXguY29tPC9yZGY6bGk+PC9yZGY6U2VxPjwvZGM6Y3JlYXRvcj48L3JkZjpEZXNjcmlwdGlvbj4KPHJkZjpEZXNjcmlwdGlvbiByZGY6YWJvdXQ9IiIgIHhtbG5zOnhtcD0iaHR0cDovL25zLmFkb2JlLmNvbS94YXAvMS4wLyI+Cjx4bXA6Q3JlYXRvclRvb2w+TWljcm9zb2Z0wq4gV29yZCBwYXJhIE1pY3Jvc29mdCAzNjU8L3htcDpDcmVhdG9yVG9vbD48eG1wOkNyZWF0ZURhdGU+MjAyNi0wMi0wNVQxNDo0ODo1NC0wNjowMDwveG1wOkNyZWF0ZURhdGU+PHhtcDpNb2RpZnlEYXRlPjIwMjYtMDItMDVUMTQ6NDg6NTQtMDY6MDA8L3htcDpNb2RpZnlEYXRlPjwvcmRmOkRlc2NyaXB0aW9uPgo8cmRmOkRlc2NyaXB0aW9uIHJkZjphYm91dD0iIiAgeG1sbnM6eG1wTU09Imh0dHA6Ly9ucy5hZG9iZS5jb20veGFwLzEuMC9tbS8iPgo8eG1wTU06RG9jdW1lbnRJRD51dWlkOkMyMTE2RDVCLTQxNzktNDM0Ri04NjE5LThDOEE3OTI2QTdFRTwveG1wTU06RG9jdW1lbnRJRD48eG1wTU06SW5zdGFuY2VJRD51dWlkOkMyMTE2RDVCLTQxNzktNDM0Ri04NjE5LThDOEE3OTI2QTdFRTwveG1wTU06SW5zdGFuY2VJRD48L3JkZjpEZXNjcmlwdGlvbj4KICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgIAogICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgCiAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAKICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgIAogICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgCiAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAKICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgIAogICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgCiAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAKICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgIAogICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgCiAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAKICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgIAogICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgCiAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAKICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgIAogICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgCiAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAKICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgIAogICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgCjwvcmRmOlJERj48L3g6eG1wbWV0YT48P3hwYWNrZXQgZW5kPSJ3Ij8+DQplbmRzdHJlYW0NCmVuZG9iag0KNjA5IDAgb2JqDQo8PC9EaXNwbGF5RG9jVGl0bGUgdHJ1ZT4+DQplbmRvYmoNCjYxMCAwIG9iag0KPDwvVHlwZS9YUmVmL1NpemUgNjEwL1dbIDEgNCAyXSAvUm9vdCAxIDAgUi9JbmZvIDIxIDAgUi9JRFs8NUI2RDExQzI3OTQxNEY0Mzg2MTk4QzhBNzkyNkE3RUU+PDVCNkQxMUMyNzk0MTRGNDM4NjE5OEM4QTc5MjZBN0VFPl0gL0ZpbHRlci9GbGF0ZURlY29kZS9MZW5ndGggMTM1MT4+DQpzdHJlYW0NCnicPdcHtJdlAcfx+7vKdACiOFFRUzFnCwg1RFCp3CKONA3JpqUioSZmiotogZo5cO9NzqycuSokFFyYcXFr0jQa0vu8n/vEuYfPed/7vP/fveeec7/339HR/FuxIs3/Azo6CldjcUvnmJZh4/FBy/DlLSO2xcSWkYNbdty1ZfLQlikzWqbOaTmxq+Wk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NSBmDQowMDAwMDAwMDI5IDY1NTM1IGYNCjAwMDAwMDAwMzAgNjU1MzUgZg0KMDAwMDAwMDAzMSA2NTUzNSBmDQowMDAwMDAwMDMyIDY1NTM1IGYNCjAwMDAwMDAwMzMgNjU1MzUgZg0KMDAwMDAwMDAzNCA2NTUzNSBmDQowMDAwMDAwMDM1IDY1NTM1IGYNCjAwMDAwMDAwMzYgNjU1MzUgZg0KMDAwMDAwMDAzNyA2NTUzNSBmDQowMDAwMDAwMDM4IDY1NTM1IGYNCjAwMDAwMDAwNDEgNjU1MzUgZg0KMDAwMDA1NDI1MiAwMDAwMCBuDQowMDAwMDU0MzEzIDAwMDAwIG4NCjAwMDAwMDAwNDQgNjU1MzUgZg0KMDAwMDA1NDM2MiAwMDAwMCBuDQowMDAwMDU0NDIzIDAwMDAwIG4NCjAwMDAwMDAwNDUgNjU1MzUgZg0KMDAwMDAwMDA0NiA2NTUzNSBmDQowMDAwMDAwMDQ3IDY1NTM1IGYNCjAwMDAwMDAwNDggNjU1MzUgZg0KMDAwMDAwMDA0OSA2NTUzNSBmDQowMDAwMDAwMDUwIDY1NTM1IGYNCjAwMDAwMDAwNTEgNjU1MzUgZg0KMDAwMDAwMDA1MiA2NTUzNSBmDQowMDAwMDAwMDUzIDY1NTM1IGYNCjAwMDAwMDAwNTQgNjU1MzUgZg0KMDAwMDAwMDA1NSA2NTUzNSBmDQowMDAwMDAwMDU4IDY1NTM1IGYNCjAwMDAwNTQ0NzIgMDAwMDAgbg0KMDAwMDA1NDUzMyAwMDAwMCBuDQowMDAwMDAwMDU5IDY1NTM1IGYNCjAwMDAwMDAwNjAgNjU1MzUgZg0KMDAwMDAwMDA2MSA2NTUzNSBmDQowMDAwMDAwMDY0IDY1NTM1IGYNCjAwMDAwNTQ1ODMgMDAwMDAgbg0KMDAwMDA1NDY0NCAwMDAwMCBuDQowMDAwMDAwMDY1IDY1NTM1IGYNCjAwMDAwMDAwNjYgNjU1MzUgZg0KMDAwMDAwMDA2NyA2NTUzNSBmDQowMDAwMDAwMDcwIDY1NTM1IGYNCjAwMDAwNTQ2OTQgMDAwMDAgbg0KMDAwMDA1NDc1NSAwMDAwMCBuDQowMDAwMDAwMDcxIDY1NTM1IGYNCjAwMDAwMDAwNzIgNjU1MzUgZg0KMDAwMDAwMDA3MyA2NTUzNSBmDQowMDAwMDAwMDc2IDY1NTM1IGYNCjAwMDAwNTQ4MDEgMDAwMDAgbg0KMDAwMDA1NDg2MiAwMDAwMCBuDQowMDAwMDAwMDc3IDY1NTM1IGYNCjAwMDAwMDAwNzggNjU1MzUgZg0KMDAwMDAwMDA3OSA2NTUzNSBmDQowMDAwMDAwMDgwIDY1NTM1IGYNCjAwMDAwMDAwODEgNjU1MzUgZg0KMDAwMDAwMDA4MiA2NTUzNSBmDQowMDAwMDAwMDg0IDY1NTM1IGYNCjAwMDAwNTQ5MTAgMDAwMDAgbg0KMDAwMDAwMDA4NSA2NTUzNSBmDQowMDAwMDAwMDg2IDY1NTM1IGYNCjAwMDAwMDAwODcgNjU1MzUgZg0KMDAwMDAwMDA4OCA2NTUzNSBmDQowMDAwMDAwMDg5IDY1NTM1IGYNCjAwMDAwMDAwOTAgNjU1MzUgZg0KMDAwMDAwMDA5MSA2NTUzNSBmDQowMDAwMDAwMDkyIDY1NTM1IGYNCjAwMDAwMDAwOTMgNjU1MzUgZg0KMDAwMDAwMDA5NCA2NTUzNSBmDQowMDAwMDAwMDk1IDY1NTM1IGYNCjAwMDAwMDAwOTYgNjU1MzUgZg0KMDAwMDAwMDA5NyA2NTUzNSBmDQowMDAwMDAwMDk4IDY1NTM1IGYNCjAwMDAwMDAwOTkgNjU1MzUgZg0KMDAwMDAwMDEwMSA2NTUzNSBmDQowMDAwMDU0OTYzIDAwMDAwIG4NCjAwMDAwMDAxMDIgNjU1MzUgZg0KMDAwMDAwMDEwMyA2NTUzNSBmDQowMDAwMDAwMTA0IDY1NTM1IGYNCjAwMDAwMDAxMDUgNjU1MzUgZg0KMDAwMDAwMDEwNiA2NTUzNSBmDQowMDAwMDAwMTA3IDY1NTM1IGYNCjAwMDAwMDAxMDggNjU1MzUgZg0KMDAwMDAwMDEwOSA2NTUzNSBmDQowMDAwMDAwMTEwIDY1NTM1IGYNCjAwMDAwMDAxMTEgNjU1MzUgZg0KMDAwMDAwMDExMiA2NTUzNSBmDQowMDAwMDAwMTEzIDY1NTM1IGYNCjAwMDAwMDAxMTQgNjU1MzUgZg0KMDAwMDAwMDExNSA2NTUzNSBmDQowMDAwMDAwMTE3IDY1NTM1IGYNCjAwMDAwNTUwMTcgMDAwMDAgbg0KMDAwMDAwMDExOCA2NTUzNSBmDQowMDAwMDAwMTE5IDY1NTM1IGYNCjAwMDAwMDAxMjAgNjU1MzUgZg0KMDAwMDAwMDEyMSA2NTUzNSBmDQowMDAwMDAwMTIyIDY1NTM1IGYNCjAwMDAwMDAxMjMgNjU1MzUgZg0KMDAwMDAwMDEyNCA2NTUzNSBmDQowMDAwMDAwMTI1IDY1NTM1IGYNCjAwMDAwMDAxMjYgNjU1MzUgZg0KMDAwMDAwMDEyNyA2NTUzNSBmDQowMDAwMDAwMTI4IDY1NTM1IGYNCjAwMDAwMDAxMjkgNjU1MzUgZg0KMDAwMDAwMDEzMCA2NTUzNSBmDQowMDAwMDAwMTMxIDY1NTM1IGYNCjAwMDAwMDAxMzMgNjU1MzUgZg0KMDAwMDA1NTA3MSAwMDAwMCBuDQowMDAwMDAwMTM0IDY1NTM1IGYNCjAwMDAwMDAxMzUgNjU1MzUgZg0KMDAwMDAwMDEzNiA2NTUzNSBmDQowMDAwMDAwMTM3IDY1NTM1IGYNCjAwMDAwMDAxMzggNjU1MzUgZg0KMDAwMDAwMDEzOSA2NTUzNSBmDQowMDAwMDAwMTQwIDY1NTM1IGYNCjAwMDAwMDAxNDEgNjU1MzUgZg0KMDAwMDAwMDE0MiA2NTUzNSBmDQowMDAwMDAwMTQzIDY1NTM1IGYNCjAwMDAwMDAxNDQgNjU1MzUgZg0KMDAwMDAwMDE0NSA2NTUzNSBmDQowMDAwMDAwMTQ2IDY1NTM1IGYNCjAwMDAwMDAxNDcgNjU1MzUgZg0KMDAwMDAwMDE0OSA2NTUzNSBmDQowMDAwMDU1MTI1IDAwMDAwIG4NCjAwMDAwMDAxNTAgNjU1MzUgZg0KMDAwMDAwMDE1MSA2NTUzNSBmDQowMDAwMDAwMTUyIDY1NTM1IGYNCjAwMDAwMDAxNTMgNjU1MzUgZg0KMDAwMDAwMDE1NCA2NTUzNSBmDQowMDAwMDAwMTU1IDY1NTM1IGYNCjAwMDAwMDAxNTYgNjU1MzUgZg0KMDAwMDAwMDE1NyA2NTUzNSBmDQowMDAwMDAwMTU4IDY1NTM1IGYNCjAwMDAwMDAxNTkgNjU1MzUgZg0KMDAwMDAwMDE2MCA2NTUzNSBmDQowMDAwMDAwMTYxIDY1NTM1IGYNCjAwMDAwMDAxNjIgNjU1MzUgZg0KMDAwMDAwMDE2MyA2NTUzNSBmDQowMDAwMDAwMTY1IDY1NTM1IGYNCjAwMDAwNTUxNzkgMDAwMDAgbg0KMDAwMDAwMDE2NiA2NTUzNSBmDQowMDAwMDAwMTY3IDY1NTM1IGYNCjAwMDAwMDAxNjggNjU1MzUgZg0KMDAwMDAwMDE2OSA2NTUzNSBmDQowMDAwMDAwMTcwIDY1NTM1IGYNCjAwMDAwMDAxNzEgNjU1MzUgZg0KMDAwMDAwMDE3MiA2NTUzNSBmDQowMDAwMDAwMTczIDY1NTM1IGYNCjAwMDAwMDAxNzQgNjU1MzUgZg0KMDAwMDAwMDE3NSA2NTUzNSBmDQowMDAwMDAwMTc2IDY1NTM1IGYNCjAwMDAwMDAxNzcgNjU1MzUgZg0KMDAwMDAwMDE3OCA2NTUzNSBmDQowMDAwMDAwMTc5IDY1NTM1IGYNCjAwMDAwMDAxODAgNjU1MzUgZg0KMDAwMDAwMDE4MiA2NTUzNSBmDQowMDAwMDU1MjMzIDAwMDAwIG4NCjAwMDAwMDAxODMgNjU1MzUgZg0KMDAwMDAwMDE4NCA2NTUzNSBmDQowMDAwMDAwMTg1IDY1NTM1IGYNCjAwMDAwMDAxODYgNjU1MzUgZg0KMDAwMDAwMDE4NyA2NTUzNSBmDQowMDAwMDAwMTg4IDY1NTM1IGYNCjAwMDAwMDAxODkgNjU1MzUgZg0KMDAwMDAwMDE5MCA2NTUzNSBmDQowMDAwMDAwMTkxIDY1NTM1IGYNCjAwMDAwMDAxOTIgNjU1MzUgZg0KMDAwMDAwMDE5MyA2NTUzNSBmDQowMDAwMDAwMTk0IDY1NTM1IGYNCjAwMDAwMDAxOTUgNjU1MzUgZg0KMDAwMDAwMDE5NiA2NTUzNSBmDQowMDAwMDAwMTk4IDY1NTM1IGYNCjAwMDAwNTUyODcgMDAwMDAgbg0KMDAwMDAwMDE5OSA2NTUzNSBmDQowMDAwMDAwMjAwIDY1NTM1IGYNCjAwMDAwMDAyMDEgNjU1MzUgZg0KMDAwMDAwMDIwMiA2NTUzNSBmDQowMDAwMDAwMjAzIDY1NTM1IGYNCjAwMDAwMDAyMDQgNjU1MzUgZg0KMDAwMDAwMDIwNSA2NTUzNSBmDQowMDAwMDAwMjA2IDY1NTM1IGYNCjAwMDAwMDAyMDcgNjU1MzUgZg0KMDAwMDAwMDIwOCA2NTUzNSBmDQowMDAwMDAwMjA5IDY1NTM1IGYNCjAwMDAwMDAyMTAgNjU1MzUgZg0KMDAwMDAwMDIxMSA2NTUzNSBmDQowMDAwMDAwMjEyIDY1NTM1IGYNCjAwMDAwMDAyMTQgNjU1MzUgZg0KMDAwMDA1NTM0MSAwMDAwMCBuDQowMDAwMDAwMjE1IDY1NTM1IGYNCjAwMDAwMDAyMTYgNjU1MzUgZg0KMDAwMDAwMDIxNyA2NTUzNSBmDQowMDAwMDAwMjE4IDY1NTM1IGYNCjAwMDAwMDAyMTkgNjU1MzUgZg0KMDAwMDAwMDIyMCA2NTUzNSBmDQowMDAwMDAwMjIxIDY1NTM1IGYNCjAwMDAwMDAyMjIgNjU1MzUgZg0KMDAwMDAwMDIyMyA2NTUzNSBmDQowMDAwMDAwMjI0IDY1NTM1IGYNCjAwMDAwMDAyMjUgNjU1MzUgZg0KMDAwMDAwMDIyNiA2NTUzNSBmDQowMDAwMDAwMjI3IDY1NTM1IGYNCjAwMDAwMDAyMjggNjU1MzUgZg0KMDAwMDAwMDIzMCA2NTUzNSBmDQowMDAwMDU1Mzk1IDAwMDAwIG4NCjAwMDAwMDAyMzEgNjU1MzUgZg0KMDAwMDAwMDIzMiA2NTUzNSBmDQowMDAwMDAwMjMzIDY1NTM1IGYNCjAwMDAwMDAyMzQgNjU1MzUgZg0KMDAwMDAwMDIzNSA2NTUzNSBmDQowMDAwMDAwMjM2IDY1NTM1IGYNCjAwMDAwMDAyMzcgNjU1MzUgZg0KMDAwMDAwMDIzOCA2NTUzNSBmDQowMDAwMDAwMjM5IDY1NTM1IGYNCjAwMDAwMDAyNDAgNjU1MzUgZg0KMDAwMDAwMDI0MSA2NTUzNSBmDQowMDAwMDAwMjQyIDY1NTM1IGYNCjAwMDAwMDAyNDMgNjU1MzUgZg0KMDAwMDAwMDI0NCA2NTUzNSBmDQowMDAwMDAwMjQ2IDY1NTM1IGYNCjAwMDAwNTU0NDkgMDAwMDAgbg0KMDAwMDAwMDI0NyA2NTUzNSBmDQowMDAwMDAwMjQ4IDY1NTM1IGYNCjAwMDAwMDAyNDkgNjU1MzUgZg0KMDAwMDAwMDI1MCA2NTUzNSBmDQowMDAwMDAwMjUxIDY1NTM1IGYNCjAwMDAwMDAyNTIgNjU1MzUgZg0KMDAwMDAwMDI1MyA2NTUzNSBmDQowMDAwMDAwMjU0IDY1NTM1IGYNCjAwMDAwMDAyNTUgNjU1MzUgZg0KMDAwMDAwMDI1NiA2NTUzNSBmDQowMDAwMDAwMjU3IDY1NTM1IGYNCjAwMDAwMDAyNTggNjU1MzUgZg0KMDAwMDAwMDI1OSA2NTUzNSBmDQowMDAwMDAwMjYwIDY1NTM1IGYNCjAwMDAwMDAyNjIgNjU1MzUgZg0KMDAwMDA1NTUwMyAwMDAwMCBuDQowMDAwMDAwMjYzIDY1NTM1IGYNCjAwMDAwMDAyNjQgNjU1MzUgZg0KMDAwMDAwMDI2NSA2NTUzNSBmDQowMDAwMDAwMjY2IDY1NTM1IGYNCjAwMDAwMDAyNjcgNjU1MzUgZg0KMDAwMDAwMDI2OCA2NTUzNSBmDQowMDAwMDAwMjY5IDY1NTM1IGYNCjAwMDAwMDAyNzAgNjU1MzUgZg0KMDAwMDAwMDI3MSA2NTUzNSBmDQowMDAwMDAwMjcyIDY1NTM1IGYNCjAwMDAwMDAyNzMgNjU1MzUgZg0KMDAwMDAwMDI3NCA2NTUzNSBmDQowMDAwMDAwMjc1IDY1NTM1IGYNCjAwMDAwMDAyNzYgNjU1MzUgZg0KMDAwMDAwMDI3OCA2NTUzNSBmDQowMDAwMDU1NTU3IDAwMDAwIG4NCjAwMDAwMDAyNzkgNjU1MzUgZg0KMDAwMDAwMDI4MCA2NTUzNSBmDQowMDAwMDAwMjgxIDY1NTM1IGYNCjAwMDAwMDAyODIgNjU1MzUgZg0KMDAwMDAwMDI4MyA2NTUzNSBmDQowMDAwMDAwMjg0IDY1NTM1IGYNCjAwMDAwMDAyODUgNjU1MzUgZg0KMDAwMDAwMDI4NiA2NTUzNSBmDQowMDAwMDAwMjg3IDY1NTM1IGYNCjAwMDAwMDAyODggNjU1MzUgZg0KMDAwMDAwMDI4OSA2NTUzNSBmDQowMDAwMDAwMjkwIDY1NTM1IGYNCjAwMDAwMDAyOTEgNjU1MzUgZg0KMDAwMDAwMDI5MiA2NTUzNSBmDQowMDAwMDAwMjk0IDY1NTM1IGYNCjAwMDAwNTU2MTEgMDAwMDAgbg0KMDAwMDAwMDI5NSA2NTUzNSBmDQowMDAwMDAwMjk2IDY1NTM1IGYNCjAwMDAwMDAyOTcgNjU1MzUgZg0KMDAwMDAwMDI5OCA2NTUzNSBmDQowMDAwMDAwMjk5IDY1NTM1IGYNCjAwMDAwMDAzMDAgNjU1MzUgZg0KMDAwMDAwMDMwMSA2NTUzNSBmDQowMDAwMDAwMzAyIDY1NTM1IGYNCjAwMDAwMDAzMDMgNjU1MzUgZg0KMDAwMDAwMDMwNCA2NTUzNSBmDQowMDAwMDAwMzA1IDY1NTM1IGYNCjAwMDAwMDAzMDYgNjU1MzUgZg0KMDAwMDAwMDMwNyA2NTUzNSBmDQowMDAwMDAwMzA4IDY1NTM1IGYNCjAwMDAwMDAzMTAgNjU1MzUgZg0KMDAwMDA1NTY2NSAwMDAwMCBuDQowMDAwMDAwMzExIDY1NTM1IGYNCjAwMDAwMDAzMTIgNjU1MzUgZg0KMDAwMDAwMDMxMyA2NTUzNSBmDQowMDAwMDAwMzE0IDY1NTM1IGYNCjAwMDAwMDAzMTUgNjU1MzUgZg0KMDAwMDAwMDMxNiA2NTUzNSBmDQowMDAwMDAwMzE3IDY1NTM1IGYNCjAwMDAwMDAzMTggNjU1MzUgZg0KMDAwMDAwMDMxOSA2NTUzNSBmDQowMDAwMDAwMzIwIDY1NTM1IGYNCjAwMDAwMDAzMjEgNjU1MzUgZg0KMDAwMDAwMDMyMiA2NTUzNSBmDQowMDAwMDAwMzIzIDY1NTM1IGYNCjAwMDAwMDAzMjQgNjU1MzUgZg0KMDAwMDAwMDMyNiA2NTUzNSBmDQowMDAwMDU1NzE5IDAwMDAwIG4NCjAwMDAwMDAzMjcgNjU1MzUgZg0KMDAwMDAwMDMyOCA2NTUzNSBmDQowMDAwMDAwMzI5IDY1NTM1IGYNCjAwMDAwMDAzMzAgNjU1MzUgZg0KMDAwMDAwMDMzMSA2NTUzNSBmDQowMDAwMDAwMzMyIDY1NTM1IGYNCjAwMDAwMDAzMzMgNjU1MzUgZg0KMDAwMDAwMDMzNCA2NTUzNSBmDQowMDAwMDAwMzM1IDY1NTM1IGYNCjAwMDAwMDAzMzYgNjU1MzUgZg0KMDAwMDAwMDMzNyA2NTUzNSBmDQowMDAwMDAwMzM4IDY1NTM1IGYNCjAwMDAwMDAzMzkgNjU1MzUgZg0KMDAwMDAwMDM0MCA2NTUzNSBmDQowMDAwMDAwMzQxIDY1NTM1IGYNCjAwMDAwMDAzNDMgNjU1MzUgZg0KMDAwMDA1NTc3MyAwMDAwMCBuDQowMDAwMDAwMzQ0IDY1NTM1IGYNCjAwMDAwMDAzNDUgNjU1MzUgZg0KMDAwMDAwMDM0NiA2NTUzNSBmDQowMDAwMDAwMzQ3IDY1NTM1IGYNCjAwMDAwMDAzNDggNjU1MzUgZg0KMDAwMDAwMDM0OSA2NTUzNSBmDQowMDAwMDAwMzUwIDY1NTM1IGYNCjAwMDAwMDAzNTEgNjU1MzUgZg0KMDAwMDAwMDM1MiA2NTUzNSBmDQowMDAwMDAwMzUzIDY1NTM1IGYNCjAwMDAwMDAzNTQgNjU1MzUgZg0KMDAwMDAwMDM1NSA2NTUzNSBmDQowMDAwMDAwMzU2IDY1NTM1IGYNCjAwMDAwMDAzNTcgNjU1MzUgZg0KMDAwMDAwMDM1OSA2NTUzNSBmDQowMDAwMDU1ODI3IDAwMDAwIG4NCjAwMDAwMDAzNjAgNjU1MzUgZg0KMDAwMDAwMDM2MSA2NTUzNSBmDQowMDAwMDAwMzYyIDY1NTM1IGYNCjAwMDAwMDAzNjMgNjU1MzUgZg0KMDAwMDAwMDM2NCA2NTUzNSBmDQowMDAwMDAwMzY1IDY1NTM1IGYNCjAwMDAwMDAzNjYgNjU1MzUgZg0KMDAwMDAwMDM2NyA2NTUzNSBmDQowMDAwMDAwMzY4IDY1NTM1IGYNCjAwMDAwMDAzNjkgNjU1MzUgZg0KMDAwMDAwMDM3MCA2NTUzNSBmDQowMDAwMDAwMzcxIDY1NTM1IGYNCjAwMDAwMDAzNzIgNjU1MzUgZg0KMDAwMDAwMDM3MyA2NTUzNSBmDQowMDAwMDAwMzc1IDY1NTM1IGYNCjAwMDAwNTU4ODEgMDAwMDAgbg0KMDAwMDAwMDM3NiA2NTUzNSBmDQowMDAwMDAwMzc3IDY1NTM1IGYNCjAwMDAwMDAzNzggNjU1MzUgZg0KMDAwMDAwMDM3OSA2NTUzNSBmDQowMDAwMDAwMzgwIDY1NTM1IGYNCjAwMDAwMDAzODEgNjU1MzUgZg0KMDAwMDAwMDM4MiA2NTUzNSBmDQowMDAwMDAwMzgzIDY1NTM1IGYNCjAwMDAwMDAzODQgNjU1MzUgZg0KMDAwMDAwMDM4NSA2NTUzNSBmDQowMDAwMDAwMzg2IDY1NTM1IGYNCjAwMDAwMDAzODcgNjU1MzUgZg0KMDAwMDAwMDM4OCA2NTUzNSBmDQowMDAwMDAwMzg5IDY1NTM1IGYNCjAwMDAwMDAzOTEgNjU1MzUgZg0KMDAwMDA1NTkzNSAwMDAwMCBuDQowMDAwMDAwMzkyIDY1NTM1IGYNCjAwMDAwMDAzOTMgNjU1MzUgZg0KMDAwMDAwMDM5NCA2NTUzNSBmDQowMDAwMDAwMzk1IDY1NTM1IGYNCjAwMDAwMDAzOTYgNjU1MzUgZg0KMDAwMDAwMDM5NyA2NTUzNSBmDQowMDAwMDAwMzk4IDY1NTM1IGYNCjAwMDAwMDAzOTkgNjU1MzUgZg0KMDAwMDAwMDQwMCA2NTUzNSBmDQowMDAwMDAwNDAxIDY1NTM1IGYNCjAwMDAwMDA0MDIgNjU1MzUgZg0KMDAwMDAwMDQwMyA2NTUzNSBmDQowMDAwMDAwNDA0IDY1NTM1IGYNCjAwMDAwMDA0MDUgNjU1MzUgZg0KMDAwMDAwMDQwNyA2NTUzNSBmDQowMDAwMDU1OTg5IDAwMDAwIG4NCjAwMDAwMDA0MDggNjU1MzUgZg0KMDAwMDAwMDQwOSA2NTUzNSBmDQowMDAwMDAwNDEwIDY1NTM1IGYNCjAwMDAwMDA0MTEgNjU1MzUgZg0KMDAwMDAwMDQxMiA2NTUzNSBmDQowMDAwMDAwNDEzIDY1NTM1IGYNCjAwMDAwMDA0MTQgNjU1MzUgZg0KMDAwMDAwMDQxNSA2NTUzNSBmDQowMDAwMDAwNDE2IDY1NTM1IGYNCjAwMDAwMDA0MTcgNjU1MzUgZg0KMDAwMDAwMDQxOCA2NTUzNSBmDQowMDAwMDAwNDE5IDY1NTM1IGYNCjAwMDAwMDA0MjAgNjU1MzUgZg0KMDAwMDAwMDQyMSA2NTUzNSBmDQowMDAwMDAwNDIyIDY1NTM1IGYNCjAwMDAwMDA0MjQgNjU1MzUgZg0KMDAwMDA1NjA0MyAwMDAwMCBuDQowMDAwMDAwNDI1IDY1NTM1IGYNCjAwMDAwMDA0MjYgNjU1MzUgZg0KMDAwMDAwMDQyNyA2NTUzNSBmDQowMDAwMDAwNDI4IDY1NTM1IGYNCjAwMDAwMDA0MjkgNjU1MzUgZg0KMDAwMDAwMDQzMCA2NTUzNSBmDQowMDAwMDAwNDMxIDY1NTM1IGYNCjAwMDAwMDA0MzIgNjU1MzUgZg0KMDAwMDAwMDQzMyA2NTUzNSBmDQowMDAwMDAwNDM0IDY1NTM1IGYNCjAwMDAwMDA0MzUgNjU1MzUgZg0KMDAwMDAwMDQzNiA2NTUzNSBmDQowMDAwMDAwNDM3IDY1NTM1IGYNCjAwMDAwMDA0MzggNjU1MzUgZg0KMDAwMDAwMDQ0MCA2NTUzNSBmDQowMDAwMDU2MDk3IDAwMDAwIG4NCjAwMDAwMDA0NDEgNjU1MzUgZg0KMDAwMDAwMDQ0MiA2NTUzNSBmDQowMDAwMDAwNDQzIDY1NTM1IGYNCjAwMDAwMDA0NDQgNjU1MzUgZg0KMDAwMDAwMDQ0NSA2NTUzNSBmDQowMDAwMDAwNDQ2IDY1NTM1IGYNCjAwMDAwMDA0NDcgNjU1MzUgZg0KMDAwMDAwMDQ0OCA2NTUzNSBmDQowMDAwMDAwNDQ5IDY1NTM1IGYNCjAwMDAwMDA0NTAgNjU1MzUgZg0KMDAwMDAwMDQ1MSA2NTUzNSBmDQowMDAwMDAwNDUyIDY1NTM1IGYNCjAwMDAwMDA0NTMgNjU1MzUgZg0KMDAwMDAwMDQ1NCA2NTUzNSBmDQowMDAwMDAwNDU2IDY1NTM1IGYNCjAwMDAwNTYxNTEgMDAwMDAgbg0KMDAwMDAwMDQ1NyA2NTUzNSBmDQowMDAwMDAwNDU4IDY1NTM1IGYNCjAwMDAwMDA0NTkgNjU1MzUgZg0KMDAwMDAwMDQ2MCA2NTUzNSBmDQowMDAwMDAwNDYxIDY1NTM1IGYNCjAwMDAwMDA0NjIgNjU1MzUgZg0KMDAwMDAwMDQ2MyA2NTUzNSBmDQowMDAwMDAwNDY0IDY1NTM1IGYNCjAwMDAwMDA0NjUgNjU1MzUgZg0KMDAwMDAwMDQ2NiA2NTUzNSBmDQowMDAwMDAwNDY3IDY1NTM1IGYNCjAwMDAwMDA0NjggNjU1MzUgZg0KMDAwMDAwMDQ2OSA2NTUzNSBmDQowMDAwMDAwNDcwIDY1NTM1IGYNCjAwMDAwMDA0NzEgNjU1MzUgZg0KMDAwMDAwMDQ3MyA2NTUzNSBmDQowMDAwMDU2MjA1IDAwMDAwIG4NCjAwMDAwMDA0NzQgNjU1MzUgZg0KMDAwMDAwMDQ3NSA2NTUzNSBmDQowMDAwMDAwNDc2IDY1NTM1IGYNCjAwMDAwMDA0NzcgNjU1MzUgZg0KMDAwMDAwMDQ3OCA2NTUzNSBmDQowMDAwMDAwNDc5IDY1NTM1IGYNCjAwMDAwMDA0ODAgNjU1MzUgZg0KMDAwMDAwMDQ4MSA2NTUzNSBmDQowMDAwMDAwNDgyIDY1NTM1IGYNCjAwMDAwMDA0ODMgNjU1MzUgZg0KMDAwMDAwMDQ4NCA2NTUzNSBmDQowMDAwMDAwNDg1IDY1NTM1IGYNCjAwMDAwMDA0ODYgNjU1MzUgZg0KMDAwMDAwMDQ4NyA2NTUzNSBmDQowMDAwMDAwNDg5IDY1NTM1IGYNCjAwMDAwNTYyNTkgMDAwMDAgbg0KMDAwMDAwMDQ5MCA2NTUzNSBmDQowMDAwMDAwNDkxIDY1NTM1IGYNCjAwMDAwMDA0OTIgNjU1MzUgZg0KMDAwMDAwMDQ5MyA2NTUzNSBmDQowMDAwMDAwNDk0IDY1NTM1IGYNCjAwMDAwMDA0OTUgNjU1MzUgZg0KMDAwMDAwMDQ5NiA2NTUzNSBmDQowMDAwMDAwNDk3IDY1NTM1IGYNCjAwMDAwMDA0OTggNjU1MzUgZg0KMDAwMDAwMDQ5OSA2NTUzNSBmDQowMDAwMDAwNTAwIDY1NTM1IGYNCjAwMDAwMDA1MDEgNjU1MzUgZg0KMDAwMDAwMDUwMiA2NTUzNSBmDQowMDAwMDAwNTAzIDY1NTM1IGYNCjAwMDAwMDA1MDUgNjU1MzUgZg0KMDAwMDA1NjMxMyAwMDAwMCBuDQowMDAwMDAwNTA2IDY1NTM1IGYNCjAwMDAwMDA1MDcgNjU1MzUgZg0KMDAwMDAwMDUwOCA2NTUzNSBmDQowMDAwMDAwNTA5IDY1NTM1IGYNCjAwMDAwMDA1MTAgNjU1MzUgZg0KMDAwMDAwMDUxMSA2NTUzNSBmDQowMDAwMDAwNTEyIDY1NTM1IGYNCjAwMDAwMDA1MTMgNjU1MzUgZg0KMDAwMDAwMDUxNCA2NTUzNSBmDQowMDAwMDAwNTE1IDY1NTM1IGYNCjAwMDAwMDA1MTYgNjU1MzUgZg0KMDAwMDAwMDUxNyA2NTUzNSBmDQowMDAwMDAwNTE4IDY1NTM1IGYNCjAwMDAwMDA1MTkgNjU1MzUgZg0KMDAwMDAwMDUyMSA2NTUzNSBmDQowMDAwMDU2MzY3IDAwMDAwIG4NCjAwMDAwMDA1MjIgNjU1MzUgZg0KMDAwMDAwMDUyMyA2NTUzNSBmDQowMDAwMDAwNTI0IDY1NTM1IGYNCjAwMDAwMDA1MjUgNjU1MzUgZg0KMDAwMDAwMDUyNiA2NTUzNSBmDQowMDAwMDAwNTI3IDY1NTM1IGYNCjAwMDAwMDA1MjggNjU1MzUgZg0KMDAwMDAwMDUyOSA2NTUzNSBmDQowMDAwMDAwNTMwIDY1NTM1IGYNCjAwMDAwMDA1MzEgNjU1MzUgZg0KMDAwMDAwMDUzMiA2NTUzNSBmDQowMDAwMDAwNTMzIDY1NTM1IGYNCjAwMDAwMDA1MzQgNjU1MzUgZg0KMDAwMDAwMDUzNSA2NTUzNSBmDQowMDAwMDAwNTM3IDY1NTM1IGYNCjAwMDAwNTY0MjEgMDAwMDAgbg0KMDAwMDAwMDUzOCA2NTUzNSBmDQowMDAwMDAwNTM5IDY1NTM1IGYNCjAwMDAwMDA1NDAgNjU1MzUgZg0KMDAwMDAwMDU0MSA2NTUzNSBmDQowMDAwMDAwNTQyIDY1NTM1IGYNCjAwMDAwMDA1NDMgNjU1MzUgZg0KMDAwMDAwMDU0NCA2NTUzNSBmDQowMDAwMDAwNTQ1IDY1NTM1IGYNCjAwMDAwMDA1NDYgNjU1MzUgZg0KMDAwMDAwMDU0NyA2NTUzNSBmDQowMDAwMDAwNTQ4IDY1NTM1IGYNCjAwMDAwMDA1NDkgNjU1MzUgZg0KMDAwMDAwMDU1MCA2NTUzNSBmDQowMDAwMDAwNTUxIDY1NTM1IGYNCjAwMDAwMDA1NTMgNjU1MzUgZg0KMDAwMDA1NjQ3NSAwMDAwMCBuDQowMDAwMDAwNTU0IDY1NTM1IGYNCjAwMDAwMDA1NTUgNjU1MzUgZg0KMDAwMDAwMDU1NiA2NTUzNSBmDQowMDAwMDAwNTU3IDY1NTM1IGYNCjAwMDAwMDA1NTggNjU1MzUgZg0KMDAwMDAwMDU1OSA2NTUzNSBmDQowMDAwMDAwNTYwIDY1NTM1IGYNCjAwMDAwMDA1NjEgNjU1MzUgZg0KMDAwMDAwMDU2MiA2NTUzNSBmDQowMDAwMDAwNTYzIDY1NTM1IGYNCjAwMDAwMDA1NjQgNjU1MzUgZg0KMDAwMDAwMDU2NSA2NTUzNSBmDQowMDAwMDAwNTY2IDY1NTM1IGYNCjAwMDAwMDA1NjcgNjU1MzUgZg0KMDAwMDAwMDU3MCA2NTUzNSBmDQowMDAwMDU2NTI5IDAwMDAwIG4NCjAwMDAwNTY1OTIgMDAwMDAgbg0KMDAwMDAwMDU3MSA2NTUzNSBmDQowMDAwMDAwNTcyIDY1NTM1IGYNCjAwMDAwMDA1NzMgNjU1MzUgZg0KMDAwMDAwMDU3NiA2NTUzNSBmDQowMDAwMDU2NjQyIDAwMDAwIG4NCjAwMDAwNTY3MDUgMDAwMDAgbg0KMDAwMDAwMDU3NyA2NTUzNSBmDQowMDAwMDAwNTc4IDY1NTM1IGYNCjAwMDAwMDA1NzkgNjU1MzUgZg0KMDAwMDAwMDU4MCA2NTUzNSBmDQowMDAwMDAwNTgxIDY1NTM1IGYNCjAwMDAwMDA1ODQgNjU1MzUgZg0KMDAwMDA1ODAwNCAwMDAwMCBuDQowMDAwMDU4MDY3IDAwMDAwIG4NCjAwMDAwMDA1ODcgNjU1MzUgZg0KMDAwMDA1ODExOCAwMDAwMCBuDQowMDAwMDU4MTgxIDAwMDAwIG4NCjAwMDAwMDA1ODggNjU1MzUgZg0KMDAwMDAwMDU4OSA2NTUzNSBmDQowMDAwMDAwNTkwIDY1NTM1IGYNCjAwMDAwMDA1OTEgNjU1MzUgZg0KMDAwMDAwMDU5NCA2NTUzNSBmDQowMDAwMDU4MjMyIDAwMDAwIG4NCjAwMDAwNTgyOTUgMDAwMDAgbg0KMDAwMDAwMDU5NyA2NTUzNSBmDQowMDAwMDU4MzQ0IDAwMDAwIG4NCjAwMDAwNTg0MDcgMDAwMDAgbg0KMDAwMDAwMDU5OCA2NTUzNSBmDQowMDAwMDAwNTk5IDY1NTM1IGYNCjAwMDAwMDA2MDAgNjU1MzUgZg0KMDAwMDAwMDYwMSA2NTUzNSBmDQowMDAwMDAwNjAyIDY1NTM1IGYNCjAwMDAwMDA2MDMgNjU1MzUgZg0KMDAwMDAwMDAwMCA2NTUzNSBmDQowMDAwMDU4NDU1IDAwMDAwIG4NCjAwMDAwNTkwNDkgMDAwMDAgbg0KMDAwMDExMDMyMCAwMDAwMCBuDQowMDAwMTEwODAwIDAwMDAwIG4NCjAwMDAxNDAzNjUgMDAwMDAgbg0KMDAwMDE0MzYzMSAwMDAwMCBuDQowMDAwMTQzNjc3IDAwMDAwIG4NCnRyYWlsZXINCjw8L1NpemUgNjExL1Jvb3QgMSAwIFIvSW5mbyAyMSAwIFIvSURbPDVCNkQxMUMyNzk0MTRGNDM4NjE5OEM4QTc5MjZBN0VFPjw1QjZEMTFDMjc5NDE0RjQzODYxOThDOEE3OTI2QTdFRT5dID4+DQpzdGFydHhyZWYNCjE0NTIzMg0KJSVFT0YNCnhyZWYNCjAgMA0KdHJhaWxlcg0KPDwvU2l6ZSA2MTEvUm9vdCAxIDAgUi9JbmZvIDIxIDAgUi9JRFs8NUI2RDExQzI3OTQxNEY0Mzg2MTk4QzhBNzkyNkE3RUU+PDVCNkQxMUMyNzk0MTRGNDM4NjE5OEM4QTc5MjZBN0VFPl0gL1ByZXYgMTQ1MjMyL1hSZWZTdG0gMTQzNjc3Pj4NCnN0YXJ0eHJlZg0KMTU3NjEyDQolJUVPRg==';
  var byteChars = atob(b64);
  var byteNums = new Array(byteChars.length);
  for(var i=0;i<byteChars.length;i++) byteNums[i]=byteChars.charCodeAt(i);
  var blob = new Blob([new Uint8Array(byteNums)],{type:'application/pdf'});
  var url  = URL.createObjectURL(blob);
  var a    = document.createElement('a');
  a.href = url;
  a.download = 'IT-FO-002_Mantenimiento_Preventivo_Rev_9.pdf';
  document.body.appendChild(a); a.click();
  document.body.removeChild(a);
  URL.revokeObjectURL(url);
  toast('✅ Formato descargado', true);
}
");

        sb.AppendLine("function toast(msg,ok){const t=document.createElement('div');t.className='toast '+(ok?'toast-ok':'toast-err');t.textContent=msg;document.body.appendChild(t);setTimeout(()=>t.remove(),3500);}");





        sb.AppendLine("let correctivoTemp={};");

        sb.AppendLine("function abrirModalCorrectivo(id,planta,equipo,ubicacion){");
        sb.AppendLine(" correctivoTemp={id:id};");
        sb.AppendLine(" document.getElementById('c_planta').value=planta||'';");
        sb.AppendLine(" document.getElementById('c_equipo').value=equipo||'';");
        sb.AppendLine(" document.getElementById('c_linea').value=ubicacion||'';");
        sb.AppendLine(" document.getElementById('c_reporte').value=nombreActual||'';");
        sb.AppendLine(" document.getElementById('c_fecha').value=new Date().toISOString().split('T')[0];");
        sb.AppendLine(" ['c_marca','c_modelo','c_serie','c_falla','c_accesorios'].forEach(id=>document.getElementById(id).value='');");
        sb.AppendLine(" document.getElementById('modalCorrectivo').classList.add('show');");
        sb.AppendLine("}");

        sb.AppendLine("function cerrarModalCorrectivo(){");
        sb.AppendLine(" document.getElementById('modalCorrectivo').classList.remove('show');");
        sb.AppendLine("}");

        sb.AppendLine("async function guardarCorrectivo(){");
        sb.AppendLine(" const body={");
        sb.AppendLine(" planta:document.getElementById('c_planta').value,");
        sb.AppendLine(" fecha_solicitud:document.getElementById('c_fecha').value,");
        sb.AppendLine(" linea_persona:document.getElementById('c_linea').value,");
        sb.AppendLine(" equipo:document.getElementById('c_equipo').value,");
        sb.AppendLine(" marca:document.getElementById('c_marca').value,");
        sb.AppendLine(" modelo:document.getElementById('c_modelo').value,");
        sb.AppendLine(" numero_serie:document.getElementById('c_serie').value,");
        sb.AppendLine(" reporte_elaborado_por:document.getElementById('c_reporte').value,");
        sb.AppendLine(" descripcion_falla:document.getElementById('c_falla').value,");
        sb.AppendLine(" accesorio_solicitado:document.getElementById('c_accesorios').value");
        sb.AppendLine(" };");

        sb.AppendLine(" await fetch('/MANTENIMIENTOS_CORRECTIVOS',{");
        sb.AppendLine(" method:'POST',");
        sb.AppendLine(" headers:{'Content-Type':'application/json'},");
        sb.AppendLine(" body:JSON.stringify(body)");
        sb.AppendLine(" });");

        sb.AppendLine(" cerrarModalCorrectivo();");
        sb.AppendLine(" toast('Correctivo registrado',true);");
        sb.AppendLine("}");

        // ── Form Reparación JS ──
        sb.AppendLine("function actualizarPreviewRep(){");
        sb.AppendLine("  const rack=document.getElementById('rep-rack').value;");
        sb.AppendLine("  const esp=document.getElementById('rep-espacio').value;");
        sb.AppendLine("  const prev=document.getElementById('rep-preview');");
        sb.AppendLine("  const txt=document.getElementById('rep-preview-txt');");
        sb.AppendLine("  if(rack&&esp){prev.style.display='block';txt.textContent='Soporte Site Reparacion ('+rack+' espacio '+esp+')';}");
        sb.AppendLine("  else{prev.style.display='none';}");
        sb.AppendLine("}");

        sb.AppendLine("function toggleFormReparacion(){");
        sb.AppendLine("  const f=document.getElementById('form-reparacion');");
        sb.AppendLine("  if(!f)return;");
        sb.AppendLine("  const abriendo=f.style.display==='none';");
        sb.AppendLine("  f.style.display=abriendo?'block':'none';");
        sb.AppendLine("  if(abriendo){");
        sb.AppendLine("    document.getElementById('rep-rack').value='';");
        sb.AppendLine("    document.getElementById('rep-espacio').value='';");
        sb.AppendLine("    document.getElementById('rep-id-prestamo').value='';");
        sb.AppendLine("    document.getElementById('rep-preview').style.display='none';");
        sb.AppendLine("    document.getElementById('rep-error').style.display='none';");
        sb.AppendLine("  }");
        sb.AppendLine("}");

        sb.AppendLine("function cerrarFormReparacion(){");
        sb.AppendLine("  const f=document.getElementById('form-reparacion');");
        sb.AppendLine("  if(f) f.style.display='none';");
        sb.AppendLine("}");

        sb.AppendLine("async function guardarReparacion(){");
        sb.AppendLine("  const errEl=document.getElementById('rep-error');");
        sb.AppendLine("  errEl.style.display='none';");
        sb.AppendLine("  const idDisp=recalIdDispositivo;");
        sb.AppendLine("  if(!idDisp){errEl.textContent='No hay dispositivo seleccionado.';errEl.style.display='block';return;}");
        sb.AppendLine("  const rack=document.getElementById('rep-rack').value.trim();");
        sb.AppendLine("  const espacio=document.getElementById('rep-espacio').value.trim();");
        sb.AppendLine("  const idPrestamo=document.getElementById('rep-id-prestamo').value.trim();");
        sb.AppendLine("  if(!rack){errEl.textContent='Selecciona un rack.';errEl.style.display='block';return;}");
        sb.AppendLine("  if(!espacio){errEl.textContent='Selecciona un espacio.';errEl.style.display='block';return;}");
        sb.AppendLine("  if(!idPrestamo){errEl.textContent='El ID del dispositivo de préstamo es obligatorio.';errEl.style.display='block';return;}");
        sb.AppendLine("  const btn=document.getElementById('btnGuardarRep');");
        sb.AppendLine("  btn.disabled=true;btn.textContent='Guardando...';");
        sb.AppendLine("  try{");
        sb.AppendLine("    const res=await fetch('/PREVENTIVO/RECAL_REPARACION',{");
        sb.AppendLine("      method:'POST',");
        sb.AppendLine("      headers:{'Content-Type':'application/json'},");
        sb.AppendLine("      body:JSON.stringify({id_dispositivo:idDisp,rack,espacio,id_dispositivo_prestamo:idPrestamo,usuario:usuarioActual})");
        sb.AppendLine("    });");
        sb.AppendLine("    const d=await res.json();");
        sb.AppendLine("    if(d.ok){");
        sb.AppendLine("      cerrarFormReparacion();");
        sb.AppendLine("      cerrarRecal();");
        sb.AppendLine("      // Actualizar tarjeta visualmente sin recargar");
        sb.AppendLine("      aplicarEstadoReparacion(idDisp);");
        sb.AppendLine("      toast('🔧 En reparación: '+d.nueva_ubicacion,true);");
        sb.AppendLine("    }else{");
        sb.AppendLine("      errEl.textContent=d.error||'Error al guardar';errEl.style.display='block';");
        sb.AppendLine("    }");
        sb.AppendLine("  }catch(e){errEl.textContent='Error de conexión';errEl.style.display='block';}");
        sb.AppendLine("  finally{btn.disabled=false;btn.textContent='💾 Guardar Reparación';}");
        sb.AppendLine("}");

        // ── Aplicar estado EN REPARACION a la tarjeta dinámicamente ──
        sb.AppendLine("function aplicarEstadoReparacion(id){");
        sb.AppendLine("  const card=document.querySelector('.card[data-id=\"'+id+'\"]');");
        sb.AppendLine("  if(!card)return;");
        // Borde rojo
        sb.AppendLine("  card.style.border='2px solid #EF4444';");
        sb.AppendLine("  card.style.boxShadow='0 0 0 2px rgba(239,68,68,.25)';");
        // Badge color rojo en card-top
        sb.AppendLine("  const badge=card.querySelector('.color-badge');");
        sb.AppendLine("  if(badge){badge.textContent='Rojo';badge.style.background='#1f0000';badge.style.color='#EF4444';badge.style.borderColor='rgba(239,68,68,.4)';}");
        // Etiqueta EN REPARACION
        sb.AppendLine("  let repTag=card.querySelector('.rep-tag');");
        sb.AppendLine("  if(!repTag){");
        sb.AppendLine("    repTag=document.createElement('div');");
        sb.AppendLine("    repTag.className='rep-tag';");
        sb.AppendLine("    repTag.style.cssText='background:rgba(239,68,68,.18);border:1px solid rgba(239,68,68,.5);color:#fca5a5;font-weight:800;font-size:11px;text-transform:uppercase;letter-spacing:.1em;padding:5px 12px;text-align:center;border-radius:6px;margin:8px 16px 0;';");
        sb.AppendLine("    repTag.textContent='🔧 EN REPARACIÓN';");
        sb.AppendLine("    const cardTop=card.querySelector('.card-top');");
        sb.AppendLine("    if(cardTop)cardTop.after(repTag);");
        sb.AppendLine("  }");
        // Ocultar TODOS los botones de acción excepto Generar Correctivo
        sb.AppendLine("  const actions=card.querySelector('.card-actions');");
        sb.AppendLine("  if(actions){");
        sb.AppendLine("    actions.querySelectorAll('.btn').forEach(b=>{b.style.display='none';});");
        sb.AppendLine("    // Mostrar solo botón de Generar Correctivo");
        sb.AppendLine("    let btnCorr=actions.querySelector('.btn-correctivo-rep');");
        sb.AppendLine("    if(!btnCorr){");
        sb.AppendLine("      btnCorr=document.createElement('button');");
        sb.AppendLine("      btnCorr.className='btn btn-danger btn-correctivo-rep';");
        sb.AppendLine("      btnCorr.style.width='100%';");
        sb.AppendLine("      const ub=document.getElementById('ubicacion_'+id)?.value||'';");
        sb.AppendLine("      const pl=document.getElementById('recal-planta')?.textContent||'';");
        sb.AppendLine("      const eq=document.getElementById('recal-id-equipo')?.textContent||'';");
        sb.AppendLine("      btnCorr.textContent='⚠️ Generar Correctivo';");
        sb.AppendLine("      btnCorr.onclick=()=>abrirModalCorrectivo(id,pl,eq,ub);");
        sb.AppendLine("      const row=actions.querySelector('.ca-edit-row')||actions;");
        sb.AppendLine("      row.appendChild(btnCorr);");
        sb.AppendLine("    }else{btnCorr.style.display='';}");
        sb.AppendLine("  }");
        // Ocultar sección PM
        sb.AppendLine("  const pmSec=card.querySelector('.pm-section');");
        sb.AppendLine("  if(pmSec)pmSec.style.display='none';");
        sb.AppendLine("}");

        // ── Recalendarización JS ──
        sb.AppendLine("let recalIdDispositivo=null,recalIdOcupante=null;");
        sb.AppendLine("let todasUbicaciones=[];");
        sb.AppendLine("async function cargarUbicacionesRecal(){");
        sb.AppendLine("  try{const r=await fetch('/PREVENTIVO/UBICACIONES_TODAS');const d=await r.json();todasUbicaciones=d.ubicaciones||[];");
        sb.AppendLine("    ['recal-ub-list','recal-ub-list2'].forEach(lid=>{const dl=document.getElementById(lid);if(dl){dl.innerHTML=todasUbicaciones.map(u=>'<option value=\"'+u+'\">').join('');}});");
        sb.AppendLine("  }catch(e){}");
        sb.AppendLine("}");
        sb.AppendLine("function abrirRecal(id,idEquipo,ubActual,planta,dispositivo){");
        sb.AppendLine("  if(!usuarioActual){abrirLogin();return;}");
        sb.AppendLine("  recalIdDispositivo=id;recalIdOcupante=null;");
        sb.AppendLine("  document.getElementById('recal-id-equipo').textContent=idEquipo;");
        sb.AppendLine("  document.getElementById('recal-dispositivo').textContent=dispositivo;");
        sb.AppendLine("  document.getElementById('recal-ub-actual').textContent=ubActual;");
        sb.AppendLine("  document.getElementById('recal-planta').textContent=planta;");
        sb.AppendLine("  document.getElementById('recal-nueva-ub').value='';");
        sb.AppendLine("  document.getElementById('recal-ub-ocupante').value='';");
        sb.AppendLine("  document.getElementById('recal-paso2').style.display='none';");
        sb.AppendLine("  document.getElementById('recal-error').style.display='none';");
        sb.AppendLine("  document.getElementById('btnRecalConfirmar').disabled=false;");
        sb.AppendLine("  document.getElementById('btnRecalConfirmar').textContent='Confirmar →';");
        sb.AppendLine("  cargarUbicacionesRecal();");
        sb.AppendLine("  document.getElementById('modalRecal').classList.add('show');");
        sb.AppendLine("  setTimeout(()=>document.getElementById('recal-nueva-ub').focus(),100);");
        sb.AppendLine("}");
        sb.AppendLine("function cerrarRecal(){document.getElementById('modalRecal').classList.remove('show');}");
        sb.AppendLine("async function confirmarRecal(){");
        sb.AppendLine("  const errEl=document.getElementById('recal-error');");
        sb.AppendLine("  errEl.style.display='none';");
        sb.AppendLine("  const nuevaUb=document.getElementById('recal-nueva-ub').value.trim();");
        sb.AppendLine("  if(!nuevaUb){errEl.textContent='Ingresa la nueva ubicación';errEl.style.display='block';return;}");
        sb.AppendLine("  const ubOcupante=document.getElementById('recal-ub-ocupante').value.trim();");
        sb.AppendLine("  const paso2=document.getElementById('recal-paso2').style.display!=='none';");
        sb.AppendLine("  if(paso2&&!ubOcupante){errEl.textContent='Ingresa dónde mover el dispositivo existente';errEl.style.display='block';return;}");
        sb.AppendLine("  const btn=document.getElementById('btnRecalConfirmar');");
        sb.AppendLine("  btn.disabled=true;btn.textContent='Guardando...';");
        sb.AppendLine("  try{");
        sb.AppendLine("    const body={idDispositivo:recalIdDispositivo,nuevaUbicacion:nuevaUb,usuario:usuarioActual};");
        sb.AppendLine("    if(paso2)body.ubicacionOcupante=ubOcupante;");
        sb.AppendLine("    const res=await fetch('/PREVENTIVO/RECALENDARIZAR',{method:'POST',headers:{'Content-Type':'application/json','X-Usuario':usuarioActual},body:JSON.stringify(body)});");
        sb.AppendLine("    const data=await res.json();");
        sb.AppendLine("    if(data.ok){toast('Recalendarización completada ✅',true);cerrarRecal();}");
        sb.AppendLine("    else if(data.ocupada){");
        sb.AppendLine("      // Mostrar paso 2");
        sb.AppendLine("      document.getElementById('recal-ocupante-info').textContent=(data.dispositivo_ocupante||'')+(data.equipo_ocupante?' ('+data.equipo_ocupante+')':'');");
        sb.AppendLine("      document.getElementById('recal-paso2').style.display='block';");
        sb.AppendLine("      btn.disabled=false;btn.textContent='Confirmar →';");
        sb.AppendLine("    }else{errEl.textContent=data.error||'Error desconocido';errEl.style.display='block';btn.disabled=false;btn.textContent='Confirmar →';}");
        sb.AppendLine("  }catch(e){errEl.textContent='Error de conexión';errEl.style.display='block';btn.disabled=false;btn.textContent='Confirmar →';}");
        sb.AppendLine("}");
        sb.AppendLine("// Animación de botones");
        sb.AppendLine("document.addEventListener('click',function(e){");
        sb.AppendLine("  const btn=e.target.closest('.btn');");
        sb.AppendLine("  if(!btn)return;");
        sb.AppendLine("  btn.classList.remove('animating');");
        sb.AppendLine("  void btn.offsetWidth;");
        sb.AppendLine("  btn.classList.add('animating');");
        sb.AppendLine("  btn.addEventListener('animationend',()=>btn.classList.remove('animating'),{once:true});");
        sb.AppendLine("  const r=document.createElement('span');");
        sb.AppendLine("  r.className='btn-ripple';");
        sb.AppendLine("  const rect=btn.getBoundingClientRect();");
        sb.AppendLine("  r.style.left=(e.clientX-rect.left)+'px';");
        sb.AppendLine("  r.style.top=(e.clientY-rect.top)+'px';");
        sb.AppendLine("  btn.appendChild(r);");
        sb.AppendLine("  setTimeout(()=>r.remove(),500);");
        sb.AppendLine("});");
        // ── Generar QR de cada dispositivo al cargar ──
        sb.AppendLine("document.addEventListener('DOMContentLoaded',function(){");
        sb.AppendLine("  // Generar QR de cada dispositivo");
        sb.AppendLine("  document.querySelectorAll('[id^=\"qr_\"]').forEach(function(el){");
        sb.AppendLine("    const equipo=el.getAttribute('data-equipo')||'SIN-ID';");
        sb.AppendLine("    new QRCode(el,{text:equipo,width:74,height:74,correctLevel:QRCode.CorrectLevel.M});");
        sb.AppendLine("  });");
        sb.AppendLine("  // ── Restaurar sesion desde cookie si existe ──");
        sb.AppendLine("  fetch('/obtener-usuario',{credentials:'include'})");
        sb.AppendLine("    .then(function(r){return r.ok?r.json():null;})");
        sb.AppendLine("    .then(function(data){");
        sb.AppendLine("      if(!data||!data.usuario)return;");
        sb.AppendLine("      usuarioActual=data.usuario;");
        sb.AppendLine("      nombreActual=data.nombre||data.usuario;");
        sb.AppendLine("      const esAdmin=data.rol==='ADMIN';");
        sb.AppendLine("      document.getElementById('userNombre').textContent=nombreActual;");
        sb.AppendLine("      document.getElementById('userChip').style.display='flex';");
        sb.AppendLine("      document.getElementById('btnLogin').style.display='none';");
        sb.AppendLine("      document.querySelectorAll('.card').forEach(function(card){");
        sb.AppendLine("        const p1=card.dataset.tienePm==='true';");
        sb.AppendLine("        const p2=card.dataset.tienePm2==='true';");
        sb.AppendLine("        const sec=card.querySelector('.pm-section');if(sec)sec.style.display='flex';");
        sb.AppendLine("        const b1hacer=card.querySelector('.btn-p1');");
        sb.AppendLine("        const b2hacer=card.querySelector('.btn-p2');");
        sb.AppendLine("        const b1ver=card.querySelector('.btn-ver1');");
        sb.AppendLine("        const b1edit=card.querySelector('.btn-edit1');");
        sb.AppendLine("        const b1del=card.querySelector('.btn-del1');");
        sb.AppendLine("        const b2ver=card.querySelector('.btn-ver2');");
        sb.AppendLine("        const b2edit=card.querySelector('.btn-edit2');");
        sb.AppendLine("        const b2del=card.querySelector('.btn-del2');");
        sb.AppendLine("        if(b1hacer)b1hacer.style.display=p1?'none':'inline-flex';");
        sb.AppendLine("        if(b1ver)b1ver.style.display=p1?'inline-flex':'none';");
        sb.AppendLine("        if(b1edit)b1edit.style.display=p1?'inline-flex':'none';");
        sb.AppendLine("        if(b1del)b1del.style.display=(p1&&esAdmin)?'inline-flex':'none';");
        sb.AppendLine("        if(b2hacer)b2hacer.style.display=p2?'none':'inline-flex';");
        sb.AppendLine("        if(b2ver)b2ver.style.display=p2?'inline-flex':'none';");
        sb.AppendLine("        if(b2edit)b2edit.style.display=p2?'inline-flex':'none';");
        sb.AppendLine("        if(b2del)b2del.style.display=(p2&&esAdmin)?'inline-flex':'none';");
        sb.AppendLine("      });");
        sb.AppendLine("      // Mostrar botones de recalendarización");
        sb.AppendLine("      document.querySelectorAll('[id^=\"btn_recal_\"]').forEach(b=>b.style.display='inline-flex');");
        sb.AppendLine("    })");
        sb.AppendLine("    .catch(function(){});");
        sb.AppendLine("});");
        // ── Función imprimir QRs — etiquetadora 10cm x 16cm, todos en una sola hoja ──
        sb.AppendLine("function imprimirTodosQr(){");
        sb.AppendLine("  const tarjetas=[];");
        sb.AppendLine("  document.querySelectorAll('[id^=\"qr_\"]').forEach(function(el){");
        sb.AppendLine("    const equipo=el.getAttribute('data-equipo')||'';");
        sb.AppendLine("    const card=el.closest('.card');");
        sb.AppendLine("    const disp=card?card.querySelector('.dev-name h3')?.textContent||'':'';");
        sb.AppendLine("    if(equipo) tarjetas.push({equipo,disp});");
        sb.AppendLine("  });");
        sb.AppendLine("  if(!tarjetas.length){alert('No hay dispositivos para imprimir.');return;}");
        sb.AppendLine("  const w=window.open('','_blank','width=420,height=680');");
        sb.AppendLine("  let html='<!DOCTYPE html><html><head><meta charset=\"UTF-8\">';");
        sb.AppendLine("  html+='<title>Imprimir QR — Etiquetadora</title>';");
        sb.AppendLine("  html+='<script src=\"https://cdnjs.cloudflare.com/ajax/libs/qrcodejs/1.0.0/qrcode.min.js\"><\\/script>';");
        sb.AppendLine("  html+='<style>';");
        sb.AppendLine("  /* Tamaño real de la etiqueta: 10cm ancho x 16cm alto */");
        sb.AppendLine("  html+='@page{size:100mm 160mm;margin:0;}';");
        sb.AppendLine("  html+='*{box-sizing:border-box;margin:0;padding:0;}';");
        // Pantalla: centrar preview con fondo gris
        sb.AppendLine("  html+='body{font-family:Arial,sans-serif;background:#e5e7eb;display:flex;flex-direction:column;align-items:center;justify-content:flex-start;padding:20px;gap:16px;min-height:100vh;}';");
        sb.AppendLine("  html+='.no-print{display:flex;gap:10px;align-items:center;margin-bottom:4px;}';");
        sb.AppendLine("  html+='.btn-print{padding:10px 28px;background:#3B82F6;color:white;border:none;border-radius:8px;font-size:13px;font-weight:600;cursor:pointer;}';");
        sb.AppendLine("  html+='.btn-print:hover{background:#2563EB;}';");
        // La etiqueta en pantalla muestra una sombra para simular la hoja
        sb.AppendLine("  html+='.etiqueta{width:100mm;background:#fff;box-shadow:0 4px 24px #0002;border-radius:6px;padding:6mm 5mm;display:flex;flex-direction:column;align-items:center;gap:4mm;page-break-after:always;}';");
        sb.AppendLine("  html+='.etiqueta:last-child{page-break-after:auto;}';");
        sb.AppendLine("  html+='.eti-titulo{font-size:8pt;font-weight:700;color:#64748b;text-transform:uppercase;letter-spacing:.08em;text-align:center;}';");
        sb.AppendLine("  html+='.eti-qrs{display:flex;flex-direction:row;justify-content:center;align-items:flex-start;flex-wrap:wrap;gap:4mm;width:100%;}';");
        sb.AppendLine("  html+='.eti-item{display:flex;flex-direction:column;align-items:center;gap:1.5mm;}';");
        sb.AppendLine("  html+='.eti-disp{font-size:7pt;font-weight:700;color:#1e293b;text-align:center;max-width:28mm;line-height:1.2;}';");
        sb.AppendLine("  html+='.eti-id{font-size:7.5pt;font-weight:700;color:#3B82F6;font-family:monospace;text-align:center;}';");
        sb.AppendLine("  html+='.eti-sep{width:80%;height:1px;background:#e2e8f0;margin:1mm 0;}';");
        sb.AppendLine("  html+='.eti-ubicacion{font-size:6.5pt;color:#94a3b8;text-align:center;}';");

        // Al imprimir: sin fondo gris, tamaño exacto, sin sombra
        sb.AppendLine("  html+='@media print{body{background:#fff!important;padding:0!important;gap:0!important;}.no-print{display:none!important;}.etiqueta{width:100mm;min-height:160mm;box-shadow:none!important;border-radius:0!important;padding:6mm 5mm;}}';");
        sb.AppendLine("  html+='</style></head><body>';");
        sb.AppendLine("  html+='<div class=\"no-print\"><button class=\"btn-print\" onclick=\"window.print()\">🖨️ Imprimir etiqueta</button></div>';");
        // Una sola etiqueta con todos los QR juntos
        sb.AppendLine("  const ubicacion=document.querySelector('.top-title p')?.textContent||'';");
        sb.AppendLine("  html+='<div class=\"etiqueta\">';");
        sb.AppendLine("  html+='<div class=\"eti-titulo\">📋 Dispositivos — '+ubicacion+'</div>';");
        // ── QRs individuales ──
        sb.AppendLine("  html+='<div class=\"eti-qrs\">';");
        sb.AppendLine("  tarjetas.forEach(function(t,i){");
        sb.AppendLine("    html+='<div class=\"eti-item\">';");
        sb.AppendLine("    html+='<div id=\"qr'+i+'\"></div>';");
        sb.AppendLine("    html+='<div class=\"eti-disp\">'+t.disp+'</div>';");
        sb.AppendLine("    html+='<div class=\"eti-id\">'+t.equipo+'</div>';");
        sb.AppendLine("    html+='</div>';");
        sb.AppendLine("  });");
        sb.AppendLine("  html+='</div>';");  // cierra eti-qrs
        sb.AppendLine("  html+='<div class=\"eti-sep\"></div>';");
        sb.AppendLine("  html+='<div class=\"eti-ubicacion\">'+ubicacion+'</div>';");
        sb.AppendLine("  html+='</div>';");  // cierra etiqueta
        sb.AppendLine("  html+='<script>';");
        // QR de 26mm (~98px a 96dpi) para que quepan 3 en 10cm de ancho
        sb.AppendLine("  tarjetas.forEach(function(t,i){");
        sb.AppendLine("    html+='new QRCode(document.getElementById(\"qr'+i+'\"),{text:\"'+t.equipo+'\",width:98,height:98,correctLevel:QRCode.CorrectLevel.M});';");
        sb.AppendLine("  });");
        sb.AppendLine("  html+='<\\/script></body></html>';");
        sb.AppendLine("  w.document.write(html);");
        sb.AppendLine("  w.document.close();");
        sb.AppendLine("}");
        // ── Close the main <script> block ──
        sb.AppendLine("</script>");
        // ── Modal Correctivo (HTML outside <script>) ──
        sb.AppendLine("<style>.auto-tag{font-size:9px;font-weight:700;padding:1px 6px;border-radius:4px;background:rgba(59,130,246,.18);color:#60a5fa;letter-spacing:.06em;vertical-align:middle;margin-left:4px;}.input-auto{background:rgba(59,130,246,.06)!important;border-color:rgba(59,130,246,.3)!important;color:var(--muted2)!important;cursor:not-allowed!important;}</style>");
        sb.AppendLine("<div class=\"modal\" id=\"modalCorrectivo\">");
        sb.AppendLine("<div class=\"modal-box\" style=\"width:min(520px,95vw);max-height:90vh;overflow-y:auto;\">");
        sb.AppendLine("<h3>⚠️ Registrar Correctivo</h3>");
        sb.AppendLine("<p>Completa los datos del correctivo</p>");
        sb.AppendLine("<div class=\"modal-field\"><label>Planta <span class=\"auto-tag\">AUTO</span></label>");
        sb.AppendLine("<input id=\"c_planta\" type=\"text\" readonly class=\"input-auto\"></div>");
        sb.AppendLine("<div class=\"modal-field\"><label>Fecha</label>");
        sb.AppendLine("<input id=\"c_fecha\" type=\"date\"></div>");
        sb.AppendLine("<div class=\"modal-field\"><label>Línea / Persona <span class=\"auto-tag\">AUTO</span></label>");
        sb.AppendLine("<input id=\"c_linea\" type=\"text\" readonly class=\"input-auto\"></div>");
        sb.AppendLine("<div class=\"modal-field\"><label>Equipo <span class=\"auto-tag\">AUTO</span></label>");
        sb.AppendLine("<input id=\"c_equipo\" type=\"text\" readonly class=\"input-auto\"></div>");
        sb.AppendLine("<div class=\"modal-field\"><label>Marca</label>");
        sb.AppendLine("<input id=\"c_marca\" type=\"text\"></div>");
        sb.AppendLine("<div class=\"modal-field\"><label>Modelo</label>");
        sb.AppendLine("<input id=\"c_modelo\" type=\"text\"></div>");
        sb.AppendLine("<div class=\"modal-field\"><label>No Serie</label>");
        sb.AppendLine("<input id=\"c_serie\" type=\"text\"></div>");
        sb.AppendLine("<div class=\"modal-field\"><label>Reporte elaborado por <span class=\"auto-tag\">AUTO</span></label>");
        sb.AppendLine("<input id=\"c_reporte\" type=\"text\" readonly class=\"input-auto\"></div>");
        sb.AppendLine("<div class=\"modal-field\"><label>Descripción de la falla</label>");
        sb.AppendLine("<input id=\"c_falla\" type=\"text\"></div>");
        sb.AppendLine("<div class=\"modal-field\"><label>Accesorios solicitados</label>");
        sb.AppendLine("<input id=\"c_accesorios\" type=\"text\"></div>");
        sb.AppendLine("<div class=\"modal-footer\">");
        sb.AppendLine("<button class=\"btn btn-ghost\" onclick=\"cerrarModalCorrectivo()\">Cancelar</button>");
        sb.AppendLine("<button class=\"btn btn-danger\" onclick=\"guardarCorrectivo()\">Guardar Correctivo</button>");
        sb.AppendLine("</div>");
        sb.AppendLine("</div></div>");
        // ── Modal Baja de Equipo ──────────────────────────────────────────────
        sb.AppendLine("<div class=\"modal\" id=\"modalBaja\">");
        sb.AppendLine("<div class=\"modal-baja-box\">");
        sb.AppendLine("<h3 style=\"font-size:17px;font-weight:700;margin-bottom:4px;\">📤 Baja de Equipo</h3>");
        sb.AppendLine("<p style=\"font-size:12px;color:var(--muted2);margin-bottom:18px;\">Registra la baja y asigna el equipo de reemplazo</p>");
        sb.AppendLine("<div class=\"baja-grid\">");
        sb.AppendLine("  <div class=\"baja-field\"><label>Folio</label><input id=\"baja_FOLIO\" placeholder=\"Ej: BJ-2025-001\"></div>");
        sb.AppendLine("  <div class=\"baja-field\"><label>Planta <span class=\"auto-tag\">AUTO</span></label><input id=\"baja_PLANTA\" class=\"auto-fill\" readonly></div>");
        sb.AppendLine("  <div class=\"baja-field\"><label>Fecha</label><input id=\"baja_FECHA\" type=\"date\"></div>");
        sb.AppendLine("  <div class=\"baja-field\"><label>Equipo (ID actual) <span class=\"auto-tag\">AUTO</span></label><input id=\"baja_EQUIPO\" class=\"auto-fill\" readonly></div>");
        sb.AppendLine("  <div class=\"baja-field\"><label>Marca</label><input id=\"baja_MARCA\" placeholder=\"Ej: HP, Dell\"></div>");
        sb.AppendLine("  <div class=\"baja-field\"><label>Modelo</label><input id=\"baja_MODELO\" placeholder=\"Ej: EliteBook 840\"></div>");
        sb.AppendLine("  <div class=\"baja-field\"><label>No. Serie <span class=\"auto-tag\">AUTO (id_equipo)</span></label><input id=\"baja_NO_SERIE\" class=\"auto-fill\" readonly></div>");
        sb.AppendLine("  <div class=\"baja-field\"><label>Activo Fijo</label><input id=\"baja_ACTIVO_FIJO\" placeholder=\"Ej: AF-00123\"></div>");
        sb.AppendLine("  <div class=\"baja-field full\"><label>Ubicación / Persona <span class=\"auto-tag\">AUTO</span></label><input id=\"baja_UBICACION_PERSONA\" class=\"auto-fill\" readonly></div>");
        sb.AppendLine("  <div class=\"baja-field full\"><label>Motivo de Baja</label><input id=\"baja_MOTIVO_DE_BAJA\" placeholder=\"Motivo...\"></div>");
        sb.AppendLine("  <div class=\"baja-field full\"><label>Diagnóstico</label><textarea id=\"baja_DIAGNOSTICO\" placeholder=\"Diagnóstico técnico...\"></textarea></div>");
        sb.AppendLine("  <div class=\"baja-field full\"><label>Comentarios</label><textarea id=\"baja_COMENTARIOS\" placeholder=\"Comentarios adicionales...\"></textarea></div>");
        sb.AppendLine("  <div class=\"baja-field full\"><label>Motivo de Cancelación</label><input id=\"baja_MOTIVO_DE_CANCELACION\" placeholder=\"Solo si aplica\"></div>");
        sb.AppendLine("  <div class=\"baja-field full\" style=\"border-top:1px solid rgba(234,88,12,.35);padding-top:14px;margin-top:4px;\">");
        sb.AppendLine("    <label style=\"color:#fb923c;\">🔄 ID Equipo de Reemplazo <span style=\"color:#ef4444;\">*</span></label>");
        sb.AppendLine("    <input id=\"baja_ID_REEMPLAZO\" placeholder=\"ID del equipo que reemplaza (obligatorio)\" style=\"border-color:rgba(234,88,12,.5);\">");
        sb.AppendLine("    <span style=\"font-size:10px;color:var(--muted2);margin-top:3px;\">Este ID actualizará el id_equipo de la tarjeta en el sistema</span>");
        sb.AppendLine("  </div>");
        sb.AppendLine("</div>");
        sb.AppendLine("<div id=\"baja-error\" style=\"color:#fca5a5;font-size:12px;margin-bottom:10px;display:none;\"></div>");
        sb.AppendLine("<div style=\"display:flex;gap:8px;justify-content:flex-end;\">");
        sb.AppendLine("  <button class=\"btn btn-ghost\" onclick=\"cerrarBaja()\">Cancelar</button>");
        sb.AppendLine("  <button class=\"btn\" style=\"background:linear-gradient(135deg,#7c2d12,#ea580c);color:white;\" onclick=\"guardarBaja()\">💾 Registrar Baja</button>");
        sb.AppendLine("</div>");
        sb.AppendLine("</div></div>");
        // ── JS: lógica de Baja de Equipo ─────────────────────────────────────
        sb.AppendLine("<script>");
        sb.AppendLine("let bajaTemp={};");
        sb.AppendLine("function abrirBaja(id,periodo,idEquipo,ubicacion,planta){");
        sb.AppendLine("  if(!usuarioActual){abrirLogin();return;}");
        sb.AppendLine("  bajaTemp={id:id,periodo:periodo,idEquipoOriginal:idEquipo};");
        sb.AppendLine("  // Auto-llenado campos mapeados");
        sb.AppendLine("  document.getElementById('baja_PLANTA').value=planta||'';");
        sb.AppendLine("  document.getElementById('baja_EQUIPO').value=idEquipo||'';");
        sb.AppendLine("  document.getElementById('baja_NO_SERIE').value=idEquipo||'';");
        sb.AppendLine("  document.getElementById('baja_UBICACION_PERSONA').value=ubicacion||'';");
        sb.AppendLine("  document.getElementById('baja_FECHA').value=new Date().toISOString().split('T')[0];");
        sb.AppendLine("  // Limpiar campos manuales");
        sb.AppendLine("  ['baja_FOLIO','baja_MARCA','baja_MODELO','baja_ACTIVO_FIJO',");
        sb.AppendLine("   'baja_MOTIVO_DE_BAJA','baja_DIAGNOSTICO','baja_COMENTARIOS',");
        sb.AppendLine("   'baja_MOTIVO_DE_CANCELACION','baja_ID_REEMPLAZO'].forEach(function(fid){");
        sb.AppendLine("    var el=document.getElementById(fid);if(el)el.value='';");
        sb.AppendLine("  });");
        sb.AppendLine("  document.getElementById('baja-error').style.display='none';");
        sb.AppendLine("  document.getElementById('modalBaja').classList.add('show');");
        sb.AppendLine("  setTimeout(function(){document.getElementById('baja_FOLIO').focus();},100);");
        sb.AppendLine("}");
        sb.AppendLine("function cerrarBaja(){document.getElementById('modalBaja').classList.remove('show');}");
        sb.AppendLine("async function guardarBaja(){");
        sb.AppendLine("  var errEl=document.getElementById('baja-error');errEl.style.display='none';");
        sb.AppendLine("  var idReemplazo=document.getElementById('baja_ID_REEMPLAZO').value.trim();");
        sb.AppendLine("  if(!idReemplazo){errEl.textContent='El ID Equipo de Reemplazo es obligatorio';errEl.style.display='block';return;}");
        sb.AppendLine("  var fecha=document.getElementById('baja_FECHA').value;");
        sb.AppendLine("  if(!fecha){errEl.textContent='Selecciona la fecha';errEl.style.display='block';return;}");
        sb.AppendLine("  var btnGuardar=document.querySelector('#modalBaja .btn:not(.btn-ghost)');");
        sb.AppendLine("  btnGuardar.disabled=true;btnGuardar.textContent='Guardando...';");
        sb.AppendLine("  try{");
        sb.AppendLine("    var body={");
        sb.AppendLine("      bajaDto:{");
        sb.AppendLine("        FOLIO:document.getElementById('baja_FOLIO').value,");
        sb.AppendLine("        ESTADO:'PENDIENTE',");
        sb.AppendLine("        PLANTA:document.getElementById('baja_PLANTA').value,");
        sb.AppendLine("        FECHA:fecha,");
        sb.AppendLine("        EQUIPO:document.getElementById('baja_EQUIPO').value,");
        sb.AppendLine("        MARCA:document.getElementById('baja_MARCA').value,");
        sb.AppendLine("        MODELO:document.getElementById('baja_MODELO').value,");
        sb.AppendLine("        NO_SERIE:document.getElementById('baja_NO_SERIE').value,");
        sb.AppendLine("        ACTIVO_FIJO:document.getElementById('baja_ACTIVO_FIJO').value,");
        sb.AppendLine("        UBICACION_PERSONA:document.getElementById('baja_UBICACION_PERSONA').value,");
        sb.AppendLine("        MOTIVO_DE_BAJA:document.getElementById('baja_MOTIVO_DE_BAJA').value,");
        sb.AppendLine("        DIAGNOSTICO:document.getElementById('baja_DIAGNOSTICO').value,");
        sb.AppendLine("        COMENTARIOS:document.getElementById('baja_COMENTARIOS').value,");
        sb.AppendLine("        MOTIVO_DE_CANCELACION:document.getElementById('baja_MOTIVO_DE_CANCELACION').value");
        sb.AppendLine("      },");
        sb.AppendLine("      idPreventivoDb:bajaTemp.id,");
        sb.AppendLine("      idEquipoReemplazo:idReemplazo,");
        sb.AppendLine("      periodo:bajaTemp.periodo,");
        sb.AppendLine("      usuario:usuarioActual");
        sb.AppendLine("    };");
        sb.AppendLine("    var res=await fetch('/PREVENTIVO/REGISTRAR_BAJA',{method:'POST',headers:{'Content-Type':'application/json','X-Usuario':usuarioActual},body:JSON.stringify(body)});");
        sb.AppendLine("    var data=await res.json();");
        sb.AppendLine("    if(data.ok){");
        sb.AppendLine("      var idAnterior=bajaTemp.idEquipoOriginal;");
        sb.AppendLine("      var p=bajaTemp.periodo;");
        sb.AppendLine("      var card=document.getElementById('equipo_'+bajaTemp.id)?.closest('.card');");
        sb.AppendLine("      if(card){");
        sb.AppendLine("        // Actualizar ID equipo en input y encabezado");
        sb.AppendLine("        var equipoInput=document.getElementById('equipo_'+bajaTemp.id);");
        sb.AppendLine("        if(equipoInput)equipoInput.value=idReemplazo;");
        sb.AppendLine("        var idSpan=card.querySelector('.dev-name span');if(idSpan)idSpan.textContent=idReemplazo;");
        sb.AppendLine("        // Observaciones del PM del período");
        sb.AppendLine("        var obsEl=document.getElementById('obs_pm'+p+'_'+bajaTemp.id);");
        sb.AppendLine("        if(obsEl){var obsMsg='Antes: '+idAnterior+', se dio de baja, ahora: '+idReemplazo;obsEl.value=(obsEl.value?obsEl.value+'\\n':'')+obsMsg;}");
        sb.AppendLine("        // Marcar período como completado: badge + plazo + botones");
        sb.AppendLine("        var badge=document.getElementById('pbadge'+p+'_'+bajaTemp.id);");
        sb.AppendLine("        if(badge){badge.textContent='📋 P'+p+': ✅ Registrado';badge.className='periodo-badge periodo-ok';}");
        sb.AppendLine("        var plazoEl=document.getElementById(p===2?'plazo_p2_'+bajaTemp.id:'plazo_'+bajaTemp.id);");
        sb.AppendLine("        if(plazoEl&&data.proximo_pm)plazoEl.textContent=data.proximo_pm;");
        sb.AppendLine("        if(p===1){");
        sb.AppendLine("          card.dataset.tienePm='true';");
        sb.AppendLine("          var b1=card.querySelector('.btn-p1');if(b1)b1.style.display='none';");
        sb.AppendLine("          var bv1=card.querySelector('.btn-ver1');if(bv1)bv1.style.display='inline-flex';");
        sb.AppendLine("          var be1=card.querySelector('.btn-edit1');if(be1)be1.style.display='inline-flex';");
        sb.AppendLine("        }else{");
        sb.AppendLine("          card.dataset.tienePm2='true';");
        sb.AppendLine("          var b2=card.querySelector('.btn-p2');if(b2)b2.style.display='none';");
        sb.AppendLine("          var bv2=card.querySelector('.btn-ver2');if(bv2)bv2.style.display='inline-flex';");
        sb.AppendLine("          var be2=card.querySelector('.btn-edit2');if(be2)be2.style.display='inline-flex';");
        sb.AppendLine("        }");
        sb.AppendLine("        // Regenerar QR con el nuevo ID");
        sb.AppendLine("        var qrEl=document.getElementById('qr_'+bajaTemp.id);");
        sb.AppendLine("        if(qrEl){qrEl.innerHTML='';qrEl.setAttribute('data-equipo',idReemplazo);");
        sb.AppendLine("          new QRCode(qrEl,{text:idReemplazo,width:74,height:74,correctLevel:QRCode.CorrectLevel.M});");
        sb.AppendLine("          var qrLabel=qrEl.nextElementSibling;if(qrLabel)qrLabel.textContent=idReemplazo;}");
        sb.AppendLine("      }");
        sb.AppendLine("      cerrarBaja();");
        sb.AppendLine("      toast('Baja registrada — PM P'+p+' completado — Equipo: '+idReemplazo,true);");
        sb.AppendLine("    }else{errEl.textContent=data.error||'Error al registrar baja';errEl.style.display='block';}");
        sb.AppendLine("  }catch(e){errEl.textContent='Error de conexión. Intenta de nuevo.';errEl.style.display='block';}");
        sb.AppendLine("  finally{btnGuardar.disabled=false;btnGuardar.textContent='💾 Registrar Baja';}");
        sb.AppendLine("}");
        // ── Cambio de planta ─────────────────────────────────────────────────────
        sb.AppendLine(@"
function abrirCambioPlantaModal(){
  document.getElementById('cp-error').style.display='none';
  document.getElementById('cp-planta').value='';
  document.getElementById('btnCpGuardar').disabled=false;
  document.getElementById('btnCpGuardar').textContent='💾 Guardar';
  document.getElementById('modalCambioPlanta').classList.add('show');
}
function cerrarCambioPlantaModal(){
  document.getElementById('modalCambioPlanta').classList.remove('show');
}
async function guardarCambioPlanta(){
  const errEl=document.getElementById('cp-error');
  errEl.style.display='none';
  const planta=document.getElementById('cp-planta').value.trim();
  if(!planta){errEl.textContent='Debes seleccionar una planta';errEl.style.display='block';return;}
  const btn=document.getElementById('btnCpGuardar');
  btn.disabled=true;btn.textContent='⏳ Guardando...';
  try{
    const res=await fetch('/PREVENTIVO/CAMBIO_PLANTA',{
      method:'POST',
      headers:{'Content-Type':'application/json','X-Usuario':usuarioActual||''},
      body:JSON.stringify({idDispositivo:recalIdDispositivo,planta,usuario:usuarioActual})
    });
    const data=await res.json();
    if(data.ok){
      // 4.3 Actualizar tarjeta en frontend
      const card=document.getElementById('equipo_'+recalIdDispositivo)?.closest('.card');
      if(card){
        const plantaEl=card.querySelector('.card-planta');
        if(plantaEl)plantaEl.textContent=planta;
        // Actualizar también el input oculto de planta si existe
        const plantaInput=document.getElementById('planta_'+recalIdDispositivo);
        if(plantaInput)plantaInput.value=planta;
        // Actualizar el dato visible en el modal de recalendarización
        document.getElementById('recal-planta').textContent=planta;
      }
      cerrarCambioPlantaModal();
      toast('✅ Planta actualizada: '+planta,true);
    }else{
      errEl.textContent=data.error||'Error desconocido';
      errEl.style.display='block';
      btn.disabled=false;btn.textContent='💾 Guardar';
    }
  }catch(e){
    errEl.textContent='Error de conexión';
    errEl.style.display='block';
    btn.disabled=false;btn.textContent='💾 Guardar';
  }
}
");

        // ── Stock Soporte Site ───────────────────────────────────────────────
        sb.AppendLine(@"
function abrirStockModal(){
  document.getElementById('stock-error').style.display='none';
  document.getElementById('stock-rack').value='A';
  document.getElementById('stock-espacio').value='1';
  document.getElementById('btnStockGuardar').disabled=false;
  document.getElementById('btnStockGuardar').textContent='💾 Guardar';
  document.getElementById('modalStock').classList.add('show');
}
function cerrarStockModal(){
  document.getElementById('modalStock').classList.remove('show');
}
async function guardarStock(){
  const errEl=document.getElementById('stock-error');
  errEl.style.display='none';

  const rack=document.getElementById('stock-rack').value.trim();
  const espacio=document.getElementById('stock-espacio').value.trim();
  const nuevaUbicacion='Soporte Site ('+rack+') '+espacio;

  // 4.1 Pedir ID de dispositivo de reemplazo (obligatorio)
  const idReemplazo=prompt('Ingresa el ID del dispositivo de reemplazo (id_dispositivo_reemplazo):');
  if(!idReemplazo||!idReemplazo.trim()){
    errEl.textContent='El ID de reemplazo es obligatorio';
    errEl.style.display='block';
    return;
  }

  const btn=document.getElementById('btnStockGuardar');
  btn.disabled=true; btn.textContent='⏳ Guardando...';

  try{
    const body={
      idDispositivo: recalIdDispositivo,
      idReemplazo: idReemplazo.trim(),
      rack,
      espacio,
      nuevaUbicacion,
      usuario: usuarioActual
    };
    const res=await fetch('/PREVENTIVO/STOCK',{
      method:'POST',
      headers:{'Content-Type':'application/json','X-Usuario':usuarioActual||''},
      body:JSON.stringify(body)
    });
    const data=await res.json();
    if(data.ok){
      // 4.6 Actualizar frontend
      const card=document.getElementById('equipo_'+recalIdDispositivo)?.closest('.card');
      if(card){
        // Actualizar ubicación visible
        const ubEl=card.querySelector('.card-ubicacion');
        if(ubEl) ubEl.textContent=nuevaUbicacion;
        // Cambiar color de la tarjeta a rosa
        card.style.borderColor='rgba(244,114,182,.5)';
        const badge=card.querySelector('.color-badge');
        if(badge){ badge.style.background='rgba(244,114,182,.15)'; badge.style.color='#f472b6'; badge.style.borderColor='rgba(244,114,182,.4)'; badge.textContent='Rosa'; }
      }
      // Rellenar campo nueva ubicación en modalRecal
      document.getElementById('recal-nueva-ub').value=nuevaUbicacion;
      cerrarStockModal();
      toast('✅ Equipo movido a '+nuevaUbicacion+' | Reemplazo: '+idReemplazo.trim(), true);
    }else{
      errEl.textContent=data.error||'Error desconocido';
      errEl.style.display='block';
      btn.disabled=false; btn.textContent='💾 Guardar';
    }
  }catch(e){
    errEl.textContent='Error de conexión';
    errEl.style.display='block';
    btn.disabled=false; btn.textContent='💾 Guardar';
  }
}
");

        sb.AppendLine("</script>");
        sb.AppendLine("</body></html>");
        return sb.ToString();
    }


    // ── POST /PREVENTIVO/STOCK ────────────────────────────────────────────────
    // 1. Duplica el registro del dispositivo actual con el nuevo id_equipo (reemplazo)
    // 2. Actualiza ubicacion y categoria_color="ROSA" del dispositivo original
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
            if (string.IsNullOrWhiteSpace(data.IdReemplazo))
                return Ok(new { ok = false, error = "ID de reemplazo es obligatorio" });
            if (string.IsNullOrWhiteSpace(data.Rack) || string.IsNullOrWhiteSpace(data.Espacio))
                return Ok(new { ok = false, error = "Rack y espacio son requeridos" });

            var nuevaUbicacion = $"Soporte Site ({data.Rack.Trim()}) {data.Espacio.Trim()}";

            using var conn = _db.Open();

            // 4.2 Leer datos completos del dispositivo actual para duplicar
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

            // 4.2 Crear nuevo registro (equipo de reemplazo) con toda la info del actual
            using var ins = conn.CreateCommand();
            ins.CommandText = """
                INSERT INTO public.mantenimientos_preventivos
                (id_equipo, ubicacion, plazo, realizado_por, fecha_realizacion,
                 observaciones, nombre_dispositivo, planta, categoria_color, anio_creacion)
                VALUES (@e, @u, @p, @rp, @fr, @o, @nd, @pl, @cc, @ac)
                RETURNING id
                """;
            ins.Parameters.AddWithValue("e", (object?)data.IdReemplazo ?? DBNull.Value);
            ins.Parameters.AddWithValue("u", (object?)ubicOrig ?? DBNull.Value);
            ins.Parameters.AddWithValue("p", (object?)plazoOrig ?? DBNull.Value);
            ins.Parameters.AddWithValue("rp", (object?)realPorOrig ?? DBNull.Value);
            ins.Parameters.AddWithValue("fr", (object?)fechaOrig ?? DBNull.Value);
            ins.Parameters.AddWithValue("o", (object?)obsOrig ?? DBNull.Value);
            ins.Parameters.AddWithValue("nd", (object?)nomDispOrig ?? DBNull.Value);
            ins.Parameters.AddWithValue("pl", (object?)plantaOrig ?? DBNull.Value);
            ins.Parameters.AddWithValue("cc", (object?)colorOrig ?? DBNull.Value);
            ins.Parameters.AddWithValue("ac", (object?)anioOrig ?? DBNull.Value);
            var nuevoId = ins.ExecuteScalar();

            // 4.3 + 4.4 + 4.5 Actualizar dispositivo actual: nueva ubicacion y color ROSA
            using var upd = conn.CreateCommand();
            upd.CommandText = """
                UPDATE public.mantenimientos_preventivos
                SET ubicacion = @u, categoria_color = 'ROSA'
                WHERE id = @id
                """;
            upd.Parameters.AddWithValue("u", nuevaUbicacion);
            upd.Parameters.AddWithValue("id", data.IdDispositivo);
            upd.ExecuteNonQuery();

            return Ok(new
            {
                ok = true,
                nueva_ubicacion = nuevaUbicacion,
                id_nuevo_registro = nuevoId,
                mensaje = $"Equipo movido a {nuevaUbicacion}. Reemplazo registrado con ID {data.IdReemplazo}."
            });
        }
        catch (Exception ex) { return Ok(new { ok = false, error = ex.Message }); }
    }

    // ── POST /PREVENTIVO/CAMBIO_PLANTA ───────────────────────────────────────
    // Actualiza SOLO el campo planta del registro. Sin duplicar, sin cambiar status.
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
            upd.CommandText = "UPDATE public.mantenimientos_preventivos SET planta = @p WHERE id = @id";
            upd.Parameters.AddWithValue("p", data.Planta.Trim());
            upd.Parameters.AddWithValue("id", data.IdDispositivo);
            int rows = upd.ExecuteNonQuery();

            if (rows == 0)
                return Ok(new { ok = false, error = "Dispositivo no encontrado" });

            return Ok(new { ok = true, planta = data.Planta.Trim() });
        }
        catch (Exception ex) { return Ok(new { ok = false, error = ex.Message }); }
    }

    public class CambioPlantaRequest
    {
        public long IdDispositivo { get; set; }
        public string Planta { get; set; } = "";
        public string? Usuario { get; set; }
    }

    public class StockRequest
    {
        public long IdDispositivo { get; set; }
        public string IdReemplazo { get; set; } = "";
        public string Rack { get; set; } = "";
        public string Espacio { get; set; } = "";
        public string NuevaUbicacion { get; set; } = "";
        public string? Usuario { get; set; }
    }

    private static DateTime LunesDeSemanaISO(int anio, int semana)
    {
        var simple = new DateTime(anio, 1, 1).AddDays((semana - 1) * 7);
        int dow = (int)simple.DayOfWeek; // 0=dom,1=lun,...,6=sab
        int offset = dow == 0 ? -6 : 1 - dow;
        return simple.AddDays(offset);
    }

    private static string Esc(string? s) =>
        (s ?? "").Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;");

    private static (string color, string bg, string label) ColorBadge(string cat)
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

    private static string DispIcon(string disp)
    {
        var d = (disp ?? "").ToUpper();
        if (d.Contains("COMPUTADORA") || d.Contains("CPU")) return "🖥️";
        if (d.Contains("PORTATIL") || d.Contains("LAPTOP")) return "💻";
        if (d.Contains("IMPRESORA")) return "🖨️";
        if (d.Contains("UPS")) return "🔋";
        return "🔧";
    }

    private static string ActsHtml(string disp)
    {
        var sb = new StringBuilder();
        foreach (var a in ActividadesDispositivo(disp))
            sb.Append("<label class=\"act-item\"><input type=\"checkbox\"><span class=\"act-check\"></span><span class=\"act-text\">" + Esc(a) + "</span></label>");
        return sb.ToString();
    }

    private static List<string> ActividadesDispositivo(string disp)
    {
        var d = (disp ?? "").ToUpper();
        if (d.Contains("COMPUTADORA") || d.Contains("CPU"))
            return new List<string> { "Sopletear el gabinete", "Limpieza de contactos de memoria RAM", "Sopletear fuente de poder y ventiladores", "Limpieza del gabinete", "Limpieza del monitor o pantalla", "Limpieza y sopleteado del teclado y mouse", "Sopleteado de ventiladores y ranuras de enfriamiento", "Limpieza exterior del lector óptico", "Limpieza del cableado", "Actualizaciones del sistema operativo", "Actualizaciones de Office", "Eliminación de archivos temporales y vaciar reciclaje", "Revisión del antivirus y escaneo", "Desfragmentar las unidades de disco duro", "Conectar todos los periféricos correspondientes", "Verificar cables y conectores sin daños", "Encender el equipo y verificar funcionamiento", "Verificar que los periféricos funcionen correctamente", "Verificación vida de la pila del BIOS", "Cambiar Qr del Dispositivo", "Cambiar Qr del Area" };
        if (d.Contains("PORTATIL") || d.Contains("LAPTOP"))
            return new List<string> { "Sopletear el gabinete / chasis", "Limpieza de contactos de memoria RAM", "Sopletear fuente de poder y ventiladores", "Limpieza del monitor o pantalla", "Limpieza y sopleteado del teclado y touchpad", "Sopleteado de ventiladores y ranuras de enfriamiento", "Limpieza del cableado", "Actualizaciones del sistema operativo", "Actualizaciones de Office", "Eliminación de archivos temporales y vaciar reciclaje", "Revisión del antivirus y escaneo", "Desfragmentar las unidades de disco duro", "Conectar todos los periféricos correspondientes", "Verificar cables y conectores sin daños", "Encender el equipo y verificar funcionamiento", "Verificar que los periféricos funcionen correctamente", "Cambiar Qr de la Laptop" };
        if (d.Contains("IMPRESORA"))
            return new List<string> { "Sopletear la impresora térmica", "Limpieza de rodillos (no usar alcohol)", "Limpieza del cabezal de la impresora térmica", "Limpieza exterior de la impresora", "Limpieza del cableado", "Rutear cables / anclar eliminador de impresora", "Conectar todos los periféricos correspondientes", "Verificar cables y conectores sin daños", "Verificar que los periféricos funcionen correctamente", "Cambiar Qr del Dispositivo", "cambiar Qr del Dispositivo" };
        if (d.Contains("UPS"))
            return new List<string> { "Limpieza y verificación del UPS", "Limpieza del cableado", "Conectar todos los periféricos correspondientes", "Verificar cables y conectores sin daños", "Verificación vida de la pila del UPS", "Inspección y funcionamiento del UPS", "Verificar que solo equipo IT esté conectado al UPS" };
        return new List<string> { "Inspección general", "Limpieza exterior", "Verificación de funcionamiento" };
    }

    // ── POST /PREVENTIVO/RECAL_REPARACION ────────────────────────────────────
    // Recalendarización por reparación:
    // 4.2 Duplica el registro actual con el id_dispositivo_prestamo
    // 4.3 Construye ubicación: "Soporte Site Reparacion (RACK espacio ESPACIO)"
    // 4.4 Actualiza dispositivo original: ubicacion = generada, categoria_color = 'ROJO'
    //     + inserta en mantenimientos_correctivos con status PENDIENTE
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

            // 4.3 Construir ubicación
            var nuevaUbicacion = $"Soporte Site Reparacion ({data.Rack.Trim()} espacio {data.Espacio.Trim()})";

            using var conn = _db.Open();

            // Leer datos completos del dispositivo actual para duplicar
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
                if (!r.Read())
                    return Ok(new { ok = false, error = "Dispositivo no encontrado" });
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

            // 4.2 Duplicar tarjeta con id_dispositivo_prestamo
            using var ins = conn.CreateCommand();
            ins.CommandText = """
                INSERT INTO public.mantenimientos_preventivos
                (id_equipo, ubicacion, plazo, realizado_por, fecha_realizacion,
                 observaciones, nombre_dispositivo, planta, categoria_color, anio_creacion)
                VALUES (@e, @u, @p, @rp, @fr, @o, @nd, @pl, @cc, @ac)
                RETURNING id
                """;
            ins.Parameters.AddWithValue("e", (object?)data.IdDispositivoPrestamo.Trim() ?? DBNull.Value);
            ins.Parameters.AddWithValue("u", (object?)ubicOrig ?? DBNull.Value);
            ins.Parameters.AddWithValue("p", (object?)plazoOrig ?? DBNull.Value);
            ins.Parameters.AddWithValue("rp", (object?)realPorOrig ?? DBNull.Value);
            ins.Parameters.AddWithValue("fr", (object?)fechaOrig ?? DBNull.Value);
            ins.Parameters.AddWithValue("o", (object?)obsOrig ?? DBNull.Value);
            ins.Parameters.AddWithValue("nd", (object?)nomDispOrig ?? DBNull.Value);
            ins.Parameters.AddWithValue("pl", (object?)plantaOrig ?? DBNull.Value);
            ins.Parameters.AddWithValue("cc", (object?)colorOrig ?? DBNull.Value);
            ins.Parameters.AddWithValue("ac", (object?)anioOrig ?? DBNull.Value);
            ins.ExecuteScalar();

            // 4.4 Actualizar dispositivo original: nueva ubicación + color ROJO
            using var upd = conn.CreateCommand();
            upd.CommandText = """
                UPDATE public.mantenimientos_preventivos
                SET ubicacion = @u, categoria_color = 'ROJO'
                WHERE id = @id
                """;
            upd.Parameters.AddWithValue("u", nuevaUbicacion);
            upd.Parameters.AddWithValue("id", data.IdDispositivo);
            upd.ExecuteNonQuery();

            // Insertar correctivo en mantenimientos_correctivos con los datos del dispositivo
            using var corrCmd = conn.CreateCommand();
            corrCmd.CommandText = """
                INSERT INTO public.mantenimientos_correctivos
                    (status, planta, linea_persona, equipo, descripcion_falla,
                     fecha_solicitud, reporte_elaborado_por, observaciones)
                VALUES
                    ('PENDIENTE', @planta, @linea, @equipo, @falla,
                     CURRENT_DATE, @reporte, @obs)
                """;
            corrCmd.Parameters.AddWithValue("planta", (object?)plantaOrig ?? DBNull.Value);
            corrCmd.Parameters.AddWithValue("linea", (object?)ubicOrig ?? DBNull.Value);
            corrCmd.Parameters.AddWithValue("equipo", (object?)idEquipoOrig ?? DBNull.Value);
            corrCmd.Parameters.AddWithValue("falla", $"Equipo en reparación — enviado a {nuevaUbicacion}");
            corrCmd.Parameters.AddWithValue("reporte", usuario.ToUpper());
            corrCmd.Parameters.AddWithValue("obs", $"Recalendarización por reparación. Equipo préstamo: {data.IdDispositivoPrestamo.Trim()}");
            corrCmd.ExecuteNonQuery();

            return Ok(new
            {
                ok = true,
                nueva_ubicacion = nuevaUbicacion,
                mensaje = $"Equipo enviado a reparación. Ubicación: {nuevaUbicacion}"
            });
        }
        catch (Exception ex) { return Ok(new { ok = false, error = ex.Message }); }
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

// ── DTO para registrar baja desde QrPage ─────────────────────────────────────
public class RegistrarBajaRequest
{
    /// <summary>Datos del formulario de baja (los mismos campos que BajaDto)</summary>
    public BajaDto? BajaDto { get; set; }

    /// <summary>ID (PK) del registro en mantenimientos_preventivos a actualizar</summary>
    public int IdPreventivoDb { get; set; }

    /// <summary>ID del equipo de reemplazo — actualiza id_equipo en preventivos</summary>
    public string? IdEquipoReemplazo { get; set; }

    /// <summary>Período desde donde se presionó el botón (1 o 2) — informativo</summary>
    public int Periodo { get; set; }

    /// <summary>Usuario que realiza la baja</summary>
    public string? Usuario { get; set; }
}