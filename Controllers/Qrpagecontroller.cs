using ChiIT.Data;
using Microsoft.AspNetCore.Mvc;
using System.Text;

namespace ChiIT.Controllers;

[ApiController]
public class QrPageController : ControllerBase
{
    private readonly DbConnectionPool _db;
    public QrPageController(DbConnectionPool db) => _db = db;

    [HttpGet("preventivos/qr/{ubicacion}")]
    public ContentResult VerQrPreventivo(string ubicacion)
    {
        ubicacion = Uri.UnescapeDataString(ubicacion);

        using var conn = _db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT id, id_equipo, nombre_dispositivo, planta,
                   categoria_color, fecha_realizacion, plazo, observaciones,
                   CASE WHEN preventivo_digital IS NOT NULL THEN true ELSE false END AS tiene_pm,
                   anio_creacion
            FROM public.mantenimientos_preventivos
            WHERE TRIM(LOWER(ubicacion)) = TRIM(LOWER(@u))
            ORDER BY nombre_dispositivo
            """;
        cmd.Parameters.AddWithValue("u", ubicacion);

        var rows = new List<(long id, string idEquipo, string dispositivo, string planta,
                             string colorCat, string? fecha, string? plazo, string obs, bool tienePm, int? anio)>();

        using var r = cmd.ExecuteReader();
        while (r.Read())
            rows.Add((r.GetInt64(0),
                      r.IsDBNull(1) ? "" : r.GetString(1),
                      r.IsDBNull(2) ? "" : r.GetString(2),
                      r.IsDBNull(3) ? "" : r.GetString(3),
                      r.IsDBNull(4) ? "" : r.GetString(4),
                      r.IsDBNull(5) ? null : r.GetDateTime(5).ToString("yyyy-MM-dd"),
                      r.IsDBNull(6) ? null : r.GetString(6),
                      r.IsDBNull(7) ? "" : r.GetString(7),
                      !r.IsDBNull(8) && r.GetBoolean(8),
                      r.IsDBNull(9) ? (int?)null : r.GetInt32(9)));

        var cards = new StringBuilder();
        foreach (var row in rows)
        {
            var (badgeColor, badgeBg, badgeLabel) = ColorBadge(row.colorCat);
            var icon = DispIcon(row.dispositivo);
            var fechaStr = row.fecha ?? "Sin registro";
            var plazoStr = row.plazo ?? "No definido";
            var dotClass = row.fecha != null ? "dot-ok" : "dot-warn";
            var dotLabel = row.fecha != null ? "Último PM: " + fechaStr : "Sin mantenimiento registrado";
            var actsHtml = ActsHtml(row.dispositivo);

            // Siempre generar los 4 botones — todos ocultos, se muestran al login segun estado
            string btnPm =
                "<button class=\"pm-btn btn-pm btn btn-purple\" id=\"btn_hacer_" + row.id + "\" onclick=\"abrirForm(" + row.id + ")\" style=\"display:none\">📋 Hacer Preventivo</button>\n" +
                "<button class=\"pm-btn btn-ver btn btn-cyan\"   id=\"btn_ver_" + row.id + "\" onclick=\"verPM(" + row.id + ")\" style=\"display:none\">👁 Ver PM</button>\n" +
                "<button class=\"pm-btn btn-edit btn btn-amber\"  id=\"btn_edit_" + row.id + "\" onclick=\"abrirEditarPM(" + row.id + ")\" style=\"display:none\">✏️ Editar PM</button>\n" +
                "<button class=\"pm-btn btn-del btn btn-danger\" id=\"btn_del_" + row.id + "\" onclick=\"eliminarPreventivo(" + row.id + ")\" style=\"display:none\">🗑 Eliminar</button>";
            bool tienePmFlag = row.tienePm;

            cards.Append("<div class=\"card\" data-tiene-pm=\"" + (row.tienePm ? "true" : "false") + "\">\n");
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
            cards.Append("      <div class=\"info-item\"><label>Dispositivo</label><input id=\"disp_" + row.id + "\" value=\"" + Esc(row.dispositivo) + "\" disabled></div>\n");
            cards.Append("      <div class=\"info-item\"><label>Planta</label><input id=\"planta_" + row.id + "\" value=\"" + Esc(row.planta) + "\" disabled></div>\n");
            cards.Append("      <div class=\"info-item\"><label>Color</label><input id=\"color_" + row.id + "\" value=\"" + Esc(row.colorCat) + "\" disabled></div>\n");
            cards.Append("      <div class=\"info-item\"><label>Año Creación</label><input id=\"anio_vis_" + row.id + "\" value=\"" + (row.anio?.ToString() ?? "") + "\" disabled></div>\n");
            cards.Append("    </div>\n");
            cards.Append("    <div class=\"status-row\">\n");
            cards.Append("      <span class=\"status-dot " + dotClass + "\"></span>\n");
            cards.Append("      <span>" + dotLabel + "</span>\n");
            cards.Append("      <span style=\"margin-left:auto;font-family:'DM Mono',monospace;font-size:10px;color:#475569\">Plazo: " + plazoStr + "</span>\n");
            cards.Append("    </div>\n");
            cards.Append("    <div id=\"obswrap_" + row.id + "\" style=\"display:block;margin-top:10px\">\n");
            cards.Append("      <div class=\"obs-label\">Observaciones</div>\n");
            cards.Append("      <textarea class=\"obs-edit-field\" id=\"obs_" + row.id + "\" disabled>" + Esc(row.obs) + "</textarea>\n");
            cards.Append("    </div>\n");
            cards.Append("    <div class=\"mini-form\" id=\"form_" + row.id + "\" style=\"display:none\">\n");
            cards.Append("      <div class=\"form-sep\" style=\"margin-top:12px\">📋 Actividades</div>\n");
            cards.Append("      <div class=\"acts-list\">" + actsHtml + "</div>\n");
            cards.Append("      <div class=\"form-sep\" style=\"margin-top:10px\">📅 Fecha</div>\n");
            cards.Append("      <input type=\"date\" class=\"date-input\" id=\"fecha_" + row.id + "\">\n");
            cards.Append("      <div class=\"form-sep\" style=\"margin-top:8px\">📝 Observaciones</div>\n");
            cards.Append("      <textarea class=\"date-input\" style=\"min-height:52px;resize:vertical;\" id=\"obs_pm_" + row.id + "\" placeholder=\"Observaciones...\"></textarea>\n");
            cards.Append("      <div class=\"form-actions\" style=\"margin-top:8px\">\n");
            cards.Append("        <button class=\"btn btn-ghost\" onclick=\"cancelarForm(" + row.id + ")\">✕ Cancelar</button>\n");
            cards.Append("        <button class=\"btn btn-success\" onclick=\"guardarPreventivo(" + row.id + ")\">💾 Guardar</button>\n");
            cards.Append("      </div>\n    </div>\n");
            cards.Append("    <div class=\"mini-form\" id=\"ver_" + row.id + "\" style=\"display:none\">\n");
            cards.Append("      <div class=\"form-sep\" style=\"margin-top:12px;color:#06B6D4\">👁 Preventivo registrado</div>\n");
            cards.Append("      <div class=\"acts-list\" id=\"ver_acts_" + row.id + "\" style=\"max-height:180px;overflow-y:auto\"></div>\n");
            cards.Append("      <div style=\"margin-top:10px;font-size:11px;color:var(--muted2);display:flex;flex-wrap:wrap;gap:8px\">\n");
            cards.Append("        <span>👤 <b id=\"ver_usuario_" + row.id + "\"></b></span>\n");
            cards.Append("        <span>📅 <b id=\"ver_fecha_" + row.id + "\"></b></span>\n");
            cards.Append("        <span>⏭ <b id=\"ver_proximo_" + row.id + "\"></b></span>\n");
            cards.Append("      </div>\n");
            cards.Append("      <div style=\"margin-top:6px;font-size:11px;color:var(--muted2)\" id=\"ver_obs_" + row.id + "\"></div>\n");
            cards.Append("      <div class=\"form-actions\" style=\"margin-top:8px\">\n");
            cards.Append("        <button class=\"btn btn-ghost\" onclick=\"cerrarVer(" + row.id + ")\">✕ Cerrar</button>\n");
            cards.Append("      </div>\n    </div>\n");
            cards.Append("    <div class=\"mini-form\" id=\"edit_pm_" + row.id + "\" style=\"display:none\">\n");
            cards.Append("      <div class=\"form-sep\" style=\"margin-top:12px;color:var(--amber)\">✏️ Editar Preventivo</div>\n");
            cards.Append("      <div class=\"acts-list\" id=\"edit_acts_" + row.id + "\">" + actsHtml + "</div>\n");
            cards.Append("      <div class=\"form-sep\" style=\"margin-top:10px\">📅 Fecha</div>\n");
            cards.Append("      <input type=\"date\" class=\"date-input\" id=\"edit_fecha_" + row.id + "\">\n");
            cards.Append("      <div class=\"form-sep\" style=\"margin-top:8px\">📝 Observaciones</div>\n");
            cards.Append("      <textarea class=\"date-input\" style=\"min-height:52px;resize:vertical;\" id=\"edit_obs_pm_" + row.id + "\" placeholder=\"Observaciones...\"></textarea>\n");
            cards.Append("      <div class=\"form-actions\" style=\"margin-top:8px\">\n");
            cards.Append("        <button class=\"btn btn-ghost\" onclick=\"cerrarEditarPM(" + row.id + ")\">✕ Cancelar</button>\n");
            cards.Append("        <button class=\"btn btn-amber\" onclick=\"guardarEditarPM(" + row.id + ")\">💾 Guardar Cambios</button>\n");
            cards.Append("      </div>\n    </div>\n  </div>\n");
            cards.Append("  <div class=\"card-actions\">\n");
            cards.Append("    <button class=\"btn btn-blue\" onclick=\"abrirEditar(" + row.id + ")\">✏️ Editar</button>\n");
            cards.Append("    <button class=\"btn btn-green\" onclick=\"guardarCambios(" + row.id + ")\">💾 Guardar</button>\n");
            cards.Append("    <button class=\"btn btn-ghost\" onclick=\"cancelarEditar(" + row.id + ")\">↩ Cancelar</button>\n");
            cards.Append("    <button class=\"btn btn-ghost\" onclick=\"window.close()\">✕ Salir</button>\n");
            cards.Append("    " + btnPm + "\n");
            cards.Append("  </div>\n</div>\n");
        }

        var html = HtmlPage(Esc(ubicacion), cards.ToString());
        return Content(html, "text/html; charset=utf-8");
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
        sb.AppendLine(".btn-login{background:linear-gradient(135deg,#1D4ED8,#3B82F6);color:white;}");
        sb.AppendLine(".grid{display:grid;grid-template-columns:repeat(auto-fill,minmax(320px,1fr));gap:14px;padding:16px 20px;}");
        sb.AppendLine(".card{background:var(--surface);border:1px solid var(--border);border-radius:var(--radius);overflow:hidden;transition:border-color .2s;}");
        sb.AppendLine(".card-top{display:flex;align-items:center;gap:12px;padding:14px 16px;border-bottom:1px solid var(--border);background:var(--surface2);}");
        sb.AppendLine(".dev-icon{width:38px;height:38px;border-radius:9px;background:rgba(59,130,246,.12);border:1px solid rgba(59,130,246,.2);display:flex;align-items:center;justify-content:center;font-size:18px;flex-shrink:0;}");
        sb.AppendLine(".dev-name{flex:1;}.dev-name h3{font-size:13px;font-weight:700;}.dev-name span{font-size:11px;color:var(--muted2);}");
        sb.AppendLine(".color-badge{padding:4px 10px;border-radius:999px;font-size:10px;font-weight:700;text-transform:uppercase;}");
        sb.AppendLine(".card-body{padding:14px 16px;}");
        sb.AppendLine(".info-row{display:grid;grid-template-columns:1fr 1fr 1fr;gap:10px;margin-bottom:12px;}");
        sb.AppendLine(".info-item label{font-size:9px;font-weight:600;text-transform:uppercase;letter-spacing:.1em;color:var(--muted);display:block;margin-bottom:4px;}");
        sb.AppendLine(".info-item input{width:100%;background:var(--surface2);border:1px solid var(--border2);border-radius:6px;padding:7px 9px;font-size:12px;color:var(--text);opacity:.6;}");
        sb.AppendLine(".status-row{display:flex;align-items:center;gap:8px;padding:9px 12px;background:rgba(255,255,255,.03);border:1px solid var(--border);border-radius:8px;margin-bottom:12px;font-size:11px;color:var(--muted2);}");
        sb.AppendLine(".status-dot{width:7px;height:7px;border-radius:50%;flex-shrink:0;}");
        sb.AppendLine(".dot-ok{background:var(--green);box-shadow:0 0 6px var(--green);}.dot-warn{background:var(--amber);box-shadow:0 0 6px var(--amber);}");
        sb.AppendLine(".pm-btn{font-size:12px !important;padding:8px 14px !important;}");
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
        sb.AppendLine(".card-actions{display:flex;flex-wrap:wrap;gap:6px;padding:12px 16px;border-top:1px solid var(--border);background:rgba(0,0,0,.15);}");
        sb.AppendLine(".card-actions .btn{font-size:11px;padding:7px 12px;}");
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
        sb.AppendLine("</style></head><body>");
        sb.AppendLine("<div class=\"top-bar\">");
        sb.AppendLine("  <div class=\"top-icon\">🔧</div>");
        sb.AppendLine("  <div class=\"top-title\"><h1>Mantenimiento Preventivo</h1><p>📍 " + ubicacion + "</p></div>");
        sb.AppendLine("  <div class=\"user-chip\" id=\"userChip\">👤 <span id=\"userNombre\"></span></div>");
        sb.AppendLine("  <button class=\"btn btn-login\" id=\"btnLogin\" onclick=\"abrirLogin()\">🔑 Iniciar Sesión</button>");
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
        sb.AppendLine("<script>");
        sb.AppendLine("let usuarioActual=null,nombreActual=null,usuarioTarjeta={};");
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
        sb.AppendLine("  document.getElementById('userNombre').textContent=nombreActual;");
        sb.AppendLine("  document.getElementById('userChip').style.display='flex';");
        sb.AppendLine("  document.getElementById('btnLogin').style.display='none';");
        sb.AppendLine("  document.querySelectorAll('.card').forEach(card=>{");
        sb.AppendLine("    const tienePm=card.dataset.tienePm==='true';");
        sb.AppendLine("    card.querySelectorAll('.btn-pm').forEach(b=>b.style.display=tienePm?'none':'inline-flex');");
        sb.AppendLine("    card.querySelectorAll('.btn-ver,.btn-edit,.btn-del').forEach(b=>b.style.display=tienePm?'inline-flex':'none');");
        sb.AppendLine("  });");
        sb.AppendLine("  cerrarLogin();toast('Sesión iniciada — '+nombreActual,true);");
        sb.AppendLine("}");
        sb.AppendLine("function abrirForm(id){if(!usuarioActual){abrirLogin();return;}document.getElementById('form_'+id).style.display='block';document.getElementById('fecha_'+id).value=new Date().toISOString().split('T')[0];}");
        sb.AppendLine("function cancelarForm(id){document.getElementById('form_'+id).style.display='none';document.querySelectorAll('#form_'+id+' input[type=checkbox]').forEach(cb=>cb.checked=false);}");
        sb.AppendLine("async function guardarPreventivo(id){");
        sb.AppendLine("  const fecha=document.getElementById('fecha_'+id).value;");
        sb.AppendLine("  if(!fecha){toast('Selecciona la fecha',false);return;}");
        sb.AppendLine("  const cbs=document.querySelectorAll('#form_'+id+' input[type=checkbox]');");
        sb.AppendLine("  const checks=[];cbs.forEach((cb,i)=>{if(cb.checked)checks.push(i);});");
        sb.AppendLine("  if(!checks.length){toast('Marca al menos una actividad',false);return;}");
        sb.AppendLine("  const obs=document.getElementById('obs_pm_'+id)?.value||'';");
        sb.AppendLine("  const btn=document.querySelector('#form_'+id+' .btn-success');");
        sb.AppendLine("  btn.disabled=true;btn.textContent='Guardando...';");
        sb.AppendLine("  const res=await fetch('/PREVENTIVO/GUARDAR_PM/'+id,{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify({usuario:usuarioActual,fecha,checks,observaciones:obs})});");
        sb.AppendLine("  const data=await res.json();");
        sb.AppendLine("  if(data.ok){");
        sb.AppendLine("    toast('Guardado. Proximo PM: '+data.proximo_pm,true);");
        sb.AppendLine("    const card=document.getElementById('form_'+id).closest('.card');");
        sb.AppendLine("    card.querySelector('.status-dot').className='status-dot dot-ok';");
        sb.AppendLine("    card.querySelectorAll('.status-row span')[1].textContent='Último PM: '+fecha;");
        sb.AppendLine("    card.querySelectorAll('.status-row span')[2].textContent='Plazo: '+data.proximo_pm;");
        sb.AppendLine("    cancelarForm(id);");
        sb.AppendLine("    // Intercambiar botones: quitar btn-purple, mostrar ver/editar/eliminar");
        sb.AppendLine("    card.dataset.tienePm='true'; card.querySelectorAll('.btn-pm').forEach(b=>b.style.display='none');");
        sb.AppendLine("    card.querySelectorAll('.btn-ver,.btn-edit,.btn-del').forEach(b=>b.style.display='inline-flex');");
        sb.AppendLine("  }else{btn.disabled=false;btn.textContent='Guardar';toast('Error: '+(data.error||'desconocido'),false);}");
        sb.AppendLine("}");
        sb.AppendLine("async function verPM(id){");
        sb.AppendLine("  const res=await fetch('/PREVENTIVO/DIGITAL/'+id);const data=await res.json();");
        sb.AppendLine("  if(!data.existe){toast('No hay preventivo guardado',false);return;}");
        sb.AppendLine("  const pm=data.data;");
        sb.AppendLine("  const actsEl=document.getElementById('ver_acts_'+id);");
        sb.AppendLine("  const allActs=Array.from(document.querySelectorAll('#edit_acts_'+id+' .act-text')).map(e=>e.textContent);");
        sb.AppendLine("  actsEl.innerHTML='';");
        sb.AppendLine("  allActs.forEach((act,i)=>{const m=pm.checks&&pm.checks.includes(i);actsEl.innerHTML+='<div class=\"act-item\" style=\"opacity:'+(m?1:.35)+'\"><span class=\"act-check\" style=\"'+(m?'background:var(--accent);border-color:var(--accent);color:white':'')+'\">'+( m?'\\u2713':'')+' </span><span class=\"act-text\">'+act+'</span></div>';});");
        sb.AppendLine("  document.getElementById('ver_usuario_'+id).textContent=pm.usuario||'—';");
        sb.AppendLine("  document.getElementById('ver_fecha_'+id).textContent=pm.fecha||'—';");
        sb.AppendLine("  document.getElementById('ver_proximo_'+id).textContent=pm.proximo_pm||'—';");
        sb.AppendLine("  const o=document.getElementById('ver_obs_'+id);if(o)o.textContent=pm.observaciones?'📝 '+pm.observaciones:'';");
        sb.AppendLine("  document.getElementById('ver_'+id).style.display='block';");
        sb.AppendLine("}");
        sb.AppendLine("function cerrarVer(id){document.getElementById('ver_'+id).style.display='none';}");
        sb.AppendLine("async function abrirEditarPM(id){");
        sb.AppendLine("  if(!usuarioActual){abrirLogin();return;}");
        sb.AppendLine("  const res=await fetch('/PREVENTIVO/DIGITAL/'+id);const data=await res.json();");
        sb.AppendLine("  const cbs=document.querySelectorAll('#edit_acts_'+id+' input[type=checkbox]');");
        sb.AppendLine("  cbs.forEach(cb=>cb.checked=false);");
        sb.AppendLine("  if(data.existe&&data.data.checks)data.data.checks.forEach(i=>{if(cbs[i])cbs[i].checked=true;});");
        sb.AppendLine("  const f=document.getElementById('edit_fecha_'+id);if(f)f.value=data.existe?(data.data.fecha||''):'';");
        sb.AppendLine("  const o=document.getElementById('edit_obs_pm_'+id);if(o)o.value=data.existe?(data.data.observaciones||''):'';");
        sb.AppendLine("  document.getElementById('edit_pm_'+id).style.display='block';");
        sb.AppendLine("}");
        sb.AppendLine("function cerrarEditarPM(id){document.getElementById('edit_pm_'+id).style.display='none';}");
        sb.AppendLine("async function guardarEditarPM(id){");
        sb.AppendLine("  const fecha=document.getElementById('edit_fecha_'+id).value;");
        sb.AppendLine("  if(!fecha){toast('Selecciona la fecha',false);return;}");
        sb.AppendLine("  const cbs=document.querySelectorAll('#edit_acts_'+id+' input[type=checkbox]');");
        sb.AppendLine("  const checks=[];cbs.forEach((cb,i)=>{if(cb.checked)checks.push(i);});");
        sb.AppendLine("  if(!checks.length){toast('Marca al menos una actividad',false);return;}");
        sb.AppendLine("  const obs=document.getElementById('edit_obs_pm_'+id).value;");
        sb.AppendLine("  const btn=document.querySelector('#edit_pm_'+id+' .btn-amber');");
        sb.AppendLine("  btn.disabled=true;btn.textContent='Guardando...';");
        sb.AppendLine("  const res=await fetch('/PREVENTIVO/GUARDAR_PM/'+id,{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify({usuario:usuarioActual,fecha,checks,observaciones:obs})});");
        sb.AppendLine("  const data=await res.json();");
        sb.AppendLine("  if(data.ok){");
        sb.AppendLine("    toast('Actualizado. Proximo PM: '+data.proximo_pm,true);");
        sb.AppendLine("    const card=document.getElementById('edit_pm_'+id).closest('.card');");
        sb.AppendLine("    card.querySelector('.status-dot').className='status-dot dot-ok';");
        sb.AppendLine("    card.querySelectorAll('.status-row span')[1].textContent='Último PM: '+fecha;");
        sb.AppendLine("    card.querySelectorAll('.status-row span')[2].textContent='Plazo: '+data.proximo_pm;");
        sb.AppendLine("    cerrarEditarPM(id);");
        sb.AppendLine("  }else{btn.disabled=false;btn.textContent='Guardar Cambios';toast('Error: '+(data.error||'desconocido'),false);}");
        sb.AppendLine("}");
        sb.AppendLine("async function eliminarPreventivo(id){");
        sb.AppendLine("  if(!confirm('Eliminar el preventivo guardado?'))return;");
        sb.AppendLine("  const res=await fetch('/PREVENTIVO/ELIMINAR_DIGITAL/'+id,{method:'DELETE'});");
        sb.AppendLine("  const data=await res.json();");
        sb.AppendLine("  if(data.ok){");
        sb.AppendLine("    toast('Preventivo eliminado',true);");
        sb.AppendLine("    document.getElementById('ver_'+id).style.display='none';");
        sb.AppendLine("    document.getElementById('edit_pm_'+id).style.display='none';");
        sb.AppendLine("    // Intercambiar botones: ocultar ver/editar/eliminar, mostrar hacer preventivo");
        sb.AppendLine("    const card=document.getElementById('btn_del_'+id).closest('.card');");
        sb.AppendLine("    card.dataset.tienePm='false'; card.querySelectorAll('.btn-ver,.btn-edit,.btn-del').forEach(b=>b.style.display='none');");
        sb.AppendLine("    card.querySelectorAll('.btn-pm').forEach(b=>b.style.display='inline-flex');");
        sb.AppendLine("    card.querySelector('.status-dot').className='status-dot dot-warn';");
        sb.AppendLine("    card.querySelectorAll('.status-row span')[1].textContent='Sin mantenimiento registrado';");
        sb.AppendLine("    card.querySelectorAll('.status-row span')[2].textContent='Plazo: No definido';");
        sb.AppendLine("  }else toast('Error al eliminar',false);");
        sb.AppendLine("}");
        sb.AppendLine("function abrirEditar(id){if(!usuarioActual){abrirLogin();return;}usuarioTarjeta[id]=usuarioActual;['equipo_','disp_','planta_','color_','anio_vis_'].forEach(p=>{const el=document.getElementById(p+id);if(el)el.disabled=false;});document.getElementById('obs_'+id).disabled=false;const card=document.getElementById('btn_hacer_'+id)?.closest('.card')||document.getElementById('equipo_'+id)?.closest('.card');if(card)card.classList.add('editing');}");
        sb.AppendLine("function cancelarEditar(id){['equipo_','disp_','planta_','color_','anio_vis_'].forEach(p=>{const el=document.getElementById(p+id);if(el)el.disabled=true;});document.getElementById('obs_'+id).disabled=true;const card=document.getElementById('equipo_'+id)?.closest('.card');if(card)card.classList.remove('editing');}");
        sb.AppendLine("async function guardarCambios(id){");
        sb.AppendLine("  const anioVal=document.getElementById('anio_vis_'+id)?.value;  const datos={ID_EQUIPO:document.getElementById('equipo_'+id).value,UBICACION:document.getElementById('ubicacion_'+id).value,nombre_dispositivo:document.getElementById('disp_'+id).value,PLANTA:document.getElementById('planta_'+id).value,CATEGORIA_COLOR:document.getElementById('color_'+id).value,OBSERVACIONES:document.getElementById('obs_'+id).value,ANIO_CREACION:anioVal?parseInt(anioVal):null};");
        sb.AppendLine("  const usuario=usuarioTarjeta[id]||usuarioActual||'SISTEMA';");
        sb.AppendLine("  const res=await fetch('/PREVENTIVO/'+id+'?usuario='+encodeURIComponent(usuario),{method:'PUT',headers:{'Content-Type':'application/json','X-Usuario':usuario},body:JSON.stringify(datos)});");
        sb.AppendLine("  const data=await res.json();");
        sb.AppendLine("  if(data.mensaje){toast('Cambios guardados',true);cancelarEditar(id);}else toast('Error al guardar',false);");
        sb.AppendLine("}");
        sb.AppendLine("function toast(msg,ok){const t=document.createElement('div');t.className='toast '+(ok?'toast-ok':'toast-err');t.textContent=msg;document.body.appendChild(t);setTimeout(()=>t.remove(),3000);}");
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
        sb.AppendLine("</script></body></html>");
        return sb.ToString();
    }

    private static string Esc(string? s) =>
        (s ?? "").Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;");

    private static (string color, string bg, string label) ColorBadge(string cat)
    {
        var c = cat.ToLower();
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
        var d = disp.ToUpper();
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
        var d = disp.ToUpper();
        if (d.Contains("COMPUTADORA") || d.Contains("CPU"))
            return new List<string> { "Sopletear el gabinete", "Limpieza de contactos de memoria RAM", "Sopletear fuente de poder y ventiladores", "Limpieza del gabinete", "Limpieza del monitor o pantalla", "Limpieza y sopleteado del teclado y mouse", "Sopleteado de ventiladores y ranuras de enfriamiento", "Limpieza exterior del lector óptico", "Limpieza del cableado", "Actualizaciones del sistema operativo", "Actualizaciones de Office", "Eliminación de archivos temporales y vaciar reciclaje", "Revisión del antivirus y escaneo", "Desfragmentar las unidades de disco duro", "Conectar todos los periféricos correspondientes", "Verificar cables y conectores sin daños", "Encender el equipo y verificar funcionamiento", "Verificar que los periféricos funcionen correctamente", "Verificación vida de la pila del BIOS" };
        if (d.Contains("PORTATIL") || d.Contains("LAPTOP"))
            return new List<string> { "Sopletear el gabinete / chasis", "Limpieza de contactos de memoria RAM", "Sopletear fuente de poder y ventiladores", "Limpieza del monitor o pantalla", "Limpieza y sopleteado del teclado y touchpad", "Sopleteado de ventiladores y ranuras de enfriamiento", "Limpieza del cableado", "Actualizaciones del sistema operativo", "Actualizaciones de Office", "Eliminación de archivos temporales y vaciar reciclaje", "Revisión del antivirus y escaneo", "Desfragmentar las unidades de disco duro", "Conectar todos los periféricos correspondientes", "Verificar cables y conectores sin daños", "Encender el equipo y verificar funcionamiento", "Verificar que los periféricos funcionen correctamente" };
        if (d.Contains("IMPRESORA"))
            return new List<string> { "Sopletear la impresora térmica", "Limpieza de rodillos (no usar alcohol)", "Limpieza del cabezal de la impresora térmica", "Limpieza exterior de la impresora", "Limpieza del cableado", "Rutear cables / anclar eliminador de impresora", "Conectar todos los periféricos correspondientes", "Verificar cables y conectores sin daños", "Verificar que los periféricos funcionen correctamente" };
        if (d.Contains("UPS"))
            return new List<string> { "Limpieza y verificación del UPS", "Limpieza del cableado", "Conectar todos los periféricos correspondientes", "Verificar cables y conectores sin daños", "Verificación vida de la pila del UPS", "Inspección y funcionamiento del UPS", "Verificar que solo equipo IT esté conectado al UPS" };
        return new List<string> { "Inspección general", "Limpieza exterior", "Verificación de funcionamiento" };
    }
}