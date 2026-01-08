#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Builds the Chocolatey package for ddcswitch with proper checksum embedding.

.DESCRIPTION
    This script:
    1. Extracts the version from CHANGELOG.md
    2. Downloads the release ZIP from GitHub (or uses a local file)
    3. Calculates the SHA256 checksum
    4. Replaces placeholders in nuspec and install script
    5. Builds the .nupkg package

.PARAMETER Version
    The version to build. If not specified, extracts from CHANGELOG.md

.PARAMETER LocalZip
    Path to a local ZIP file to use instead of downloading from GitHub

.EXAMPLE
    .\build-chocolatey.ps1
    
.EXAMPLE
    .\build-chocolatey.ps1 -Version 1.0.2
    
.EXAMPLE
    .\build-chocolatey.ps1 -LocalZip "D:\Programming\DDCSwitch\dist\ddcswitch-1.0.2-win-x64.zip"
#>

[CmdletBinding()]
param(
    [string]$Version,
    [string]$LocalZip
)

$ErrorActionPreference = 'Stop'

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Building Chocolatey Package for ddcswitch" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host

# Get version from CHANGELOG.md if not specified
if (-not $Version) {
    Write-Host "Extracting version from CHANGELOG.md..." -ForegroundColor Yellow
    $changelogPath = Join-Path $PSScriptRoot "CHANGELOG.md"
    
    if (-not (Test-Path $changelogPath)) {
        Write-Error "CHANGELOG.md not found at: $changelogPath"
        exit 1
    }
    
    $changelogContent = Get-Content $changelogPath -Raw
    if ($changelogContent -match '##\s+\[(\d+\.\d+\.\d+)\]') {
        $Version = $matches[1]
        Write-Host "Found version: $Version" -ForegroundColor Green
    } else {
        Write-Error "Could not extract version from CHANGELOG.md. Expected format: ## [X.Y.Z]"
        exit 1
    }
}

Write-Host "Building package for version: $Version" -ForegroundColor Green
Write-Host

# Get or download the ZIP file and calculate checksum
if ($LocalZip) {
    Write-Host "Using local ZIP file: $LocalZip" -ForegroundColor Yellow
    
    if (-not (Test-Path $LocalZip)) {
        Write-Error "Local ZIP file not found: $LocalZip"
        exit 1
    }
    
    $zipPath = $LocalZip
} else {
    Write-Host "Downloading release ZIP from GitHub..." -ForegroundColor Yellow
    $url = "https://github.com/markdwags/ddcswitch/releases/download/v$Version/ddcswitch-$Version-win-x64.zip"
    $zipPath = Join-Path $env:TEMP "ddcswitch-$Version.zip"
    
    try {
        Invoke-WebRequest -Uri $url -OutFile $zipPath -UseBasicParsing
        Write-Host "Downloaded: $url" -ForegroundColor Green
    } catch {
        Write-Error "Failed to download release ZIP. Make sure the release exists: $url"
        exit 1
    }
}

# Calculate checksum
Write-Host "Calculating SHA256 checksum..." -ForegroundColor Yellow
$checksumObj = Get-FileHash -Path $zipPath -Algorithm SHA256
$checksum = $checksumObj.Hash
Write-Host "Checksum: $checksum" -ForegroundColor Green
Write-Host

# Prepare chocolatey directory
$chocoDir = Join-Path $PSScriptRoot "chocolatey"
$workDir = Join-Path $PSScriptRoot "chocolatey-build"

if (Test-Path $workDir) {
    Remove-Item $workDir -Recurse -Force
}

Write-Host "Copying chocolatey files to working directory..." -ForegroundColor Yellow
Copy-Item $chocoDir -Destination $workDir -Recurse

# Replace placeholders in nuspec
Write-Host "Updating nuspec with version $Version..." -ForegroundColor Yellow
$nuspecPath = Join-Path $workDir "ddcswitch.nuspec"
$nuspecContent = Get-Content $nuspecPath -Raw
$nuspecContent = $nuspecContent -replace '__VERSION__', $Version
Set-Content $nuspecPath -Value $nuspecContent -NoNewline

