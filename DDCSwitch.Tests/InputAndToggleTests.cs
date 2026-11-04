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
    [InlineData(" 17 ", 0x11u)]
    [InlineData("0X11", 0x11u)]
    [InlineData("hdmi2", 0x12u)]
    [InlineData("HDMI_2", 0x12u)]
    [InlineData("Display Port 1", 0x0Fu)]
    [InlineData("dp", 0x0Fu)]
    [InlineData("dp2", 0x10u)]
    [InlineData("vga", 0x01u)]
    [InlineData("VGA2", 0x02u)]
    [InlineData("dvi", 0x03u)]
    [InlineData("dvi2", 0x04u)]
    [InlineData("composite2", 0x06u)]
    [InlineData("s-video", 0x07u)]
    [InlineData("tuner3", 0x0Bu)]
    [InlineData(" component3", 0x0Eu)]
    [InlineData("0x1B", 0x1Bu)] // vendor-specific codes pass straight through
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
    [InlineData("")]
    [InlineData("hdmi3")]
    [InlineData("0x100000000")]
    [InlineData("4294967296")]
    [InlineData("-1")]
    public void RejectsInvalidInputSources(string input)
    {
        Assert.False(InputSource.TryParse(input, out _));
    }

    [Theory]
    [InlineData(0x01u, "Analog1 (VGA1)")]
    [InlineData(0x03u, "DVI1")]
    [InlineData(0x0Fu, "DisplayPort1 (DP1)")]
    [InlineData(0x10u, "DisplayPort2 (DP2)")]
    [InlineData(0x11u, "HDMI1")]
    [InlineData(0x12u, "HDMI2")]
    [InlineData(0x1Bu, "Unknown (0x1B)")]
    [InlineData(0x00u, "Unknown (0x00)")]
    public void NamesInputSources(uint code, string expected)
    {
        Assert.Equal(expected, InputSource.GetName(code));
    }

    [Fact]
    public void EveryNamedInputSource_ParsesBackToItsCode()
    {
        for (uint code = 0x01; code <= 0x12; code++)
        {
            string name = InputSource.GetName(code);
            Assert.DoesNotContain("Unknown", name);

            // "DisplayPort1 (DP1)" -> "DisplayPort1"
            string primary = name.Split(' ')[0];
            Assert.True(InputSource.TryParse(primary, out uint parsed), $"'{primary}' did not parse");
            Assert.Equal(code, parsed);
        }
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
