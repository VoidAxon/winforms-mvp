# GitHub Releases + Packages Publishing Pipeline — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Publish `WinformsMVP` and `WinformsMVP.DependencyInjection` NuGet packages (preview and stable) to GitHub Packages on every `v*` tag, attaching the `.nupkg` files to a GitHub Release.

**Architecture:** Centralized package metadata in a repo-root `Directory.Build.props`; per-project identity + README packing in the two shippable csproj; version injected from the git tag by a tag-triggered GitHub Actions workflow running on `windows-latest`; the bundled analyzer's presence in the core package asserted in CI.

**Tech Stack:** .NET SDK (multi-target net40/net48), MSBuild `Directory.Build.props`, `dotnet pack`, GitHub Actions, GitHub Packages NuGet feed, `gh` CLI, `Microsoft.NETFramework.ReferenceAssemblies.net40` for net40 CI builds.

**Spec:** `docs/superpowers/specs/2026-06-02-github-release-packaging-design.md`

---

## File structure

| File | Responsibility | Action |
|---|---|---|
| `Directory.Build.props` | Shared package metadata; default `IsPackable=false` | Create |
| `src/WinformsMVP/WinformsMVP.csproj` | Core package identity, README packing, net40 reference assemblies, opt-in `IsPackable=true` | Modify |
| `src/WinformsMVP.DependencyInjection/WinformsMVP.DependencyInjection.csproj` | DI package identity, README packing, opt-in `IsPackable=true` | Modify |
| `.github/workflows/release.yml` | Tag-driven build/test/pack/publish/release workflow | Create |
| `README.md` | Consumer install instructions + maintainer release instructions | Modify |

Verification in this plan is pack-and-inspect (config/infra work), not unit tests. Each task edits, verifies locally, and commits. The final task is a gated live-fire end-to-end test.

---

## Task 1: Centralized package metadata (`Directory.Build.props`)

**Files:**
- Create: `Directory.Build.props`

- [ ] **Step 1: Create `Directory.Build.props` at the repo root**

```xml
<Project>

  <!--
    Shared NuGet package metadata for all shippable projects. This file is
    auto-imported by every project under the repo root.

    IsPackable defaults to false here: nothing is packable unless a project
    explicitly opts in with <IsPackable>true</IsPackable>. This prevents
    accidentally publishing samples, tests, or the (bundled) analyzer project.

    Note: do NOT put the README <None Include> here. Directory.Build.props is
    imported at the TOP of each project, before the csproj body sets
    IsPackable=true, so an IsPackable-conditioned item here would evaluate
    against the default (false) and be skipped. The README item lives in each
    shippable csproj instead.
  -->
  <PropertyGroup>
    <Authors>VoidAxon</Authors>
    <Company>VoidAxon</Company>
    <Copyright>Copyright (c) 2026 VoidAxon</Copyright>
    <PackageLicenseExpression>MIT</PackageLicenseExpression>
    <PackageProjectUrl>https://github.com/VoidAxon/winforms-mvp</PackageProjectUrl>
    <RepositoryUrl>https://github.com/VoidAxon/winforms-mvp</RepositoryUrl>
    <RepositoryType>git</RepositoryType>
    <PackageReadmeFile>README.md</PackageReadmeFile>
    <PublishRepositoryUrl>true</PublishRepositoryUrl>
    <EmbedUntrackedSources>true</EmbedUntrackedSources>

    <IsPackable>false</IsPackable>
  </PropertyGroup>

</Project>
```

- [ ] **Step 2: Verify the solution still builds clean (no new warnings, analyzers off in non-relevant projects)**

Run: `dotnet build winforms-mvp.sln -c Release --no-incremental 2>&1 | Select-String -Pattern "warning (MVP|RS|CS|IDE)|error"`
Expected: no MVP/RS/CS/IDE warnings, no errors. (Pre-existing xUnit1031 warnings are unrelated and acceptable.)

- [ ] **Step 3: Verify nothing packs by default yet**

