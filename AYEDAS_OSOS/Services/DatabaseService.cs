using System.Data;
using Dapper;
using Npgsql;

namespace AYEDAS_OSOS.Services;

public class DatabaseService
{
    private readonly ILogger<DatabaseService> _logger;
    private readonly IConfiguration _configuration;
    private readonly string _connectionString;

    public DatabaseService(ILogger<DatabaseService> logger, IConfiguration configuration)
    {
        _logger = logger;
        _configuration = configuration;
        _connectionString = _configuration["ConnectionStrings:PostgreSQL"] ?? 
                           "Host=localhost;Port=5432;Database=AvrupaSarjDb;Username=postgres;Password=avrupasarj;";
    }

    public IDbConnection CreateConnection()
    {
        return new NpgsqlConnection(_connectionString);
    }

    public async Task<int> ExecuteAsync(string sql, object? param = null)
    {
        try
        {
            using var connection = CreateConnection();
            return await connection.ExecuteAsync(sql, param);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SQL sorgusu çalıştırılırken hata oluştu: {Sql}", sql);
            throw;
        }
    }

    public async Task<IEnumerable<T>> QueryAsync<T>(string sql, object? param = null)
    {
        try
        {
            using var connection = CreateConnection();
            return await connection.QueryAsync<T>(sql, param);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SQL sorgusu çalıştırılırken hata oluştu: {Sql}", sql);
            throw;
        }
    }

    public async Task<T?> QuerySingleOrDefaultAsync<T>(string sql, object? param = null)
    {
        try
        {
            using var connection = CreateConnection();
            return await connection.QuerySingleOrDefaultAsync<T>(sql, param);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SQL sorgusu çalıştırılırken hata oluştu: {Sql}", sql);
            throw;
        }
    }
} 