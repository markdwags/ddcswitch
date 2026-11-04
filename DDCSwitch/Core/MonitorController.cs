using System.Collections;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;

namespace DDCSwitch;

/// <summary>
/// The set of enumerated monitors. Disposing it releases every physical monitor handle.
/// </summary>
public sealed class MonitorCollection : IReadOnlyList<Monitor>, IDisposable
{
    private readonly List<Monitor> _monitors;

    internal MonitorCollection(List<Monitor> monitors) => _monitors = monitors;

    public Monitor this[int index] => _monitors[index];
    public int Count => _monitors.Count;
    public IEnumerator<Monitor> GetEnumerator() => _monitors.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public void Dispose()
    {
        foreach (var monitor in _monitors)
        {
            monitor.Dispose();
        }
    }
}

/// <summary>
/// Enumerates and looks up DDC/CI monitors.
/// </summary>
public static class MonitorController
{
    /// <summary>
    /// Enumerates all physical monitors. Performs no DDC/CI I/O.
    /// </summary>
    public static MonitorCollection EnumerateMonitors()
    {
        var monitors = new List<Monitor>();
        int index = 0;
        ExceptionDispatchInfo? failure = null;

        NativeMethods.MonitorEnumProc callback =
            (IntPtr hMonitor, IntPtr hdcMonitor, ref NativeMethods.RECT lprcMonitor, IntPtr dwData) =>
            {
                var monitorInfo = new NativeMethods.MONITORINFOEX();
                monitorInfo.cbSize = (uint)Marshal.SizeOf(monitorInfo);
                bool gotInfo = NativeMethods.GetMonitorInfo(hMonitor, ref monitorInfo);
                string deviceName = gotInfo ? monitorInfo.szDevice : "Unknown";
                bool isPrimary = gotInfo && (monitorInfo.dwFlags & NativeMethods.MONITORINFOF_PRIMARY) != 0;

                if (!NativeMethods.GetNumberOfPhysicalMonitorsFromHMONITOR(hMonitor, out uint count) || count == 0)
                {
                    DdcTrace.Write($"{deviceName}: no physical monitors (error 0x{Marshal.GetLastWin32Error():X8})");
                    return true;
                }

                var physicalMonitors = new NativeMethods.PHYSICAL_MONITOR[count];
                if (!NativeMethods.GetPhysicalMonitorsFromHMONITOR(hMonitor, count, physicalMonitors))
                {
                    DdcTrace.Write($"{deviceName}: GetPhysicalMonitorsFromHMONITOR failed (error 0x{Marshal.GetLastWin32Error():X8})");
                    return true;
                }

                // Take ownership of every handle immediately so none leak if construction fails.
                var handles = physicalMonitors.Select(pm => new PhysicalMonitorHandle(pm.hPhysicalMonitor)).ToList();
                int consumed = 0;

                // Exceptions must not propagate through the native EnumDisplayMonitors frame
                // (NativeAOT fail-fasts), so capture and rethrow after enumeration returns.
                try
                {
                    // Physical monitor i on this output corresponds to the i-th active monitor device on it.
                    var devicePaths = gotInfo ? DisplayDeviceResolver.GetMonitorDevicePaths(deviceName) : [];

                    for (int i = 0; i < handles.Count; i++)
                    {
                        string? devicePath = i < devicePaths.Count ? devicePaths[i] : null;
                        var monitor = new Monitor(index, physicalMonitors[i].szPhysicalMonitorDescription,
                            deviceName, isPrimary, handles[i], devicePath);
                        consumed++;
                        DdcTrace.Write($"monitor {index}: {monitor.Name} on {deviceName} handle={handles[i].Value} ({devicePath ?? "no device path"})");
                        monitors.Add(monitor);
                        index++;
                    }
                }
                catch (Exception ex)
                {
                    failure = ExceptionDispatchInfo.Capture(ex);
                    foreach (var handle in handles.Skip(consumed)) handle.Dispose();
                    return false; // stop enumeration
                }

                return true;
            };

        NativeMethods.EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, callback, IntPtr.Zero);
        GC.KeepAlive(callback);

        if (failure != null)
        {
            foreach (var monitor in monitors) monitor.Dispose();
            failure.Throw();
        }

        return new MonitorCollection(monitors);
    }

    /// <summary>
    /// Finds a monitor by index, or by a case-insensitive substring of its name, model, hardware ID
    /// (e.g. "ACR06CF") or display device name (e.g. "DISPLAY2").
    /// </summary>
    public static Monitor? FindMonitor(IReadOnlyList<Monitor> monitors, string identifier)
    {
        if (int.TryParse(identifier, out int index))
        {
            return monitors.FirstOrDefault(m => m.Index == index);
        }

        return monitors.FirstOrDefault(m =>
            Matches(m.Name, identifier) ||
            Matches(m.ModelName, identifier) ||
            Matches(m.HardwareId, identifier) ||
            Matches(m.DeviceName, identifier) ||
            Matches(m.WindowsDescription, identifier));
    }

    private static bool Matches(string? value, string identifier) =>
        value != null && value.Contains(identifier, StringComparison.OrdinalIgnoreCase);
}
