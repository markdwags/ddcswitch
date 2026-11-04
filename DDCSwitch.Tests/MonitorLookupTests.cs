using DDCSwitch;

namespace DDCSwitch.Tests;

public class MonitorLookupTests : IDisposable
{
    private readonly List<Monitor> _monitors =
    [
        TestMonitor.Create(0, "Dell U2720Q", deviceName: @"\\.\DISPLAY1", hardwareId: "TST0001", isPrimary: true),
        TestMonitor.Create(1, "Generic PnP Monitor", deviceName: @"\\.\DISPLAY2", hardwareId: "TST0002"),
    ];

    public void Dispose()
    {
        foreach (var m in _monitors) m.Dispose();
    }

    [Theory]
    [InlineData("0", 0)]
    [InlineData("1", 1)]
    [InlineData("dell", 0)]             // name (from Windows description), case-insensitive
    [InlineData("U2720", 0)]
    [InlineData("monitor 1", 1)]        // fallback name
    [InlineData("DISPLAY2", 1)]         // display device name
    [InlineData("tst0002", 1)]          // hardware ID
    [InlineData("generic pnp", 1)]      // Windows description
    public void FindsMonitorByAnyIdentifier(string identifier, int expectedIndex)
    {
        var found = MonitorController.FindMonitor(_monitors, identifier);
        Assert.NotNull(found);
        Assert.Equal(expectedIndex, found.Index);
    }

    [Theory]
    [InlineData("nope")]
    [InlineData("2")] // numeric ids only match indexes, even though "\\.\DISPLAY2" contains "2"
    [InlineData("-1")]
    public void ReturnsNullWhenNothingMatches(string identifier)
    {
        Assert.Null(MonitorController.FindMonitor(_monitors, identifier));
    }

    [Fact]
    public void MonitorWithoutEdid_ParsesHardwareIdButHasNoEdidData()
    {
        var monitor = _monitors[0];
        Assert.Equal("TST0001", monitor.HardwareId);
        Assert.Null(monitor.ParsedEdid);
        Assert.Null(monitor.ModelName);
        Assert.True(monitor.IsPrimary);
    }

    [Fact]
    public void FallbackName_PrefersWindowsDescription()
    {
        Assert.Equal("Dell U2720Q", _monitors[0].Name);
    }

    [Fact]
    public void FallbackName_IgnoresGenericPnpMonitor()
    {
        Assert.Equal("Monitor 1", _monitors[1].Name);
    }

    [Fact]
    public void FallbackName_UsesIndexWhenDescriptionIsEmpty()
    {
        using var monitor = TestMonitor.Create(3, windowsDescription: "");
        Assert.Equal("Monitor 3", monitor.Name);
        Assert.Null(monitor.HardwareId);
    }
}
