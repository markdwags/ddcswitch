using System.Diagnostics;
using System.Runtime.InteropServices;

namespace DDCSwitch;

/// <summary>
/// A physical monitor and its DDC/CI channel. Construction does no DDC/CI I/O; only the monitor a
/// command actually targets is ever talked to.
/// </summary>
public sealed class Monitor : IDisposable
{
    private readonly PhysicalMonitorHandle _handle;

    internal Monitor(int index, string windowsDescription, string deviceName, bool isPrimary,
        PhysicalMonitorHandle handle, string? devicePath)
    {
        Index = index;
        WindowsDescription = windowsDescription;
        DeviceName = deviceName;
        IsPrimary = isPrimary;
        DevicePath = devicePath;
        _handle = handle;

        if (DisplayDeviceResolver.TryParseDevicePath(devicePath, out var hardwareId, out _))
        {
            HardwareId = hardwareId;
        }

        LoadEdidData();
        Name = MonitorNameResolver.ResolveMonitorName(this, ParsedEdid);
    }

    public int Index { get; }

    /// <summary>Friendly name, from EDID when available (e.g. "Acer VG270U P").</summary>
    public string Name { get; }

    /// <summary>Description Windows reports for the physical monitor (usually "Generic PnP Monitor").</summary>
    public string WindowsDescription { get; }

    /// <summary>Display output the monitor is attached to, e.g. \\.\DISPLAY1.</summary>
    public string DeviceName { get; }

    /// <summary>Monitor device interface path, e.g. \\?\DISPLAY#ACR06CF#5&amp;...#{guid}.</summary>
    public string? DevicePath { get; }

    /// <summary>PnP hardware ID, e.g. ACR06CF.</summary>
    public string? HardwareId { get; }

    public bool IsPrimary { get; }

    // EDID-derived properties
    public ParsedEdidInfo? ParsedEdid { get; private set; }
    public string? ManufacturerId { get; private set; }
    public string? ManufacturerName { get; private set; }
    public string? ModelName { get; private set; }
    public string? SerialNumber { get; private set; }
    public ushort? ProductCode { get; private set; }
    public int? ManufactureYear { get; private set; }
    public int? ManufactureWeek { get; private set; }
    public EdidVersion? EdidVersion { get; private set; }
    public VideoInputDefinition? VideoInputDefinition { get; private set; }
    public SupportedFeatures? SupportedFeatures { get; private set; }
    public ChromaticityCoordinates? Chromaticity { get; private set; }

    private void LoadEdidData()
    {
        var edid = DisplayDeviceResolver.ReadEdid(DevicePath);
        if (edid == null) return;

        ParsedEdid = EdidParser.ParseFromBytes(edid);
        if (ParsedEdid == null) return;

        ManufacturerId = ParsedEdid.ManufacturerCode;
        ManufacturerName = ParsedEdid.ManufacturerName;
        ProductCode = ParsedEdid.ProductCode;
        ManufactureYear = ParsedEdid.ManufactureYear;
        ManufactureWeek = ParsedEdid.ManufactureWeek;
        ModelName = EdidParser.ParseModelName(edid);
        SerialNumber = EdidParser.ParseSerialNumber(edid);
        EdidVersion = EdidParser.ParseEdidVersion(edid);
        VideoInputDefinition = EdidParser.ParseVideoInputDefinition(edid);
        SupportedFeatures = EdidParser.ParseSupportedFeatures(edid);
        Chromaticity = EdidParser.ParseChromaticity(edid);
    }

    /// <summary>
    /// Reads a VCP feature, retrying when the monitor does not reply.
    /// </summary>
    public bool TryGetVcpFeature(byte vcpCode, out uint currentValue, out uint maxValue, out int errorCode) =>
        TryGetVcpFeature(vcpCode, out currentValue, out maxValue, out errorCode, DdcRetryPolicy.Default);

    public bool TryGetVcpFeature(byte vcpCode, out uint currentValue, out uint maxValue) =>
        TryGetVcpFeature(vcpCode, out currentValue, out maxValue, out _);

