using DdcSwitch;
using DdcSwitch.Commands;

namespace DdcSwitch.Tests;

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
    [InlineData(0x11u, 1)] // on HDMI1 -> DP1
    [InlineData(0x0Fu, 0)] // on DP1 -> HDMI1
    [InlineData(0x12u, 0)] // on neither -> first input
    public void TogglePicksTheOtherInput(uint current, int expectedIndex)
    {
        Assert.Equal(expectedIndex, ToggleCommand.DetermineTargetIndex(current, [0x11, 0x0F], strict: false));
    }

    [Theory]
    [InlineData(40u, 1)]
    [InlineData(60u, 2)]
    [InlineData(80u, 0)] // wraps around
    [InlineData(55u, 0)] // on none -> first value
    public void ToggleCyclesThroughThreeValues(uint current, int expectedIndex)
    {
        Assert.Equal(expectedIndex, ToggleCommand.DetermineTargetIndex(current, [40, 60, 80], strict: false));
    }

    [Fact]
    public void StrictToggle_ReturnsNullWhenCurrentValueIsNotListed()
    {
        Assert.Null(ToggleCommand.DetermineTargetIndex(55, [40, 60, 80], strict: true));
        Assert.Equal(2, ToggleCommand.DetermineTargetIndex(60, [40, 60, 80], strict: true));
    }

    [Theory]
    [InlineData("HDMI1", "DP1")]
    [InlineData("0x11", "0x0F")] // two args stay inputs even though 0x11 is also a VCP code
    [InlineData("17", "15")]
    public void TwoValues_AreAlwaysTheInputForm(string a, string b)
    {
        Assert.True(ToggleCommand.TryParseArgs(["toggle", "0", a, b], out var request, out var error, out _), error);
        Assert.Equal(InputSource.VcpInputSource, request!.Feature.Code);
        Assert.Equal([0x11u, 0x0Fu], request.Values.Select(v => v.Raw));
        Assert.False(request.Strict);
    }

    [Fact]
    public void FeatureForm_ParsesPercentages()
    {
        Assert.True(ToggleCommand.TryParseArgs(["toggle", "0", "brightness", "40%", "60%", "80%"], out var request, out var error, out _), error);
        Assert.Equal(VcpFeature.Brightness.Code, request!.Feature.Code);
        Assert.Equal([40u, 60u, 80u], request.Values.Select(v => v.Percentage!.Value));
    }

    [Fact]
    public void FeatureForm_AcceptsVcpCodeAndRawValues()
    {
        Assert.True(ToggleCommand.TryParseArgs(["toggle", "0", "0xEA", "0", "1"], out var request, out var error, out _), error);
        Assert.Equal(0xEA, request!.Feature.Code);
        Assert.Equal([0u, 1u], request.Values.Select(v => v.Raw));
    }

    [Fact]
    public void ThreeInputsWithoutFeatureName_CycleInputs()
    {
        Assert.True(ToggleCommand.TryParseArgs(["toggle", "0", "HDMI1", "DP1", "HDMI2"], out var request, out var error, out _), error);
        Assert.Equal(InputSource.VcpInputSource, request!.Feature.Code);
        Assert.Equal([0x11u, 0x0Fu, 0x12u], request.Values.Select(v => v.Raw));
    }

    [Theory]
    [InlineData("--strict", "0", "HDMI1", "DP1")]
    [InlineData("0", "HDMI1", "--STRICT", "DP1")]
    [InlineData("0", "HDMI1", "DP1", "--strict")]
    public void StrictFlag_IsAcceptedAnywhere(params string[] rest)
    {
        Assert.True(ToggleCommand.TryParseArgs(["toggle", .. rest], out var request, out var error, out _), error);
        Assert.True(request!.Strict);
        Assert.Equal(2, request.Values.Count);
    }

    [Fact]
    public void FeatureWithOneValue_AsksForTwoValues()
    {
        Assert.False(ToggleCommand.TryParseArgs(["toggle", "0", "brightness", "50"], out _, out var error, out _));
        Assert.Contains("at least two values for 'brightness'", error);
    }

    [Fact]
    public void DuplicateInputs_AreRejectedBeforeAnyMonitorIo()
    {
        Assert.False(ToggleCommand.TryParseArgs(["toggle", "0", "hdmi1", "0x11"], out _, out var error, out _));
        Assert.Contains("must be different", error);
    }

    [Fact]
    public void UnknownFeature_IsRejected()
    {
        Assert.False(ToggleCommand.TryParseArgs(["toggle", "0", "sparkle", "1", "2"], out _, out var error, out _));
        Assert.Contains("Invalid feature 'sparkle'", error);
    }

    [Fact]
    public void ReadOnlyFeature_IsRejected()
    {
        var readOnly = VcpFeature.AllFeatures.First(f => f.Type == VcpFeatureType.ReadOnly);
        Assert.False(ToggleCommand.TryParseArgs(["toggle", "0", $"0x{readOnly.Code:X2}", "1", "2"], out _, out var error, out _));
        Assert.Contains("read-only", error);
    }

    [Fact]
    public void ResolveRawValues_ConvertsPercentagesWithMonitorMax()
    {
        Assert.True(ToggleCommand.TryParseArgs(["toggle", "0", "brightness", "40%", "80%"], out var request, out _, out _));
        Assert.True(ToggleCommand.TryResolveRawValues(request!.Feature, request.Values, 50, out var raws, out var error), error);
        Assert.Equal([20u, 40u], raws);
    }

    [Fact]
    public void ResolveRawValues_RejectsPercentagesThatCollideOnSmallMax()
    {
        Assert.True(ToggleCommand.TryParseArgs(["toggle", "0", "brightness", "40%", "41%"], out var request, out _, out _));
        Assert.False(ToggleCommand.TryResolveRawValues(request!.Feature, request.Values, 10, out _, out var error));
        Assert.Contains("must be different", error);
    }

    [Fact]
    public void ResolveRawValues_RejectsRawValuesAboveMax()
    {
        Assert.True(ToggleCommand.TryParseArgs(["toggle", "0", "0xEA", "0", "200"], out var request, out _, out _));
        Assert.False(ToggleCommand.TryResolveRawValues(request!.Feature, request.Values, 100, out _, out var error));
        Assert.NotNull(error);
    }
}
