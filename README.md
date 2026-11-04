# ddcswitch

[![GitHub Release](https://img.shields.io/github/v/release/markdwags/ddcswitch)](https://github.com/markdwags/ddcswitch/releases)
[![License](https://img.shields.io/github/license/markdwags/ddcswitch)](https://github.com/markdwags/ddcswitch/blob/main/LICENSE)
[![Platform](https://img.shields.io/badge/platform-Windows-blue)](https://github.com/markdwags/ddcswitch)
[![.NET](https://img.shields.io/badge/.NET-10.0-512BD4)](https://dotnet.microsoft.com/)

A Windows command-line tool to control monitors over DDC/CI: switch inputs, set brightness and contrast, and read or write any VCP feature without touching the monitor's buttons. Ships as a single NativeAOT executable with no .NET runtime dependency.

📚 **[Examples](EXAMPLES.md)** | 📝 **[Changelog](CHANGELOG.md)**

## Installation

```powershell
choco install ddcswitch
```

Or download `ddcswitch.exe` from [Releases](../../releases) and put it on your `PATH`.

**Build from source** (.NET 10 SDK and the Visual Studio "Desktop development with C++" workload):

```powershell
git clone https://github.com/markdwags/ddcswitch.git
cd ddcswitch
.\build.ps1          # produces dist\ddcswitch.exe
```

## Usage

```powershell
ddcswitch list                          # monitors, current input, DDC/CI status
ddcswitch info 0                        # EDID details
ddcswitch get 0 brightness              # read a feature
ddcswitch set 0 brightness 75%          # write a feature
ddcswitch set 0 input HDMI1             # switch input
ddcswitch toggle 0 HDMI1 DP1            # flip between two inputs (great for hotkeys)
ddcswitch get 0 0x10                    # raw VCP code
ddcswitch get 0                         # scan all VCP codes on one monitor
ddcswitch get all                       # scan every monitor
```

**Selecting a monitor:** use its index from `list`, or part of its name, model, hardware ID or display device, e.g. `toggle VG270U HDMI1 DP1` or `get DISPLAY2 input`.

**Inputs:** `HDMI1`, `HDMI2`, `DP1`, `DP2`, `DVI1`, `DVI2`, `VGA1`, `VGA2`, or a hex code like `0x11`.

**Features:** `brightness`, `contrast`, `input`, `volume`, `mute`, `red-gain`, `green-gain`, `blue-gain`, and more by name, or any VCP code `0x00`–`0xFF`. Brightness, contrast and volume accept percentages.

**Flags:**

| Flag | Effect |
|------|--------|
| `--json` | Machine-readable output for scripts |
| `--verbose` | Extra EDID and brightness/contrast columns in `list` |
| `--trace` | Log every DDC/CI call with error code and timing to stderr (or set `DDCSWITCH_TRACE=1`) |

Set `NO_COLOR=1` for plain output without colors or icons.

## Troubleshooting

- **"No DDC/CI" or no response:** make sure DDC/CI is enabled in the monitor's OSD menu, then run the command again with `--trace` to see the exact error. Some monitors only accept DDC/CI on certain inputs or cables. Power-cycling the monitor can help.
- **"Failed to set input source":** the input may not exist on that monitor. Check `list`, or try the raw hex code.
- **Wrong current input shown:** some monitor firmware reports non-standard values even though switching still works.

For a GUI, try NirSoft's [ControlMyMonitor](https://www.nirsoft.net/utils/control_my_monitor.html).

## Why Windows only?

Linux already has [ddcutil](https://www.ddcutil.com/). ddcswitch fills the same niche on Windows as a standalone native executable.

## License

MIT. See [LICENSE](LICENSE). Provided as-is, without warranty: DDC/CI commands change monitor settings, so use at your own risk.

Built with [Spectre.Console](https://spectreconsole.net/). Inspired by ddcutil.
