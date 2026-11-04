namespace DDCSwitch;

/// <summary>
/// Bounded retry-with-backoff for DDC/CI calls. DDC/CI runs over I2C and a monitor may occasionally
/// not reply (busy scaler, bus noise); the DDC/CI spec expects hosts to retry a few times.
/// An explicit "VCP code not supported" answer is never retried.
/// </summary>
internal sealed class DdcRetryPolicy
{
    /// <summary>Delay (ms) before each attempt. Element 0 is the first attempt.</summary>
    public IReadOnlyList<int> Delays { get; }

    private readonly Action<int> _sleep;

    public DdcRetryPolicy(IReadOnlyList<int> delays, Action<int>? sleep = null)
    {
        if (delays.Count == 0) throw new ArgumentException("At least one attempt is required", nameof(delays));
        Delays = delays;
        _sleep = sleep ?? Thread.Sleep;
    }

    /// <summary>
    /// Default policy: 3 attempts, 150ms of added delay at most on top of the ~60ms dxva2 spends per call.
    /// </summary>
    public static DdcRetryPolicy Default { get; } = new([0, 50, 100]);

    /// <summary>Single attempt, used for bulk scans after the monitor has already answered once.</summary>
    public static DdcRetryPolicy Once { get; } = new([0]);

    /// <summary>
    /// Runs <paramref name="call"/> until it succeeds, returns a non-retryable error, or attempts run out.
    /// </summary>
    /// <param name="call">Performs one attempt; returns success and the Win32 error captured immediately after the call.</param>
    /// <param name="isRetryable">Decides whether a given error code is worth retrying.</param>
    /// <param name="lastError">Error from the final attempt (0 on success).</param>
    /// <param name="attempts">Number of attempts made.</param>
    public bool Execute(Func<int, (bool Success, int Error)> call, Func<int, bool> isRetryable, out int lastError, out int attempts)
    {
        lastError = 0;
        attempts = 0;

        for (int i = 0; i < Delays.Count; i++)
        {
            if (Delays[i] > 0) _sleep(Delays[i]);

            attempts++;
            var (success, error) = call(attempts);
            if (success)
            {
                lastError = 0;
                return true;
            }

            lastError = error;
            if (!isRetryable(error)) return false;
        }

        return false;
    }

    /// <summary>
    /// Default retry predicate: retry anything except an explicit "VCP code not supported" answer
    /// (the monitor answered and said no) and an invalid handle.
    /// </summary>
    public static bool IsTransient(int error) =>
        error != DdcErrors.VcpNotSupported && error != DdcErrors.InvalidHandle;
}
