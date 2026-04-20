using ChiIT.Data;
using DocumentFormat.OpenXml.Spreadsheet;
using Microsoft.AspNetCore.Mvc;
using SixLabors.ImageSharp.Drawing.Processing;
using System.Text;

namespace ChiIT.Controllers;

[ApiController]
public class AdminUsuariosPageController : ControllerBase
{
    private readonly DbConnectionPool _db;
    public AdminUsuariosPageController(DbConnectionPool db) => _db = db;

    [HttpGet("admin/usuarios")]
    public IActionResult Panel([FromQuery] string? usuario)
    {
        // Verificar que sea ADMIN o AUDITOR
        var usr = Request.Cookies["usuario"] ?? usuario ?? "";
        if (string.IsNullOrWhiteSpace(usr))
            return Content(HtmlAccesoDenegado(), "text/html; charset=utf-8");

        using var conn = _db.Open();
        using var chk = conn.CreateCommand();
        chk.CommandText = "SELECT rol FROM public.usuarios WHERE usuario=@u AND activo=true";
        chk.Parameters.AddWithValue("u", usr.ToUpper());
        var rol = chk.ExecuteScalar()?.ToString();
        if (rol != "ADMIN" && rol != "AUDITOR")
            return Content(HtmlAccesoDenegado(), "text/html; charset=utf-8");

        return Content(HtmlPage(rol), "text/html; charset=utf-8");
    }

