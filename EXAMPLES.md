# DDCSwitch Examples

This document contains detailed examples and use cases for DDCSwitch.

## Basic Usage Examples

### Check What Monitors Support DDC/CI

```powershell
DDCSwitch list
```

This will show all your monitors and indicate which ones support DDC/CI control. Monitors with "OK" status can be controlled.

### Get Current Input of Primary Monitor

```powershell
# If primary monitor is index 0
DDCSwitch get 0
```

### Switch Between Two Inputs Quickly

```powershell
# Switch to console (HDMI)
DDCSwitch set 0 HDMI1

# Switch to PC (DisplayPort)
DDCSwitch set 0 DP1
```

## JSON Output and Automation

All DDCSwitch commands support the `--json` flag for machine-readable output. This is perfect for scripting, automation, and integration with other tools.

### PowerShell JSON Examples

#### Example 1: Conditional Input Switching

Check the current input and switch only if needed:

```powershell
# Check if monitor is on HDMI1, switch to DP1 if not
$result = DDCSwitch get 0 --json | ConvertFrom-Json

if ($result.success -and $result.currentInputCode -ne "0x11") {
    Write-Host "Monitor is on $($result.currentInput), switching to HDMI1..."
    DDCSwitch set 0 HDMI1 --json | Out-Null
} else {
    Write-Host "Monitor already on HDMI1"
}
```

#### Example 2: Switch All Monitors with Error Handling

```powershell
# Switch all available monitors to HDMI1 with error handling
$listResult = DDCSwitch list --json | ConvertFrom-Json

if ($listResult.success) {
    foreach ($monitor in $listResult.monitors) {
        if ($monitor.status -eq "ok") {
            Write-Host "Switching $($monitor.name) to HDMI1..."
            $setResult = DDCSwitch set $monitor.index HDMI1 --json | ConvertFrom-Json
            
            if ($setResult.success) {
                Write-Host "✓ Successfully switched $($monitor.name)" -ForegroundColor Green
            } else {
                Write-Host "✗ Failed: $($setResult.error)" -ForegroundColor Red
            }
        }
    }
} else {
    Write-Error "Failed to list monitors: $($listResult.error)"
}
```

#### Example 3: Monitor Status Dashboard

Create a simple dashboard showing all monitor states:

```powershell
# monitor-dashboard.ps1
$result = DDCSwitch list --json | ConvertFrom-Json

if ($result.success) {
    Write-Host "`n=== Monitor Status Dashboard ===" -ForegroundColor Cyan
    Write-Host "Total Monitors: $($result.monitors.Count)`n" -ForegroundColor Cyan
    
    foreach ($monitor in $result.monitors) {
        $isPrimary = if ($monitor.isPrimary) { " [PRIMARY]" } else { "" }
        Write-Host "Monitor $($monitor.index): $($monitor.name)$isPrimary" -ForegroundColor Yellow
        Write-Host "  Device: $($monitor.deviceName)"
        Write-Host "  Input: $($monitor.currentInput) ($($monitor.currentInputCode))"
        Write-Host "  Status: $($monitor.status.ToUpper())"
        Write-Host ""
    }
}
```

#### Example 4: Toggle Between Two Inputs

```powershell
# toggle-input.ps1
param([int]$MonitorIndex = 0)

$result = DDCSwitch get $MonitorIndex --json | ConvertFrom-Json

if ($result.success) {
    $newInput = if ($result.currentInputCode -eq "0x11") { "DP1" } else { "HDMI1" }
    $switchResult = DDCSwitch set $MonitorIndex $newInput --json | ConvertFrom-Json
    
    if ($switchResult.success) {
        Write-Host "Toggled from $($result.currentInput) to $($switchResult.newInput)" -ForegroundColor Green
    }
}
```

### Python JSON Examples

#### Example 1: Simple Monitor Switcher

```python
#!/usr/bin/env python3
import subprocess
import json
import sys

def run_ddc(args):
    """Run DDCSwitch and return JSON result"""
    result = subprocess.run(
        ['DDCSwitch'] + args + ['--json'],
        capture_output=True,
        text=True
    )
    return json.loads(result.stdout)

def list_monitors():
    """List all monitors"""
    data = run_ddc(['list'])
    if data['success']:
        for monitor in data['monitors']:
            primary = " [PRIMARY]" if monitor['isPrimary'] else ""
            print(f"{monitor['index']}: {monitor['name']}{primary}")
            print(f"   Current: {monitor['currentInput']} ({monitor['currentInputCode']})")
    else:
        print(f"Error: {data['error']}", file=sys.stderr)

