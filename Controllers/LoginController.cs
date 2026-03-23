using ChiIT.Data;
using ChiIT.Models;
using Microsoft.AspNetCore.Mvc;
using Npgsql;
using System.Security.Cryptography;
using System.Text;

namespace ChiIT.Controllers;

[ApiController]
public class LoginController : ControllerBase
{
    private readonly DbConnectionPool _db;

    public LoginController(DbConnectionPool db) => _db = db;

    // ── POST /LOGIN ──────────────────────────────────────
    [HttpPost("LOGIN")]
    public IActionResult Login([FromBody] LoginRequest req)
    {
        using var conn = _db.Open();
        using var cmd  = conn.CreateCommand();
        cmd.CommandText = """
            SELECT id, usuario, nombre, rol, password_hash, password_temporal, activo
            FROM public.usuarios
            WHERE usuario = @usr
            """;
        cmd.Parameters.AddWithValue("usr", req.Usuario.ToUpper());

        using var reader = cmd.ExecuteReader();
        if (!reader.Read())
            return Ok(new { ok = false, mensaje = "Usuario o contraseña incorrectos" });

        var id              = reader.GetInt32(0);
        var usuario         = reader.GetString(1);
        var nombre          = reader.GetString(2);
        var rol             = reader.GetString(3);
        var pwdHash         = reader.GetString(4);
        var pwdTemporal     = reader.GetBoolean(5);
        var activo          = reader.GetBoolean(6);
        reader.Close();

        if (!activo)
            return Ok(new { ok = false, mensaje = "Usuario desactivado" });

        if (HashPassword(req.Password) != pwdHash)
            return Ok(new { ok = false, mensaje = "Usuario o contraseña incorrectos" });

        // Actualizar último acceso
        using var upd = conn.CreateCommand();
        upd.CommandText = "UPDATE public.usuarios SET ultimo_acceso = NOW() WHERE id = @id";
        upd.Parameters.AddWithValue("id", id);
        upd.ExecuteNonQuery();

        // Cookie de sesión
        Response.Cookies.Append("usuario", usuario, new CookieOptions { HttpOnly = true });

        return Ok(new { ok = true, usuario, nombre, rol, password_temporal = pwdTemporal });
    }

    // ── POST /CAMBIAR_PASSWORD ───────────────────────────
    [HttpPost("CAMBIAR_PASSWORD")]
    public IActionResult CambiarPassword([FromBody] CambioPasswordRequest req)
    {
        using var conn = _db.Open();
        using var cmd  = conn.CreateCommand();
        cmd.CommandText = "SELECT id, password_hash FROM public.usuarios WHERE usuario = @usr";
        cmd.Parameters.AddWithValue("usr", req.Usuario.ToUpper());

        using var reader = cmd.ExecuteReader();
        if (!reader.Read())
            return Ok(new { ok = false, mensaje = "Usuario no encontrado" });

        var id      = reader.GetInt32(0);
        var pwdHash = reader.GetString(1);
        reader.Close();

        if (HashPassword(req.PasswordActual) != pwdHash)
            return Ok(new { ok = false, mensaje = "Contraseña actual incorrecta" });

        if (req.PasswordNuevo.Length < 6)
            return Ok(new { ok = false, mensaje = "La nueva contraseña debe tener al menos 6 caracteres" });

        using var upd = conn.CreateCommand();
        upd.CommandText = "UPDATE public.usuarios SET password_hash=@h, password_temporal=false WHERE id=@id";
        upd.Parameters.AddWithValue("h",  HashPassword(req.PasswordNuevo));
        upd.Parameters.AddWithValue("id", id);
        upd.ExecuteNonQuery();

        return Ok(new { ok = true, mensaje = "Contraseña actualizada correctamente" });
    }

    // ── GET /obtener-usuario ─────────────────────────────
    [HttpGet("obtener-usuario")]
    public IActionResult ObtenerUsuario()
    {
        var usuario = Request.Cookies["usuario"];
        if (string.IsNullOrEmpty(usuario))
            return Ok(new { usuario = "SISTEMA" });

        using var conn = _db.Open();
        using var cmd  = conn.CreateCommand();
        cmd.CommandText = "SELECT nombre FROM public.usuarios WHERE usuario = @usr";
        cmd.Parameters.AddWithValue("usr", usuario);

        var nombre = cmd.ExecuteScalar()?.ToString();
        return Ok(new { usuario = nombre ?? "SISTEMA" });
    }

    // ── Hash SHA-256 ─────────────────────────────────────
    private static string HashPassword(string password)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(password));
        return Convert.ToHexString(bytes).ToLower();
    }
}
