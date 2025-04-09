using Newtonsoft.Json;

namespace AYEDAS_OSOS.Models;

public class TokenModel
{
    [JsonProperty("access_token")]
    public string AccessToken { get; set; } = string.Empty;
    
    [JsonProperty("expires_in")]
    public int ExpiresIn { get; set; }
    
    [JsonProperty("refresh_expires_in")]
    public int RefreshExpiresIn { get; set; }
    
    [JsonProperty("refresh_token")]
    public string RefreshToken { get; set; } = string.Empty;
    
    [JsonProperty("token_type")]
    public string TokenType { get; set; } = string.Empty;
    
    [JsonProperty("id_token")]
    public string IdToken { get; set; } = string.Empty;
    
    [JsonProperty("not-before-policy")]
    public long NotBeforePolicy { get; set; }
    
    [JsonProperty("session_state")]
    public string SessionState { get; set; } = string.Empty;
    
    [JsonProperty("scope")]
    public string Scope { get; set; } = string.Empty;
} 