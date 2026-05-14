using ChiIT.Data;
using Microsoft.Data.SqlClient;
using System.Text.Json;

namespace ChiIT.Services;

public class AuditoriaServiceCorrectivos
{
    private readonly DbConnectionPool _db;

    public AuditoriaServiceCorrectivos(DbConnectionPool db) => _db = db;

    public void RegistrarCorrectivo(
        long registroId,
        string usuario,
        object anterior,
        object nuevo)
    {
        try
        {
            using var conn = _db.Open();
            using var cmd = new SqlCommand(@"
                INSERT INTO auditoria_correctivos
                    (registro_id, usuario, registro_anterior, registro_nuevo, fecha_cambio)
                VALUES
                    (@rid, @usr, @ant, @nue, GETDATE())", conn);

            cmd.Parameters.AddWithValue("@rid", registroId);
            cmd.Parameters.AddWithValue("@usr", (object?)usuario ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@ant", JsonSerializer.Serialize(anterior,
                new JsonSerializerOptions { WriteIndented = false }));
            cmd.Parameters.AddWithValue("@nue", JsonSerializer.Serialize(nuevo,
                new JsonSerializerOptions { WriteIndented = false }));
            cmd.ExecuteNonQuery();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[AuditoriaServiceCorrectivos] Error: {ex.Message}");
        }
    }
}