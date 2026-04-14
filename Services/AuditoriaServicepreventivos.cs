using ChiIT.Data;
using Npgsql;
using System.Text.Json;

namespace ChiIT.Services;

public class AuditoriaServicepreventivos
{
    private readonly DbConnectionPool _db;

    public AuditoriaServicepreventivos(DbConnectionPool db) => _db = db;

    public void Registrar(int registroId, string usuario, object anterior, object nuevo)
    {
        try
        {
            using var conn = _db.Open();
            using var cmd  = conn.CreateCommand();
            cmd.CommandText = """
                INSERT INTO public.auditoria_preventivos
                (registro_id, usuario, registro_anterior, registro_nuevo, fecha_cambio)
                VALUES (@rid, @usr, @ant, @nvo, NOW())
                """;
            cmd.Parameters.AddWithValue("rid", registroId);
            cmd.Parameters.AddWithValue("usr", usuario);
            cmd.Parameters.AddWithValue("ant", JsonSerializer.Serialize(anterior));
            cmd.Parameters.AddWithValue("nvo", JsonSerializer.Serialize(nuevo));
            cmd.ExecuteNonQuery();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Auditoría] Error: {ex.Message}");
        }
    }
}
