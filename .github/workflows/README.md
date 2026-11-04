# GitHub Actions Workflows

## ci-cd.yml: build, test and release

**Triggers:** pull requests to `main`, pushes to `main`, and manual dispatch.
Feature branches are built through their pull request, so the same commit isn't built twice. A newer push
to a pull request cancels that PR's run that is still in progress. Runs on `main` are never cancelled.

### `build` job (every run)
1. Installs the .NET SDK pinned in `/global.json` and restores NuGet packages from cache.
2. Restores the solution. **High or critical NuGet vulnerability advisories (NU1903/NU1904) fail the build.**
3. Runs the unit tests (`DDCSwitch.Tests`, xUnit v3 on Microsoft.Testing.Platform) with coverlet coverage.
   The test summary is posted to the job summary page. TRX and Cobertura files are uploaded as the
   `test-results-<sha>` artifact (7 days).
4. Publishes the NativeAOT `ddcswitch.exe`, checks that it exists, and smoke-runs `ddcswitch --json version`.
5. Reads the version from `CHANGELOG.md` and checks whether tag `v<version>` already exists. The job
   summary says whether merging will create a release.
6. Uploads `ddcswitch.exe` as the `DDCSwitch-build-<sha>` artifact (7 days).

### `release` job (push to `main` with a new version only)
Runs only when the CHANGELOG version has **no tag yet**. Merges that don't bump the version don't release.
1. Packages `ddcswitch-<version>-win-x64.zip` (exe, README, LICENSE, EXAMPLES, CHANGELOG) and computes its SHA256.
2. Extracts that version's section of `CHANGELOG.md` as the release notes.
3. Fills in the Chocolatey templates (version and checksum) and runs `choco pack`.
4. Creates the GitHub Release and tag `v<version>` on the built commit, attaching the ZIP, the exe and the `.nupkg`.
5. Uploads the `.nupkg` as an artifact (90 days).

Only this job has `contents: write`. The rest of the workflow is read-only.

## Releasing

1. Add a new version at the top of `CHANGELOG.md`:
   ```markdown
   ## [1.2.3] - 2026-01-15

   ### Added
   - New feature description
   ```
2. Open a pull request. CI builds and tests it, and the job summary says `v1.2.3` will be released.
3. Merge. The `release` job creates tag `v1.2.3` and the GitHub Release.

The version must be the first `[X.Y.Z]` in `CHANGELOG.md`. The project file reads the same value for the
assembly version.

## Keeping dependencies current

`.github/dependabot.yml` opens weekly PRs for NuGet packages, GitHub Actions and the .NET SDK in `global.json`.

## Troubleshooting

- **"Could not find version in CHANGELOG.md"**: the first version heading must look like `## [X.Y.Z] - YYYY-MM-DD`.
- **Release didn't run after a merge**: the version's tag already exists. Bump the version in `CHANGELOG.md`.
- **NU1903 / NU1904 restore error**: a package (often the SDK-provided ILCompiler) has a known vulnerability.
  Update the SDK in `global.json` or the package version.
- **NativeAOT link fails locally with `'vswhere.exe' is not recognized`**: add
  `%ProgramFiles(x86)%\Microsoft Visual Studio\Installer` to `PATH`.

## Local equivalent

```powershell
dotnet restore DDCSwitch.sln -r win-x64
dotnet test --project DDCSwitch.Tests -c Release
dotnet publish DDCSwitch/DDCSwitch.csproj -c Release -r win-x64
```