def switch_input(monitor_index, input_name):
    """Switch monitor input"""
    data = run_ddc(['set', str(monitor_index), input_name])
    if data['success']:
        print(f"✓ Switched {data['monitor']['name']} to {data['newInput']}")
    else:
        print(f"✗ Error: {data['error']}", file=sys.stderr)
        sys.exit(1)

# Example usage
if __name__ == '__main__':
    list_monitors()
    # switch_input(0, 'HDMI1')
```

#### Example 2: Gaming Mode Automation

```python
#!/usr/bin/env python3
"""
Automatically switch monitors based on running applications
Usage: python gaming_mode.py
"""
import subprocess
import json
import time
import psutil

def run_ddc(args):
    result = subprocess.run(['DDCSwitch'] + args + ['--json'],
                          capture_output=True, text=True)
    return json.loads(result.stdout)

def switch_to_gaming():
    """Switch all monitors to HDMI (console inputs)"""
    print("🎮 Activating gaming mode...")
    data = run_ddc(['list'])
    
    if data['success']:
        for monitor in data['monitors']:
            if monitor['status'] == 'ok':
                result = run_ddc(['set', str(monitor['index']), 'HDMI1'])
                if result['success']:
                    print(f"  ✓ {monitor['name']} → HDMI1")

def switch_to_work():
    """Switch all monitors to DisplayPort (PC inputs)"""
    print("💼 Activating work mode...")
    data = run_ddc(['list'])
    
    if data['success']:
        for monitor in data['monitors']:
            if monitor['status'] == 'ok':
                result = run_ddc(['set', str(monitor['index']), 'DP1'])
                if result['success']:
                    print(f"  ✓ {monitor['name']} → DP1")

def is_game_running():
    """Check if specific games are running"""
    game_processes = ['steam.exe', 'EpicGamesLauncher.exe']
    for proc in psutil.process_iter(['name']):
        if proc.info['name'] in game_processes:
            return True
    return False

# Monitor and switch automatically
if __name__ == '__main__':
    previous_state = None
    
    while True:
        gaming = is_game_running()
        
        if gaming != previous_state:
            if gaming:
                switch_to_gaming()
            else:
                switch_to_work()
            previous_state = gaming
        
        time.sleep(10)  # Check every 10 seconds
```

### Node.js JSON Examples

#### Example 1: Monitor Information API

```javascript
// monitor-api.js
const { execSync } = require('child_process');

class DDCSwitch {
    static exec(args) {
        const output = execSync(`DDCSwitch ${args.join(' ')} --json`, {
            encoding: 'utf-8'
        });
        return JSON.parse(output);
    }

    static listMonitors() {
        return this.exec(['list']);
    }

    static getCurrentInput(monitorIndex) {
        return this.exec(['get', monitorIndex]);
    }

    static setInput(monitorIndex, input) {
        return this.exec(['set', monitorIndex, input]);
    }
}

// Usage example
const monitors = DDCSwitch.listMonitors();
console.log(`Found ${monitors.monitors.length} monitors`);

monitors.monitors.forEach(monitor => {
    console.log(`${monitor.index}: ${monitor.name} - ${monitor.currentInput}`);
});

// Switch first monitor to HDMI1
const result = DDCSwitch.setInput(0, 'HDMI1');
if (result.success) {
    console.log(`✓ Switched to ${result.newInput}`);
}
```

#### Example 2: Express.js REST API

```javascript
// server.js - Web API for monitor control
const express = require('express');
const { execSync } = require('child_process');

const app = express();
app.use(express.json());

function runDDC(args) {
    try {
        const output = execSync(`DDCSwitch ${args.join(' ')} --json`, {
            encoding: 'utf-8'
        });
        return JSON.parse(output);
    } catch (error) {
        return { success: false, error: error.message };
    }
}

// GET /monitors - List all monitors
app.get('/monitors', (req, res) => {
    const result = runDDC(['list']);
    res.json(result);
});

// GET /monitors/:id - Get specific monitor
app.get('/monitors/:id', (req, res) => {
    const result = runDDC(['get', req.params.id]);
    res.json(result);
});

// POST /monitors/:id/input - Set monitor input
app.post('/monitors/:id/input', (req, res) => {
    const { input } = req.body;
    const result = runDDC(['set', req.params.id, input]);
    res.json(result);
});

app.listen(3000, () => {
    console.log('DDCSwitch API running on http://localhost:3000');
});
```

### Batch Script JSON Example

```batch
@echo off
REM check-and-switch.bat - Switch only if not already on target input

