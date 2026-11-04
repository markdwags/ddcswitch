using System.Text.Json;
using DDCSwitch;

namespace DDCSwitch.Tests;

/// <summary>
/// The --json output is a scripting contract; these pin its shape (camelCase, nulls omitted).
/// </summary>
public class JsonContextTests
{
    [Fact]
    public void ErrorResponse_IsCamelCase_AndOmitsNulls()
    {
        string json = JsonSerializer.Serialize(new ErrorResponse(false, "boom"), JsonContext.Default.ErrorResponse);

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        Assert.False(root.GetProperty("success").GetBoolean());
        Assert.Equal("boom", root.GetProperty("error").GetString());
        Assert.False(root.TryGetProperty("monitor", out _));
    }

    [Fact]
    public void ErrorResponse_RoundTrips()
    {
        var original = new ErrorResponse(false, "boom", new MonitorReference(1, "Acer VG270U P", @"\\.\DISPLAY2", true));
        string json = JsonSerializer.Serialize(original, JsonContext.Default.ErrorResponse);
        var back = JsonSerializer.Deserialize(json, JsonContext.Default.ErrorResponse);

        Assert.Equal(original, back);
    }

    [Fact]
    public void GetVcpResponse_UsesStableFieldNames()
    {
        var response = new GetVcpResponse(true, new MonitorReference(0, "Dell", @"\\.\DISPLAY1"), "brightness", 64, 100, 64);
        string json = JsonSerializer.Serialize(response, JsonContext.Default.GetVcpResponse);

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        Assert.Equal("brightness", root.GetProperty("featureName").GetString());
        Assert.Equal(64u, root.GetProperty("rawValue").GetUInt32());
        Assert.Equal(100u, root.GetProperty("maxValue").GetUInt32());
        Assert.Equal(64u, root.GetProperty("percentageValue").GetUInt32());
        Assert.Equal(0, root.GetProperty("monitor").GetProperty("index").GetInt32());
        Assert.False(root.TryGetProperty("errorMessage", out _));
    }
}
