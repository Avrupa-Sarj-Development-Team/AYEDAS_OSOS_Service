using System.Globalization;
using AYEDAS_OSOS.Models;
using OfficeOpenXml;
using OfficeOpenXml.Style;

namespace AYEDAS_OSOS.Services;

public class ExcelExportService
{
    private readonly ILogger<ExcelExportService> _logger;
    private readonly DatabaseService _databaseService;
    private readonly IConfiguration _configuration;

    public ExcelExportService(
        ILogger<ExcelExportService> logger,
        DatabaseService databaseService,
        IConfiguration configuration)
    {
        _logger = logger;
        _databaseService = databaseService;
        _configuration = configuration;
        
        // EPPlus 6.x için lisans ayarı
        ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
    }

    /// <summary>
    /// Verilen tarih aralığında tüm tesisatlara ait tüketim verilerini Excel'e aktarır
    /// </summary>
    /// <param name="startDate">Başlangıç tarihi</param>
    /// <param name="endDate">Bitiş tarihi</param>
    /// <returns>Excel dosyasının byte array'i</returns>
    public async Task<byte[]> ExportConsumptionDataToExcel(DateTime startDate, DateTime endDate)
    {
        _logger.LogInformation($"Excel export işlemi başlatıldı. Tarih aralığı: {startDate:dd.MM.yyyy} - {endDate:dd.MM.yyyy}");

        try
        {
            // Veritabanından verileri çek
            var consumptionData = await GetConsumptionDataInDateRange(startDate, endDate);
            
            if (consumptionData == null || !consumptionData.Any())
            {
                _logger.LogWarning($"Belirtilen tarih aralığında veri bulunamadı: {startDate:dd.MM.yyyy} - {endDate:dd.MM.yyyy}");
                return CreateEmptyExcel("Belirtilen tarih aralığında veri bulunamadı");
            }
            
            _logger.LogInformation($"Toplam {consumptionData.Count()} adet veri bulundu");
            
            // Excel dosyasını oluştur ve verileri aktar
            return CreateExcelFile(consumptionData, startDate, endDate);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Excel export işlemi sırasında hata oluştu");
            return CreateEmptyExcel($"Hata: {ex.Message}");
        }
    }

    /// <summary>
    /// Belirtilen tarih aralığındaki tüketim verilerini veritabanından çeker
    /// </summary>
    private async Task<IEnumerable<MeterOsosConsumption>> GetConsumptionDataInDateRange(DateTime startDate, DateTime endDate)
    {
        try
        {
            _logger.LogInformation($"Veritabanından tüketim verileri alınıyor. Tarih aralığı: {startDate:dd.MM.yyyy} - {endDate:dd.MM.yyyy}");
            
            string sql = @"
                SELECT * FROM ""MeterOsosConsumption""
                WHERE ""DateTime"" >= @StartDate AND ""DateTime"" <= @EndDate
                ORDER BY ""Etso"", ""TesisatNo"", ""DateTime""
            ";

            var parameters = new 
            {
                StartDate = startDate.Date,
                EndDate = endDate.Date.AddDays(1).AddSeconds(-1) // Bitiş tarihinin son saniyesi
            };
            
            _logger.LogInformation($"SQL sorgusu: {sql}");
            _logger.LogInformation($"Parametre StartDate: {parameters.StartDate:yyyy-MM-dd HH:mm:ss}");
            _logger.LogInformation($"Parametre EndDate: {parameters.EndDate:yyyy-MM-dd HH:mm:ss}");

            var data = await _databaseService.QueryAsync<MeterOsosConsumption>(sql, parameters);
            
            if (data == null || !data.Any())
            {
                _logger.LogWarning("Veritabanında belirtilen tarih aralığında veri bulunamadı");
            }
            else
            {
                _logger.LogInformation($"Toplam {data.Count()} adet veri bulundu");
                
                // Bulunan verilerin genel özeti
                var tesisatCount = data.Select(d => d.TesisatNo).Distinct().Count();
                var etsoCount = data.Select(d => d.Etso).Distinct().Count();
                var dayCount = data.Select(d => d.DateTime.Date).Distinct().Count();
                
                _logger.LogInformation($"Bulunan benzersiz tesisat sayısı: {tesisatCount}");
                _logger.LogInformation($"Bulunan benzersiz ETSO sayısı: {etsoCount}");
                _logger.LogInformation($"Bulunan benzersiz gün sayısı: {dayCount}");
                
                // İlk ve son kaydı göster
                var firstRecord = data.FirstOrDefault();
                var lastRecord = data.LastOrDefault();
                
                if (firstRecord != null)
                {
                    _logger.LogInformation($"İlk kayıt - Tesisat: {firstRecord.TesisatNo}, " +
                                          $"ETSO: {firstRecord.Etso}, " +
                                          $"Tarih: {firstRecord.DateTime:yyyy-MM-dd HH:mm:ss}, " +
                                          $"Değer: {firstRecord.Value}");
                }
                
                if (lastRecord != null)
                {
                    _logger.LogInformation($"Son kayıt - Tesisat: {lastRecord.TesisatNo}, " +
                                          $"ETSO: {lastRecord.Etso}, " +
                                          $"Tarih: {lastRecord.DateTime:yyyy-MM-dd HH:mm:ss}, " +
                                          $"Değer: {lastRecord.Value}");
                }
            }
            
            return data;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Veritabanından tüketim verileri alınırken hata oluştu");
            throw;
        }
    }

