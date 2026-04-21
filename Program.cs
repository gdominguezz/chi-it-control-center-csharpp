using ChiIT.Services;
using ChiIT.Data;

// ── Zona horaria México/Chihuahua ──
Environment.SetEnvironmentVariable("TZ", "America/Chihuahua");
AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

var builder = WebApplication.CreateBuilder(args);

// ── Servicios ──
builder.Services.AddControllers()
    .AddJsonOptions(o =>
        o.JsonSerializerOptions.PropertyNamingPolicy = null); // mantiene mayúsculas tal cual
builder.Services.AddSingleton<DbConnectionPool>();                         // SERVICIO DE METODO DE CONEXION A LA DB
builder.Services.AddScoped<AuditoriaServicepreventivos>();                // AUDITORIA DE PREVENTIVOS
builder.Services.AddSingleton<AuditoriaServiceCorrectivos>();            // AUDITORIA DE CORRECTIVOS
builder.Services.AddScoped<ExcelService>();                             // SERVICIOS DE EXCEL
builder.Services.AddScoped<QrService>();                               // QRS
builder.Services.AddScoped<BajasService>();  // BAJAS

///////////////////////////////////////////////////////// PRESUPUESTO //////////////////////////////////////
builder.Services.AddScoped<PresupuestosReqVsOcService>();           //SERVICIO DEL MODULO REQ VS OC
builder.Services.AddScoped<RegistroEntradasTemporalService>(); // REGISTRO ENTRADAS TEMPORAL

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
Directory.CreateDirectory("PDF_DATABASE/BAJAS");         //  BAJAS
Directory.CreateDirectory("QR_CODES/MESAS");


// ── Puerto dinámico para NORTFLANK ──
var port = Environment.GetEnvironmentVariable("PORT") ?? "10000";
app.Run($"http://0.0.0.0:{port}");