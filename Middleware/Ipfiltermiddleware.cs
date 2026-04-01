using System.Net;

namespace ChiIT.Middleware;

public class IpFilterMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<IpFilterMiddleware> _logger;

    private static readonly string[] _prefijosPermitidos =
    {
        "172.24.104."   // Red interna de la empresa
    };

    public IpFilterMiddleware(RequestDelegate next, ILogger<IpFilterMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var ip = ObtenerIpReal(context);
        var ipStr = ip.ToString();

        // Loguear siempre la IP para diagnóstico
        _logger.LogInformation("Petición desde IP: {IP} → {Path}", ipStr, context.Request.Path);

        // Permitir loopback (health checks internos de Render)
        if (IPAddress.IsLoopback(ip))
        {
            await _next(context);
            return;
        }

        var permitida = _prefijosPermitidos.Any(p => ipStr.StartsWith(p));

        if (!permitida)
        {
            _logger.LogWarning("Acceso BLOQUEADO desde IP: {IP}", ipStr);
            context.Response.StatusCode = 403;
            context.Response.ContentType = "text/html; charset=utf-8";
            await context.Response.WriteAsync(PaginaBloqueo(ipStr));
            return;
        }

        await _next(context);
    }

    private static IPAddress ObtenerIpReal(HttpContext context)
    {
        // Render y proxies usan distintos headers — probar en orden de prioridad
        string[] headersOrden = {
            "CF-Connecting-IP",   // Cloudflare
            "X-Real-IP",          // nginx
            "X-Forwarded-For",    // estándar (puede ser lista separada por comas)
            "X-Client-IP",
            "True-Client-IP"
        };

        foreach (var header in headersOrden)
        {
            var valor = context.Request.Headers[header].FirstOrDefault();
            if (string.IsNullOrWhiteSpace(valor)) continue;

            // X-Forwarded-For puede ser "ip1, ip2, ip3" — tomar la primera (cliente original)
            var candidata = valor.Split(',')[0].Trim();
            if (IPAddress.TryParse(candidata, out var ip))
                return ip;
        }

        // Fallback: IP directa de la conexión TCP
        return context.Connection.RemoteIpAddress ?? IPAddress.Loopback;
    }

    private static string PaginaBloqueo(string ip)
    {
        return "<!DOCTYPE html>" +
               "<html lang=\"es\">" +
               "<head>" +
               "<meta charset=\"UTF-8\">" +
               "<meta name=\"viewport\" content=\"width=device-width,initial-scale=1\">" +
               "<title>Acceso restringido</title>" +
               "<style>" +
               "* { box-sizing:border-box; margin:0; padding:0; }" +
               "body { font-family:'Segoe UI',sans-serif; background:#0B0F1A; color:#F1F5F9; min-height:100vh; display:flex; align-items:center; justify-content:center; }" +
               ".card { background:#111827; border:1px solid rgba(255,255,255,.08); border-radius:16px; padding:48px 40px; max-width:420px; text-align:center; box-shadow:0 20px 60px rgba(0,0,0,.5); }" +
               ".icon { font-size:56px; margin-bottom:20px; }" +
               "h1 { font-size:22px; font-weight:700; margin-bottom:12px; color:#EF4444; }" +
               "p { font-size:14px; color:#94A3B8; line-height:1.6; margin-bottom:8px; }" +
               ".ip { margin-top:20px; padding:10px 16px; background:rgba(239,68,68,.08); border:1px solid rgba(239,68,68,.2); border-radius:8px; font-family:monospace; font-size:13px; color:#FCA5A5; }" +
               "</style>" +
               "</head>" +
               "<body>" +
               "<div class=\"card\">" +
               "<div class=\"icon\">🔒</div>" +
               "<h1>Acceso restringido</h1>" +
               "<p>Este sistema solo está disponible desde la red interna de <strong>S-Riko Automotive Hose de Chihuahua</strong>.</p>" +
               "<p>Conéctate a la red de la empresa e intenta de nuevo.</p>" +
               $"<div class=\"ip\">Tu IP: {ip}</div>" +
               "</div>" +
               "</body>" +
               "</html>";
    }
}