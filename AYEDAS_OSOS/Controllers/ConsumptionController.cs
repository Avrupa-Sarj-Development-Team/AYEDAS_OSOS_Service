using System.Globalization;
using System.Net.Http.Headers;
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

    [HttpGet("ImportConsumptionData")]
    public async Task<IActionResult> ImportConsumptionData()
    {
        try
        {
            _logger.LogInformation("Tüketim verisi aktarma işlemi başlatıldı");
            
            // API ayarlarını al
            string baseUrl = _configuration["ApiSettings:BaseUrl"] ?? "https://ososweb-ayedas-api.eedas.com.tr";
            string email = _configuration["ApiSettings:DefaultEmail"] ?? "info@avrupaelektrik.com.tr";
            int companyId = _configuration.GetValue<int>("ApiSettings:DefaultCompanyId", 2);
            
            // Önce tesisatları al
            var tesisatNoList = await GetTesisatNumbers(baseUrl, email, companyId);
            
            if (tesisatNoList == null || !tesisatNoList.Any())
            {
                _logger.LogWarning("Hiç tesisat numarası bulunamadı");
                return NotFound("Hiç tesisat numarası bulunamadı");
            }
            
            _logger.LogInformation($"Toplam {tesisatNoList.Count} adet tesisat numarası bulundu");
            
            // Son iki yıla ait verileri çek
            DateTime now = DateTime.Now;
            var monthYearPairs = GenerateMonthYearPairs(now, 24); // Son 24 ay
            
            int totalSuccess = 0;
            int totalError = 0;
            var errorList = new List<string>();
            
            foreach (var tesisatNo in tesisatNoList)
            {
                foreach (var (month, year) in monthYearPairs)
                {
                    try
                    {
                        _logger.LogInformation($"Tesisat No: {tesisatNo}, Ay: {month}, Yıl: {year} için veri çekiliyor");
                        var consumptionData = await GetHourlyConsumptionData(baseUrl, tesisatNo, email, companyId, month, year);
                        
                        if (consumptionData?.Data == null || !consumptionData.Data.Any())
                        {
                            _logger.LogWarning($"Tesisat No: {tesisatNo}, Ay: {month}, Yıl: {year} için veri bulunamadı");
                            continue;
                        }
                        
                        // Verileri veritabanına kaydet
                        await SaveConsumptionDataToDatabase(consumptionData.Data, tesisatNo, month, year);
                        _logger.LogInformation($"Tesisat No: {tesisatNo}, Ay: {month}, Yıl: {year} için {consumptionData.Data.Count} adet veri kaydedildi");
                        totalSuccess++;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, $"Tesisat No: {tesisatNo}, Ay: {month}, Yıl: {year} için veri çekilirken hata oluştu");
                        errorList.Add($"Tesisat: {tesisatNo}, Ay: {month}, Yıl: {year}, Hata: {ex.Message}");
                        totalError++;
                    }
                    
                    // Rate limiting için kısa bir bekleme
                    await Task.Delay(500);
                }
            }
            
            return Ok(new
            {
                TotalSuccess = totalSuccess,
                TotalError = totalError,
                Errors = errorList
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Tüketim verisi aktarma işleminde hata oluştu");
            return StatusCode(500, new { message = $"Hata oluştu: {ex.Message}" });
        }
    }
    
    private async Task<List<string>> GetTesisatNumbers(string baseUrl, string email, int companyId)
    {
        try
        {
            // ID Token alınıyor
            string idToken = _tokenService.GetIdToken();
            
            if (string.IsNullOrEmpty(idToken))
            {
                _logger.LogWarning("ID token boş, yenileme yapılıyor");
                // ID token yenileme
                var tokenData = await _tokenService.GetValidTokenAsync(true);
                
                if (tokenData == null || string.IsNullOrEmpty(tokenData.IdToken))
                {
                    _logger.LogError("ID token alınamadı");
                    throw new Exception("Kimlik doğrulama servisinden token alınamadı.");
                }
                
                idToken = tokenData.IdToken;
            }
            
            // API isteği yap - Tüm tesisatları getir
            string apiUrl = $"{baseUrl}/InstallationOperations/GetInstallationInfo?email={email}&companyId={companyId}&pageSize=1000&page=1";
            
            _logger.LogInformation($"Tesisat Listesi API Endpoint: {apiUrl}");
            
            // Yeni bir HttpRequestMessage oluştur
            var request = new HttpRequestMessage(HttpMethod.Get, apiUrl);
            
            // ID Token'ı Authorization header'a ekle
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", idToken);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            
            // İsteği gönder
            var response = await _httpClient.SendAsync(request);
            string responseContent = await response.Content.ReadAsStringAsync();
            
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError($"API yanıtı başarısız: {response.StatusCode} - {responseContent}");
                throw new Exception($"API yanıtı başarısız: {response.StatusCode}");
            }
            
            var installationResponse = JsonConvert.DeserializeObject<InstallationResponse>(responseContent);
            
            if (installationResponse?.Data == null || !installationResponse.Data.Any())
            {
                _logger.LogWarning("Tesisat listesi boş");
                return new List<string>();
            }
            
            // Tesisat numaralarını döndür
            return installationResponse.Data
                .Where(i => !string.IsNullOrEmpty(i.TesisatNo))
                .Select(i => i.TesisatNo!)
                .ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Tesisat numaraları alınırken hata oluştu");
            throw;
        }
    }
    
    private async Task<HourlyConsumptionResponse?> GetHourlyConsumptionData(string baseUrl, string tesisatNo, string email, int companyId, int month, int year)
    {
        try
        {
            // ID Token alınıyor
            string idToken = _tokenService.GetIdToken();
            
            if (string.IsNullOrEmpty(idToken))
            {
                _logger.LogWarning("ID token boş, yenileme yapılıyor");
                // ID token yenileme
                var tokenData = await _tokenService.GetValidTokenAsync(true);
                
                if (tokenData == null || string.IsNullOrEmpty(tokenData.IdToken))
                {
                    _logger.LogError("ID token alınamadı");
                    throw new Exception("Kimlik doğrulama servisinden token alınamadı.");
                }
                
                idToken = tokenData.IdToken;
            }
            
            // API isteği yap
            string apiUrl = $"{baseUrl}/HourlyConsumption/ReadHourlyListData?tesisatNo={tesisatNo}&email={email}&companyId={companyId}&month={month}&year={year}";
            
            _logger.LogInformation($"Saatlik Tüketim API Endpoint: {apiUrl}");
            
            // Yeni bir HttpRequestMessage oluştur
            var request = new HttpRequestMessage(HttpMethod.Get, apiUrl);
            
            // ID Token'ı Authorization header'a ekle
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", idToken);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            
            // İsteği gönder
            var response = await _httpClient.SendAsync(request);
            string responseContent = await response.Content.ReadAsStringAsync();
            
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError($"API yanıtı başarısız: {response.StatusCode} - {responseContent}");
                throw new Exception($"API yanıtı başarısız: {response.StatusCode}");
            }
            
            return JsonConvert.DeserializeObject<HourlyConsumptionResponse>(responseContent);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Saatlik tüketim verileri alınırken hata oluştu: TesisatNo={tesisatNo}, Ay={month}, Yıl={year}");
            throw;
        }
    }
    
    private async Task SaveConsumptionDataToDatabase(List<HourlyConsumptionData> consumptionDataList, string tesisatNo, int month, int year)
    {
        try
        {
            foreach (var data in consumptionDataList)
            {
                // TimeStamp'i parse et
                if (!TryParseDateTime(data.TimeStamp, out DateTime dateTime))
                {
                    _logger.LogWarning($"Geçersiz zaman damgası: {data.TimeStamp}");
                    continue;
                }
                
                // Consumption değerini parse et
                if (!decimal.TryParse(data.Consumption, NumberStyles.Any, CultureInfo.InvariantCulture, out decimal consumptionValue))
                {
                    _logger.LogWarning($"Geçersiz tüketim değeri: {data.Consumption}");
                    continue;
                }
                
                // Parametreleri oluştur
                var parameters = new
                {
                    Period = $"{year}-{month:D2}", 
                    Etso = data.EtsoKodu, 
                    MeterId = (int?)null,
                    TesisatNo = tesisatNo,
                    DistributionCompany = "AYEDAS",
                    Year = dateTime.Year,   // TimeStamp'ten gelen yıl değeri
                    Month = dateTime.Month, // TimeStamp'ten gelen ay değeri
                    Day = dateTime.Day,     // TimeStamp'ten gelen gün değeri
                    Hour = dateTime.Hour,   // TimeStamp'ten gelen saat değeri
                    DateTime = dateTime,    // Parse edilmiş DateTime nesnesi
                    Value = consumptionValue,
                    CreatedAt = DateTime.UtcNow
                };
                
                // Veritabanına ekle - önce varsa sil sonra ekle stratejisi
                string checkSql = @"
                    SELECT COUNT(*) FROM ""MeterOsosConsumption"" 
                    WHERE ""Etso"" = @Etso 
                    AND ""Year"" = @Year 
                    AND ""Month"" = @Month 
                    AND ""Day"" = @Day 
                    AND ""Hour"" = @Hour
                ";
                
                int exists = await _databaseService.QuerySingleOrDefaultAsync<int>(checkSql, new { 
                    Etso = data.EtsoKodu, 
                    Year = dateTime.Year, 
                    Month = dateTime.Month, 
                    Day = dateTime.Day, 
                    Hour = dateTime.Hour
                });
                
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
                    
                    await _databaseService.ExecuteAsync(updateSql, parameters);
                }
                else
                {
                    // Veri yoksa ekle
                    string insertSql = @"
                        INSERT INTO ""MeterOsosConsumption"" 
                        (""Period"", ""Etso"", ""MeterId"", ""TesisatNo"", ""DistributionCompany"", ""Year"", ""Month"", ""Day"", ""Hour"", ""DateTime"", ""Value"", ""CreatedAt"")
                        VALUES 
                        (@Period, @Etso, @MeterId, @TesisatNo, @DistributionCompany, @Year, @Month, @Day, @Hour, @DateTime, @Value, @CreatedAt)
                    ";
                    
                    await _databaseService.ExecuteAsync(insertSql, parameters);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Veritabanına kayıt sırasında hata oluştu: TesisatNo={tesisatNo}, Ay={month}, Yıl={year}");
            throw;
        }
    }
    
    private bool TryParseDateTime(string? dateTimeStr, out DateTime result)
    {
        result = DateTime.MinValue;
        
        if (string.IsNullOrEmpty(dateTimeStr))
            return false;
            
        // Expected format: "31-01-2025 23:00"
        string[] formats = { "dd-MM-yyyy HH:mm", "dd.MM.yyyy HH:mm", "dd/MM/yyyy HH:mm" };
        return DateTime.TryParseExact(dateTimeStr, formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out result);
    }
    
    private List<(int month, int year)> GenerateMonthYearPairs(DateTime startDate, int numberOfMonths)
    {
        var result = new List<(int month, int year)>();
        
        var currentDate = startDate;
        for (int i = 0; i < numberOfMonths; i++)
        {
            result.Add((currentDate.Month, currentDate.Year));
            currentDate = currentDate.AddMonths(-1);
        }
        
        return result;
    }
} 