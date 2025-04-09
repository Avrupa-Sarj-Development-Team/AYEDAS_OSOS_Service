using System.Net.Http.Headers;
using AYEDAS_OSOS.Models;
using AYEDAS_OSOS.Services;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;

namespace AYEDAS_OSOS.Controllers;

[ApiController]
[Route("api/[controller]")]
public class InstallationController : ControllerBase
{
    private readonly ILogger<InstallationController> _logger;
    private readonly ApiService _apiService;
    private readonly IConfiguration _configuration;
    private readonly TokenService _tokenService;
    private readonly HttpClient _httpClient;

    public InstallationController(
        ILogger<InstallationController> logger, 
        ApiService apiService,
        IConfiguration configuration,
        TokenService tokenService)
    {
        _logger = logger;
        _apiService = apiService;
        _configuration = configuration;
        _tokenService = tokenService;
        _httpClient = new HttpClient();
    }
    
    
    [HttpGet("GetInfoByTesisatNo")]
    public async Task<IActionResult> GetInfoByTesisatNo(string tesisatNo)
    {
        try
        {
            _logger.LogInformation($"Tesisat sorgusu başlatıldı: {tesisatNo}");
            
            if (string.IsNullOrEmpty(tesisatNo))
            {
                _logger.LogWarning("Tesisat numarası boş olarak gönderildi");
                return BadRequest("Tesisat numarası belirtilmelidir.");
            }
            
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
                    return StatusCode(500, new { message = "Kimlik doğrulama servisinden token alınamadı." });
                }
                
                idToken = tokenData.IdToken;
            }

            // API ayarlarını al
            string baseUrl = _configuration["ApiSettings:BaseUrl"] ?? "https://ososweb-ayedas-api.eedas.com.tr";
            string email = _configuration["ApiSettings:DefaultEmail"] ?? "info@avrupaelektrik.com.tr";
            int companyId = _configuration.GetValue<int>("ApiSettings:DefaultCompanyId", 2);

            // API isteği yap
            string apiUrl = $"{baseUrl}/InstallationOperations/GetInstallationInfo?email={email}&companyId={companyId}&pageSize=25&page=1&tesisatNo={tesisatNo}";
            
            _logger.LogInformation($"API Endpoint: {apiUrl}");
            
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
                _logger.LogError($"API isteği başarısız! HTTP Kodu: {response.StatusCode}, Yanıt: {responseContent}");
                
                // Eğer 401 Unauthorized gelirse, token'ı yenilemeyi dene
                if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                {
                    _logger.LogWarning("Unauthorized hatası alındı, token yenileniyor...");
                    // Token yenileme
                    await _tokenService.RefreshTokenAsync();
                    
                    // Yeniden ID token al
                    idToken = _tokenService.GetIdToken();
                    if (!string.IsNullOrEmpty(idToken))
                    {
                        _logger.LogInformation("Token yenilendi, işlem tekrarlanıyor...");
                        return await GetInfoByTesisatNo(tesisatNo);
                    }
                }
                
                return StatusCode((int)response.StatusCode, new { message = $"API isteği başarısız: {response.StatusCode}" });
            }
            
            // Yanıtı InstallationResponse nesnesine dönüştürüyoruz
            var installationResponse = JsonConvert.DeserializeObject<InstallationResponse>(responseContent);
            
            if (installationResponse == null || !installationResponse.Success)
            {
                _logger.LogError($"API yanıtı başarısız: {installationResponse?.Message ?? "Bilinmeyen hata"}");
                return StatusCode(500, new { message = installationResponse?.Message ?? "API yanıtı başarısız" });
            }
            
            if (installationResponse.Data == null || !installationResponse.Data.Any())
            {
                _logger.LogInformation($"Tesisat sorgusu için sonuç bulunamadı: {tesisatNo}");
                return NotFound(new { message = $"Tesisat numarası için sonuç bulunamadı: {tesisatNo}" });
            }
            
            _logger.LogInformation($"Tesisat sorgusu başarıyla tamamlandı: {installationResponse.TotalCount} sonuç bulundu.");
            
            return Ok(installationResponse);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Tesisat bilgisi alınırken hata oluştu: {tesisatNo}");
            return StatusCode(500, new { message = $"Hata oluştu: {ex.Message}" });
        }
    }

    [HttpGet("GetAllInstallations")]
    public async Task<IActionResult> GetAllInstallations(int page = 1, int pageSize = 25)
    {
        try
        {
            _logger.LogInformation($"Tüm tesisatlar sorgusu başlatıldı: Sayfa={page}, Boyut={pageSize}");
            
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
                    return StatusCode(500, new { message = "Kimlik doğrulama servisinden token alınamadı." });
                }
                
                idToken = tokenData.IdToken;
            }

            // API ayarlarını al
            string baseUrl = _configuration["ApiSettings:BaseUrl"] ?? "https://ososweb-ayedas-api.eedas.com.tr";
            string email = _configuration["ApiSettings:DefaultEmail"] ?? "info@avrupaelektrik.com.tr";
            int companyId = _configuration.GetValue<int>("ApiSettings:DefaultCompanyId", 2);

            // API isteği yap
            string apiUrl = $"{baseUrl}/InstallationOperations/GetInstallationInfo?email={email}&companyId={companyId}&pageSize={pageSize}&page={page}";
            
            _logger.LogInformation($"API Endpoint: {apiUrl}");
            
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
                _logger.LogError($"API isteği başarısız! HTTP Kodu: {response.StatusCode}, Yanıt: {responseContent}");
                
                // Eğer 401 Unauthorized gelirse, token'ı yenilemeyi dene
                if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                {
                    _logger.LogWarning("Unauthorized hatası alındı, token yenileniyor...");
                    // Token yenileme
                    await _tokenService.RefreshTokenAsync();
                    
                    // Yeniden ID token al
                    idToken = _tokenService.GetIdToken();
                    if (!string.IsNullOrEmpty(idToken))
                    {
                        _logger.LogInformation("Token yenilendi, işlem tekrarlanıyor...");
                        return await GetAllInstallations(page, pageSize);
                    }
                }
                
                return StatusCode((int)response.StatusCode, new { message = $"API isteği başarısız: {response.StatusCode}" });
            }
            
            // Yanıtı InstallationResponse nesnesine dönüştürüyoruz
            var installationResponse = JsonConvert.DeserializeObject<InstallationResponse>(responseContent);
            
            if (installationResponse == null || !installationResponse.Success)
            {
                _logger.LogError($"API yanıtı başarısız: {installationResponse?.Message ?? "Bilinmeyen hata"}");
                return StatusCode(500, new { message = installationResponse?.Message ?? "API yanıtı başarısız" });
            }
            
            if (installationResponse.Data == null || !installationResponse.Data.Any())
            {
                _logger.LogInformation("Tesisat sorgusu için sonuç bulunamadı");
                return Ok(new { data = new List<InstallationModel>(), totalCount = 0, success = true, message = "Sonuç bulunamadı" });
            }
            
            _logger.LogInformation($"Tesisat sorgusu başarıyla tamamlandı: {installationResponse.TotalCount} sonuç bulundu, {installationResponse.Data.Count} tanesi getiriliyor.");
            
            return Ok(installationResponse);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Tüm tesisatlar listelenirken hata oluştu");
            return StatusCode(500, new { message = $"Hata oluştu: {ex.Message}" });
        }
    }
} 