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
    private readonly IConfiguration _configuration;
    private readonly IWebHostEnvironment _environment;

    public TokenController(
        ILogger<TokenController> logger,
        TokenService tokenService,
        IConfiguration configuration,
        IWebHostEnvironment environment)
    {
        _logger = logger;
        _tokenService = tokenService;
        _configuration = configuration;
        _environment = environment;
    }

    [HttpGet("GetToken")]
    public async Task<IActionResult> GetToken()
    {
        try
        {
            _logger.LogInformation("Token isteği alındı");
            var tokenData = await _tokenService.GetValidTokenAsync(false);
            
            if (tokenData == null)
            {
                _logger.LogWarning("Token alınamadı, yeniden deneniyor");
                tokenData = await _tokenService.GetValidTokenAsync(true);
                
                if (tokenData == null)
                {
                    return BadRequest(new { message = "Token alınamadı" });
                }
            }

            return Ok(new
            {
                access_token = tokenData.AccessToken,
                token_type = tokenData.TokenType,
                expires_in = tokenData.ExpiresIn
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Token alınırken hata oluştu");
            return StatusCode(500, new { message = $"Hata oluştu: {ex.Message}" });
        }
    }
    
    [HttpGet("GetAyedasToken")]
    public async Task<IActionResult> GetAyedasToken()
    {
        try
        {
            _logger.LogInformation("AYEDAS Token isteği alındı");
            var tokenData = await _tokenService.GetValidTokenAsync(false);
            
            if (tokenData == null)
            {
                _logger.LogWarning("Token alınamadı, yeniden deneniyor");
                tokenData = await _tokenService.GetValidTokenAsync(true);
                
                if (tokenData == null)
                {
                    return BadRequest(new { message = "Token alınamadı" });
                }
            }

            // AYEDAS API, access_token parametresini URL'de bekliyor
            _logger.LogInformation($"AYEDAS token başarıyla oluşturuldu. Token: {tokenData.AccessToken.Substring(0, Math.Min(20, tokenData.AccessToken.Length))}...");
            
            return Ok(new
            {
                access_token = tokenData.AccessToken,
                tokenUrl = $"https://mdmsaatlik.ayedas.com.tr/ayedas/mdm-api/customer/installation-list?access_token={tokenData.AccessToken}",
                expires_in = tokenData.ExpiresIn
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AYEDAS token alınırken hata oluştu");
            return StatusCode(500, new { message = $"Hata oluştu: {ex.Message}" });
        }
    }
} 