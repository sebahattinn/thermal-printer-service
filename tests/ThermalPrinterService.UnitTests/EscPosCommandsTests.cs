using System.Text;
using ThermalPrinterService.Domain.Enums;
using ThermalPrinterService.Infrastructure.Printers;

namespace ThermalPrinterService.UnitTests;

public sealed class EscPosCommandsTests
{
    // ----- Status parser tests -----

    [Fact]
    public void ParsePrinterStatus_returns_null_because_it_does_not_map_to_a_specific_state()
    {
        // DLE EOT 1 yalnızca online/offline der; sebep verdiği için Ready'i bozmaz.
        Assert.Null(EscPosCommands.ParsePrinterStatus(0x00));
        Assert.Null(EscPosCommands.ParsePrinterStatus(0x12)); // base bits set
        Assert.Null(EscPosCommands.ParsePrinterStatus(0xFF));
    }

    [Theory]
    [InlineData((byte)0x00, null)]
    [InlineData((byte)0x12, null)]
    [InlineData((byte)0x04, PrinterState.CoverOpen)]
    [InlineData((byte)0x16, PrinterState.CoverOpen)] // cover + base bits
    public void ParseOfflineStatus_maps_cover_open(byte status, PrinterState? expected)
    {
        Assert.Equal(expected, EscPosCommands.ParseOfflineStatus(status));
    }

    [Theory]
    [InlineData((byte)0x00, null)]
    [InlineData((byte)0x12, null)]
    [InlineData((byte)0x08, PrinterState.PaperJam)]      // cutter
    [InlineData((byte)0x40, PrinterState.Overheat)]      // print head over heat
    [InlineData((byte)0x20, PrinterState.CommError)]     // unrecoverable
    [InlineData((byte)0x28, PrinterState.CommError)]     // unrecoverable + cutter -> unrecoverable wins
    [InlineData((byte)0x48, PrinterState.PaperJam)]      // cutter + overheat -> cutter wins
    public void ParseErrorStatus_maps_cutter_overheat_unrecoverable(byte status, PrinterState? expected)
    {
        Assert.Equal(expected, EscPosCommands.ParseErrorStatus(status));
    }

    [Theory]
    [InlineData((byte)0x00, null)]
    [InlineData((byte)0x12, null)]
    [InlineData((byte)0x0C, null)]                       // paper near-end (warning only)
    [InlineData((byte)0x60, PrinterState.PaperOut)]      // paper end
    [InlineData((byte)0x6C, PrinterState.PaperOut)]      // paper end + near-end -> end wins
    public void ParsePaperSensorStatus_maps_paper_out(byte status, PrinterState? expected)
    {
        Assert.Equal(expected, EscPosCommands.ParsePaperSensorStatus(status));
    }

    // ----- CombineStates priority order -----

    [Fact]
    public void CombineStates_returns_Ready_when_all_inputs_null()
    {
        Assert.Equal(PrinterState.Ready, EscPosCommands.CombineStates(null, null, null, null));
    }

    [Fact]
    public void CombineStates_picks_most_severe_state()
    {
        var result = EscPosCommands.CombineStates(
            null,
            PrinterState.CoverOpen,
            PrinterState.Overheat,
            PrinterState.PaperOut);

        // PaperOut > CoverOpen > Overheat by severity table
        Assert.Equal(PrinterState.PaperOut, result);
    }

    [Fact]
    public void CombineStates_CommError_beats_PaperOut()
    {
        var result = EscPosCommands.CombineStates(
            null, null, PrinterState.CommError, PrinterState.PaperOut);
        Assert.Equal(PrinterState.CommError, result);
    }

    [Fact]
    public void CombineStates_with_no_inputs_returns_Ready()
    {
        Assert.Equal(PrinterState.Ready, EscPosCommands.CombineStates());
    }

    // ----- Formatlama komutu byte sıraları -----

    [Theory]
    [InlineData(EscPosCommands.Alignment.Left, (byte)0)]
    [InlineData(EscPosCommands.Alignment.Center, (byte)1)]
    [InlineData(EscPosCommands.Alignment.Right, (byte)2)]
    public void SetAlignment_emits_ESC_a_n(EscPosCommands.Alignment a, byte expectedN)
    {
        var b = EscPosCommands.SetAlignment(a);
        Assert.Equal(new byte[] { 0x1B, 0x61, expectedN }, b);
    }

