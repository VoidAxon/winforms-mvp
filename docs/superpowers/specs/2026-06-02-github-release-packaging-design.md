# Design: GitHub Releases + GitHub Packages (NuGet) publishing pipeline

Date: 2026-06-02
Status: Approved for planning
Repository: https://github.com/VoidAxon/winforms-mvp

## Goal

Publish a NuGet package for every version of the framework — starting with
preview builds before the official 1.0.0 — to **GitHub Packages**, and attach
the `.nupkg` files to **GitHub Releases**. Internal company use comes first; the
repository is public, so external users may consume it later. The pipeline must
leave a trivial path to also publishing on nuget.org when the project goes
public.

## Distribution model (decided)

- **Primary feed:** GitHub Packages NuGet feed for `VoidAxon`.
  Consuming from this feed **requires authentication (a PAT) even for a public
  repository** — this is a GitHub limitation, unrelated to repo visibility.
  Acceptable for internal developers, who configure a PAT once.
- **Auth-free fallback:** the `.nupkg` files are attached as **GitHub Release
  assets**, which are publicly downloadable without authentication. An external
  user can download the `.nupkg` and install it from a local folder source.
- **Future:** publishing to nuget.org (no consumer auth) is a later addition —
  one extra `dotnet nuget push` step against the nuget.org API key. The package
  metadata and pack steps are identical, so no rework is needed.

## Packages shipped

Two packages; the analyzer is **not** shipped standalone — it is bundled inside
the core package.

| Package | Contents | Target frameworks |
|---|---|---|
| `WinformsMVP` | Core framework + bundled Roslyn analyzers | net40; net48 |
| `WinformsMVP.DependencyInjection` | M.E.DI integration | net48 |

`WinformsMVP.DependencyInjection` references the core via `ProjectReference`, so
`dotnet pack` records a dependency on `WinformsMVP` at the **same version** packed
in the same run. Both packages always ship together at one version.

Non-shippable projects (`samples/*`, `tests/*`, `WinformsMVP.Net40SmokeTest`,
`WinformsMVP.Analyzers`) set `IsPackable=false`. The release workflow packs only
the two shippable projects explicitly, so nothing else can be published by mistake.

## Section 1 — Package metadata

A new repo-root **`Directory.Build.props`** centralizes shared metadata applied
to all packable projects, avoiding duplication:

- `Authors` = `VoidAxon`
- `Company` = `VoidAxon`
- `PackageLicenseExpression` = `MIT` (the repo ships `LICENSE.txt`, MIT, © 2026 VoidAxon)
- `PackageProjectUrl` / `RepositoryUrl` = `https://github.com/VoidAxon/winforms-mvp`
- `RepositoryType` = `git`
- `PackageReadmeFile` = `README.md`. Because the README lives at the repo root
  but the packable projects are under `src/X/`, the `None` item in
  `Directory.Build.props` must reference the root copy explicitly, or each
  project would look for a non-existent `src/X/README.md`:
  `<None Include="$(MSBuildThisFileDirectory)README.md" Pack="true" PackagePath="\" Visible="false"/>`
- `Copyright` = `Copyright (c) 2026 VoidAxon`
- `PublishRepositoryUrl` = `true`, `EmbedUntrackedSources` = `true` (source-link friendliness)

Each shippable `.csproj` keeps only its **own** identity:

- `WinformsMVP`: `PackageId`, `Description`, `PackageTags` (`mvp;winforms;model-view-presenter;desktop`).
- `WinformsMVP.DependencyInjection`: `PackageId`, `Description`, `PackageTags`.

`Directory.Build.props` must not set `Version` (version comes from the tag — see
Section 2) and must not force `IsPackable=true` (non-shippable projects opt out).

### Analyzer bundling — verification gate (most common pitfall)

`IsPackable=false` on the analyzer project only means "this project produces no
package of its own"; it does **not** make the analyzer DLL flow into the core
package. That wiring is already configured in `src/WinformsMVP/WinformsMVP.csproj`
(commit 548785b):

```xml
<None Include="$(MSBuildThisFileDirectory)..\WinformsMVP.Analyzers\bin\$(Configuration)\netstandard2.0\WinformsMVP.Analyzers.dll"
      Pack="true" PackagePath="analyzers/dotnet/cs" Visible="false" />
```

Because the path is `$(Configuration)`-dependent, the **CI Release pack must be
verified** to contain `analyzers/dotnet/cs/WinformsMVP.Analyzers.dll`. The
`BuildMVPAnalyzers` target runs `BeforeTargets="Build;Pack"` and the analyzer is
built in the same configuration, so the Release DLL exists before pack. This was
verified once locally (`dotnet pack -c Release` produced the entry); the workflow
adds an automated assertion so a future change can't silently drop the analyzer.

## Section 2 — Versioning