    /// <summary>
    /// Boş bir Excel dosyası oluşturur (hata durumlarında kullanılır)
    /// </summary>
    private byte[] CreateEmptyExcel(string message)
    {
        using var package = new ExcelPackage();
        
        // Bilgi sayfası
        var infoSheet = package.Workbook.Worksheets.Add("Bilgi");
        infoSheet.Cells[1, 1].Value = message;
        infoSheet.Cells[1, 1].Style.Font.Bold = true;
        infoSheet.Cells[1, 1].Style.Font.Size = 14;
        infoSheet.Cells[1, 1].Style.Fill.PatternType = ExcelFillStyle.Solid;
        infoSheet.Cells[1, 1].Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.LightYellow);
        
        // Veri sayfası - Resimde gördüğümüz gibi boş bir tablo oluştur
        var dataSheet = package.Workbook.Worksheets.Add("Tüketim Verileri");
        
        // Başlık
        dataSheet.Cells[1, 1, 1, 2].Merge = true;
        dataSheet.Cells[1, 1].Value = "AYEDAS Tüketim Verileri";
        dataSheet.Cells[1, 1].Style.Font.Bold = true;
        dataSheet.Cells[1, 1].Style.Font.Size = 14;
        dataSheet.Cells[1, 1].Style.HorizontalAlignment = ExcelHorizontalAlignment.Left;
        
        // Tarih aralığı
        DateTime now = DateTime.Now;
        DateTime startDate = now.AddDays(-7);
        DateTime endDate = now;
        
        dataSheet.Cells[2, 1, 2, 2].Merge = true;
        dataSheet.Cells[2, 1].Value = $"Tarih Aralığı: {startDate:dd.MM.yyyy} - {endDate:dd.MM.yyyy}";
        dataSheet.Cells[2, 1].Style.Font.Bold = true;
        
        // Oluşturma tarihi
        dataSheet.Cells[3, 1, 3, 2].Merge = true;
        dataSheet.Cells[3, 1].Value = $"Oluşturma Tarihi: {DateTime.Now:dd.MM.yyyy HH:mm:ss}";
        
        // Tablo başlıkları
        // Sınırlarla birlikte tablo oluştur
        int rowStart = 5;
        int columnCount = 6;
        int rowCount = 15;
        
        // Tüm tablo sınırları
        using (var tableRange = dataSheet.Cells[rowStart, 1, rowStart + rowCount, columnCount])
        {
            tableRange.Style.Border.Top.Style = ExcelBorderStyle.Thin;
            tableRange.Style.Border.Left.Style = ExcelBorderStyle.Thin;
            tableRange.Style.Border.Right.Style = ExcelBorderStyle.Thin;
            tableRange.Style.Border.Bottom.Style = ExcelBorderStyle.Thin;
        }
        
        // Sütun genişliklerini ayarla
        dataSheet.Column(1).Width = 15; // Tesisat No
        dataSheet.Column(2).Width = 15; // Tarih
        dataSheet.Column(3).Width = 10; // Saat
        dataSheet.Column(4).Width = 15; // Tüketim (kWh)
        dataSheet.Column(5).Width = 15; // ETSO
        dataSheet.Column(6).Width = 15; // Dağıtım Şirketi
        
        // Sayfayı aktif hale getir
        dataSheet.View.TabSelected = true;
        
