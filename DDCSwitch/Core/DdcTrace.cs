namespace DDCSwitch;

/// <summary>
/// Opt-in diagnostic trace of every DDC/CI call, written to stderr so it never corrupts --json output.
/// Enable with the --trace flag or DDCSWITCH_TRACE=1.
/// </summary>
internal static class DdcTrace
{
    public static bool Enabled { get; set; } =
        Environment.GetEnvironmentVariable("DDCSWITCH_TRACE") is "1" or "true";

    public static void Write(string message)
    {
        if (Enabled)
        {
            Console.Error.WriteLine($"[ddc {DateTime.Now:HH:mm:ss.fff}] {message}");
        }
    }
}
