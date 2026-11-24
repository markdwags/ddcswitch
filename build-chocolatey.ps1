#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Builds the ddcswitch Chocolatey package (.nupkg).

.DESCRIPTION
    The package does not embed the exe: its install script downloads the GitHub release ZIP and
    checks it against a SHA256 checksum that this script embeds. So the checksum must be the
    checksum of the ZIP that is (or will be) attached to the GitHub release.

    1. Gets the version from CHANGELOG.md (or -Version).
    2. Downloads the release ZIP from GitHub (or uses -LocalZip) and computes its SHA256.
    3. Copies chocolatey/ to a work directory and fills in __VERSION__ / __CHECKSUM__.
    4. Runs choco pack, writing ddcswitch.<PackageVersion>.nupkg to -OutputDir.

    CI uses this same script with -LocalZip pointing at the ZIP it is about to release.

.PARAMETER Version
    Release version (X.Y.Z) the package installs. Defaults to the first version in CHANGELOG.md.

.PARAMETER PackageVersion
    Chocolatey package version. Defaults to -Version. Use a package-fix version such as
    1.0.5.20261118 to republish a fixed package for the same release.

.PARAMETER LocalZip
    Use this ZIP instead of downloading the release. Only meaningful if it is byte-for-byte the
    release ZIP (as in CI), otherwise the install-time checksum check will fail.

.PARAMETER OutputDir
    Where the .nupkg goes. Defaults to dist\ (the work directory is dist\chocolatey-build).

.EXAMPLE
    .\build-chocolatey.ps1

.EXAMPLE
    .\build-chocolatey.ps1 -Version 1.0.5 -PackageVersion 1.0.5.20261118
#>

[CmdletBinding()]
param(
    [string]$Version,
    [string]$PackageVersion,
    [string]$LocalZip,
    [string]$OutputDir = (Join-Path $PSScriptRoot 'dist')
)

$ErrorActionPreference = 'Stop'

# Explicit UTF-8 (no BOM) so the emoji in the nuspec survive Windows PowerShell 5.1 too.
$utf8 = New-Object System.Text.UTF8Encoding($false)
function Update-Placeholders([string]$Path, [hashtable]$Values) {
    $text = [System.IO.File]::ReadAllText($Path)
    foreach ($key in $Values.Keys) { $text = $text.Replace($key, $Values[$key]) }
    if ($text -match '__[A-Z]+__') { throw "Unreplaced placeholder $($matches[0]) in $Path" }
    [System.IO.File]::WriteAllText($Path, $text, $utf8)
}

if (-not $Version) {
    $changelog = [System.IO.File]::ReadAllText((Join-Path $PSScriptRoot 'CHANGELOG.md'))
    if ($changelog -notmatch '##\s+\[(\d+\.\d+\.\d+)\]') { throw 'Could not find a "## [X.Y.Z]" version in CHANGELOG.md' }
    $Version = $matches[1]
}
if (-not $PackageVersion) { $PackageVersion = $Version }
Write-Host "Release version: $Version  Package version: $PackageVersion" -ForegroundColor Cyan

$zipUrl = "https://github.com/markdwags/ddcswitch/releases/download/v$Version/ddcswitch-$Version-win-x64.zip"
$downloaded = $false
if ($LocalZip) {
    if (-not (Test-Path $LocalZip)) { throw "Local ZIP not found: $LocalZip" }
    $zipPath = (Resolve-Path $LocalZip).Path
} else {
    $zipPath = Join-Path ([System.IO.Path]::GetTempPath()) "ddcswitch-$Version-win-x64.zip"
    Write-Host "Downloading $zipUrl"
    try {
        Invoke-WebRequest -Uri $zipUrl -OutFile $zipPath -UseBasicParsing
    } catch {
        throw "Could not download the release ZIP (does release v$Version exist?): $zipUrl"
    }
    $downloaded = $true
}

try {
    $checksum = (Get-FileHash -Path $zipPath -Algorithm SHA256).Hash
    Write-Host "SHA256: $checksum" -ForegroundColor Green

    New-Item -ItemType Directory -Force -Path $OutputDir | Out-Null
    $OutputDir = (Resolve-Path $OutputDir).Path
    $workDir = Join-Path $OutputDir 'chocolatey-build'
    if (Test-Path $workDir) { Remove-Item $workDir -Recurse -Force }
    Copy-Item (Join-Path $PSScriptRoot 'chocolatey') -Destination $workDir -Recurse
    Remove-Item (Join-Path $workDir 'README.md') -ErrorAction SilentlyContinue  # maintainer docs, not package content

    $values = @{ '__VERSION__' = $Version; '__CHECKSUM__' = $checksum }
    Update-Placeholders (Join-Path $workDir 'ddcswitch.nuspec') @{ '__VERSION__' = $PackageVersion }
    Update-Placeholders (Join-Path $workDir 'tools\chocolateyinstall.ps1') $values

    if (-not (Get-Command choco -ErrorAction SilentlyContinue)) { throw 'choco is not installed (https://chocolatey.org/install)' }
    $nupkg = Join-Path $OutputDir "ddcswitch.$PackageVersion.nupkg"
    if (Test-Path $nupkg) { Remove-Item $nupkg -Force }

    choco pack (Join-Path $workDir 'ddcswitch.nuspec') --outputdirectory $OutputDir --limit-output
    if ($LASTEXITCODE -ne 0) { throw "choco pack failed with exit code $LASTEXITCODE" }
    if (-not (Test-Path $nupkg)) { throw "choco pack did not produce $nupkg" }
} finally {
    if ($downloaded -and (Test-Path $zipPath)) { Remove-Item $zipPath -Force }
}

Write-Host
Write-Host "Package: $nupkg" -ForegroundColor Green
Write-Host "Filled-in sources: $workDir"
Write-Host
Write-Host 'Test install (elevated shell):' -ForegroundColor Cyan
Write-Host "  choco install ddcswitch -s `"$OutputDir`" -y --force"
Write-Host 'Submit:' -ForegroundColor Cyan
Write-Host "  choco push `"$nupkg`" --source https://push.chocolatey.org/"