    // ────────────────────────────────────────────────────────────────────────
    private static string HtmlPage(string rol = "ADMIN")
    {
        bool soloLectura = rol == "AUDITOR";
        var sb = new StringBuilder();
        sb.AppendLine("<!DOCTYPE html>");
        sb.AppendLine("<html lang=\"es\">");
        sb.AppendLine("<head>");
        sb.AppendLine("<meta charset=\"UTF-8\">");
        sb.AppendLine("<meta name=\"viewport\" content=\"width=device-width,initial-scale=1\">");
        sb.AppendLine("<title>Admin — Gestión de Usuarios</title>");
        sb.AppendLine("<link href=\"https://fonts.googleapis.com/css2?family=DM+Sans:wght@300;400;500;600;700&family=DM+Mono:wght@400;500&display=swap\" rel=\"stylesheet\">");
        sb.AppendLine("<style>");

        // ── Variables & reset ──────────────────────────────────────────────
        sb.AppendLine(":root{");
        sb.AppendLine("  --bg:#080C14;--surface:#0E1420;--surface2:#151D2E;--surface3:#1C2640;");
        sb.AppendLine("  --border:rgba(255,255,255,0.06);--border2:rgba(255,255,255,0.11);");
        sb.AppendLine("  --accent:#3B82F6;--accent2:#1D4ED8;");
        sb.AppendLine("  --text:#F1F5F9;--muted:#64748B;--muted2:#94A3B8;");
        sb.AppendLine("  --green:#10B981;--red:#EF4444;--amber:#F59E0B;--cyan:#06B6D4;--purple:#8B5CF6;");
        sb.AppendLine("  --radius:14px;--radius-sm:8px;");
        sb.AppendLine("}");
        sb.AppendLine("*{box-sizing:border-box;margin:0;padding:0;}");
        sb.AppendLine("body{font-family:'DM Sans',sans-serif;background:var(--bg);color:var(--text);min-height:100vh;}");

        sb.AppendLine(".badge-auditor{background:rgba(6,182,212,.18);border:1px solid rgba(6,182,212,.4);color:#67e8f9;}");

        // ── Top bar ────────────────────────────────────────────────────────
        sb.AppendLine(".top-bar{background:linear-gradient(135deg,#0a1628,#080C14);border-bottom:1px solid var(--border2);padding:14px 24px;display:flex;align-items:center;gap:14px;position:sticky;top:0;z-index:100;backdrop-filter:blur(8px);}");
        sb.AppendLine(".top-icon{width:44px;height:44px;border-radius:11px;background:linear-gradient(135deg,#1D4ED8,#3B82F6);display:flex;align-items:center;justify-content:center;font-size:21px;flex-shrink:0;box-shadow:0 4px 14px rgba(59,130,246,.35);}");
        sb.AppendLine(".top-title{flex:1;}.top-title h1{font-size:16px;font-weight:700;letter-spacing:-.01em;}.top-title p{font-size:11px;color:var(--muted2);margin-top:2px;}");
        sb.AppendLine(".top-actions{display:flex;gap:8px;align-items:center;position:relative;}.user-chip{display:flex;align-items:center;gap:8px;padding:7px 14px;border-radius:999px;background:rgba(255,255,255,.07);border:1px solid rgba(255,255,255,.13);font-size:12px;font-weight:600;color:#94A3B8;cursor:pointer;position:relative;user-select:none;transition:background .2s,border-color .2s;}.user-chip:hover{background:rgba(255,255,255,.12);border-color:#3B82F6;}.chip-arrow{font-size:9px;color:#64748B;transition:transform .2s;}.user-chip.open .chip-arrow{transform:rotate(180deg);}.user-dropdown{display:none;position:absolute;top:calc(100% + 10px);right:0;min-width:210px;background:#111827;border:1px solid rgba(255,255,255,.13);border-radius:12px;box-shadow:0 16px 48px rgba(0,0,0,.6);padding:8px;z-index:999;animation:dropIn .15s ease;}@keyframes dropIn{from{opacity:0;transform:translateY(-6px)}to{opacity:1;transform:translateY(0)}}.user-chip.open .user-dropdown{display:block;}.dropdown-header{padding:10px 12px 8px;border-bottom:1px solid rgba(255,255,255,.07);margin-bottom:6px;}.d-name{font-size:13px;font-weight:700;color:#F1F5F9;}.d-role{font-size:10px;color:#64748B;font-family:monospace;text-transform:uppercase;letter-spacing:.08em;margin-top:2px;}.dropdown-item{display:flex;align-items:center;gap:10px;padding:9px 12px;border-radius:8px;font-size:12px;font-weight:500;cursor:pointer;color:#94A3B8;transition:background .15s,color .15s;border:none;background:none;width:100%;text-align:left;font-family:inherit;}.dropdown-item:hover{background:rgba(255,255,255,.06);color:#F1F5F9;}.dropdown-item.danger{color:#FCA5A5;}.dropdown-item.danger:hover{background:rgba(239,68,68,.1);color:#fff;}.dropdown-sep{height:1px;background:rgba(255,255,255,.07);margin:6px 0;}");

        // ── Botones ────────────────────────────────────────────────────────
        sb.AppendLine("@keyframes pop{0%{transform:scale(1)}30%{transform:scale(.88)}65%{transform:scale(1.08)}100%{transform:scale(1)}}");
        sb.AppendLine("@keyframes ripple{0%{transform:translate(-50%,-50%) scale(0);opacity:.5}100%{transform:translate(-50%,-50%) scale(4);opacity:0}}");
        sb.AppendLine(".btn{display:inline-flex;align-items:center;gap:6px;padding:9px 16px;border:none;border-radius:var(--radius-sm);font-family:'DM Sans',sans-serif;font-size:13px;font-weight:600;cursor:pointer;transition:transform .15s,filter .15s,opacity .15s;position:relative;overflow:hidden;}");
        sb.AppendLine(".btn.animating{animation:pop .35s cubic-bezier(.36,.07,.19,.97) forwards;}");
        sb.AppendLine(".btn-ripple{position:absolute;width:40px;height:40px;border-radius:50%;background:rgba(255,255,255,.35);pointer-events:none;animation:ripple .5s ease forwards;}");
        sb.AppendLine(".btn:hover{transform:translateY(-2px);filter:brightness(1.12);}");
        sb.AppendLine(".btn:disabled{opacity:.45;pointer-events:none;}");
        sb.AppendLine(".btn-primary{background:var(--accent);color:white;}");
        sb.AppendLine(".btn-success{background:var(--green);color:white;}");
        sb.AppendLine(".btn-danger{background:var(--red);color:white;}");
        sb.AppendLine(".btn-amber{background:var(--amber);color:#1c1400;}");
        sb.AppendLine(".btn-ghost{background:var(--surface2);color:var(--muted2);border:1px solid var(--border2);}");
        sb.AppendLine(".btn-cyan{background:var(--cyan);color:#001a1f;}");
        sb.AppendLine(".btn-sm{font-size:11px;padding:6px 11px;}");

        // ── Contenido principal ────────────────────────────────────────────
        sb.AppendLine(".main{padding:20px 24px;max-width:1200px;margin:0 auto;}");

        // ── Stats cards ────────────────────────────────────────────────────
        sb.AppendLine(".stats-row{display:grid;grid-template-columns:repeat(auto-fill,minmax(160px,1fr));gap:12px;margin-bottom:22px;}");
        sb.AppendLine(".stat-card{background:var(--surface);border:1px solid var(--border);border-radius:var(--radius);padding:16px 18px;display:flex;flex-direction:column;gap:6px;}");
        sb.AppendLine(".stat-card .stat-label{font-size:10px;font-weight:700;text-transform:uppercase;letter-spacing:.1em;color:var(--muted);}");
        sb.AppendLine(".stat-card .stat-val{font-size:26px;font-weight:700;font-family:'DM Mono',monospace;line-height:1;}");
        sb.AppendLine(".stat-card .stat-sub{font-size:11px;color:var(--muted2);}");

        // ── Barra de búsqueda / filtros ────────────────────────────────────
        sb.AppendLine(".toolbar{display:flex;align-items:center;gap:10px;margin-bottom:16px;flex-wrap:wrap;}");
        sb.AppendLine(".search-wrap{flex:1;min-width:200px;position:relative;}");
        sb.AppendLine(".search-wrap input{width:100%;background:var(--surface);border:1px solid var(--border2);border-radius:var(--radius-sm);padding:9px 12px 9px 36px;font-size:13px;color:var(--text);font-family:'DM Sans',sans-serif;outline:none;transition:border-color .2s;}");
        sb.AppendLine(".search-wrap input:focus{border-color:var(--accent);}");
        sb.AppendLine(".search-icon{position:absolute;left:11px;top:50%;transform:translateY(-50%);font-size:14px;pointer-events:none;}");
        sb.AppendLine("select.filter-sel{background:var(--surface);border:1px solid var(--border2);border-radius:var(--radius-sm);padding:9px 12px;font-size:12px;color:var(--text);font-family:'DM Sans',sans-serif;cursor:pointer;outline:none;}");

        // ── Tabla de usuarios ──────────────────────────────────────────────
        sb.AppendLine(".table-wrap{background:var(--surface);border:1px solid var(--border);border-radius:var(--radius);overflow:hidden;}");
        sb.AppendLine(".table-header{display:grid;grid-template-columns:50px 140px 1fr 90px 90px 100px 130px 130px;padding:10px 16px;border-bottom:1px solid var(--border2);background:var(--surface2);}");
        sb.AppendLine(".th{font-size:10px;font-weight:700;text-transform:uppercase;letter-spacing:.08em;color:var(--muted);}");
        sb.AppendLine(".user-row{display:grid;grid-template-columns:50px 140px 1fr 90px 90px 100px 130px 130px;padding:12px 16px;border-bottom:1px solid var(--border);align-items:center;transition:background .15s;}");
        sb.AppendLine(".user-row:last-child{border-bottom:none;}");
        sb.AppendLine(".user-row:hover{background:var(--surface2);}");
        sb.AppendLine(".user-row .cell{font-size:12px;color:var(--text);}");
        sb.AppendLine(".user-row .cell-muted{font-size:11px;color:var(--muted2);font-family:'DM Mono',monospace;}");
        sb.AppendLine(".row-actions{display:flex;gap:5px;flex-wrap:wrap;}");

        // ── Badges ────────────────────────────────────────────────────────
        sb.AppendLine(".badge{display:inline-flex;align-items:center;gap:4px;padding:3px 9px;border-radius:999px;font-size:10px;font-weight:700;text-transform:uppercase;letter-spacing:.05em;}");
        sb.AppendLine(".badge-admin{background:rgba(139,92,246,.18);border:1px solid rgba(139,92,246,.4);color:#c4b5fd;}");
        sb.AppendLine(".badge-user{background:rgba(59,130,246,.15);border:1px solid rgba(59,130,246,.35);color:#93c5fd;}");
        sb.AppendLine(".badge-on{background:rgba(16,185,129,.15);border:1px solid rgba(16,185,129,.35);color:#6ee7b7;}");
        sb.AppendLine(".badge-off{background:rgba(239,68,68,.12);border:1px solid rgba(239,68,68,.3);color:#fca5a5;}");
        sb.AppendLine(".badge-temp{background:rgba(245,158,11,.13);border:1px solid rgba(245,158,11,.35);color:#fcd34d;}");

        // ── Avatar ─────────────────────────────────────────────────────────
        sb.AppendLine(".avatar{width:32px;height:32px;border-radius:50%;display:flex;align-items:center;justify-content:center;font-size:12px;font-weight:700;flex-shrink:0;border:2px solid var(--border2);}");

        // ── Empty state ────────────────────────────────────────────────────
        sb.AppendLine(".empty{text-align:center;padding:50px 20px;color:var(--muted2);}");
        sb.AppendLine(".empty .empty-icon{font-size:40px;margin-bottom:12px;}");
        sb.AppendLine(".empty p{font-size:13px;}");

        // ── Modal ──────────────────────────────────────────────────────────
        sb.AppendLine(".modal{display:none;position:fixed;inset:0;background:rgba(0,0,0,.75);backdrop-filter:blur(5px);justify-content:center;align-items:center;z-index:9999;padding:20px;}");
        sb.AppendLine(".modal.show{display:flex;}");
        sb.AppendLine(".modal-box{background:var(--surface);border:1px solid var(--border2);border-radius:16px;padding:0;width:min(480px,100%);max-height:90vh;overflow-y:auto;box-shadow:0 20px 60px rgba(0,0,0,.6);}");
        sb.AppendLine(".modal-head{padding:22px 24px 0;border-bottom:1px solid var(--border);padding-bottom:16px;display:flex;align-items:center;gap:12px;}");
        sb.AppendLine(".modal-head-icon{width:40px;height:40px;border-radius:10px;display:flex;align-items:center;justify-content:center;font-size:18px;flex-shrink:0;}");
        sb.AppendLine(".modal-head h3{font-size:15px;font-weight:700;}.modal-head p{font-size:11px;color:var(--muted2);margin-top:2px;}");
        sb.AppendLine(".modal-body{padding:20px 24px;}");
        sb.AppendLine(".modal-footer{padding:14px 24px;border-top:1px solid var(--border);display:flex;gap:8px;justify-content:flex-end;}");
        sb.AppendLine(".field{margin-bottom:16px;}");
        sb.AppendLine(".field label{font-size:10px;font-weight:700;text-transform:uppercase;letter-spacing:.1em;color:var(--muted);display:block;margin-bottom:6px;}");
        sb.AppendLine(".field input,.field select{width:100%;background:var(--surface2);border:1px solid var(--border2);border-radius:var(--radius-sm);padding:10px 12px;font-size:13px;color:var(--text);font-family:'DM Sans',sans-serif;outline:none;transition:border-color .2s;}");
        sb.AppendLine(".field input:focus,.field select:focus{border-color:var(--accent);}");
        sb.AppendLine(".field-row{display:grid;grid-template-columns:1fr 1fr;gap:12px;}");
        sb.AppendLine(".field-hint{font-size:10px;color:var(--muted);margin-top:4px;}");
        sb.AppendLine(".field-err{font-size:11px;color:#fca5a5;margin-top:4px;display:none;}");
        sb.AppendLine(".toggle-wrap{display:flex;align-items:center;gap:10px;padding:10px 12px;background:var(--surface2);border:1px solid var(--border2);border-radius:var(--radius-sm);}");
        sb.AppendLine(".toggle-wrap label{font-size:12px;font-weight:600;flex:1;cursor:pointer;}");
        sb.AppendLine(".toggle{width:38px;height:20px;border-radius:999px;background:var(--surface3);border:1px solid var(--border2);cursor:pointer;position:relative;transition:background .2s,border-color .2s;flex-shrink:0;}");
        sb.AppendLine(".toggle.on{background:rgba(16,185,129,.4);border-color:var(--green);}");
        sb.AppendLine(".toggle::after{content:'';position:absolute;top:2px;left:2px;width:14px;height:14px;border-radius:50%;background:var(--muted);transition:transform .2s,background .2s;}");
        sb.AppendLine(".toggle.on::after{transform:translateX(18px);background:var(--green);}");

        // ── Confirm modal ──────────────────────────────────────────────────
        sb.AppendLine(".confirm-box{background:var(--surface);border:1px solid var(--border2);border-radius:16px;padding:28px;width:min(360px,95vw);text-align:center;}");
        sb.AppendLine(".confirm-box .confirm-icon{font-size:36px;margin-bottom:12px;}");
        sb.AppendLine(".confirm-box h3{font-size:16px;font-weight:700;margin-bottom:8px;}");
        sb.AppendLine(".confirm-box p{font-size:12px;color:var(--muted2);margin-bottom:22px;line-height:1.6;}");
        sb.AppendLine(".confirm-box .confirm-actions{display:flex;gap:8px;justify-content:center;}");

        // ── Toast ──────────────────────────────────────────────────────────
        sb.AppendLine(".toast{position:fixed;bottom:24px;right:24px;padding:12px 20px;border-radius:10px;font-size:13px;font-weight:600;z-index:99999;pointer-events:none;animation:toastIn .3s ease;}");
        sb.AppendLine("@keyframes toastIn{from{opacity:0;transform:translateY(10px)}to{opacity:1;transform:translateY(0)}}");
        sb.AppendLine(".toast-ok{background:#052e16;border:1px solid #10B981;color:#6ee7b7;}");
        sb.AppendLine(".toast-err{background:#1f0000;border:1px solid #EF4444;color:#fca5a5;}");
        sb.AppendLine(".toast-info{background:#001233;border:1px solid #3B82F6;color:#93c5fd;}");

        // ── Responsive ─────────────────────────────────────────────────────
        sb.AppendLine("@media(max-width:900px){");
        sb.AppendLine(".table-header,.user-row{grid-template-columns:40px 120px 1fr 80px 80px;}");
        sb.AppendLine(".col-acceso,.col-creado{display:none;}");
        sb.AppendLine("}");
        sb.AppendLine("@media(max-width:600px){");
        sb.AppendLine(".table-header,.user-row{grid-template-columns:40px 1fr 80px 90px;}");
        sb.AppendLine(".col-login,.col-rol{display:none;}");
        sb.AppendLine("}");

        sb.AppendLine("</style></head><body>");

        // ── Top bar ────────────────────────────────────────────────────────
        sb.AppendLine("<div class=\"top-bar\">");
        sb.AppendLine("  <div class=\"top-icon\">👥</div>");
        sb.AppendLine("  <div class=\"top-title\"><h1>Gestión de Usuarios</h1><p>Panel de administración — ChiIT</p></div>");
        sb.AppendLine("  <div class=\"top-actions\">");
        if (!soloLectura)
            sb.AppendLine("    <button class=\"btn btn-primary\" onclick=\"abrirCrear()\">➕ Nuevo Usuario</button>");
        else
            sb.AppendLine("    <span class=\"badge badge-auditor\" style=\"font-size:11px;padding:5px 12px;\">👁 Modo Auditor — Solo lectura</span>");
        // User chip
        sb.AppendLine("    <div class=\"user-chip\" id=\"userChipAdmin\" onclick=\"toggleChipAdmin()\">");
        sb.AppendLine("      👤 <span id=\"adminNombreChip\">Cargando...</span>");
        sb.AppendLine("      <span class=\"chip-arrow\">▼</span>");
        sb.AppendLine("      <div class=\"user-dropdown\">");
        sb.AppendLine("        <div class=\"dropdown-header\">");
        sb.AppendLine("          <div class=\"d-name\" id=\"adminDropNombre\">—</div>");
        sb.AppendLine("          <div class=\"d-role\" id=\"adminDropRol\">—</div>");
        sb.AppendLine("        </div>");
        sb.AppendLine("        <button class=\"dropdown-item\" onclick=\"window.location.href='/menu'\">🏠 &nbsp;Menú Principal</button>");
        sb.AppendLine("        <div class=\"dropdown-sep\"></div>");
        sb.AppendLine("        <button class=\"dropdown-item danger\" onclick=\"cerrarSesionAdmin()\">⏻ &nbsp;Cerrar sesión</button>");
        sb.AppendLine("      </div>");
        sb.AppendLine("    </div>");
        sb.AppendLine("  </div>");
        sb.AppendLine("</div>");

        // ── Main ───────────────────────────────────────────────────────────
        sb.AppendLine("<div class=\"main\">");

        sb.AppendLine("  <div class=\"stats-row\">");
        sb.AppendLine("    <div class=\"stat-card\"><span class=\"stat-label\">Total</span><span class=\"stat-val\" id=\"statTotal\">—</span><span class=\"stat-sub\">usuarios registrados</span></div>");
        sb.AppendLine("    <div class=\"stat-card\"><span class=\"stat-label\">Activos</span><span class=\"stat-val\" style=\"color:var(--green)\" id=\"statActivos\">—</span><span class=\"stat-sub\">en servicio</span></div>");
        sb.AppendLine("    <div class=\"stat-card\"><span class=\"stat-label\">Inactivos</span><span class=\"stat-val\" style=\"color:var(--red)\" id=\"statInactivos\">—</span><span class=\"stat-sub\">deshabilitados</span></div>");
        sb.AppendLine("    <div class=\"stat-card\"><span class=\"stat-label\">Admins</span><span class=\"stat-val\" style=\"color:var(--purple)\" id=\"statAdmins\">—</span><span class=\"stat-sub\">con rol ADMIN</span></div>");
        sb.AppendLine("    <div class=\"stat-card\"><span class=\"stat-label\">Auditores</span><span class=\"stat-val\" style=\"color:var(--cyan)\" id=\"statAuditores\">—</span><span class=\"stat-sub\">con rol AUDITOR</span></div>");
        sb.AppendLine("    <div class=\"stat-card\"><span class=\"stat-label\">Temporales</span><span class=\"stat-val\" style=\"color:var(--amber)\" id=\"statTemp\">—</span><span class=\"stat-sub\">contraseña temporal</span></div>");
        sb.AppendLine("  </div>");

        // Toolbar
        sb.AppendLine("  <div class=\"toolbar\">");
        sb.AppendLine("    <div class=\"search-wrap\"><span class=\"search-icon\">🔍</span><input id=\"searchInput\" type=\"text\" placeholder=\"Buscar por usuario o nombre...\" oninput=\"filtrar()\"></div>");
        sb.AppendLine("    <select class=\"filter-sel\" id=\"filtroRol\" onchange=\"filtrar()\">");
        sb.AppendLine("      <option value=\"\">Todos los roles</option>");
        sb.AppendLine("      <option value=\"ADMIN\">ADMIN</option>");
        sb.AppendLine("      <option value=\"AUDITOR\">AUDITOR</option>");
        sb.AppendLine("      <option value=\"USER\">USER</option>");
        sb.AppendLine("      <option value=\"ENCARGADO_CORRECTIVOS\">ENCARGADO_CORRECTIVOS</option>");
        sb.AppendLine("      <option value=\"ENCARGADO_BAJAS\">ENCARGADO_BAJAS</option>");
        sb.AppendLine("    </select>");
        sb.AppendLine("    <select class=\"filter-sel\" id=\"filtroActivo\" onchange=\"filtrar()\">");
        sb.AppendLine("      <option value=\"\">Todos los estados</option>");
        sb.AppendLine("      <option value=\"true\">Activos</option>");
        sb.AppendLine("      <option value=\"false\">Inactivos</option>");
        sb.AppendLine("    </select>");
        sb.AppendLine("    <button class=\"btn btn-ghost btn-sm\" onclick=\"cargarUsuarios()\">🔄 Actualizar</button>");
        sb.AppendLine("  </div>");

        // Tabla
        sb.AppendLine("  <div class=\"table-wrap\">");
        sb.AppendLine("    <div class=\"table-header\">");
        sb.AppendLine("      <div class=\"th\">#</div>");
        sb.AppendLine("      <div class=\"th col-login\">Usuario</div>");
        sb.AppendLine("      <div class=\"th\">Nombre</div>");
        sb.AppendLine("      <div class=\"th col-rol\">Rol</div>");
        sb.AppendLine("      <div class=\"th\">Estado</div>");
        sb.AppendLine("      <div class=\"th\">Contraseña</div>");
        sb.AppendLine("      <div class=\"th col-acceso\">Último Acceso</div>");
        sb.AppendLine("      <div class=\"th col-creado\">Acciones</div>");
        sb.AppendLine("    </div>");
        sb.AppendLine("    <div id=\"tableBody\"><div class=\"empty\"><div class=\"empty-icon\">⏳</div><p>Cargando usuarios...</p></div></div>");
        sb.AppendLine("  </div>");
        sb.AppendLine("</div>"); // /main

        // ── Modal Crear / Editar ────────────────────────────────────────────
        sb.AppendLine("<div class=\"modal\" id=\"modalUsuario\">");
        sb.AppendLine("  <div class=\"modal-box\">");
        sb.AppendLine("    <div class=\"modal-head\">");
        sb.AppendLine("      <div class=\"modal-head-icon\" id=\"mIcon\" style=\"background:rgba(59,130,246,.15)\">👤</div>");
        sb.AppendLine("      <div><h3 id=\"mTitulo\">Nuevo Usuario</h3><p id=\"mSub\">Completa los datos para registrar el usuario</p></div>");
        sb.AppendLine("    </div>");
        sb.AppendLine("    <div class=\"modal-body\">");
        sb.AppendLine("      <input type=\"hidden\" id=\"mId\">");
        sb.AppendLine("      <div class=\"field-row\">");
        sb.AppendLine("        <div class=\"field\"><label>Usuario (login)</label><input id=\"mUsuario\" type=\"text\" placeholder=\"Ej: DOMINGUEZG\" style=\"text-transform:uppercase\" oninput=\"this.value=this.value.toUpperCase()\"><div class=\"field-err\" id=\"errUsuario\">El usuario ya existe</div></div>");
        sb.AppendLine("        <div class=\"field\"><label>Nombre Completo</label><input id=\"mNombre\" type=\"text\" placeholder=\"Nombre del colaborador\"></div>");
        sb.AppendLine("      </div>");
        sb.AppendLine("      <div class=\"field-row\">");
        sb.AppendLine("        <div class=\"field\"><label>Contraseña</label><input id=\"mPassword\" type=\"password\" placeholder=\"Mínimo 6 caracteres\"><div class=\"field-hint\" id=\"pwdHint\">Dejar vacío para no cambiar</div></div>");
        sb.AppendLine("        <div class=\"field\"><label>Rol</label><select id=\"mRol\"><option value=\"USER\">USER</option><option value=\"AUDITOR\">AUDITOR</option><option value=\"ADMIN\">ADMIN</option></select></div>");
        sb.AppendLine("      </div>");
        sb.AppendLine("      <div class=\"field-row\">");
        sb.AppendLine("        <div class=\"field\"><label>Estado de cuenta</label>");
        sb.AppendLine("          <div class=\"toggle-wrap\" onclick=\"toggleActivo()\">");
        sb.AppendLine("            <label id=\"activoLabel\">Cuenta Activa</label>");
        sb.AppendLine("            <div class=\"toggle on\" id=\"toggleActivo\"></div>");
        sb.AppendLine("          </div>");
        sb.AppendLine("        </div>");
        sb.AppendLine("        <div class=\"field\"><label>Contraseña temporal</label>");
        sb.AppendLine("          <div class=\"toggle-wrap\" onclick=\"toggleTemp()\">");
        sb.AppendLine("            <label id=\"tempLabel\">Requiere cambio</label>");
        sb.AppendLine("            <div class=\"toggle on\" id=\"toggleTemp\"></div>");
        sb.AppendLine("          </div>");
        sb.AppendLine("        </div>");
        sb.AppendLine("      </div>");
        sb.AppendLine("      <div id=\"modalErr\" style=\"color:#fca5a5;font-size:12px;display:none;padding:8px 12px;background:rgba(239,68,68,.1);border-radius:6px;border:1px solid rgba(239,68,68,.3);\"></div>");
        sb.AppendLine("    </div>");
        sb.AppendLine("    <div class=\"modal-footer\">");
        sb.AppendLine("      <button class=\"btn btn-ghost\" onclick=\"cerrarModal()\">Cancelar</button>");
        sb.AppendLine("      <button class=\"btn btn-primary\" id=\"mGuardarBtn\" onclick=\"guardarUsuario()\">💾 Guardar</button>");
        sb.AppendLine("    </div>");
        sb.AppendLine("  </div>");
        sb.AppendLine("</div>");

        // ── Modal Confirm ───────────────────────────────────────────────────
        sb.AppendLine("<div class=\"modal\" id=\"modalConfirm\">");
        sb.AppendLine("  <div class=\"confirm-box\">");
        sb.AppendLine("    <div class=\"confirm-icon\" id=\"confirmIcon\">⚠️</div>");
        sb.AppendLine("    <h3 id=\"confirmTitulo\">¿Confirmar acción?</h3>");
        sb.AppendLine("    <p id=\"confirmMsg\">Esta acción no se puede deshacer.</p>");
        sb.AppendLine("    <div class=\"confirm-actions\">");
        sb.AppendLine("      <button class=\"btn btn-ghost\" onclick=\"cerrarConfirm()\">Cancelar</button>");
        sb.AppendLine("      <button class=\"btn btn-danger\" id=\"confirmOkBtn\" onclick=\"ejecutarConfirm()\">Confirmar</button>");
        sb.AppendLine("    </div>");
        sb.AppendLine("  </div>");
        sb.AppendLine("</div>");

        // ── Modal Reset Password ────────────────────────────────────────────
        sb.AppendLine("<div class=\"modal\" id=\"modalReset\">");
        sb.AppendLine("  <div class=\"modal-box\">");
        sb.AppendLine("    <div class=\"modal-head\">");
        sb.AppendLine("      <div class=\"modal-head-icon\" style=\"background:rgba(245,158,11,.15)\">🔑</div>");
        sb.AppendLine("      <div><h3>Restablecer Contraseña</h3><p id=\"resetSub\">Nueva contraseña para el usuario</p></div>");
        sb.AppendLine("    </div>");
        sb.AppendLine("    <div class=\"modal-body\">");
        sb.AppendLine("      <input type=\"hidden\" id=\"resetId\">");
        sb.AppendLine("      <div class=\"field\"><label>Nueva Contraseña</label><input id=\"resetPwd\" type=\"password\" placeholder=\"Mínimo 6 caracteres\"></div>");
        sb.AppendLine("      <div class=\"field\"><label>Confirmar Contraseña</label><input id=\"resetPwd2\" type=\"password\" placeholder=\"Repite la contraseña\"></div>");
        sb.AppendLine("      <div id=\"resetErr\" style=\"color:#fca5a5;font-size:12px;display:none;margin-top:8px;\"></div>");
        sb.AppendLine("    </div>");
        sb.AppendLine("    <div class=\"modal-footer\">");
        sb.AppendLine("      <button class=\"btn btn-ghost\" onclick=\"cerrarReset()\">Cancelar</button>");
        sb.AppendLine("      <button class=\"btn btn-amber\" onclick=\"confirmarReset()\">🔑 Restablecer</button>");
        sb.AppendLine("    </div>");
        sb.AppendLine("  </div>");
        sb.AppendLine("</div>");

        // ── JavaScript ──────────────────────────────────────────────────────
        sb.AppendLine("<script>");
        sb.AppendLine($"const soloLectura={soloLectura.ToString().ToLower()};");
        sb.AppendLine("let todosUsuarios=[];let confirmCallback=null;const usuarioActual=(sessionStorage.getItem('usuario')||localStorage.getItem('usuario')||'').toUpperCase();");

        // cargarUsuarios
        sb.AppendLine("async function cargarUsuarios(){");
        sb.AppendLine("  try{");
        sb.AppendLine("    const res=await fetch('/admin/usuarios/api',{credentials:'include',headers:{'X-Usuario':usuarioActual}});");
        sb.AppendLine("    const data=await res.json();");
        sb.AppendLine("    todosUsuarios=data.usuarios||[];");
        sb.AppendLine("    actualizarStats();");
        sb.AppendLine("    filtrar();");
        sb.AppendLine("  }catch(e){");
        sb.AppendLine("    document.getElementById('tableBody').innerHTML='<div class=\"empty\"><div class=\"empty-icon\">❌</div><p>Error al cargar usuarios</p></div>';");
        sb.AppendLine("  }");
        sb.AppendLine("}");

        // actualizarStats
        sb.AppendLine("function actualizarStats(){");
        sb.AppendLine("  document.getElementById('statTotal').textContent=todosUsuarios.length;");
        sb.AppendLine("  document.getElementById('statActivos').textContent=todosUsuarios.filter(u=>u.activo===true||u.activo==='true').length;");
        sb.AppendLine("  document.getElementById('statInactivos').textContent=todosUsuarios.filter(u=>u.activo!==true&&u.activo!=='true').length;");
        sb.AppendLine("  document.getElementById('statAdmins').textContent=todosUsuarios.filter(u=>u.rol==='ADMIN').length;");
        sb.AppendLine("  document.getElementById('statAuditores').textContent=todosUsuarios.filter(u=>u.rol==='AUDITOR').length;");
        sb.AppendLine("  document.getElementById('statTemp').textContent=todosUsuarios.filter(u=>u.password_temporal===true||u.password_temporal==='true').length;");
        sb.AppendLine("}");

        // filtrar
        sb.AppendLine("function filtrar(){");
        sb.AppendLine("  const q=document.getElementById('searchInput').value.toLowerCase();");
        sb.AppendLine("  const rol=document.getElementById('filtroRol').value;");
        sb.AppendLine("  const activo=document.getElementById('filtroActivo').value;");
        sb.AppendLine("  let lista=todosUsuarios.filter(u=>{");
        sb.AppendLine("    const matchQ=!q||u.usuario.toLowerCase().includes(q)||u.nombre.toLowerCase().includes(q);");
        sb.AppendLine("    const matchRol=!rol||u.rol===rol;");
        sb.AppendLine("    const uActivo=u.activo===true||u.activo==='true';const matchActivo=activo===''||(activo==='true'?uActivo:!uActivo);");
        sb.AppendLine("    return matchQ&&matchRol&&matchActivo;");
        sb.AppendLine("  });");
        sb.AppendLine("  renderTabla(lista);");
        sb.AppendLine("}");

        // renderTabla
        sb.AppendLine("function renderTabla(lista){");
        sb.AppendLine("  const tb=document.getElementById('tableBody');");
        sb.AppendLine("  if(!lista.length){tb.innerHTML='<div class=\"empty\"><div class=\"empty-icon\">🔍</div><p>No se encontraron usuarios</p></div>';return;}");
        sb.AppendLine("  const avatarColor=u=>{const colors=['#3B82F6','#8B5CF6','#10B981','#F59E0B','#EF4444','#06B6D4','#F472B6'];let h=0;for(let c of u)h=(h*31+c.charCodeAt(0))%colors.length;return colors[h];};");
        sb.AppendLine("  tb.innerHTML=lista.map(u=>{");
        sb.AppendLine("    const ac=u.activo===true||u.activo==='true'||u.activo===1;");
        sb.AppendLine("    const col=avatarColor(u.usuario);");
        sb.AppendLine("    const av=`<div class=\"avatar\" style=\"background:${col}22;border-color:${col}44;color:${col}\">${u.nombre.charAt(0).toUpperCase()}</div>`;");
        sb.AppendLine("    const rolBadge=u.rol==='ADMIN'?'<span class=\"badge badge-admin\">⚡ Admin</span>':u.rol==='AUDITOR'?'<span class=\"badge badge-auditor\">🔍 Auditor</span>':'<span class=\"badge badge-user\">👤 User</span>';");
        sb.AppendLine("    const estadoBadge=ac?'<span class=\"badge badge-on\">● Activo</span>':'<span class=\"badge badge-off\">○ Inactivo</span>';");
        sb.AppendLine("    const pt=u.password_temporal===true||u.password_temporal==='true';const tmpBadge=pt?'<span class=\"badge badge-temp\">⚠ Temporal</span>':'<span class=\"badge badge-on\">✓ Fija</span>';");
        sb.AppendLine("    const acceso=u.ultimo_acceso?fmtFecha(u.ultimo_acceso):'<span style=\"color:var(--muted)\">Nunca</span>';");
        sb.AppendLine("    const acciones=soloLectura");
        sb.AppendLine("      ?'<span style=\"font-size:11px;color:var(--muted)\">Solo lectura</span>'");
        sb.AppendLine("      :`<button class=\"btn btn-primary btn-sm\" onclick=\"abrirEditar(${u.id})\">✏️</button>`");
        sb.AppendLine("       +`<button class=\"btn btn-amber btn-sm\" onclick=\"abrirReset(${u.id},'${u.usuario}')\">🔑</button>`");
        sb.AppendLine("       +`<button class=\"btn btn-sm\" style=\"background:${ac?'rgba(239,68,68,.2)':'rgba(16,185,129,.2)'};color:${ac?'#fca5a5':'#6ee7b7'};border:1px solid ${ac?'rgba(239,68,68,.3)':'rgba(16,185,129,.3)'}\" onclick=\"toggleEstado(${u.id},${ac},'${u.usuario}')\">${ac?'🚫':'✅'}</button>`;");
        sb.AppendLine("    return `<div class=\"user-row\">" +
                       "<div class=\"cell\">${av}</div>" +
                       "<div class=\"cell col-login\" style=\"font-family:'DM Mono',monospace;font-size:11px;font-weight:600\">${u.usuario}</div>" +
                       "<div class=\"cell\">${u.nombre}</div>" +
                       "<div class=\"cell col-rol\">${rolBadge}</div>" +
                       "<div class=\"cell\">${estadoBadge}</div>" +
                       "<div class=\"cell\">${tmpBadge}</div>" +
                       "<div class=\"cell cell-muted col-acceso\">${acceso}</div>" +
                       "<div class=\"cell col-creado\"><div class=\"row-actions\">${acciones}</div></div>" +
                       "</div>`;");
        sb.AppendLine("  }).join('');");
        sb.AppendLine("}");

        // fmtFecha
        sb.AppendLine("function fmtFecha(iso){try{const d=new Date(iso);return d.toLocaleDateString('es-MX',{day:'2-digit',month:'short',year:'numeric',hour:'2-digit',minute:'2-digit'});}catch{return iso;}}");

        // Toggle activo/temp en modal
        sb.AppendLine("function toggleActivo(){const t=document.getElementById('toggleActivo');t.classList.toggle('on');document.getElementById('activoLabel').textContent=t.classList.contains('on')?'Cuenta Activa':'Cuenta Inactiva';}");
        sb.AppendLine("function toggleTemp(){const t=document.getElementById('toggleTemp');t.classList.toggle('on');document.getElementById('tempLabel').textContent=t.classList.contains('on')?'Requiere cambio':'Contraseña fija';}");

        // Abrir crear
        sb.AppendLine("function abrirCrear(){");
        sb.AppendLine("  if(soloLectura)return;");
        sb.AppendLine("  document.getElementById('mId').value='';");
        sb.AppendLine("  document.getElementById('mTitulo').textContent='Nuevo Usuario';");
        sb.AppendLine("  document.getElementById('mSub').textContent='Completa los datos para registrar el usuario';");
        sb.AppendLine("  document.getElementById('mIcon').textContent='👤';");
        sb.AppendLine("  document.getElementById('mIcon').style.background='rgba(59,130,246,.15)';");
        sb.AppendLine("  document.getElementById('mUsuario').value='';document.getElementById('mUsuario').disabled=false;");
        sb.AppendLine("  document.getElementById('mNombre').value='';");
        sb.AppendLine("  document.getElementById('mPassword').value='';document.getElementById('mPassword').placeholder='Mínimo 6 caracteres (requerido)';");
        sb.AppendLine("  document.getElementById('pwdHint').textContent='Requerido para nuevo usuario';");
        sb.AppendLine("  document.getElementById('mRol').value='USER';");
        sb.AppendLine("  const ta=document.getElementById('toggleActivo');ta.classList.add('on');document.getElementById('activoLabel').textContent='Cuenta Activa';");
        sb.AppendLine("  const tt=document.getElementById('toggleTemp');tt.classList.add('on');document.getElementById('tempLabel').textContent='Requiere cambio';");
        sb.AppendLine("  document.getElementById('modalErr').style.display='none';");
        sb.AppendLine("  document.getElementById('errUsuario').style.display='none';");
        sb.AppendLine("  document.getElementById('mGuardarBtn').textContent='💾 Crear Usuario';");
        sb.AppendLine("  document.getElementById('modalUsuario').classList.add('show');");
        sb.AppendLine("  setTimeout(()=>document.getElementById('mUsuario').focus(),100);");
        sb.AppendLine("}");

        // Abrir editar
        sb.AppendLine("function abrirEditar(id){");
        sb.AppendLine("  if(soloLectura)return;");
        sb.AppendLine("  const u=todosUsuarios.find(x=>x.id===id);if(!u)return;");
        sb.AppendLine("  document.getElementById('mId').value=u.id;");
        sb.AppendLine("  document.getElementById('mTitulo').textContent='Editar Usuario';");
        sb.AppendLine("  document.getElementById('mSub').textContent=u.usuario;");
        sb.AppendLine("  document.getElementById('mIcon').textContent='✏️';");
        sb.AppendLine("  document.getElementById('mIcon').style.background='rgba(245,158,11,.15)';");
        sb.AppendLine("  document.getElementById('mUsuario').value=u.usuario;document.getElementById('mUsuario').disabled=true;");
        sb.AppendLine("  document.getElementById('mNombre').value=u.nombre;");
        sb.AppendLine("  document.getElementById('mPassword').value='';document.getElementById('mPassword').placeholder='Dejar vacío para no cambiar';");
        sb.AppendLine("  document.getElementById('pwdHint').textContent='Dejar vacío para mantener la contraseña actual';");
        sb.AppendLine("  document.getElementById('mRol').value=u.rol;");
        sb.AppendLine("  const ta=document.getElementById('toggleActivo');");
        sb.AppendLine("  u.activo?ta.classList.add('on'):ta.classList.remove('on');");
        sb.AppendLine("  document.getElementById('activoLabel').textContent=u.activo?'Cuenta Activa':'Cuenta Inactiva';");
        sb.AppendLine("  const tt=document.getElementById('toggleTemp');");
        sb.AppendLine("  u.password_temporal?tt.classList.add('on'):tt.classList.remove('on');");
        sb.AppendLine("  document.getElementById('tempLabel').textContent=u.password_temporal?'Requiere cambio':'Contraseña fija';");
        sb.AppendLine("  document.getElementById('modalErr').style.display='none';");
        sb.AppendLine("  document.getElementById('errUsuario').style.display='none';");
        sb.AppendLine("  document.getElementById('mGuardarBtn').textContent='💾 Guardar Cambios';");
        sb.AppendLine("  document.getElementById('modalUsuario').classList.add('show');");
        sb.AppendLine("  setTimeout(()=>document.getElementById('mNombre').focus(),100);");
        sb.AppendLine("}");

        // cerrarModal
        sb.AppendLine("function cerrarModal(){document.getElementById('modalUsuario').classList.remove('show');}");

        // guardarUsuario
        sb.AppendLine("async function guardarUsuario(){");
        sb.AppendLine("  if(soloLectura)return;");
        sb.AppendLine("  const id=document.getElementById('mId').value;");
        sb.AppendLine("  const usuario=document.getElementById('mUsuario').value.trim().toUpperCase();");
        sb.AppendLine("  const nombre=document.getElementById('mNombre').value.trim();");
        sb.AppendLine("  const password=document.getElementById('mPassword').value;");
        sb.AppendLine("  const rol=document.getElementById('mRol').value;");
        sb.AppendLine("  const activo=document.getElementById('toggleActivo').classList.contains('on');");
        sb.AppendLine("  const passwordTemporal=document.getElementById('toggleTemp').classList.contains('on');");
        sb.AppendLine("  const errEl=document.getElementById('modalErr');");
        sb.AppendLine("  errEl.style.display='none';");
        sb.AppendLine("  if(!usuario||!nombre){errEl.textContent='Usuario y nombre son requeridos';errEl.style.display='block';return;}");
        sb.AppendLine("  if(!id&&!password){errEl.textContent='La contraseña es requerida para nuevos usuarios';errEl.style.display='block';return;}");
        sb.AppendLine("  if(password&&password.length<6){errEl.textContent='La contraseña debe tener al menos 6 caracteres';errEl.style.display='block';return;}");
        sb.AppendLine("  const btn=document.getElementById('mGuardarBtn');");
        sb.AppendLine("  btn.disabled=true;btn.textContent='Guardando...';");
        sb.AppendLine("  const body={usuario,nombre,rol,activo,password_temporal:passwordTemporal};");
        sb.AppendLine("  if(password)body.password=password;");
        sb.AppendLine("  const url=id?`/admin/usuarios/api/${id}`:'/admin/usuarios/api';");
        sb.AppendLine("  const method=id?'PUT':'POST';");
        sb.AppendLine("  try{");
        sb.AppendLine("    const res=await fetch(url,{method,credentials:'include',headers:{'Content-Type':'application/json','X-Usuario':usuarioActual},body:JSON.stringify(body)});");
        sb.AppendLine("    const data=await res.json();");
        sb.AppendLine("    if(data.ok){cerrarModal();toast(id?'Usuario actualizado':'Usuario creado correctamente',true);cargarUsuarios();}");
        sb.AppendLine("    else{errEl.textContent=data.error||'Error desconocido';errEl.style.display='block';}");
        sb.AppendLine("  }catch(e){errEl.textContent='Error de conexión';errEl.style.display='block';}");
        sb.AppendLine("  btn.disabled=false;btn.textContent=id?'💾 Guardar Cambios':'💾 Crear Usuario';");
        sb.AppendLine("}");

        // toggleEstado
        sb.AppendLine("function toggleEstado(id,activo,usuario){");
        sb.AppendLine("  if(soloLectura)return;");
        sb.AppendLine("  const accion=activo?'desactivar':'activar';");
        sb.AppendLine("  abrirConfirm(");
        sb.AppendLine("    activo?'🚫':'✅',");
        sb.AppendLine("    activo?'Desactivar Usuario':'Activar Usuario',");
        sb.AppendLine("    `¿Deseas ${accion} la cuenta de ${usuario}?`,");
        sb.AppendLine("    activo?'btn-danger':'btn-success',");
        sb.AppendLine("    activo?'Desactivar':'Activar',");
        sb.AppendLine("    async()=>{");
        sb.AppendLine("      const res=await fetch(`/admin/usuarios/api/${id}/estado`,{method:'PATCH',credentials:'include',headers:{'Content-Type':'application/json','X-Usuario':usuarioActual},body:JSON.stringify({activo:!activo})});");
        sb.AppendLine("      const data=await res.json();");
        sb.AppendLine("      if(data.ok){toast(`Usuario ${accion==='activar'?'activado':'desactivado'}`,true);document.getElementById('filtroActivo').value='';cargarUsuarios();}");
        sb.AppendLine("      else toast('Error: '+(data.error||'desconocido'),false);");
        sb.AppendLine("    }");
        sb.AppendLine("  );");
        sb.AppendLine("}");

        // Modal Confirm
        sb.AppendLine("function abrirConfirm(icon,titulo,msg,btnClass,btnLabel,cb){");
        sb.AppendLine("  document.getElementById('confirmIcon').textContent=icon;");
        sb.AppendLine("  document.getElementById('confirmTitulo').textContent=titulo;");
        sb.AppendLine("  document.getElementById('confirmMsg').textContent=msg;");
        sb.AppendLine("  const btn=document.getElementById('confirmOkBtn');");
        sb.AppendLine("  btn.className='btn '+btnClass;btn.textContent=btnLabel;");
        sb.AppendLine("  confirmCallback=cb;");
        sb.AppendLine("  document.getElementById('modalConfirm').classList.add('show');");
        sb.AppendLine("}");
        sb.AppendLine("function cerrarConfirm(){document.getElementById('modalConfirm').classList.remove('show');confirmCallback=null;}");
        sb.AppendLine("async function ejecutarConfirm(){const fn=confirmCallback;cerrarConfirm();if(fn)await fn();}");

        // Reset password modal
        sb.AppendLine("function abrirReset(id,usuario){");
        sb.AppendLine("  if(soloLectura)return;");
        sb.AppendLine("  document.getElementById('resetId').value=id;");
        sb.AppendLine("  document.getElementById('resetSub').textContent='Usuario: '+usuario;");
        sb.AppendLine("  document.getElementById('resetPwd').value='';");
        sb.AppendLine("  document.getElementById('resetPwd2').value='';");
        sb.AppendLine("  document.getElementById('resetErr').style.display='none';");
        sb.AppendLine("  document.getElementById('modalReset').classList.add('show');");
        sb.AppendLine("  setTimeout(()=>document.getElementById('resetPwd').focus(),100);");
        sb.AppendLine("}");
        sb.AppendLine("function cerrarReset(){document.getElementById('modalReset').classList.remove('show');}");
        sb.AppendLine("async function confirmarReset(){");
        sb.AppendLine("  if(soloLectura)return;");
        sb.AppendLine("  const id=document.getElementById('resetId').value;");
        sb.AppendLine("  const pwd=document.getElementById('resetPwd').value;");
        sb.AppendLine("  const pwd2=document.getElementById('resetPwd2').value;");
        sb.AppendLine("  const errEl=document.getElementById('resetErr');");
        sb.AppendLine("  if(pwd.length<6){errEl.textContent='Mínimo 6 caracteres';errEl.style.display='block';return;}");
        sb.AppendLine("  if(pwd!==pwd2){errEl.textContent='Las contraseñas no coinciden';errEl.style.display='block';return;}");
        sb.AppendLine("  const res=await fetch(`/admin/usuarios/api/${id}/reset-password`,{method:'PATCH',credentials:'include',headers:{'Content-Type':'application/json','X-Usuario':usuarioActual},body:JSON.stringify({password:pwd})});");
        sb.AppendLine("  const data=await res.json();");
        sb.AppendLine("  if(data.ok){cerrarReset();toast('Contraseña restablecida',true);cargarUsuarios();}");
        sb.AppendLine("  else{errEl.textContent=data.error||'Error';errEl.style.display='block';}");
        sb.AppendLine("}");

        // Toast
        sb.AppendLine("function toast(msg,ok){const t=document.createElement('div');t.className='toast '+(ok===true?'toast-ok':ok===false?'toast-err':'toast-info');t.textContent=msg;document.body.appendChild(t);setTimeout(()=>t.remove(),3500);}");

        // Ripple en botones
        sb.AppendLine("document.addEventListener('click',function(e){const btn=e.target.closest('.btn');if(!btn)return;btn.classList.remove('animating');void btn.offsetWidth;btn.classList.add('animating');btn.addEventListener('animationend',()=>btn.classList.remove('animating'),{once:true});const r=document.createElement('span');r.className='btn-ripple';const rect=btn.getBoundingClientRect();r.style.left=(e.clientX-rect.left)+'px';r.style.top=(e.clientY-rect.top)+'px';btn.appendChild(r);setTimeout(()=>r.remove(),500);});");

        // Cerrar modales con Escape
        sb.AppendLine("document.addEventListener('keydown',e=>{if(e.key==='Escape'){cerrarModal();cerrarConfirm();cerrarReset();}});");

        // Init
        sb.AppendLine("function toggleChipAdmin(){document.getElementById('userChipAdmin').classList.toggle('open');}document.addEventListener('click',e=>{const c=document.getElementById('userChipAdmin');if(c&&!c.contains(e.target))c.classList.remove('open');});async function cerrarSesionAdmin(){await fetch('/LOGOUT',{method:'POST',credentials:'include'});window.location.href='/static/login.html';}fetch('/obtener-usuario',{credentials:'include'}).then(r=>r.ok?r.json():null).then(d=>{if(!d)return;document.getElementById('adminNombreChip').textContent=d.nombre||d.usuario;document.getElementById('adminDropNombre').textContent=d.nombre||d.usuario;document.getElementById('adminDropRol').textContent=d.rol||'ADMIN';}).catch(()=>{});cargarUsuarios();");
        sb.AppendLine("</script></body></html>");

        return sb.ToString();
    }

    private static string HtmlAccesoDenegado() => """
        <!DOCTYPE html><html lang="es"><head><meta charset="UTF-8">
        <title>Acceso Denegado</title>
        <style>body{font-family:sans-serif;background:#080C14;color:#F1F5F9;display:flex;align-items:center;justify-content:center;min-height:100vh;flex-direction:column;gap:12px;}
        h1{font-size:22px;}p{font-size:13px;color:#64748B;}</style></head>
        <body><div style="font-size:48px">🚫</div><h1>Acceso Denegado</h1>
        <p>Se requiere rol ADMIN o AUDITOR para acceder a este panel.</p></body></html>
        """;
}