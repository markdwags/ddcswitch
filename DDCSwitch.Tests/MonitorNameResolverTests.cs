using DDCSwitch;

namespace DDCSwitch.Tests;

public class MonitorNameResolverTests
{
    private static ParsedEdidInfo Parse(EdidBuilder builder) => EdidParser.ParseFromBytes(builder.Build())!;

    [Fact]
    public void CombinesManufacturerNameAndModel()
    {
        var info = Parse(new EdidBuilder().Manufacturer("DEL").ModelName("DELL U2720Q"));
        // Model already starts with the brand's first word, so the brand isn't repeated.
        Assert.Equal("DELL U2720Q", MonitorNameResolver.BuildEdidName(info));
    }

    [Fact]
    public void DropsDuplicatedBrand()
    {
        var info = Parse(new EdidBuilder().Manufacturer("GSM").ModelName("LG ULTRAGEAR"));
        Assert.Equal("LG ULTRAGEAR", MonitorNameResolver.BuildEdidName(info));
    }

    [Fact]
    public void KeepsBrandWhenModelDoesNotRepeatIt()
    {
        var info = Parse(new EdidBuilder().Manufacturer("SAM").ModelName("Odyssey G7"));
        Assert.Equal("Samsung Electric Company Odyssey G7", MonitorNameResolver.BuildEdidName(info));
    }

    [Fact]
    public void UnknownManufacturer_UsesPnpCode()
    {
        var info = Parse(new EdidBuilder().Manufacturer("QQQ").ModelName("X1"));
        Assert.Equal("QQQ X1", MonitorNameResolver.BuildEdidName(info));
    }

    [Fact]
    public void NoModelName_FallsBackToProductCode()
    {
        var info = Parse(new EdidBuilder().Manufacturer("GSM").ProductCode(0x5BB1));
        Assert.Equal("LG Electronics 0x5BB1", MonitorNameResolver.BuildEdidName(info));
    }

    [Fact]
    public void ResolveMonitorName_PrefersEdidOverWindowsDescription()
    {
        using var monitor = TestMonitor.Create(0, "Some Windows Name");
        var info = Parse(new EdidBuilder().Manufacturer("ACR").ModelName("VG270U P"));

        Assert.Equal("Acer Technologies VG270U P", MonitorNameResolver.ResolveMonitorName(monitor, info));
        Assert.Equal("Some Windows Name", MonitorNameResolver.ResolveMonitorName(monitor, null));
    }
}
