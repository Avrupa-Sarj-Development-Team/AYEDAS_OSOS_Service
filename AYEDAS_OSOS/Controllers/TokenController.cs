using AYEDAS_OSOS.Models;
using AYEDAS_OSOS.Services;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;

namespace AYEDAS_OSOS.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TokenController : ControllerBase
{
    private readonly ILogger<TokenController> _logger;
    private readonly TokenService _tokenService;
    private readonly ApiService _apiService;
    private readonly IConfiguration _configuration;
    private readonly IWebHostEnvironment _environment;

    public TokenController(
        ILogger<TokenController> logger,
        TokenService tokenService,
        ApiService apiService,
        IConfiguration configuration,
        IWebHostEnvironment environment)
    {
        _logger = logger;
        _tokenService = tokenService;
        _apiService = apiService;
        _configuration = configuration;
        _environment = environment;
    }

    [HttpGet("CheckIdToken")]
    public IActionResult CheckIdToken()
    {
        try
        {
            _logger.LogInformation("ID token kontrolü yapılıyor");
            string idToken = _tokenService.GetIdToken();
            
            bool isValid = !string.IsNullOrEmpty(idToken);
            
            if (isValid)
            {
                _logger.LogInformation("ID token geçerli");
                return Ok(new
                {
                    valid = true,
                    idToken = idToken,
                    message = "Token geçerli"
                });
            }
            else
            {
                _logger.LogWarning("ID token geçersiz veya boş");
                return Ok(new
                {
                    valid = false,
                    idToken = string.Empty,
                    message = "Token geçersiz veya boş"
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ID token kontrolü sırasında hata oluştu");
            return StatusCode(500, new { message = $"Hata oluştu: {ex.Message}" });
        }
    }
    
    [HttpPost("RefreshToken")]
    public async Task<IActionResult> RefreshToken()
    {
        try
        {
            _logger.LogInformation("ID token yenileme isteği alındı");
            await _tokenService.RefreshTokenAsync();
            
            string idToken = _tokenService.GetIdToken();
            bool isSuccess = !string.IsNullOrEmpty(idToken);
            
            if (isSuccess)
            {
                _logger.LogInformation("ID token başarıyla yenilendi");
                return Ok(new
                {
                    success = true,
                    idToken = idToken,
                    message = "Token başarıyla yenilendi"
                });
            }
            else
            {
                _logger.LogWarning("ID token yenilenemedi");
                return BadRequest(new
                {
                    success = false,
                    idToken = string.Empty,
                    message = "Token yenilenemedi"
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ID token yenileme sırasında hata oluştu");
            return StatusCode(500, new
            {
                success = false,
                message = $"Hata oluştu: {ex.Message}"
            });
        }
    }

    [HttpPost("ForceRefresh")]
    public async Task<IActionResult> ForceRefresh()
    {
        try
        {
            _logger.LogInformation("Manuel token yenileme isteği alındı");
            var tokenData = await _tokenService.GetValidTokenAsync(true); // true: Force refresh

            if (tokenData == null)
            {
                return BadRequest(new { message = "Token yenilenemedi" });
            }

            return Ok(new
            {
                message = "Token başarıyla yenilendi",
                tokenPreview = tokenData.AccessToken.Substring(0, Math.Min(20, tokenData.AccessToken.Length)) + "...",
                expiresIn = tokenData.ExpiresIn,
                refreshExpiresIn = tokenData.RefreshExpiresIn
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Token yenileme sırasında hata oluştu");
            return StatusCode(500, new { message = $"Hata oluştu: {ex.Message}" });
        }
    }

    [HttpGet("Current")]
    public async Task<IActionResult> GetCurrentToken()
    {
        try
        {
            var tokenData = await _tokenService.GetValidTokenAsync(false);
            
            if (tokenData == null)
            {
                return NotFound(new { message = "Geçerli token bulunamadı" });
            }

            return Ok(new
            {
                tokenPreview = tokenData.AccessToken.Substring(0, Math.Min(20, tokenData.AccessToken.Length)) + "...",
                expiresIn = tokenData.ExpiresIn,
                refreshExpiresIn = tokenData.RefreshExpiresIn,
                tokenType = tokenData.TokenType
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Token bilgisi alınırken hata oluştu");
            return StatusCode(500, new { message = $"Hata oluştu: {ex.Message}" });
        }
    }

    [HttpPost("Test")]
    public async Task<IActionResult> TestToken()
    {
        try
        {
            _logger.LogInformation("Token testi başlatıldı");
            
            // 1. Önce token al
            var tokenData = await _tokenService.GetValidTokenAsync(true);
            
            if (tokenData == null)
            {
                return BadRequest(new { message = "Token alınamadı" });
            }
            
            // 2. API isteği yap (TestConnection endpointi yerine gerçek bir API çağrısı)
            string baseUrl = _configuration["ApiSettings:BaseUrl"] ?? "";
            string tesisatNo = "4003829127"; // Örnek tesisat numarası
            string email = _configuration["ApiSettings:DefaultEmail"] ?? "";
            int companyId = _configuration.GetValue<int>("ApiSettings:DefaultCompanyId", 2);
            
            string apiUrl = $"{baseUrl}/InstallationOperations/GetInstallationInfo?email={email}&companyId={companyId}&pageSize=25&page=1&tesisatNo={tesisatNo}";
            
            _logger.LogInformation($"API test isteği yapılıyor: {apiUrl}");
            
            var result = await _apiService.SendRequestWithToken<object>(apiUrl);
            
            if (result == null)
            {
                return BadRequest(new { message = "API isteği başarısız oldu" });
            }
            
            return Ok(new
            {
                message = "Token ve API testi başarılı",
                tokenInfo = new
                {
                    tokenPreview = tokenData.AccessToken.Substring(0, Math.Min(20, tokenData.AccessToken.Length)) + "...",
                    expiresIn = tokenData.ExpiresIn
                },
                apiResult = result
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Token testi sırasında hata oluştu");
            return StatusCode(500, new { message = $"Hata oluştu: {ex.Message}" });
        }
    }
} 