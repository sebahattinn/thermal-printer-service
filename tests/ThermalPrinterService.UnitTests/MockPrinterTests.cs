using Microsoft.Extensions.Logging.Abstractions;
using ThermalPrinterService.Domain.Enums;
using ThermalPrinterService.Infrastructure.Printers;

namespace ThermalPrinterService.UnitTests;

public sealed class MockPrinterTests
{
    private static MockPrinter NewPrinter() => new(NullLogger<MockPrinter>.Instance);

    [Fact]
    public void Mode_is_Mock()
    {
        Assert.Equal(ConnectionMode.Mock, NewPrinter().Mode);
    }

    [Fact]
    public void Starts_disconnected()
    {
        var p = NewPrinter();
        Assert.False(p.IsConnected);
        Assert.Equal(PrinterState.Disconnected, p.CurrentState);
    }

    [Fact]
    public async Task Connect_always_succeeds_and_sets_Ready()
    {
        var p = NewPrinter();
        var ok = await p.ConnectAsync(default);

        Assert.True(ok);
        Assert.True(p.IsConnected);
        Assert.Equal(PrinterState.Ready, p.CurrentState);
    }

    [Fact]
    public async Task QueryState_returns_Ready_after_connect()
    {
        var p = NewPrinter();
        await p.ConnectAsync(default);

        var state = await p.QueryStateAsync(default);
        Assert.Equal(PrinterState.Ready, state);
    }

    [Fact]
    public async Task Disconnect_returns_to_Disconnected()
    {
        var p = NewPrinter();
        await p.ConnectAsync(default);
        await p.DisconnectAsync(default);

        Assert.False(p.IsConnected);
        Assert.Equal(PrinterState.Disconnected, p.CurrentState);
    }

    [Fact]
    public async Task Print_methods_increment_counters()
    {
        var p = NewPrinter();
        await p.ConnectAsync(default);

        await p.PrintTextAsync("hello", default);
        await p.PrintTextAsync("world", default);
        await p.PrintImageAsync(new byte[] { 1, 2, 3 }, default);
        await p.PrintRawAsync(new byte[] { 9, 9 }, default);

        Assert.Equal(2, p.PrintedTextCount);
        Assert.Equal(1, p.PrintedImageCount);
        Assert.Equal(1, p.PrintedRawCount);
    }
}
