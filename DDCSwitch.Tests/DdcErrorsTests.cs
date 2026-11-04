using DDCSwitch;

namespace DDCSwitch.Tests;

public class DdcErrorsTests
{
    public static TheoryData<int> CommunicationFailures =>
    [
        DdcErrors.I2CNotSupported,
        DdcErrors.I2CDeviceDoesNotExist,
        DdcErrors.I2CErrorTransmittingData,
        DdcErrors.I2CErrorReceivingData,
        DdcErrors.InvalidData,
        DdcErrors.InvalidMessageCommand,
        DdcErrors.InvalidMessageLength,
        DdcErrors.InvalidMessageChecksum,
    ];

    [Theory]
    [MemberData(nameof(CommunicationFailures))]
    public void TransportErrors_AreCommunicationFailures_AndRetryable(int error)
    {
        Assert.True(DdcErrors.IsCommunicationFailure(error));
        Assert.False(DdcErrors.IsNotSupported(error));
        Assert.True(DdcRetryPolicy.IsTransient(error));
    }

    [Fact]
    public void VcpNotSupported_IsAnAnswerNotACommunicationFailure()
    {
        Assert.True(DdcErrors.IsNotSupported(DdcErrors.VcpNotSupported));
        Assert.False(DdcErrors.IsCommunicationFailure(DdcErrors.VcpNotSupported));
        Assert.False(DdcRetryPolicy.IsTransient(DdcErrors.VcpNotSupported));
    }

    [Fact]
    public void InvalidHandle_IsNotRetried()
    {
        Assert.False(DdcErrors.IsCommunicationFailure(DdcErrors.InvalidHandle));
        Assert.False(DdcRetryPolicy.IsTransient(DdcErrors.InvalidHandle));
    }

    [Theory]
    [InlineData(0, "no error")]
    [InlineData(DdcErrors.InvalidHandle, "invalid monitor handle")]
    [InlineData(DdcErrors.I2CErrorReceivingData, "monitor did not reply")]
    [InlineData(DdcErrors.VcpNotSupported, "not supported by monitor")]
    [InlineData(DdcErrors.InvalidMessageChecksum, "checksum")]
    [InlineData(0x12345678, "unknown error")]
    public void Describe_GivesReadableText(int error, string expectedFragment)
    {
        Assert.Contains(expectedFragment, DdcErrors.Describe(error));
    }

    [Theory]
    [MemberData(nameof(CommunicationFailures))]
    public void Describe_CoversEveryKnownTransportError(int error)
    {
        Assert.NotEqual("unknown error", DdcErrors.Describe(error));
    }

    [Fact]
    public void ErrorCodes_MatchWinerrorH()
    {
        // ERROR_GRAPHICS_* values are a public ABI; a typo here would misclassify real failures.
        Assert.Equal(unchecked((int)0xC0262583), DdcErrors.I2CErrorReceivingData);
        Assert.Equal(unchecked((int)0xC0262584), DdcErrors.VcpNotSupported);
        Assert.Equal(6, DdcErrors.InvalidHandle);
    }
}
