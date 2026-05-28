using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace ThermalPrinterService.UnitTests;

/// <summary>
/// Test icin yalin IOptionsMonitor uygulamasi; OnChange callback'i kullanilmaz.
/// </summary>
internal sealed class TestOptionsMonitor<T> : IOptionsMonitor<T> where T : class
{
    public T CurrentValue { get; }
    public T Get(string? name) => CurrentValue;
    public IDisposable? OnChange(Action<T, string?> listener) => null;
    public TestOptionsMonitor(T value) { CurrentValue = value; }
}

internal sealed class TestHostEnvironment : IHostEnvironment
{
    public string EnvironmentName { get; set; } = "Test";
    public string ApplicationName { get; set; } = "ThermalPrinterService.Tests";
    public string ContentRootPath { get; set; } = Path.GetTempPath();
    public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
}
