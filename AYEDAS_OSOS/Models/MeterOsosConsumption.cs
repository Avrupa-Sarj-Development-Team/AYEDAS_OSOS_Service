using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AYEDAS_OSOS.Models;

[Table("\"MeterOsosConsumption\"")]
public class MeterOsosConsumption
{
    [Key]
    [Column("id")]
    public int Id { get; set; }
    
    [Column("period")]
    public string? Period { get; set; }
    
    [Column("etso")]
    public string? Etso { get; set; }
    
    [Column("tesisatno")]
    public string? TesisatNo { get; set; }
    
    [Column("meterid")]
    public int? MeterId { get; set; }
    
    [Column("distributioncompany")]
    public string DistributionCompany { get; set; } = "AYEDAS";
    
    [Column("year")]
    public int Year { get; set; }
    
    [Column("month")]
    public int Month { get; set; }
    
    [Column("day")]
    public int Day { get; set; }
    
    [Column("hour")]
    public int Hour { get; set; }
    
    [Column("datetime")]
    public DateTime DateTime { get; set; }
    
    [Column("value")]
    public decimal Value { get; set; }
    
    [Column("createdat")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
} 