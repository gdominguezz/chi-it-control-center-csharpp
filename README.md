# 🛡️ Chi-IT Control Center
### Sistema de Gestión de Mantenimientos Preventivos
**S-Riko Automotive Hose de Chihuahua**

---

## 📋 Descripción

Chi-IT Control Center es una aplicación web interna desarrollada en **ASP.NET Core 8** para la gestión completa de mantenimientos preventivos de equipos de cómputo. Permite registrar, consultar, editar y dar seguimiento a los mantenimientos realizados por el equipo de TI, con soporte para generación de QR por ubicación y por equipo, exportación a Excel, carga de PDFs y un dashboard con gráficas en tiempo real.

---

## 🚀 Tecnologías

| Capa | Tecnología |
|---|---|
| Backend | ASP.NET Core 8 (C#) |
| Base de datos | PostgreSQL (Render) |
| Frontend | HTML + CSS + JS vanilla |
| Gráficas | Chart.js 4.4 |
| Generación QR | QRCoder + SixLabors.ImageSharp |
| Excel | ClosedXML |
| Deploy | Render (Docker) |

---

## 📁 Estructura del Proyecto

```
chi-it-control-center/
│
├── Controllers/
│   ├── LoginController.cs          # Autenticación, sesión, cambio de contraseña
│   ├── PreventivoController.cs     # CRUD de mantenimientos, PDF, Excel, QR
│   ├── DashboardController.cs      # KPIs, gráficas, técnico del mes
│   ├── AdminUsuariosApiController.cs # Gestión de usuarios (solo ADMIN)
│   └── QrPageController.cs         # Página web del QR por ubicación
│
├── Services/
│   ├── QrService.cs                # Generación de imágenes QR (ubicación y equipo)
│   ├── ExcelService.cs             # Exportación a .xlsx con colores
│   └── AuditoriaService.cs         # Registro de cambios en auditoría
│
├── Data/
│   └── DbConnectionPool.cs         # Pool de conexiones PostgreSQL
│
├── Models/
│   └── ...                         # Modelos de request/response
│
├── wwwroot/static/
│   ├── login.html                  # Página de login
│   ├── menu.html                   # Menú principal
│   ├── preventivos.html            # Gestión de mantenimientos preventivos
│   └── adminusuarios.html          # Administración de usuarios
│
├── Program.cs                      # Configuración de la app y servicios
├── appsettings.json                # Configuración (BD, URLs, rutas)
├── Dockerfile                      # Imagen Docker para Render
└── ChiIT_Web.csproj               # Dependencias NuGet
```

---

## ⚙️ Configuración

### `appsettings.json`

```json
{
  "ConnectionStrings": {
    "Postgres": "Host=...;Port=5432;Database=...;Username=...;Password=...;"
  },
  "AppSettings": {
    "ServerBaseUrl": "https://tu-app.onrender.com",
    "PdfDir": "PDF_DATABASE/PREVENTIVOS",
    "QrDir": "QR_CODES/MESAS"
  }
}
```

> ⚠️ **Nunca subas credenciales reales al repositorio.** Usa variables de entorno en producción.

---

## 🗄️ Base de Datos

### Tablas principales

**`public.mantenimientos_preventivos`**
| Campo | Tipo | Descripción |
|---|---|---|
| `id` | bigserial | ID único del registro |
| `id_equipo` | text | Identificador del equipo |
| `ubicacion` | text | Ubicación física (mesa/línea) |
| `nombre_dispositivo` | text | Tipo de dispositivo |
| `planta` | text | Planta o área |
| `categoria_color` | text | Color de categoría |
| `fecha_realizacion` | timestamp | Fecha del último PM |
| `plazo` | text | Fecha del próximo PM |
| `realizado_por` | text | Técnico que realizó el PM |
| `observaciones` | text | Notas adicionales |
| `preventivo_digital` | jsonb | Datos del PM digital |
| `pdf` | text | Ruta al PDF físico |
| `anio_creacion` | int | Año de creación del registro |

**`public.usuarios`**
| Campo | Tipo | Descripción |
|---|---|---|
| `id` | serial | ID único |
| `usuario` | text | Nombre de usuario (mayúsculas) |
| `nombre` | text | Nombre completo |
| `password_hash` | text | SHA-256 de la contraseña |
| `rol` | text | `ADMIN` o `USER` |
| `activo` | boolean | Si el usuario está habilitado |
| `password_temporal` | boolean | Fuerza cambio de contraseña |
| `ultimo_acceso` | timestamp | Último login |

**`public.auditoria_preventivos`**
| Campo | Tipo | Descripción |
|---|---|---|
| `registro_id` | int | ID del registro modificado |
| `usuario` | text | Quien realizó el cambio |
| `registro_anterior` | jsonb | Estado antes del cambio |
| `registro_nuevo` | jsonb | Estado después del cambio |
| `fecha_cambio` | timestamp | Cuándo se realizó |

---

## 🔐 Seguridad

- **Autenticación** por cookies HTTP-only (`usuario`, `rol`)
- **Contraseñas** hasheadas con SHA-256
- **Bloqueo por intentos fallidos**: 5 intentos incorrectos bloquean el usuario 10 minutos
- **Roles**: `ADMIN` puede crear, editar y eliminar — `USER` solo puede consultar y registrar PM
- **Protección de rutas**: todas las páginas verifican sesión activa y redirigen al login si no hay cookie válida
- **HTTP Basic Auth** configurado en Render como primera capa de seguridad

---

## 📡 API Endpoints

### Autenticación
| Método | Ruta | Descripción |
|---|---|---|
| POST | `/LOGIN` | Iniciar sesión |
| POST | `/LOGOUT` | Cerrar sesión |
| POST | `/CAMBIAR_PASSWORD` | Cambiar contraseña |
| GET | `/obtener-usuario` | Obtener usuario de la sesión activa |

### Mantenimientos Preventivos
| Método | Ruta | Descripción |
|---|---|---|
| GET | `/PREVENTIVOS` | Listar con filtros y paginación |
| POST | `/PREVENTIVO` | Crear nuevo registro |
| PUT | `/PREVENTIVO/{id}` | Editar registro |
| DELETE | `/PREVENTIVO/{id}` | Eliminar (solo ADMIN) |
| GET | `/PREVENTIVOS/{id}/HISTORIAL` | Ver historial de cambios |

### PDF
| Método | Ruta | Descripción |
|---|---|---|
| POST | `/PREVENTIVO/PDF/{id}` | Subir PDF físico |
| GET | `/PREVENTIVO/PDF/{id}` | Descargar PDF |
| DELETE | `/PREVENTIVO/PDF/{id}` | Eliminar PDF |

### PM Digital
| Método | Ruta | Descripción |
|---|---|---|
| POST | `/PREVENTIVO/GUARDAR_PM/{id}` | Registrar PM individual |
| POST | `/PREVENTIVO/GUARDAR_DIGITAL/{id}` | Guardar PM digital completo |
| GET | `/PREVENTIVO/DIGITAL/{id}` | Obtener PM digital guardado |
| DELETE | `/PREVENTIVO/ELIMINAR_DIGITAL/{id}` | Eliminar PM digital |

### QR
| Método | Ruta | Descripción |
|---|---|---|
| GET | `/QR_MESA_GENERAR/{ubicacion}` | Generar QR de ubicación (valida contra DB) |
| GET | `/QR_EQUIPO/{id}` | Generar QR con ID de equipo |
| GET | `/QR_GENERAR_TODOS` | Descargar ZIP con todos los QR |
| GET | `/preventivos/qr/{ubicacion}` | Página web del QR escaneado |

### Exportación
| Método | Ruta | Descripción |
|---|---|---|
| GET | `/PREVENTIVOS/EXPORTAR_TODO` | Exportar todo a Excel |
| GET | `/PREVENTIVOS/EXPORTAR_FILTRADO` | Exportar con filtros activos |
| GET | `/PREVENTIVOS/EXPORTAR_ANIO` | Exportar por año |

### Dashboard
| Método | Ruta | Descripción |
|---|---|---|
| GET | `/DASHBOARD` | KPIs, gráficas, técnico del mes, vencidos, próximos |

### Administración de Usuarios
| Método | Ruta | Descripción |
|---|---|---|
| GET | `/admin/usuarios/api` | Listar usuarios (solo ADMIN) |
| POST | `/admin/usuarios/api` | Crear usuario |
| PUT | `/admin/usuarios/api/{id}` | Editar usuario |
| PATCH | `/admin/usuarios/api/{id}/estado` | Activar/desactivar |
| PATCH | `/admin/usuarios/api/{id}/reset-password` | Resetear contraseña |

---

## 📊 Dashboard

El dashboard incluye:

- **KPIs** — Total equipos, con PM digital, sin PM digital, vencidos, PM esta semana
- **🍩 Dona** — Distribución por categoría de color
- **📊 Barras** — PM por planta
- **🍩 Dona** — PM Digital vs Sin PM Digital
- **📈 Línea** — PM realizados mes a mes (últimos 12 meses)
- **🏆 Técnico del mes** — Podio animado con ranking de los 5 mejores técnicos
- **Tablas** — Esta semana, próxima semana, próximos del mes, vencidos, últimos 10 realizados

---

## 📷 Código de Colores

| Color | Significado |
|---|---|
| 🟢 Verde | Equipos con sistema SRK de líneas |
| ⚫ Gris | Equipos sin sistema SRK |
| 🔵 Azul | Equipos fuera de área de producción |
| 🔴 Rojo | Correctivos |
| 🟡 Amarillo | Equipos en individual |
| 🩷 Rosa | Soporte Site 1 y 2 Stock |

---

## 🐳 Docker & Deploy

### Dockerfile

```dockerfile
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY . .
RUN dotnet restore
RUN dotnet publish -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
RUN apt-get update && apt-get install -y --no-install-recommends \
    fonts-liberation fontconfig \
    && fc-cache -fv \
    && rm -rf /var/lib/apt/lists/*
COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://0.0.0.0:10000
ENTRYPOINT ["dotnet", "ChiIT.Web.dll"]
```

> La instalación de `fonts-liberation` es necesaria para que la generación de imágenes QR con texto funcione en Linux.

### Variables de entorno en Render
| Variable | Valor |
|---|---|
| `PORT` | Asignado automáticamente por Render |
| `TZ` | `America/Chihuahua` (configurado en `Program.cs`) |

---

## 🛠️ Dependencias NuGet

| Paquete | Versión | Uso |
|---|---|---|
| `Npgsql` | 8.0.3 | Conexión a PostgreSQL |
| `ClosedXML` | 0.102.2 | Generación de archivos Excel |
| `QRCoder` | 1.6.0 | Generación de códigos QR |
| `SixLabors.ImageSharp` | 3.1.4 | Procesamiento de imágenes |
| `SixLabors.ImageSharp.Drawing` | 2.1.3 | Dibujo de texto en QR |

---

## 👥 Roles de Usuario

| Rol | Permisos |
|---|---|
| `ADMIN` | Crear, editar, eliminar registros · Gestionar usuarios · Ver todo |
| `USER` | Consultar registros · Registrar PM · Subir/ver PDFs · Ver dashboard |

---

## 📝 Notas de Desarrollo

- La zona horaria está configurada como `America/Chihuahua` directamente en `Program.cs` via `Environment.SetEnvironmentVariable("TZ", ...)` para corregir el desfase con el servidor de Render (Oregon, UTC-7/8).
- Los QR de ubicación validan contra la base de datos antes de generarse — no se puede crear un QR para una ubicación que no exista.
- El bloqueo por intentos fallidos es en memoria (`ConcurrentDictionary`) — se resetea si el servidor se reinicia.
- Los archivos PDF y QR se almacenan en el sistema de archivos del contenedor — en Render estos se pierden en cada deploy (usar almacenamiento externo para persistencia real).

---

## 📄 Licencia

Uso interno — S-Riko Automotive Hose de Chihuahua · Departamento de TI
