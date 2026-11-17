using System.Text;
using DdcSwitch.Commands;

// Windows consoles default to the OEM code page (e.g. 437), which can't represent the
// emoji and symbols used in terminal output, so they render as '?'. Switch the console
// to UTF-8 for the duration of the run (before Spectre captures Console.Out) and restore
// the original code page afterwards, since it's shared with the parent shell.
Encoding? originalOutputEncoding = null;
if (!Console.IsOutputRedirected)
{
    try
    {
        originalOutputEncoding = Console.OutputEncoding;
        Console.OutputEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
    }
    catch (IOException)
    {
        originalOutputEncoding = null;
    }
}

try
{
    return CommandRouter.Route(args);
}
finally
{
    if (originalOutputEncoding is not null)
    {
        try { Console.OutputEncoding = originalOutputEncoding; }
        catch (IOException) { }
    }
}
