using ChiIT.Data;
using ChiIT.Services;

var builder = WebApplication.CreateBuilder(args);

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