# WinForms MVP Framework

[![.NET Framework](https://img.shields.io/badge/.NET%20Framework-4.8-blue.svg)](https://dotnet.microsoft.com/download/dotnet-framework)
[![License](https://img.shields.io/badge/license-MIT-green.svg)](LICENSE.txt)

.NET Framework 上で WinForms アプリケーションを **構築・保守する** チームのための、**Model-View-Presenter (MVP)** フレームワーク。Presenter から WinForms 型を完全に切り離し、**UI スレッドなしで単体テストできる** クリーンな関心の分離をもたらします。

**こんな課題に効きます:**

- ビジネスロジックがフォームのコードビハインドに癒着していて、単体テストが書けない
- `MessageBox.Show()` やダイアログ呼び出しが UI と密結合していて、モックできない
- 画面遷移・ウィンドウのクローズ処理・dirty 判定が場当たり的で、再利用も検証も難しい

WPF 風のコマンドバインドとサービス抽象 (`IMessageService` / `IDialogProvider` …) でこれらを解きほぐします。コアライブラリは **外部依存ゼロ**。主ターゲットは `net48` ですが、`net40` でも動作するため、ランタイムを固定された企業内システムにも導入できます。

> **ドキュメントは [GitHub Wiki](https://github.com/VoidAxon/winforms-mvp/wiki) を参照してください。**
> 本 README はプロジェクトの入口です。詳細な使い方・設計指針・サンプル解説は Wiki に集約されています。

---

## 主な特徴

- 🎮 **ViewAction システム** — WPF の `ICommand` 相当を WinForms に。型安全なアクションキー、宣言的バインド、`CanExecute` による自動 Enabled 制御
- 🏗️ **クリーンな MVP 分離** — Presenter は WinForms 型を一切知らない。View インターフェイスにも `Button` / `TextBox` を露出させない
- 🪟 **ウィンドウナビゲーション** — Modal / 非 Modal、パラメータ付き、Fluent API、基底 `RequestClose` による結果返却
- 🔁 **二方向ウィンドウクローズモデル** — Push (Presenter 主導) と Pull (フレームワーク主導) を明確に分離。dirty 判定の単一情報源
- 🚀 **サービス抽象化レイヤー** — `IMessageService` / `IDialogProvider` / `IFileService` で `MessageBox.Show()` から脱却。完全モック可能
- 📊 **構造化ロギング** — `Microsoft.Extensions.Logging` 互換 API の自社抽象。BCL のみ依存で `net40` でも動作
- 🔄 **ChangeTracker** — 編集/キャンセル用の堅牢な変更追跡。`IRevertibleChangeTracking` 実装、深いコピー対応
- 📡 **EventAggregator** — 弱参照ベースの pub/sub。UI スレッドへの自動マーシャリング、コンパイル済みデリゲートで高速
- 🎨 **柔軟な DI** — Service Locator / Constructor Injection / Hybrid の 3 パターンに対応。`Microsoft.Extensions.DependencyInjection` 連携用の `WinformsMVP.DependencyInjection` パッケージも同梱
- 🧪 **テストファースト設計** — Presenter は UI スレッド不要で単体テスト可能

---

## クイック例

```csharp
// 1. View インターフェイス（UI 型を一切公開しない）
public interface IUserEditorView : IWindowView
{
    string UserName { get; set; }
    bool HasUnsavedChanges { get; }
    ViewActionBinder ActionBinder { get; }
}

// 2. Presenter（WinForms 知識ゼロ）
public class UserEditorPresenter : WindowPresenterBase<IUserEditorView>
{
    protected override void RegisterViewActions()
    {
        Dispatcher.Register(StandardActions.Save, OnSave,
            canExecute: () => View.HasUnsavedChanges);
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

    public bool HasUnsavedChanges => /* ... */;
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

### 方法B — Releases から .nupkg をダウンロード（認証不要）

1. リポジトリの **Releases** ページを開き、目的のバージョンの `.nupkg` ファイルをダウンロードします。
2. ローカルフォルダに置き、ソースとして登録してインストールします:

   ```bash
   dotnet nuget add source C:\path\to\folder --name winformsmvp-local
   dotnet add package WinformsMVP --prerelease
   ```

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

4. テストを実行

   ```bash
   dotnet test tests/WinformsMVP.Samples.Tests/WinformsMVP.Samples.Tests.csproj
   ```

---

## ドキュメント

すべてのドキュメントは **[Wiki](https://github.com/VoidAxon/winforms-mvp/wiki)** にあります。

### 入門

- [はじめての方へ (Getting Started)](https://github.com/VoidAxon/winforms-mvp/wiki/Getting-Started) — 5 分で動かす最小サンプル
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
| ターゲットフレームワーク | .NET Framework 4.0 / 4.8 (multi-targeting) |
| 言語 | コンパイル済み DLL を配布するため利用側の C# バージョンは不問（ライブラリ本体は `LangVersion=latest` でビルド） |
| プロジェクト形式 | SDK-style `.csproj` |
| テストフレームワーク | xUnit 2.9 |
| IDE | Visual Studio 2019+、JetBrains Rider、VS Code |

メインパッケージ (`WinformsMVP`) は **外部依存ゼロ** で `net40` / `net48` をマルチターゲットします。
`Microsoft.Extensions.DependencyInjection` 連携が必要なら、オプションパッケージ **`WinformsMVP.DependencyInjection`** を使います (`net48` 以降)。`Microsoft.Extensions.Logging` 連携については出荷パッケージは無く、M.E.L. の `ILoggerFactory` を框架の `ILoggerFactory` に橋渡しする **~30 行のアダプタを自分で書きます** ([Logging](https://github.com/VoidAxon/winforms-mvp/wiki/Reference-Logging) 参照、`net48` 以降)。

---

## プロジェクト構造

```
winforms-mvp/
├── src/
│   ├── WinformsMVP/                          コアフレームワーク (net40;net48)
│   ├── WinformsMVP.DependencyInjection/      M.E.DI 連携 (オプション、net48)
│   ├── WinformsMVP.Samples/                  サンプル WinForms アプリ
│   ├── WinformsMVP.Samples.Tests/            xUnit テストプロジェクト
│   └── MultiProjectDemo.*/                   複数プロジェクト DI 構成のデモ
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
