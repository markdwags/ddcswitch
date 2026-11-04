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

    [Fact]
    public void DefaultPolicy_FirstAttemptIsImmediate()
    {
        Assert.Equal(0, DdcRetryPolicy.Default.Delays[0]);
        Assert.True(DdcRetryPolicy.Default.Delays.Count > 1);
    }

    [Fact]
    public void OncePolicy_MakesExactlyOneAttempt()
    {
        Assert.Equal([0], DdcRetryPolicy.Once.Delays);

        int calls = 0;
        bool ok = DdcRetryPolicy.Once.Execute(_ => { calls++; return (false, I2CReceiveError); },
            DdcRetryPolicy.IsTransient, out int error, out int attempts);

        Assert.False(ok);
        Assert.Equal(1, calls);
        Assert.Equal(1, attempts);
        Assert.Equal(I2CReceiveError, error);
    }

    [Fact]
    public void EmptyDelayList_IsRejected()
    {
        Assert.Throws<ArgumentException>(() => new DdcRetryPolicy([]));
    }

    [Fact]
    public void DoesNotRetry_InvalidHandle()
    {
        var sleeps = new List<int>();
        bool ok = Policy(sleeps).Execute(_ => (false, DdcErrors.InvalidHandle), DdcRetryPolicy.IsTransient, out int error, out int attempts);

        Assert.False(ok);
        Assert.Equal(DdcErrors.InvalidHandle, error);
        Assert.Equal(1, attempts);
        Assert.Empty(sleeps);
    }

    [Fact]
    public void SleepsBeforeFirstAttempt_WhenFirstDelayIsNonZero()
    {
        var sleeps = new List<int>();
        var policy = new DdcRetryPolicy([25], sleeps.Add);

        Assert.True(policy.Execute(_ => (true, 0), DdcRetryPolicy.IsTransient, out _, out _));
        Assert.Equal([25], sleeps);
    }

    [Fact]
    public void ClearsErrorFromEarlierAttempts_OnSuccess()
    {
        var sleeps = new List<int>();
        bool ok = Policy(sleeps).Execute(
            attempt => attempt == 1 ? (false, I2CReceiveError) : (true, 0),
            DdcRetryPolicy.IsTransient, out int error, out int attempts);

        Assert.True(ok);
        Assert.Equal(0, error);
        Assert.Equal(2, attempts);
    }

    [Fact]
    public void PassesOneBasedAttemptNumber()
    {
        var seen = new List<int>();
        Policy([]).Execute(attempt => { seen.Add(attempt); return (false, I2CReceiveError); },
            DdcRetryPolicy.IsTransient, out _, out _);

        Assert.Equal([1, 2, 3, 4], seen);
    }
}
