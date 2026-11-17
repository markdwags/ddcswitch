using Spectre.Console;
using System.Text.Json;

namespace DdcSwitch.Commands;

internal static class ToggleCommand
{
    private const string StrictFlag = "--strict";

    private const string Usage =
        "Usage: ddcswitch toggle [--strict] <monitor> <input1> <input2>  or  ddcswitch toggle [--strict] <monitor> <feature> <value1> <value2> [value3...]";

    /// <summary>One toggle value as typed, plus its parsed raw value or percentage.</summary>
    internal readonly record struct ToggleValue(string Text, uint Raw, uint? Percentage);

    /// <summary>A parsed toggle command line. Values still need <see cref="TryResolveRawValues"/> once the monitor's max is known.</summary>
    internal sealed record ToggleRequest(string MonitorArg, VcpFeature Feature, IReadOnlyList<ToggleValue> Values, bool Strict);

    public static int Execute(string[] args, bool jsonOutput)
    {
        if (!TryParseArgs(args, out var request, out string? parseError, out string? parseHint))
        {
            return WriteError(parseError!, parseHint, null, jsonOutput);
        }

        var feature = request!.Feature;
        bool isInput = feature.Code == InputSource.VcpInputSource;

        using var monitors = MonitorController.EnumerateMonitors();
        if (monitors.Count == 0)
        {
            return WriteError("No DDC/CI capable monitors found", null, null, jsonOutput);
        }

        var monitor = MonitorController.FindMonitor(monitors, request.MonitorArg);
        if (monitor == null)
        {
            return WriteError($"Monitor '{request.MonitorArg}' not found", "Use 'ddcswitch list' to see available monitors.", null, jsonOutput);
        }

        // Read the current value. Monitor.TryGetVcpFeature already retries when the monitor does not reply.
        // If it still can't be read we stop: guessing would silently do nothing whenever the guess
        // matches the value the monitor is already on.
        if (!monitor.TryGetVcpFeature(feature.Code, out uint current, out uint max, out int readError))
        {
            return isInput
                ? WriteError(
                    $"Could not read the current input of monitor [{monitor.Index}] {monitor.Name}: {DdcErrors.Describe(readError)} (0x{readError:X8})",
                    $"Use 'ddcswitch set {monitor.Index} input <source>' to switch explicitly, or run with --trace for details.",
                    monitor, jsonOutput)
                : WriteError(DescribeDdcFailure(monitor, feature, "read", readError, null), null, monitor, jsonOutput);
        }

        if (!TryResolveRawValues(feature, request.Values, max, out uint[] raws, out string? resolveError))
        {
            return WriteError(resolveError!, null, monitor, jsonOutput);
        }

        int? targetIndex = DetermineTargetIndex(current, raws, request.Strict);
        if (targetIndex == null)
        {
            string allowed = string.Join(", ", raws.Select(r => FormatValue(feature, r, max)));
            return WriteError(
                $"Current {feature.Name} of monitor [{monitor.Index}] {monitor.Name} is {FormatValue(feature, current, max)}, which is not one of the toggle values ({allowed}); nothing was changed",
                $"Remove {StrictFlag} to switch to the first value ({FormatValue(feature, raws[0], max)}) instead.",
                monitor, jsonOutput);
        }

        uint target = raws[targetIndex.Value];
        string? warning = raws.Contains(current)
            ? null
            : isInput
                ? $"Current input '{InputSource.GetName(current)}' is not one of the specified inputs, switching to '{InputSource.GetName(target)}'"
                : $"Current {feature.Name} {FormatValue(feature, current, max)} is not one of the specified values, switching to {FormatValue(feature, target, max)}";

        bool success = false;
        int writeError = 0;
        void Switch() => success = monitor.TrySetVcpFeature(feature.Code, target, out writeError);

        if (jsonOutput)
        {
            Switch();
        }
        else
        {
            string status = isInput
                ? $"Switching {monitor.Name} to {InputSource.GetName(target)}..."
                : $"Setting {monitor.Name} {feature.Name} to {FormatValue(feature, target, max)}...";
            AnsiConsole.Status()
                .Spinner(Spinner.Known.Dots)
                .SpinnerStyle(Style.Parse("cyan"))
                .Start(Markup.Escape(status), _ => Switch());
        }

        if (!success)
        {
            return isInput
                ? WriteError(
                    $"Failed to switch monitor '{monitor.Name}' to {InputSource.GetName(target)}: {DdcErrors.Describe(writeError)} (0x{writeError:X8})",
                    null, monitor, jsonOutput)
                : WriteError(DescribeDdcFailure(monitor, feature, "write", writeError, target), null, monitor, jsonOutput);
        }

        if (isInput)
        {
            OutputInputToggleSuccess(monitor, current, target, warning, jsonOutput);
        }
        else
        {
            OutputFeatureToggleSuccess(monitor, feature, current, target, max, warning, jsonOutput);
        }

        return 0;
    }

