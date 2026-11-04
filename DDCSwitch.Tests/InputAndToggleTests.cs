using DDCSwitch;
using DDCSwitch.Commands;

namespace DDCSwitch.Tests;

public class InputAndToggleTests
{
    [Theory]
    [InlineData("hdmi1", 0x11u)]
    [InlineData("HDMI", 0x11u)]
    [InlineData("dp1", 0x0Fu)]
    [InlineData("DisplayPort-2", 0x10u)]
    [InlineData("0x11", 0x11u)]
    [InlineData("17", 0x11u)]
    public void ParsesInputSources(string input, uint expected)
    {
        Assert.True(InputSource.TryParse(input, out uint value));
        Assert.Equal(expected, value);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("0x0")]
    [InlineData("0xZZ")]
    [InlineData("usb-c")]
    public void RejectsInvalidInputSources(string input)
    {
        Assert.False(InputSource.TryParse(input, out _));
    }

    [Theory]
    [InlineData(0x11u, 0x0Fu)] // on HDMI1 -> DP1
    [InlineData(0x0Fu, 0x11u)] // on DP1 -> HDMI1
    [InlineData(0x12u, 0x11u)] // on neither -> first input
    public void TogglePicksTheOtherInput(uint current, uint expectedTarget)
    {
        Assert.Equal(expectedTarget, ToggleCommand.DetermineTargetInput(current, 0x11, 0x0F));
    }
}
