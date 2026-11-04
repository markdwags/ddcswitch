# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

ddcswitch is a Windows-only CLI (C#, .NET 10, NativeAOT, Spectre.Console) that controls monitors over DDC/CI via the Win32 dxva2 API: input switching, brightness/contrast, and raw VCP feature read/write.

## Commands

```powershell
.\build.ps1                                   # clean + NativeAOT publish -> dist\ddcswitch.exe
dotnet build                                  # quick JIT build (no native link)
dotnet run --project DDCSwitch -- list --trace   # run from source
dotnet test --project DDCSwitch.Tests         # all unit tests (no monitor needed)

# Single class / single test (xUnit v3 on Microsoft.Testing.Platform; wildcards only at start/end)
dotnet test --project DDCSwitch.Tests --filter-class "DDCSwitch.Tests.DdcRetryPolicyTests"
dotnet test --project DDCSwitch.Tests --filter-method "*SucceedsOnFirstAttempt*"

# After editing DDCSwitch/VcpFeatureData.json (requires python)
.\tools\Regenerate.ps1
```

- SDK is pinned in `global.json`; it also opts `dotnet test` into Microsoft.Testing.Platform, so use `--project`, `--filter-class`/`--filter-method` (not VSTest `--filter`).
- NativeAOT publish needs the VS "Desktop development with C++" workload. If linking fails with `'vswhere.exe' is not recognized`, add `%ProgramFiles(x86)%\Microsoft Visual Studio\Installer` to `PATH`.
- CI skips the build (and release) for `chore/*` branch PRs and their merges into `main`, and for `chore:` commits on `main`.
- CI (`.github/workflows/ci-cd.yml`) fails restore on high/critical NuGet advisories (NU1903/NU1904) and smoke-runs `ddcswitch --json version` on the published exe.

## Versioning and releases

The version lives only in `CHANGELOG.md`: the first `## [X.Y.Z] - YYYY-MM-DD` heading. The csproj's `SetVersionFromChangelog` target reads it for the assembly version, and CI creates tag `v<version>` + GitHub Release + Chocolatey package when a push to `main` has a CHANGELOG version with no existing tag. Merges that don't bump the version don't release. See `.github/workflows/README.md`.

## Architecture

Flow: `Program.cs` → `Commands/CommandRouter.Route` strips global flags (`--json`, `--verbose`, `--trace`) from `args`, dispatches to a static `XxxCommand.Execute`, and converts uncaught exceptions to an error (JSON `ErrorResponse` when `--json`). `get <monitor>` with no feature and `get all` run `VcpScanCommand`.

- **Core/** – `MonitorController.EnumerateMonitors()` walks `EnumDisplayMonitors` → physical monitors and returns a disposable `MonitorCollection`; always dispose it (it owns the dxva2 handles). `FindMonitor` matches an index or a case-insensitive substring of name/model/hardware ID/`DISPLAYn`. `Monitor` does EDID loading at construction but **no DDC/CI I/O** until a `TryGet/TrySetVcpFeature` call, so only the targeted monitor is ever talked to.
- **DDC/CI reliability** – every read/write goes through `DdcRetryPolicy` (default 3 attempts, 0/50/100 ms backoff; `Once` for bulk scans after one successful read). "VCP not supported" and invalid-handle errors are never retried. `DdcErrors` maps Win32 codes to text; `DdcTrace` logs every call to **stderr** (so `--json` stdout stays clean).
- **PhysicalMonitorHandle** – SafeHandle wrapper. dxva2 handles are small integers from a pool shared across processes, so **handle value 0 is valid**; don't treat zero as invalid (this was a real bug).
- **Exceptions inside native callbacks** – the `EnumDisplayMonitors` callback must not throw (NativeAOT fail-fasts); exceptions are captured with `ExceptionDispatchInfo` and rethrown after enumeration.
- **Hardware/** – `DisplayDeviceResolver` maps a display output to its exact monitor device interface path (`EnumDisplayDevices` with `EDD_GET_DEVICE_INTERFACE_NAME`) and reads EDID from that instance's registry key; no registry heuristics. `EdidParser` decodes EDID 1.3/1.4 (chromaticity, color encodings, descriptors).
- **Identification/** – `MonitorNameResolver`: EDID manufacturer + model → Windows description → `Monitor N`.
- **VcpFeatures/** – `VcpFeature.Generated.cs` is generated from `VcpFeatureData.json` by `tools/GenerateVcpFeatures.py`; edit the JSON and regenerate, never hand-edit the generated file. `FeatureResolver` maps names/aliases/hex codes to features and handles percentage ↔ raw conversion. `InputSource` holds input names (HDMI1, DP1, …). `VcpErrorHandler` builds user-facing error messages.
- **JsonContext.cs** – source-generated `JsonSerializerContext` plus all response records. New JSON output types must be added here with `[JsonSerializable]`.

## Conventions

- Everything must be NativeAOT-compatible: no reflection-based serialization or dynamic code. All JSON goes through `JsonContext.Default.*`.
- Every command supports `--json`, including on errors (an `ErrorResponse` or a response record with `Success = false`). Exit codes: 0 success, 1 any failure.
- All Win32 P/Invoke declarations live in `Core/NativeMethods.cs`.
- Use the `Try*` pattern for operations that may fail on real hardware; terminal output goes through Spectre.Console / `ConsoleOutputFormatter` (Spectre itself honors `NO_COLOR`).
- Validate that the monitor exists and answers DDC/CI before operating on it; error messages name the monitor and suggest a fix (see `VcpErrorHandler`).

## Testing

Tests must not require a monitor. `DDCSwitch.Tests/TestHelpers.cs` provides `TestMonitor.Create(...)` (fake hardware ID and a default invalid handle, so no dxva2 calls) and `EdidBuilder` for synthetic 128-byte EDID blocks. The main project exposes internals via `InternalsVisibleTo`. `DdcRetryPolicy` takes an injectable sleep for deterministic retry tests.
