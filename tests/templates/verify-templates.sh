#!/usr/bin/env bash
# Instantiates every WinformsMVP template against locally packed packages and builds the result
# for each target framework; with RUN_TESTS=1 (Windows only) also runs the generated tests.
#
#   tests/templates/verify-templates.sh            # build only (works on Linux / macOS / Windows)
#   RUN_TESTS=1 tests/templates/verify-templates.sh
set -euo pipefail

# Lets MSBuild evaluate *-windows projects on non-Windows hosts (also needed for the item
# templates' RootNamespace lookup).
export EnableWindowsTargeting=true

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
WORK="$(mktemp -d)"
VERSION="0.0.0-templatetest"
FEED="$WORK/feed"
HIVE="$WORK/hive"
[ "${KEEP:-0}" = "1" ] && echo "Keeping $WORK" || trap 'rm -rf "$WORK"' EXIT

echo "== Packing into $FEED"
for project in src/WinformsMVP.Core/WinformsMVP.Core.csproj src/WinformsMVP/WinformsMVP.csproj templates/WinformsMVP.Templates.csproj; do
  dotnet pack "$ROOT/$project" -c Release -p:Version="$VERSION" -o "$FEED" -nologo -v quiet
done

dotnet new install "$FEED/WinformsMVP.Templates.$VERSION.nupkg" --debug:custom-hive "$HIVE" >/dev/null

new() { dotnet new "$@" --debug:custom-hive "$HIVE" >/dev/null; }

for framework in net10.0-windows net472; do
  echo "== $framework"
  dir="$WORK/$framework"
  mkdir -p "$dir"
  cat > "$dir/nuget.config" <<NUGET
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <add key="local" value="$FEED" />
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
  </packageSources>
</configuration>
NUGET

  new winformsmvp -n DemoApp -o "$dir/DemoApp" --framework "$framework" --mvp-version "$VERSION" --no-restore
  # Item templates read the project's RootNamespace, which needs a restored project
  # (a real `dotnet new winformsmvp` restores automatically).
  dotnet restore "$dir/DemoApp/DemoApp.csproj" -nologo -v quiet
  (
    cd "$dir/DemoApp"
    new mvp-window -n About
    new mvp-window -n OrderEdit --param-type int --validation
    new mvp-control -n CustomerPanel
    mkdir Dialogs && cd Dialogs && new mvp-window -n Confirm --namespace DemoApp.Dialogs
  )
  new winformsmvp-tests -n DemoApp.Tests -o "$dir/DemoApp.Tests" --framework "$framework" \
      --mvp-version "$VERSION" --app-project ../DemoApp/DemoApp.csproj

  # Item templates take the project's root namespace unless --namespace is given.
  grep -q "^namespace DemoApp$" "$dir/DemoApp/OrderEditPresenter.cs"
  grep -q "^namespace DemoApp$" "$dir/DemoApp/CustomerPanelPresenter.cs"
  grep -q "^namespace DemoApp.Dialogs$" "$dir/DemoApp/Dialogs/ConfirmPresenter.cs"
  grep -q "WindowPresenterBase<IOrderEditView, int>" "$dir/DemoApp/OrderEditPresenter.cs"
  [ -f "$dir/DemoApp/OrderEditModel.cs" ] && [ ! -f "$dir/DemoApp/AboutModel.cs" ]

  # -warnaserror: generated code must compile without warnings, MVP analyzer rules included.
  dotnet build "$dir/DemoApp.Tests/DemoApp.Tests.csproj" -warnaserror -nologo -v quiet
  if [ "${RUN_TESTS:-0}" = "1" ]; then
    dotnet test "$dir/DemoApp.Tests/DemoApp.Tests.csproj" --no-build -nologo
  fi
done

echo "== All templates OK"
