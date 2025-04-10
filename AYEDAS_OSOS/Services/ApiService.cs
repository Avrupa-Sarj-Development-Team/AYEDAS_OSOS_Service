using System.Net;
using System.Net.Http.Headers;
using AYEDAS_OSOS.Models;
using Newtonsoft.Json;

namespace AYEDAS_OSOS.Services;

public class ApiService
{
    private readonly ILogger<ApiService> _logger;
    private readonly IConfiguration _configuration;
    private readonly HttpClient _httpClient;
    private readonly IWebHostEnvironment _environment;
    private readonly TokenService _tokenService;

    public ApiService(
        ILogger<ApiService> logger, 
        IConfiguration configuration, 
        IWebHostEnvironment environment,
        TokenService tokenService)
    {
        _logger = logger;
        _configuration = configuration;
        _environment = environment;
        _tokenService = tokenService;
        
        // HttpClient oluştur ve proxy ayarlarını kontrol et
        var handler = new HttpClientHandler();
        
        // Proxy kullanılacaksa ayarla
        string proxyUrl = _configuration["ApiSettings:ProxyUrl"] ?? "";
        bool useProxy = _configuration.GetValue<bool>("ApiSettings:UseProxy");
        
        if (useProxy && !string.IsNullOrEmpty(proxyUrl))
        {
            handler.Proxy = new WebProxy(proxyUrl);
            handler.UseProxy = true;
            _logger.LogInformation($"Proxy kullanılıyor: {proxyUrl}");
        }
        
        // SSL sertifika doğrulamasını devre dışı bırak (sadece test ortamı için)
#if DEBUG
        handler.ServerCertificateCustomValidationCallback = (sender, cert, chain, sslPolicyErrors) => true;
        _logger.LogWarning("SSL sertifika doğrulaması devre dışı bırakıldı (sadece test ortamında)");
#endif

        _httpClient = new HttpClient(handler);
    }

    public async Task<T?> SendRequestWithToken<T>(string url, HttpMethod? method = null) where T : class
    {
        method ??= HttpMethod.Get;
        
        try
        {
            // TokenService'den Access token al
            string accessToken = _tokenService.GetAccessToken();
            
            if (string.IsNullOrEmpty(accessToken))
            {
                _logger.LogWarning("Access token boş, yenileme yapılıyor");
                // Access token alınamadı, yenileme yap
                var tokenResult = await _tokenService.GetValidTokenAsync(true);
                
                if (tokenResult == null || string.IsNullOrEmpty(tokenResult.AccessToken))
                {
                    _logger.LogError("Access token alınamadı");
                    return null;
                }
                
                accessToken = tokenResult.AccessToken;
            }
            
            _logger.LogInformation($"API isteği için Access token kullanılıyor: {accessToken.Substring(0, Math.Min(20, accessToken.Length))}...");

            // HTTP istek header'larını ayarla
            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "AYEDAS_OSOS_Client");
            
            // Özel API başlıkları ekle
            AddApiHeaders();
            
            _logger.LogInformation($"API isteği gönderiliyor: {url}");

            // İstek gönder
            HttpResponseMessage response;
            
            if (method == HttpMethod.Get)
            {
                response = await _httpClient.GetAsync(url);
            }
            else if (method == HttpMethod.Post)
            {
                response = await _httpClient.PostAsync(url, null);
            }
            else
            {
                throw new NotImplementedException($"HTTP metodu desteklenmiyor: {method}");
            }

            // Yanıt header'larını logla
            _logger.LogInformation($"Yanıt başlıkları: {string.Join(", ", response.Headers.Select(h => $"{h.Key}: {string.Join(", ", h.Value)}"))}");
            
            string responseContent = await response.Content.ReadAsStringAsync();
            
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError($"API isteği başarısız! HTTP Kodu: {response.StatusCode}, Yanıt: {responseContent}");
                
                // 401 Unauthorized hatası alındığında token yenileme dene
                if (response.StatusCode == HttpStatusCode.Unauthorized)
                {
                    _logger.LogWarning("Yetkisiz erişim hatası. Token yenileme denemesi yapılıyor...");
                    
                    // Token yenileme ve tekrar deneme
                    var newTokenData = await _tokenService.GetValidTokenAsync(true);
                    
                    if (newTokenData != null && !string.IsNullOrEmpty(newTokenData.AccessToken))
                    {
                        _logger.LogInformation("Token yenilendi, istek tekrarlanıyor...");
                        return await SendRequestWithToken<T>(url, method);
                    }
                }
                
                return null;
            }

            _logger.LogInformation("API isteği başarılı!");
            return JsonConvert.DeserializeObject<T>(responseContent);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"API isteği sırasında hata oluştu: {url}");
            return null;
        }
    }
    
    private void AddApiHeaders()
    {
        // OSOS API'si için özel başlıklar ekleyebilirsiniz
        // Bazı API'ler ek başlıklara ihtiyaç duyabilir
        try
        {
            // Örnek: x-api-key başlığı
            string apiKey = _configuration["ApiSettings:ApiKey"] ?? "";
            if (!string.IsNullOrEmpty(apiKey))
            {
                _httpClient.DefaultRequestHeaders.Add("x-api-key", apiKey);
            }
            
            // Diğer özel başlıklar eklenebilir
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "API başlıkları eklenirken hata oluştu");
        }
    }
    
    private string GetAbsolutePath(string relativePath)
    {
        // İçerik dizini içinde tam yolu dön
        return Path.Combine(_environment.ContentRootPath, relativePath);
    }
} 