    /// <summary>
    /// Parses <c>toggle [--strict] &lt;monitor&gt; &lt;input1&gt; &lt;input2&gt;</c> and
    /// <c>toggle [--strict] &lt;monitor&gt; &lt;feature&gt; &lt;value1&gt; &lt;value2&gt; [...]</c>.
    /// </summary>
    /// <remarks>
    /// Exactly two arguments after the monitor are always the input form, so <c>toggle 0 0x11 0x0F</c>
    /// keeps meaning "two inputs" even though 0x11 is also a VCP code. With three or more, the first one
    /// is the feature; if it isn't one but every argument is an input, they are inputs to cycle through.
    /// </remarks>
    internal static bool TryParseArgs(string[] args, out ToggleRequest? request, out string? error, out string? hint)
    {
        request = null;
        hint = Usage;

        bool strict = args.Any(IsStrictFlag);
        args = args.Where(a => !IsStrictFlag(a)).ToArray();

        // args[0] is "toggle"
        if (args.Length < 4)
        {
            error = args.Length switch
            {
                1 => "Monitor and two input sources required",
                2 => "Two input sources required",
                _ => FeatureResolver.TryResolveFeature(args[2], out var f) && !InputSource.TryParse(args[2], out _)
                    ? $"toggle needs at least two values for '{f!.Name}'"
                    : "Second input source required"
            };
            return false;
        }

        string monitorArg = args[1];
        string[] rest = args[2..];

        if (rest.Length == 2)
        {
            if (!InputSource.TryParse(rest[0], out _) && FeatureResolver.TryResolveFeature(rest[0], out var f))
            {
                error = $"toggle needs at least two values for '{f!.Name}'";
                return false;
            }

            return TryParseValues(monitorArg, VcpFeature.InputSource, rest, strict, out request, out error, out hint);
        }

        if (FeatureResolver.TryResolveFeature(rest[0], out var feature))
        {
            return TryParseValues(monitorArg, feature!, rest[1..], strict, out request, out error, out hint);
        }

        if (rest.All(r => InputSource.TryParse(r, out _)))
        {
            return TryParseValues(monitorArg, VcpFeature.InputSource, rest, strict, out request, out error, out hint);
        }

        error = $"Invalid feature '{rest[0]}'. {FeatureResolver.GetVcpCodeValidationError(rest[0])}";
        hint = "Valid features: brightness, contrast, input, or VCP code (0x10, 0x12, etc.)";
        return false;
    }

    private static bool IsStrictFlag(string arg) => arg.Equals(StrictFlag, StringComparison.OrdinalIgnoreCase);