Run: `dotnet pack winforms-mvp.sln -c Release -o artifacts-check 2>&1 ; Get-ChildItem artifacts-check -ErrorAction SilentlyContinue`
Expected: command succeeds and `artifacts-check` is empty or absent (no project is packable yet — they opt in during Tasks 2 and 3).

- [ ] **Step 4: Clean up the check folder**

Run: `Remove-Item -Recurse -Force artifacts-check -ErrorAction SilentlyContinue`

- [ ] **Step 5: Commit**

```bash
git add Directory.Build.props
git commit -m "build: add Directory.Build.props with shared package metadata"
```

---

## Task 2: Core package identity + net40 reference assemblies (`WinformsMVP.csproj`)

**Files:**
- Modify: `src/WinformsMVP/WinformsMVP.csproj`

- [ ] **Step 1: Add package identity + README packing to the main `<PropertyGroup>`**

In `src/WinformsMVP/WinformsMVP.csproj`, change the opening `<PropertyGroup>` from:

```xml
  <PropertyGroup>
    <TargetFrameworks>net40;net48</TargetFrameworks>
    <OutputType>Library</OutputType>
    <RootNamespace>WinformsMVP</RootNamespace>
    <AssemblyName>WinformsMVP</AssemblyName>
    <LangVersion>latest</LangVersion>
    <UseWindowsForms>true</UseWindowsForms>
    <GenerateAssemblyInfo>false</GenerateAssemblyInfo>
  </PropertyGroup>
```

to:

```xml
  <PropertyGroup>
    <TargetFrameworks>net40;net48</TargetFrameworks>
    <OutputType>Library</OutputType>
    <RootNamespace>WinformsMVP</RootNamespace>
    <AssemblyName>WinformsMVP</AssemblyName>
    <LangVersion>latest</LangVersion>
    <UseWindowsForms>true</UseWindowsForms>
    <GenerateAssemblyInfo>false</GenerateAssemblyInfo>

    <!-- Shippable: opt in to packing (Directory.Build.props defaults to false). -->
    <IsPackable>true</IsPackable>
    <PackageId>WinformsMVP</PackageId>
    <Description>A WinForms Model-View-Presenter (Supervising Controller) framework for .NET Framework, with compile-time MVP design-rule analyzers bundled in. Multi-targets net40 and net48.</Description>
    <PackageTags>mvp;winforms;model-view-presenter;desktop;dotnet-framework;analyzers</PackageTags>
  </PropertyGroup>
```

- [ ] **Step 2: Add the README pack item and the net40 reference-assemblies package**

In `src/WinformsMVP/WinformsMVP.csproj`, find the existing net40 `<ItemGroup>`:

```xml
  <ItemGroup Condition="'$(TargetFramework)' == 'net40'">
    <PackageReference Include="System.ValueTuple" Version="4.5.0" />
  </ItemGroup>
```

and replace it with:

```xml
  <ItemGroup Condition="'$(TargetFramework)' == 'net40'">
    <PackageReference Include="System.ValueTuple" Version="4.5.0" />
    <!--
      GitHub-hosted runners do not ship the net40 targeting pack. This package
      supplies the net40 reference assemblies (including WinForms) so the net40
      target compiles without a locally installed targeting pack. Build-only:
      PrivateAssets="all" keeps it out of the package's dependency list.
    -->
    <PackageReference Include="Microsoft.NETFramework.ReferenceAssemblies.net40" Version="1.0.3" PrivateAssets="all" />
  </ItemGroup>
```

Then, directly after that `</ItemGroup>`, add the README pack item (the path climbs two levels from `src/WinformsMVP/` to the repo root):

```xml
  <!-- Pack the repo-root README into the package (referenced by PackageReadmeFile in Directory.Build.props). -->
  <ItemGroup>
    <None Include="..\..\README.md" Pack="true" PackagePath="\" Visible="false" />
  </ItemGroup>
```

- [ ] **Step 3: Verify the local build still works and stays warning-clean (net40 + net48)**

