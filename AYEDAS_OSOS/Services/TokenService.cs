using System.Net.Http.Headers;
using System.Text;
using AYEDAS_OSOS.Models;
using Newtonsoft.Json;

namespace AYEDAS_OSOS.Services;

public class TokenService : IHostedService, IDisposable
{
    private readonly ILogger<TokenService> _logger;
    private readonly IConfiguration _configuration;
    private Timer? _timer;
    private readonly HttpClient _httpClient;
    private readonly IWebHostEnvironment _environment;
    private string _latestAccessToken = string.Empty;
    private DateTime _lastRefreshTime = DateTime.MinValue;

    public TokenService(ILogger<TokenService> logger, IConfiguration configuration, IWebHostEnvironment environment)
    {
        _logger = logger;
        _configuration = configuration;
        _environment = environment;
        _httpClient = new HttpClient();
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Token yenileme servisi başlatılıyor");

        // İlk başlangıçta token yenileme işlemini gerçekleştir
        await RefreshTokenAsync();

        // Belirtilen aralıkta token yenileme zamanlayıcısını başlat (4 dakikada bir)
        int refreshInterval = _configuration.GetValue<int>("TokenSettings:RefreshIntervalMinutes", 4);
        _timer = new Timer(DoRefreshToken, null, TimeSpan.FromMinutes(refreshInterval), TimeSpan.FromMinutes(refreshInterval));

        return;
    }

    private void DoRefreshToken(object? state)
    {
        RefreshTokenAsync().ConfigureAwait(false);
    }
    
    // Access Token'ı döndüren metot
    public string GetAccessToken()
    {
        if (string.IsNullOrEmpty(_latestAccessToken) || DateTime.Now.Subtract(_lastRefreshTime).TotalMinutes > 4)
        {
            _logger.LogWarning("Access Token boş veya süresi dolmuş, yenileme gerekiyor");
            return string.Empty;
        }
        
        return _latestAccessToken;
    }

    public async Task<TokenModel?> GetValidTokenAsync(bool forceRefresh = false)
    {
        // Eğer zorla yenileme isteniyorsa veya token boşsa veya süresi dolduysa
        if (forceRefresh || string.IsNullOrEmpty(_latestAccessToken) || DateTime.Now.Subtract(_lastRefreshTime).TotalMinutes > 2) // 4 dakika yerine 2 dakika süresi olan tokenleri de yenile
        {
            _logger.LogInformation("Token yeniliyor (forceRefresh: {0}, tokenEmpty: {1}, timeElapsed: {2:0.00} dakika)", 
                forceRefresh, 
                string.IsNullOrEmpty(_latestAccessToken),
                DateTime.Now.Subtract(_lastRefreshTime).TotalMinutes);
            
            await RefreshTokenAsync();
        }
        
        if (string.IsNullOrEmpty(_latestAccessToken))
        {
            _logger.LogError("Geçerli token alınamadı");
            return null;
        }
        
        return new TokenModel
        {
            AccessToken = _latestAccessToken,
            TokenType = "Bearer",
            ExpiresIn = 240, // 4 dakika
            RefreshExpiresIn = 1800
        };
    }

