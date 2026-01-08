# Chocolatey Package for ddcswitch

This directory contains the files needed to create a Chocolatey package for ddcswitch.

## Structure

- `ddcswitch.nuspec` - Package metadata and configuration
- `tools/chocolateyinstall.ps1` - Installation script (downloads from GitHub releases)
- `tools/chocolateyuninstall.ps1` - Uninstallation script
- `tools/VERIFICATION.txt` - Verification instructions and checksums

## Building the Package Locally

**Note**: The version and checksum are automatically populated from `CHANGELOG.md` and GitHub releases.

- **Version**: Set in `ddcswitch.nuspec` as `__VERSION__` placeholder, then passed to PowerShell via `$env:chocolateyPackageVersion`
- **Checksum**: Embedded directly in `chocolateyinstall.ps1` as `__CHECKSUM__` placeholder (required by Chocolatey validation)

### Prerequisites

1. Install Chocolatey if you haven't already:
   ```powershell
   Set-ExecutionPolicy Bypass -Scope Process -Force; [System.Net.ServicePointManager]::SecurityProtocol = [System.Net.ServicePointManager]::SecurityProtocol -bor 3072; iex ((New-Object System.Net.WebClient).DownloadString('https://community.chocolatey.org/install.ps1'))
   ```

### Using the Build Script (Recommended)

The easiest way to build the package is using the provided script:

```powershell
# Build from the latest version in CHANGELOG.md (downloads from GitHub releases)
.\build-chocolatey.ps1

# Build a specific version
.\build-chocolatey.ps1 -Version 1.0.2

# Build using a local ZIP file (useful before pushing to GitHub)
.\build-chocolatey.ps1 -LocalZip "dist\ddcswitch-1.0.2-win-x64.zip"
```

The script will:
1. Extract the version from CHANGELOG.md (or use the specified version)
2. Download the release ZIP from GitHub (or use the local file)
3. Calculate the SHA256 checksum
4. Replace placeholders in nuspec and install script
5. Build the .nupkg package

### Testing the Package

After building:

```powershell
# Install locally
choco install ddcswitch -s . --force

# Test it works
ddcswitch --version
ddcswitch list

# Uninstall
choco uninstall ddcswitch
```


## Automated Package Creation

The GitHub Actions workflow (`.github/workflows/ci-cd.yml`) automatically:
1. Detects the version from `CHANGELOG.md` (e.g., `[1.0.2]`)
2. Calculates the SHA256 checksum of the release ZIP
3. Replaces `__VERSION__` in `ddcswitch.nuspec` (Chocolatey passes this to scripts via `$env:chocolateyPackageVersion`)
4. Replaces `__CHECKSUM__` in `chocolateyinstall.ps1`
5. Creates the `.nupkg` file
6. Uploads it as an artifact and to the GitHub release

**No manual version updates needed!** Just update `CHANGELOG.md` with the new version.

The version flows naturally: `CHANGELOG.md` → `nuspec` → `$env:chocolateyPackageVersion` → PowerShell script.
3. Creates the `.nupkg` file
4. Uploads it as an artifact

## Submitting to Chocolatey

### First-Time Submission (Manual)

1. Create a Chocolatey account at https://community.chocolatey.org/
2. Download the `.nupkg` artifact from GitHub Actions
3. Submit via https://community.chocolatey.org/packages/submit
4. Wait for moderation and approval (typically 1-7 days)
5. Address any feedback from moderators

### Optional: Automated Submission (After Establishing Trust)

After 2-3 successful manual submissions, you can enable automatic pushing:

1. Get your API key from https://community.chocolatey.org/account
2. Add it as a GitHub Actions secret: `CHOCO_API_KEY`
3. Add a step to the workflow to automatically push packages:
   ```yaml
   - name: Push to Chocolatey
     shell: pwsh
     run: |
       choco apikey --key $env:CHOCO_API_KEY --source https://push.chocolatey.org/
       choco push chocolatey/*.nupkg --source https://push.chocolatey.org/
     env:
       CHOCO_API_KEY: ${{ secrets.CHOCO_API_KEY }}
   ```

**Note**: Automatic pushing is not recommended for initial submissions as they require human moderation.

## Package Maintenance

For each new release:

1. **Update `CHANGELOG.md`** with the new version:
   ```markdown
   ## [1.0.3] - 2026-01-15
   
   ### Added
   - New feature
   ```

2. **Push to `main` branch** - triggers the workflow

3. **Wait for GitHub Actions** (~5-10 minutes):
   - Builds ddcswitch.exe
   - Creates GitHub release with ZIP
   - Downloads ZIP and calculates SHA256 checksum
   - Creates Chocolatey package (`.nupkg`)
   - Uploads to GitHub release assets and artifacts

4. **Download the `.nupkg` file** from GitHub release assets

5. **Manually submit** to https://community.chocolatey.org/packages/submit

6. **Wait for approval** (faster after first approval)

**That's it!** The version is automatically sourced from `CHANGELOG.md` - no manual file editing needed.

**The version is automatically detected from CHANGELOG.md - no manual file updates needed!**

## Resources

- [Chocolatey Package Creation Guide](https://docs.chocolatey.org/en-us/create/create-packages)
- [Package Guidelines](https://docs.chocolatey.org/en-us/community-repository/moderation/package-validator)
- [Submitting Packages](https://docs.chocolatey.org/en-us/community-repository/maintainers/package-submission)

