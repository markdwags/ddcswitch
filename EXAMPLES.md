# ddcswitch Examples

Practical recipes for ddcswitch: switching inputs, adjusting brightness and other VCP features, scripting with `--json`, and wiring commands to hotkeys and schedules. See the [README](README.md) for installation and the full command list.

Every example assumes `ddcswitch.exe` is on your `PATH`. DDC/CI must be enabled in each monitor's on-screen menu.

## Selecting a monitor

Every command that takes a `<monitor>` accepts any of these forms:

```powershell
ddcswitch list                     # shows each monitor's index, name, device and current input
ddcswitch get 0 brightness         # index from `list`
ddcswitch get VG270U brightness    # part of the name or model (case-insensitive)
ddcswitch get "LG ULTRA" input     # quote names that contain spaces
ddcswitch get ACR06CF input        # PnP hardware ID (shown by --trace)
ddcswitch get DISPLAY2 input       # Windows display device
```

If a name matches more than one monitor, the first match wins. Use the index or the hardware ID to tell identical models apart.

## Switching inputs

```powershell
ddcswitch set 0 input HDMI1
ddcswitch set 0 input DP1
ddcswitch set VG270U input HDMI2
ddcswitch set 0 input 0x11         # raw MCCS input code (0x11 = HDMI1)
```

`set` always takes three arguments: monitor, feature, value. `ddcswitch set 0 HDMI1` without `input` is an error.

Accepted input names, case-insensitive:

| Name | Code | Also accepted |
|------|------|---------------|
| `VGA1`, `VGA2` | `0x01`, `0x02` | `vga`, `analog1`, `analog2` |
| `DVI1`, `DVI2` | `0x03`, `0x04` | `dvi` |
| `DP1`, `DP2` | `0x0F`, `0x10` | `dp`, `displayport1`, `displayport2` |
| `HDMI1`, `HDMI2` | `0x11`, `0x12` | `hdmi` |

