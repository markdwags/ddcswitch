using DDCSwitch;
using DDCSwitch.Commands;

namespace DDCSwitch.Tests;

public class SetValueParsingTests
{
    [Theory]
    [InlineData("hdmi1", 0x11u)]
    [InlineData("DP2", 0x10u)]
    [InlineData("0x0F", 0x0Fu)]
    public void InputFeature_UsesInputSourceNames(string value, uint expected)
    {
        var (setValue, percentage, error) = SetCommand.ParseAndValidateValue(VcpFeature.InputSource, value);
        Assert.Null(error);
        Assert.Null(percentage);
        Assert.Equal(expected, setValue);
    }

    [Fact]
    public void InputFeature_RejectsUnknownInput()
    {
        var (_, _, error) = SetCommand.ParseAndValidateValue(VcpFeature.InputSource, "usb-c");
        Assert.NotNull(error);
        Assert.Contains("Invalid input source 'usb-c'", error);
    }

    [Theory]
    [InlineData("75%", 75u)]
    [InlineData("75", 75u)] // bare numbers 0-100 are percentages for percentage features
    [InlineData("0", 0u)]
    public void PercentageFeature_ParsesPercentages(string value, uint expected)
    {
        var (_, percentage, error) = SetCommand.ParseAndValidateValue(VcpFeature.Brightness, value);
        Assert.Null(error);
        Assert.Equal(expected, percentage);
    }

    [Fact]
    public void PercentageFeature_NumberOver100_IsTreatedAsRawValue()
    {
        var (setValue, percentage, error) = SetCommand.ParseAndValidateValue(VcpFeature.Brightness, "150");
        Assert.Null(error);
        Assert.Null(percentage);
        Assert.Equal(150u, setValue);
    }

    [Theory]
    [InlineData("150%")]
    [InlineData("bright")]
    public void PercentageFeature_RejectsInvalidValues(string value)
    {
        var (_, _, error) = SetCommand.ParseAndValidateValue(VcpFeature.Brightness, value);
        Assert.NotNull(error);
    }

    [Fact]
    public void RawFeature_ParsesNumbers()
    {
        var (setValue, percentage, error) = SetCommand.ParseAndValidateValue(VcpFeature.PowerMode, "4");
        Assert.Null(error);
        Assert.Null(percentage);
        Assert.Equal(4u, setValue);
    }

    [Theory]
    [InlineData("off")]
    [InlineData("50%")]
    public void RawFeature_RejectsNonNumericValues(string value)
    {
        var (_, _, error) = SetCommand.ParseAndValidateValue(VcpFeature.PowerMode, value);
        Assert.NotNull(error);
        Assert.Contains("for feature 'power-mode'", error);
    }
}
