using DDCSwitch;

namespace DDCSwitch.Tests;

public class DisplayDeviceResolverTests
{
    // Real device interface paths captured from EnumDisplayDevices(EDD_GET_DEVICE_INTERFACE_NAME).
    [Theory]
    [InlineData(@"\\?\DISPLAY#ACR06CF#5&39152f42&0&UID4353#{e6f07b5f-ee97-4a90-b076-33f57bf4eaa7}", "ACR06CF", "5&39152f42&0&UID4353")]
    [InlineData(@"\\?\DISPLAY#SKG2410#5&39152f42&0&UID4352#{e6f07b5f-ee97-4a90-b076-33f57bf4eaa7}", "SKG2410", "5&39152f42&0&UID4352")]
    [InlineData(@"\\?\display#GSM5BB1#5&39152f42&0&UID4356#{e6f07b5f-ee97-4a90-b076-33f57bf4eaa7}", "GSM5BB1", "5&39152f42&0&UID4356")]
    public void ParsesDevicePath(string path, string expectedHardwareId, string expectedInstance)
    {
        Assert.True(DisplayDeviceResolver.TryParseDevicePath(path, out var hardwareId, out var instance));
        Assert.Equal(expectedHardwareId, hardwareId);
        Assert.Equal(expectedInstance, instance);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(@"\\.\DISPLAY1")]
    [InlineData(@"\\?\PCI#VEN_10DE#whatever#{guid}")]
    [InlineData(@"\\?\DISPLAY##instance#{guid}")]
    public void RejectsNonMonitorPaths(string? path)
    {
        Assert.False(DisplayDeviceResolver.TryParseDevicePath(path, out _, out _));
    }

    [Fact]
    public void BuildsRegistryKeyPath()
    {
        Assert.Equal(
            @"SYSTEM\CurrentControlSet\Enum\DISPLAY\ACR06CF\5&39152f42&0&UID4353",
            DisplayDeviceResolver.GetRegistryKeyPath("ACR06CF", "5&39152f42&0&UID4353"));
    }
}
