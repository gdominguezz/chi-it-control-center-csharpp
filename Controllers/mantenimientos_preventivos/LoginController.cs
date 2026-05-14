using ChiIT.Data;
using ChiIT.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;

namespace ChiIT.Controllers;

[ApiController]
public class LoginController : ControllerBase
{
    private readonly DbConnectionPool _db;
    private readonly IWebHostEnvironment _env;

    // ── Control de intentos fallidos ─────────────────────
    // Clave: usuario en mayúsculas
    // Valor: (cantidad de intentos fallidos, momento del último intento)
    private static readonly ConcurrentDictionary<string, (int intentos, DateTime ultimo)> _intentos = new();

    private const int MAX_INTENTOS = 5;               // bloquear después de 5 fallos
    private const int MINUTOS_BLOQUEO = 10;             // bloqueo dura 10 minutos

    private bool EstasBloqueado(string usuario, out int minutosRestantes)
    {
        minutosRestantes = 0;
        if (!_intentos.TryGetValue(usuario, out var estado)) return false;

        // Si ya pasó el tiempo de bloqueo, limpiar
        if (DateTime.UtcNow - estado.ultimo > TimeSpan.FromMinutes(MINUTOS_BLOQUEO))
        {
            _intentos.TryRemove(usuario, out _);
            return false;
        }

        if (estado.intentos >= MAX_INTENTOS)
        {
            minutosRestantes = MINUTOS_BLOQUEO - (int)(DateTime.UtcNow - estado.ultimo).TotalMinutes;
            minutosRestantes = Math.Max(1, minutosRestantes);
            return true;
        }

        return false;
    }

    private void RegistrarFallo(string usuario)
    {
        _intentos.AddOrUpdate(
            usuario,
            _ => (1, DateTime.UtcNow),
            (_, anterior) => (anterior.intentos + 1, DateTime.UtcNow)
        );
    }

    private void LimpiarIntentos(string usuario)
    {
        _intentos.TryRemove(usuario, out _);
    }

    public LoginController(DbConnectionPool db, IWebHostEnvironment env)
    {
        _db = db;
        _env = env;
    }

