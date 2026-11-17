$ErrorActionPreference = 'Stop'

$toolsDir = "$(Split-Path -parent $MyInvocation.MyCommand.Definition)"

# The release version and ZIP checksum below are filled in by build-chocolatey.ps1 when packing.
# The URL uses the release version, not $env:ChocolateyPackageVersion, so a package-fix version
# (e.g. 1.0.5.20261118) still downloads the right release.
$packageArgs = @{
  packageName    = $env:ChocolateyPackageName
  unzipLocation  = $toolsDir
  url64bit       = 'https://github.com/markdwags/ddcswitch/releases/download/v__VERSION__/ddcswitch-__VERSION__-win-x64.zip'
  checksum64     = '__CHECKSUM__'
  checksumType64 = 'sha256'
}

Install-ChocolateyZipPackage @packageArgs

# The ZIP has ddcswitch.exe at its root; Chocolatey creates the `ddcswitch` shim for it automatically
# and removes the shim and this folder on uninstall.