    private static bool TryParseValues(string monitorArg, VcpFeature feature, string[] valueArgs, bool strict,
        out ToggleRequest? request, out string? error, out string? hint)
    {
        request = null;
        hint = null;
        bool isInput = feature.Code == InputSource.VcpInputSource;

        if (feature.Type != VcpFeatureType.ReadWrite)
        {
            error = $"Feature '{feature.Name}' is {(feature.Type == VcpFeatureType.ReadOnly ? "read-only" : "write-only")} and can't be toggled";
            hint = "toggle reads the current value and writes the next one, so the feature must be readable and writable.";
            return false;
        }

        var values = new List<ToggleValue>(valueArgs.Length);
        for (int i = 0; i < valueArgs.Length; i++)
        {
            var (raw, percentage, validationError) = SetCommand.ParseAndValidateValue(feature, valueArgs[i]);
            if (validationError != null)
            {
                error = isInput && valueArgs.Length == 2
                    ? $"Invalid {(i == 0 ? "first" : "second")} input source: '{valueArgs[i]}'"
                    : validationError;
                if (isInput)
                {
                    hint = "Valid input sources include: HDMI1, HDMI2, DisplayPort1, DisplayPort2, DVI1, DVI2, VGA1, VGA2, or numeric codes (e.g., 0x11, 17)";
                }

                return false;
            }

            values.Add(new ToggleValue(valueArgs[i], raw, percentage));
        }

        // Percentages can only be compared once the monitor's max is known; raw values can be checked now,
        // before any DDC/CI traffic.
        if (values.All(v => v.Percentage == null) && TryFindDuplicate(feature, values, values.Select(v => v.Raw).ToArray()) is { } duplicate)
        {
            error = duplicate;
            hint = isInput ? "Please specify different input sources to toggle between" : null;
            return false;
        }

        request = new ToggleRequest(monitorArg, feature, values, strict);
        error = null;
        return true;
    }

    /// <summary>
    /// Converts the toggle values to raw values for a monitor whose max for the feature is <paramref name="maxValue"/>,
    /// checking range and rejecting values that end up the same.
    /// </summary>
    internal static bool TryResolveRawValues(VcpFeature feature, IReadOnlyList<ToggleValue> values, uint maxValue, out uint[] raws, out string? error)
    {
        bool isInput = feature.Code == InputSource.VcpInputSource;
        raws = new uint[values.Count];

        for (int i = 0; i < values.Count; i++)
        {
            var value = values[i];
            if (value.Percentage is uint percentage)
            {
                raws[i] = FeatureResolver.ConvertPercentageToRaw(percentage, maxValue);
            }
            else
            {
                if (!isInput && !FeatureResolver.IsValidRawVcpValue(value.Raw, maxValue))
                {
                    error = VcpErrorHandler.CreateRangeValidationMessage(feature, value.Raw, maxValue);
                    return false;
                }

                raws[i] = value.Raw;
            }
        }

        error = TryFindDuplicate(feature, values, raws);
        return error == null;
    }

    private static string? TryFindDuplicate(VcpFeature feature, IReadOnlyList<ToggleValue> values, uint[] raws)
    {
        for (int i = 0; i < raws.Length; i++)
        {
            for (int j = i + 1; j < raws.Length; j++)
            {
                if (raws[i] != raws[j]) continue;

                return feature.Code == InputSource.VcpInputSource
                    ? $"Input sources must be different. '{values[i].Text}' and '{values[j].Text}' both resolve to {InputSource.GetName(raws[i])}"
                    : $"Toggle values must be different. '{values[i].Text}' and '{values[j].Text}' both resolve to raw value {raws[i]} for {feature.Name}";
            }
        }

        return null;
    }

    /// <summary>
    /// Picks the index of the value to switch to: the one after the current value (wrapping around),
    /// or the first value when the monitor is on none of them. Returns null in that case when <paramref name="strict"/>.
    /// </summary>
    internal static int? DetermineTargetIndex(uint current, IReadOnlyList<uint> values, bool strict)
    {
        for (int i = 0; i < values.Count; i++)
        {
            if (values[i] == current) return (i + 1) % values.Count;
        }

        return strict ? null : 0;
    }

