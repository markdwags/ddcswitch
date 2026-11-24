# Chocolatey package for ddcswitch

Templates for the `ddcswitch` package on the Chocolatey Community Repository. The package does **not**
embed the exe: `chocolateyinstall.ps1` downloads the GitHub release ZIP, checks its SHA256, and extracts
it. Because nothing is embedded, there is no `tools/VERIFICATION.txt` (Chocolatey only wants one for
packages that embed binaries). Chocolatey then creates the `ddcswitch` shim on `PATH` automatically, and removes the shim and files
on uninstall.

| File | Purpose |
|------|---------|
| `ddcswitch.nuspec` | Package metadata. `__VERSION__` = package version. |
| `tools/chocolateyinstall.ps1` | Downloads + verifies the release ZIP. `__VERSION__` = release version, `__CHECKSUM__` = ZIP SHA256. |

Don't fill the placeholders in by hand. `build-chocolatey.ps1` (repo root) does it in a copy under
`dist/chocolatey-build/` and packs the result. CI uses the same script.

## Build locally

Requires `choco`. The release (`v<version>`) must already exist on GitHub, since the checksum has to
match the ZIP users will download.

```powershell
.\build-chocolatey.ps1                                              # CHANGELOG version -> dist\ddcswitch.<ver>.nupkg
.\build-chocolatey.ps1 -Version 1.0.5                               # a specific release
.\build-chocolatey.ps1 -Version 1.0.5 -PackageVersion 1.0.5.20261118  # package fix for the same release
```

## Test the package

In an **elevated** PowerShell:

```powershell
choco install ddcswitch -s .\dist -y --force
# new shell, so PATH is fresh:
ddcswitch --json version
where.exe ddcswitch        # C:\ProgramData\chocolatey\bin\ddcswitch.exe
choco uninstall ddcswitch -y
```

## In CI

When a push to `main` releases a new version, the `release` job runs
`build-chocolatey.ps1 -LocalZip <release zip>` and attaches `ddcswitch.<ver>.nupkg` to the GitHub Release
(and as a 90-day artifact). It then pushes that `.nupkg` to Chocolatey using the `CHOCO_API_KEY`
repository secret (`gh secret set CHOCO_API_KEY`, or Settings → Secrets and variables → Actions). The push
runs after the GitHub Release is created, because Chocolatey's verifier downloads the release ZIP. If the
secret isn't set, the step logs a warning and skips. The release still goes out.

## Submitting / publishing

1. Sign in at https://community.chocolatey.org/ and copy your API key from https://community.chocolatey.org/account.
2. Once per machine: `choco apikey --key <KEY> --source https://push.chocolatey.org/`
3. `choco push dist\ddcswitch.<ver>.nupkg --source https://push.chocolatey.org/`
   (or use the `.nupkg` attached to the GitHub Release).
4. The package goes through automated validation, verification (a test install) and a virus scan, then
   human moderation. You get emails with results; fix issues by pushing again.
   A rejected/unapproved version can be resubmitted with the same version number; an approved one can't,
   so use `-PackageVersion X.Y.Z.YYYYMMDD` for a package-only fix.

CI does step 3 for every release (see [In CI](#in-ci)). Push by hand only as a fallback: when the CI push
failed, or for a package-only fix (`-PackageVersion X.Y.Z.YYYYMMDD`).

## References

- [Create packages](https://docs.chocolatey.org/en-us/create/create-packages)
- [Package validator rules](https://docs.chocolatey.org/en-us/community-repository/moderation/package-validator/rules/)
- [Moderation process](https://docs.chocolatey.org/en-us/community-repository/moderation/)
