using DDCSwitch;

namespace DDCSwitch.Tests;

public class EdidParserTests
{
    // Base EDID block of an Acer VG270U P (HKLM\...\Enum\DISPLAY\ACR06CF\...\Device Parameters\EDID).
    private static readonly byte[] AcerEdid =
    [
        0x00, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0x00, 0x04, 0x72, 0xCF, 0x06, 0xEE, 0x6E, 0x21, 0x10,
        0x02, 0x1F, 0x01, 0x04, 0xB5, 0x3C, 0x22, 0x78, 0x3B, 0x93, 0x25, 0xAD, 0x4F, 0x44, 0xA9, 0x26,
        0x0D, 0x50, 0x54, 0xBF, 0xEF, 0x80, 0x71, 0x4F, 0x81, 0x40, 0x81, 0x80, 0x81, 0xC0, 0x81, 0x00,
        0x95, 0x00, 0xB3, 0x00, 0xD1, 0xC0, 0x7E, 0xE5, 0x00, 0xA0, 0xA0, 0xA0, 0x3C, 0x50, 0x08, 0x20,
        0x98, 0x04, 0x55, 0x50, 0x21, 0x00, 0x00, 0x1E, 0x56, 0x5E, 0x00, 0xA0, 0xA0, 0xA0, 0x29, 0x50,
        0x30, 0x20, 0x35, 0x00, 0x55, 0x50, 0x21, 0x00, 0x00, 0x1A, 0x00, 0x00, 0x00, 0xFD, 0x00, 0x28,
        0x90, 0xDE, 0xDE, 0x3C, 0x01, 0x0A, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x00, 0x00, 0x00, 0xFC,
        0x00, 0x56, 0x47, 0x32, 0x37, 0x30, 0x55, 0x20, 0x50, 0x0A, 0x20, 0x20, 0x20, 0x20, 0x01, 0x9C
    ];

    [Fact]
    public void ParsesManufacturerProductAndModel()
    {
        var info = EdidParser.ParseFromBytes(AcerEdid);

        Assert.NotNull(info);
        Assert.Equal("ACR", info.ManufacturerCode);
        Assert.Equal((ushort)0x06CF, info.ProductCode);
        Assert.Equal("VG270U P", EdidParser.ParseModelName(AcerEdid));
        Assert.Equal(2021, info.ManufactureYear);
    }

    [Fact]
    public void ResolvedNameCombinesManufacturerAndModel()
    {
        var info = EdidParser.ParseFromBytes(AcerEdid)!;
        Assert.EndsWith("VG270U P", MonitorNameResolver.BuildEdidName(info));
    }

    [Fact]
    public void RejectsInvalidHeader()
    {
        var bad = (byte[])AcerEdid.Clone();
        bad[1] = 0x00;
        Assert.Null(EdidParser.ParseFromBytes(bad));
        Assert.False(EdidParser.ValidateHeader(bad));
        Assert.True(EdidParser.ValidateHeader(AcerEdid));
    }

    [Fact]
    public void ParsesVersionInputAndManufactureDate()
    {
        Assert.Equal("1.4", EdidParser.ParseEdidVersion(AcerEdid)?.ToString());

        var input = EdidParser.ParseVideoInputDefinition(AcerEdid);
        Assert.NotNull(input);
        Assert.True(input.IsDigital);
        Assert.Equal("Digital", input.ToString());

        Assert.Equal(2, EdidParser.ParseManufactureWeek(AcerEdid));
        Assert.Equal(2021, EdidParser.ParseManufactureYear(AcerEdid));

        var info = EdidParser.ParseFromBytes(AcerEdid, isFromActiveEntry: true)!;
        Assert.Equal(1, info.EdidVersion);
        Assert.Equal(4, info.EdidRevision);
        Assert.Equal("Digital", info.VideoInputDefinition);
        Assert.Equal(2, info.ManufactureWeek);
        Assert.Equal("Acer Technologies", info.ManufacturerName);
        Assert.True(info.IsFromActiveEntry);
    }