- **The git tag is the single source of truth.** Scheme: `vMAJOR.MINOR.PATCH`
  with an optional SemVer prerelease suffix:
  `v1.0.0-preview.1` → `v1.0.0-preview.2` → … → `v1.0.0` (official).
- The `.csproj` files carry no real version (a dev-only default like
  `1.0.0-dev` is fine). The workflow extracts the version from the tag (strips
  the leading `v`) and injects it: `dotnet pack -p:Version=1.0.0-preview.1`.
- **Prerelease handling — two independent systems, do not conflate:**
  - **NuGet** automatically treats any version containing a hyphen as a
    prerelease and hides it from default listings (only `--prerelease` /
    "include prerelease" surfaces it). This is automatic.
  - **GitHub Releases** does **not** infer prerelease from the tag name. The
    workflow must pass `--prerelease` to `gh release create` explicitly when the
    tag contains `-`. (Result is correct because Section 3 passes it explicitly.)

## Section 3 — CI workflow (`.github/workflows/release.yml`)

- **Trigger:** `on: push: tags: ['v*']`.
- **Runner:** `windows-latest`. Required — net40/net48 + WinForms build on
  .NET Framework MSBuild, which is not available on Linux runners.
- **net40 build prerequisite:** GitHub runners do not ship the net40 targeting
  pack. The core project adds `Microsoft.NETFramework.ReferenceAssemblies.net40`
  (build-only, `PrivateAssets=all`) so net40 — including its WinForms reference
  assemblies — compiles without a locally installed targeting pack. `PrivateAssets=all`
  keeps it out of the package's dependency list.
- **Permissions:** `packages: write`, `contents: write`.
- **Steps:**
  1. `actions/checkout`
  2. `actions/setup-dotnet` (SDK pinned)
  3. `dotnet restore winforms-mvp.sln`
  4. `dotnet build winforms-mvp.sln -c Release --no-restore`
  5. `dotnet test tests/WinformsMVP.Samples.Tests/... -c Release --no-build`
     — **gate**: a red test stops the release (333 tests today).
  6. Derive `VERSION` from `${{ github.ref_name }}` (strip leading `v`).
  7. `dotnet pack src/WinformsMVP/WinformsMVP.csproj -c Release -p:Version=$VERSION`
     and the same for `WinformsMVP.DependencyInjection`.
  8. **Assert** the core `.nupkg` contains `analyzers/dotnet/cs/WinformsMVP.Analyzers.dll`
     (unzip + grep; fail the job if missing — the pitfall guard from Section 1).
  9. `dotnet nuget push **/*.nupkg --source https://nuget.pkg.github.com/VoidAxon/index.json --api-key ${{ secrets.GITHUB_TOKEN }} --skip-duplicate`.
  10. `gh release create ${{ github.ref_name }} <nupkg files> --generate-notes`
      plus `--prerelease` when the tag contains `-`.

The built-in `GITHUB_TOKEN` is sufficient for both `packages: write` (push) and
`contents: write` (release). No extra secret is needed for GitHub Packages.

## Section 4 — Consumer documentation

- This repository's own build does **not** add the GitHub Packages feed to a
  `nuget.config`. Adding it would force authentication on every restore of this
  repo, which neither needs nor consumes its own packages.
- README gains a **"Installation"** section documenting, for consumers:
  - The GitHub Packages route: a sample consumer-side `nuget.config` adding the
    `https://nuget.pkg.github.com/VoidAxon/index.json` source, plus creating a
    PAT with `read:packages` scope and supplying it (env var / `nuget.config`
    credentials). Note the auth requirement up front.
  - The auth-free route: download the `.nupkg` from the Release page and install
    from a local folder source.
- A short **"Cutting a release"** note for maintainers: push a tag
  (`git tag v1.0.0-preview.1 && git push origin v1.0.0-preview.1`); the workflow
  does the rest.

## Out of scope

- Publishing to nuget.org (planned later; one added push step, no rework).
- Symbol packages (`.snupkg`) / a debugging-symbols server.
- Automated changelog generation beyond `gh release --generate-notes`.
- Signing the packages.

## Success criteria

1. Pushing tag `v1.0.0-preview.1` produces, with no manual steps:
   - Both packages in the GitHub Packages feed at `1.0.0-preview.1`.
   - A GitHub Release `v1.0.0-preview.1` flagged **pre-release** with both
     `.nupkg` files attached.
2. The core `.nupkg` contains `analyzers/dotnet/cs/WinformsMVP.Analyzers.dll`
   and `lib/net40` + `lib/net48` assemblies; its dependency list does not include
   the analyzer or the net40 reference-assemblies package.
3. `WinformsMVP.DependencyInjection` depends on `WinformsMVP` at the identical version.
4. A consumer following the README can install the package (PAT route) or use a
   Release asset (auth-free route).
5. A failing test blocks the release.