    /// <summary>Formats a raw value for messages: input name, percentage, or the raw number.</summary>
    private static string FormatValue(VcpFeature feature, uint raw, uint maxValue)
    {
        if (feature.Code == InputSource.VcpInputSource) return InputSource.GetName(raw);
        if (feature.SupportsPercentage)
        {
            uint percentage = FeatureResolver.ConvertRawToPercentage(raw, maxValue);
            return percentage == raw ? $"{percentage}%" : $"{percentage}% (raw {raw})";
        }

        return raw.ToString();
    }

    private static string DescribeDdcFailure(Monitor monitor, VcpFeature feature, string operation, int errorCode, uint? attemptedValue)
    {
        if (VcpErrorHandler.IsTimeoutError(errorCode)) return VcpErrorHandler.CreateTimeoutMessage(monitor, feature, operation);
        if (VcpErrorHandler.IsUnsupportedFeatureError(errorCode)) return VcpErrorHandler.CreateUnsupportedFeatureMessage(monitor, feature);
        if (errorCode == 0x00000006) return VcpErrorHandler.CreateCommunicationFailureMessage(monitor); // ERROR_INVALID_HANDLE
        return attemptedValue is uint value
            ? VcpErrorHandler.CreateWriteFailureMessage(monitor, feature, value)
            : VcpErrorHandler.CreateReadFailureMessage(monitor, feature);
    }

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

    private static void OutputInputToggleSuccess(Monitor monitor, uint currentInput, uint targetInput, string? warning, bool jsonOutput)
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

        WriteSuccessPanel(monitor, "Input Toggled Successfully",
            $"[cyan]{Markup.Escape(fromInputName)}[/] [dim](0x{currentInput:X2})[/]  [bold yellow]→[/]  [bold cyan]{Markup.Escape(toInputName)}[/] [dim](0x{targetInput:X2})[/]",
            warning);
    }

    private static void OutputFeatureToggleSuccess(Monitor monitor, VcpFeature feature, uint current, uint target, uint maxValue, string? warning, bool jsonOutput)
    {
        uint? fromPercentage = feature.SupportsPercentage ? FeatureResolver.ConvertRawToPercentage(current, maxValue) : null;
        uint? toPercentage = feature.SupportsPercentage ? FeatureResolver.ConvertRawToPercentage(target, maxValue) : null;

        if (jsonOutput)
        {
            var monitorRef = new MonitorReference(monitor.Index, monitor.Name, monitor.DeviceName, monitor.IsPrimary);
            var result = new ToggleVcpResponse(
                Success: true,
                Monitor: monitorRef,
                FeatureName: feature.Name,
                FromValue: current,
                ToValue: target,
                MaxValue: maxValue,
                FromPercentage: fromPercentage,
                ToPercentage: toPercentage,
                Warning: warning);
            Console.WriteLine(JsonSerializer.Serialize(result, JsonContext.Default.ToggleVcpResponse));
            return;
        }

        string from = fromPercentage.HasValue ? $"[cyan]{fromPercentage}%[/] [dim](raw: {current})[/]" : $"[cyan]{current}[/]";
        string to = toPercentage.HasValue ? $"[bold green]{toPercentage}%[/] [dim](raw: {target})[/]" : $"[bold green]{target}[/]";

        WriteSuccessPanel(monitor, "Toggled Successfully",
            $"[bold yellow]{Markup.Escape(feature.Name)}:[/] {from}  [bold yellow]→[/]  {to}",
            warning);
    }

    private static void WriteSuccessPanel(Monitor monitor, string title, string changeMarkup, string? warning)
    {
        var successPanel = new Panel(
            $"[bold white]{Markup.Escape(monitor.Name)}[/]\n" +
            $"[dim]Device:[/] [dim]{Markup.Escape(monitor.DeviceName)}[/]\n\n" +
            changeMarkup)
        {
            Header = new PanelHeader($"[bold green]✓ 🔄 {title}[/]", Justify.Left),
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
