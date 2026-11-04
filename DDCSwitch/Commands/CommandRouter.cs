using System.Text.Json;

namespace DDCSwitch.Commands;

internal static class CommandRouter
{
    public static int Route(string[] args)
    {
        bool jsonOutput = HasFlag(ref args, "--json");
        bool verboseOutput = HasFlag(ref args, "--verbose");
        if (HasFlag(ref args, "--trace"))
        {
            DdcTrace.Enabled = true;
        }

        if (args.Length == 0)
        {
            HelpCommand.ShowUsage();
            return 1;
        }

        try
        {
            return args[0].ToLowerInvariant() switch
            {
                "list" or "ls" => ListCommand.Execute(jsonOutput, verboseOutput),
                "get" => GetCommand.Execute(args, jsonOutput),
                "set" => SetCommand.Execute(args, jsonOutput),
                "toggle" => ToggleCommand.Execute(args, jsonOutput),
                "info" => InfoCommand.Execute(args, jsonOutput),
                "version" or "-v" or "--version" => HelpCommand.ShowVersion(jsonOutput),
                "help" or "-h" or "--help" or "/?" => HelpCommand.ShowUsage(),
                _ => InvalidCommand(args[0], jsonOutput)
            };
        }
        catch (Exception ex)
        {
            if (jsonOutput)
            {
                var error = new ErrorResponse(false, ex.Message);
                Console.WriteLine(JsonSerializer.Serialize(error, JsonContext.Default.ErrorResponse));
            }
            else
            {
                ConsoleOutputFormatter.WriteError(ex.Message);
            }

            return 1;
        }
    }

    /// <summary>Removes every occurrence of a flag from args and reports whether it was present.</summary>
    private static bool HasFlag(ref string[] args, string flag)
    {
        int before = args.Length;
        args = args.Where(a => !a.Equals(flag, StringComparison.OrdinalIgnoreCase)).ToArray();
        return args.Length != before;
    }

    private static int InvalidCommand(string command, bool jsonOutput)
    {
        if (jsonOutput)
        {
            var error = new ErrorResponse(false, $"Unknown command: {command}");
            Console.WriteLine(JsonSerializer.Serialize(error, JsonContext.Default.ErrorResponse));
        }
        else
        {
            ConsoleOutputFormatter.WriteError($"Unknown command: {command}");
            ConsoleOutputFormatter.WriteInfo("Run ddcswitch help for usage information.");
        }

        return 1;
    }
}