    internal bool TryGetVcpFeature(byte vcpCode, out uint currentValue, out uint maxValue, out int errorCode,
        DdcRetryPolicy policy)
    {
        uint current = 0, max = 0;
        currentValue = 0;
        maxValue = 0;

        if (_handle.IsInvalid || _handle.IsClosed)
        {
            errorCode = DdcErrors.InvalidHandle;
            DdcTrace.Write($"monitor {Index} ({Name}) get 0x{vcpCode:X2}: handle not usable, no request sent");
            return false;
        }

        bool ok = policy.Execute(attempt =>
        {
            var sw = Stopwatch.StartNew();
            bool success = NativeMethods.GetVCPFeatureAndVCPFeatureReply(_handle, vcpCode, out _, out current, out max);
            int error = success ? 0 : Marshal.GetLastWin32Error();
            Trace("get", vcpCode, attempt, success, error, sw.ElapsedMilliseconds, success ? $" value=0x{current:X2} max=0x{max:X2}" : "");
            return (success, error);
        }, DdcRetryPolicy.IsTransient, out errorCode, out _);

        if (ok)
        {
            currentValue = current;
            maxValue = max;
        }

        return ok;
    }

    /// <summary>
    /// Writes a VCP feature, retrying transient DDC/CI failures.
    /// </summary>
    public bool TrySetVcpFeature(byte vcpCode, uint value, out int errorCode)
    {
        if (_handle.IsInvalid || _handle.IsClosed)
        {
            errorCode = DdcErrors.InvalidHandle;
            return false;
        }

        return DdcRetryPolicy.Default.Execute(attempt =>
        {
            var sw = Stopwatch.StartNew();
            bool success = NativeMethods.SetVCPFeature(_handle, vcpCode, value);
            int error = success ? 0 : Marshal.GetLastWin32Error();
            Trace("set", vcpCode, attempt, success, error, sw.ElapsedMilliseconds, $" value=0x{value:X2}");
            return (success, error);
        }, DdcRetryPolicy.IsTransient, out errorCode, out _);
    }

    public bool TrySetVcpFeature(byte vcpCode, uint value) => TrySetVcpFeature(vcpCode, value, out _);

    /// <summary>
    /// Scans all VCP codes (0x00-0xFF) to discover supported features.
    /// </summary>
    public Dictionary<byte, VcpFeatureInfo> ScanVcpFeatures()
    {
        var features = new Dictionary<byte, VcpFeatureInfo>();
        if (_handle.IsInvalid || _handle.IsClosed) return features;

        var predefinedFeatures = FeatureResolver.GetPredefinedFeatures().ToDictionary(f => f.Code, f => f);

        // One fully-retried read first, then single attempts so 256
        // unsupported codes don't each pay the retry budget.
        TryGetVcpFeature(VcpFeature.Brightness.Code, out _, out _, out _, DdcRetryPolicy.Default);

        for (int code = 0; code <= 255; code++)
        {
            byte vcpCode = (byte)code;
            predefinedFeatures.TryGetValue(vcpCode, out VcpFeature? predefined);
            string name = predefined?.Name ?? $"VCP_{vcpCode:X2}";
            bool supported = TryGetVcpFeature(vcpCode, out uint currentValue, out uint maxValue, out _, DdcRetryPolicy.Once);

            features[vcpCode] = new VcpFeatureInfo(
                vcpCode,
                name,
                predefined?.Description ?? $"VCP feature {name}",
                supported ? predefined?.Type ?? VcpFeatureType.ReadWrite : VcpFeatureType.ReadWrite,
                predefined?.Category ?? VcpFeatureCategory.Miscellaneous,
                supported ? currentValue : 0,
                supported ? maxValue : 0,
                supported);
        }

        return features;
    }

    private void Trace(string op, byte vcpCode, int attempt, bool success, int error, long elapsedMs, string extra)
    {
        if (!DdcTrace.Enabled) return;
        string result = success ? "ok" : $"FAIL 0x{error:X8} ({DdcErrors.Describe(error)})";
        DdcTrace.Write($"monitor {Index} ({Name}) {op} 0x{vcpCode:X2} attempt {attempt}: {result} in {elapsedMs}ms{extra}");
    }

    public void Dispose() => _handle.Dispose();

    public override string ToString() => $"[{Index}] {Name} ({DeviceName}){(IsPrimary ? " *PRIMARY*" : "")}";
}
