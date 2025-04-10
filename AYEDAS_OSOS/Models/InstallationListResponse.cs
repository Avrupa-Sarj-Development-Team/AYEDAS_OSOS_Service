using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace AYEDAS_OSOS.Models;

public class InstallationListResponse
{
    [JsonPropertyName("status")]
    [JsonProperty("status")]
    public int Status { get; set; }
    
    [JsonPropertyName("message")]
    [JsonProperty("message")]
    public string? Message { get; set; }
    
    [JsonPropertyName("instalation_list")]
    [JsonProperty("instalation_list")]
    public List<InstallationInfo>? InstallationList { get; set; }
    
    [JsonPropertyName("transactionId")]
    [JsonProperty("transactionId")]
    public string? TransactionId { get; set; }
}

public class InstallationInfo
{
    [JsonPropertyName("instalationNumber")]
    [JsonProperty("instalationNumber")]
    public string? InstallationNumber { get; set; }
    
    [JsonPropertyName("etso")]
    [JsonProperty("etso")]
    public string? Etso { get; set; }
    
    [JsonPropertyName("customerName")]
    [JsonProperty("customerName")]
    public string? CustomerName { get; set; }
    
    [JsonPropertyName("customerAdress")]
    [JsonProperty("customerAdress")]
    public string? CustomerAddress { get; set; }
    
    [JsonPropertyName("aboneTip")]
    [JsonProperty("aboneTip")]
    public string? AboneTip { get; set; }
    
    [JsonPropertyName("aboneTipTanim")]
    [JsonProperty("aboneTipTanim")]
    public string? AboneTipTanim { get; set; }
    
    [JsonPropertyName("il")]
    [JsonProperty("il")]
    public string? Il { get; set; }
    
    [JsonPropertyName("ilce")]
    [JsonProperty("ilce")]
    public string? Ilce { get; set; }
    
    [JsonPropertyName("koyuMahallesi")]
    [JsonProperty("koyuMahallesi")]
    public string? KoyuMahallesi { get; set; }
    
    [JsonPropertyName("caddesiSokagi")]
    [JsonProperty("caddesiSokagi")]
    public string? CaddesiSokagi { get; set; }
    
    [JsonPropertyName("tarifeTipi")]
    [JsonProperty("tarifeTipi")]
    public string? TarifeTipi { get; set; }
    
    [JsonPropertyName("tarifeTuru")]
    [JsonProperty("tarifeTuru")]
    public string? TarifeTuru { get; set; }
    
    [JsonPropertyName("tesisatTurTanim")]
    [JsonProperty("tesisatTurTanim")]
    public string? TesisatTurTanim { get; set; }
    
    [JsonPropertyName("kuruluGucu")]
    [JsonProperty("kuruluGucu")]
    public string? KuruluGucu { get; set; }
    
    [JsonPropertyName("koordinatX")]
    [JsonProperty("koordinatX")]
    public string? KoordinatX { get; set; }
    
    [JsonPropertyName("koordinatY")]
    [JsonProperty("koordinatY")]
    public string? KoordinatY { get; set; }
    
    [JsonPropertyName("meterNumber")]
    [JsonProperty("meterNumber")]
    public string? MeterNumber { get; set; }
    
    [JsonPropertyName("meterModel")]
    [JsonProperty("meterModel")]
    public string? MeterModel { get; set; }
    
    [JsonPropertyName("meterMultiplier")]
    [JsonProperty("meterMultiplier")]
    public string? MeterMultiplier { get; set; }
    
    [JsonPropertyName("muhatapNo")]
    [JsonProperty("muhatapNo")]
    public string? MuhatapNo { get; set; }
    
    [JsonPropertyName("sayimNokTanim")]
    [JsonProperty("sayimNokTanim")]
    public string? SayimNokTanim { get; set; }
} 