using System.Text.Json.Serialization;

namespace AYEDAS_OSOS.Models;

public class HourlyConsumptionResponse
{
    [JsonPropertyName("data")]
    public List<HourlyConsumptionData> Data { get; set; } = new List<HourlyConsumptionData>();
    
    [JsonPropertyName("totalCount")]
    public int TotalCount { get; set; }
    
    [JsonPropertyName("success")]
    public bool Success { get; set; }
    
    [JsonPropertyName("message")]
    public string? Message { get; set; }
}

public class HourlyConsumptionData
{
    [JsonPropertyName("tesisatNo")]
    public string? TesisatNo { get; set; }
    
    [JsonPropertyName("muhattapNo")]
    public string? MuhattapNo { get; set; }
    
    [JsonPropertyName("serialNo")]
    public string? SerialNo { get; set; }
    
    [JsonPropertyName("etsoKodu")]
    public string? EtsoKodu { get; set; }
    
    [JsonPropertyName("timeStamp")]
    public string? TimeStamp { get; set; }
    
    [JsonPropertyName("generation")]
    public string? Generation { get; set; }
    
    [JsonPropertyName("consumption")]
    public string? Consumption { get; set; }
    
    [JsonPropertyName("totalCount")]
    public string? TotalCount { get; set; }
} 