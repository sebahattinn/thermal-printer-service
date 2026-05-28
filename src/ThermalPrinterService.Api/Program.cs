using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using ThermalPrinterService.Api.Extensions;
using ThermalPrinterService.Api.HealthChecks;
using ThermalPrinterService.Api.Middleware;

var builder = WebApplication.CreateBuilder(args);

// .env değişkenleri (örn. Printer__Lan__Host) ortam değişkeni olarak yüklenir
// docker-compose.yml veya host shell tarafından — burada ek bir paket gerekmiyor,
// .NET Configuration zaten environment variable sağlayıcısını standart olarak kullanır.

builder.Services
    .AddControllers()
    .AddJsonOptions(o =>
    {
        // Enum'lar string olarak serileştirilsin: "Usb", "Ready" vb.
        o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Termal yazıcı omurgası: options, state machine, USB/LAN sürücüleri, factory, session,
// kuyruk, log, composer ve arka plan servisleri.
builder.Services.AddPrinterServices(builder.Configuration);

// Liveness + readiness health checks.
builder.Services.AddHealthChecks()
    .AddCheck<PrinterReadinessCheck>("printer", tags: new[] { "ready" });

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseMiddleware<ApiKeyMiddleware>();

// wwwroot icinden statik UI servisi: /index.html (operator paneli).
// Dev'de tarayicinin eski JS'i cache'lememesi icin Cache-Control no-store.
app.UseDefaultFiles();
app.UseStaticFiles(new StaticFileOptions
{
    OnPrepareResponse = ctx =>
    {
        ctx.Context.Response.Headers["Cache-Control"] = "no-store, no-cache, must-revalidate";
        ctx.Context.Response.Headers["Pragma"] = "no-cache";
        ctx.Context.Response.Headers["Expires"] = "0";
    }
});

app.MapControllers();

// /health -> liveness (her zaman Healthy, servis ayakta).
// /health/ready -> readiness (printer bağlantısı, vb.).
app.MapHealthChecks("/health", new HealthCheckOptions
{
    Predicate = _ => false, // liveness: hicbir check'i calistirma
    ResponseWriter = HealthResponseWriter.WriteJson
});
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = r => r.Tags.Contains("ready"),
    ResponseWriter = HealthResponseWriter.WriteJson
});

app.Run();

// WebApplicationFactory<Program> entegrasyon testleri icin gereklidir.
public partial class Program { }
