using ChiIT.Data;
using ChiIT.Middleware;
using ChiIT.Services;

// ── Zona horaria México/Chihuahua ──
Environment.SetEnvironmentVariable("TZ", "America/Chihuahua");
AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

var builder = WebApplication.CreateBuilder(args);

// Necesario para leer la IP real detrás del proxy de Render
builder.Services.Configure<Microsoft.AspNetCore.HttpOverrides.ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedFor
                             | Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedProto;
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});

// ── Servicios ──
builder.Services.AddControllers();
builder.Services.AddSingleton<DbConnectionPool>();
builder.Services.AddScoped<AuditoriaService>();
builder.Services.AddScoped<ExcelService>();
builder.Services.AddScoped<QrService>();

// CORS — permite que los HTMLs hagan fetch desde cualquier origen en la red local
builder.Services.AddCors(opt => opt.AddDefaultPolicy(p =>
    p.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));

var app = builder.Build();

app.UseForwardedHeaders();

// ── Filtro de IP — solo red interna 172.24.104.x ──
app.UseMiddleware<IpFilterMiddleware>();

app.UseCors();

// ── Archivos estáticos (HTML, CSS, JS) ──
app.UseStaticFiles();          // sirve /wwwroot/
app.UseDefaultFiles();         // index.html por defecto

// ── Ruta raíz → login ──
app.MapGet("/", () => Results.Redirect("/static/login.html"));

app.MapControllers();

// ── Crear directorios necesarios al iniciar ──
Directory.CreateDirectory("PDF_DATABASE/PREVENTIVOS");
Directory.CreateDirectory("QR_CODES/MESAS");

// ── Puerto dinámico para Render ──
var port = Environment.GetEnvironmentVariable("PORT") ?? "10000";
app.Run($"http://0.0.0.0:{port}");