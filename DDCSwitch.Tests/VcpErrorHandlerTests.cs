using DDCSwitch;

namespace DDCSwitch.Tests;

public class VcpErrorHandlerTests
{
    [Theory]
    [InlineData(0x00000102, true)] // ERROR_TIMEOUT
    [InlineData(0x00000121, true)] // ERROR_SEM_TIMEOUT
    [InlineData(DdcErrors.I2CErrorReceivingData, true)]
    [InlineData(DdcErrors.I2CDeviceDoesNotExist, true)]
    [InlineData(DdcErrors.VcpNotSupported, false)]
    [InlineData(0, false)]
    public void ClassifiesTimeouts(int error, bool expected)
    {
        Assert.Equal(expected, VcpErrorHandler.IsTimeoutError(error));
    }

    [Theory]
    [InlineData(DdcErrors.VcpNotSupported, true)]
    [InlineData(0x00000032, true)] // ERROR_NOT_SUPPORTED
    [InlineData(0x00000001, true)] // ERROR_INVALID_FUNCTION
    [InlineData(DdcErrors.I2CErrorReceivingData, false)]
    [InlineData(0, false)]
    public void ClassifiesUnsupportedFeatures(int error, bool expected)
    {
        Assert.Equal(expected, VcpErrorHandler.IsUnsupportedFeatureError(error));
    }

    [Fact]
    public void ReadAndWriteFailureMessages_NameTheMonitorAndFeature()
    {
        using var monitor = TestMonitor.Create(0, "Office Display");

        string read = VcpErrorHandler.CreateReadFailureMessage(monitor, VcpFeature.Brightness);
        Assert.Contains("brightness", read);
        Assert.Contains("'Office Display'", read);

        string write = VcpErrorHandler.CreateWriteFailureMessage(monitor, VcpFeature.Contrast, 42);
        Assert.Contains("contrast to 42", write);
        Assert.Contains("'Office Display'", write);
    }

    [Fact]
    public void UnsupportedFeatureMessage_IncludesVcpCode()
    {
        using var monitor = TestMonitor.Create(0, "Office Display");
        string message = VcpErrorHandler.CreateUnsupportedFeatureMessage(monitor, VcpFeature.InputSource);
        Assert.Contains("VCP 0x60", message);
    }

    [Fact]
    public void TimeoutAndCommunicationMessages_IncludeTroubleshooting()
    {
        using var monitor = TestMonitor.Create(0, "Office Display");

        string timeout = VcpErrorHandler.CreateTimeoutMessage(monitor, VcpFeature.Brightness, "read");
        Assert.StartsWith("Timeout while trying to read brightness on monitor 'Office Display'", timeout);

        string comm = VcpErrorHandler.CreateCommunicationFailureMessage(monitor);
        Assert.StartsWith("DDC/CI communication failed with monitor 'Office Display'", comm);
        Assert.True(comm.Length > "DDC/CI communication failed with monitor 'Office Display'. ".Length);
    }

    [Fact]
    public void RangeValidationMessage_Percentage()
    {
        string message = VcpErrorHandler.CreateRangeValidationMessage(VcpFeature.Brightness, 150, 100, isPercentage: true);
        Assert.Equal("brightness value 150% is out of range. Valid range: 0-100%", message);
    }

    [Fact]
    public void RangeValidationMessage_Raw_ReportsMonitorMax()
    {
        string message = VcpErrorHandler.CreateRangeValidationMessage(VcpFeature.Contrast, 300, 255);
        Assert.StartsWith("contrast value 300 is out of range for this monitor. Valid range: 0-255.", message);
    }
}
