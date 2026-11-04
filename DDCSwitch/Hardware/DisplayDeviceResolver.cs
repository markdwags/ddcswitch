using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace DDCSwitch;

/// <summary>
/// Maps a display output (\\.\DISPLAY1) to the exact monitor device attached to it and reads that
/// monitor's EDID from its own registry key. No guessing: the device interface path returned by
/// EnumDisplayDevices(EDD_GET_DEVICE_INTERFACE_NAME) names the registry instance directly, e.g.
/// \\?\DISPLAY#ACR06CF#5&amp;39152f42&amp;0&amp;UID4353#{e6f07b5f-...} →
/// HKLM\SYSTEM\CurrentControlSet\Enum\DISPLAY\ACR06CF\5&amp;39152f42&amp;0&amp;UID4353\Device Parameters\EDID
/// </summary>
internal static class DisplayDeviceResolver
{
    private const string DisplayEnumKey = @"SYSTEM\CurrentControlSet\Enum\DISPLAY";

    /// <summary>
    /// Returns the device interface paths of the active monitors attached to a display output, in order.
    /// </summary>
    public static List<string> GetMonitorDevicePaths(string adapterDeviceName)
    {
        var paths = new List<string>();
        var device = new NativeMethods.DISPLAY_DEVICE();
        device.cb = (uint)Marshal.SizeOf(device);

        for (uint i = 0;
             NativeMethods.EnumDisplayDevices(adapterDeviceName, i, ref device, NativeMethods.EDD_GET_DEVICE_INTERFACE_NAME);
             i++)
        {
            if ((device.StateFlags & NativeMethods.DISPLAY_DEVICE_ACTIVE) != 0 && !string.IsNullOrEmpty(device.DeviceID))
            {
                paths.Add(device.DeviceID);
            }

            device = new NativeMethods.DISPLAY_DEVICE();
            device.cb = (uint)Marshal.SizeOf(device);
        }

        return paths;
    }

    /// <summary>
    /// Parses a monitor device interface path into its hardware ID (e.g. ACR06CF) and instance ID.
    /// </summary>
    public static bool TryParseDevicePath(string? devicePath, out string hardwareId, out string instanceId)
    {
        hardwareId = string.Empty;
        instanceId = string.Empty;

        if (string.IsNullOrEmpty(devicePath)) return false;

        string path = devicePath.StartsWith(@"\\?\", StringComparison.Ordinal) ? devicePath[4..] : devicePath;
        var parts = path.Split('#');
        if (parts.Length < 3 || !parts[0].Equals("DISPLAY", StringComparison.OrdinalIgnoreCase)) return false;
        if (parts[1].Length == 0 || parts[2].Length == 0) return false;

        hardwareId = parts[1];
        instanceId = parts[2];
        return true;
    }

    /// <summary>Registry key (under HKLM) holding the monitor's device instance.</summary>
    public static string GetRegistryKeyPath(string hardwareId, string instanceId) =>
        $@"{DisplayEnumKey}\{hardwareId}\{instanceId}";

    /// <summary>Reads the EDID for a monitor device interface path, or null if unavailable.</summary>
    public static byte[]? ReadEdid(string? devicePath)
    {
        if (!TryParseDevicePath(devicePath, out var hardwareId, out var instanceId)) return null;

        try
        {
            using var key = Registry.LocalMachine.OpenSubKey($@"{GetRegistryKeyPath(hardwareId, instanceId)}\Device Parameters");
            return key?.GetValue("EDID") as byte[];
        }
        catch (Exception ex) when (ex is System.Security.SecurityException or UnauthorizedAccessException or IOException)
        {
            return null;
        }
    }
}
