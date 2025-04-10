using AYEDAS_OSOS.Services;
using Microsoft.AspNetCore.Diagnostics;
using System.Text.Json;
using Microsoft.OpenApi.Models;
using OfficeOpenXml;
using System.Text;

// EPPlus lisans ayarı
ExcelPackage.LicenseContext = LicenseContext.NonCommercial;

var builder = WebApplication.CreateBuilder(args);

// Logging seviyesini ayarla
builder.Logging.AddConsole();
builder.Logging.SetMinimumLevel(LogLevel.Information);

// Add services to the container.
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.WriteIndented = true;
    });

// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// CORS politikasını ekle
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
            .AllowAnyMethod()
            .AllowAnyHeader();
    });
});

// HTTP Client yapılandırması
builder.Services.AddHttpClient();

// Token yenileme servisini Singleton olarak ekle
builder.Services.AddSingleton<TokenService>();
builder.Services.AddHostedService(provider => provider.GetRequiredService<TokenService>());

// Veritabanı servisini ekle
builder.Services.AddScoped<DatabaseService>();

// ConsumptionController'ı AddScoped olarak ekle
builder.Services.AddScoped<AYEDAS_OSOS.Controllers.ConsumptionController>();

// Excel export servisi ekle
builder.Services.AddScoped<ExcelExportService>();

// Tüketim verisi aktarma servisini ekle
builder.Services.AddHostedService<ConsumptionDataImportService>();

var app = builder.Build();

// Global hata yönetimi
app.UseExceptionHandler(appError =>
{
    appError.Run(async context =>
    {
        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        context.Response.ContentType = "application/json";

        var contextFeature = context.Features.Get<IExceptionHandlerFeature>();
        if (contextFeature != null)
        {
            var logger = app.Services.GetRequiredService<ILogger<Program>>();
            logger.LogError(contextFeature.Error, "Bir hata oluştu");

            await context.Response.WriteAsync(JsonSerializer.Serialize(new
            {
                StatusCode = context.Response.StatusCode,
                Message = "Bir hata oluştu. Detaylar için logları kontrol edin."
            }));
        }
    });
});

// Veritabanı yapılandırmasını kontrol et ve tabloların var olduğundan emin ol
using (var scope = app.Services.CreateScope())
{
    var dbService = scope.ServiceProvider.GetRequiredService<DatabaseService>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    
    try
    {
        logger.LogInformation("Veritabanı yapılandırması kontrol ediliyor...");
        await dbService.EnsureMeterOsosConsumptionTableExistsAsync();
        logger.LogInformation("Veritabanı yapılandırması tamamlandı.");
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Veritabanı yapılandırması sırasında hata oluştu");
    }
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

// CORS politikasını uygula
app.UseCors("AllowAll");


app.UseAuthorization();

app.MapControllers();

app.Run();