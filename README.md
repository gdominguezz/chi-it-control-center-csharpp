# CHI-IT Control Center — Backend C# (ASP.NET Core 8)

Migración completa del backend Python/FastAPI a C#/ASP.NET Core.
**La base de datos y los HTMLs no cambian.**

## Estructura del proyecto

```
ChiIT/
├── Controllers/
│   ├── LoginController.cs        ← Equivalente a login.py
│   └── PreventivoController.cs   ← Equivalente a preventivos.py
├── Data/
│   └── DbConnectionPool.cs       ← Equivalente a database.py
├── Models/
│   └── Models.cs                 ← Modelos de request/response
├── Services/
│   ├── AuditoriaService.cs       ← Registro de historial de cambios
│   ├── ExcelService.cs           ← Exportación a Excel con colores
│   └── QrService.cs              ← Generación de QR con texto
├── Program.cs                    ← Equivalente a main.py
├── appsettings.json              ← Configuración (conexión BD, rutas)
└── ChiIT.csproj                  ← Dependencias NuGet
```

## Requisitos

- .NET 8 SDK — https://dotnet.microsoft.com/download
- PostgreSQL corriendo con la base de datos SISTEMAS
- Las mismas 3 tablas: usuarios, mantenimientos_preventivos, auditoria_preventivos

## Configuración

Editar `appsettings.json`:

```json
{
  "ConnectionStrings": {
    "Postgres": "Host=127.0.0.1;Port=5432;Database=SISTEMAS;Username=postgres;Password=TU_PASSWORD;"
  },
  "AppSettings": {
    "ServerBaseUrl": "http://172.24.104.1:8000"
  }
}
```

## Instalar dependencias y ejecutar

```bash
cd ChiIT
dotnet restore
dotnet run
```

El servidor queda escuchando en: http://0.0.0.0:8000

## Archivos estáticos (HTMLs)

Copiar la carpeta `static/` dentro de `wwwroot/`:

```
ChiIT/
└── wwwroot/
    └── static/
        ├── login.html
        ├── menu.html
        ├── preventivos.html
        └── formato_preventivo_virtual.html
```

## Equivalencia de endpoints

Todos los endpoints son idénticos a los de Python — los HTMLs no necesitan ningún cambio.

| Python (FastAPI)             | C# (ASP.NET Core)                    |
|------------------------------|--------------------------------------|
| POST /LOGIN                  | LoginController.Login()              |
| POST /CAMBIAR_PASSWORD       | LoginController.CambiarPassword()    |
| GET  /obtener-usuario        | LoginController.ObtenerUsuario()     |
| GET  /PREVENTIVOS            | PreventivoController.ObtenerPreventivos() |
| POST /PREVENTIVO             | PreventivoController.Crear()         |
| PUT  /PREVENTIVO/{id}        | PreventivoController.Editar()        |
| DELETE /PREVENTIVO/{id}      | PreventivoController.Eliminar()      |
| GET  /PREVENTIVOS/{id}/HISTORIAL | PreventivoController.ObtenerHistorial() |
| POST /PREVENTIVO/PDF/{id}    | PreventivoController.SubirPdf()      |
| GET  /PREVENTIVO/PDF/{id}    | PreventivoController.ObtenerPdf()    |
| DELETE /PREVENTIVO/PDF/{id}  | PreventivoController.EliminarPdf()   |
| GET  /PREVENTIVOS/EXPORTAR_TODO | PreventivoController.ExportarTodo() |
| GET  /PREVENTIVOS/EXPORTAR_FILTRADO | PreventivoController.ExportarFiltrado() |
| GET  /PREVENTIVOS/EXPORTAR_ANIO | PreventivoController.ExportarAnio() |
| GET  /QR_MESA_GENERAR/{ub}   | PreventivoController.GenerarQr()     |
| GET  /QR_GENERAR_TODOS       | PreventivoController.GenerarTodosQr() |
| GET  /QR_REIMPRIMIR/{ub}     | PreventivoController.ReimprimirQr()  |
| POST /PREVENTIVO/GUARDAR_DIGITAL/{id} | PreventivoController.GuardarDigital() |
| GET  /PREVENTIVO/DIGITAL/{id} | PreventivoController.ObtenerDigital() |
| DELETE /PREVENTIVO/ELIMINAR_DIGITAL/{id} | PreventivoController.EliminarDigital() |
| GET  /PREVENTIVO/VERIFICAR_USUARIO | PreventivoController.VerificarUsuario() |
| POST /PREVENTIVO/GUARDAR_PM/{id} | PreventivoController.GuardarPmIndividual() |
| GET  /preventivos/qr/{ub}    | QrPageController (pendiente — genera HTML dinámico) |
