using System.Globalization;
using System.Net.Http.Headers;
using System.Text;
using AYEDAS_OSOS.Models;
using AYEDAS_OSOS.Services;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;

namespace AYEDAS_OSOS.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ConsumptionController : ControllerBase
{
    private readonly ILogger<ConsumptionController> _logger;
    private readonly ApiService _apiService;
    private readonly TokenService _tokenService;
    private readonly DatabaseService _databaseService;
    private readonly IConfiguration _configuration;
    private readonly HttpClient _httpClient;

    public ConsumptionController(
        ILogger<ConsumptionController> logger,
        ApiService apiService,
        TokenService tokenService,
        DatabaseService databaseService,
        IConfiguration configuration)
    {
        _logger = logger;
        _apiService = apiService;
        _tokenService = tokenService;
        _databaseService = databaseService;
        _configuration = configuration;
        _httpClient = new HttpClient();
    }

    [HttpGet("ImportAyedasConsumptionData")]
    public async Task<IActionResult> ImportAyedasConsumptionData()
    {
        try
        {
            _logger.LogInformation("AYEDAS OSOS tüketim verisi aktarma işlemi başlatıldı");
            
            // İlk token al
            var accessToken = await GetValidAccessToken();
            if (string.IsNullOrEmpty(accessToken))
            {
                return BadRequest(new { message = "Token alınamadı" });
            }
            
            // Tesisat listesini al
            var installationList = await GetInstallationList(accessToken);
            if (installationList == null || installationList.InstallationList == null || !installationList.InstallationList.Any())
            {
                return NotFound(new { message = "Tesisat listesi alınamadı veya boş" });
            }
            
            _logger.LogInformation($"Toplam {installationList.InstallationList.Count} adet tesisat bulundu");
            
            // Tarih aralığını belirle
            var startDate = DateTime.Now.AddMonths(-24);
            var monthYearPairs = GenerateMonthYearPairs(startDate, 24);
            
            int totalSuccess = 0;
            int totalError = 0;
            var errorList = new List<string>();
            
            // Her tesisat için döngü
            foreach (var installation in installationList.InstallationList)
            {
                if (string.IsNullOrEmpty(installation.InstallationNumber) || string.IsNullOrEmpty(installation.Etso))
                {
                    _logger.LogWarning($"Tesisat numarası veya ETSO kodu eksik: {installation.CustomerName}");
                    continue;
                }
                
                string tesisatNo = installation.InstallationNumber;
                string etso = installation.Etso;
                
                _logger.LogInformation($"Tesisat işleniyor: {tesisatNo}, ETSO: {etso}, Müşteri: {installation.CustomerName}");
                
                // Her ay için döngü
                foreach (var (month, year) in monthYearPairs)
                {
                    try
                    {
                        // Her istek için yeni token al
                        accessToken = await GetValidAccessToken();
                        if (string.IsNullOrEmpty(accessToken))
                        {
                            _logger.LogError("API isteği için geçerli token alınamadı");
                            errorList.Add($"Tesisat: {tesisatNo}, Ay: {month}/{year}, Hata: Token alınamadı");
                            totalError++;
                            continue;
                        }
                        
                        string meterMonth = $"{year}-{month:D2}";
                        string fromDate = $"{year}-{month:D2}-01 00:00:00";
                        
                        // Saatlik tüketim verilerini al
                        var meterData = await GetHourlyMeterData(accessToken, meterMonth, tesisatNo, fromDate);
                        
                        if (meterData == null || meterData.Items == null || !meterData.Items.ContainsKey(tesisatNo) || meterData.Items[tesisatNo] == null || !meterData.Items[tesisatNo].Any())
                        {
                            _logger.LogWarning($"Tesisat için veri bulunamadı: {tesisatNo}, Ay: {meterMonth}");
                            continue;
                        }
                        
                        _logger.LogInformation($"Tesisat için {meterData.Items[tesisatNo].Count} adet veri bulundu: {tesisatNo}, Ay: {meterMonth}");
                        
                        // Verileri veritabanına kaydet
                        await SaveHourlyConsumptionDataToDatabase(meterData.Items[tesisatNo], tesisatNo, month, year);
                        totalSuccess++;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, $"Tesisat için veri alınırken hata oluştu: {tesisatNo}, Ay: {month}/{year}");
                        errorList.Add($"Tesisat: {tesisatNo}, Ay: {month}/{year}, Hata: {ex.Message}");
                        totalError++;
                    }
                    
                    // API rate limiting için bekleme
                    await Task.Delay(500);
                }
            }
            
            return Ok(new
            {
                message = $"Veri aktarımı tamamlandı: {totalSuccess} başarılı, {totalError} başarısız",
                totalSuccess,
                totalError,
                errors = errorList
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AYEDAS OSOS tüketim verisi aktarma işleminde hata");
            return StatusCode(500, new { message = $"İşlem sırasında hata: {ex.Message}" });
        }
    }
    
    private async Task<string> GetValidAccessToken()
    {
        try
        {
            var tokenData = await _tokenService.GetValidTokenAsync(false);
            
            if (tokenData == null || string.IsNullOrEmpty(tokenData.AccessToken))
            {
                _logger.LogWarning("Access token alınamadı, yenileniyor");
                tokenData = await _tokenService.GetValidTokenAsync(true);
                
                if (tokenData == null || string.IsNullOrEmpty(tokenData.AccessToken))
                {
                    _logger.LogError("Access token yenileme başarısız");
                    return string.Empty;
                }
            }
            
            return tokenData.AccessToken;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Access token alınırken hata");
            return string.Empty;
        }
    }
    
    private async Task<InstallationListResponse?> GetInstallationList(string accessToken)
    {
        try
        {
            // Access token geçerli mi kontrol et
            if (string.IsNullOrEmpty(accessToken))
            {
                _logger.LogWarning("GetInstallationList: Geçersiz token, yeni token alınıyor");
                accessToken = await GetValidAccessToken();
                
                if (string.IsNullOrEmpty(accessToken))
                {
                    _logger.LogError("GetInstallationList: Yeni token alınamadı");
                    return null;
                }
            }

            string apiUrl = $"https://mdmsaatlik.ayedas.com.tr/ayedas/mdm-api/customer/installation-list?access_token={accessToken}";
            _logger.LogInformation($"Tesisat listesi isteği yapılıyor: {apiUrl}");
            
            var request = new HttpRequestMessage(HttpMethod.Get, apiUrl);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            
            var response = await _httpClient.SendAsync(request);
            var responseContent = await response.Content.ReadAsStringAsync();
            
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError($"Tesisat listesi alınamadı. HTTP Kodu: {response.StatusCode}, Yanıt: {responseContent}");
                
                // Token hatasıysa
                if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                {
                    _logger.LogWarning("Unauthorized hatası alındı, token yenileniyor");
                    accessToken = await GetValidAccessToken();
                    
                    if (!string.IsNullOrEmpty(accessToken))
                    {
                        // Yeni token ile tekrar dene
                        return await GetInstallationList(accessToken);
                    }
                }
                
                return null;
            }
            
            _logger.LogInformation($"Tesisat listesi başarıyla alındı: {responseContent.Substring(0, Math.Min(200, responseContent.Length))}...");
            return JsonConvert.DeserializeObject<InstallationListResponse>(responseContent);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Tesisat listesi alınırken hata");
            return null;
        }
    }
    
