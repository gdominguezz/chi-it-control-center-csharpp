using ChiIT.Services;
using ChiIT.Data;

// ── Zona horaria México/Chihuahua ──
Environment.SetEnvironmentVariable("TZ", "America/Chihuahua");
AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

var builder = WebApplication.CreateBuilder(args);

// ── Servicios ──
builder.Services.AddControllers();
builder.Services.AddSingleton<DbConnectionPool>();
builder.Services.AddScoped<AuditoriaServicepreventivos>();
builder.Services.AddSingleton<AuditoriaServiceCorrectivos>();
builder.Services.AddScoped<ExcelService>();
builder.Services.AddScoped<QrService>();

// CORS
builder.Services.AddCors(opt => opt.AddDefaultPolicy(p =>
    p.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));

var app = builder.Build();

app.UseCors();

// ── Archivos estáticos (HTML, CSS, JS) ──
app.UseStaticFiles();
app.UseDefaultFiles();

// ── Ruta raíz → login ──
app.MapGet("/", () => Results.Redirect("/static/login.html"));

app.MapControllers();

// ── Crear directorios necesarios al iniciar ──
Directory.CreateDirectory("PDF_DATABASE/PREVENTIVOS");
Directory.CreateDirectory("QR_CODES/MESAS");


// ── Puerto dinámico para Render ──
var port = Environment.GetEnvironmentVariable("PORT") ?? "10000";
app.Run($"http://0.0.0.0:{port}");