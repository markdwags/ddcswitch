using DDCSwitch;

namespace DDCSwitch.Tests;

public class DdcRetryPolicyTests
{
    private const int I2CReceiveError = unchecked((int)0xC0262583);

    private static DdcRetryPolicy Policy(List<int> sleeps) => new([0, 50, 100, 200], sleeps.Add);

    [Fact]
    public void SucceedsOnFirstAttempt_WithoutSleeping()
    {
        var sleeps = new List<int>();
        bool ok = Policy(sleeps).Execute(_ => (true, 0), DdcRetryPolicy.IsTransient, out int error, out int attempts);

        Assert.True(ok);
        Assert.Equal(0, error);
        Assert.Equal(1, attempts);
        Assert.Empty(sleeps);
    }

    [Fact]
    public void RetriesTransientFailures_UntilSuccess()
    {
        // Monitor misses two replies at the I2C level, then answers.
        var sleeps = new List<int>();
        bool ok = Policy(sleeps).Execute(
            attempt => attempt < 3 ? (false, I2CReceiveError) : (true, 0),
            DdcRetryPolicy.IsTransient, out int error, out int attempts);

        Assert.True(ok);
        Assert.Equal(0, error);
        Assert.Equal(3, attempts);
        Assert.Equal([50, 100], sleeps);
    }

    [Fact]
    public void GivesUpAfterLastAttempt_ReportingLastError()
    {
        var sleeps = new List<int>();
        bool ok = Policy(sleeps).Execute(_ => (false, I2CReceiveError), DdcRetryPolicy.IsTransient, out int error, out int attempts);

        Assert.False(ok);
        Assert.Equal(I2CReceiveError, error);
        Assert.Equal(4, attempts);
        Assert.Equal([50, 100, 200], sleeps);
    }

    [Fact]
    public void DoesNotRetry_WhenMonitorSaysVcpNotSupported()
    {
        var sleeps = new List<int>();
        bool ok = Policy(sleeps).Execute(_ => (false, DdcErrors.VcpNotSupported), DdcRetryPolicy.IsTransient, out int error, out int attempts);

        Assert.False(ok);
        Assert.Equal(DdcErrors.VcpNotSupported, error);
        Assert.Equal(1, attempts);
        Assert.Empty(sleeps);
    }

    [Fact]
    public void DefaultPolicy_HasBoundedTotalDelay()
    {
        int total = DdcRetryPolicy.Default.Delays.Sum();
        Assert.InRange(total, 50, 500);
    }
}