Composite, S-Video, component and tuner inputs are also recognised. For a monitor that uses non-standard codes (common for USB-C or a third HDMI port), use the hex code. See [Finding a monitor's input codes](#finding-a-monitors-input-codes).

### Toggling between two inputs

```powershell
ddcswitch toggle 0 HDMI1 DP1
ddcswitch toggle "LG ULTRAGEAR" HDMI1 HDMI2
```

`toggle` reads the current input and switches to the other one of the pair:

- On `HDMI1`: switches to `DP1`.
- On `DP1`: switches to `HDMI1`.
- On neither: switches to the first input (`HDMI1`) and prints a warning.
- If the current input can't be read, `toggle` fails instead of guessing. Use `set … input` in that case.

This makes `toggle` the natural command to put behind a single hotkey. See [Integrations](#integrations).

## Brightness, contrast and other features

```powershell
ddcswitch set 0 brightness 75%     # percentage of the monitor's maximum
ddcswitch set 0 contrast 80%
ddcswitch set 0 brightness 40      # raw value
ddcswitch get 0 brightness         # shows percentage and raw value/max
```

A value with `%` is scaled to the monitor's maximum. A bare number is sent as-is. Most monitors use a 0–100 range, but not all, so `get` shows the real maximum (`Raw value: 60/100`). Raw values above that maximum are rejected.

Common features (the full list of names and aliases is in [`VcpFeatureData.json`](DDCSwitch/VcpFeatureData.json); any VCP code `0x00`–`0xFF` also works):

| Feature | Code | Aliases | `%` |
|---------|------|---------|-----|
| `brightness` | `0x10` | `bright` | yes |
| `contrast` | `0x12` | | yes |
| `backlight` | `0x13` | | yes |
| `red-gain`, `green-gain`, `blue-gain` | `0x16`, `0x18`, `0x1A` | `red`, `green`, `blue` | yes |
| `select-color-preset` | `0x14` | `color-preset` | no (preset number) |
| `color-temp-request` | `0x0C` | `color-temp` | no |
| `sharpness` | `0x87` | `sharp` | yes |
| `color-saturation` | `0x8A` | `saturation`, `sat` | yes |
| `input` | `0x60` | `source` | no |
| `volume` | `0x62` | `vol` | yes |
| `mute` | `0x8D` | | no (1 = mute, 2 = unmute) |
| `image-mode` | `0xDB` | `mode` | no |
| `power-mode` | `0xD6` | `power` | no |

```powershell
ddcswitch set 0 red-gain 90%
ddcswitch set 0 volume 30%
ddcswitch set 0 mute 1
ddcswitch get 0 color-preset       # e.g. 5; what each preset number means is monitor-specific
```

Monitors implement only some of these. Unsupported features fail with a "not supported" error and change nothing.

> **Careful with the reset codes.** `restore-defaults` (`0x04`, alias `factory-reset`), `restore-brightness-contrast` (`0x05`) and `restore-color` (`0x08`) reset the monitor's settings immediately, with no confirmation:
>
> ```powershell
> ddcswitch set 0 restore-color 1
> ```

## Discovering what a monitor supports

```powershell
ddcswitch get 0                    # scan all 256 VCP codes on monitor 0 (takes several seconds)
ddcswitch get VG270U               # same, by name
ddcswitch get all                  # scan every monitor
ddcswitch get 0 0xDC               # read one raw code
ddcswitch info 0                   # EDID: manufacturer, model, serial, year, color gamut, color encodings
ddcswitch list --verbose           # adds brightness, contrast and EDID columns to the list
```

A scan lists the codes the monitor answered, with their names. Some firmware answers codes it doesn't actually implement, so check that a write does what you expect before relying on it.

## Scripting with JSON

Add `--json` to any command for machine-readable output on stdout. Diagnostic `--trace` output goes to stderr, so it never corrupts the JSON.

**Exit codes:** `0` on success and `1` on any failure, so `$LASTEXITCODE` (PowerShell) or `%ERRORLEVEL%` (cmd) works without parsing.

### Response shapes

`ddcswitch list --verbose --json` (without `--verbose`, `brightness` and `contrast` are left out):

```json
{
  "success": true,
  "monitors": [
    {
      "index": 0,
      "name": "Acer Technologies VG270U P",
      "deviceName": "\\\\.\\DISPLAY1",
      "isPrimary": true,
      "currentInput": "DisplayPort1 (DP1)",
      "currentInputCode": "0x0F",
      "status": "ok",
      "brightness": "60%",
      "contrast": "50%",
      "manufacturerId": "ACR",
      "manufacturerName": "Acer Technologies",
      "modelName": "VG270U P",
      "serialNumber": "123456789",
      "productCode": 1743,
      "manufactureYear": 2021,
      "manufactureWeek": 2
    }
  ]
}
```

- `status` is `"ok"` or `"no_ddc_ci"`.
- `brightness`/`contrast` are strings, and `"N/A"` when they can't be read.
- `currentInput` is a display name (`"HDMI1"`, `"DisplayPort1 (DP1)"`, `"Analog1 (VGA1)"`). **Compare and store `currentInputCode` instead**: it's stable, and you can pass it straight back to `set … input`.

`ddcswitch get 0 brightness --json`:

```json
{
  "success": true,
  "monitor": { "index": 0, "name": "Acer Technologies VG270U P", "deviceName": "\\\\.\\DISPLAY1", "isPrimary": true },
  "featureName": "brightness",
  "rawValue": 60,
  "maxValue": 100,
  "percentageValue": 60
}
```

`percentageValue` only appears for features that support percentages. For `get 0 input --json`, `rawValue` is the input code (`15` = `0x0F` = DP1) and `maxValue` has no meaning.

`ddcswitch set 0 brightness 75% --json`:

```json
{
  "success": true,
  "monitor": { "index": 0, "name": "Acer Technologies VG270U P", "deviceName": "\\\\.\\DISPLAY1", "isPrimary": true },
  "featureName": "brightness",
  "setValue": 75,
  "percentageValue": 75
}
```

`ddcswitch toggle 0 HDMI1 DP1 --json`:

```json
{
  "success": true,
  "monitor": { "index": 0, "name": "Acer Technologies VG270U P", "deviceName": "\\\\.\\DISPLAY1", "isPrimary": true },
  "fromInput": "DisplayPort1 (DP1)",
  "toInput": "HDMI1",
  "fromInputCode": 15,
  "toInputCode": 17
}
```

A `"warning"` field is added when the monitor was on neither input.

`ddcswitch info 0 --json` returns `monitor`, `status`, `currentInput`, `currentInputCode` and an `edid` object (`manufacturerName`, `modelName`, `serialNumber`, `productCode`, `manufactureYear`, `features.displayType`, `chromaticity.red|green|blue|white.x|y`, and more).

Every failure has this shape. Some errors also include the `monitor` object:

```json
{
  "success": false,
  "error": "Monitor 'nosuch' not found"
}
```

### PowerShell

#### Switch only if the monitor isn't already on the target input

```powershell
# switch-if-needed.ps1  —  .\switch-if-needed.ps1 -Monitor 0 -TargetCode 0x11
param(
    [string]$Monitor = "0",
    [int]$TargetCode = 0x11   # 0x11 = HDMI1, 0x0F = DP1
)

$current = ddcswitch get $Monitor input --json | ConvertFrom-Json
if (-not $current.success) { Write-Error $current.error; exit 1 }

if ($current.rawValue -eq $TargetCode) {
    Write-Host ("Monitor {0} is already on 0x{1:X2}" -f $Monitor, $TargetCode)
    exit 0
}

ddcswitch set $Monitor input ("0x{0:X2}" -f $TargetCode)
exit $LASTEXITCODE
```

#### Brightness by time of day

```powershell
# auto-brightness.ps1  —  run from Task Scheduler every hour
$hour = (Get-Date).Hour
$level = switch ($hour) {
    { $_ -lt 7 }  { 20; break }
    { $_ -lt 12 } { 70; break }
    { $_ -lt 17 } { 85; break }
    { $_ -lt 21 } { 50; break }
    default       { 25 }
}

$list = ddcswitch list --json | ConvertFrom-Json
foreach ($m in $list.monitors | Where-Object status -eq 'ok') {
    ddcswitch set $m.index brightness "$level%" | Out-Null
    if ($LASTEXITCODE -eq 0) { Write-Host "$($m.name): $level%" }
    else { Write-Warning "$($m.name): brightness not set" }
}
```

#### Apply a named profile

```powershell
# profile.ps1  —  .\profile.ps1 gaming
param([ValidateSet('work', 'gaming', 'movie')][string]$Name = 'work')

$profiles = @{
    work   = @{ Monitor = '0'; Input = 'DP1';   Brightness = '60%'; Contrast = '70%' }
    gaming = @{ Monitor = '0'; Input = 'HDMI1'; Brightness = '90%'; Contrast = '80%' }
    movie  = @{ Monitor = '0'; Input = 'HDMI1'; Brightness = '35%'; Contrast = '75%' }
}
$p = $profiles[$Name]

ddcswitch set $p.Monitor brightness $p.Brightness
ddcswitch set $p.Monitor contrast $p.Contrast
ddcswitch set $p.Monitor input $p.Input
```

The input is switched last on purpose. Some monitors stop answering DDC/CI on a cable whose input is no longer shown, so settings sent after a switch away from this PC can be lost.

#### Save and restore every monitor's settings

```powershell
$profilePath = "$HOME\monitor-profile.json"

function Save-MonitorProfile {
    $list = ddcswitch list --verbose --json | ConvertFrom-Json
    if (-not $list.success) { throw $list.error }
    $list.monitors | Where-Object status -eq 'ok' |
        Select-Object index, name, currentInputCode, brightness, contrast |
        ConvertTo-Json | Set-Content $profilePath
    Write-Host "Saved $profilePath"
}

function Restore-MonitorProfile {
    foreach ($m in (Get-Content $profilePath -Raw | ConvertFrom-Json)) {
        foreach ($feature in 'brightness', 'contrast') {
            if ($m.$feature -and $m.$feature -ne 'N/A') { ddcswitch set $m.index $feature $m.$feature | Out-Null }
        }
        if ($m.currentInputCode) { ddcswitch set $m.index input $m.currentInputCode | Out-Null }
        Write-Host "Restored $($m.name)"
    }
}
```

Monitors are restored by index. Indexes can change when you connect monitors differently, so save again after rearranging.

#### Only configure a specific model

```powershell
$info = ddcswitch info 0 --json | ConvertFrom-Json
if ($info.success -and $info.edid.modelName -like '*VG270U*') {
    ddcswitch set 0 brightness 80%
    ddcswitch set 0 contrast 75%
}
```

### Python

```python
import json
import subprocess


def ddc(*args):
    """Run ddcswitch with --json and return the parsed response (raises on failure)."""
    result = subprocess.run(["ddcswitch", *map(str, args), "--json"],
                            capture_output=True, text=True)
    data = json.loads(result.stdout)
    if not data.get("success"):
        raise RuntimeError(data.get("error", "ddcswitch failed"))
    return data


for m in ddc("list", "--verbose")["monitors"]:
    print(f"[{m['index']}] {m['name']}: {m.get('currentInput')} "
          f"({m.get('currentInputCode')}), brightness {m.get('brightness', 'n/a')}")

current = ddc("get", 0, "brightness")
print(f"Brightness {current['percentageValue']}% ({current['rawValue']}/{current['maxValue']})")

ddc("set", 0, "brightness", f"{min(100, current['percentageValue'] + 10)}%")

if ddc("get", 0, "input")["rawValue"] != 0x11:
    ddc("set", 0, "input", "HDMI1")
```

## Integrations

### AutoHotkey v2

```autohotkey
#Requires AutoHotkey v2.0

; Ctrl+Alt+I: toggle monitor 0 between HDMI1 and DP1
^!i:: Run("ddcswitch.exe toggle 0 HDMI1 DP1", , "Hide")

; Ctrl+Alt+1 / Ctrl+Alt+2: switch monitor 0 to a specific input
^!1:: Run("ddcswitch.exe set 0 input HDMI1", , "Hide")
^!2:: Run("ddcswitch.exe set 0 input DP1", , "Hide")

; Ctrl+Alt+Up / Down: brightness presets on every monitor
^!Up:: {
    Loop 3
        Run("ddcswitch.exe set " (A_Index - 1) " brightness 90%", , "Hide")
}
^!Down:: {
    Loop 3
        Run("ddcswitch.exe set " (A_Index - 1) " brightness 30%", , "Hide")
}
```

Change `Loop 3` to your number of monitors.

### Stream Deck

Add a **System → Open** action for each button. Enter the full path and the arguments:

```text
C:\Tools\ddcswitch.exe toggle 0 HDMI1 DP1
C:\Tools\ddcswitch.exe set 0 brightness 80%
```

A console window flashes briefly. To avoid it, point the button at an AutoHotkey script that runs the command hidden.

### Task Scheduler

```powershell
# Switch to DP1 at 8:00 on weekdays
schtasks /Create /TN "ddcswitch work input" /SC WEEKLY /D MON,TUE,WED,THU,FRI /ST 08:00 `
    /TR "C:\Tools\ddcswitch.exe set 0 input DP1"

# Run the auto-brightness script every hour
schtasks /Create /TN "ddcswitch auto brightness" /SC HOURLY `
    /TR "powershell -NoProfile -WindowStyle Hidden -File C:\Scripts\auto-brightness.ps1"
```

Tasks run as the signed-in user. ddcswitch doesn't need administrator rights.

### Desktop shortcut or batch file

Create a shortcut with this target, then give it a hotkey under **Properties → Shortcut key**:

```text
C:\Tools\ddcswitch.exe toggle 0 HDMI1 DP1
```

Or a batch file that sets up several monitors:

```bat
@echo off
ddcswitch set 0 brightness 60%%
ddcswitch set 0 input DP1
ddcswitch set 1 input HDMI1
if errorlevel 1 echo Monitor 1 did not respond & pause
```

In a `.bat` file, write `%` as `%%`.

### Run dialog and PowerToys Run

With ddcswitch on your `PATH`, type a command straight into **Win+R**, or into PowerToys Run with the `>` shell prefix:

```text
ddcswitch toggle 0 HDMI1 DP1
```

### PowerShell profile shortcuts

Add these to `$PROFILE`:

```powershell
function ddc-list  { ddcswitch list --verbose }
function ddc-hdmi  { ddcswitch set 0 input HDMI1 }
function ddc-dp    { ddcswitch set 0 input DP1 }
function ddc-swap  { ddcswitch toggle 0 HDMI1 DP1 }
function ddc-bright([ValidateRange(0, 100)][int]$Level) { ddcswitch set 0 brightness "$Level%" }
```

Then run `ddc-swap` or `ddc-bright 40`.

### Windows Terminal

Add a profile under `profiles.list` in `settings.json` to open a tab showing your monitors:

```json
{
  "name": "Monitors",
  "commandline": "powershell -NoExit -Command ddcswitch list --verbose"
}
```

### WSL

Windows executables run from WSL, so you can use `jq` on the output:

```bash
alias ddc='/mnt/c/Tools/ddcswitch.exe'

ddc list --verbose --json | jq -r '.monitors[] | "\(.index)  \(.name)  \(.currentInputCode)  \(.brightness)"'
ddc set 0 brightness 50%
```

## Troubleshooting

### Is DDC/CI working?

```powershell
ddcswitch list               # Status column: OK or "No DDC/CI"
ddcswitch get 0 input        # a single, fast read
```

If a monitor shows "No DDC/CI", turn on DDC/CI in its on-screen menu. Some monitors only accept DDC/CI on certain inputs or cables, and docks and KVM switches often block it. Power-cycling the monitor can also help.

### See every DDC/CI call

```powershell
ddcswitch get 0 brightness --trace
$env:DDCSWITCH_TRACE = 1; ddcswitch toggle 0 HDMI1 DP1      # same, via environment variable
```

```text
[ddc 12:23:50.167] monitor 0: Acer Technologies VG270U P on \\.\DISPLAY1 handle=0 (\\?\DISPLAY#ACR06CF#...)
[ddc 12:23:50.236] monitor 0 (Acer Technologies VG270U P) get 0x10 attempt 1: ok in 59ms value=0x3C max=0x64
```

Each line shows the attempt number, the Win32 error code on failure, and the timing. ddcswitch retries a call up to 3 times when the monitor doesn't answer, so occasional `attempt 2` lines are normal. The trace also shows each monitor's hardware ID (`ACR06CF`), which you can use to select it.

### Finding a monitor's input codes

Some monitors use non-standard codes, especially for USB-C or extra HDMI ports:

1. Switch to an input using the monitor's own buttons. If the monitor stops answering DDC/CI while showing another input, connect this PC to that port instead.
2. Run `ddcswitch get 0 input`. It prints the input's hex code, e.g. `(0x1B)`. Repeat for each input.
3. Use that code from then on: `ddcswitch set 0 input 0x1B`.

[ControlMyMonitor](https://www.nirsoft.net/utils/control_my_monitor.html) by NirSoft can also show every VCP value a monitor reports.

### Plain output

Set `NO_COLOR=1` to turn off colors and icons, for example when logging to a file. For scripts, use `--json`.