for /f "delims=" %%i in ('DDCSwitch get 0 --json') do set JSON_OUTPUT=%%i

REM Simple check if contains HDMI1
echo %JSON_OUTPUT% | find "0x11" >nul
if errorlevel 1 (
    echo Switching to HDMI1...
    DDCSwitch set 0 HDMI1
) else (
    echo Already on HDMI1
)
```

### Rust JSON Example

```rust
// monitor_switcher.rs
use serde::{Deserialize, Serialize};
use std::process::Command;

#[derive(Debug, Deserialize)]
struct MonitorInfo {
    index: u32,
    name: String,
    #[serde(rename = "deviceName")]
    device_name: String,
    #[serde(rename = "isPrimary")]
    is_primary: bool,
    #[serde(rename = "currentInput")]
    current_input: Option<String>,
    #[serde(rename = "currentInputCode")]
    current_input_code: Option<String>,
    status: String,
}

#[derive(Debug, Deserialize)]
struct ListResponse {
    success: bool,
    monitors: Option<Vec<MonitorInfo>>,
    error: Option<String>,
}

fn main() {
    let output = Command::new("DDCSwitch")
        .args(&["list", "--json"])
        .output()
        .expect("Failed to execute DDCSwitch");

    let json_str = String::from_utf8_lossy(&output.stdout);
    let result: ListResponse = serde_json::from_str(&json_str)
        .expect("Failed to parse JSON");

    if result.success {
        if let Some(monitors) = result.monitors {
            for monitor in monitors {
                println!("{}: {} - {:?}", 
                    monitor.index, 
                    monitor.name, 
                    monitor.current_input);
            }
        }
    }
}
```

## Advanced Examples

### Multi-Monitor Setup Scripts

#### Scenario: Work Setup
Switch all monitors to PC inputs:

```powershell
# work-setup.ps1
Write-Host "Switching to work setup..." -ForegroundColor Cyan
DDCSwitch set 0 DP1
DDCSwitch set 1 DP2
DDCSwitch set 2 HDMI1
Write-Host "Work setup ready!" -ForegroundColor Green
```

#### Scenario: Gaming Setup
Switch monitors to console inputs:

```powershell
# gaming-setup.ps1
Write-Host "Switching to gaming setup..." -ForegroundColor Cyan
DDCSwitch set 0 HDMI1  # Main monitor to PS5
DDCSwitch set 1 HDMI2  # Secondary to Switch
Write-Host "Gaming setup ready!" -ForegroundColor Green
```

### AutoHotkey Integration

Create a comprehensive input switching system:

```autohotkey
; DDCSwitch AutoHotkey Script
; Place DDCSwitch.exe in C:\Tools\ or update path below

; Global variables
DDCSwitchPath := "C:\Tools\DDCSwitch.exe"

; Function to run DDCSwitch
RunDDCSwitch(args) {
    global DDCSwitchPath
    Run, %DDCSwitchPath% %args%, , Hide
}

; Ctrl+Alt+1: Switch monitor 0 to HDMI1
^!1::
    RunDDCSwitch("set 0 HDMI1")
    TrayTip, DDCSwitch, Switched to HDMI1, 1
    return

; Ctrl+Alt+2: Switch monitor 0 to HDMI2
^!2::
    RunDDCSwitch("set 0 HDMI2")
    TrayTip, DDCSwitch, Switched to HDMI2, 1
    return

; Ctrl+Alt+D: Switch monitor 0 to DisplayPort
^!d::
    RunDDCSwitch("set 0 DP1")
    TrayTip, DDCSwitch, Switched to DisplayPort, 1
    return

; Ctrl+Alt+W: Work setup (all monitors to PC)
^!w::
    RunDDCSwitch("set 0 DP1")
    Sleep 500
    RunDDCSwitch("set 1 DP2")
    TrayTip, DDCSwitch, Work Setup Activated, 1
    return

; Ctrl+Alt+G: Gaming setup (all monitors to console)
^!g::
    RunDDCSwitch("set 0 HDMI1")
    Sleep 500
    RunDDCSwitch("set 1 HDMI2")
    TrayTip, DDCSwitch, Gaming Setup Activated, 1
    return

; Ctrl+Alt+L: List all monitors
^!l::
    Run, cmd /k DDCSwitch.exe list
    return