Run: `dotnet build src/WinformsMVP/WinformsMVP.csproj -c Release --no-incremental 2>&1 | Select-String -Pattern "warning|error" | Select-String -NotMatch "xUnit1031"`
Expected: no errors and no new warnings; both `bin/Release/net40/WinformsMVP.dll` and `bin/Release/net48/WinformsMVP.dll` produced.

- [ ] **Step 4: Pack the core package and inspect its contents**

Run:
```powershell
Remove-Item -Recurse -Force artifacts -ErrorAction SilentlyContinue
dotnet pack src/WinformsMVP/WinformsMVP.csproj -c Release -p:Version=1.0.0-preview.1 -o artifacts
```
Then inspect the layout (using the Bash tool for `unzip`):
```bash
unzip -l artifacts/WinformsMVP.1.0.0-preview.1.nupkg
```
Expected entries include:
- `lib/net40/WinformsMVP.dll`
- `lib/net48/WinformsMVP.dll`
- `analyzers/dotnet/cs/WinformsMVP.Analyzers.dll`
- `README.md`
- `WinformsMVP.nuspec`

- [ ] **Step 5: Verify the nuspec metadata and dependency list**

Run (Bash tool):
```bash
unzip -p artifacts/WinformsMVP.1.0.0-preview.1.nupkg WinformsMVP.nuspec
```
Expected:
- `<license type="expression">MIT</license>`, `<authors>VoidAxon</authors>`, `<readme>README.md</readme>`, `<projectUrl>` and `<repository>` pointing at the GitHub repo.
- net48 dependency group is empty; net40 dependency group lists only `System.ValueTuple`.
- **No** dependency on `WinformsMVP.Analyzers` and **no** dependency on `Microsoft.NETFramework.ReferenceAssemblies.net40` (both are build-only / bundled).

- [ ] **Step 6: Clean up the artifacts folder**

Run: `Remove-Item -Recurse -Force artifacts -ErrorAction SilentlyContinue`

- [ ] **Step 7: Commit**

```bash
git add src/WinformsMVP/WinformsMVP.csproj
git commit -m "build: add WinformsMVP package identity and net40 reference assemblies"
```

---

## Task 3: DI package identity (`WinformsMVP.DependencyInjection.csproj`)

**Files:**
- Modify: `src/WinformsMVP.DependencyInjection/WinformsMVP.DependencyInjection.csproj`

- [ ] **Step 1: Add package identity to the main `<PropertyGroup>`**

In `src/WinformsMVP.DependencyInjection/WinformsMVP.DependencyInjection.csproj`, change the opening `<PropertyGroup>` from:

```xml
  <PropertyGroup>
    <TargetFramework>net48</TargetFramework>
    <OutputType>Library</OutputType>
    <RootNamespace>WinformsMVP.DependencyInjection</RootNamespace>
    <AssemblyName>WinformsMVP.DependencyInjection</AssemblyName>
    <LangVersion>latest</LangVersion>
    <UseWindowsForms>true</UseWindowsForms>
    <GenerateAssemblyInfo>false</GenerateAssemblyInfo>
  </PropertyGroup>
```

to:

```xml
  <PropertyGroup>
    <TargetFramework>net48</TargetFramework>
    <OutputType>Library</OutputType>
    <RootNamespace>WinformsMVP.DependencyInjection</RootNamespace>
    <AssemblyName>WinformsMVP.DependencyInjection</AssemblyName>
    <LangVersion>latest</LangVersion>
    <UseWindowsForms>true</UseWindowsForms>
    <GenerateAssemblyInfo>false</GenerateAssemblyInfo>

    <!-- Shippable: opt in to packing (Directory.Build.props defaults to false). -->
    <IsPackable>true</IsPackable>
    <PackageId>WinformsMVP.DependencyInjection</PackageId>
    <Description>Microsoft.Extensions.DependencyInjection integration for the WinformsMVP framework: presenter factory, module registration, and service wiring.</Description>
    <PackageTags>mvp;winforms;dependency-injection;di;microsoft-extensions-dependencyinjection</PackageTags>
  </PropertyGroup>
```

- [ ] **Step 2: Add the README pack item**

In the same file, after the existing `<ItemGroup>` that contains the `ProjectReference` to `..\WinformsMVP\WinformsMVP.csproj`, add:

