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

                // ========== BOTONES PRINCIPALES EN LA PARTE SUPERIOR (justo después del body) ==========
                cards.Append("  </div>\n"); // cierra card-body
                cards.Append("  <div class=\"card-actions-top\">\n");
                cards.Append("    <div class=\"ca-edit-row\">\n");
                cards.Append("      <button class=\"btn btn-blue\" onclick=\"abrirEditar(" + row.id + ")\">✏️ Editar</button>\n");
                cards.Append("      <button class=\"btn btn-green\" onclick=\"guardarCambios(" + row.id + ")\">💾 Guardar</button>\n");
                cards.Append("      <button class=\"btn btn-ghost\" onclick=\"cancelarTodo(" + row.id + ")\">↩ Cancelar</button>\n");
                cards.Append("      <button class=\"btn btn-ghost\" onclick=\"window.close()\">✕ Salir</button>\n");
                cards.Append("      <button class=\"btn btn-recal\" id=\"btn_recal_" + row.id + "\" style=\"display:none\" " +
                    "onclick=\"abrirRecal(" + row.id + ",'" + Esc(row.idEquipo) + "','" + Esc(ubicacion) + "','" + Esc(row.planta) + "','" + Esc(row.dispositivo) + "')\">📍 Recalendarización</button>\n");
                cards.Append("    </div>\n");
                cards.Append("  </div>\n");

                // ========== SECCIÓN PM Y QR ==========
                cards.Append("  <div class=\"card-actions-pm\">\n");
                cards.Append("    " + btnPm + "\n");
                cards.Append("  </div>\n");

                // ── Formularios de registro (P1 y P2) ──
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

                // ── Sección QR del dispositivo ──
                cards.Append("    <div class=\"qr-section\">\n");
                cards.Append("      <div class=\"form-sep\" style=\"margin:10px 0 6px\">📷 QR del Dispositivo</div>\n");
                cards.Append("      <div style=\"display:flex;align-items:center;gap:14px;\">\n");
                cards.Append("        <div id=\"qr_" + row.id + "\" data-equipo=\"" + Esc(row.idEquipo) + "\" style=\"background:white;padding:8px;border-radius:8px;width:90px;height:90px;flex-shrink:0;\"></div>\n");
                cards.Append("        <span style=\"font-size:11px;font-family:'DM Mono',monospace;color:var(--muted2);\">" + Esc(row.idEquipo) + "</span>\n");
                cards.Append("      </div>\n");
                cards.Append("    </div>\n");
                cards.Append("</div>\n"); // cierra card
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

            var bajaId = (long)(await cmdBaja.ExecuteScalarAsync())!;

            // 2. Actualizar el id_equipo del preventivo con el equipo de reemplazo
            await using var cmdUpdate = new Npgsql.NpgsqlCommand("""
                UPDATE mantenimientos_preventivos
                SET id_equipo = @id_equipo_nuevo
                WHERE id_equipo = @id_equipo_viejo
                """, conn);

            cmdUpdate.Parameters.AddWithValue("id_equipo_nuevo", req.IdEquipoReemplazo);
            cmdUpdate.Parameters.AddWithValue("id_equipo_viejo", req.BajaDto.EQUIPO ?? "");
            await cmdUpdate.ExecuteNonQueryAsync();

            return Ok(new { id = bajaId });
        }
        catch (Exception ex)
        {
            Console.WriteLine("[REGISTRAR_BAJA] Error: " + ex);
            return StatusCode(500, new { error = ex.Message });
        }
    }

    // ── Helpers ─────────────────────────────────────────────────────────────
    private static string Esc(string? s) =>
        string.IsNullOrEmpty(s) ? "" :
        s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;");

    private static string HtmlPage(string ubicacion, string cards) => $$"""
        <!DOCTYPE html>
        <html lang="es">
        <!-- contenido generado por QrPageController -->
        <head>
          <meta charset="UTF-8">
          <meta name="viewport" content="width=device-width,initial-scale=1">
          <title>Preventivos — {{ubicacion}}</title>
        </head>
        <body>
          {{cards}}
        </body>
        </html>
        """;
}

// ── DTOs ────────────────────────────────────────────────────────────────────
public class RegistrarBajaRequest
{
    public BajaEquipoDto? BajaDto { get; set; }
    public string? IdEquipoReemplazo { get; set; }
}

public class BajaEquipoDto
{
    public string? FOLIO { get; set; }
    public string? ESTADO { get; set; }
    public string? PLANTA { get; set; }
    public string? FECHA { get; set; }
    public string? EQUIPO { get; set; }
    public string? MARCA { get; set; }
    public string? MODELO { get; set; }
    public string? NO_SERIE { get; set; }
    public string? ACTIVO_FIJO { get; set; }
    public string? UBICACION_PERSONA { get; set; }
    public string? MOTIVO_DE_BAJA { get; set; }
    public string? DIAGNOSTICO { get; set; }
    public string? COMENTARIOS { get; set; }
    public string? MOTIVO_DE_CANCELACION { get; set; }
}