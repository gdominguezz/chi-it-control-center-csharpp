using ChiIT.Data;
using Microsoft.AspNetCore.Mvc;
using System.Text;

namespace ChiIT.Controllers;

[ApiController]
public class CalendarioPageController : ControllerBase
{
    private readonly DbConnectionPool _db;
    public CalendarioPageController(DbConnectionPool db) => _db = db;

    [HttpGet("CALENDARIO")]
    public IActionResult Calendario()
    {
        return Content(HtmlPage(), "text/html; charset=utf-8");
    }

    private static string HtmlPage()
    {
        var sb = new StringBuilder();
        sb.AppendLine("<!DOCTYPE html>");
        sb.AppendLine("<html lang=\"es\">");
        sb.AppendLine("<head>");
        sb.AppendLine("<meta charset=\"UTF-8\">");
        sb.AppendLine("<meta name=\"viewport\" content=\"width=device-width,initial-scale=1\">");
        sb.AppendLine("<title>Calendario de Mantenimiento</title>");
        sb.AppendLine("<link href=\"https://fonts.googleapis.com/css2?family=DM+Sans:wght@300;400;500;600;700&family=DM+Mono:wght@400;500&display=swap\" rel=\"stylesheet\">");
        sb.AppendLine("<style>");

        // ── Variables ──
        sb.AppendLine(":root{--bg:#0B0F1A;--surface:#111827;--surface2:#1a2235;--surface3:#1e2a40;--border:rgba(255,255,255,0.07);--border2:rgba(255,255,255,0.13);--accent:#3B82F6;--text:#F1F5F9;--muted:#64748B;--muted2:#94A3B8;--green:#10B981;--red:#EF4444;--amber:#F59E0B;--radius:12px;}");
        sb.AppendLine("*{box-sizing:border-box;margin:0;padding:0;}");
        sb.AppendLine("body{font-family:'DM Sans',sans-serif;background:var(--bg);color:var(--text);min-height:100vh;}");

        // ── Top bar ──
        sb.AppendLine(".top-bar{background:linear-gradient(135deg,#0f1e35,#0B0F1A);border-bottom:1px solid var(--border2);padding:14px 20px;display:flex;align-items:center;gap:12px;position:sticky;top:0;z-index:100;}");
        sb.AppendLine(".top-icon{width:42px;height:42px;border-radius:10px;background:linear-gradient(135deg,#1D4ED8,#3B82F6);display:flex;align-items:center;justify-content:center;font-size:20px;flex-shrink:0;}");
        sb.AppendLine(".top-title{flex:1;}.top-title h1{font-size:15px;font-weight:700;}.top-title p{font-size:11px;color:var(--muted2);margin-top:2px;}");

        // ── Nav tabs de planta ──
        sb.AppendLine(".plant-nav{display:flex;gap:6px;padding:14px 20px 0;flex-wrap:wrap;border-bottom:1px solid var(--border);background:var(--surface);}");
        sb.AppendLine(".plant-tab{padding:8px 18px;border-radius:8px 8px 0 0;font-size:12px;font-weight:600;cursor:pointer;border:1px solid transparent;border-bottom:none;color:var(--muted2);background:transparent;transition:all .15s;white-space:nowrap;}");
        sb.AppendLine(".plant-tab:hover{color:var(--text);background:var(--surface2);}");
        sb.AppendLine(".plant-tab.active{color:var(--text);background:var(--surface2);border-color:var(--border2);border-bottom-color:var(--surface2);}");

        // ── Toolbar ──
        sb.AppendLine(".toolbar{display:flex;align-items:center;gap:10px;padding:14px 20px;flex-wrap:wrap;background:var(--surface);border-bottom:1px solid var(--border);}");
        sb.AppendLine(".toolbar select,.toolbar input{background:var(--surface2);border:1px solid var(--border2);border-radius:7px;padding:7px 11px;font-size:12px;color:var(--text);font-family:'DM Sans',sans-serif;}");
        sb.AppendLine(".periodo-toggle{display:flex;gap:4px;background:var(--surface2);border:1px solid var(--border2);border-radius:8px;padding:3px;}");
        sb.AppendLine(".periodo-btn{padding:5px 14px;border-radius:6px;font-size:12px;font-weight:600;cursor:pointer;border:none;background:transparent;color:var(--muted2);transition:all .15s;}");
        sb.AppendLine(".periodo-btn.active{background:var(--accent);color:white;}");
        sb.AppendLine(".semana-jump{display:flex;align-items:center;gap:6px;margin-left:auto;}");
        sb.AppendLine(".semana-jump label{font-size:11px;color:var(--muted2);}");
        sb.AppendLine(".semana-jump input{width:60px;text-align:center;}");
        sb.AppendLine(".btn-refresh{display:flex;align-items:center;gap:5px;padding:7px 13px;background:var(--accent);border:none;border-radius:7px;color:white;font-size:12px;font-weight:600;cursor:pointer;}");
        sb.AppendLine(".btn-refresh:hover{filter:brightness(1.1);}");
        sb.AppendLine(".anio-sel{display:flex;align-items:center;gap:6px;}");
        sb.AppendLine(".anio-sel label{font-size:11px;color:var(--muted2);}");

        // ── Semanas grid ──
        sb.AppendLine(".calendario-wrap{padding:16px 20px;overflow-x:auto;}");
        sb.AppendLine(".semanas-grid{display:flex;flex-direction:column;gap:8px;min-width:900px;}");
        sb.AppendLine(".semana-row{background:var(--surface);border:1px solid var(--border);border-radius:var(--radius);overflow:hidden;transition:border-color .15s;}");
        sb.AppendLine(".semana-row.tiene-equipos{border-color:rgba(59,130,246,.25);}");
        sb.AppendLine(".semana-row.semana-actual{border-color:rgba(16,185,129,.4);box-shadow:0 0 0 1px rgba(16,185,129,.15);}");
        sb.AppendLine(".semana-header{display:flex;align-items:center;gap:12px;padding:10px 14px;cursor:pointer;user-select:none;background:var(--surface2);}");
        sb.AppendLine(".semana-header:hover{background:var(--surface3);}");
        sb.AppendLine(".semana-num{font-family:'DM Mono',monospace;font-size:11px;font-weight:700;color:var(--muted);min-width:58px;}");
        sb.AppendLine(".semana-rango{font-size:11px;color:var(--muted2);flex:1;}");
        sb.AppendLine(".semana-badge{font-size:10px;font-weight:700;padding:3px 9px;border-radius:999px;}");
        sb.AppendLine(".badge-ok{background:rgba(16,185,129,.15);border:1px solid rgba(16,185,129,.3);color:#6ee7b7;}");
        sb.AppendLine(".badge-pend{background:rgba(245,158,11,.12);border:1px solid rgba(245,158,11,.3);color:#fcd34d;}");
        sb.AppendLine(".badge-venc{background:rgba(239,68,68,.12);border:1px solid rgba(239,68,68,.3);color:#fca5a5;}");
        sb.AppendLine(".semana-arrow{font-size:10px;color:var(--muted);transition:transform .2s;}");
        sb.AppendLine(".semana-row.open .semana-arrow{transform:rotate(180deg);}");
        sb.AppendLine(".semana-body{display:none;padding:10px 14px;border-top:1px solid var(--border);}");
        sb.AppendLine(".semana-row.open .semana-body{display:block;}");

        // ── Tabla de equipos ──
        sb.AppendLine(".eq-table{width:100%;border-collapse:collapse;font-size:12px;}");
        sb.AppendLine(".eq-table th{text-align:left;padding:6px 10px;font-size:10px;font-weight:700;text-transform:uppercase;letter-spacing:.08em;color:var(--muted);border-bottom:1px solid var(--border);}");
        sb.AppendLine(".eq-table td{padding:7px 10px;border-bottom:1px solid var(--border);vertical-align:middle;}");
        sb.AppendLine(".eq-table tr:last-child td{border-bottom:none;}");
        sb.AppendLine(".eq-table tr:hover td{background:rgba(255,255,255,.02);}");
        sb.AppendLine(".tag-color{padding:2px 8px;border-radius:999px;font-size:10px;font-weight:700;text-transform:uppercase;}");
        sb.AppendLine(".tag-verde{background:#052e16;color:#6ee7b7;border:1px solid rgba(16,185,129,.3);}");
        sb.AppendLine(".tag-rojo{background:#1f0000;color:#fca5a5;border:1px solid rgba(239,68,68,.3);}");
        sb.AppendLine(".tag-azul{background:#001233;color:#93c5fd;border:1px solid rgba(59,130,246,.3);}");
        sb.AppendLine(".tag-amarillo{background:#1c1400;color:#fcd34d;border:1px solid rgba(245,158,11,.3);}");
        sb.AppendLine(".tag-gris{background:#0f172a;color:#94a3b8;border:1px solid rgba(148,163,184,.2);}");
        sb.AppendLine(".tag-rosa{background:#1f0011;color:#f9a8d4;border:1px solid rgba(244,114,182,.3);}");
        sb.AppendLine(".tag-default{background:var(--surface2);color:var(--muted2);border:1px solid var(--border2);}");
        sb.AppendLine(".estado-ok{color:#6ee7b7;font-size:11px;}");
        sb.AppendLine(".estado-pend{color:#fcd34d;font-size:11px;}");
        sb.AppendLine(".estado-venc{color:#fca5a5;font-size:11px;}");
        sb.AppendLine(".estado-sin{color:var(--muted);font-size:11px;}");

        // ── Empty / loading ──
        sb.AppendLine(".empty-msg{text-align:center;padding:18px;color:var(--muted);font-size:12px;}");
        sb.AppendLine(".loading{text-align:center;padding:40px;color:var(--muted2);font-size:13px;}");

        // ── Toast ──
        sb.AppendLine(".toast{position:fixed;bottom:24px;right:24px;padding:11px 18px;border-radius:10px;font-size:13px;font-weight:600;z-index:9999;pointer-events:none;}");
        sb.AppendLine(".toast-ok{background:#052e16;border:1px solid #10B981;color:#6ee7b7;}");
        sb.AppendLine(".toast-err{background:#1f0000;border:1px solid #EF4444;color:#fca5a5;}");

        // ── Leyenda actual ──
        sb.AppendLine(".semana-actual-label{display:inline-flex;align-items:center;gap:5px;padding:2px 9px;border-radius:999px;background:rgba(16,185,129,.12);border:1px solid rgba(16,185,129,.3);color:#6ee7b7;font-size:10px;font-weight:700;margin-left:6px;}");

        sb.AppendLine("</style></head><body>");

        // ── Top bar HTML ──
        sb.AppendLine("<div class=\"top-bar\">");
        sb.AppendLine("  <div class=\"top-icon\">📅</div>");
        sb.AppendLine("  <div class=\"top-title\"><h1>Calendario de Mantenimiento Preventivo</h1><p>Vista semanal por planta — Períodos P1 y P2</p></div>");
        sb.AppendLine("  <a href=\"/static/menu.html\" style=\"font-size:12px;color:var(--muted2);text-decoration:none;padding:7px 13px;background:var(--surface2);border:1px solid var(--border2);border-radius:7px;\">← Menú</a>");
        sb.AppendLine("</div>");

        // ── Plant nav placeholder (se llena por JS) ──
        sb.AppendLine("<div class=\"plant-nav\" id=\"plantNav\"></div>");

        // ── Toolbar ──
        sb.AppendLine("<div class=\"toolbar\">");
        sb.AppendLine("  <div class=\"periodo-toggle\">");
        sb.AppendLine("    <button class=\"periodo-btn active\" id=\"btnP1\" onclick=\"setPeriodo(1)\">Período 1</button>");
        sb.AppendLine("    <button class=\"periodo-btn\" id=\"btnP2\" onclick=\"setPeriodo(2)\">Período 2</button>");
        sb.AppendLine("    <button class=\"periodo-btn\" id=\"btnAmbos\" onclick=\"setPeriodo(0)\">Ambos</button>");
        sb.AppendLine("  </div>");
        sb.AppendLine("  <div class=\"anio-sel\">");
        sb.AppendLine("    <label>Año</label>");
        sb.AppendLine("    <select id=\"selAnio\" onchange=\"cargarCalendario()\"></select>");
        sb.AppendLine("  </div>");
        sb.AppendLine("  <label style=\"font-size:11px;color:var(--muted2);\">Filtrar semana</label>");
        sb.AppendLine("  <input type=\"text\" id=\"filtroSemana\" placeholder=\"Ej: 12\" style=\"width:70px;\" oninput=\"filtrarSemana(this.value)\">");
        sb.AppendLine("  <label style=\"font-size:11px;color:var(--muted2);\">Buscar equipo</label>");
        sb.AppendLine("  <input type=\"text\" id=\"filtroEquipo\" placeholder=\"Dispositivo o ID...\" style=\"min-width:180px;\" oninput=\"filtrarEquipo(this.value)\">");
        sb.AppendLine("  <div class=\"semana-jump\">");
        sb.AppendLine("    <label>Ir a semana</label>");
        sb.AppendLine("    <input type=\"number\" id=\"jumpSemana\" min=\"1\" max=\"53\" placeholder=\"—\" onchange=\"irASemana(this.value)\">");
        sb.AppendLine("  </div>");
        sb.AppendLine("  <button class=\"btn-refresh\" onclick=\"cargarCalendario()\">🔄 Actualizar</button>");
        sb.AppendLine("</div>");

        // ── Calendario ──
        sb.AppendLine("<div class=\"calendario-wrap\">");
        sb.AppendLine("  <div id=\"calendarioContainer\" class=\"loading\">Cargando calendario...</div>");
        sb.AppendLine("</div>");

        // ── JS ──
        sb.AppendLine("<script>");
        sb.AppendLine("let datosCalendario=null, plantaActual=null, periodoActual=1;");

        // Poblar selector de años
        sb.AppendLine("(function(){");
        sb.AppendLine("  const sel=document.getElementById('selAnio');");
        sb.AppendLine("  const hoy=new Date().getFullYear();");
        sb.AppendLine("  for(let y=hoy-1;y<=hoy+2;y++){");
        sb.AppendLine("    const o=document.createElement('option');o.value=y;o.textContent=y;");
        sb.AppendLine("    if(y===hoy)o.selected=true;");
        sb.AppendLine("    sel.appendChild(o);");
        sb.AppendLine("  }");
        sb.AppendLine("})();");

        // cargarCalendario
        sb.AppendLine("async function cargarCalendario(){");
        sb.AppendLine("  document.getElementById('calendarioContainer').innerHTML='<div class=\"loading\">Cargando...</div>';");
        sb.AppendLine("  const anio=document.getElementById('selAnio').value;");
        sb.AppendLine("  try{");
        sb.AppendLine("    const res=await fetch('/CALENDARIO/API?anio='+anio);");
        sb.AppendLine("    datosCalendario=await res.json();");
        sb.AppendLine("    construirNavPlantas(datosCalendario.plantas);");
        sb.AppendLine("    if(!plantaActual||!datosCalendario.plantas.includes(plantaActual))");
        sb.AppendLine("      plantaActual=datosCalendario.plantas[0]||null;");
        sb.AppendLine("    renderCalendario();");
        sb.AppendLine("  }catch(e){");
        sb.AppendLine("    document.getElementById('calendarioContainer').innerHTML='<div class=\"empty-msg\">Error al cargar datos</div>';");
        sb.AppendLine("    toast('Error al cargar calendario',false);");
        sb.AppendLine("  }");
        sb.AppendLine("}");

        // construirNavPlantas
        sb.AppendLine("function construirNavPlantas(plantas){");
        sb.AppendLine("  const nav=document.getElementById('plantNav');");
        sb.AppendLine("  nav.innerHTML='';");
        sb.AppendLine("  plantas.forEach(p=>{");
        sb.AppendLine("    const t=document.createElement('button');");
        sb.AppendLine("    t.className='plant-tab'+(p===plantaActual?' active':'');");
        sb.AppendLine("    t.textContent=p;");
        sb.AppendLine("    t.onclick=()=>{plantaActual=p;document.querySelectorAll('.plant-tab').forEach(x=>x.classList.remove('active'));t.classList.add('active');renderCalendario();};");
        sb.AppendLine("    nav.appendChild(t);");
        sb.AppendLine("  });");
        sb.AppendLine("}");

        // setPeriodo
        sb.AppendLine("function setPeriodo(p){");
        sb.AppendLine("  periodoActual=p;");
        sb.AppendLine("  ['btnP1','btnP2','btnAmbos'].forEach(id=>document.getElementById(id).classList.remove('active'));");
        sb.AppendLine("  if(p===1)document.getElementById('btnP1').classList.add('active');");
        sb.AppendLine("  else if(p===2)document.getElementById('btnP2').classList.add('active');");
        sb.AppendLine("  else document.getElementById('btnAmbos').classList.add('active');");
        sb.AppendLine("  renderCalendario();");
        sb.AppendLine("}");

        // renderCalendario — construye las semanas 1-53
        sb.AppendLine("function renderCalendario(){");
        sb.AppendLine("  if(!datosCalendario||!plantaActual){document.getElementById('calendarioContainer').innerHTML='<div class=\"empty-msg\">Sin datos</div>';return;}");
        sb.AppendLine("  const anio=parseInt(document.getElementById('selAnio').value);");
        sb.AppendLine("  const calPlanta=datosCalendario.calendario[plantaActual]||{};");
        sb.AppendLine("  const semanaActual=isoWeek(new Date());");
        sb.AppendLine("  const hoy=new Date();");
        sb.AppendLine("  let html='<div class=\"semanas-grid\">';");
        sb.AppendLine("  const totalSemanas=semanasEnAnio(anio);");
        sb.AppendLine("  for(let s=1;s<=totalSemanas;s++){");
        sb.AppendLine("    const semDatos=calPlanta[s]||{p1:[],p2:[]};");
        sb.AppendLine("    const listaP1=semDatos.p1||[];");
        sb.AppendLine("    const listaP2=semDatos.p2||[];");
        sb.AppendLine("    let equipos=[];");
        sb.AppendLine("    if(periodoActual===1)equipos=listaP1.map(e=>({...e,_periodo:1}));");
        sb.AppendLine("    else if(periodoActual===2)equipos=listaP2.map(e=>({...e,_periodo:2}));");
        sb.AppendLine("    else equipos=[...listaP1.map(e=>({...e,_periodo:1})),...listaP2.map(e=>({...e,_periodo:2}))];");
        sb.AppendLine("    const tieneEquipos=equipos.length>0;");
        sb.AppendLine("    const esActual=s===semanaActual&&new Date(anio,0,1).getFullYear()===hoy.getFullYear();");
        sb.AppendLine("    const {lunes,domingo}=rangoSemana(anio,s);");
        sb.AppendLine("    const clases='semana-row'+(tieneEquipos?' tiene-equipos':'')+(esActual?' semana-actual open':'');");
        sb.AppendLine("    html+='<div class=\"'+clases+'\" id=\"semana-'+s+'\">';");
        sb.AppendLine("    html+='<div class=\"semana-header\" onclick=\"toggleSemana('+s+')\">';");
        sb.AppendLine("    html+='<span class=\"semana-num\">Semana '+s+'</span>';");
        sb.AppendLine("    html+='<span class=\"semana-rango\">'+fmtFecha(lunes)+' — '+fmtFecha(domingo)+(esActual?'<span class=\"semana-actual-label\">● Esta semana</span>':'')+' </span>';");
        sb.AppendLine("    if(tieneEquipos){");
        sb.AppendLine("      const ok=equipos.filter(e=>e.realizado).length;");
        sb.AppendLine("      const pend=equipos.filter(e=>!e.realizado&&!estaVencido(e.plazo,hoy)).length;");
        sb.AppendLine("      const venc=equipos.filter(e=>!e.realizado&&estaVencido(e.plazo,hoy)).length;");
        sb.AppendLine("      if(ok>0)html+='<span class=\"semana-badge badge-ok\">✓ '+ok+' realizado'+(ok>1?'s':'')+'</span>';");
        sb.AppendLine("      if(pend>0)html+='<span class=\"semana-badge badge-pend\">⏳ '+pend+' pendiente'+(pend>1?'s':'')+'</span>';");
        sb.AppendLine("      if(venc>0)html+='<span class=\"semana-badge badge-venc\">⚠ '+venc+' vencido'+(venc>1?'s':'')+'</span>';");
        sb.AppendLine("    }");
        sb.AppendLine("    html+='<span class=\"semana-arrow\">▼</span>';");
        sb.AppendLine("    html+='</div>';"); // end semana-header
        sb.AppendLine("    html+='<div class=\"semana-body\">';");
        sb.AppendLine("    if(!tieneEquipos){");
        sb.AppendLine("      html+='<div class=\"empty-msg\">Sin mantenimientos programados esta semana</div>';");
        sb.AppendLine("    }else{");
        sb.AppendLine("      html+='<table class=\"eq-table\"><thead><tr>';");
        sb.AppendLine("      html+='<th>Período</th><th>ID Equipo</th><th>Dispositivo</th><th>Ubicación</th><th>Color</th><th>Último PM</th><th>Plazo</th><th>Técnico</th><th>Estado</th>';");
        sb.AppendLine("      html+='</tr></thead><tbody>';");
        sb.AppendLine("      equipos.forEach(e=>{");
        sb.AppendLine("        const estado=estadoEquipo(e,hoy);");
        sb.AppendLine("        html+='<tr>';");
        sb.AppendLine("        html+='<td><span style=\"font-size:10px;font-weight:700;padding:2px 7px;border-radius:999px;background:'+(e._periodo===1?'rgba(139,92,246,.15)':'rgba(6,182,212,.15)')+';color:'+(e._periodo===1?'#c4b5fd':'#67e8f9')+';border:1px solid '+(e._periodo===1?'rgba(139,92,246,.3)':'rgba(6,182,212,.3)')+'\">P'+e._periodo+'</span></td>';");
        sb.AppendLine("        html+='<td style=\"font-family:monospace;font-size:11px\">'+esc(e.id_equipo)+'</td>';");
        sb.AppendLine("        html+='<td>'+esc(e.dispositivo)+'</td>';");
        sb.AppendLine("        html+='<td>'+esc(e.ubicacion)+'</td>';");
        sb.AppendLine("        html+='<td>'+colorTag(e.color)+'</td>';");
        sb.AppendLine("        html+='<td style=\"font-family:monospace;font-size:11px\">'+( e.fecha||'—')+'</td>';");
        sb.AppendLine("        html+='<td style=\"font-family:monospace;font-size:11px\">'+( e.plazo||'—')+'</td>';");
        sb.AppendLine("        html+='<td>'+esc(e.tecnico||'—')+'</td>';");
        sb.AppendLine("        html+='<td>'+estado+'</td>';");
        sb.AppendLine("        html+='</tr>';");
        sb.AppendLine("      });");
        sb.AppendLine("      html+='</tbody></table>';");
        sb.AppendLine("    }");
        sb.AppendLine("    html+='</div>';"); // end semana-body
        sb.AppendLine("    html+='</div>';"); // end semana-row
        sb.AppendLine("  }");
        sb.AppendLine("  html+='</div>';");
        sb.AppendLine("  document.getElementById('calendarioContainer').innerHTML=html;");
        sb.AppendLine("}");

        // toggleSemana
        sb.AppendLine("function toggleSemana(s){document.getElementById('semana-'+s).classList.toggle('open');}");

        // irASemana
        sb.AppendLine("function irASemana(s){");
        sb.AppendLine("  if(!s)return;");
        sb.AppendLine("  const el=document.getElementById('semana-'+s);");
        sb.AppendLine("  if(!el)return;");
        sb.AppendLine("  el.classList.add('open');");
        sb.AppendLine("  el.scrollIntoView({behavior:'smooth',block:'start'});");
        sb.AppendLine("}");

        // filtrarSemana — oculta semanas que no coinciden con el número
        sb.AppendLine("function filtrarSemana(val){");
        sb.AppendLine("  document.querySelectorAll('.semana-row').forEach(row=>{");
        sb.AppendLine("    if(!val){row.style.display='';return;}");
        sb.AppendLine("    const num=row.id.replace('semana-','');");
        sb.AppendLine("    row.style.display=num.includes(val.trim())?'':'none';");
        sb.AppendLine("  });");
        sb.AppendLine("}");

        // filtrarEquipo — filtra filas dentro de las tablas
        sb.AppendLine("function filtrarEquipo(val){");
        sb.AppendLine("  const v=val.toLowerCase().trim();");
        sb.AppendLine("  document.querySelectorAll('.eq-table tbody tr').forEach(tr=>{");
        sb.AppendLine("    tr.style.display=!v||tr.textContent.toLowerCase().includes(v)?'':'none';");
        sb.AppendLine("  });");
        sb.AppendLine("}");

        // Helpers de fecha y semana ISO
        sb.AppendLine("function isoWeek(d){");
        sb.AppendLine("  const date=new Date(Date.UTC(d.getFullYear(),d.getMonth(),d.getDate()));");
        sb.AppendLine("  const day=date.getUTCDay()||7;");
        sb.AppendLine("  date.setUTCDate(date.getUTCDate()+4-day);");
        sb.AppendLine("  const yearStart=new Date(Date.UTC(date.getUTCFullYear(),0,1));");
        sb.AppendLine("  return Math.ceil((((date-yearStart)/86400000)+1)/7);");
        sb.AppendLine("}");

        sb.AppendLine("function semanasEnAnio(y){");
        sb.AppendLine("  const d28=new Date(y,11,28);");
        sb.AppendLine("  return isoWeek(d28)===1?52:isoWeek(d28);");
        sb.AppendLine("}");

        sb.AppendLine("function rangoSemana(anio,semana){");
        sb.AppendLine("  const simple=new Date(anio,0,1+(semana-1)*7);");
        sb.AppendLine("  const day=simple.getDay()||7;");
        sb.AppendLine("  const lunes=new Date(simple);lunes.setDate(simple.getDate()-day+1);");
        sb.AppendLine("  const domingo=new Date(lunes);domingo.setDate(lunes.getDate()+6);");
        sb.AppendLine("  return{lunes,domingo};");
        sb.AppendLine("}");

        sb.AppendLine("function fmtFecha(d){");
        sb.AppendLine("  if(!d)return'—';");
        sb.AppendLine("  const dd=String(d.getDate()).padStart(2,'0');");
        sb.AppendLine("  const mm=String(d.getMonth()+1).padStart(2,'0');");
        sb.AppendLine("  return dd+'/'+mm;");
        sb.AppendLine("}");

        sb.AppendLine("function estaVencido(plazo,hoy){");
        sb.AppendLine("  if(!plazo)return false;");
        sb.AppendLine("  return new Date(plazo)<hoy;");
        sb.AppendLine("}");

        sb.AppendLine("function estadoEquipo(e,hoy){");
        sb.AppendLine("  if(e.realizado)return'<span class=\"estado-ok\">✓ Realizado</span>';");
        sb.AppendLine("  if(!e.plazo)return'<span class=\"estado-sin\">Sin plazo</span>';");
        sb.AppendLine("  if(new Date(e.plazo)<hoy)return'<span class=\"estado-venc\">⚠ Vencido</span>';");
        sb.AppendLine("  return'<span class=\"estado-pend\">⏳ Pendiente</span>';");
        sb.AppendLine("}");

        sb.AppendLine("function colorTag(c){");
        sb.AppendLine("  const v=(c||'').toLowerCase();");
        sb.AppendLine("  if(v.includes('verde'))return'<span class=\"tag-color tag-verde\">Verde</span>';");
        sb.AppendLine("  if(v.includes('rojo'))return'<span class=\"tag-color tag-rojo\">Rojo</span>';");
        sb.AppendLine("  if(v.includes('azul'))return'<span class=\"tag-color tag-azul\">Azul</span>';");
        sb.AppendLine("  if(v.includes('amarillo'))return'<span class=\"tag-color tag-amarillo\">Amarillo</span>';");
        sb.AppendLine("  if(v.includes('gris'))return'<span class=\"tag-color tag-gris\">Gris</span>';");
        sb.AppendLine("  if(v.includes('rosa'))return'<span class=\"tag-color tag-rosa\">Rosa</span>';");
        sb.AppendLine("  return'<span class=\"tag-color tag-default\">'+(c||'—')+'</span>';");
        sb.AppendLine("}");

        sb.AppendLine("function esc(s){return(s||'').replace(/&/g,'&amp;').replace(/</g,'&lt;').replace(/>/g,'&gt;');}");

        sb.AppendLine("function toast(msg,ok){const t=document.createElement('div');t.className='toast '+(ok?'toast-ok':'toast-err');t.textContent=msg;document.body.appendChild(t);setTimeout(()=>t.remove(),3000);}");

        // Auto-ir a la semana actual al cargar
        sb.AppendLine("cargarCalendario().then(()=>{");
        sb.AppendLine("  const s=isoWeek(new Date());");
        sb.AppendLine("  document.getElementById('jumpSemana').value=s;");
        sb.AppendLine("  setTimeout(()=>{const el=document.getElementById('semana-'+s);if(el)el.scrollIntoView({behavior:'smooth',block:'start'});},300);");
        sb.AppendLine("});");

        sb.AppendLine("</script></body></html>");
        return sb.ToString();
    }
}