```xml
  <!-- Pack the repo-root README into the package (referenced by PackageReadmeFile in Directory.Build.props). -->
  <ItemGroup>
    <None Include="..\..\README.md" Pack="true" PackagePath="\" Visible="false" />
  </ItemGroup>
```

- [ ] **Step 3: Pack the DI package with the same version as core and inspect the dependency**

Run:
```powershell
Remove-Item -Recurse -Force artifacts -ErrorAction SilentlyContinue
dotnet pack src/WinformsMVP.DependencyInjection/WinformsMVP.DependencyInjection.csproj -c Release -p:Version=1.0.0-preview.1 -o artifacts
```
Then inspect (Bash tool):
```bash
unzip -p artifacts/WinformsMVP.DependencyInjection.1.0.0-preview.1.nupkg WinformsMVP.DependencyInjection.nuspec
```
Expected dependencies (net48 group):
- `WinformsMVP` at version `1.0.0-preview.1` (the `-p:Version` global property flows to the referenced core project, aligning the dependency).
- `Microsoft.Extensions.DependencyInjection.Abstractions` `8.0.0`.
Also confirm `<license type="expression">MIT</license>`, `<authors>VoidAxon</authors>`, `<readme>README.md</readme>`.

- [ ] **Step 4: Clean up the artifacts folder**

Run: `Remove-Item -Recurse -Force artifacts -ErrorAction SilentlyContinue`

- [ ] **Step 5: Commit**

```bash
git add src/WinformsMVP.DependencyInjection/WinformsMVP.DependencyInjection.csproj
git commit -m "build: add WinformsMVP.DependencyInjection package identity"
```

---

## Task 4: Release workflow (`.github/workflows/release.yml`)

**Files:**
- Create: `.github/workflows/release.yml`

- [ ] **Step 1: Create the workflow file**

```yaml
name: Release

on:
  push:
    tags:
      - 'v*'

permissions:
  contents: write   # create GitHub Releases
  packages: write   # push to GitHub Packages

jobs:
  release:
    runs-on: windows-latest   # net40/net48 + WinForms require .NET Framework MSBuild (Windows only)

    steps:
      - name: Checkout
        uses: actions/checkout@v4
        with:
          fetch-depth: 0

      - name: Setup .NET SDK
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '9.0.x'

      - name: Derive version from tag
        shell: pwsh
        run: |
          $version = "${{ github.ref_name }}" -replace '^v', ''
          "VERSION=$version" | Out-File -FilePath $env:GITHUB_ENV -Append -Encoding utf8
          if ($version -match '-') {
            "IS_PRERELEASE=true" | Out-File -FilePath $env:GITHUB_ENV -Append -Encoding utf8
          } else {
            "IS_PRERELEASE=false" | Out-File -FilePath $env:GITHUB_ENV -Append -Encoding utf8
          }
          Write-Host "Releasing $version (prerelease=$($version -match '-'))"

      - name: Restore
        run: dotnet restore winforms-mvp.sln

      - name: Build (Release)
        shell: pwsh
        run: dotnet build winforms-mvp.sln -c Release --no-restore -p:Version=$env:VERSION

      - name: Test (gate)
        run: dotnet test tests/WinformsMVP.Samples.Tests/WinformsMVP.Samples.Tests.csproj -c Release --no-build

      - name: Pack
        shell: pwsh
        run: |
          dotnet pack src/WinformsMVP/WinformsMVP.csproj -c Release -p:Version=$env:VERSION -o artifacts
          dotnet pack src/WinformsMVP.DependencyInjection/WinformsMVP.DependencyInjection.csproj -c Release -p:Version=$env:VERSION -o artifacts

      - name: Assert analyzer is bundled in the core package
        shell: pwsh
        run: |
          $pkg = Get-ChildItem artifacts/WinformsMVP.*.nupkg | Where-Object { $_.Name -notlike '*DependencyInjection*' } | Select-Object -First 1
          if (-not $pkg) { throw "Core package not found in artifacts" }
          Add-Type -AssemblyName System.IO.Compression.FileSystem
          $zip = [System.IO.Compression.ZipFile]::OpenRead($pkg.FullName)
          try {
            $entry = $zip.Entries | Where-Object { $_.FullName -eq 'analyzers/dotnet/cs/WinformsMVP.Analyzers.dll' }
          } finally {
            $zip.Dispose()
          }
          if (-not $entry) { throw "Analyzer DLL missing from $($pkg.Name) - expected analyzers/dotnet/cs/WinformsMVP.Analyzers.dll" }
          Write-Host "OK: analyzer bundled in $($pkg.Name)"

      - name: Push to GitHub Packages
        shell: pwsh
        run: dotnet nuget push "artifacts/*.nupkg" --source "https://nuget.pkg.github.com/VoidAxon/index.json" --api-key ${{ secrets.GITHUB_TOKEN }} --skip-duplicate

      - name: Create GitHub Release
        shell: pwsh
        env:
          GH_TOKEN: ${{ secrets.GITHUB_TOKEN }}
        run: |
          $files = Get-ChildItem artifacts/*.nupkg | ForEach-Object { $_.FullName }
          $ghArgs = @("${{ github.ref_name }}") + $files + @('--title', "${{ github.ref_name }}", '--generate-notes')
          if ($env:IS_PRERELEASE -eq 'true') { $ghArgs += '--prerelease' }
          gh release create @ghArgs
```