    public async Task RefreshTokenAsync()
    {
        try
        {
            string tokenUrl = _configuration["TokenSettings:TokenUrl"] ?? "https://mdmsaatlik.ayedas.com.tr/ayedas/mdm-api/oauth/token";
            string clientId = _configuration["TokenSettings:ClientId"] ?? "";
            string clientSecret = _configuration["TokenSettings:ClientSecret"] ?? "";
            string consumerId = _configuration["TokenSettings:ConsumerId"] ?? "MDMAYPRD";

            _logger.LogInformation($"Token URL: {tokenUrl}");
            _logger.LogInformation($"Client ID: {clientId}");
            _logger.LogInformation($"Consumer ID: {consumerId}");

            // HTTP başlıklarını temizle
            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "AYEDAS_OSOS_Client");
            
            // URL'yi query string parametreleri ile oluştur
            var queryParameters = $"?grant_type=client_credentials&client_id={Uri.EscapeDataString(clientId)}&client_secret={Uri.EscapeDataString(clientSecret)}&consumerID={Uri.EscapeDataString(consumerId)}";
            string fullUrl = tokenUrl + queryParameters;
            
            _logger.LogInformation($"Tam URL: {fullUrl}");
            
            // Token isteği gönder - POST metodu ve boş body ile
            var response = await _httpClient.PostAsync(fullUrl, null);
            string responseContent = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError($"Token isteği başarısız! HTTP Kodu: {response.StatusCode}, Yanıt: {responseContent}");
                
                // Alternatif metod dene - FormUrlEncoded yerine query string
                _logger.LogInformation("Alternatif metod deneniyor...");
                var formContent = new FormUrlEncodedContent(new[]
                {
                    new KeyValuePair<string, string>("grant_type", "client_credentials"),
                    new KeyValuePair<string, string>("client_id", clientId),
                    new KeyValuePair<string, string>("client_secret", clientSecret),
                    new KeyValuePair<string, string>("consumerID", consumerId)
                });

                string formContentString = await formContent.ReadAsStringAsync();
                _logger.LogInformation($"Form içeriği: {formContentString}");

                _logger.LogInformation($"Token isteği gönderiliyor: grant_type=client_credentials, client_id={clientId}, consumerID={consumerId}");
                
                response = await _httpClient.PostAsync(tokenUrl, formContent);
                responseContent = await response.Content.ReadAsStringAsync();
                
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError($"İkinci yöntem de başarısız! HTTP Kodu: {response.StatusCode}, Yanıt: {responseContent}");
                    return;
                }
            }

            _logger.LogInformation("Token isteği başarılı!");

            // Yanıtı işle
            TokenModel? tokenResponse = JsonConvert.DeserializeObject<TokenModel>(responseContent);
            
            if (tokenResponse == null)
            {
                _logger.LogError("Token yanıtı işlenemedi!");
                return;
            }

            // Access Token'ı değişkende sakla
            if (!string.IsNullOrEmpty(tokenResponse.AccessToken))
            {
                _latestAccessToken = tokenResponse.AccessToken;
                _lastRefreshTime = DateTime.Now;
                
                _logger.LogInformation($"Yeni Access token alındı: {_latestAccessToken.Substring(0, Math.Min(20, _latestAccessToken.Length))}...");
                _logger.LogInformation($"Token yenileme zamanı: {_lastRefreshTime:yyyy-MM-dd HH:mm:ss}");
            }
            else
            {
                _logger.LogError("Yanıtta Access token bulunamadı!");
                return;
            }
            
            // Token yanıtını JSON dosyasına kaydet (debugging için)
            string accessTokenFile = GetAbsolutePath(_configuration["TokenSettings:AccessTokenFile"] ?? "accessToken.json");
            Directory.CreateDirectory(Path.GetDirectoryName(accessTokenFile) ?? string.Empty);
            await File.WriteAllTextAsync(accessTokenFile, responseContent);
            _logger.LogInformation($"Token yanıtı kaydedildi: {accessTokenFile}");

            _logger.LogInformation("Token başarıyla yenilendi. Bir sonraki yenileme 4 dakika sonra gerçekleşecek.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Token yenileme sırasında hata oluştu");
        }
    }

    private string GetAbsolutePath(string relativePath)
    {
        // İçerik dizini içinde tam yolu dön
        return Path.Combine(_environment.ContentRootPath, relativePath);
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Token yenileme servisi durduruluyor");

        _timer?.Change(Timeout.Infinite, 0);

        return Task.CompletedTask;
    }

    public void Dispose()
    {
        _timer?.Dispose();
        _httpClient.Dispose();
    }
} 