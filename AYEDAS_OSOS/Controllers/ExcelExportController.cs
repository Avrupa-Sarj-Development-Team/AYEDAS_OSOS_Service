using AYEDAS_OSOS.Services;
using Microsoft.AspNetCore.Mvc;
using OfficeOpenXml;

namespace AYEDAS_OSOS.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ExcelExportController : ControllerBase
{
    private readonly ILogger<ExcelExportController> _logger;
    private readonly ExcelExportService _excelExportService;
    private readonly IConfiguration _configuration;

    public ExcelExportController(
        ILogger<ExcelExportController> logger,
        ExcelExportService excelExportService,
        IConfiguration configuration)
    {
        _logger = logger;
        _excelExportService = excelExportService;
        _configuration = configuration;
    }

    /// <summary>
    /// Belirtilen tarih aralığındaki tüketim verilerini Excel formatında indirir
    /// </summary>
    /// <param name="startDate">Başlangıç tarihi (GG.AA.YYYY)</param>
    /// <param name="endDate">Bitiş tarihi (GG.AA.YYYY)</param>
    /// <returns>Excel dosyası</returns>
    [HttpGet("DownloadConsumptionData")]
    public async Task<IActionResult> DownloadConsumptionData(
        [FromQuery] string startDate,
        [FromQuery] string endDate)
    {
        try
        {
            _logger.LogInformation($"Excel dışa aktarma isteği alındı. Tarih aralığı: {startDate} - {endDate}");
            
            // Tarih formatlarını kontrol et ve dönüştür
            if (!DateTime.TryParseExact(startDate, "dd.MM.yyyy", null, System.Globalization.DateTimeStyles.None, out DateTime parsedStartDate))
            {
                _logger.LogWarning($"Geçersiz başlangıç tarihi formatı: {startDate}");
                return BadRequest(new { message = "Başlangıç tarihi geçerli bir format değil. Lütfen GG.AA.YYYY formatında giriniz." });
            }

            if (!DateTime.TryParseExact(endDate, "dd.MM.yyyy", null, System.Globalization.DateTimeStyles.None, out DateTime parsedEndDate))
            {
                _logger.LogWarning($"Geçersiz bitiş tarihi formatı: {endDate}");
                return BadRequest(new { message = "Bitiş tarihi geçerli bir format değil. Lütfen GG.AA.YYYY formatında giriniz." });
            }
            
            _logger.LogInformation($"Tarihler başarıyla parse edildi: {parsedStartDate:yyyy-MM-dd} - {parsedEndDate:yyyy-MM-dd}");

            // Tarih aralığını kontrol et
            if (parsedEndDate < parsedStartDate)
            {
                _logger.LogWarning($"Bitiş tarihi başlangıç tarihinden önce: {parsedStartDate:yyyy-MM-dd} > {parsedEndDate:yyyy-MM-dd}");
                return BadRequest(new { message = "Bitiş tarihi başlangıç tarihinden önce olamaz." });
            }

            // Maximum tarih aralığını kontrol et (Örn: 31 günden fazla sorgulanmasını engelle)
            var maxDateRange = _configuration.GetValue<int>("ExcelExportSettings:MaxDateRangeInDays", 31);
            if ((parsedEndDate - parsedStartDate).TotalDays > maxDateRange)
            {
                _logger.LogWarning($"Maksimum tarih aralığı aşıldı: {(parsedEndDate - parsedStartDate).TotalDays} gün > {maxDateRange} gün");
                return BadRequest(new { message = $"Maksimum {maxDateRange} günlük veri çekilebilir. Lütfen tarih aralığını daraltın." });
            }

            _logger.LogInformation($"Excel export isteği başlatıldı. Tarih aralığı: {parsedStartDate:dd.MM.yyyy} - {parsedEndDate:dd.MM.yyyy}");

            // Excel verisini oluştur
            var excelData = await _excelExportService.ExportConsumptionDataToExcel(parsedStartDate, parsedEndDate);
            
            _logger.LogInformation($"Excel verisi oluşturuldu. Veri boyutu: {excelData?.Length ?? 0} byte");

            // Dosya adını oluştur
            string fileName = $"AYEDAS_Tuketim_{parsedStartDate:ddMMyyyy}_{parsedEndDate:ddMMyyyy}.xlsx";
            
            _logger.LogInformation($"Excel dosyası oluşturuldu: {fileName}");

            // Excel dosyasını döndür
            return File(excelData, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Excel export işlemi sırasında hata oluştu");
            return StatusCode(500, new { message = $"Excel dosyası oluşturulurken bir hata oluştu: {ex.Message}" });
        }
    }

    /// <summary>
    /// Test amaçlı basit bir Excel dosyası oluşturur ve indirir
    /// </summary>
    [HttpGet("TestExcel")]
    public IActionResult TestExcel()
    {
        try
        {
            _logger.LogInformation("Test Excel dosyası oluşturuluyor...");
            
            using var package = new ExcelPackage();
            var worksheet = package.Workbook.Worksheets.Add("Test");
            
            // Başlık ekle
            worksheet.Cells[1, 1].Value = "Test Excel Dosyası";
            worksheet.Cells[1, 1].Style.Font.Bold = true;
            worksheet.Cells[1, 1].Style.Font.Size = 16;
            
            // Basit bir tablo oluştur
            worksheet.Cells[3, 1].Value = "Sıra No";
            worksheet.Cells[3, 2].Value = "Tarih";
            worksheet.Cells[3, 3].Value = "Değer";
            
            // Başlık stillerini ayarla
            using (var headerRange = worksheet.Cells[3, 1, 3, 3])
            {
                headerRange.Style.Font.Bold = true;
                headerRange.Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
                headerRange.Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.LightGray);
            }
            
            // Örnek veriler ekle
            for (int i = 0; i < 10; i++)
            {
                worksheet.Cells[i + 4, 1].Value = i + 1;
                worksheet.Cells[i + 4, 2].Value = DateTime.Now.AddDays(i).ToString("dd.MM.yyyy");
                worksheet.Cells[i + 4, 3].Value = Math.Round(new Random().NextDouble() * 100, 2);
            }
            
            // Sütun genişliklerini ayarla
            worksheet.Cells.AutoFitColumns();
            
            // Excel dosyasını byte array olarak al
            byte[] fileBytes = package.GetAsByteArray();
            
            // Dosya olarak döndür
            return File(fileBytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "TestExcel.xlsx");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Test Excel dosyası oluşturulurken hata oluştu");
            return StatusCode(500, new { message = $"Excel dosyası oluşturulurken bir hata oluştu: {ex.Message}" });
        }
    }

