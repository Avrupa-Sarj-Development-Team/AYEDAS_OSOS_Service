using System.Text.Json.Serialization;

namespace AYEDAS_OSOS.Models;

public class InstallationResponse
{
    [JsonPropertyName("data")]
    public List<InstallationModel> Data { get; set; } = new List<InstallationModel>();
    
    [JsonPropertyName("totalCount")]
    public int TotalCount { get; set; }
    
    [JsonPropertyName("success")]
    public bool Success { get; set; }
    
    [JsonPropertyName("message")]
    public string? Message { get; set; }
}

public class InstallationModel
{
    [JsonPropertyName("id")]
    public int Id { get; set; }
    
    [JsonPropertyName("tesisatNo")]
    public string? TesisatNo { get; set; }
    
    [JsonPropertyName("sozlesmeHesapNo")]
    public string? SozlesmeHesapNo { get; set; }
    
    [JsonPropertyName("aboneAdiSoyadi")]
    public string? AboneAdiSoyadi { get; set; }
    
    [JsonPropertyName("modemSeriNo")]
    public string? ModemSeriNo { get; set; }
    
    [JsonPropertyName("sayacSeriNo")]
    public string? SayacSeriNo { get; set; }
    
    [JsonPropertyName("sayacModeli")]
    public string? SayacModeli { get; set; }
    
    [JsonPropertyName("sayacMarka")]
    public string? SayacMarka { get; set; }
    
    [JsonPropertyName("adres")]
    public string? Adres { get; set; }
    
    [JsonPropertyName("headEndId")]
    public int HeadEndId { get; set; }
    
    [JsonPropertyName("isTedas")]
    public int IsTedas { get; set; }
    
    [JsonPropertyName("companyId")]
    public int CompanyId { get; set; }
    
    [JsonPropertyName("tahakkukCarpani")]
    public decimal TahakkukCarpani { get; set; }
    
    [JsonPropertyName("muhattapNo")]
    public string? MuhattapNo { get; set; }
    
    [JsonPropertyName("tedarikciNo")]
    public string? TedarikciNo { get; set; }
    
    [JsonPropertyName("bolgeNo")]
    public string? BolgeNo { get; set; }
    
    [JsonPropertyName("x")]
    public string? X { get; set; }
    
    [JsonPropertyName("y")]
    public string? Y { get; set; }
    
    [JsonPropertyName("aboneTipTanim")]
    public string? AboneTipTanim { get; set; }
    
    [JsonPropertyName("modemMarka")]
    public string? ModemMarka { get; set; }
} 