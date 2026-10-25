#!/usr/bin/env bash
# Instantiates every Kibinago template against locally packed packages and builds the result
# for each target framework, then runs the generated tests (net10 on any OS; net472 with RUN_TESTS=1, Windows).
#
#   tests/templates/verify-templates.sh            # Linux / macOS / Windows
#   RUN_TESTS=1 tests/templates/verify-templates.sh # also the net472 tests (Windows)
set -euo pipefail

# Lets MSBuild evaluate *-windows projects on non-Windows hosts (also needed for the item
# templates' RootNamespace lookup).
export EnableWindowsTargeting=true

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
WORK="$(mktemp -d)"
VERSION="0.0.0-templatetest"
FEED="$WORK/feed"
HIVE="$WORK/hive"
# Private package cache: every run packs the same version, and a shared ~/.nuget/packages would
# keep serving the package from an earlier run.
export NUGET_PACKAGES="$WORK/packages"
# nuget.config is read by dotnet itself: give it a native path (Git Bash on Windows uses /tmp/...).
FEED_NATIVE="$(cygpath -w "$FEED" 2>/dev/null || echo "$FEED")"
[ "${KEEP:-0}" = "1" ] && echo "Keeping $WORK" || trap 'rm -rf "$WORK"' EXIT

echo "== Packing into $FEED"
for project in src/Kibinago.Core/Kibinago.Core.csproj src/Kibinago/Kibinago.csproj src/Kibinago.Testing/Kibinago.Testing.csproj templates/Kibinago.Templates.csproj; do
  dotnet pack "$ROOT/$project" -c Release -p:Version="$VERSION" -o "$FEED" -nologo -v quiet
done

dotnet new install "$FEED/Kibinago.Templates.$VERSION.nupkg" --debug:custom-hive "$HIVE" >/dev/null

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
    <add key="local" value="$FEED_NATIVE" />
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
  </packageSources>
</configuration>
NUGET

  # No --kibinago-version: the templates must default to the packages they were packed with.
  new kibinago -n DemoApp -o "$dir/DemoApp" --framework "$framework" --no-restore
  grep -q "Include=\"Kibinago\" Version=\"$VERSION\"" "$dir/DemoApp/DemoApp.csproj"
  # Item templates read the project's RootNamespace, which needs a restored project
  # (a real `dotnet new kibinago` restores automatically).
  dotnet restore "$dir/DemoApp/DemoApp.csproj" -nologo -v quiet
  (
    cd "$dir/DemoApp"
    new mvp-window -n About
    new mvp-window -n OrderEdit --param-type int --validation
    new mvp-control -n CustomerPanel
    mkdir Dialogs && cd Dialogs && new mvp-window -n Confirm --namespace DemoApp.Dialogs
  )
  new kibinago-tests -n DemoApp.Tests -o "$dir/DemoApp.Tests" --framework "$framework" \
      --app-project ../DemoApp/DemoApp.csproj
  grep -q "Version=\"$VERSION\"" "$dir/DemoApp.Tests/DemoApp.Tests.csproj"

  # Item templates take the project's root namespace unless --namespace is given.
  grep -q "^namespace DemoApp$" "$dir/DemoApp/OrderEditPresenter.cs"
  grep -q "^namespace DemoApp$" "$dir/DemoApp/CustomerPanelPresenter.cs"
  grep -q "^namespace DemoApp.Dialogs$" "$dir/DemoApp/Dialogs/ConfirmPresenter.cs"
  grep -q "WindowPresenterBase<IOrderEditView, int>" "$dir/DemoApp/OrderEditPresenter.cs"
  grep -q 'WithQualifier("OrderEdit")' "$dir/DemoApp/OrderEditActions.cs"
  # Program.cs: high-DPI setup only where the API exists.
  if [ "$framework" = "net472" ]; then
    ! grep -q "SetHighDpiMode" "$dir/DemoApp/Program.cs"
  else
    grep -q "SetHighDpiMode" "$dir/DemoApp/Program.cs"
  fi
  [ -f "$dir/DemoApp/OrderEditModel.cs" ] && [ ! -f "$dir/DemoApp/AboutModel.cs" ]
  [ -f "$dir/DemoApp/.gitignore" ]
  # Current idioms in the generated code.
  grep -q "SubscribeViewEvent(h => View.DataChanged" "$dir/DemoApp/OrderEditPresenter.cs"
  grep -q "View.FocusError(first.Field)" "$dir/DemoApp/OrderEditPresenter.cs"
  grep -q "AttachChild(new CustomerPanelPresenter(), View.CustomerPanel)" "$dir/DemoApp/CustomerPanelPresenter.cs"

  # -warnaserror: generated code must compile without warnings, MVP analyzer rules included.
  dotnet build "$dir/DemoApp.Tests/DemoApp.Tests.csproj" -warnaserror -nologo -v quiet
  # The generated tests need no Windows Desktop runtime: net10 runs them on any OS; net472 needs Windows.
  if [ "$framework" = "net10.0-windows" ] || [ "${RUN_TESTS:-0}" = "1" ]; then
    dotnet test "$dir/DemoApp.Tests/DemoApp.Tests.csproj" --no-build -nologo
  fi
done

echo "== All templates OK"