# Replace placeholders in install script
Write-Host "Updating install script with checksum..." -ForegroundColor Yellow
$installScriptPath = Join-Path $workDir "tools\chocolateyinstall.ps1"
$installContent = Get-Content $installScriptPath -Raw
$installContent = $installContent -replace '__CHECKSUM__', $checksum
Set-Content $installScriptPath -Value $installContent -NoNewline

# Update VERIFICATION.txt
Write-Host "Updating VERIFICATION.txt..." -ForegroundColor Yellow
$verificationPath = Join-Path $workDir "tools\VERIFICATION.txt"
$verificationContent = @"
VERIFICATION
============

Verification is intended to assist moderators and community in verifying that the
package is published by the software author.

## Source Code

The source code for ddcswitch is available on GitHub:
https://github.com/markdwags/ddcswitch

## Binary Release

The binary included in this package was downloaded from the official GitHub releases:
https://github.com/markdwags/ddcswitch/releases/tag/v$Version

Direct download link:
https://github.com/markdwags/ddcswitch/releases/download/v$Version/ddcswitch-$Version-win-x64.zip

## Checksum Verification

You can verify the integrity of the downloaded file using PowerShell:

```powershell
`$url = "https://github.com/markdwags/ddcswitch/releases/download/v$Version/ddcswitch-$Version-win-x64.zip"
`$file = "`$env:TEMP\ddcswitch-verify.zip"
Invoke-WebRequest -Uri `$url -OutFile `$file
Get-FileHash `$file -Algorithm SHA256
Remove-Item `$file
```

Expected checksum (SHA256):
$checksum

This checksum is embedded in the chocolateyinstall.ps1 script and verified during installation.

## License

ddcswitch is licensed under the MIT License.
See: https://github.com/markdwags/ddcswitch/blob/main/LICENSE
"@

Set-Content $verificationPath -Value $verificationContent

# Remove CHECKSUM file as it's no longer needed (checksum is now embedded in script)
$checksumFilePath = Join-Path $workDir "tools\CHECKSUM"
if (Test-Path $checksumFilePath) {
    Remove-Item $checksumFilePath
    Write-Host "Removed obsolete CHECKSUM file (checksum is now embedded in install script)" -ForegroundColor Yellow
}

# Build the package
Write-Host "Building Chocolatey package..." -ForegroundColor Yellow
Push-Location $workDir
try {
    choco pack
    
    if ($LASTEXITCODE -ne 0) {
        Write-Error "choco pack failed with exit code $LASTEXITCODE"
        exit 1
    }
    
    # Move the package to the root directory
    $nupkg = Get-ChildItem -Filter "ddcswitch.$Version.nupkg" | Select-Object -First 1
    if ($nupkg) {
        $destPath = Join-Path $PSScriptRoot $nupkg.Name
        Move-Item $nupkg.FullName $destPath -Force
        Write-Host
        Write-Host "========================================" -ForegroundColor Green
        Write-Host "Package built successfully!" -ForegroundColor Green
        Write-Host "========================================" -ForegroundColor Green
        Write-Host "Output: $destPath" -ForegroundColor Green
        Write-Host
        Write-Host "To test locally:" -ForegroundColor Cyan
        Write-Host "  choco install ddcswitch -s . --force" -ForegroundColor White
        Write-Host
        Write-Host "To submit to Chocolatey:" -ForegroundColor Cyan
        Write-Host "  https://community.chocolatey.org/packages/submit" -ForegroundColor White
    } else {
        Write-Error "Package file not found after build"
        exit 1
    }
} finally {
    Pop-Location
}

# Cleanup temp file if we downloaded it
if (-not $LocalZip -and (Test-Path $zipPath)) {
    Remove-Item $zipPath
}

Write-Host
Write-Host "Build directory preserved at: $workDir" -ForegroundColor Yellow
Write-Host "You can inspect the files before submission." -ForegroundColor Yellow

