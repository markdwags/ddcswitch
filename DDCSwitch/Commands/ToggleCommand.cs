using Spectre.Console;
using System.Text.Json;

namespace DDCSwitch.Commands;

internal static class ToggleCommand
{
    public static int Execute(string[] args, bool jsonOutput)
    {
        // toggle <monitor> <input1> <input2>
        if (args.Length < 4)
        {
            return HandleMissingArguments(args.Length, jsonOutput);
        }

        string monitorArg = args[1];
        string input1Arg = args[2];
        string input2Arg = args[3];

        if (!InputSource.TryParse(input1Arg, out uint input1Value))
        {
            return HandleInvalidInput(input1Arg, "first", jsonOutput);
        }

        if (!InputSource.TryParse(input2Arg, out uint input2Value))
        {
            return HandleInvalidInput(input2Arg, "second", jsonOutput);
        }

        if (input1Value == input2Value)
        {
            return WriteError(
                $"Input sources must be different. Both inputs resolve to the same source: '{input1Arg}' and '{input2Arg}'",
                "Please specify two different input sources to toggle between", null, jsonOutput);
        }

        using var monitors = MonitorController.EnumerateMonitors();
        if (monitors.Count == 0)
        {
            return WriteError("No DDC/CI capable monitors found", null, null, jsonOutput);
        }

        var monitor = MonitorController.FindMonitor(monitors, monitorArg);
        if (monitor == null)
        {
            return WriteError($"Monitor '{monitorArg}' not found", "Use 'ddcswitch list' to see available monitors.", null, jsonOutput);
        }

        // Read the current input. Monitor.TryGetVcpFeature already retries when the monitor does not reply.
        // If it still can't be read we stop: guessing would silently do nothing whenever the guess
        // matches the input the monitor is already on.
        if (!monitor.TryGetVcpFeature(InputSource.VcpInputSource, out uint currentInput, out _, out int readError))
        {
            return WriteError(
                $"Could not read the current input of monitor [{monitor.Index}] {monitor.Name}: {DdcErrors.Describe(readError)} (0x{readError:X8})",
                $"Use 'ddcswitch set {monitor.Index} input <source>' to switch explicitly, or run with --trace for details.",
                monitor, jsonOutput);
        }

        uint targetInput = DetermineTargetInput(currentInput, input1Value, input2Value);
        string? warning = currentInput != input1Value && currentInput != input2Value
            ? $"Current input '{InputSource.GetName(currentInput)}' is not one of the specified inputs, switching to '{InputSource.GetName(targetInput)}'"
            : null;

        bool success = false;
        int writeError = 0;
        void Switch() => success = monitor.TrySetVcpFeature(InputSource.VcpInputSource, targetInput, out writeError);

        if (jsonOutput)
        {
            Switch();
        }
        else
        {
            AnsiConsole.Status()
                .Spinner(Spinner.Known.Dots)
                .SpinnerStyle(Style.Parse("cyan"))
                .Start(Markup.Escape($"Switching {monitor.Name} to {InputSource.GetName(targetInput)}..."), _ => Switch());
        }

        if (!success)
        {
            return WriteError(
                $"Failed to switch monitor '{monitor.Name}' to {InputSource.GetName(targetInput)}: {DdcErrors.Describe(writeError)} (0x{writeError:X8})",
                null, monitor, jsonOutput);
        }

        OutputToggleSuccess(monitor, currentInput, targetInput, warning, jsonOutput);
        return 0;
    }

    /// <summary>
    /// Picks the input to switch to: the other of the pair, or input1 when the monitor is on neither.
    /// </summary>
    internal static uint DetermineTargetInput(uint currentInput, uint input1Value, uint input2Value)
    {
        if (currentInput == input1Value) return input2Value;
        if (currentInput == input2Value) return input1Value;
        return input1Value;
    }

    private static int HandleMissingArguments(int actualCount, bool jsonOutput)
    {
        string errorMessage = actualCount switch
        {
            1 => "Monitor and two input sources required",
            2 => "Two input sources required",
            3 => "Second input source required",
            _ => "Command 'toggle' requires monitor and two input sources"
        };

        return WriteError(errorMessage, "Usage: ddcswitch toggle <monitor> <input1> <input2>", null, jsonOutput);
    }

    private static int HandleInvalidInput(string invalidInput, string position, bool jsonOutput) =>
        WriteError(
            $"Invalid {position} input source: '{invalidInput}'",
            "Valid input sources include: HDMI1, HDMI2, DisplayPort1, DisplayPort2, DVI1, DVI2, VGA1, VGA2, or numeric codes (e.g., 0x11, 17)",
            null, jsonOutput);

    private static int WriteError(string message, string? hint, Monitor? monitor, bool jsonOutput)
    {
        if (jsonOutput)
        {
            var monitorRef = monitor == null
                ? null
                : new MonitorReference(monitor.Index, monitor.Name, monitor.DeviceName, monitor.IsPrimary);
            var error = new ErrorResponse(false, hint == null ? message : $"{message}. {hint}", monitorRef);
            Console.WriteLine(JsonSerializer.Serialize(error, JsonContext.Default.ErrorResponse));
        }
        else
        {
            ConsoleOutputFormatter.WriteError(Markup.Escape(message));
            if (hint != null)
            {
                AnsiConsole.MarkupLine($"[dim]{Markup.Escape(hint)}[/]");
            }
        }

        return 1;
    }

    private static void OutputToggleSuccess(Monitor monitor, uint currentInput, uint targetInput, string? warning, bool jsonOutput)
    {
        string fromInputName = InputSource.GetName(currentInput);
        string toInputName = InputSource.GetName(targetInput);

        if (jsonOutput)
        {
            var monitorRef = new MonitorReference(monitor.Index, monitor.Name, monitor.DeviceName, monitor.IsPrimary);
            var result = new ToggleInputResponse(
                Success: true,
                Monitor: monitorRef,
                FromInput: fromInputName,
                ToInput: toInputName,
                FromInputCode: currentInput,
                ToInputCode: targetInput,
                Warning: warning);
            Console.WriteLine(JsonSerializer.Serialize(result, JsonContext.Default.ToggleInputResponse));
            return;
        }

        var successPanel = new Panel(
            $"[bold white]{Markup.Escape(monitor.Name)}[/]\n" +
            $"[dim]Device:[/] [dim]{Markup.Escape(monitor.DeviceName)}[/]\n\n" +
            $"[cyan]{Markup.Escape(fromInputName)}[/] [dim](0x{currentInput:X2})[/]  [bold yellow]→[/]  [bold cyan]{Markup.Escape(toInputName)}[/] [dim](0x{targetInput:X2})[/]")
        {
            Header = new PanelHeader("[bold green]✓ 🔄 Input Toggled Successfully[/]", Justify.Left),
            Border = BoxBorder.Rounded,
            BorderStyle = new Style(Color.Green)
        };
        AnsiConsole.Write(successPanel);

        if (warning != null)
        {
            AnsiConsole.WriteLine();
            AnsiConsole.Write(new Panel(Markup.Escape(warning))
            {
                Header = new PanelHeader("[bold yellow]⚠ Warning[/]", Justify.Left),
                Border = BoxBorder.Rounded,
                BorderStyle = new Style(Color.Yellow)
            });
        }
    }
}
