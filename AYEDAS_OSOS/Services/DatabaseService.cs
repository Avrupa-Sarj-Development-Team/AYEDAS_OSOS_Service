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

    /// <summary>
    /// Bağlantı string'ini döndürür (güvenlik için şifre maskelenir)
    /// </summary>
    public string GetConnectionString()
    {
        // Bağlantı string'inden şifreyi maskeleyerek döndür
        string maskedConnectionString = _connectionString;
        if (!string.IsNullOrEmpty(maskedConnectionString))
        {
            // "Password=xxx;" formatını bul ve maskele
            var regex = new System.Text.RegularExpressions.Regex(@"Password=([^;]*);");
            maskedConnectionString = regex.Replace(maskedConnectionString, "Password=********;");
        }
        return maskedConnectionString;
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
    
    /// <summary>
    /// Veritabanında MeterOsosConsumption tablosunun var olduğunu kontrol eder, yoksa oluşturur
    /// </summary>
    public async Task EnsureMeterOsosConsumptionTableExistsAsync()
    {
        try
        {
            _logger.LogInformation("MeterOsosConsumption tablosunun var olup olmadığı kontrol ediliyor...");
            
            // Tablo var mı kontrol et
            string checkTableSql = @"
                SELECT EXISTS (
                    SELECT FROM pg_tables
                    WHERE schemaname = 'public'
                    AND tablename = 'MeterOsosConsumption'
                );
            ";
            
            bool tableExists;
            using (var connection = CreateConnection())
            {
                tableExists = await connection.QuerySingleAsync<bool>(checkTableSql);
            }
            
            if (tableExists)
            {
                _logger.LogInformation("MeterOsosConsumption tablosu zaten mevcut.");
                return;
            }
            
            _logger.LogWarning("MeterOsosConsumption tablosu bulunamadı, oluşturuluyor...");
            
            // Tablo yoksa oluştur
            string createTableSql = @"
                CREATE TABLE ""MeterOsosConsumption"" (
                    ""id"" SERIAL PRIMARY KEY,
                    ""period"" VARCHAR(10),
                    ""etso"" VARCHAR(50),
                    ""tesisatno"" VARCHAR(50),
                    ""meterid"" INTEGER,
                    ""distributioncompany"" VARCHAR(50) DEFAULT 'AYEDAS',
                    ""year"" INTEGER NOT NULL,
                    ""month"" INTEGER NOT NULL,
                    ""day"" INTEGER NOT NULL,
                    ""hour"" INTEGER NOT NULL,
                    ""datetime"" TIMESTAMP NOT NULL,
                    ""value"" NUMERIC(18, 2) NOT NULL,
                    ""createdat"" TIMESTAMP DEFAULT CURRENT_TIMESTAMP
                );
                
                -- İndeksler
                CREATE INDEX idx_meterososconsumption_datetime ON ""MeterOsosConsumption"" (""datetime"");
                CREATE INDEX idx_meterososconsumption_etso ON ""MeterOsosConsumption"" (""etso"");
                CREATE INDEX idx_meterososconsumption_tesisatno ON ""MeterOsosConsumption"" (""tesisatno"");
                CREATE INDEX idx_meterososconsumption_year_month ON ""MeterOsosConsumption"" (""year"", ""month"");
            ";
            
            using (var connection = CreateConnection())
            {
                await connection.ExecuteAsync(createTableSql);
            }
            
            _logger.LogInformation("MeterOsosConsumption tablosu başarıyla oluşturuldu.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "MeterOsosConsumption tablosu oluşturulurken hata oluştu");
            throw;
        }
    }
} 