- [ ] **Step 2: Validate the YAML syntax**

Run (Bash tool, if `python3` is available):
```bash
python3 -c "import yaml,sys; yaml.safe_load(open('.github/workflows/release.yml')); print('YAML OK')"
```
Expected: `YAML OK`. If `python3`/`yaml` is unavailable, instead re-read the file and confirm: trigger is `tags: ['v*']`, `runs-on: windows-latest`, permissions include `contents: write` and `packages: write`, and the push/release steps reference `${{ secrets.GITHUB_TOKEN }}`.

- [ ] **Step 3: Commit**

```bash
git add .github/workflows/release.yml
git commit -m "ci: add tag-driven release workflow for GitHub Packages + Releases"
```

---

## Task 5: Consumer + maintainer documentation (`README.md`)

**Files:**
- Modify: `README.md`

- [ ] **Step 1: Read the README to choose insertion points**

Run: `Read README.md`
Identify the end of the introductory section (after the project title and one-paragraph description). The **Installation** section goes there; the **Cutting a release** section goes at the end of the file.

- [ ] **Step 2: Insert the Installation section after the intro**

Insert this block:

```markdown
## Installation

The packages are published to **GitHub Packages**. GitHub Packages requires
authentication for NuGet restore even from public repositories, so pick one of
the two routes below.

### Option A — GitHub Packages feed (recommended for ongoing use)

1. Create a GitHub Personal Access Token (classic) with the `read:packages` scope.
2. Add a `nuget.config` next to your solution:

   ```xml
   <?xml version="1.0" encoding="utf-8"?>
   <configuration>
     <packageSources>
       <add key="github-voidaxon" value="https://nuget.pkg.github.com/VoidAxon/index.json" />
     </packageSources>
     <packageSourceCredentials>
       <github-voidaxon>
         <add key="Username" value="YOUR_GITHUB_USERNAME" />
         <add key="ClearTextPassword" value="YOUR_PAT_WITH_read_packages" />
       </github-voidaxon>
     </packageSourceCredentials>
   </configuration>
   ```

   Prefer supplying the PAT via an environment variable or
   `dotnet nuget add source ... --username ... --password ...` over committing it.

3. Install (the `--prerelease` flag is required while only preview versions exist):

   ```bash
   dotnet add package WinformsMVP --prerelease
   dotnet add package WinformsMVP.DependencyInjection --prerelease
   ```

### Option B — download the .nupkg from Releases (no authentication)

1. Open the repository's **Releases** page and download the `.nupkg` files for the
   version you want.
2. Put them in a local folder, register it as a source, and install:

   ```bash
   dotnet nuget add source C:\path\to\folder --name winformsmvp-local
   dotnet add package WinformsMVP --prerelease
   ```
```

