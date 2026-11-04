using DDCSwitch;

namespace DDCSwitch.Tests;

public class PhysicalMonitorHandleTests
{
    // Regression: dxva2 returns 0 as a perfectly valid handle for the first monitor opened when no
    // other DDC/CI tool is running. ddcswitch used to treat 0 as invalid and never talked to monitor 0
    // unless ControlMyMonitor was open (holding the low handle values).
    [Fact]
    public void HandleZero_IsValid()
    {
        var handle = new PhysicalMonitorHandle(IntPtr.Zero);
        Assert.False(handle.IsInvalid);
        handle.SetHandleAsInvalid(); // don't call DestroyPhysicalMonitor on a handle we never opened
    }

    [Fact]
    public void DefaultConstructed_IsInvalid()
    {
        var handle = new PhysicalMonitorHandle();
        Assert.True(handle.IsInvalid);
    }
}
