# HowTo: テンプレートを使う (`dotnet new`)

このページでは、`WinformsMVP.Templates` パッケージの `dotnet new` テンプレートで、アプリ・ウィンドウ・UserControl・テストプロジェクトを生成する方法を示します。
新規アプリは **テンプレートから始めるのが推奨** です。生成されるコードは MVP 設計ルール (アナライザー含む) を守り、警告ゼロでビルドできる状態になっています。

| テンプレート | 短縮名 | 種類 | 生成物 |
|------------|-------|------|-------|
| WinformsMVP Application | `winformsmvp` | プロジェクト | `Program.Main` (サービス登録・View 登録) + メインウィンドウ |
| WinformsMVP Window | `mvp-window` | アイテム | View インターフェイス + Actions + Presenter + Form |
| WinformsMVP UserControl | `mvp-control` | アイテム | View インターフェイス + Actions + Presenter + UserControl |
| WinformsMVP Presenter Tests | `winformsmvp-tests` | プロジェクト | xUnit テストプロジェクト (`WinformsMVP.Testing` 参照済み) + サンプルテスト |

---

## 目次

- [インストール](#インストール)
- [クイックスタート](#クイックスタート)
- [winformsmvp: アプリケーション](#winformsmvp-アプリケーション)
- [mvp-window: ウィンドウ](#mvp-window-ウィンドウ)
- [mvp-control: UserControl](#mvp-control-usercontrol)
- [winformsmvp-tests: Presenter テスト](#winformsmvp-tests-presenter-テスト)
- [Designer と WFO1000](#designer-と-wfo1000)
- [トラブルシューティング](#トラブルシューティング)
- [関連ページ](#関連ページ)

---

## インストール

テンプレートパッケージは他のパッケージと同じく **GitHub Packages** (または Releases の `.nupkg`) で配布しています。入手方法は [README のインストール節](https://github.com/VoidAxon/winforms-mvp#インストール) を参照してください。

```bash
# フィード経由 (GitHub Packages を nuget.config に設定済みの場合)。
# プレビュー版しか公開されていない間は、バージョン指定が必要です
# (バージョンなしの install は正式版だけを探すため見つかりません)。
dotnet new install WinformsMVP.Templates::<version>        # 例: ::1.0.0-preview.1

# 正式版の公開後は最新版をそのままインストールできます
dotnet new install WinformsMVP.Templates

# ダウンロードした / ローカルでパックした .nupkg から
dotnet new install C:\path\to\WinformsMVP.Templates.1.0.0-preview.1.nupkg
```

確認とアンインストール:

```bash
dotnet new list mvp                       # インストール済みテンプレートを表示
dotnet new uninstall WinformsMVP.Templates
```

> **ポイント:** 生成されたプロジェクトは `WinformsMVP` パッケージを参照します。復元できるよう、GitHub Packages フィードまたは `.nupkg` を置いたローカルフォルダ (`WinformsMVP` と `WinformsMVP.Core` の両方) を `nuget.config` のソースに登録しておいてください。

---

## クイックスタート

```bash
dotnet new winformsmvp -n MyApp                     # アプリ (自動で restore)
cd MyApp
dotnet new mvp-window -n OrderEdit --param-type int --validation
cd ..
dotnet new winformsmvp-tests -n MyApp.Tests --app-project ../MyApp/MyApp.csproj
dotnet test MyApp.Tests
dotnet run --project MyApp
```

---

## winformsmvp: アプリケーション

```bash
dotnet new winformsmvp -n MyApp [--framework net10.0-windows|net472] [--mvp-version <version>] [--no-restore]
```

| オプション | 既定値 | 説明 |
|-----------|-------|------|
| `-f`, `--framework` | `net10.0-windows` | ターゲット。`net10.0-windows` (.NET 10) または `net472` (.NET Framework 4.7.2) |
| `--mvp-version` | `*-*` | 参照する `WinformsMVP` のバージョン。既定は **プレリリースを含む最新** |
| `--no-restore` | (なし) | 生成後の自動 `dotnet restore` を行わない |

生成されるファイル:

| ファイル | 内容 |
|---------|------|
| `MyApp.csproj` | WinForms (`UseWindowsForms`) + `WinformsMVP` の `PackageReference` |
| `Program.cs` | `ServiceLocator.Configure(...)` (業務サービスの登録場所)、`RegisterFromAssembly` で View を自動登録、`new MainPresenter().Connect(mainForm)` → `Application.Run` |
| `IMainView.cs` / `MainActions.cs` | メインウィンドウの View インターフェイスと ViewAction (`SayHello` / `Exit`) |
| `MainPresenter.cs` | `WindowPresenterBase<IMainView>`。`Messages.ShowInfo` と `RequestClose` の例 |
| `MainForm.cs` / `MainForm.Designer.cs` | Designer で編集できる Form。`ViewActionBinder` でボタンをアクションに結び付ける |

メインウィンドウは `Program.Main` で自分で作るため、`Connect` による **Adopted ホスティング** で Presenter をつなぎます。それ以外のウィンドウは Presenter から `Navigator` で開きます ([ウィンドウクローズモデル](Concept-Window-Closing-Model) 参照)。

`net10.0-windows` では `Application.SetHighDpiMode(HighDpiMode.SystemAware)` も呼ばれます (`net472` には API が無いため生成されません)。

---

## mvp-window: ウィンドウ

**アプリのプロジェクトフォルダ内で** 実行します。

```bash
dotnet new mvp-window -n OrderEdit [--param-type int] [--validation] [--namespace MyApp.Orders]
```

| オプション | 既定値 | 説明 |
|-----------|-------|------|
| `--param-type` | (なし) | ウィンドウを開くときのパラメータ型。指定すると `WindowPresenterBase<IOrderEditView, int>` と `OnInitialize(int parameters)` になる |
| `--validation` | `false` | `OrderEditModel` (DataAnnotations) を追加し、View を `IValidatableView` にして `ErrorProviderDisplay` でエラーをフィールドの横に表示 |
| `-ns`, `--namespace` | プロジェクトの `RootNamespace` | 生成ファイルの名前空間。サブフォルダに生成するときに指定する |

生成されるファイル: `IOrderEditView.cs` / `OrderEditActions.cs` / `OrderEditPresenter.cs` / `OrderEditForm.cs` / `OrderEditForm.Designer.cs` (`--validation` 時は `OrderEditModel.cs` も)。

- **Ok** は非同期アクション (`async Task OnOkAsync(CancellationToken)`) です。実行中は自動で無効化され、ダブルクリックは無視、ウィンドウを閉じるとキャンセルされます。保存処理を書いたら `RequestClose(InteractionStatus.Ok)` で閉じます (`RequestClose` も X と同じ `CanClose` を通るので、変更追跡をしているなら保存後 `RequestClose` の前に `AcceptChanges()` します)。`--validation` 時は `Validation.Check(model)` で全エラーを表示して不正なら中断します。
- **Cancel** は `RequestClose(InteractionStatus.Cancel)` で閉じます。X と同じく `CanClose` を通るので、未保存の確認は `CanClose` のオーバーライドに書けば「閉じる」ボタンでも出ます。確認なしに破棄して閉じるボタンにするなら、`RequestClose` の前に `AcceptChanges()` します ([HowTo: ウィンドウクローズを扱う](HowTo-Handle-Window-Closing#閉じるボタン--x-と同じ確認を出す))。
- `Program.Main` の `RegisterFromAssembly` が新しい Form を自動登録するので、すぐに開けます:

```csharp
// 別の Presenter から
Navigator.For(new OrderEditPresenter()).ShowAsModal();
// --param-type int の場合
Navigator.For(new OrderEditPresenter()).WithParam(orderId).ShowAsModal();
```

> **ポイント:** 名前空間の既定値はプロジェクトの `RootNamespace` から読み取ります。これには **restore 済みのプロジェクト** が必要です (`dotnet new winformsmvp` は自動で restore します。`--no-restore` で作った場合は先に `dotnet restore`)。サブフォルダ (例: `Dialogs/`) に生成するときは `--namespace MyApp.Dialogs` のように明示してください。

---

## mvp-control: UserControl

```bash
dotnet new mvp-control -n CustomerPanel [--namespace MyApp.Customers]
```

生成されるファイル: `ICustomerPanelView.cs` / `CustomerPanelActions.cs` / `CustomerPanelPresenter.cs` (`ControlPresenterBase<ICustomerPanelView>`) / `CustomerPanelControl.cs` / `CustomerPanelControl.Designer.cs`。

Presenter は `OnInitializeAsync` と `Refresh` アクションで同じ非同期読み込み処理を実行する例になっています。親の Form (View コード) で Control を配置し、Presenter を接続します:

```csharp
new CustomerPanelPresenter().Connect(customerPanelControl);
```

Presenter は Control と一緒に破棄されます。業務サービスが必要なら Presenter のコンストラクタに追加してください ([Presenter 基底クラス](Reference-Presenter-Base-Classes) 参照)。

---

## winformsmvp-tests: Presenter テスト

```bash
dotnet new winformsmvp-tests -n MyApp.Tests [--framework net10.0-windows|net472] [--mvp-version <version>] [--app-project ../MyApp/MyApp.csproj]
```

| オプション | 既定値 | 説明 |
|-----------|-------|------|
| `-f`, `--framework` | `net10.0-windows` | アプリと同じターゲットを指定する |
| `--mvp-version` | `*-*` | 参照する `WinformsMVP` / `WinformsMVP.Testing` のバージョン |
| `--app-project` | (なし) | テスト対象アプリへの相対パス。指定すると `ProjectReference` が追加される |

生成されるファイル:

- `MyApp.Tests.csproj` — xUnit 2.9 + `WinformsMVP` + `WinformsMVP.Testing` (`PresenterTestHost`、`TestServices`、モック View 用の `CapturingActionBinder`、フレームワークサービスのフェイク。バージョンは `--mvp-version` と同じ)
- `ExamplePresenterTests.cs` — `TestServices.CreateFakes()` → `PresenterTestHost.Start(fakes.Provider, presenter, view)` → `await host.Initialized` → `view.Binder.DispatchAsync(...)` → `host.CloseStatus` / `host.CloseResult` を検証する例。自分の Presenter と View に置き換えて使う

テストの書き方は [HowTo: Presenter をテストする](HowTo-Test-A-Presenter) を参照してください。

---

## Designer と WFO1000

.NET 9 以降の WinForms アナライザー **WFO1000** は、Form / UserControl の書き込み可能な public プロパティに `DesignerSerializationVisibility` などの指定を求め、無ければ**既定でビルドエラー**にします (Designer を使うかどうかに関係なく)。View インターフェイスを実装するプロパティはまさにこれに当たります。

- **Form** では実害はほぼありません (Designer は Form の基底クラスをインスタンス化するため、Form 自身が宣言したプロパティはシリアル化されない)。
- **UserControl** を別の Form に置くと、その Form の Designer が UserControl の読み取り可能な public プロパティを `.Designer.cs` に書き出します。こちらは属性が実際に効きます。

どちらにしてもエラーは解消する必要があります (既定の重大度が Error のため、アナライザーの DiagnosticSuppressor では抑止できません)。

```csharp
// View のデータであって Designer のプロパティではない: .Designer.cs に書き出させない
[DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
public string Title
{
    get => txtTitle.Text;
    set => txtTitle.Text = value;
}
```

テンプレートが生成する Form / UserControl はすでにこの属性を付けています。手書きの場合は、Visual Studio で WFO1000 の 1 件に `Ctrl+.` →「DesignerSerializationVisibility を追加」→「プロジェクト内のすべて修正」を選ぶと、WinForms 標準のコード修正で一括付与できます。属性を書きたくない場合は、View のプロパティを明示的なインターフェイス実装 (`string IOrderView.Title { get; set; }`) にすると WFO1000 の対象外になります。

---

## トラブルシューティング

**`dotnet new winformsmvp` の restore で `WinformsMVP` が見つからない**

`nuget.config` に GitHub Packages フィード (要 PAT) または `.nupkg` を置いたローカルフォルダが登録されていません。[README のインストール節](https://github.com/VoidAxon/winforms-mvp#インストール) を参照してください。プレビュー版のみの間は既定の `--mvp-version *-*` のままにしてください。

**`mvp-window` / `mvp-control` の名前空間が `MyApp` になる / プロジェクトと違う**

プロジェクトが restore されていないため `RootNamespace` を読めず、既定値 `MyApp` になっています。`dotnet restore` してから生成し直すか、`--namespace` を指定してください。

**テンプレートを変更した (メンテナ向け)**

`tests/templates/verify-templates.sh` がテンプレートをローカルフィードにパックし、両フレームワーク向けに全テンプレートを生成して名前空間・バリアントを検証し、`-warnaserror` でビルドします (`RUN_TESTS=1` で生成されたテストも実行、Windows のみ)。CI でも実行されます。

---

## 関連ページ

- [はじめに (Getting Started)](Getting-Started) — テンプレートを使わずに最小構成を手で組み立てる
- [チュートリアル: 最初のアプリを作る](Tutorial-Building-Your-First-App)
- [Presenter 基底クラス](Reference-Presenter-Base-Classes) — `WindowPresenterBase` / `ControlPresenterBase`
- [WindowNavigator](Reference-WindowNavigator) — 生成したウィンドウを開く
- [HowTo: フォーム入力を検証する](HowTo-Validate-Form-Input) — `--validation` で生成されるコードの詳細
- [HowTo: Presenter をテストする](HowTo-Test-A-Presenter)
- [HowTo: リリースする](HowTo-Release) — テンプレートパッケージの発行
