using System.Data;
using Npgsql;

namespace PrecoBoi.Api.Repositories;

public abstract class BaseRepository
{
    private readonly string _connectionString;

    protected BaseRepository(IConfiguration config)
    {
        _connectionString = config.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string não configurada");
    }

    protected IDbConnection CreateConnection() => new NpgsqlConnection(_connectionString);
}