    // ── POST /LOGIN ──────────────────────────────────────
    [HttpPost("LOGIN")]
    public IActionResult Login([FromBody] LoginRequest req)
    {
        var usuarioUpper = req.Usuario.ToUpper();

        // Verificar si está bloqueado
        if (EstasBloqueado(usuarioUpper, out int minutosRestantes))
            return Ok(new
            {
                ok = 0,
                mensaje = $"Usuario bloqueado por demasiados intentos fallidos. Intenta en {minutosRestantes} minuto{(minutosRestantes != 1 ? "s" : "")}."
            });

        using var conn = _db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT id, usuario, nombre, rol, password_hash, password_temporal, activo
            FROM usuarios
            WHERE usuario = @usr
            """;
        cmd.Parameters.AddWithValue("usr", usuarioUpper);

        using var reader = cmd.ExecuteReader();
        if (!reader.Read())
        {
            RegistrarFallo(usuarioUpper);
            return Ok(new { ok = 0, mensaje = "Usuario o contraseña incorrectos" });
        }

        var id = reader.GetInt32(0);
        var usuario = reader.GetString(1);
        var nombre = reader.GetString(2);
        var rol = reader.GetString(3);
        var pwdHash = reader.GetString(4);
        var pwdTemporal = reader.GetBoolean(5);
        var activo = reader.GetBoolean(6);
        reader.Close();

        if (!activo)
            return Ok(new { ok = 0, mensaje = "Usuario desactivado" });

        if (HashPassword(req.Password) != pwdHash)
        {
            RegistrarFallo(usuarioUpper);

            // Calcular intentos restantes para informar al usuario
            _intentos.TryGetValue(usuarioUpper, out var estado);
            int intentosRestantes = MAX_INTENTOS - estado.intentos;
            string mensaje = intentosRestantes > 0
                ? $"Usuario o contraseña incorrectos. Te quedan {intentosRestantes} intento{(intentosRestantes != 1 ? "s" : "")}."
                : $"Usuario bloqueado por {MINUTOS_BLOQUEO} minutos.";

            return Ok(new { ok = 0, mensaje });
        }

        // Login exitoso — limpiar intentos fallidos
        LimpiarIntentos(usuarioUpper);

        // Actualizar último acceso
        using var upd = conn.CreateCommand();
        upd.CommandText = "UPDATE usuarios SET ultimo_acceso = GETDATE() WHERE id = @id";
        upd.Parameters.AddWithValue("id", id);
        upd.ExecuteNonQuery();

        if (pwdTemporal)
        {


            return Ok(new
            {
                ok = 1,
                usuario,
                nombre,
                rol,
                password_temporal = 1
            });
        }


        // Setear cookie HTTP segura para verificación server-side
        Response.Cookies.Append("usuario", usuario, new CookieOptions
        {
            HttpOnly = true,
            SameSite = SameSiteMode.Lax,
            Secure = true
        });
        Response.Cookies.Append("rol", rol, new CookieOptions
        {
            HttpOnly = false,  // el JS del menú necesita leerlo
            SameSite = SameSiteMode.Lax,
            Secure = true
        });

        return Ok(new { ok = 1, usuario, nombre, rol, password_temporal = pwdTemporal });
    }

    //GET PARA BLOQUEAR CACHE DEL MENU 
    [HttpGet("menu")]
    public IActionResult Menu()
    {
        Response.Headers["Cache-Control"] = "no-store, no-cache, must-revalidate";
        Response.Headers["Pragma"] = "no-cache";
        Response.Headers["Expires"] = "0";

        var path = Path.Combine(_env.ContentRootPath, "wwwroot", "static", "menu.html");
        return PhysicalFile(path, "text/html");
    }
    // ── POST /CAMBIAR_PASSWORD ───────────────────────────
    [HttpPost("CAMBIAR_PASSWORD")]
    public IActionResult CambiarPassword([FromBody] CambioPasswordRequest req)
    {
        using var conn = _db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT id, password_hash FROM usuarios WHERE usuario = @usr";
        cmd.Parameters.AddWithValue("usr", req.Usuario.ToUpper());

        using var reader = cmd.ExecuteReader();
        if (!reader.Read())
            return Ok(new { ok = 0, mensaje = "Usuario no encontrado" });

        var id = reader.GetInt32(0);
        var pwdHash = reader.GetString(1);
        reader.Close();

        Console.WriteLine("PasswordNuevo: " + req.PasswordNuevo);

        if (string.IsNullOrWhiteSpace(req.PasswordNuevo) || req.PasswordNuevo.Length < 6)
            return Ok(new { ok = 0, mensaje = "La nueva contraseña debe tener al menos 6 caracteres" });

        using var upd = conn.CreateCommand();
        upd.CommandText = "UPDATE usuarios SET password_hash=@h, password_temporal=0 WHERE id=@id";
        upd.Parameters.AddWithValue("h", HashPassword(req.PasswordNuevo));
        upd.Parameters.AddWithValue("id", id);
        upd.ExecuteNonQuery();

        // Recuperar rol del usuario para incluirlo en la cookie
        using var rolCmd = conn.CreateCommand();
        rolCmd.CommandText = "SELECT rol, nombre FROM usuarios WHERE id=@id";
        rolCmd.Parameters.AddWithValue("id", id);
        string rolUsuario = "USER"; string nombreUsuario = "";
        using var rolReader = rolCmd.ExecuteReader();
        if (rolReader.Read()) { rolUsuario = rolReader.GetString(0); nombreUsuario = rolReader.GetString(1); }
        rolReader.Close();

        // Crear sesión después del cambio de contraseña
        Response.Cookies.Append("usuario", req.Usuario.ToUpper(), new CookieOptions
        {
            HttpOnly = true,
            SameSite = SameSiteMode.Lax,
            Secure = true
        });
        Response.Cookies.Append("rol", rolUsuario, new CookieOptions
        {
            HttpOnly = false,
            SameSite = SameSiteMode.Lax,
            Secure = true
        });

        return Ok(new { ok = 1, mensaje = "Contraseña actualizada correctamente", nombre = nombreUsuario, rol = rolUsuario });
    }

    // ── GET /obtener-usuario ─────────────────────────────
    [HttpGet("obtener-usuario")]
    public IActionResult ObtenerUsuario()
    {
        var usuario = Request.Cookies["usuario"];

        if (string.IsNullOrEmpty(usuario))
            return Unauthorized(); // no hay sesión

        using var conn = _db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT nombre FROM usuarios WHERE usuario = @usr";
        cmd.Parameters.AddWithValue("usr", usuario);

        var nombre = cmd.ExecuteScalar()?.ToString();

        if (string.IsNullOrEmpty(nombre))
            return Unauthorized();

        using var rolCmd2 = conn.CreateCommand();
        rolCmd2.CommandText = "SELECT rol FROM usuarios WHERE usuario=@u2";
        rolCmd2.Parameters.AddWithValue("u2", usuario);
        var rolVal = rolCmd2.ExecuteScalar()?.ToString() ?? "USER";

        return Ok(new { usuario, nombre, rol = rolVal });
    }

    // ── POST /LOGOUT ─────────────────────────────────────
    [HttpPost("LOGOUT")]
    public IActionResult Logout()
    {
        Response.Cookies.Delete("usuario");
        Response.Cookies.Delete("rol");
        return Ok(new { ok = 1 });
    }

    // ── Hash SHA-256 ─────────────────────────────────────
    private static string HashPassword(string password)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(password));
        return Convert.ToHexString(bytes).ToLower();
    }
}