- [ ] **Step 3: Append the maintainer release section at the end of the README**

```markdown
## Cutting a release (maintainers)

Releases are tag-driven. Pushing a tag that starts with `v` triggers
`.github/workflows/release.yml`, which builds, runs the test suite, packs both
packages, publishes them to GitHub Packages, and creates a GitHub Release with
the `.nupkg` files attached.

```bash
git tag v1.0.0-preview.1
git push origin v1.0.0-preview.1
```

A tag containing a hyphen (e.g. `v1.0.0-preview.1`) is published as a NuGet
prerelease and marked as a GitHub pre-release. A clean tag (e.g. `v1.0.0`) is a
stable release. The package version is taken directly from the tag (the leading
`v` is stripped).
```

- [ ] **Step 4: Verify the README renders sensibly**

Run: `Read README.md`
Expected: the Installation section appears after the intro; the Cutting a release section is at the end; code fences are balanced.

- [ ] **Step 5: Commit**

```bash
git add README.md
git commit -m "docs: document GitHub Packages install and tag-driven release process"
```

---

## Task 6: Live-fire end-to-end test (GATED — requires user authorization)

> **This task performs outward-facing actions: it pushes a real tag, creates a real GitHub Release, and publishes real package versions. Do NOT run it without explicit user go-ahead.** All effects are reversible via the cleanup steps. Confirm with the user before Step 1.

**Files:** none (operational verification).

- [ ] **Step 1: Confirm the workflow and secrets are in place**

The workflow uses the built-in `GITHUB_TOKEN` (no extra secret needed). Confirm the workflow file is on the default branch (push the prior commits first):
```bash
git push origin master
```

- [ ] **Step 2: Push a throwaway prerelease tag to trigger the workflow**

```bash
git tag v0.0.1-citest.1
git push origin v0.0.1-citest.1
```

- [ ] **Step 3: Watch the workflow run**

Run: `gh run watch` (or `gh run list --workflow release.yml`)
Expected: all steps succeed, including "Assert analyzer is bundled in the core package".

- [ ] **Step 4: Verify the success criteria**

Run: `gh release view v0.0.1-citest.1`
Expected: the release exists, is flagged **Pre-release**, and has both `WinformsMVP.0.0.1-citest.1.nupkg` and `WinformsMVP.DependencyInjection.0.0.1-citest.1.nupkg` attached.

Then confirm the packages appear at `https://github.com/VoidAxon/winforms-mvp/packages` at version `0.0.1-citest.1`.

- [ ] **Step 5: Clean up the throwaway release, tag, and package versions**

```bash
gh release delete v0.0.1-citest.1 --yes
git push origin :refs/tags/v0.0.1-citest.1
git tag -d v0.0.1-citest.1
```
Delete the two package versions via the package pages on GitHub (Settings → delete version), or, if `VoidAxon` is a user account, via the API:
```bash
# List versions, then delete each test version by id (user-owned packages endpoint):
gh api /user/packages/nuget/WinformsMVP/versions
gh api --method DELETE /user/packages/nuget/WinformsMVP/versions/VERSION_ID
gh api /user/packages/nuget/WinformsMVP.DependencyInjection/versions
gh api --method DELETE /user/packages/nuget/WinformsMVP.DependencyInjection/versions/VERSION_ID
```
(If `VoidAxon` is an organization, use the `/orgs/VoidAxon/packages/...` endpoints instead. The UI route works regardless of account type.)

- [ ] **Step 6: Confirm cleanup**

Run: `gh release list` and check the packages page.
Expected: no `v0.0.1-citest.1` release and no `0.0.1-citest.1` package versions remain.

---

## Notes for the implementer

- **`.claude/settings.local.json`** is intentionally left uncommitted throughout — never `git add` it.
- The repo's commit convention ends commit messages with the `Co-Authored-By` trailer used elsewhere in the history.
- The real first preview release (after this plan is verified) is cut by pushing `v1.0.0-preview.1` per Task 5 — that is normal operation, not part of this plan.
