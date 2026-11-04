using Spectre.Console;
using System.Text.Json;

namespace DDCSwitch.Commands;

internal static class ListCommand
{
    private enum DdcStatus { Ok, NoDdcCi }

    private sealed record Probe(DdcStatus Status, uint? InputCode, string? Brightness, string? Contrast);

    public static int Execute(bool jsonOutput, bool verboseOutput)
    {
        using var monitors = MonitorController.EnumerateMonitors();

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

        List<Probe> probes = null!;
        if (jsonOutput)
        {
            probes = monitors.Select(m => ProbeMonitor(m, verboseOutput)).ToList();
            OutputJsonList(monitors, probes);
        }
        else
        {
            AnsiConsole.Status()
                .Spinner(Spinner.Known.Dots)
                .SpinnerStyle(Style.Parse("cyan"))
                .Start("Querying monitors...", _ => probes = monitors.Select(m => ProbeMonitor(m, verboseOutput)).ToList());
            OutputTableList(monitors, probes, verboseOutput);
        }

        return 0;
    }

    /// <summary>
    /// Talks to one monitor once. A monitor that answers "VCP code not supported" for input select
    /// still has working DDC/CI (e.g. a single-input display).
    /// </summary>
    private static Probe ProbeMonitor(Monitor monitor, bool verboseOutput)
    {
        DdcStatus status;
        uint? inputCode = null;

        if (monitor.TryGetVcpFeature(InputSource.VcpInputSource, out uint current, out _, out int error))
        {
            status = DdcStatus.Ok;
            inputCode = current;
        }
        else
        {
            status = DdcErrors.IsNotSupported(error) ? DdcStatus.Ok : DdcStatus.NoDdcCi;
        }

        string? brightness = null, contrast = null;
        if (verboseOutput)
        {
            brightness = status == DdcStatus.Ok ? ReadPercentage(monitor, VcpFeature.Brightness.Code) : "N/A";
            contrast = status == DdcStatus.Ok ? ReadPercentage(monitor, VcpFeature.Contrast.Code) : "N/A";
        }

        return new Probe(status, inputCode, brightness, contrast);
    }

    private static string ReadPercentage(Monitor monitor, byte code) =>
        monitor.TryGetVcpFeature(code, out uint value, out uint max)
            ? $"{FeatureResolver.ConvertRawToPercentage(value, max)}%"
            : "N/A";

    private static void OutputJsonList(IReadOnlyList<Monitor> monitors, List<Probe> probes)
    {
        var monitorList = monitors.Select((monitor, i) =>
        {
            var probe = probes[i];
            return new MonitorInfo(
                monitor.Index,
                monitor.Name,
                monitor.DeviceName,
                monitor.IsPrimary,
                probe.InputCode is { } code ? InputSource.GetName(code) : null,
                probe.InputCode is { } c ? $"0x{c:X2}" : null,
                probe.Status == DdcStatus.Ok ? "ok" : "no_ddc_ci",
                probe.Brightness,
                probe.Contrast,
                monitor.ManufacturerId,
                monitor.ManufacturerName,
                monitor.ModelName,
                monitor.SerialNumber,
                monitor.ProductCode,
                monitor.ManufactureYear,
                monitor.ManufactureWeek);
        }).ToList();

        var result = new ListMonitorsResponse(true, monitorList);
        Console.WriteLine(JsonSerializer.Serialize(result, JsonContext.Default.ListMonitorsResponse));
    }

    private static void OutputTableList(IReadOnlyList<Monitor> monitors, List<Probe> probes, bool verboseOutput)
    {
        int ddcciCount = probes.Count(p => p.Status == DdcStatus.Ok);
        int primaryCount = monitors.Count(m => m.IsPrimary);

        var summaryPanel = new Panel(
            $"[bold white]Total Monitors:[/] [cyan]{monitors.Count}[/]  " +
            $"[bold white]DDC/CI Capable:[/] [green]{ddcciCount}[/]  " +
            $"[bold white]Primary:[/] [yellow]{primaryCount}[/]")
        {
            Header = new PanelHeader("[bold cyan]🖥️  Monitor Overview[/]", Justify.Left),
            Border = BoxBorder.Rounded,
            BorderStyle = new Style(Color.Cyan)
        };
        AnsiConsole.Write(summaryPanel);
        AnsiConsole.WriteLine();

        var table = new Table()
            .Border(TableBorder.Rounded)
            .BorderColor(Color.White)
            .AddColumn(new TableColumn("[bold yellow]Index[/]").Centered())
            .AddColumn(new TableColumn("[bold yellow]Monitor Name[/]").LeftAligned())
            .AddColumn(new TableColumn("[bold yellow]Device[/]").LeftAligned())
            .AddColumn(new TableColumn("[bold yellow]Current Input[/]").LeftAligned());

        if (verboseOutput)
        {
            table.AddColumn(new TableColumn("[bold yellow]Manufacturer[/]").LeftAligned());
            table.AddColumn(new TableColumn("[bold yellow]Model[/]").LeftAligned());
            table.AddColumn(new TableColumn("[bold yellow]Serial[/]").LeftAligned());
            table.AddColumn(new TableColumn("[bold yellow]Mfg Date[/]").LeftAligned());
            table.AddColumn(new TableColumn("[bold yellow]Brightness[/]").Centered());
            table.AddColumn(new TableColumn("[bold yellow]Contrast[/]").Centered());
        }

        table.AddColumn(new TableColumn("[bold yellow]Status[/]").Centered());

        for (int i = 0; i < monitors.Count; i++)
        {
            var monitor = monitors[i];
            var probe = probes[i];

            var row = new List<string>
            {
                monitor.IsPrimary ? $"[bold cyan]{monitor.Index}[/] [yellow]●[/]" : $"[cyan]{monitor.Index}[/]",
                Markup.Escape(monitor.Name),
                $"[dim]{Markup.Escape(monitor.DeviceName)}[/]",
                probe.InputCode is { } code
                    ? $"[cyan]{Markup.Escape(InputSource.GetName(code))}[/] [dim](0x{code:X2})[/]"
                    : "[dim]N/A[/]"
            };

            if (verboseOutput)
            {
                row.Add(Cyan(monitor.ManufacturerName));
                row.Add(Cyan(monitor.ModelName));
                row.Add(monitor.SerialNumber != null ? $"[dim]{Markup.Escape(monitor.SerialNumber)}[/]" : "[dim]N/A[/]");
                row.Add(monitor.ManufactureYear.HasValue
                    ? monitor.ManufactureWeek.HasValue
                        ? $"[dim]{monitor.ManufactureYear}/W{monitor.ManufactureWeek}[/]"
                        : $"[dim]{monitor.ManufactureYear}[/]"
                    : "[dim]N/A[/]");
                row.Add(Percentage(probe.Brightness));
                row.Add(Percentage(probe.Contrast));
            }

            row.Add(probe.Status == DdcStatus.Ok
                ? "[green]✓[/] [bold green]OK[/]"
                : "[yellow]⚠[/] [bold yellow]No DDC/CI[/]");

            table.AddRow(row.ToArray());
        }

        AnsiConsole.Write(table);
    }

    private static string Cyan(string? value) => value != null ? $"[cyan]{Markup.Escape(value)}[/]" : "[dim]N/A[/]";

    private static string Percentage(string? value) =>
        value is null or "N/A" ? "[dim]N/A[/]" : $"[green]{value}[/]";
}
