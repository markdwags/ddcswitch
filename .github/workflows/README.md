# GitHub Actions Workflows

## ci-cd.yml: build, test and release

**Triggers:** pull requests to `main`, pushes to `main`, and manual dispatch.
Feature branches are built through their pull request, so the same commit isn't built twice. A newer push
to a pull request cancels that PR's run that is still in progress. Runs on `main` are never cancelled.

**Chore work is skipped:** pull requests from `chore/*` branches, and pushes to `main` that merge one
(`Merge pull request #N from owner/chore/...`) or whose commit message starts with `chore:` / `chore(`
(squash merges), don't build or release. The skip is on the `build` job, so the check still reports as
skipped and branch protection is satisfied. Use **Run workflow** (manual dispatch) to build anyway.
Don't put a version bump in a chore change: it won't release until a later non-chore merge.

### `build` job (every run)
1. Installs the .NET SDK pinned in `/global.json` and restores NuGet packages from cache.
2. Restores the solution. **High or critical NuGet vulnerability advisories (NU1903/NU1904) fail the build.**
3. Runs the unit tests (`tests/ddcswitch.Tests`, xUnit v3 on Microsoft.Testing.Platform) with coverlet coverage.
   The test summary is posted to the job summary page. TRX and Cobertura files are uploaded as the
   `test-results-<sha>` artifact (7 days).
4. Publishes the NativeAOT `ddcswitch.exe`, checks that it exists, and smoke-runs `ddcswitch --json version`
   (failing if the reported version isn't the CHANGELOG version).
5. Reads the version from `CHANGELOG.md` and checks whether tag `v<version>` already exists. The job
   summary says whether merging will create a release.
6. Uploads `ddcswitch.exe` as the `ddcswitch-build-<sha>` artifact (7 days).

### `release` job (push to `main` with a new version only)
Runs only when the CHANGELOG version has **no tag yet**. Merges that don't bump the version don't release.
1. Packages `ddcswitch-<version>-win-x64.zip` (exe, README, LICENSE, EXAMPLES, CHANGELOG) and computes its SHA256.
2. Extracts that version's section of `CHANGELOG.md` as the release notes.
3. Runs `build-chocolatey.ps1 -LocalZip <that zip>` (same script as local builds) to fill in the Chocolatey
   templates (version and checksum) and `choco pack` into `chocolatey-out/`.
4. Creates the GitHub Release and tag `v<version>` on the built commit, attaching the ZIP, the exe and the `.nupkg`.
5. Pushes the `.nupkg` to the Chocolatey Community Repository with the `CHOCO_API_KEY` repository secret.
   This runs after the GitHub Release exists, because Chocolatey's verifier downloads the release ZIP. If the
   secret isn't set, the step warns and skips. The version then waits for Chocolatey moderation.
6. Uploads the `.nupkg` as an artifact (90 days).

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
- **"CHOCO_API_KEY secret is not set" warning**: add the secret (`gh secret set CHOCO_API_KEY`, key from
  https://community.chocolatey.org/account), then push the `.nupkg` from the GitHub Release by hand (see
  `chocolatey/README.md`).
- **Push to Chocolatey failed**: the GitHub Release already exists. Fix the cause (for example an expired
  key) and use **Re-run failed jobs**, or push the release's `.nupkg` by hand.
- **NU1903 / NU1904 restore error**: a package (often the SDK-provided ILCompiler) has a known vulnerability.
  Update the SDK in `global.json` or the package version.
- **NativeAOT link fails locally with `'vswhere.exe' is not recognized`**: add
  `%ProgramFiles(x86)%\Microsoft Visual Studio\Installer` to `PATH`.

## Local equivalent

```powershell
dotnet restore ddcswitch.slnx -r win-x64
dotnet test --project tests/ddcswitch.Tests -c Release
dotnet publish src/ddcswitch/ddcswitch.csproj -c Release -r win-x64
```