```

### Stream Deck Integration

If you use Elgato Stream Deck, create a "System" action with these commands:

**Button 1: PC Input**
```
Title: PC Mode
Command: C:\Tools\DDCSwitch.exe set 0 DP1
```

**Button 2: Console Input**
```
Title: Console Mode
Command: C:\Tools\DDCSwitch.exe set 0 HDMI1
```

**Button 3: List Monitors**
```
Title: Monitor Info
Command: cmd /k C:\Tools\DDCSwitch.exe list
```

### Task Scheduler Integration

Automatically switch inputs at specific times:

#### Morning: Switch to Work Setup (8 AM)

1. Open Task Scheduler
2. Create Basic Task → "Morning Work Setup"
3. Trigger: Daily at 8:00 AM
4. Action: Start a program
   - Program: `C:\Tools\DDCSwitch.exe`
   - Arguments: `set 0 DP1`

#### Evening: Switch to Gaming Setup (6 PM)

Same steps, but with trigger at 6:00 PM and arguments: `set 0 HDMI1`

### Windows Terminal Alias

Add to your PowerShell profile (`$PROFILE`):

```powershell
# DDCSwitch aliases
function ddc-list { DDCSwitch list }
function ddc-work { 
    DDCSwitch set 0 DP1
    DDCSwitch set 1 DP2
    Write-Host "✓ Work setup activated" -ForegroundColor Green
}
function ddc-game { 
    DDCSwitch set 0 HDMI1
    DDCSwitch set 1 HDMI2
    Write-Host "✓ Gaming setup activated" -ForegroundColor Green
}
function ddc-hdmi { DDCSwitch set 0 HDMI1 }
function ddc-dp { DDCSwitch set 0 DP1 }

# Then use: ddc-work, ddc-game, ddc-hdmi, ddc-dp, ddc-list
```

### KVM Switch Replacement

Use DDCSwitch as a software KVM (without USB switching):

```powershell
# kvm-to-pc1.ps1
DDCSwitch set 0 DP1
DDCSwitch set 1 DP1
Write-Host "Switched all monitors to PC1" -ForegroundColor Green

# kvm-to-pc2.ps1
DDCSwitch set 0 DP2
DDCSwitch set 1 DP2
Write-Host "Switched all monitors to PC2" -ForegroundColor Green
```

### Testing Monitor Compatibility

If your monitor doesn't respond to standard codes, try discovering the correct codes:

```powershell
# Test different input codes
$monitor = 0
foreach ($code in 0x01, 0x03, 0x0F, 0x10, 0x11, 0x12, 0x13, 0x14, 0x15) {
    Write-Host "Testing code 0x$($code.ToString('X2'))..."
    DDCSwitch set $monitor "0x$($code.ToString('X2'))"
    Start-Sleep -Seconds 3
}
```

### Finding Non-Standard VCP Codes

Some monitors use non-standard DDC/CI codes. If DDCSwitch shows the wrong current input or switching doesn't work, you can find the correct codes:

#### Method 1: Use ControlMyMonitor by NirSoft

1. Download [ControlMyMonitor](https://www.nirsoft.net/utils/control_my_monitor.html) (free DDC/CI debugging tool)
2. Run ControlMyMonitor.exe
3. Find your monitor in the list
4. Look for VCP Code `60` (Input Source)
5. Note the **Current Value** - this is the hex code your monitor actually uses
6. Manually switch your monitor's input using its physical buttons
7. Refresh ControlMyMonitor to see the new value
8. Document which physical input corresponds to which hex code

**Example:**
```
VCP Code 60 (Input Source):
- HDMI1 physical input → Shows value 17 (0x11) ✓ Standard
- HDMI2 physical input → Shows value 18 (0x12) ✓ Standard  
- DisplayPort physical input → Shows value 15 (0x0F) ✓ Standard
- DisplayPort physical input → Shows value 27 (0x1B) ✗ Non-standard!
```

Once you know the correct codes, use them with DDCSwitch:
```powershell
DDCSwitch set 0 0x1B  # Use the actual code your monitor responds to
```

#### Method 2: Trial and Error

If ControlMyMonitor isn't available, systematically test codes:

```powershell
# test-all-codes.ps1
Write-Host "Testing input codes for Monitor 0" -ForegroundColor Cyan
Write-Host "Watch your monitor and note which codes cause it to switch inputs" -ForegroundColor Yellow
Write-Host ""

$codes = @(0x01, 0x02, 0x03, 0x04, 0x0F, 0x10, 0x11, 0x12, 0x13, 0x14, 0x15, 0x16, 0x17, 0x18, 0x19, 0x1A, 0x1B)

foreach ($code in $codes) {
    $hexCode = "0x{0:X2}" -f $code
    Write-Host "Testing $hexCode..." -ForegroundColor Green
    DDCSwitch set 0 $hexCode
    Start-Sleep -Seconds 3
}

