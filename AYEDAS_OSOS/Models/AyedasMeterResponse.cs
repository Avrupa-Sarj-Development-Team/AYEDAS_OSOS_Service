using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace AYEDAS_OSOS.Models;

public class AyedasMeterResponse
{
    [JsonProperty("message")]
    [JsonPropertyName("message")]
    public string? Message { get; set; }
    
    [JsonProperty("items")]
    [JsonPropertyName("items")]
    public Dictionary<string, List<MeterData>>? Items { get; set; }
}

public class MeterData
{
    [JsonProperty("installationNumber")]
    [JsonPropertyName("installationNumber")]
    public string? InstallationNumber { get; set; }
    
    [JsonProperty("meterSerialNo")]
    [JsonPropertyName("meterSerialNo")]
    public string? MeterSerialNo { get; set; }
    
    [JsonProperty("modemSerialNo")]
    [JsonPropertyName("modemSerialNo")]
    public string? ModemSerialNo { get; set; }
    
    [JsonProperty("valueList")]
    [JsonPropertyName("valueList")]
    public List<MeterValue>? ValueList { get; set; }
}

public class MeterValue
{
    [JsonProperty("meterDate")]
    [JsonPropertyName("meterDate")]
    public string? MeterDate { get; set; }
    
    [JsonProperty("activeConsumption")]
    [JsonPropertyName("activeConsumption")]
    public double ActiveConsumption { get; set; }
} 