using AYEDAS_OSOS.Controllers;

namespace AYEDAS_OSOS.Services;

public class ConsumptionDataImportService : BackgroundService
{
    private readonly ILogger<ConsumptionDataImportService> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly IConfiguration _configuration;
    private readonly int _refreshIntervalHours;

    public ConsumptionDataImportService(
        ILogger<ConsumptionDataImportService> logger,
        IServiceProvider serviceProvider,
        IConfiguration configuration)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
        _configuration = configuration;
        _refreshIntervalHours = _configuration.GetValue<int>("DataImportSettings:RefreshIntervalHours", 24);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Tüketim verisi otomatik aktarma servisi başlatıldı");

        // Konfigürasyondan otomatik aktarma etkin mi kontrol et
        bool autoImportEnabled = _configuration.GetValue<bool>("DataImportSettings:AutoImportEnabled", false);
        
        if (!autoImportEnabled)
        {
            _logger.LogInformation("Otomatik veri aktarma devre dışı. Servis pasif durumda.");
            return;
        }
        
        // İlk çalıştırma için bekle
        int initialDelayMinutes = _configuration.GetValue<int>("DataImportSettings:InitialDelayMinutes", 5);
        await Task.Delay(TimeSpan.FromMinutes(initialDelayMinutes), stoppingToken);

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogInformation("Planlanan tüketim verisi aktarma işlemi başlatılıyor...");

                try
                {
                    using (var scope = _serviceProvider.CreateScope())
                    {
                        var consumptionController = scope.ServiceProvider.GetRequiredService<ConsumptionController>();
                        // AYEDAS verilerini çek
                        await consumptionController.ImportAyedasConsumptionData();
                    }

                    _logger.LogInformation("Planlanan tüketim verisi aktarma işlemi tamamlandı");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Planlanan tüketim verisi aktarma işleminde hata oluştu");
                }

                // Sonraki çalıştırma için bekle
                await Task.Delay(TimeSpan.FromHours(_refreshIntervalHours), stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Tüketim verisi otomatik aktarma servisi durduruldu");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Tüketim verisi otomatik aktarma servisinde beklenmeyen hata");
            throw;
        }
    }
} 