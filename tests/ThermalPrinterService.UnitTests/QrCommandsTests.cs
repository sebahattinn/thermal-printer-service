using System.Text;
using ThermalPrinterService.Infrastructure.Printers;

namespace ThermalPrinterService.UnitTests;

public sealed class QrCommandsTests
{
    [Fact]
    public void SelectModel_emits_GS_paren_k_04_00_31_41_n_00()
    {
        Assert.Equal(
            new byte[] { 0x1D, 0x28, 0x6B, 0x04, 0x00, 0x31, 0x41, 50, 0x00 },
            QrCommands.SelectModel(QrModel.Model2));
        Assert.Equal(
            new byte[] { 0x1D, 0x28, 0x6B, 0x04, 0x00, 0x31, 0x41, 49, 0x00 },
            QrCommands.SelectModel(QrModel.Model1));
    }

    [Fact]
    public void SetModuleSize_emits_GS_paren_k_03_00_31_43_n()
    {
        Assert.Equal(
            new byte[] { 0x1D, 0x28, 0x6B, 0x03, 0x00, 0x31, 0x43, 6 },
            QrCommands.SetModuleSize(6));
    }

    [Theory]
    [InlineData(QrErrorCorrection.L, (byte)48)]
    [InlineData(QrErrorCorrection.M, (byte)49)]
    [InlineData(QrErrorCorrection.Q, (byte)50)]
    [InlineData(QrErrorCorrection.H, (byte)51)]
    public void SetErrorCorrection_emits_GS_paren_k_03_00_31_45_n(QrErrorCorrection level, byte expected)
    {
        Assert.Equal(
            new byte[] { 0x1D, 0x28, 0x6B, 0x03, 0x00, 0x31, 0x45, expected },
            QrCommands.SetErrorCorrection(level));
    }

    [Fact]
    public void StoreData_encodes_pL_pH_as_data_length_plus_three()
    {
        var data = Encoding.UTF8.GetBytes("ACO");
        var b = QrCommands.StoreData(data);

        // header: 1D 28 6B pL pH 31 50 30 + data
        Assert.Equal(0x1D, b[0]);
        Assert.Equal(0x28, b[1]);
        Assert.Equal(0x6B, b[2]);

        // pL+pH*256 = data.Length + 3 = 6
        Assert.Equal(6, b[3] + (b[4] << 8));

        Assert.Equal(0x31, b[5]); // cn
        Assert.Equal(0x50, b[6]); // fn (P)
        Assert.Equal(0x30, b[7]); // m
        Assert.Equal((byte)'A', b[8]);
        Assert.Equal((byte)'C', b[9]);
        Assert.Equal((byte)'O', b[10]);
        Assert.Equal(11, b.Length);
    }

    [Fact]
    public void StoreData_handles_long_data_with_two_byte_length()
    {
        // 300 byte → fullLen=303, pL=303%256=47, pH=303/256=1
        var data = new byte[300];
        var b = QrCommands.StoreData(data);

        Assert.Equal(303, b[3] + (b[4] << 8));
        Assert.Equal(308, b.Length); // 8 header + 300 data
    }

    [Fact]
    public void PrintBuffer_emits_GS_paren_k_03_00_31_51_30()
    {
        Assert.Equal(
            new byte[] { 0x1D, 0x28, 0x6B, 0x03, 0x00, 0x31, 0x51, 0x30 },
            QrCommands.PrintBuffer);
    }

    [Fact]
    public void Build_concatenates_all_five_subcommands_in_order()
    {
        var bytes = QrCommands.Build("X", moduleSize: 4, ecc: QrErrorCorrection.M, model: QrModel.Model2);

        // 9 (model) + 8 (size) + 8 (ecc) + 9 (store: 8 hdr + 1 data) + 8 (print) = 42
        Assert.Equal(42, bytes.Length);

        // Sub-blocks başlangıçları kontrol — her biri 1D 28 6B ile başlamalı
        Assert.Equal(0x1D, bytes[0]);
        Assert.Equal(0x1D, bytes[9]);
        Assert.Equal(0x1D, bytes[17]);
        Assert.Equal(0x1D, bytes[25]);
        Assert.Equal(0x1D, bytes[34]);

        // ECC seviyesi M = 49 (4. blok, offset 17, içeride n offset 17+7=24)
        Assert.Equal(49, bytes[24]);
    }

    [Fact]
    public void Build_with_url_produces_valid_data_chain_for_aco_receipt()
    {
        // ACO fişi gibi gerçek bir QR datası
        var url = "https://aco.test/receipt/12345";
        var bytes = QrCommands.Build(url);

        // İlk komut model seçimi, son komut Print Buffer
        Assert.Equal(0x1D, bytes[0]);
        // Son 8 byte = Print buffer komutu
        var last8 = bytes[^8..];
        Assert.Equal(QrCommands.PrintBuffer, last8);
    }

    [Theory]
    [InlineData((byte)0)]
    [InlineData((byte)17)]
    public void Build_rejects_module_size_out_of_range(byte size)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => QrCommands.Build("x", moduleSize: size));
    }

    [Fact]
    public void Build_rejects_empty_data()
    {
        Assert.Throws<ArgumentException>(() => QrCommands.Build(""));
    }
}
