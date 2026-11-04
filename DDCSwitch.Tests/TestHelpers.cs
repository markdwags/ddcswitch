using System.Text;
using DDCSwitch;

namespace DDCSwitch.Tests;

/// <summary>Builds <see cref="Monitor"/> instances without touching DDC/CI or real EDID data.</summary>
internal static class TestMonitor
{
    /// <summary>
    /// Creates a monitor whose EDID lookup misses (fake hardware ID), so its name comes from the fallback rules.
    /// The handle is the default invalid one, so disposing never calls into dxva2.
    /// </summary>
    public static Monitor Create(int index, string windowsDescription = "Generic PnP Monitor",
        string? deviceName = null, string? hardwareId = null, bool isPrimary = false)
    {
        string? devicePath = hardwareId == null
            ? null
            : $@"\\?\DISPLAY#{hardwareId}#1&ddcswitchtest&0&UID{index}#{{e6f07b5f-ee97-4a90-b076-33f57bf4eaa7}}";

        return new Monitor(index, windowsDescription, deviceName ?? $@"\\.\DISPLAY{index + 1}", isPrimary,
            new PhysicalMonitorHandle(), devicePath);
    }
}

/// <summary>Builds a structurally valid 128-byte EDID base block.</summary>
internal sealed class EdidBuilder
{
    private readonly byte[] _edid = new byte[128];
    private int _nextDescriptor = 54;

    public EdidBuilder()
    {
        byte[] header = [0x00, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0x00];
        header.CopyTo(_edid, 0);
        _edid[18] = 1; // EDID 1.4
        _edid[19] = 4;
        _edid[20] = 0x80; // digital input
    }

    public EdidBuilder Manufacturer(string pnpId)
    {
        if (pnpId.Length != 3) throw new ArgumentException("PNP ID must be 3 letters", nameof(pnpId));
        int id = ((pnpId[0] - 'A' + 1) << 10) | ((pnpId[1] - 'A' + 1) << 5) | (pnpId[2] - 'A' + 1);
        _edid[8] = (byte)(id >> 8);
        _edid[9] = (byte)id;
        return this;
    }

    public EdidBuilder ProductCode(ushort code)
    {
        _edid[10] = (byte)code;
        _edid[11] = (byte)(code >> 8);
        return this;
    }

    public EdidBuilder NumericSerial(uint serial)
    {
        BitConverter.GetBytes(serial).CopyTo(_edid, 12);
        return this;
    }

    public EdidBuilder Manufactured(int week, int year)
    {
        _edid[16] = (byte)week;
        _edid[17] = (byte)(year - 1990);
        return this;
    }

    public EdidBuilder ModelName(string name) => TextDescriptor(0xFC, name);

    public EdidBuilder SerialString(string serial) => TextDescriptor(0xFF, serial);

    private EdidBuilder TextDescriptor(byte tag, string text)
    {
        if (_nextDescriptor > 108) throw new InvalidOperationException("All four descriptors are used");
        if (text.Length > 13) throw new ArgumentException("Descriptor text is at most 13 characters", nameof(text));

        int offset = _nextDescriptor;
        _nextDescriptor += 18;

        _edid[offset + 3] = tag;
        var payload = new byte[13];
        Array.Fill(payload, (byte)0x20);
        Encoding.ASCII.GetBytes(text).CopyTo(payload, 0);
        if (text.Length < 13) payload[text.Length] = 0x0A;
        payload.CopyTo(_edid, offset + 5);
        return this;
    }

    public byte[] Build()
    {
        var result = (byte[])_edid.Clone();
        int sum = 0;
        for (int i = 0; i < 127; i++) sum += result[i];
        result[127] = (byte)(256 - sum % 256);
        return result;
    }
}
