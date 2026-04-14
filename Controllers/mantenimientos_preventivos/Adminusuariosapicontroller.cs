using ChiIT.Data;
using Microsoft.AspNetCore.Mvc;
using Npgsql;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace ChiIT.Controllers;

[ApiController]
public class AdminUsuariosApiController : ControllerBase
{
    private readonly DbConnectionPool _db;
    public AdminUsuariosApiController(DbConnectionPool db) => _db = db;

    // ── Helpers ───────────────────────────────────────────────────────────
    private string? ObtenerRol()
    {
        var usr = Request.Cookies["usuario"]
               ?? Request.Headers["X-Usuario"].FirstOrDefault()
               ?? "";
        if (string.IsNullOrWhiteSpace(usr)) return null;
        using var conn = _db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT rol FROM public.usuarios WHERE usuario=@u AND activo=true";
        cmd.Parameters.AddWithValue("u", usr.ToUpper());
        return cmd.ExecuteScalar()?.ToString();
    }

    private bool EsAdmin() => ObtenerRol() == "ADMIN";
    private bool EsAdminOAuditor() { var r = ObtenerRol(); return r == "ADMIN" || r == "AUDITOR"; }

    private static string HashPassword(string password)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(password));
        return Convert.ToHexString(bytes).ToLower();
    }

    // ── GET /admin/usuarios/api ───────────────────────────────────────────
    [HttpGet("admin/usuarios/api")]
    public IActionResult ObtenerTodos()
    {
        if (!EsAdminOAuditor()) return Ok(new { error = "No autorizado" });

        using var conn = _db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT id, usuario, nombre, rol,
                   password_temporal, activo,
                   creado_en, ultimo_acceso
            FROM public.usuarios
            ORDER BY activo DESC, rol, nombre
            """;

        var lista = new List<Dictionary<string, object?>>();
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            lista.Add(new Dictionary<string, object?>
            {
                ["id"] = r.GetInt32(0),
                ["usuario"] = r.GetString(1),
                ["nombre"] = r.GetString(2),
                ["rol"] = r.GetString(3),
                ["password_temporal"] = !r.IsDBNull(4) && r.GetBoolean(4),
                ["activo"] = !r.IsDBNull(5) && r.GetBoolean(5),
                ["creado_en"] = r.IsDBNull(6) ? null : r.GetDateTime(6).ToString("o"),
                ["ultimo_acceso"] = r.IsDBNull(7) ? null : r.GetDateTime(7).ToString("o"),
            });
        }

        return Ok(new { usuarios = lista });
    }

    // ── GET /admin/usuarios/api/{id} ──────────────────────────────────────
    [HttpGet("admin/usuarios/api/{id:int}")]
    public IActionResult ObtenerUno(int id)
    {
        if (!EsAdminOAuditor()) return Ok(new { error = "No autorizado" });

        using var conn = _db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT id, usuario, nombre, rol,
                   password_temporal, activo,
                   creado_en, ultimo_acceso
            FROM public.usuarios WHERE id=@id
            """;
        cmd.Parameters.AddWithValue("id", id);

        using var r = cmd.ExecuteReader();
        if (!r.Read()) return Ok(new { error = "Usuario no encontrado" });

        return Ok(new
        {
            id = r.GetInt32(0),
            usuario = r.GetString(1),
            nombre = r.GetString(2),
            rol = r.GetString(3),
            password_temporal = !r.IsDBNull(4) && r.GetBoolean(4),
            activo = !r.IsDBNull(5) && r.GetBoolean(5),
            creado_en = r.IsDBNull(6) ? null : r.GetDateTime(6).ToString("o"),
            ultimo_acceso = r.IsDBNull(7) ? null : r.GetDateTime(7).ToString("o"),
        });
    }

    // ── POST /admin/usuarios/api ──────────────────────────────────────────
    [HttpPost("admin/usuarios/api")]
    public IActionResult Crear([FromBody] UsuarioRequest data)
    {
        if (!EsAdmin()) return Ok(new { ok = false, error = "No autorizado" });

        if (string.IsNullOrWhiteSpace(data.usuario))
            return Ok(new { ok = false, error = "El campo usuario es requerido" });
        if (string.IsNullOrWhiteSpace(data.nombre))
            return Ok(new { ok = false, error = "El campo nombre es requerido" });
        if (string.IsNullOrWhiteSpace(data.password))
            return Ok(new { ok = false, error = "La contraseña es requerida" });
        if (data.password.Length < 6)
            return Ok(new { ok = false, error = "La contraseña debe tener al menos 6 caracteres" });

        var usuarioUpper = data.usuario.Trim().ToUpper();

        using var conn = _db.Open();

        // Verificar que no exista
        using var chk = conn.CreateCommand();
        chk.CommandText = "SELECT COUNT(*) FROM public.usuarios WHERE usuario=@u";
        chk.Parameters.AddWithValue("u", usuarioUpper);
        if (Convert.ToInt64(chk.ExecuteScalar()!) > 0)
            return Ok(new { ok = false, error = "El usuario ya existe" });

        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO public.usuarios
            (usuario, nombre, password_hash, rol, password_temporal, activo)
            VALUES (@u, @n, @ph, @r, @pt, @a)
            RETURNING id
            """;
        cmd.Parameters.AddWithValue("u", usuarioUpper);
        cmd.Parameters.AddWithValue("n", data.nombre.Trim());
        cmd.Parameters.AddWithValue("ph", HashPassword(data.password));
        cmd.Parameters.AddWithValue("r", (data.rol ?? "USER").ToUpper());
        cmd.Parameters.AddWithValue("pt", data.password_temporal ?? true);
        cmd.Parameters.AddWithValue("a", data.activo ?? true);

        var newId = cmd.ExecuteScalar();
        return Ok(new { ok = true, id = newId });
    }

    // ── PUT /admin/usuarios/api/{id} ──────────────────────────────────────
    [HttpPut("admin/usuarios/api/{id:int}")]
    public IActionResult Editar(int id, [FromBody] UsuarioRequest data)
    {
        if (!EsAdmin()) return Ok(new { ok = false, error = "No autorizado" });

        if (string.IsNullOrWhiteSpace(data.nombre))
            return Ok(new { ok = false, error = "El nombre es requerido" });
        if (!string.IsNullOrWhiteSpace(data.password) && data.password.Length < 6)
            return Ok(new { ok = false, error = "La contraseña debe tener al menos 6 caracteres" });

        using var conn = _db.Open();

        // Si viene nueva contraseña, actualizarla también
        if (!string.IsNullOrWhiteSpace(data.password))
        {
            using var updFull = conn.CreateCommand();
            updFull.CommandText = """
                UPDATE public.usuarios SET
                    nombre=@n, rol=@r, activo=@a,
                    password_temporal=@pt, password_hash=@ph
                WHERE id=@id
                """;
            updFull.Parameters.AddWithValue("n", data.nombre.Trim());
            updFull.Parameters.AddWithValue("r", (data.rol ?? "USER").ToUpper());
            updFull.Parameters.AddWithValue("a", data.activo ?? true);
            updFull.Parameters.AddWithValue("pt", data.password_temporal ?? false);
            updFull.Parameters.AddWithValue("ph", HashPassword(data.password));
            updFull.Parameters.AddWithValue("id", id);
            updFull.ExecuteNonQuery();
        }
        else
        {
            using var upd = conn.CreateCommand();
            upd.CommandText = """
                UPDATE public.usuarios SET
                    nombre=@n, rol=@r, activo=@a, password_temporal=@pt
                WHERE id=@id
                """;
            upd.Parameters.AddWithValue("n", data.nombre.Trim());
            upd.Parameters.AddWithValue("r", (data.rol ?? "USER").ToUpper());
            upd.Parameters.AddWithValue("a", data.activo ?? true);
            upd.Parameters.AddWithValue("pt", data.password_temporal ?? false);
            upd.Parameters.AddWithValue("id", id);
            upd.ExecuteNonQuery();
        }

        return Ok(new { ok = true });
    }

    // ── PATCH /admin/usuarios/api/{id}/estado ─────────────────────────────
    [HttpPatch("admin/usuarios/api/{id:int}/estado")]
    public IActionResult CambiarEstado(int id, [FromBody] EstadoRequest data)
    {
        if (!EsAdmin()) return Ok(new { ok = false, error = "No autorizado" });

        using var conn = _db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "UPDATE public.usuarios SET activo=@a WHERE id=@id";
        cmd.Parameters.AddWithValue("a", data.activo);
        cmd.Parameters.AddWithValue("id", id);
        cmd.ExecuteNonQuery();

        return Ok(new { ok = true });
    }

    // ── PATCH /admin/usuarios/api/{id}/reset-password ─────────────────────
    [HttpPatch("admin/usuarios/api/{id:int}/reset-password")]
    public IActionResult ResetPassword(int id, [FromBody] ResetPasswordRequest data)
    {
        if (!EsAdmin()) return Ok(new { ok = false, error = "No autorizado" });

        if (string.IsNullOrWhiteSpace(data.password))
            return Ok(new { ok = false, error = "La contraseña es requerida" });
        if (data.password.Length < 6)
            return Ok(new { ok = false, error = "Mínimo 6 caracteres" });

        using var conn = _db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            UPDATE public.usuarios
            SET password_hash=@ph, password_temporal=true
            WHERE id=@id
            """;
        cmd.Parameters.AddWithValue("ph", HashPassword(data.password));
        cmd.Parameters.AddWithValue("id", id);
        cmd.ExecuteNonQuery();

        return Ok(new { ok = true });
    }
}

// ── Request models ────────────────────────────────────────────────────────
public class UsuarioRequest
{
    public string? usuario { get; set; }
    public string? nombre { get; set; }
    public string? password { get; set; }
    public string? rol { get; set; }
    public bool? activo { get; set; }
    public bool? password_temporal { get; set; }
}

public class EstadoRequest
{
    public bool activo { get; set; }
}

public class ResetPasswordRequest
{
    public string? password { get; set; }
}