    private async Task<AyedasMeterResponse?> GetHourlyMeterData(string accessToken, string meterMonth, string installationNumber, string fromDate)
    {
        try
        {
            // Access token geçerli mi kontrol et
            if (string.IsNullOrEmpty(accessToken))
            {
                _logger.LogWarning("GetHourlyMeterData: Geçersiz token, yeni token alınıyor");
                accessToken = await GetValidAccessToken();
                
                if (string.IsNullOrEmpty(accessToken))
                {
                    _logger.LogError("GetHourlyMeterData: Yeni token alınamadı");
                    return null;
                }
            }

            string apiUrl = $"https://mdmsaatlik.ayedas.com.tr/ayedas/mdm-api/customer/hourly-meter-information-multi-installation?access_token={accessToken}&meterMonth={Uri.EscapeDataString(meterMonth)}&installationNumbers={Uri.EscapeDataString(installationNumber)}&fromDate={Uri.EscapeDataString(fromDate)}";
            _logger.LogInformation($"Saatlik tüketim verisi isteği yapılıyor: {apiUrl}");
            
            var request = new HttpRequestMessage(HttpMethod.Get, apiUrl);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            
            var response = await _httpClient.SendAsync(request);
            var responseContent = await response.Content.ReadAsStringAsync();
            
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError($"Saatlik tüketim verisi alınamadı. HTTP Kodu: {response.StatusCode}, Yanıt: {responseContent}");
                
                // Token hatasıysa
                if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                {
                    _logger.LogWarning("Unauthorized hatası alındı, istek yeniden denenecek");
                    return null;
                }
                
                return null;
            }
            
            _logger.LogInformation($"Saatlik tüketim verisi başarıyla alındı: {responseContent.Substring(0, Math.Min(500, responseContent.Length))}...");
            
            var result = JsonConvert.DeserializeObject<AyedasMeterResponse>(responseContent);
            
            if (result == null)
            {
                _logger.LogWarning($"API yanıtı AyedasMeterResponse'a dönüştürülemedi: {responseContent}");
                return null;
            }
            
            if (result.Items == null || !result.Items.ContainsKey(installationNumber) || result.Items[installationNumber] == null || !result.Items[installationNumber].Any())
            {
                _logger.LogWarning($"Tesisat {installationNumber} için {meterMonth} ayında veri bulunamadı. API Yanıtı: Message={result.Message}");
                return null;
            }
            
            var meterData = result.Items[installationNumber];
            if (meterData == null || !meterData.Any() || meterData[0].ValueList == null || !meterData[0].ValueList.Any())
            {
                _logger.LogWarning($"Tesisat {installationNumber} için {meterMonth} ayında ölçüm değeri bulunamadı.");
                return null;
            }
            
            _logger.LogInformation($"Toplam {meterData[0].ValueList.Count} adet ölçüm değeri alındı. Tesisat: {installationNumber}, Ay: {meterMonth}");
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Saatlik tüketim verisi alınırken hata: Tesisat: {installationNumber}, Ay: {meterMonth}");
            return null;
        }
    }
    
    private async Task SaveHourlyConsumptionDataToDatabase(List<MeterData> dataList, string tesisatNo, int month, int year)
    {
        if (dataList == null || !dataList.Any() || dataList[0].ValueList == null || !dataList[0].ValueList.Any())
        {
            _logger.LogWarning("Kayıt için veri yok");
            return;
        }
        
        var valueList = dataList[0].ValueList;
        _logger.LogInformation($"Veritabanına {valueList.Count} adet veri kaydedilecek. Tesisat: {tesisatNo}, Ay: {month}/{year}");
        
        string etsoKodu = ""; // Bu bilgiyi API yanıtından alamıyoruz, ayrıca öğrenmek gerekebilir
        
        // Tesisat numarasına göre ETSO kodunu veritabanından al
        try
        {
            string etsoQuery = @"SELECT ""Etso"" FROM ""MeterOsosConsumption"" WHERE ""TesisatNo"" = @TesisatNo LIMIT 1";
            etsoKodu = await _databaseService.QuerySingleOrDefaultAsync<string>(etsoQuery, new { TesisatNo = tesisatNo }) ?? "";
            
            // Veritabanında bu tesisata ait veri yoksa, InstallationList'ten almaya çalış
            if (string.IsNullOrEmpty(etsoKodu))
            {
                // Token al
                var accessToken = await GetValidAccessToken();
                if (!string.IsNullOrEmpty(accessToken))
                {
                    var installationList = await GetInstallationList(accessToken);
                    if (installationList?.InstallationList != null)
                    {
                        var installation = installationList.InstallationList.FirstOrDefault(i => i.InstallationNumber == tesisatNo);
                        if (installation != null && !string.IsNullOrEmpty(installation.Etso))
                        {
                            etsoKodu = installation.Etso;
                            _logger.LogInformation($"ETSO kodu tesisat listesinden alındı: {etsoKodu}");
                        }
                    }
                }
            }
            else
            {
                _logger.LogInformation($"ETSO kodu veritabanından alındı: {etsoKodu}");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ETSO kodu alınırken hata oluştu");
        }
        
        if (string.IsNullOrEmpty(etsoKodu))
        {
            _logger.LogWarning($"Tesisat {tesisatNo} için ETSO kodu bulunamadı, varsayılan olarak tesisat numarası kullanılacak");
            etsoKodu = tesisatNo;
        }
        
        int successCount = 0;
        int errorCount = 0;
        
        foreach (var value in valueList)
        {
            try
            {
                // MeterDate'i parse et
                if (!TryParseDateTime(value.MeterDate, out DateTime dateTime))
                {
                    _logger.LogWarning($"Geçersiz tarih formatı: {value.MeterDate}");
                    errorCount++;
                    continue;
                }
                
                _logger.LogDebug($"MeterDate başarıyla parse edildi: {value.MeterDate} -> {dateTime:yyyy-MM-dd HH:mm:ss}");
                
                // Consumption değeri
                decimal consumptionValue = Convert.ToDecimal(value.ActiveConsumption);
                
                _logger.LogDebug($"ActiveConsumption değeri: {value.ActiveConsumption} -> {consumptionValue}");
                
                // Parametreleri oluştur
                var parameters = new
                {
                    Period = $"{year}-{month:D2}", 
                    Etso = etsoKodu, 
                    MeterId = (int?)null,
                    TesisatNo = tesisatNo,
                    DistributionCompany = "AYEDAS",
                    Year = dateTime.Year,
                    Month = dateTime.Month,
                    Day = dateTime.Day,
                    Hour = dateTime.Hour,
                    DateTime = dateTime,
                    Value = consumptionValue,
                    CreatedAt = DateTime.UtcNow
                };
                
                try
                {
                    // Veritabanında var mı kontrol et
                    string checkSql = @"
                        SELECT COUNT(*) FROM ""MeterOsosConsumption"" 
                        WHERE ""Etso"" = @Etso 
                        AND ""Year"" = @Year 
                        AND ""Month"" = @Month 
                        AND ""Day"" = @Day 
                        AND ""Hour"" = @Hour
                    ";
                    
                    int exists = await _databaseService.QuerySingleOrDefaultAsync<int>(checkSql, new { 
                        Etso = etsoKodu, 
                        Year = dateTime.Year, 
                        Month = dateTime.Month, 
                        Day = dateTime.Day, 
                        Hour = dateTime.Hour
                    });
                    
                    _logger.LogDebug($"Veritabanı sorgusu yapıldı. Sonuç: {exists}");
                    
                    if (exists > 0)
                    {
                        // Veri varsa güncelle
                        string updateSql = @"
                            UPDATE ""MeterOsosConsumption"" 
                            SET ""Value"" = @Value, 
                                ""DateTime"" = @DateTime,
                                ""CreatedAt"" = @CreatedAt,
                                ""TesisatNo"" = @TesisatNo
                            WHERE ""Etso"" = @Etso 
                            AND ""Year"" = @Year 
                            AND ""Month"" = @Month 
                            AND ""Day"" = @Day 
                            AND ""Hour"" = @Hour
                        ";
                        
                        int rowsAffected = await _databaseService.ExecuteAsync(updateSql, parameters);
                        _logger.LogInformation($"Tüketim verisi güncellendi: {etsoKodu}, Tarih: {dateTime:yyyy-MM-dd HH:mm}, Değer: {consumptionValue}, Etkilenen Satır: {rowsAffected}");
                        successCount++;
                    }
                    else
                    {
                        // Veri yoksa ekle
                        string insertSql = @"
                            INSERT INTO ""MeterOsosConsumption"" (
                                ""Period"", ""Etso"", ""MeterId"", ""TesisatNo"", ""DistributionCompany"", 
                                ""Year"", ""Month"", ""Day"", ""Hour"", ""DateTime"", ""Value"", ""CreatedAt""
                            ) VALUES (
                                @Period, @Etso, @MeterId, @TesisatNo, @DistributionCompany,
                                @Year, @Month, @Day, @Hour, @DateTime, @Value, @CreatedAt
                            )
                        ";
                        
                        int rowsAffected = await _databaseService.ExecuteAsync(insertSql, parameters);
                        _logger.LogInformation($"Tüketim verisi eklendi: {etsoKodu}, Tarih: {dateTime:yyyy-MM-dd HH:mm}, Değer: {consumptionValue}, Etkilenen Satır: {rowsAffected}");
                        successCount++;
                    }
                }
                catch (Exception dbEx)
                {
                    _logger.LogError(dbEx, $"Veritabanı işlemi sırasında hata: {dbEx.Message}");
                    errorCount++;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Tüketim verisi kaydedilirken hata: Tesisat: {tesisatNo}, Tarih: {value.MeterDate}");
                errorCount++;
            }
        }
        
        _logger.LogInformation($"Veritabanı işlemi tamamlandı. Başarılı: {successCount}, Hatalı: {errorCount}. Tesisat: {tesisatNo}, Ay: {month}/{year}");
    }
    
    private bool TryParseDateTime(string? dateTimeStr, out DateTime result)
    {
        result = DateTime.MinValue;
        
        if (string.IsNullOrEmpty(dateTimeStr))
            return false;
            
        return DateTime.TryParse(dateTimeStr, CultureInfo.InvariantCulture, DateTimeStyles.None, out result);
    }
    
    private List<(int month, int year)> GenerateMonthYearPairs(DateTime startDate, int numberOfMonths)
    {
        var result = new List<(int month, int year)>();
        var currentDate = new DateTime(startDate.Year, startDate.Month, 1);
        
        // Geçmiş tarihlere doğru git
        for (int i = 0; i < numberOfMonths; i++)
        {
            // Şimdiki tarihten geriye doğru git
            var targetDate = currentDate.AddMonths(-i);
            result.Add((targetDate.Month, targetDate.Year));
        }
        
        return result;
    }
    
    // Eski API metotları aşağıda kalacak
    
    // ... diğer metotlar ...
} 