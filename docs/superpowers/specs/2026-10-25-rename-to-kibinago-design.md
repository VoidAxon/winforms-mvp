# Rename to Kibinago — design

Date: 2026-10-25 · Status: draft (for the owner's review) · Scope: B (packages + namespaces + public naming; the repository keeps its name)

## Why

The onboarding trial (a fresh agent following only README / wiki) installed the wrong packages. nuget.org has
`WinFormsMvp` 1.0.1 and `WinFormsMVP.Core` 1.0.2008.2017 (another author, 2015–2017). NuGet IDs are
case-insensitive, so our `WinformsMVP` / `WinformsMVP.Core` collide; the templates' default `Version="*-*"` picks
`1.0.2008.2017` over `1.0.0-preview.8`, nuget.org being in every default source list. Result: "namespace
WinformsMVP not found" errors a newcomer cannot explain, and a dependency-confusion risk (anyone publishing a
higher version of those IDs is installed by our users).

The owner chose a distinctive product name: **Kibinago** (きびなご, silver-stripe round herring — small, quick,
moves in schools). Checked 2026-10-25: no `kibinago*` package on nuget.org (search: 0 hits), no GitHub repo of
that name. Trademarks not checked.

## Decisions

### Owner's
- Name: **Kibinago** (not `Kibi75`, not a company / account prefix, not a hyphenated variant).
- Scope **B**: package IDs, assembly / project names, namespaces and public naming all become Kibinago; the
  GitHub repository stays `winforms-mvp` (renaming it is a later, separate step).

### Proposed here (to confirm)

| What | Now | After |
|---|---|---|
| Packages | `WinformsMVP`, `WinformsMVP.Core`, `WinformsMVP.Testing`, `WinformsMVP.DependencyInjection`, `WinformsMVP.Templates` | `Kibinago`, `Kibinago.Core`, `Kibinago.Testing`, `Kibinago.DependencyInjection`, `Kibinago.Templates` |
| Projects / folders / assemblies | `src/WinformsMVP.Core/WinformsMVP.Core.csproj` → `WinformsMVP.Core.dll` … (also `WinformsMVP.Analyzers`, tests, `samples/WinformsMVP.Samples`) | `src/Kibinago.Core/Kibinago.Core.csproj` → `Kibinago.Core.dll` … (`Kibinago.Analyzers`, `tests/Kibinago.Samples.Tests`, `tests/Kibinago.Analyzers.Tests`, `samples/Kibinago.Samples`); `MultiProjectDemo.*` keep their names |
| Solution | `winforms-mvp.sln` | `Kibinago.sln` |
| Namespaces | `WinformsMVP.*` (`WinformsMVP.MVP.Presenters`, `WinformsMVP.Services`, `WinformsMVP.Common`, `WinformsMVP.Logging`, `WinformsMVP.Testing` …) | `Kibinago.*` with the same structure (`Kibinago.MVP.Presenters`, `Kibinago.Services` …) |
| API names carrying the old name | `AddWinformsMVP`, `UseWinformsMVP`, `WinformsMvpBuilder` | `AddKibinago`, `UseKibinago`, `KibinagoBuilder` |
| API names saying "MVP" (the pattern, not the product) | `MvpApplication`, `MvpCloseReason` (internal), `MvpSymbols` (internal) | unchanged |
| Analyzer diagnostic IDs | `MVP001`…`MVP017` | unchanged (they name the pattern's rules; users may have suppressions) |
| Templates | package `WinformsMVP.Templates`; short names `winformsmvp`, `winformsmvp-tests`, `mvp-window`, `mvp-control`; option `--mvp-version` | package `Kibinago.Templates`; `kibinago`, `kibinago-tests`, `mvp-window`, `mvp-control` (unchanged: they make MVP screens); option `--kibinago-version`; identities `Kibinago.Templates.*` |
| Template package version | `Version="*-*"` by default | **default = the template package's own version** (injected at pack time), so a template always pulls the packages it was released with; `--kibinago-version` overrides |
| Docs | README, wiki, CLAUDE.md, FEATURES, templates' guidance | product called "Kibinago" (subtitle "WinForms MVP framework"); install / using / API names updated; CHANGELOG BREAKING entry with an old → new table |
| History | `docs/superpowers/*`, `docs/archive/*`, `.claude/handoffs`, CHANGELOG entries of released versions | unchanged (records of their time) |
| CI / release | `.github/workflows/ci.yml`, `release.yml` (paths, pack list, analyzer check `analyzers/dotnet/cs/WinformsMVP.Analyzers.dll`) | Kibinago paths; analyzer check on `Kibinago.Analyzers.dll` |
| Wiki page names | `Reference-*.md`, `HowTo-*.md` | unchanged |

### Also in this change (from the same onboarding finding)
- `tests/templates/verify-templates.sh`: generate with the default version (no `--mvp-version` override) against
  the local feed, so the "template pulls its own version" rule is tested.
- README install steps: no `*-*`; the `nuget add source` example writes to the solution's `nuget.config`
  (`--configfile`), matching the templates' guidance.

## Not in this change
- Renaming the GitHub repository / wiki URL.
- Publishing to nuget.org and reserving the `Kibinago.` prefix (only possible once published there).
- The other onboarding findings (test template only on Windows, generated code not using the newest idioms,
  no XML docs in packages, MVP008 on fake views, several "recommended" ways in docs, …) — separate PRs after this.
- Deleting the old `WinformsMVP*` packages from GitHub Packages (the owner decides later; they stay as history).

## How (mechanical, test-guarded)
1. `git mv` the project folders / csproj files / solution; update `ProjectReference`s, `InternalsVisibleTo`,
   `Directory.Build.props`, CI paths.
2. Replace namespaces `WinformsMVP.` → `Kibinago.` (and `namespace WinformsMVP` / `using WinformsMVP`), including
   the analyzers' metadata names (`MvpSymbols`: `"WinformsMVP.MVP.Presenters.PresenterBase`1"` etc.) and analyzer
   test sources.
3. Rename the three API names; templates (folders' content, `template.json` identities / short names / symbols,
   generated code, the pack-time version default).
4. Docs.
5. Verify: `dotnet build Kibinago.sln` (0 warnings), analyzer tests, the net10 harness, `verify-templates.sh`,
   then CI (net48 tests + template run). A grep for `WinformsMVP` outside the history folders must find nothing
   except deliberate mentions (CHANGELOG mapping, "formerly WinformsMVP" in README).
6. codex review; owner approves; merge; release `v1.0.0-preview.9` (first Kibinago packages).
7. Local sample (`consultation-record-sample`): `$(MvpRoot)` project references and `using`s updated; tests pass.

## Risks
- Hidden string references (reflection by type name, analyzer metadata names, `InternalsVisibleTo`, resources):
  caught by tests and the grep.
- Template symbol renames break the template tests' scripts: updated in the same PR.
- Users following old links / old package names: none yet (no users); README says "formerly WinformsMVP".
