using Microsoft.Data.SqlClient;

namespace ChiIT.Data;

/// <summary>
/// Pool de conexiones a SQL Server — migrado desde PostgreSQL/Npgsql
/// </summary>
public class DbConnectionPool
{
    private readonly string _connStr;

    public DbConnectionPool(IConfiguration config)
    {
        _connStr = config.GetConnectionString("SqlServer")
                     ?? throw new InvalidOperationException("Falta ConnectionStrings:SqlServer en appsettings.json");
    }

    /// <summary>
    /// Abre una conexión async. El caller debe hacer await using con ella.
    /// </summary>
    public async Task<SqlConnection> OpenAsync()
    {
        var conn = new SqlConnection(_connStr);
        await conn.OpenAsync();
        return conn;
    }

    /// <summary>
    /// Versión síncrona para compatibilidad con controladores no-async.
    /// </summary>
    public SqlConnection Open()
    {
        var conn = new SqlConnection(_connStr);
        conn.Open();
        return conn;
    }
}