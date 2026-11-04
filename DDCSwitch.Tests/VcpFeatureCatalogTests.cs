using System.Text.Json;
using DDCSwitch;

namespace DDCSwitch.Tests;

/// <summary>
/// Guards the generated feature catalog (VcpFeature.Generated.cs) and its source data (VcpFeatureData.json).
/// </summary>
public class VcpFeatureCatalogTests
{
    [Fact]
    public void FeatureCodesAreUnique()
    {
        var duplicates = VcpFeature.AllFeatures
            .GroupBy(f => f.Code)
            .Where(g => g.Count() > 1)
            .Select(g => $"0x{g.Key:X2}: {string.Join(", ", g.Select(f => f.Name))}")
            .ToList();

        Assert.Empty(duplicates);
    }

    [Fact]
    public void NamesAndAliasesNeverPointAtTwoDifferentFeatures()
    {
        // FeatureResolver builds a case-insensitive name map; a collision would silently shadow a feature.
        var owner = new Dictionary<string, VcpFeature>(StringComparer.OrdinalIgnoreCase);
        var collisions = new List<string>();

        foreach (var feature in VcpFeature.AllFeatures)
        {
            foreach (var key in feature.Aliases.Prepend(feature.Name))
            {
                if (owner.TryGetValue(key, out var existing) && existing.Code != feature.Code)
                {
                    collisions.Add($"'{key}' -> 0x{existing.Code:X2} and 0x{feature.Code:X2}");
                }

                owner[key] = feature;
            }
        }

        Assert.Empty(collisions);
    }

    [Fact]
    public void NamesAreUsableOnTheCommandLine()
    {
        Assert.All(VcpFeature.AllFeatures, f =>
        {
            Assert.False(string.IsNullOrWhiteSpace(f.Name));
            Assert.False(string.IsNullOrWhiteSpace(f.Description));
            Assert.DoesNotContain(' ', f.Name);
            Assert.All(f.Aliases, a => Assert.DoesNotContain(' ', a));
        });
    }

    [Theory]
    [InlineData(0x10, "brightness", true)]
    [InlineData(0x12, "contrast", true)]
    [InlineData(0x60, "input", false)]
    public void CoreFeaturesHaveExpectedDefinitions(byte code, string name, bool supportsPercentage)
    {
        var feature = FeatureResolver.GetFeatureByCode(code);
        Assert.Equal(name, feature.Name);
        Assert.Equal(supportsPercentage, feature.SupportsPercentage);
        Assert.Equal(VcpFeatureType.ReadWrite, feature.Type);
    }

    [Fact]
    public void InputSourceConstantMatchesCatalog()
    {
        Assert.Equal(InputSource.VcpInputSource, VcpFeature.InputSource.Code);
    }

    [Fact]
    public void FeaturesAreEqualByCode()
    {
        var a = new VcpFeature(0x10, "a", VcpFeatureType.ReadOnly, false);
        var b = new VcpFeature(0x10, "b", VcpFeatureType.ReadWrite, true);
        var c = new VcpFeature(0x11, "a", VcpFeatureType.ReadOnly, false);

        Assert.Equal(a, b);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
        Assert.NotEqual(a, c);
        Assert.Equal("a (0x10)", a.ToString());
    }

    [Fact]
    public void GeneratedCatalogMatchesEmbeddedJson()
    {
        // Fails when VcpFeatureData.json is edited without re-running tools/Regenerate.ps1.
        var assembly = typeof(VcpFeature).Assembly;
        string resource = assembly.GetManifestResourceNames().Single(n => n.EndsWith("VcpFeatureData.json", StringComparison.Ordinal));
        using var stream = assembly.GetManifestResourceStream(resource)!;
        using var doc = JsonDocument.Parse(stream);

        var fromJson = doc.RootElement.GetProperty("features").EnumerateArray()
            .Select(f => new
            {
                Code = Convert.ToByte(f.GetProperty("code").GetString(), 16),
                Name = f.GetProperty("name").GetString(),
                Type = Enum.Parse<VcpFeatureType>(f.GetProperty("type").GetString()!),
                Category = Enum.Parse<VcpFeatureCategory>(f.GetProperty("category").GetString()!),
                Percentage = f.GetProperty("supportsPercentage").GetBoolean(),
                Aliases = f.TryGetProperty("aliases", out var a)
                    ? string.Join(",", a.EnumerateArray().Select(x => x.GetString()))
                    : ""
            })
            .OrderBy(f => f.Code)
            .ToList();

        var generated = VcpFeature.AllFeatures
            .Select(f => new
            {
                f.Code,
                Name = (string?)f.Name,
                f.Type,
                f.Category,
                Percentage = f.SupportsPercentage,
                Aliases = string.Join(",", f.Aliases)
            })
            .OrderBy(f => f.Code)
            .ToList();

        Assert.Equal(fromJson, generated);
    }
}
