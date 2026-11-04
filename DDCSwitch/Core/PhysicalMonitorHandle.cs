using System.Runtime.InteropServices;

namespace DDCSwitch;

/// <summary>
/// Owns a dxva2 physical monitor handle and releases it with DestroyPhysicalMonitor.
///
/// IMPORTANT: 0 is a valid physical monitor handle. dxva2 hands out small integers from a pool shared
/// across processes, so the first monitor opened while no other DDC/CI tool is running gets handle 0.
/// Treating 0 as "invalid" is what made ddcswitch ignore monitor 0 unless a tool such as
/// ControlMyMonitor was running and holding the low handle values.
/// </summary>
internal sealed class PhysicalMonitorHandle : SafeHandle
{
    private static readonly IntPtr Unset = new(-1);

    // Required by the P/Invoke marshaller.
    public PhysicalMonitorHandle() : base(Unset, ownsHandle: true)
    {
    }

    public PhysicalMonitorHandle(IntPtr handle) : base(Unset, ownsHandle: true)
    {
        SetHandle(handle);
    }

    /// <summary>Raw handle value, for diagnostics.</summary>
    public IntPtr Value => handle;

    public override bool IsInvalid => handle == Unset;

    protected override bool ReleaseHandle() => NativeMethods.DestroyPhysicalMonitor(handle);
}
