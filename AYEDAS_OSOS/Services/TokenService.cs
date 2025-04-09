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
    private string _latestIdToken = string.Empty;
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

        // Belirtilen aralıkta token yenileme zamanlayıcısını başlat (1 dakikada bir)
        int refreshInterval = 4; // dakika cinsinden
        _timer = new Timer(DoRefreshToken, null, TimeSpan.FromMinutes(refreshInterval), TimeSpan.FromMinutes(refreshInterval));

        return;
    }

    private void DoRefreshToken(object? state)
    {
        RefreshTokenAsync().ConfigureAwait(false);
    }
    
    // ID Token'ı döndüren metot
    public string GetIdToken()
    {
        if (string.IsNullOrEmpty(_latestIdToken) || DateTime.Now.Subtract(_lastRefreshTime).TotalMinutes > 1)
        {
            _logger.LogWarning("ID Token boş veya süresi dolmuş, yenileme gerekiyor");
            // Token'ı yenileme isteği yapılmalı, ancak burası senkron olmalı
            // Bu nedenle null döndürüp, çağıran tarafta yenileme işlemi başlatılmalı
            return string.Empty;
        }
        
        return _latestIdToken;
    }

    public async Task<TokenModel?> GetValidTokenAsync(bool forceRefresh = false)
    {
        // Eğer zorla yenileme isteniyorsa veya token boşsa veya süresi dolduysa
        if (forceRefresh || string.IsNullOrEmpty(_latestIdToken) || DateTime.Now.Subtract(_lastRefreshTime).TotalMinutes > 1)
        {
            _logger.LogInformation("Token yeniliyor...");
            await RefreshTokenAsync();
        }
        
        if (string.IsNullOrEmpty(_latestIdToken))
        {
            _logger.LogError("Geçerli token alınamadı");
            return null;
        }
        
        // Burada sahte bir TokenModel oluşturuyoruz
        // Gerçekte ID Token ile çalıştığımız için tam modeli doldurmuyoruz
        return new TokenModel
        {
            IdToken = _latestIdToken,
            TokenType = "Bearer",
            ExpiresIn = 60, // 1 dakika
            AccessToken = _latestIdToken, // ID Token'ı Access Token olarak gösteriyoruz
            RefreshExpiresIn = 1800
        };
    }

    public async Task RefreshTokenAsync()
    {
        try
        {
            string refreshTokenFile = GetAbsolutePath(_configuration["TokenSettings:RefreshTokenFile"] ?? "refreshToken.txt");
            string tokenUrl = _configuration["TokenSettings:TokenUrl"] ?? "https://identity.enerjisa.com.tr/auth/realms/OsosWeb/protocol/openid-connect/token";
            string clientId = _configuration["TokenSettings:ClientId"] ?? "ososayedas-react";

            _logger.LogInformation($"Token URL: {tokenUrl}");
            _logger.LogInformation($"Client ID: {clientId}");
            _logger.LogInformation($"Refresh Token Dosyası: {refreshTokenFile}");

            // Kullanıcıdan refresh token'ı oku
            string refreshToken = "";
            if (File.Exists(refreshTokenFile))
            {
                refreshToken = await File.ReadAllTextAsync(refreshTokenFile);
                refreshToken = refreshToken.Trim(); // Boşlukları temizle
                _logger.LogInformation($"Refresh token okundu: {refreshToken.Substring(0, Math.Min(20, refreshToken.Length))}...");
            }
            else
            {
                _logger.LogWarning($"Refresh token dosyası bulunamadı: {refreshTokenFile}. Lütfen geçerli bir refresh token ekleyin.");
                // Dosya yoksa oluştur ve örnek token ekle
                Directory.CreateDirectory(Path.GetDirectoryName(refreshTokenFile) ?? string.Empty);
                File.WriteAllText(refreshTokenFile, "REFRESH_TOKEN_BURAYA_EKLEYIN");
                return;
            }

            if (string.IsNullOrWhiteSpace(refreshToken))
            {
                _logger.LogWarning("Refresh token boş, işlem yapılmayacak.");
                return;
            }

            // HTTP başlıklarını temizle
            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            
            // HTTP istek parametrelerini oluştur
            var formContent = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("grant_type", "refresh_token"),
                new KeyValuePair<string, string>("refresh_token", refreshToken),
                new KeyValuePair<string, string>("client_id", clientId)
            });

            // Form verilerini görüntüle
            string formContentString = await formContent.ReadAsStringAsync();
            _logger.LogInformation($"Form içeriği: {formContentString}");

            // İsteği log'a yaz
            _logger.LogInformation($"Token isteği gönderiliyor: grant_type=refresh_token, client_id={clientId}");

            // Token isteği gönder
            var response = await _httpClient.PostAsync(tokenUrl, formContent);
            string responseContent = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError($"Token isteği başarısız! HTTP Kodu: {response.StatusCode}, Yanıt: {responseContent}");
                return;
            }

            _logger.LogInformation("Token isteği başarılı!");

            // Yanıtı işle
            TokenModel? tokenResponse = JsonConvert.DeserializeObject<TokenModel>(responseContent);
            
            if (tokenResponse == null)
            {
                _logger.LogError("Token yanıtı işlenemedi!");
                return;
            }

            // ID Token'ı değişkende sakla
            if (!string.IsNullOrEmpty(tokenResponse.IdToken))
            {
                _latestIdToken = tokenResponse.IdToken;
                _lastRefreshTime = DateTime.Now;
                
                _logger.LogInformation($"Yeni ID token alındı: {_latestIdToken.Substring(0, Math.Min(20, _latestIdToken.Length))}...");
                _logger.LogInformation($"Token yenileme zamanı: {_lastRefreshTime:yyyy-MM-dd HH:mm:ss}");
            }
            else
            {
                _logger.LogError("Yanıtta ID token bulunamadı!");
                return;
            }

            // Yeni refresh token'ı dosyaya kaydet
            await File.WriteAllTextAsync(refreshTokenFile, tokenResponse.RefreshToken);
            _logger.LogInformation($"Yeni refresh token kaydedildi: {refreshTokenFile}");
            
            // Token yanıtını JSON dosyasına kaydet (debugging için)
            string accessTokenFile = GetAbsolutePath(_configuration["TokenSettings:AccessTokenFile"] ?? "accessToken.json");
            Directory.CreateDirectory(Path.GetDirectoryName(accessTokenFile) ?? string.Empty);
            await File.WriteAllTextAsync(accessTokenFile, responseContent);
            _logger.LogInformation($"Token yanıtı kaydedildi: {accessTokenFile}");

            _logger.LogInformation("Token başarıyla yenilendi. Bir sonraki yenileme 1 dakika sonra gerçekleşecek.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Token yenileme sırasında hata oluştu");
        }
    }
    
    private async Task TryPasswordGrantAsync(string tokenUrl, string clientId, string accessTokenFile)
    {
        _logger.LogInformation("Refresh token ile token alma başarısız oldu, password grant deneniyor");
        
        // Bu örnek bir şablon, gerçek implementasyon sizin identity provider yapınıza bağlı olacaktır
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