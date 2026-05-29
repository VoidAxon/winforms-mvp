# WinForms MVP Framework

[![.NET Framework](https://img.shields.io/badge/.NET%20Framework-4.0%20%7C%204.8-blue.svg)](https://dotnet.microsoft.com/download/dotnet-framework)
[![License](https://img.shields.io/badge/license-MIT-green.svg)](LICENSE.txt)

WinForms アプリケーションのための、**Model-View-Presenter (MVP)** フレームワーク。
WPF 風のコマンドバインドとクリーンアーキテクチャを .NET Framework に持ち込みます。

> **ドキュメントは [GitHub Wiki](https://github.com/VoidAxon/winforms-mvp/wiki) を参照してください。**
> 本 README はプロジェクトの入口です。詳細な使い方・設計指針・サンプル解説は Wiki に集約されています。

---

## 主な特徴

- 🎮 **ViewAction システム** — WPF の `ICommand` 相当を WinForms に。型安全なアクションキー、宣言的バインド、`CanExecute` による自動 Enabled 制御
- 🏗️ **クリーンな MVP 分離** — Presenter は WinForms 型を一切知らない。View インターフェイスにも `Button` / `TextBox` を露出させない
- 🪟 **ウィンドウナビゲーション** — Modal / 非 Modal、パラメータ付き、Fluent API、`IRequestClose<TResult>` による結果返却
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
        Dispatcher.Register(CommonActions.Save, OnSave,
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
        _binder.Add(CommonActions.Save, _saveButton);
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

## はじめに

1. リポジトリをクローン

   ```bash
   git clone https://github.com/VoidAxon/winforms-mvp.git
   ```

2. ソリューションをビルド

   ```bash
   dotnet build src/winforms-mvp.sln
   ```

3. サンプルアプリを実行

   ```bash
   dotnet run --project src/WinformsMVP.Samples/WinformsMVP.Samples.csproj
   ```

4. テストを実行

   ```bash
   dotnet test src/WindowsMVP.Samples.Tests/WindowsMVP.Samples.Tests.csproj
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
| 言語 | C# 7.3 以上 |
| プロジェクト形式 | SDK-style `.csproj` |
| テストフレームワーク | xUnit 2.9 |
| IDE | Visual Studio 2019+、JetBrains Rider、VS Code |

メインパッケージ (`WinformsMVP`) は **外部依存ゼロ** で `net40` / `net48` をマルチターゲットします。
`Microsoft.Extensions.Logging` / `Microsoft.Extensions.DependencyInjection` 連携が必要な場合のみ、別パッケージ・別アダプタを利用してください (`net48` 以降)。

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
