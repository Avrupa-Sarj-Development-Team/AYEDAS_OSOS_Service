using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace AYEDAS_OSOS.Models;

public class HourlyConsumptionResponse
{
    [JsonPropertyName("data")]
    [JsonProperty("data")]
    public List<HourlyConsumptionData> Data { get; set; } = new List<HourlyConsumptionData>();
    
    [JsonPropertyName("totalCount")]
    [JsonProperty("totalCount")]
    public int TotalCount { get; set; }
    
    [JsonPropertyName("success")]
    [JsonProperty("success")]
    public bool Success { get; set; }
    
    [JsonPropertyName("message")]
    [JsonProperty("message")]
    public string? Message { get; set; }
}

public class HourlyConsumptionData
{
    [JsonPropertyName("tesisatNo")]
    [JsonProperty("tesisatNo")]
    public string? TesisatNo { get; set; }
    
    [JsonPropertyName("muhattapNo")]
    [JsonProperty("muhattapNo")]
    public string? MuhattapNo { get; set; }
    
    [JsonPropertyName("serialNo")]
    [JsonProperty("serialNo")]
    public string? SerialNo { get; set; }
    
    [JsonPropertyName("etsoKodu")]
    [JsonProperty("etsoKodu")]
    public string? EtsoKodu { get; set; }
    
    [JsonPropertyName("timeStamp")]
    [JsonProperty("timeStamp")]
    public string? TimeStamp { get; set; }
    
    [JsonPropertyName("generation")]
    [JsonProperty("generation")]
    public string? Generation { get; set; }
    
    [JsonPropertyName("consumption")]
    [JsonProperty("consumption")]
    public string? Consumption { get; set; }
    
    [JsonPropertyName("totalCount")]
    [JsonProperty("totalCount")]
    public string? TotalCount { get; set; }
}

public class HourlyMeterResponse
{
    [JsonPropertyName("status")]
    [JsonProperty("status")]
    public int Status { get; set; }
    
    [JsonPropertyName("message")]
    [JsonProperty("message")]
    public string? Message { get; set; }
    
    [JsonPropertyName("meter_list")]
    [JsonProperty("meter_list")]
    public List<HourlyConsumptionData>? MeterList { get; set; }
    
    [JsonPropertyName("transactionId")]
    [JsonProperty("transactionId")]
    public string? TransactionId { get; set; }
} 