    [Fact]
    public void SerialNumber_FallsBackToNumericSerialWhenNoDescriptor()
    {
        Assert.Equal(0x10216EEEu, EdidParser.ParseNumericSerialNumber(AcerEdid));
        Assert.Equal(0x10216EEEu.ToString(), EdidParser.ParseSerialNumber(AcerEdid));
        Assert.Equal(0x10216EEEu, EdidParser.ParseFromBytes(AcerEdid)!.SerialNumber);
    }

    [Fact]
    public void SerialNumber_PrefersDescriptorString()
    {
        var edid = new EdidBuilder().Manufacturer("DEL").NumericSerial(12345).ModelName("U2720Q").SerialString("ABC123XYZ").Build();
        Assert.Equal("ABC123XYZ", EdidParser.ParseSerialNumber(edid));
        Assert.Equal("U2720Q", EdidParser.ParseModelName(edid));
    }

    [Fact]
    public void SerialNumber_IsNullWhenEdidHasNone()
    {
        var edid = new EdidBuilder().Manufacturer("DEL").Build();
        Assert.Null(EdidParser.ParseSerialNumber(edid));
        Assert.Null(EdidParser.ParseModelName(edid));
    }

    [Fact]
    public void ParsesSupportedFeatures()
    {
        // Byte 24 = 0x3B: active-off DPMS, bits 4-3 = 0b11, preferred timing, continuous frequency.
        var features = EdidParser.ParseSupportedFeatures(AcerEdid);
        Assert.NotNull(features);
        Assert.False(features.DpmsStandby);
        Assert.False(features.DpmsSuspend);
        Assert.True(features.DpmsActiveOff);
        Assert.Equal(3, features.DisplayType);
        Assert.False(features.DefaultColorSpace);
        Assert.True(features.PreferredTimingMode);
        Assert.True(features.ContinuousFrequency);
        Assert.Equal(0x3B, features.RawValue);
    }

    [Fact]
    public void DigitalEdid14_DescribesColorEncodings()
    {
        // The Acer is a digital EDID 1.4 panel: bits 4-3 = 0b11 list its color encodings, not a display type.
        var features = EdidParser.ParseSupportedFeatures(AcerEdid)!;
        Assert.True(features.HasColorEncodingFormats);
        Assert.Equal("RGB 4:4:4 + YCrCb 4:4:4 + YCrCb 4:2:2", features.DisplayTypeDescription);
    }

    [Theory]
    [InlineData(0b00, "RGB 4:4:4")]
    [InlineData(0b01, "RGB 4:4:4 + YCrCb 4:4:4")]
    [InlineData(0b10, "RGB 4:4:4 + YCrCb 4:2:2")]
    [InlineData(0b11, "RGB 4:4:4 + YCrCb 4:4:4 + YCrCb 4:2:2")]
    public void DigitalEdid14_MapsEveryColorEncoding(int bits, string expected)
    {
        var edid = new EdidBuilder().Build(); // digital input, EDID 1.4
        edid[24] = (byte)(bits << 3);
        Assert.Equal(expected, EdidParser.ParseSupportedFeatures(edid)!.DisplayTypeDescription);
    }

    [Theory]
    [InlineData(0b00, "Monochrome or Grayscale")]
    [InlineData(0b01, "RGB Color")]
    [InlineData(0b10, "Non-RGB Color")]
    [InlineData(0b11, "Undefined")]
    public void AnalogInput_UsesDisplayTypeTable(int bits, string expected)
    {
        var edid = new EdidBuilder().Build();
        edid[20] = 0x00; // analog input
        edid[24] = (byte)(bits << 3);

        var features = EdidParser.ParseSupportedFeatures(edid)!;
        Assert.False(features.HasColorEncodingFormats);
        Assert.Equal(expected, features.DisplayTypeDescription);
    }

    [Fact]
    public void DigitalEdid13_UsesDisplayTypeTable()
    {
        // Before EDID 1.4, bits 4-3 meant display type for digital inputs too.
        var edid = new EdidBuilder().Build();
        edid[19] = 3; // EDID 1.3
        edid[24] = 0b01 << 3;

        var features = EdidParser.ParseSupportedFeatures(edid)!;
        Assert.False(features.HasColorEncodingFormats);
        Assert.Equal("RGB Color", features.DisplayTypeDescription);
    }