    /// <summary>
    /// Veritabanında MeterOsosConsumption tablosundaki veri sayısını kontrol eder
    /// </summary>
    [HttpGet("TestDatabaseConnection")]
    public async Task<IActionResult> TestDatabaseConnection()
    {
        try
        {
            _logger.LogInformation("Veritabanı bağlantısı test ediliyor...");
            
            // Veritabanı servisini oluştur
            var scope = HttpContext.RequestServices.CreateScope();
            var dbService = scope.ServiceProvider.GetRequiredService<DatabaseService>();
            
            // Tabloda veri olup olmadığını kontrol et
            string countSql = @"SELECT COUNT(*) FROM ""MeterOsosConsumption""";
            int recordCount = await dbService.QuerySingleOrDefaultAsync<int>(countSql);
            
            _logger.LogInformation($"MeterOsosConsumption tablosunda {recordCount} kayıt bulundu.");
            
            // Örnek kayıtları getir (son 10 kayıt)
            string recentRecordsSql = @"
                SELECT * FROM ""MeterOsosConsumption""
                ORDER BY ""DateTime"" DESC
                LIMIT 10
            ";
            
            var recentRecords = await dbService.QueryAsync<dynamic>(recentRecordsSql);
            
            // Tablo yapısını kontrol et
            string tableStructureSql = @"
                SELECT column_name, data_type, is_nullable 
                FROM information_schema.columns 
                WHERE table_name = 'MeterOsosConsumption'
                ORDER BY ordinal_position;
            ";
            
            var tableStructure = await dbService.QueryAsync<dynamic>(tableStructureSql);
            
            return Ok(new { 
                message = "Veritabanı bağlantısı başarılı", 
                connectionString = dbService.GetConnectionString(),
                recordCount,
                tableStructure,
                recentRecords
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Veritabanı bağlantısı test edilirken hata oluştu");
            return StatusCode(500, new { message = $"Veritabanı bağlantısı test edilirken hata oluştu: {ex.Message}" });
        }
    }
} 