        return package.GetAsByteArray();
    }

    /// <summary>
    /// Tüketim verilerinden Excel dosyası oluşturur
    /// </summary>
    private byte[] CreateExcelFile(IEnumerable<MeterOsosConsumption> data, DateTime startDate, DateTime endDate)
    {
        using var package = new ExcelPackage();
        
        // Özet sayfası
        var summarySheet = package.Workbook.Worksheets.Add("Özet");
        summarySheet.Cells[1, 1].Value = "AYEDAS Tüketim Verileri";
        summarySheet.Cells[1, 1].Style.Font.Bold = true;
        summarySheet.Cells[1, 1].Style.Font.Size = 16;
        
        summarySheet.Cells[2, 1].Value = $"Tarih Aralığı: {startDate:dd.MM.yyyy} - {endDate:dd.MM.yyyy}";
        summarySheet.Cells[2, 1].Style.Font.Bold = true;
        
        summarySheet.Cells[3, 1].Value = $"Oluşturma Tarihi: {DateTime.Now:dd.MM.yyyy HH:mm:ss}";
        
        // Tüketim verileri sayfası
        var worksheet = package.Workbook.Worksheets.Add("Tüketim Verileri");
        
        // Başlıkları ekle
        worksheet.Cells[1, 1].Value = "ETSO";
        worksheet.Cells[1, 2].Value = "Tesisat No";
        worksheet.Cells[1, 3].Value = "Tarih";
        worksheet.Cells[1, 4].Value = "Saat";
        worksheet.Cells[1, 5].Value = "Tüketim (kWh)";
        worksheet.Cells[1, 6].Value = "Dağıtım Şirketi";
        worksheet.Cells[1, 7].Value = "Period";
        
        // Başlık stillerini ayarla
        using (var headerRange = worksheet.Cells[1, 1, 1, 7])
        {
            headerRange.Style.Font.Bold = true;
            headerRange.Style.Fill.PatternType = ExcelFillStyle.Solid;
            headerRange.Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.LightGray);
            headerRange.Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
            headerRange.Style.Border.Bottom.Style = ExcelBorderStyle.Medium;
        }
        
        // Verileri ekle
        int row = 2;
        foreach (var item in data)
        {
            worksheet.Cells[row, 1].Value = item.Etso;
            worksheet.Cells[row, 2].Value = item.TesisatNo;
            worksheet.Cells[row, 3].Value = item.DateTime.ToString("dd.MM.yyyy");
            worksheet.Cells[row, 4].Value = item.DateTime.ToString("HH:00");
            worksheet.Cells[row, 5].Value = item.Value;
            worksheet.Cells[row, 6].Value = item.DistributionCompany;
            worksheet.Cells[row, 7].Value = item.Period;
            
            // Tüketim değeri için sayı formatı
            worksheet.Cells[row, 5].Style.Numberformat.Format = "#,##0.00";
            
            row++;
        }
        
        // Sütun genişliklerini otomatik ayarla
        worksheet.Cells.AutoFitColumns();
        
        // Tesisat bazında özet sayfası
        var summaryByInstallation = package.Workbook.Worksheets.Add("Tesisat Özeti");
        CreateInstallationSummary(summaryByInstallation, data);
        
        return package.GetAsByteArray();
    }
    
    /// <summary>
    /// Tesisat bazında özet sayfası oluşturur
    /// </summary>
    private void CreateInstallationSummary(ExcelWorksheet worksheet, IEnumerable<MeterOsosConsumption> data)
    {
        // Başlıkları ekle
        worksheet.Cells[1, 1].Value = "ETSO";
        worksheet.Cells[1, 2].Value = "Tesisat No";
        worksheet.Cells[1, 3].Value = "Toplam Tüketim (kWh)";
        worksheet.Cells[1, 4].Value = "Gün Sayısı";
        worksheet.Cells[1, 5].Value = "Veri Sayısı";
        
        // Başlık stillerini ayarla
        using (var headerRange = worksheet.Cells[1, 1, 1, 5])
        {
            headerRange.Style.Font.Bold = true;
            headerRange.Style.Fill.PatternType = ExcelFillStyle.Solid;
            headerRange.Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.LightGray);
            headerRange.Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
            headerRange.Style.Border.Bottom.Style = ExcelBorderStyle.Medium;
        }
        
        // Tesisat bazında verileri grupla
        var groupedData = data
            .GroupBy(d => new { d.Etso, d.TesisatNo })
            .Select(g => new 
            {
                Etso = g.Key.Etso,
                TesisatNo = g.Key.TesisatNo,
                TotalConsumption = g.Sum(x => x.Value),
                DayCount = g.Select(x => x.DateTime.Date).Distinct().Count(),
                RecordCount = g.Count()
            })
            .OrderBy(g => g.Etso)
            .ThenBy(g => g.TesisatNo)
            .ToList();
        
        // Verileri ekle
        int row = 2;
        foreach (var item in groupedData)
        {
            worksheet.Cells[row, 1].Value = item.Etso;
            worksheet.Cells[row, 2].Value = item.TesisatNo;
            worksheet.Cells[row, 3].Value = item.TotalConsumption;
            worksheet.Cells[row, 4].Value = item.DayCount;
            worksheet.Cells[row, 5].Value = item.RecordCount;
            
            // Tüketim değeri için sayı formatı
            worksheet.Cells[row, 3].Style.Numberformat.Format = "#,##0.00";
            
            row++;
        }
        
        // Sütun genişliklerini otomatik ayarla
        worksheet.Cells.AutoFitColumns();
    }
} 