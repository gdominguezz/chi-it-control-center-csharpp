using ChiIT.Data;
using System.Text.Json;

namespace ChiIT.Services;

/// <summary>
/// Extensión de AuditoriaService para registrar cambios en la tabla
/// auditoria_correctivos, siguiendo el mismo patrón que Registrar()
/// usa para auditoria_preventivos.
/// 
/// OPCIÓN A (recomendada): agregar este método directamente dentro de
/// la clase AuditoriaService existente en AuditoriaService.cs.
/// 
/// OPCIÓN B: Si AuditoriaService es una clase partial, este archivo
/// puede coexistir. De lo contrario, fusiona el método manualmente.
/// </summary>
public partial class AuditoriaService
{
    /// <summary>
    /// Registra un snapshot anterior/nuevo en public.auditoria_correctivos.
    /// Firma idéntica a Registrar(), pero apunta a la tabla de correctivos.
    /// </summary>
    public void RegistrarCorrectivo(
        int registroId,
        string usuario,
        Dictionary<string, object?> anterior,
        Dictionary<string, object?> nuevo)
    {
        try
        {
            using var conn = _db.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = """
                INSERT INTO public.auditoria_correctivos
                    (registro_id, usuario, registro_anterior, registro_nuevo)
                VALUES
                    (@rid, @usr, @ant::jsonb, @nue::jsonb)
                """;
            cmd.Parameters.AddWithValue("rid", registroId);
            cmd.Parameters.AddWithValue("usr", (object?)usuario ?? DBNull.Value);
            cmd.Parameters.AddWithValue("ant", JsonSerializer.Serialize(anterior,
                new JsonSerializerOptions { WriteIndented = false }));
            cmd.Parameters.AddWithValue("nue", JsonSerializer.Serialize(nuevo,
                new JsonSerializerOptions { WriteIndented = false }));
            cmd.ExecuteNonQuery();
        }
        catch (Exception ex)
        {
            // No interrumpir la respuesta HTTP si la auditoría falla
            Console.WriteLine($"[AuditoriaService.RegistrarCorrectivo] Error: {ex.Message}");
        }
    }
}