    [Theory]
    [InlineData(true, (byte)1)]
    [InlineData(false, (byte)0)]
    public void SetBold_emits_ESC_E_n(bool on, byte expectedN)
    {
        Assert.Equal(new byte[] { 0x1B, 0x45, expectedN }, EscPosCommands.SetBold(on));
    }

    [Theory]
    [InlineData(EscPosCommands.UnderlineMode.Off, (byte)0)]
    [InlineData(EscPosCommands.UnderlineMode.OneDot, (byte)1)]
    [InlineData(EscPosCommands.UnderlineMode.TwoDot, (byte)2)]
    public void SetUnderline_emits_ESC_dash_n(EscPosCommands.UnderlineMode mode, byte expectedN)
    {
        Assert.Equal(new byte[] { 0x1B, 0x2D, expectedN }, EscPosCommands.SetUnderline(mode));
    }

    [Theory]
    [InlineData((byte)1, (byte)1, (byte)0x00)]   // 1x1 -> 0x00
    [InlineData((byte)2, (byte)2, (byte)0x11)]   // 2x2 -> width=1<<4 | height=1
    [InlineData((byte)8, (byte)8, (byte)0x77)]   // 8x8 -> 7<<4 | 7
    [InlineData((byte)4, (byte)1, (byte)0x30)]   // 4x1
    public void SetTextSize_encodes_high_nibble_width_low_nibble_height(byte w, byte h, byte expected)
    {
        var bytes = EscPosCommands.SetTextSize(w, h);
        Assert.Equal(new byte[] { 0x1D, 0x21, expected }, bytes);
    }

    [Theory]
    [InlineData((byte)0, (byte)9)]   // both ranges out
    [InlineData((byte)9, (byte)1)]
    [InlineData((byte)1, (byte)0)]
    public void SetTextSize_rejects_out_of_range(byte w, byte h)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => EscPosCommands.SetTextSize(w, h));
    }

    [Fact]
    public void SetCodePage_emits_ESC_t_n_for_CP1254_Turkish()
    {
        // KP-300 manuel sayfa 36: n=32 -> WCP1254 (Turkish)
        Assert.Equal(new byte[] { 0x1B, 0x74, 32 }, EscPosCommands.SetCodePage(32));
    }

    [Fact]
    public void FeedLines_emits_ESC_d_n()
        => Assert.Equal(new byte[] { 0x1B, 0x64, 5 }, EscPosCommands.FeedLines(5));

    [Fact]
    public void FeedDots_emits_ESC_J_n()
        => Assert.Equal(new byte[] { 0x1B, 0x4A, 24 }, EscPosCommands.FeedDots(24));

    [Fact]
    public void FeedAndCut_emits_GS_V_66_n()
        => Assert.Equal(new byte[] { 0x1D, 0x56, 0x42, 10 }, EscPosCommands.FeedAndCut(10));

    // ----- Yüksek seviye yardımcılar (BuildText, WrapImagePayload) korunduğu için
    //       eski iki testi de yeni API ile yeniden yazdık. -----

    [Fact]
    public void BuildText_starts_with_init_and_ends_with_cut()
    {
        var bytes = EscPosCommands.BuildText("hi", Encoding.UTF8);
        Assert.Equal(0x1B, bytes[0]);
        Assert.Equal(0x40, bytes[1]);
        Assert.Equal((byte)'h', bytes[2]);
        Assert.Equal((byte)'i', bytes[3]);
        Assert.Equal(0x0A, bytes[4]);
        Assert.Equal(0x1D, bytes[5]);
        Assert.Equal(0x56, bytes[6]);
        Assert.Equal(0x00, bytes[7]);
        Assert.Equal(8, bytes.Length);
    }

    [Fact]
    public void WrapImagePayload_preserves_payload_between_init_and_cut()
    {
        var payload = new byte[] { 0xAA, 0xBB, 0xCC };
        var bytes = EscPosCommands.WrapImagePayload(payload);
        Assert.Equal(new byte[] { 0x1B, 0x40, 0xAA, 0xBB, 0xCC, 0x0A, 0x1D, 0x56, 0x00 }, bytes);
    }
}
