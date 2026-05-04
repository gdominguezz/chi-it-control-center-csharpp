using ChiIT.Services;
using ChiIT.Data;

// ── Zona horaria México/Chihuahua ──
Environment.SetEnvironmentVariable("TZ", "America/Chihuahua");
AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

var builder = WebApplication.CreateBuilder(args);

// ── Servicios ──
///////////////////////////////////////////////modulos principales//////////////////
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
builder.Services.AddScoped<PresupuestosReqVsOcService>();            //SERVICIO DEL MODULO REQ VS OC
builder.Services.AddScoped<RegistroEntradasTemporalService>();      // REGISTRO ENTRADAS TEMPORAL
builder.Services.AddScoped<OrdenesDeCompraService>();              // ORDENES DE COMPRA
builder.Services.AddScoped<PantallasNfService>();                 // PANTALLAS_NF
builder.Services.AddScoped<RefaccionesNFService>();              // REFACCIONES NF
builder.Services.AddScoped<BuscarGlobalService>();              // BUSCADOR GLOBAL DE PRESUPUESTOS
builder.Services.AddScoped<AccesoriosNFService>();             // ACCESORIOS NF
builder.Services.AddScoped<HerramientasNFService>();          // HERRAMIENTAS NF
builder.Services.AddScoped<DispositivosNFService>();         // DISPOSITIVOS NF
builder.Services.AddScoped<InventariosNFService>();         // INVENTARIOS NF
builder.Services.AddScoped<PerifeicosNFService>();         // PERIFÉRICOS NF
builder.Services.AddScoped<BitacoraFirecomService>();     // BITACORA FIRECOM
builder.Services.AddScoped<CamarasAudioService>();       // CAMARAS AUDIO
builder.Services.AddScoped<ImpresorasNFService>();      // IMPRESORAS NF
builder.Services.AddScoped<ServiciosProveedoresService>();//SERVICIOS POR PROVEEDORES 
builder.Services.AddScoped<ConsumiblesNFService>();      // CONSUMIBLES NF
builder.Services.AddScoped<RemisionesService>();        // REMISIONES
builder.Services.AddScoped<RadiosNFService>();         //  RADIOS_NF
builder.Services.AddScoped<TintasTonerRibonNFService>();  // TINTAS TONER RIBON NF
builder.Services.AddScoped<EquipoRedNFService>();        // EQUIPO DE RED NF
builder.Services.AddSingleton<ImpresorasReportesService>(); //REPORTES IMPRESORAS


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