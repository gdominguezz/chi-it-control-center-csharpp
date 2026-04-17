using Microsoft.AspNetCore.Mvc;
using Npgsql;
using System;
using System.Collections.Generic;

[ApiController]
public class EstatusWarehouseController : ControllerBase
{
    private readonly NpgsqlDataSource pool;

    public EstatusWarehouseController(NpgsqlDataSource pool)
    {
        this.pool = pool;
    }

    [HttpGet("estatus_warehouse")]
    public IActionResult ObtenerEstatusWarehouse(string buscar = "")
    {
        try
        {
            using var conn = pool.OpenConnection();

            string sql = @"
                SELECT
                    id,
                    estatus_id,
                    descripcion
                FROM estatus_warehouse
            ";

            if (!string.IsNullOrEmpty(buscar))
            {
                sql += @"
                WHERE
                    descripcion ILIKE @texto
                    OR CAST(estatus_id AS TEXT) ILIKE @texto
                ";
            }

            sql += " ORDER BY estatus_id";

            using var cmd = new NpgsqlCommand(sql, conn);

            if (!string.IsNullOrEmpty(buscar))
            {
                cmd.Parameters.AddWithValue("@texto", "%" + buscar + "%");
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
        catch (Exception ex)
        {
            return StatusCode(500, ex.Message);
        }
    }
}