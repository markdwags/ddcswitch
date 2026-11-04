namespace DDCSwitch;

/// <summary>
/// Picks a human-friendly monitor name: EDID manufacturer + model, then the Windows description,
/// then "Monitor N".
/// </summary>
public static class MonitorNameResolver
{
    public static string ResolveMonitorName(Monitor monitor, ParsedEdidInfo? edidInfo)
    {
        if (edidInfo != null)
        {
            var edidName = BuildEdidName(edidInfo);
            if (!string.IsNullOrEmpty(edidName))
            {
                return edidName;
            }
        }

        return GetFallbackName(monitor);
    }

    public static string GetFallbackName(Monitor monitor)
    {
        if (!string.IsNullOrEmpty(monitor.WindowsDescription) && monitor.WindowsDescription != "Generic PnP Monitor")
        {
            return monitor.WindowsDescription;
        }

        return $"Monitor {monitor.Index}";
    }

    internal static string BuildEdidName(ParsedEdidInfo edidInfo)
    {
        var nameParts = new List<string>();

        if (!string.IsNullOrEmpty(edidInfo.ManufacturerName) && edidInfo.ManufacturerName != edidInfo.ManufacturerCode)
        {
            nameParts.Add(edidInfo.ManufacturerName);
        }
        else if (!string.IsNullOrEmpty(edidInfo.ManufacturerCode))
        {
            nameParts.Add(edidInfo.ManufacturerCode);
        }

        var modelName = EdidParser.ParseModelName(edidInfo.RawData);
        if (!string.IsNullOrEmpty(modelName))
        {
            // Avoid "LG Electronics LG ULTRAGEAR"-style duplication when the model already names the brand.
            if (nameParts.Count == 1 && modelName.StartsWith(nameParts[0].Split(' ')[0] + " ", StringComparison.OrdinalIgnoreCase))
            {
                nameParts.Clear();
            }

            nameParts.Add(modelName);
        }
        else if (edidInfo.ProductCode != 0)
        {
            nameParts.Add($"0x{edidInfo.ProductCode:X4}");
        }

        return string.Join(" ", nameParts);
    }
}
