using DDCSwitch;

namespace DDCSwitch.Tests;

public class FeatureResolverTests
{
    [Theory]
    [InlineData("brightness", 0x10)]
    [InlineData("BRIGHTNESS", 0x10)]
    [InlineData("  brightness  ", 0x10)]
    [InlineData("bright", 0x10)] // alias
    [InlineData("contrast", 0x12)]
    [InlineData("input", 0x60)]
    [InlineData("source", 0x60)] // alias
    [InlineData("0x10", 0x10)]
    [InlineData("0X12", 0x12)]
    [InlineData("16", 0x10)]
    [InlineData("96", 0x60)]
    public void ResolvesNamesAliasesAndCodes(string input, byte expectedCode)
    {
        Assert.True(FeatureResolver.TryResolveFeature(input, out var feature));
        Assert.NotNull(feature);
        Assert.Equal(expectedCode, feature.Code);
    }

    [Fact]
    public void UnknownCode_ResolvesToGenericReadWriteFeature()
    {
        byte unknown = FindUnusedCode();

        Assert.True(FeatureResolver.TryResolveFeature($"0x{unknown:X2}", out var feature));
        Assert.NotNull(feature);
        Assert.Equal(unknown, feature.Code);
        Assert.Equal($"VCP_{unknown:X2}", feature.Name);
        Assert.Equal(VcpFeatureType.ReadWrite, feature.Type);
        Assert.False(feature.SupportsPercentage);

        var byCode = FeatureResolver.GetFeatureByCode(unknown);
        Assert.Equal($"VCP_{unknown:X2}", byCode.Name);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-a-feature")]
    [InlineData("0x100")]
    [InlineData("256")]
    [InlineData("-1")]
    public void RejectsUnknownNamesAndOutOfRangeCodes(string input)
    {
        Assert.False(FeatureResolver.TryResolveFeature(input, out var feature));
        Assert.Null(feature);
    }

    [Theory]
    [InlineData("0x00", 0x00)]
    [InlineData("0xff", 0xFF)]
    [InlineData("0XFF", 0xFF)]
    [InlineData(" 0x10 ", 0x10)]
    [InlineData("0", 0)]
    [InlineData("255", 255)]
    public void ParsesVcpCodes(string input, byte expected)
    {
        Assert.True(FeatureResolver.TryParseVcpCode(input, out byte code));
        Assert.Equal(expected, code);
    }

    [Theory]
    [InlineData("")]
    [InlineData("0x")]
    [InlineData("0x100")]
    [InlineData("0xZZ")]
    [InlineData("256")]
    [InlineData("-1")]
    [InlineData("ten")]
    public void RejectsInvalidVcpCodes(string input)
    {
        Assert.False(FeatureResolver.TryParseVcpCode(input, out _));
    }

    [Theory]
    [InlineData("0", 0u)]
    [InlineData("50", 50u)]
    [InlineData("50%", 50u)]
    [InlineData("50 %", 50u)]
    [InlineData(" 75% ", 75u)]
    [InlineData("100%", 100u)]
    public void ParsesPercentages(string input, uint expected)
    {
        Assert.True(FeatureResolver.TryParsePercentage(input, out uint percentage));
        Assert.Equal(expected, percentage);
    }

    [Theory]
    [InlineData("")]
    [InlineData("%")]
    [InlineData("101")]
    [InlineData("101%")]
    [InlineData("-5%")]
    [InlineData("half")]
    [InlineData("5.5%")]
    public void RejectsInvalidPercentages(string input)
    {
        Assert.False(FeatureResolver.TryParsePercentage(input, out _));
    }

    [Theory]
    [InlineData(0u, 100u, 0u)]
    [InlineData(100u, 100u, 100u)]
    [InlineData(40u, 100u, 40u)]
    [InlineData(100u, 255u, 255u)]
    [InlineData(20u, 255u, 51u)]
    [InlineData(33u, 75u, 25u)] // 24.75 rounds up
    [InlineData(50u, 0u, 0u)]   // monitor reports no range
    public void ConvertsPercentageToRaw(uint percentage, uint max, uint expectedRaw)
    {
        Assert.Equal(expectedRaw, FeatureResolver.ConvertPercentageToRaw(percentage, max));
    }

    [Fact]
    public void ConvertPercentageToRaw_RejectsOver100()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => FeatureResolver.ConvertPercentageToRaw(101, 100));
    }

    [Theory]
    [InlineData(0u, 100u, 0u)]
    [InlineData(51u, 255u, 20u)]
    [InlineData(255u, 255u, 100u)]
    [InlineData(300u, 255u, 100u)] // clamped to max
    [InlineData(10u, 0u, 0u)]
    public void ConvertsRawToPercentage(uint raw, uint max, uint expectedPercentage)
    {
        Assert.Equal(expectedPercentage, FeatureResolver.ConvertRawToPercentage(raw, max));
    }

    [Theory]
    [InlineData(100u)]
    [InlineData(255u)]
    [InlineData(1000u)]
    [InlineData(65535u)]
    public void PercentageRoundTripsThroughRaw(uint max)
    {
        // Whenever the monitor's range has at least 100 steps, every percentage must survive set -> get.
        for (uint p = 0; p <= 100; p++)
        {
            uint raw = FeatureResolver.ConvertPercentageToRaw(p, max);
            Assert.InRange(raw, 0u, max);
            Assert.Equal(p, FeatureResolver.ConvertRawToPercentage(raw, max));
        }
    }

    [Theory]
    [InlineData(50u, 100u, true)]
    [InlineData(100u, 100u, true)]
    [InlineData(101u, 100u, false)]
    public void ValidatesRawValueAgainstMax(uint value, uint max, bool expected)
    {
        Assert.Equal(expected, FeatureResolver.IsValidRawVcpValue(value, max));
    }

    [Theory]
    [InlineData("", "cannot be empty")]
    [InlineData("abc", "not a valid percentage value. Expected format")]
    [InlineData("150", "out of range")]
    [InlineData("150%", "out of range")]
    public void PercentageValidationError_DescribesTheProblem(string input, string expectedFragment)
    {
        Assert.Contains(expectedFragment, FeatureResolver.GetPercentageValidationError(input));
    }

    [Theory]
    [InlineData("", "cannot be empty")]
    [InlineData("0xZZ", "Expected hex format")]
    [InlineData("300", "out of range")]
    [InlineData("abc", "Expected format: 0-255 or 0x00-0xFF")]
    public void VcpCodeValidationError_DescribesTheProblem(string input, string expectedFragment)
    {
        Assert.Contains(expectedFragment, FeatureResolver.GetVcpCodeValidationError(input));
    }

    [Fact]
    public void SearchFeatures_MatchesNamesAndAliases()
    {
        Assert.Contains(FeatureResolver.SearchFeatures("bright"), f => f.Code == 0x10);
        Assert.Contains(FeatureResolver.SearchFeatures("SOURCE"), f => f.Code == 0x60);
        Assert.Empty(FeatureResolver.SearchFeatures(" "));
        Assert.Empty(FeatureResolver.SearchFeatures("zzz-no-such-feature-zzz"));
    }

    [Theory]
    [InlineData(VcpFeatureCategory.ImageAdjustment)]
    [InlineData(VcpFeatureCategory.Audio)]
    [InlineData(VcpFeatureCategory.Preset)]
    public void GetFeaturesByCategory_ReturnsOnlyThatCategory(VcpFeatureCategory category)
    {
        var features = FeatureResolver.GetFeaturesByCategory(category);
        Assert.NotEmpty(features);
        Assert.All(features, f => Assert.Equal(category, f.Category));
    }

    [Fact]
    public void KnownFeatureNames_IncludeNamesAndAliases()
    {
        var names = FeatureResolver.GetKnownFeatureNames().ToHashSet(StringComparer.OrdinalIgnoreCase);
        Assert.Contains("brightness", names);
        Assert.Contains("bright", names);
        Assert.Contains("source", names);
    }

    private static byte FindUnusedCode()
    {
        var used = VcpFeature.AllFeatures.Select(f => f.Code).ToHashSet();
        for (int code = 0xFF; code >= 0; code--)
        {
            if (!used.Contains((byte)code)) return (byte)code;
        }

        throw new InvalidOperationException("Every VCP code is defined");
    }
}
