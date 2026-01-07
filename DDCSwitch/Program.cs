using DDCSwitch;
using Spectre.Console;
using System.Text.Json;
using System.Text.Json.Serialization;

return DDCSwitchProgram.Run(args);

static class DDCSwitchProgram
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        TypeInfoResolver = JsonContext.Default
    };

    public static int Run(string[] args)
    {
        if (args.Length == 0)
        {
            ShowUsage();
            return 1;
        }

        // Check for --json flag
        bool jsonOutput = args.Contains("--json", StringComparer.OrdinalIgnoreCase);
        var filteredArgs = args.Where(a => !a.Equals("--json", StringComparison.OrdinalIgnoreCase)).ToArray();

        if (filteredArgs.Length == 0)
        {
            ShowUsage();
            return 1;
        }

        var command = filteredArgs[0].ToLowerInvariant();

        try
        {
            return command switch
            {
                "list" or "ls" => ListMonitors(jsonOutput),
                "get" => GetCurrentInput(filteredArgs, jsonOutput),
                "set" => SetInput(filteredArgs, jsonOutput),
                "help" or "-h" or "--help" or "/?" => ShowUsage(),
                _ => InvalidCommand(filteredArgs[0], jsonOutput)
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
                AnsiConsole.MarkupLine($"[red]Error:[/] {ex.Message}");
            }
            return 1;
        }
    }

    private static int ShowUsage()
    {
        AnsiConsole.Write(new FigletText("DDCSwitch").Color(Color.Blue));
        AnsiConsole.MarkupLine("[dim]Windows DDC/CI Monitor Input Switcher[/]\n");

        var table = new Table()
            .Border(TableBorder.Rounded)
            .AddColumn("Command")
            .AddColumn("Description")
            .AddColumn("Example");

        table.AddRow(
            "[yellow]list[/] or [yellow]ls[/]",
            "List all DDC/CI capable monitors",
            "[dim]DDCSwitch list[/]");

        table.AddRow(
            "[yellow]get[/] <monitor>",
            "Get current input source for a monitor",
            "[dim]DDCSwitch get 0[/]");

        table.AddRow(
            "[yellow]set[/] <monitor> <input>",
            "Set input source for a monitor",
            "[dim]DDCSwitch set 0 HDMI1[/]");

        AnsiConsole.Write(table);

        AnsiConsole.MarkupLine("\n[bold]Monitor:[/] Monitor index (0, 1, 2...) or name pattern");
        AnsiConsole.MarkupLine("[bold]Input:[/] Input name (HDMI1, HDMI2, DP1, DP2, DVI1, VGA1) or hex code (0x11)");
        AnsiConsole.MarkupLine("\n[bold]Options:[/]");
        AnsiConsole.MarkupLine("  [yellow]--json[/]    Output in JSON format");

        return 0;
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
            AnsiConsole.MarkupLine($"[red]Unknown command:[/] {command}");
            AnsiConsole.MarkupLine("Run [yellow]DDCSwitch help[/] for usage information.");
        }
        return 1;
    }

    private static int ListMonitors(bool jsonOutput)
{
    if (!jsonOutput)
    {
        AnsiConsole.Status()
            .Start("Enumerating monitors...", ctx =>
            {
                ctx.Spinner(Spinner.Known.Dots);
                Thread.Sleep(100); // Brief pause for visual feedback
            });
    }

    var monitors = MonitorController.EnumerateMonitors();

    if (monitors.Count == 0)
    {
        if (jsonOutput)
        {
            var result = new ListMonitorsResponse(false, null, "No DDC/CI capable monitors found");
            Console.WriteLine(JsonSerializer.Serialize(result, JsonContext.Default.ListMonitorsResponse));
        }
        else
        {
            AnsiConsole.MarkupLine("[yellow]No DDC/CI capable monitors found.[/]");
        }
        return 1;
    }

    if (jsonOutput)
    {
        var monitorList = monitors.Select(monitor =>
        {
            string? inputName = null;
            uint? inputCode = null;
            string status = "ok";

            try
            {
                if (monitor.TryGetInputSource(out uint current, out uint max))
                {
                    inputName = InputSource.GetName(current);
                    inputCode = current;
                }
                else
                {
                    status = "no_ddc_ci";
                }
            }
            catch
            {
                status = "error";
            }

            return new MonitorInfo(
                monitor.Index,
                monitor.Name,
                monitor.DeviceName,
                monitor.IsPrimary,
                inputName,
                inputCode != null ? $"0x{inputCode:X2}" : null,
                status);
        }).ToList();

        var result = new ListMonitorsResponse(true, monitorList);
        Console.WriteLine(JsonSerializer.Serialize(result, JsonContext.Default.ListMonitorsResponse));
    }
    else
    {
        var table = new Table()
            .Border(TableBorder.Rounded)
            .AddColumn("Index")
            .AddColumn("Monitor Name")
            .AddColumn("Device")
            .AddColumn("Current Input")
            .AddColumn("Status");

        foreach (var monitor in monitors)
        {
            string inputInfo = "N/A";
            string status = "[green]OK[/]";

            try
            {
                if (monitor.TryGetInputSource(out uint current, out uint max))
                {
                    inputInfo = $"{InputSource.GetName(current)} (0x{current:X2})";
                }
                else
                {
                    status = "[yellow]No DDC/CI[/]";
                }
            }
            catch
            {
                status = "[red]Error[/]";
            }

            table.AddRow(
                monitor.IsPrimary ? $"{monitor.Index} [yellow]*[/]" : monitor.Index.ToString(),
                monitor.Name,
                monitor.DeviceName,
                inputInfo,
                status);
        }

        AnsiConsole.Write(table);
    }

    // Cleanup
    foreach (var monitor in monitors)
    {
        monitor.Dispose();
    }

    return 0;
    }

    private static int GetCurrentInput(string[] args, bool jsonOutput)
{
    if (args.Length < 2)
    {
        if (jsonOutput)
        {
            var error = new ErrorResponse(false, "Monitor identifier required");
            Console.WriteLine(JsonSerializer.Serialize(error, JsonContext.Default.ErrorResponse));
        }
        else
        {
            AnsiConsole.MarkupLine("[red]Error:[/] Monitor identifier required.");
            AnsiConsole.MarkupLine("Usage: [yellow]DDCSwitch get <monitor>[/]");
        }
        return 1;
    }

    var monitors = MonitorController.EnumerateMonitors();

    if (monitors.Count == 0)
    {
        if (jsonOutput)
        {
            var error = new ErrorResponse(false, "No DDC/CI capable monitors found");
            Console.WriteLine(JsonSerializer.Serialize(error, JsonContext.Default.ErrorResponse));
        }
        else
        {
            AnsiConsole.MarkupLine("[red]Error:[/] No DDC/CI capable monitors found.");
        }
        return 1;
    }

    var monitor = MonitorController.FindMonitor(monitors, args[1]);

    if (monitor == null)
    {
        if (jsonOutput)
        {
            var error = new ErrorResponse(false, $"Monitor '{args[1]}' not found");
            Console.WriteLine(JsonSerializer.Serialize(error, JsonContext.Default.ErrorResponse));
        }
        else
        {
            AnsiConsole.MarkupLine($"[red]Error:[/] Monitor '{args[1]}' not found.");
            AnsiConsole.MarkupLine("Use [yellow]DDCSwitch list[/] to see available monitors.");
        }

        // Cleanup
        foreach (var m in monitors)
        {
            m.Dispose();
        }

        return 1;
    }

    if (!monitor.TryGetInputSource(out uint current, out uint max))
    {
        if (jsonOutput)
        {
            var monitorRef = new MonitorReference(monitor.Index, monitor.Name, monitor.DeviceName, monitor.IsPrimary);
            var error = new ErrorResponse(false, $"Failed to get input source from monitor '{monitor.Name}'", monitorRef);
            Console.WriteLine(JsonSerializer.Serialize(error, JsonContext.Default.ErrorResponse));
        }
        else
        {
            AnsiConsole.MarkupLine($"[red]Error:[/] Failed to get input source from monitor '{monitor.Name}'.");
            AnsiConsole.MarkupLine("The monitor may not support DDC/CI or requires administrator privileges.");
        }

        // Cleanup
        foreach (var m in monitors)
        {
            m.Dispose();
        }

        return 1;
    }

    if (jsonOutput)
    {
        var monitorRef = new MonitorReference(monitor.Index, monitor.Name, monitor.DeviceName, monitor.IsPrimary);
        var result = new GetInputResponse(true, monitorRef, InputSource.GetName(current), $"0x{current:X2}", max);
        Console.WriteLine(JsonSerializer.Serialize(result, JsonContext.Default.GetInputResponse));
    }
    else
    {
        AnsiConsole.MarkupLine($"[green]Monitor:[/] {monitor.Name} ({monitor.DeviceName})");
        AnsiConsole.MarkupLine($"[green]Current Input:[/] {InputSource.GetName(current)} (0x{current:X2})");
    }

    // Cleanup
    foreach (var m in monitors)
    {
        m.Dispose();
    }

    return 0;
    }

    private static int SetInput(string[] args, bool jsonOutput)
    {
        if (args.Length < 3)
        {
            if (jsonOutput)
            {
                var error = new ErrorResponse(false, "Monitor and input source required");
                Console.WriteLine(JsonSerializer.Serialize(error, JsonContext.Default.ErrorResponse));
            }
            else
            {
                AnsiConsole.MarkupLine("[red]Error:[/] Monitor and input source required.");
                AnsiConsole.MarkupLine("Usage: [yellow]DDCSwitch set <monitor> <input>[/]");
            }
            return 1;
        }

        if (!InputSource.TryParse(args[2], out uint inputValue))
        {
            if (jsonOutput)
            {
                var error = new ErrorResponse(false, $"Invalid input source '{args[2]}'");
                Console.WriteLine(JsonSerializer.Serialize(error, JsonContext.Default.ErrorResponse));
            }
            else
            {
                AnsiConsole.MarkupLine($"[red]Error:[/] Invalid input source '{args[2]}'.");
                AnsiConsole.MarkupLine("Valid inputs: HDMI1, HDMI2, DP1, DP2, DVI1, DVI2, VGA1, VGA2, or hex code (0x11)");
            }
            return 1;
        }

        var monitors = MonitorController.EnumerateMonitors();

        if (monitors.Count == 0)
        {
            if (jsonOutput)
            {
                var error = new ErrorResponse(false, "No DDC/CI capable monitors found");
                Console.WriteLine(JsonSerializer.Serialize(error, JsonContext.Default.ErrorResponse));
            }
            else
            {
                AnsiConsole.MarkupLine("[red]Error:[/] No DDC/CI capable monitors found.");
            }
            return 1;
        }

        var monitor = MonitorController.FindMonitor(monitors, args[1]);

        if (monitor == null)
        {
            if (jsonOutput)
            {
                var error = new ErrorResponse(false, $"Monitor '{args[1]}' not found");
                Console.WriteLine(JsonSerializer.Serialize(error, JsonContext.Default.ErrorResponse));
            }
            else
            {
                AnsiConsole.MarkupLine($"[red]Error:[/] Monitor '{args[1]}' not found.");
                AnsiConsole.MarkupLine("Use [yellow]DDCSwitch list[/] to see available monitors.");
            }

            // Cleanup
            foreach (var m in monitors)
            {
                m.Dispose();
            }

            return 1;
        }

        bool success = false;
        string? errorMsg = null;

        if (!jsonOutput)
        {
            AnsiConsole.Status()
                .Start($"Switching {monitor.Name} to {InputSource.GetName(inputValue)}...", ctx =>
                {
                    ctx.Spinner(Spinner.Known.Dots);

                    if (!monitor.TrySetInputSource(inputValue))
                    {
                        errorMsg = $"Failed to set input source on monitor '{monitor.Name}'. The monitor may not support this input or requires administrator privileges.";
                    }
                    else
                    {
                        success = true;
                        // Give the monitor a moment to switch
                        Thread.Sleep(500);
                    }
                });
        }
        else
        {
            if (!monitor.TrySetInputSource(inputValue))
            {
                errorMsg = $"Failed to set input source on monitor '{monitor.Name}'";
            }
            else
            {
                success = true;
                Thread.Sleep(500);
            }
        }

        if (!success)
        {
            if (jsonOutput)
            {
                var monitorRef = new MonitorReference(monitor.Index, monitor.Name, monitor.DeviceName, monitor.IsPrimary);
                var error = new ErrorResponse(false, errorMsg!, monitorRef);
                Console.WriteLine(JsonSerializer.Serialize(error, JsonContext.Default.ErrorResponse));
            }
            else
            {
                AnsiConsole.MarkupLine($"[red]Error:[/] {errorMsg}");
            }

            // Cleanup
            foreach (var m in monitors)
            {
                m.Dispose();
            }

            return 1;
        }

        if (jsonOutput)
        {
            var monitorRef = new MonitorReference(monitor.Index, monitor.Name, monitor.DeviceName, monitor.IsPrimary);
            var result = new SetInputResponse(true, monitorRef, InputSource.GetName(inputValue), $"0x{inputValue:X2}");
            Console.WriteLine(JsonSerializer.Serialize(result, JsonContext.Default.SetInputResponse));
        }
        else
        {
            AnsiConsole.MarkupLine($"[green]✓[/] Successfully switched {monitor.Name} to {InputSource.GetName(inputValue)}");
        }

        // Cleanup
        foreach (var m in monitors)
        {
            m.Dispose();
        }

        return 0;
    }
}