Write-Host ""
Write-Host "Testing complete! Document which codes worked for your monitor." -ForegroundColor Cyan
```

## Troubleshooting Examples

### Check if Monitor Responds to DDC/CI

```powershell
# Get current input (if this works, DDC/CI is functional)
DDCSwitch get 0

# Try setting to current input (should succeed instantly)
DDCSwitch list  # Note the current input
DDCSwitch set 0 <current-input>
```

### Dealing with Slow Monitors

Some monitors are slow to respond to DDC/CI commands. Add delays:

```powershell
# slow-switch.ps1
DDCSwitch set 0 HDMI1
Start-Sleep -Seconds 2  # Wait for monitor to switch
DDCSwitch set 1 HDMI2
Start-Sleep -Seconds 2
Write-Host "Done" -ForegroundColor Green
```

### Monitor by Name Instead of Index

Useful if monitor order changes:

```powershell
# Find monitor with "LG" in the name and switch it
DDCSwitch list  # Note the exact name
DDCSwitch set "LG ULTRAGEAR" HDMI1

# Partial name matching works
DDCSwitch set "ULTRAGEAR" HDMI1
```

## Integration with Other Tools

### PowerToys Run

Add DDCSwitch to your PATH, then use PowerToys Run (Alt+Space):

```
> DDCSwitch set 0 HDMI1
```

### Windows Run Dialog

Press Win+R and type:

```
DDCSwitch set 0 DP1
```

### Batch File Shortcuts

Create `.bat` files on your desktop:

**switch-to-hdmi.bat:**
```batch
@echo off
"C:\Tools\DDCSwitch.exe" set 0 HDMI1
```

**switch-to-dp.bat:**
```batch
@echo off
"C:\Tools\DDCSwitch.exe" set 0 DP1
```

Make them double-clickable for quick access!

## Tips and Tricks

1. **Add to PATH**: Add DDCSwitch.exe location to your Windows PATH for easier access
2. **Create shortcuts**: Right-click DDCSwitch.exe → Send to → Desktop (create shortcut), then edit properties to add arguments
3. **Use monitoring**: Combine with other tools to detect when certain apps launch and switch inputs automatically
4. **Test first**: Always test with `list` and `get` before creating automation scripts
5. **Admin rights**: Some monitors require running as Administrator - right-click and "Run as administrator"

## Common Patterns

### Pattern 1: Toggle Between Two Inputs

```powershell
# toggle-input.ps1
$current = (DDCSwitch get 0 | Select-String -Pattern "0x([0-9A-F]{2})").Matches[0].Groups[1].Value
$currentDecimal = [Convert]::ToInt32($current, 16)

if ($currentDecimal -eq 0x11) {  # Currently HDMI1
    DDCSwitch set 0 DP1
    Write-Host "Switched to DisplayPort"
} else {
    DDCSwitch set 0 HDMI1
    Write-Host "Switched to HDMI1"
}
```

### Pattern 2: Check Before Switch

```powershell
# smart-switch.ps1
param([string]$Input = "HDMI1")

$monitors = DDCSwitch list
if ($monitors -match "No DDC/CI") {
    Write-Error "No compatible monitors found"
    exit 1
}

DDCSwitch set 0 $Input
Write-Host "Successfully switched to $Input" -ForegroundColor Green
```

### Pattern 3: Switch All Monitors to Same Input

```powershell
# sync-all.ps1 - Using JSON for reliable monitor enumeration
param([string]$Input = "HDMI1")

$result = DDCSwitch list --json | ConvertFrom-Json

if (-not $result.success) {
    Write-Error $result.error
    exit 1
}

$okMonitors = $result.monitors | Where-Object { $_.status -eq "ok" }

foreach ($monitor in $okMonitors) {
    Write-Host "Switching monitor $($monitor.index) ($($monitor.name)) to $Input..."
    DDCSwitch set $monitor.index $Input
    Start-Sleep -Milliseconds 500
}

Write-Host "Switched $($okMonitors.Count) monitors to $Input" -ForegroundColor Green
```

**Alternative (without JSON):**
```powershell
# sync-all-legacy.ps1
param([string]$Input = "HDMI1")

$output = DDCSwitch list
$monitorCount = ($output | Select-String -Pattern "^\│ \d+" -AllMatches).Matches.Count

for ($i = 0; $i -lt $monitorCount; $i++) {
    Write-Host "Switching monitor $i to $Input..."
    DDCSwitch set $i $Input
    Start-Sleep -Milliseconds 500
}

Write-Host "All monitors switched to $Input" -ForegroundColor Green
```

Happy switching!

