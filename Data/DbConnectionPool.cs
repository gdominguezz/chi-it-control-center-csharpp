using Npgsql;

namespace ChiIT.Data;

/// <summary>
/// Pool de conexiones a PostgreSQL — equivalente a database.py
/// </summary>
public class DbConnectionPool
{
    private readonly NpgsqlDataSource _dataSource;

    public DbConnectionPool(IConfiguration config)
    {
        var connStr = config.GetConnectionString("Postgres")
                     ?? throw new InvalidOperationException("Falta ConnectionStrings:Postgres en appsettings.json");

        _dataSource = NpgsqlDataSource.Create(connStr);
    }

    /// <summary>
    /// Abre una conexión del pool. El caller debe hacer await using con ella.
    /// </summary>
    public async Task<NpgsqlConnection> OpenAsync()
        => await _dataSource.OpenConnectionAsync();

    /// <summary>
    /// Versión síncrona para compatibilidad con controladores no-async.
    /// </summary>
    public NpgsqlConnection Open()
        => _dataSource.OpenConnection();
}
