using ChiIT.Data;
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

    [HttpGet("preventivos/qr")]
    public ContentResult VerQrPreventivo([FromQuery(Name = "u")] string ubicacion)
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

                cards.Append("<div class=\"card\" data-tiene-pm=\"" + (row.tienePm ? "true" : "false") + "\" data-tiene-pm2=\"" + (row.tienePm2 ? "true" : "false") + "\">\n");
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
                cards.Append("      <label class=\"act-item act-correctivo\"><input type=\"checkbox\" id=\"req_correctivo1_" + row.id + "\"><span class=\"act-check\"></span><span class=\"act-text\">⚠️ Requiere Correctivo</span></label>\n");
                cards.Append("      <div class=\"form-sep\" style=\"margin-top:10px\">📅 Fecha</div>\n");
                cards.Append("      <input type=\"date\" class=\"date-input\" id=\"fecha1_" + row.id + "\">\n");
                cards.Append("      <div class=\"form-sep\" style=\"margin-top:8px\">📝 Observaciones</div>\n");
                cards.Append("      <textarea class=\"date-input\" style=\"min-height:52px;resize:vertical;\" id=\"obs_pm1_" + row.id + "\" placeholder=\"Observaciones P1...\"></textarea>\n");
                cards.Append("      <div class=\"form-actions\" style=\"margin-top:8px\">\n");
                cards.Append("        <button class=\"btn btn-success\" onclick=\"guardarPreventivo(" + row.id + ",1)\">💾 Guardar P1</button>\n");
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
                cards.Append("      <label class=\"act-item act-correctivo\"><input type=\"checkbox\" id=\"req_correctivo_edit1_" + row.id + "\"><span class=\"act-check\"></span><span class=\"act-text\">⚠️ Requiere Correctivo</span></label>\n");
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
                cards.Append("      <label class=\"act-item act-correctivo\"><input type=\"checkbox\" id=\"req_correctivo2_" + row.id + "\"><span class=\"act-check\"></span><span class=\"act-text\">⚠️ Requiere Correctivo</span></label>\n");
                cards.Append("      <div class=\"form-sep\" style=\"margin-top:10px\">📅 Fecha</div>\n");
                cards.Append("      <input type=\"date\" class=\"date-input\" id=\"fecha2_" + row.id + "\">\n");
                cards.Append("      <div class=\"form-sep\" style=\"margin-top:8px\">📝 Observaciones</div>\n");
                cards.Append("      <textarea class=\"date-input\" style=\"min-height:52px;resize:vertical;\" id=\"obs_pm2_" + row.id + "\" placeholder=\"Observaciones P2...\"></textarea>\n");
                cards.Append("      <div class=\"form-actions\" style=\"margin-top:8px\">\n");
                cards.Append("        <button class=\"btn btn-success\" onclick=\"guardarPreventivo(" + row.id + ",2)\">💾 Guardar P2</button>\n");
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
        sb.AppendLine("  <div class=\"modal-box\" style=\"width:min(440px,95vw);\">");
        sb.AppendLine("    <h3>📍 Recalendarización</h3>");
        sb.AppendLine("    <p>Mueve este dispositivo a una nueva ubicación</p>");
        sb.AppendLine("    <div style=\"background:var(--surface2);border:1px solid var(--border2);border-radius:10px;padding:12px 14px;margin-bottom:16px;font-size:12px;\">");
        sb.AppendLine("      <div style=\"display:grid;grid-template-columns:1fr 1fr;gap:6px;\">");
        sb.AppendLine("        <div><span style=\"color:var(--muted);font-size:10px;text-transform:uppercase;letter-spacing:.06em;\">ID Equipo</span><br><b id=\"recal-id-equipo\" style=\"font-family:'DM Mono',monospace;color:var(--accent);\"></b></div>");
        sb.AppendLine("        <div><span style=\"color:var(--muted);font-size:10px;text-transform:uppercase;letter-spacing:.06em;\">Dispositivo</span><br><b id=\"recal-dispositivo\"></b></div>");
        sb.AppendLine("        <div><span style=\"color:var(--muted);font-size:10px;text-transform:uppercase;letter-spacing:.06em;\">Ubicación actual</span><br><b id=\"recal-ub-actual\" style=\"color:var(--amber);\"></b></div>");
        sb.AppendLine("        <div><span style=\"color:var(--muted);font-size:10px;text-transform:uppercase;letter-spacing:.06em;\">Planta</span><br><b id=\"recal-planta\"></b></div>");
        sb.AppendLine("      </div>");
        sb.AppendLine("    </div>");
        sb.AppendLine("    <div class=\"modal-field\">");
        sb.AppendLine("      <label>📍 Nueva ubicación</label>");
        sb.AppendLine("      <input id=\"recal-nueva-ub\" list=\"recal-ub-list\" type=\"text\" placeholder=\"Escribe o selecciona la ubicación\" autocomplete=\"off\">");
        sb.AppendLine("      <datalist id=\"recal-ub-list\"></datalist>");
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
        sb.AppendLine("      <button class=\"btn btn-ghost\" onclick=\"cerrarRecal()\">Cancelar</button>");
        sb.AppendLine("      <button class=\"btn btn-primary\" id=\"btnRecalConfirmar\" onclick=\"confirmarRecal()\">Confirmar →</button>");
        sb.AppendLine("    </div>");
        sb.AppendLine("  </div>");
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
        sb.AppendLine("function toast(msg,ok){const t=document.createElement('div');t.className='toast '+(ok?'toast-ok':'toast-err');t.textContent=msg;document.body.appendChild(t);setTimeout(()=>t.remove(),3000);}");
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
        // Capturar la URL de la página padre ANTES de abrir la ventana nueva
        sb.AppendLine("  const urlGeneral=window.location.href;");
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
        // Estilos para el QR general grande
        sb.AppendLine("  html+='.eti-qr-general-wrap{display:flex;flex-direction:column;align-items:center;gap:2mm;width:100%;padding:4mm 5mm;background:linear-gradient(135deg,#0f172a,#1e293b);border-radius:6px;border:1.5px solid #3B82F6;margin-bottom:3mm;}';");
        sb.AppendLine("  html+='.eti-qr-general-label{font-size:7pt;font-weight:700;color:#94a3b8;text-transform:uppercase;letter-spacing:.12em;text-align:center;}';");
        sb.AppendLine("  html+='.eti-qr-general-title{font-size:9.5pt;font-weight:700;color:#f1f5f9;text-align:center;line-height:1.25;}';");
        sb.AppendLine("  html+='.eti-qr-general-sub{font-size:6.5pt;color:#3B82F6;text-align:center;letter-spacing:.06em;}';");
        sb.AppendLine("  html+='.eti-qr-general-box{background:white;padding:5px;border-radius:5px;box-shadow:0 0 0 2px #3B82F6,0 0 0 4px #0f172a;}';");
        sb.AppendLine("  html+='.eti-qr-general-divider{width:70%;height:1px;background:rgba(59,130,246,.35);margin:1mm 0;}';");
        // Al imprimir: sin fondo gris, tamaño exacto, sin sombra
        sb.AppendLine("  html+='@media print{body{background:#fff!important;padding:0!important;gap:0!important;}.no-print{display:none!important;}.etiqueta{width:100mm;min-height:160mm;box-shadow:none!important;border-radius:0!important;padding:6mm 5mm;}}';");
        sb.AppendLine("  html+='</style></head><body>';");
        sb.AppendLine("  html+='<div class=\"no-print\"><button class=\"btn-print\" onclick=\"window.print()\">🖨️ Imprimir etiqueta</button></div>';");
        // Una sola etiqueta con todos los QR juntos
        sb.AppendLine("  const ubicacion=document.querySelector('.top-title p')?.textContent||'';");
        sb.AppendLine("  html+='<div class=\"etiqueta\">';");
        sb.AppendLine("  html+='<div class=\"eti-titulo\">📋 Dispositivos — '+ubicacion+'</div>';");
        // ── QR General del área ──
        sb.AppendLine("  html+='<div class=\"eti-qr-general-wrap\">';");
        sb.AppendLine("  html+='<div class=\"eti-qr-general-label\">🌐 Acceso General al Área</div>';");
        sb.AppendLine("  html+='<div class=\"eti-qr-general-box\" id=\"qr_general\"></div>';");
        sb.AppendLine("  html+='<div class=\"eti-qr-general-divider\"></div>';");
        sb.AppendLine("  html+='<div class=\"eti-qr-general-title\">'+ubicacion+'</div>';");
        sb.AppendLine("  html+='<div class=\"eti-qr-general-sub\">📋 Ver todos los dispositivos del área</div>';");
        sb.AppendLine("  html+='</div>';");
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
        // QR general grande (140px ~37mm) — urlGeneral fue capturada en la ventana padre antes de abrir la nueva
        sb.AppendLine("  html+='new QRCode(document.getElementById(\"qr_general\"),{text:\"'+urlGeneral+'\",width:98,height:98,correctLevel:QRCode.CorrectLevel.M});';");
        // QR de 26mm (~98px a 96dpi) para que quepan 3 en 10cm de ancho
        sb.AppendLine("  tarjetas.forEach(function(t,i){");
        sb.AppendLine("    html+='new QRCode(document.getElementById(\"qr'+i+'\"),{text:\"'+t.equipo+'\",width:98,height:98,correctLevel:QRCode.CorrectLevel.M});';");
        sb.AppendLine("  });");
        sb.AppendLine("  html+='<\\/script></body></html>';");
        sb.AppendLine("  w.document.write(html);");
        sb.AppendLine("  w.document.close();");
        sb.AppendLine("}");
        sb.AppendLine("</script></body></html>");
        return sb.ToString();
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
}