    [Fact]
    public void ParsesChromaticity_PerEdidSpec()
    {
        // Bytes 25-34 of the Acer EDID: 93 25 AD 4F 44 A9 26 0D 50 54.
        // Byte 25 holds the low bits of Rx/Ry/Gx/Gy, byte 26 those of Bx/By/Wx/Wy, bytes 27-34 the high 8 bits.
        var c = EdidParser.ParseChromaticity(AcerEdid);
        Assert.NotNull(c);

        AssertPoint(694 / 1024.0, 317 / 1024.0, c.Red);
        AssertPoint(272 / 1024.0, 679 / 1024.0, c.Green);
        AssertPoint(152 / 1024.0, 54 / 1024.0, c.Blue);
        AssertPoint(321 / 1024.0, 337 / 1024.0, c.White);

        // Sanity: this panel's white point is D65 (0.3127, 0.3290).
        Assert.Equal(0.3127, c.White.X, 0.002);
        Assert.Equal(0.3290, c.White.Y, 0.002);
    }

    [Fact]
    public void ParsedInfo_CarriesChromaticity()
    {
        var color = EdidParser.ParseFromBytes(AcerEdid)!.ColorInfo;
        Assert.Equal(321 / 1024f, color.WhitePointX, 0.0001f);
        Assert.Equal(337 / 1024f, color.WhitePointY, 0.0001f);
        Assert.Equal(694 / 1024f, color.RedX, 0.0001f);
        Assert.Equal(317 / 1024f, color.RedY, 0.0001f);
    }

    [Theory]
    [InlineData("ACR", "Acer Technologies")]
    [InlineData("DEL", "Dell Inc.")]
    [InlineData("GSM", "LG Electronics")]
    [InlineData("QQQ", "QQQ")]
    [InlineData("", "Unknown")]
    [InlineData(null, "Unknown")]
    public void MapsManufacturerNames(string? pnpId, string expected)
    {
        Assert.Equal(expected, EdidParser.GetManufacturerName(pnpId));
    }

    [Theory]
    [InlineData("SAM")]
    [InlineData("GSM")]
    [InlineData("ZZZ")]
    public void ManufacturerIdRoundTripsThroughBuilder(string pnpId)
    {
        Assert.Equal(pnpId, EdidParser.ParseManufacturerId(new EdidBuilder().Manufacturer(pnpId).Build()));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(54)]
    [InlineData(255)]
    public void InvalidManufactureWeek_IsNull(int week)
    {
        var edid = new EdidBuilder().Manufactured(1, 2020).Build();
        edid[16] = (byte)week;
        Assert.Null(EdidParser.ParseManufactureWeek(edid));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(8)]
    [InlineData(10)]
    [InlineData(20)]
    [InlineData(127)]
    public void TruncatedEdid_ReturnsNullWithoutThrowing(int length)
    {
        var edid = AcerEdid[..length];

        Assert.Null(EdidParser.ParseFromBytes(edid));
        Assert.Null(EdidParser.ParseModelName(edid));
        Assert.Null(EdidParser.ParseSerialNumber(edid));
        if (length < 35) Assert.Null(EdidParser.ParseChromaticity(edid));
        if (length < 25) Assert.Null(EdidParser.ParseSupportedFeatures(edid));
        if (length < 20) Assert.Null(EdidParser.ParseEdidVersion(edid));
        if (length < 10) Assert.Null(EdidParser.ParseManufacturerId(edid));
    }

    [Fact]
    public void ModelName_StopsAtNewlineAndTrims()
    {
        var edid = new EdidBuilder().Manufacturer("ACR").ModelName("  XB271HU ").Build();
        Assert.Equal("XB271HU", EdidParser.ParseModelName(edid));
    }

    private static void AssertPoint(double expectedX, double expectedY, ColorPoint actual)
    {
        Assert.Equal(expectedX, actual.X, 0.0001);
        Assert.Equal(expectedY, actual.Y, 0.0001);
    }
}
