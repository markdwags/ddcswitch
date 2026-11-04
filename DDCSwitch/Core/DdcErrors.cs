namespace DDCSwitch;

/// <summary>
/// Win32 error codes returned by the dxva2 monitor configuration API (GetLastError after a failed call).
/// See winerror.h, ERROR_GRAPHICS_* range.
/// </summary>
internal static class DdcErrors
{
    public const int InvalidHandle = 0x00000006;                                    // ERROR_INVALID_HANDLE
    public const int I2CNotSupported = unchecked((int)0xC0262580);                  // ERROR_GRAPHICS_I2C_NOT_SUPPORTED
    public const int I2CDeviceDoesNotExist = unchecked((int)0xC0262581);            // ERROR_GRAPHICS_I2C_DEVICE_DOES_NOT_EXIST
    public const int I2CErrorTransmittingData = unchecked((int)0xC0262582);         // ERROR_GRAPHICS_I2C_ERROR_TRANSMITTING_DATA
    public const int I2CErrorReceivingData = unchecked((int)0xC0262583);            // ERROR_GRAPHICS_I2C_ERROR_RECEIVING_DATA
    public const int VcpNotSupported = unchecked((int)0xC0262584);                  // ERROR_GRAPHICS_DDCCI_VCP_NOT_SUPPORTED
    public const int InvalidData = unchecked((int)0xC0262585);                      // ERROR_GRAPHICS_DDCCI_INVALID_DATA
    public const int InvalidMessageCommand = unchecked((int)0xC0262589);            // ERROR_GRAPHICS_DDCCI_INVALID_MESSAGE_COMMAND
    public const int InvalidMessageLength = unchecked((int)0xC026258A);             // ERROR_GRAPHICS_DDCCI_INVALID_MESSAGE_LENGTH
    public const int InvalidMessageChecksum = unchecked((int)0xC026258B);           // ERROR_GRAPHICS_DDCCI_INVALID_MESSAGE_CHECKSUM

    /// <summary>The monitor answered and said it does not implement this VCP code.</summary>
    public static bool IsNotSupported(int error) => error == VcpNotSupported;

    /// <summary>I2C/DDC transport-level failure: the monitor did not answer (asleep, busy, or no DDC/CI).</summary>
    public static bool IsCommunicationFailure(int error) => error is
        I2CNotSupported or I2CDeviceDoesNotExist or I2CErrorTransmittingData or I2CErrorReceivingData or
        InvalidData or InvalidMessageCommand or InvalidMessageLength or InvalidMessageChecksum;

    public static string Describe(int error) => error switch
    {
        0 => "no error",
        InvalidHandle => "invalid monitor handle",
        I2CNotSupported => "I2C not supported on this output",
        I2CDeviceDoesNotExist => "no DDC/CI device answered",
        I2CErrorTransmittingData => "I2C transmit error",
        I2CErrorReceivingData => "I2C receive error (monitor did not reply)",
        VcpNotSupported => "VCP code not supported by monitor",
        InvalidData => "monitor returned invalid data",
        InvalidMessageCommand => "invalid DDC/CI message command",
        InvalidMessageLength => "invalid DDC/CI message length",
        InvalidMessageChecksum => "invalid DDC/CI message checksum",
        _ => "unknown error"
    };
}
