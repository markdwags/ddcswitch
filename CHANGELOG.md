# Changelog

All notable changes to ddcswitch will be documented in this file.

## [1.0.5] - 2026-11-04

### Fixed
- Monitors are now identified correctly: each monitor's EDID is read from its own device instance
  (via `EnumDisplayDevices`) instead of being guessed from registry heuristics, which could give every
  monitor the same name
- Monitor 0 reported "No DDC/CI" and could not be controlled unless ControlMyMonitor was running.
  dxva2 physical monitor handles are small integers from a pool shared across processes, so the first
  monitor gets handle 0 when no other DDC/CI tool is open; ddcswitch treated 0 as an invalid handle
  and never sent it any DDC/CI request. ControlMyMonitor "fixed" it by holding the low handle values
- DDC/CI reads and writes retry up to 3 times when the monitor doesn't reply
- `toggle` now reports an error when the current input can't be read, instead of silently switching to the
  first input (a no-op whenever the monitor was already on it)
- Monitors can be selected by name, model, hardware ID or display device (e.g. `toggle VG270U hdmi1 dp1`),
  as the README already documented
- A monitor that answers "not supported" for input select is no longer reported as "No DDC/CI"
- Physical monitor handles are no longer leaked during enumeration
- `info` reported wrong EDID chromaticity: each color's y coordinate repeated its x coordinate (bytes 31-34
  were never read). Coordinates are now decoded per the EDID 1.4 layout
- `info` showed display type "Undefined" for most digital monitors. For digital EDID 1.4 monitors those
  bits list the supported color encodings, which are now shown instead (e.g. `RGB 4:4:4 + YCrCb 4:4:4 +
  YCrCb 4:2:2`); the `displayType` value in `info --json` changes accordingly
- GitHub release notes were truncated at the first `###` subsection of the changelog

### Added
- `--trace` flag / `DDCSWITCH_TRACE=1` to log every DDC/CI call with error codes and timing to stderr

### Changed
- Enumeration no longer sends any DDC/CI traffic; only the targeted monitor is queried, making `toggle`,
  `get` and `set` faster
- Removed the 1 second pause after `toggle`
- Added a unit test project (`DDCSwitch.Tests`, xUnit v3), run on every CI build
- Built with .NET SDK 10.0.403 (pinned in `global.json`), which patches the NativeAOT compiler for
  GHSA-p3jp-9v83-76w9. Spectre.Console updated to 0.57.2
- CI: tests and coverage on every PR, NuGet caching, superseded runs cancelled, least-privilege permissions,
  and releases are created only when the changelog version is new. Release and Chocolatey packaging
  now run in one job
- Removed unused hardware-inspection and DDC/CI "identity" code

## [1.0.4] - 2026-01-09

### Added
- Enhanced visual styling across all commands with improved panels, tables, and progress bars
- Better color hierarchy and readability optimized for Windows Terminal and Command Prompt dark backgrounds
- Improved status indicators with clear symbols (✓/✗/⚠) for monitor and feature status
- Visual progress bars for percentage-based features (brightness, contrast) in `get` command
- Summary statistics in `list` command showing monitor count and DDC/CI status overview
- Better visual separation and formatting in `set`, `toggle`, and scan commands
- Documentation for `NO_COLOR` environment variable support (automatic via Spectre.Console)
- Consistent panel styling with icons for better visual hierarchy (📋⚡🎨📡)

### Improved
- Error messages now include better formatting and actionable hints
- Success panels show more detailed information with clear visual structure
- Table styling enhanced for better readability on dark backgrounds
- Warning messages have improved formatting and context

## [1.0.3] - 2026-01-08

### Added
- Added the toggle command to flip a monitor between two input sources
- `info` command to display detailed EDID (Extended Display Identification Data) information
  - Full JSON output support for programmatic access

## [1.0.2] - 2026-01-07

### Added
- Full support for all DDC/CI compliant monitors
- Improved error handling for unsupported monitors
- Enhanced documentation with additional examples
- Optimized performance for input switching operations
- Better clarity in CLI output messages

## [1.0.1] - 2026-01-07

### Added

- `version` command to display current version information

## [1.0.0] - 2026-01-07

### Initial Release

#### Added
- Monitor enumeration via Windows DDC/CI APIs
- Current input source detection (VCP feature 0x60)
- Input source switching for DDC/CI capable monitors
- Support for standard input types:
  - HDMI (HDMI1, HDMI2)
  - DisplayPort (DP1, DP2)
  - DVI (DVI1, DVI2)
  - VGA/Analog (VGA1, VGA2)
  - Custom hex codes (0x11, etc.)
- CLI commands:
  - `list` - Show all monitors with current inputs
  - `get <monitor>` - Get current input for specific monitor
  - `set <monitor> <input>` - Switch monitor input
  - `help` - Display usage information
- Monitor selection by index or name (partial match)
- Rich terminal output using Spectre.Console
- Comprehensive error handling and user feedback
- Single-file executable build
- Complete documentation:
  - README with installation and usage
  - EXAMPLES with integration scenarios
  - QUICK-REFERENCE for common tasks
  - IMPLEMENTATION-SUMMARY for developers

#### Technical Details
- Built with .NET 10.0
- Windows x64 only
- P/Invoke wrappers for dxva2.dll and user32.dll
- MCCS VCP code compliance
- Native AOT JSON serialization using System.Text.Json source generators
- Optimized release build (~19 MB)

#### Known Limitations
- Requires DDC/CI support on monitors
- Some monitors may need administrator privileges
- Input switching speed depends on monitor hardware
- No USB peripheral switching (KVM functionality)
- Some monitors have non-standard DDC/CI implementations and may report incorrect current input values (switching still works correctly)

### Future Considerations
- [ ] Add verbose logging mode
- [ ] Configuration file for monitor presets
- [ ] Brightness/contrast control (VCP codes 0x10/0x12)
- [ ] Power management (VCP code 0xD6)
- [ ] Profile system for quick setup switching
- [ ] GUI version (optional)
- [ ] Installer package

