using Microsoft.AspNetCore.Mvc;
using Npgsql;
using ChiIT.Data;

[ApiController]
public class EstatusWarehouseController : ControllerBase
{
    private readonly DbConnectionPool pool;

    public EstatusWarehouseController(DbConnectionPool pool)
    {
        this.pool = pool;
    }

    [HttpGet("estatus_warehouse")]
    public IActionResult ObtenerEstatusWarehouse(string buscar = "")
    {
        using var conn = pool.Open();

        var sql = @"
        SELECT
            id,
            estatus_id,
            descripcion
        FROM estatus_warehouse
        ";

        if (!string.IsNullOrEmpty(buscar))
        {
            sql += @"
            WHERE descripcion ILIKE @buscar
            OR CAST(estatus_id AS TEXT) ILIKE @buscar
            ";
        }

        sql += " ORDER BY estatus_id";

        using var cmd = new NpgsqlCommand(sql, conn);

        if (!string.IsNullOrEmpty(buscar))
        {
            cmd.Parameters.AddWithValue("@buscar", "%" + buscar + "%");
        }

        using var r = cmd.ExecuteReader();

        var lista = new List<object>();

        while (r.Read())
        {
            lista.Add(new
            {
                id = r.GetInt32(0),
                estatus_id = r.GetInt32(1),
                descripcion = r.IsDBNull(2) ? "" : r.GetString(2)
            });
        }

        return Ok(lista);
    }
}