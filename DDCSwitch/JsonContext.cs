using System.Text.Json.Serialization;

namespace DDCSwitch;

[JsonSerializable(typeof(ErrorResponse))]
[JsonSerializable(typeof(ListMonitorsResponse))]
[JsonSerializable(typeof(MonitorInfo))]
[JsonSerializable(typeof(GetInputResponse))]
[JsonSerializable(typeof(SetInputResponse))]
[JsonSerializable(typeof(MonitorReference))]
[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]

internal partial class JsonContext : JsonSerializerContext
{
}

// Response models
internal record ErrorResponse(bool Success, string Error, MonitorReference? Monitor = null);
internal record ListMonitorsResponse(bool Success, List<MonitorInfo>? Monitors = null, string? Error = null);
internal record GetInputResponse(bool Success, MonitorReference Monitor, string CurrentInput, string CurrentInputCode, uint MaxValue);
internal record SetInputResponse(bool Success, MonitorReference Monitor, string NewInput, string NewInputCode);

// Data models
internal record MonitorInfo(
    int Index,
    string Name,
    string DeviceName,
    bool IsPrimary,
    string? CurrentInput,
    string? CurrentInputCode,
    string Status);

internal record MonitorReference(int Index, string Name, string DeviceName, bool IsPrimary = false);

