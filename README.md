# WinForms MVP Framework

[![.NET Framework](https://img.shields.io/badge/.NET%20Framework-4.7.2%2B-blue.svg)](https://dotnet.microsoft.com/download/dotnet-framework)
[![.NET](https://img.shields.io/badge/.NET-10%20(Windows)-blue.svg)](https://dotnet.microsoft.com/download/dotnet)
[![License](https://img.shields.io/badge/license-MIT-green.svg)](LICENSE.txt)

.NET Framework / .NET (Windows) 上で WinForms アプリケーションを **構築・保守する** チームのための、**Model-View-Presenter (MVP)** フレームワーク。Presenter から WinForms 型を完全に切り離し、**UI スレッドなしで単体テストできる** クリーンな関心の分離をもたらします。

**こんな課題に効きます:**

- ビジネスロジックがフォームのコードビハインドに癒着していて、単体テストが書けない
- `MessageBox.Show()` やダイアログ呼び出しが UI と密結合していて、モックできない
- 画面遷移・ウィンドウのクローズ処理・dirty 判定が場当たり的で、再利用も検証も難しい

WPF 風のコマンドバインドとサービス抽象 (`IMessageService` / `IDialogProvider` …) でこれらを解きほぐします。コアライブラリ (`WinformsMVP.Core`, `netstandard2.0`) は **WinForms 非参照** で、外部依存は `System.ComponentModel.Annotations` (DataAnnotations) のみ — Presenter のコードはコンパイル時点で WinForms 型に触れられません。WinForms 実装 (`WinformsMVP`) は .NET Framework 4.7.2 以降と .NET 10 (Windows) をターゲットにします。

> **ドキュメントは [GitHub Wiki](https://github.com/VoidAxon/winforms-mvp/wiki) を参照してください。**
> 本 README はプロジェクトの入口です。詳細な使い方・設計指針・サンプル解説は Wiki に集約されています。

---

## 主な特徴

- 🎮 **ViewAction システム** — WPF の `ICommand` 相当を WinForms に。型安全なアクションキー、宣言的バインド、`CanExecute` による自動 Enabled 制御
- ⏳ **async/await 対応** — `async Task` のアクションハンドラ (実行中は自動で無効化、失敗はログ)、ウィンドウを止めない `OnInitializeAsync`、閉じる前に保存を待てる `CanCloseAsync`、ウィンドウを閉じるとキャンセルされる `Lifetime` トークン
- 🏗️ **クリーンな MVP 分離** — Presenter は WinForms 型を一切知らない。View インターフェイスにも `Button` / `TextBox` を露出させない
- 🪟 **ウィンドウナビゲーション** — Modal / 非 Modal、パラメータ付き、Fluent API、基底 `RequestClose` による結果返却
- 🔁 **二方向ウィンドウクローズモデル** — Push (Presenter 主導) と Pull (フレームワーク主導) を明確に分離。dirty 判定の単一情報源
- 🚀 **サービス抽象化レイヤー** — `IMessageService` / `IDialogProvider` / `IFileService` で `MessageBox.Show()` から脱却。完全モック可能
- 📊 **構造化ロギング** — `Microsoft.Extensions.Logging` 互換 API の自社抽象。BCL のみ依存 (`WinformsMVP.Core`)
- ✅ **入力検証とエラー表示** — Model の DataAnnotations + Presenter のルール (`Validation.SetError`) を `Validation` で一元管理。View は `IValidatableView` を実装して `ErrorProviderDisplay` でフィールドとコントロールを対応付けるだけ。エラーはフィールドを離れた後、または送信 (`Validation.Check`) 後に表示
- 🔄 **ChangeTracker** — 編集/キャンセル用の堅牢な変更追跡。`IRevertibleChangeTracking` 実装、深いコピー対応
- 📡 **EventAggregator** — Presenter 横断の pub/sub。購読は返されたトークンを Dispose するまで有効 (`.DisposeWith(Disposables)`)、UI スレッドへの自動マーシャリング、アナライザ MVP017 がトークンの破棄忘れを検出
- 🎨 **柔軟な DI** — Service Locator / Constructor Injection / Hybrid の 3 パターンに対応。`Microsoft.Extensions.DependencyInjection` 連携用の `WinformsMVP.DependencyInjection` パッケージも同梱
- 🧪 **テストファースト設計** — Presenter は UI スレッド不要で単体テスト可能

---

## クイック例

```csharp
// 1. View インターフェイス（UI 型を一切公開しない）
public interface IUserEditorView : IWindowView
{
    string UserName { get; set; }
    ViewActionBinder ActionBinder { get; }
}

// 2. Presenter（WinForms 知識ゼロ）
public class UserEditorPresenter : WindowPresenterBase<IUserEditorView>
{
    protected override void RegisterViewActions()
    {
        Dispatcher.Register(StandardActions.Save, OnSave);
    }

    private void OnSave()
    {
        SaveUser(View.UserName);
        Messages.ShowInfo("Saved!");
    }
}

// 3. Form 実装（UI 要素は内部に閉じ込める）
public class UserEditorForm : Form, IUserEditorView
{
    private ViewActionBinder _binder;
    public ViewActionBinder ActionBinder => _binder;

    public UserEditorForm()
    {
        InitializeComponent();
        _binder = new ViewActionBinder();
        _binder.Add(StandardActions.Save, _saveButton);
    }

    public string UserName
    {
        get => _nameTextBox.Text;
        set => _nameTextBox.Text = value;
    }
}

// 4. 起動
var register = new ViewMappingRegister();
register.RegisterFromAssembly(Assembly.GetExecutingAssembly());

var navigator = new WindowNavigator(register);
navigator.ShowWindow(new UserEditorPresenter());
```

---

## インストール

パッケージは **GitHub Packages** に公開されています。GitHub Packages は、public リポジトリであっても NuGet の復元に認証が必要です。以下の2つの方法のいずれかを選んでください。

### 方法A — GitHub Packages フィード（継続利用におすすめ）

1. `read:packages` スコープを持つ GitHub Personal Access Token（classic）を作成します。
2. ソリューションの隣に `nuget.config` を置きます:

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

   PAT はコミットせず、環境変数や `dotnet nuget add source ... --username ... --password ...` で渡すことを推奨します。

3. インストールします（プレビュー版しか無い間は `--prerelease` が必須です）:

   ```bash
   dotnet add package WinformsMVP --prerelease
   dotnet add package WinformsMVP.DependencyInjection --prerelease
   ```

   `WinformsMVP` は `WinformsMVP.Core` に依存しているため、Core と同梱のアナライザーは自動で入ります。Presenter だけを別プロジェクトに切り出す場合は、そのプロジェクトで `WinformsMVP.Core` のみを参照すると WinForms 型を使えなくなります (`dotnet add package WinformsMVP.Core --prerelease`)。

### 方法B — Releases から .nupkg をダウンロード（認証不要）

1. リポジトリの **Releases** ページを開き、目的のバージョンの `.nupkg` ファイルをダウンロードします。
2. ローカルフォルダに置き、ソースとして登録してインストールします (`WinformsMVP` は `WinformsMVP.Core` に依存するので、両方の `.nupkg` を同じフォルダに置いてください):

   ```bash
   dotnet nuget add source C:\path\to\folder --name winformsmvp-local
   dotnet add package WinformsMVP --prerelease
   ```

### プロジェクトテンプレート (`WinformsMVP.Templates`)

新規アプリはテンプレートから始めるのがおすすめです。テンプレートパッケージも同じ方法 (GitHub Packages フィード、またはダウンロードした `.nupkg`) で入手できます:

```bash
dotnet new install WinformsMVP.Templates::<version>                   # フィード経由。プレビュー期間中はバージョン指定が必須 (例: ::1.0.0-preview.1)
dotnet new install C:\path\to\folder\WinformsMVP.Templates.<version>.nupkg   # ローカルの .nupkg から
```

生成されるプロジェクトは `WinformsMVP` パッケージを参照するので、上記 A / B どちらかのソースを `nuget.config` に設定しておいてください。4 行で動くアプリ + ウィンドウ + テストができます:

```bash
dotnet new winformsmvp -n MyApp                 # アプリ (net10.0-windows。--framework net472 も可)
cd MyApp && dotnet new mvp-window -n OrderEdit  # View インターフェイス + Actions + Presenter + Form
cd .. && dotnet new winformsmvp-tests -n MyApp.Tests --app-project ../MyApp/MyApp.csproj
dotnet build MyApp.Tests && dotnet run --project MyApp
```

テンプレート一覧とオプションは [Wiki: テンプレートを使う](https://github.com/VoidAxon/winforms-mvp/wiki/HowTo-Use-Templates) を参照してください。

---

## はじめに

1. リポジトリをクローン

   ```bash
   git clone https://github.com/VoidAxon/winforms-mvp.git
   ```

2. ソリューションをビルド

   ```bash
   dotnet build winforms-mvp.sln
   ```

3. サンプルアプリを実行

   ```bash
   dotnet run --project samples/WinformsMVP.Samples/WinformsMVP.Samples.csproj
   ```

4. テストを実行 (テストプロジェクトは `net48` ターゲットのため、実行には Windows (または mono) が必要です。ビルド自体は `EnableWindowsTargeting` により Linux / macOS でも可能)

   ```bash
   dotnet test tests/WinformsMVP.Samples.Tests/WinformsMVP.Samples.Tests.csproj
   ```

---

## ドキュメント

すべてのドキュメントは **[Wiki](https://github.com/VoidAxon/winforms-mvp/wiki)** にあります。

### 入門

- [はじめての方へ (Getting Started)](https://github.com/VoidAxon/winforms-mvp/wiki/Getting-Started) — 5 分で動かす最小サンプル
- [テンプレートを使う](https://github.com/VoidAxon/winforms-mvp/wiki/HowTo-Use-Templates) — `dotnet new` でアプリ・ウィンドウ・UserControl・テストを生成
- [チュートリアル: 最初のアプリを作る](https://github.com/VoidAxon/winforms-mvp/wiki/Tutorial-Building-Your-First-App)

### 設計思想 (Concepts)

- [MVP パターンとは](https://github.com/VoidAxon/winforms-mvp/wiki/Concept-MVP-Pattern)
- [アーキテクチャ概観](https://github.com/VoidAxon/winforms-mvp/wiki/Concept-Architecture-Overview)
- [ウィンドウクローズモデル](https://github.com/VoidAxon/winforms-mvp/wiki/Concept-Window-Closing-Model)

### リファレンス (Reference)

- [Presenter 基底クラス](https://github.com/VoidAxon/winforms-mvp/wiki/Reference-Presenter-Base-Classes)
- [ViewAction システム](https://github.com/VoidAxon/winforms-mvp/wiki/Reference-ViewAction-System)
- [WindowNavigator](https://github.com/VoidAxon/winforms-mvp/wiki/Reference-WindowNavigator)
- [Platform Services](https://github.com/VoidAxon/winforms-mvp/wiki/Reference-Platform-Services)
- [Data Binding 拡張メソッド](https://github.com/VoidAxon/winforms-mvp/wiki/Reference-Data-Binding)
- [Logging](https://github.com/VoidAxon/winforms-mvp/wiki/Reference-Logging)
- [ChangeTracker](https://github.com/VoidAxon/winforms-mvp/wiki/Reference-ChangeTracker)
- [EventAggregator](https://github.com/VoidAxon/winforms-mvp/wiki/Reference-EventAggregator)
- [Dependency Injection](https://github.com/VoidAxon/winforms-mvp/wiki/Reference-DependencyInjection)

### 設計ルール

- [MVP 設計ルール (全 17 条)](https://github.com/VoidAxon/winforms-mvp/wiki/Design-Rules)

---

## 対応環境

| | 対応バージョン |
|---|---|
| ターゲットフレームワーク | `WinformsMVP.Core`: `netstandard2.0` / `WinformsMVP`・`WinformsMVP.DependencyInjection`: `net472` + `net10.0-windows` (.NET Framework 4.7.2 以降 / .NET 10 Windows) |
| 言語 | コンパイル済み DLL を配布するため利用側の C# バージョンは不問（ライブラリ本体は `LangVersion=latest` でビルド） |
| プロジェクト形式 | SDK-style `.csproj` |
| テストフレームワーク | xUnit 2.9 |
| SDK / IDE | .NET 10 SDK (`net10.0-windows` ターゲットのビルドに必要)。Visual Studio、JetBrains Rider、VS Code |

パッケージは 4 つです:

| パッケージ | ターゲット | 内容 |
|---|---|---|
| `WinformsMVP.Core` | `netstandard2.0` | Presenter 基底クラス、ViewAction ディスパッチャ、EventAggregator、ChangeTracker、検証、ロギング、サービスインターフェイス。WinForms 非参照 (外部依存は `System.ComponentModel.Annotations` のみ)。MVP アナライザーを同梱 |
| `WinformsMVP` | `net472`; `net10.0-windows` | WinForms 実装 (WindowNavigator、ViewActionBinder、ダイアログ、トースト、MessageService、View マッピング)。`WinformsMVP.Core` に依存し、アナライザーも推移的に有効 |
| `WinformsMVP.DependencyInjection` | `net472`; `net10.0-windows` | `Microsoft.Extensions.DependencyInjection` 連携 (オプション) |
| `WinformsMVP.Templates` | (テンプレート) | `dotnet new` テンプレート: `winformsmvp` (アプリ) / `mvp-window` / `mvp-control` / `winformsmvp-tests` |

名前空間はアセンブリをまたいで変わりません。`net40` のサポートは終了しました (最低 .NET Framework 4.7.2)。
`Microsoft.Extensions.Logging` 連携については出荷パッケージは無く、M.E.L. の `ILoggerFactory` を框架の `ILoggerFactory` に橋渡しする **~30 行のアダプタを自分で書きます** ([Logging](https://github.com/VoidAxon/winforms-mvp/wiki/Reference-Logging) 参照、全ターゲットで利用可)。

---

## プロジェクト構造

```
winforms-mvp/
├── src/
│   ├── WinformsMVP.Core/                     プラットフォーム非依存コア (netstandard2.0、WinForms 非参照)
│   ├── WinformsMVP/                          WinForms 実装 (net472;net10.0-windows)
│   ├── WinformsMVP.DependencyInjection/      M.E.DI 連携 (オプション、net472;net10.0-windows)
│   └── WinformsMVP.Analyzers/                Roslyn アナライザー (WinformsMVP.Core パッケージに同梱)
├── samples/
│   ├── WinformsMVP.Samples/                  サンプル WinForms アプリ
│   └── MultiProjectDemo.*/                   複数プロジェクト DI 構成のデモ
├── tests/
│   └── WinformsMVP.Samples.Tests/            xUnit テストプロジェクト (net48)
├── docs/archive/                             過去のレポート類 (参照用)
└── README.md                                 このファイル
```

---

## ライセンス

このプロジェクトは [MIT ライセンス](LICENSE.txt) のもとで公開されています。

---

## コントリビュート

バグ報告・機能提案・プルリクエストを歓迎します。
GitHub Issues / Pull Requests からどうぞ。

リリース手順（メンテナ向け）は [HowTo: リリースする](wiki/HowTo-Release.md) を参照してください。
