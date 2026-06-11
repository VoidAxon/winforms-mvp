# ViewMappingRegister

`IViewMappingRegister` は **View インターフェイスと Form クラスの紐付け** を管理します。`WindowNavigator` がこのレジストリを使って、Presenter を表示する際の View インスタンスを動的に生成します。

> **位置づけ**: Form 系 Presenter は `WindowNavigator` 経由で動的に View を生成します。「`IFooView` ⇔ `FooForm`」の紐付けがないと、Navigator はどの Form を `new` すればよいか判断できません。UserControl 系では使いません。

---

## 目次

- [なぜ View マッピングが必要か](#なぜ-view-マッピングが必要か)
- [3 つの登録方法](#3-つの登録方法)
- [Factory メソッド登録](#factory-メソッド登録)
- [自動スキャンの要件](#自動スキャンの要件)
- [上書き登録 (テスト用)](#上書き登録-テスト用)
- [モジュール化された登録](#モジュール化された登録)
- [DI コンテナとの連携](#di-コンテナとの連携)
- [完全なセットアップ例](#完全なセットアップ例)
- [トラブルシューティング](#トラブルシューティング)
- [関連ページ](#関連ページ)

---

## なぜ View マッピングが必要か

`WindowNavigator` で Presenter を表示するとき、コードはこう書きます。

```csharp
var presenter = new SimpleDialogPresenter();
navigator.ShowWindowAsModal(presenter);
```

このとき Navigator は「`ISimpleDialogView` の実装である `SimpleDialogForm` を `new` する」必要があります。
この **インターフェイス → 実装型** の解決を担うのが `IViewMappingRegister` です。

---

## 3 つの登録方法

### 方法 1: 手動登録 (明示的)

すべての View を 1 つずつ登録します。

```csharp
var register = new ViewMappingRegister();

register.Register<ISimpleDialogView,  SimpleDialogForm>();
register.Register<IInputDialogView,   InputDialogForm>();
register.Register<IConfirmDialogView, ConfirmDialogForm>();

var navigator = new WindowNavigator(register);
```

**こんなときに**:

- View が 10 個未満の小規模アプリ
- 各 View に特殊な初期化が必要 (実際には [Factory メソッド登録](#factory-メソッド登録) を使う)
- 明示的なコントロールを優先したい

### 方法 2: アセンブリの自動スキャン (推奨)

アセンブリ内の Form を一括登録します。

```csharp
var register = new ViewMappingRegister();
int registered = register.RegisterFromAssembly(Assembly.GetExecutingAssembly());

var navigator = new WindowNavigator(register);
```

`RegisterFromAssembly` は、`IWindowView` または `IViewBase` を継承するインターフェイスを実装する `Form` 派生型をすべて探し出して登録します。

**こんなときに**:

- View が 10 個以上の中〜大規模アプリ
- 規約ベースでの開発を進めたい
- ボイラープレートを削減したい

### 方法 3: 名前空間スコープ付きスキャン

特定の名前空間配下だけをスキャンします。

```csharp
var register = new ViewMappingRegister();
register.RegisterFromNamespace(Assembly.GetExecutingAssembly(), "MyApp.Dialogs");

var navigator = new WindowNavigator(register);
```

**こんなときに**:

- モジュール分割された大規模アプリ
- テスト用 View が誤って登録されるのを防ぎたい
- 機能/モジュールごとに整理したい

---

## Factory メソッド登録

コンストラクタにパラメータが必要な View 等、`new T()` で生成できないケースには **Factory メソッド** を使います。

```csharp
public class ComplexDialogForm : Form, IComplexDialogView
{
    private readonly AppSettings _settings;

    public ComplexDialogForm(AppSettings settings)    // ← パラメータが必要
    {
        _settings = settings;
        InitializeComponent();
    }
}

// 登録
var settings = LoadAppSettings();
register.Register<IComplexDialogView>(() => new ComplexDialogForm(settings));

// Navigator は Factory 経由でインスタンスを生成
navigator.ShowWindowAsModal(new ComplexDialogPresenter());
```

**こんなときに**:

- View のコンストラクタにパラメータが必要
- 何らかの依存を注入したい
- 特殊な初期化ロジックがある

---

## 自動スキャンの要件

`RegisterFromAssembly()` で拾われるためには、Form クラスは以下の条件を満たす必要があります。

| 条件 | 説明 |
|------|------|
| ✅ **`Form` を継承** | `UserControl` 等の他の派生型は対象外 (UserControl は Navigator を使わない) |
| ✅ **`IWindowView` または `IViewBase` を継承するインターフェイスを実装** | フレームワーク標準のマーカー |
| ✅ **`public` パラメータなしコンストラクタを持つ** | リフレクションで `Activator.CreateInstance` を呼ぶ |
| ✅ **`abstract` ではない** | インスタンス化可能 |

```csharp
// ✅ 自動登録される
public class SimpleDialogForm : Form, ISimpleDialogView
{
    public SimpleDialogForm()
    {
        InitializeComponent();
    }
}

// ❌ 自動登録されない (パラメータあり) → Factory 登録が必要
public class ComplexDialogForm : Form, IComplexDialogView
{
    public ComplexDialogForm(AppSettings settings) { ... }
}
```

---

## 上書き登録 (テスト用)

実 View をモック View で差し替えるには `allowOverride: true` を指定します。

```csharp
// 本番登録
register.Register<ISimpleDialogView, SimpleDialogForm>();

// テストでモックに差し替え
register.Register<ISimpleDialogView, MockSimpleDialogForm>(allowOverride: true);
```

通常 (`allowOverride: false`) で既登録のインターフェイスを再登録すると例外を投げます。

---

## モジュール化された登録

大規模アプリでは、各モジュールが自分のレジストレーションを所有する形に整理します。

### `IViewModuleRegistrar` (DI 不要)

```csharp
public class UserModuleRegistrar : IViewModuleRegistrar
{
    public void RegisterViews(IViewMappingRegister registry)
    {
        registry.RegisterFromAssembly(typeof(UserModuleRegistrar).Assembly);
    }
}

// Shell プロジェクト
var register = new ViewMappingRegister();
register.RegisterModules(
    new UserModuleRegistrar(),
    new OrderModuleRegistrar(),
    new ReportModuleRegistrar());
```

### 名前空間でフィルタする例

```csharp
public class UserModuleRegistrar : IViewModuleRegistrar
{
    public void RegisterViews(IViewMappingRegister registry)
    {
        registry.RegisterFromNamespace(
            typeof(UserModuleRegistrar).Assembly,
            "MyApp.UserModule.Views");
    }
}
```

DI 連携を含む `IModuleRegistrar` (上位インターフェイス、`RegisterServices(IServiceCollection)` 付き) は [Dependency Injection](Reference-DependencyInjection) を参照してください。

---

## DI コンテナとの連携

`Microsoft.Extensions.DependencyInjection` 等の DI コンテナを使うときは、レジストリと DI コンテナを **ブリッジ** できます。

```csharp
// 1. View 専用のレジストリを構成
var register = new ViewMappingRegister();
register.RegisterModules(/* ... */);

// 2. DI コンテナを構築
var services = new ServiceCollection();
// ... 各 Module の RegisterServices(services)
var provider = services.BuildServiceProvider();

// 3. レジストリに DI コンテナをブリッジ
//    レジストリで明示登録されていない View インターフェイスは、
//    DI コンテナにフォールバックして解決される
register.WithServiceProvider(provider);
```

ブリッジは **オプション** です。レジストリ単独でも完全に動作します (DI を使わない既存プロジェクトのため)。

**重要**: View 解決とサービス/Presenter 解決は **独立した 2 つの仕組み** です。

| レイヤー | 何を解決するか | 担当 |
|---------|------------|----|
| **View 解決** | 「`IFooView` に対応する `Form` クラスは?」 | `IViewMappingRegister` |
| **Service/Presenter 解決** | 「Presenter のコンストラクタ依存を埋める」 | `IServiceProvider` (任意の DI コンテナ) |

両者が必要な場合は両方使い、片方だけで十分なら片方だけ使います。

---

## 完全なセットアップ例

`Program.cs` の典型的な構成です。

```csharp
internal static class Program
{
    [STAThread]
    private static void Main()
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        // 1. ViewMappingRegister を構成
        var register = new ViewMappingRegister();

        // 方法 A: 自動スキャン (中〜大規模アプリ向け)
        register.RegisterFromAssembly(Assembly.GetExecutingAssembly());

        // 方法 B: 手動登録 (明示的にコントロールしたい場合)
        // register.Register<IMainView,  MainForm>();
        // register.Register<IAboutView, AboutForm>();

        // 方法 C: Factory 登録 (コンストラクタにパラメータが必要)
        var settings = LoadSettings();
        register.Register<ISettingsView>(() => new SettingsForm(settings));

        // 2. ServiceLocator に View レジストリを登録
        ServiceLocator.Configure(reg =>
        {
            reg.RegisterInstance<IViewMappingRegister>(register);
            reg.RegisterInstance<WinformsMVP.Logging.ILoggerFactory>(new DebugLoggerFactory());
        });

        // 3. メインウィンドウを表示
        var mainPresenter = new MainPresenter();
        ServiceLocator.Current.Resolve<IWindowNavigator>().ShowWindow(mainPresenter);

        Application.Run();
    }
}
```

---

## 登録状況の確認

```csharp
// 特定の View が登録されているか
if (register.IsRegistered(typeof(ISimpleDialogView)))
{
    // ...
}

// 自動スキャンの登録数
int count = register.RegisterFromAssembly(Assembly.GetExecutingAssembly());
Console.WriteLine($"Auto-registered {count} views");
```

---

## トラブルシューティング

### 「View インターフェイス `IXxxView` に対応する実装型が見つかりません」

**原因**: `IXxxView` がレジストリに登録されていない、または自動スキャンの要件を満たしていない。

**対処**:

```csharp
// 明示登録
register.Register<IXxxView, XxxForm>();

// または自動スキャン
register.RegisterFromAssembly(Assembly.GetExecutingAssembly());
```

`XxxForm` が以下の条件を満たしているか確認:

- `Form` を継承している
- `IXxxView` を実装している
- `public` パラメータなしコンストラクタを持つ
- `abstract` ではない

パラメータ付きコンストラクタが必要なら [Factory 登録](#factory-メソッド登録) を使います。

### 「既に登録されています」

**原因**: 同じインターフェイスを 2 回登録しようとしている。

**対処**:

- 重複登録を削除する、または
- `allowOverride: true` を指定して上書きを明示

### 自動スキャンで Form が 0 件しか見つからない

**確認項目**:

1. Form が `IWindowView` または `IViewBase` を継承するインターフェイスを実装しているか
2. `public` パラメータなしコンストラクタがあるか
3. `abstract` クラスになっていないか
4. スキャン対象のアセンブリ・名前空間が正しいか

---

## 関連ページ

- [WindowNavigator](Reference-WindowNavigator) — レジストリのクライアント (Navigator が View を解決する仕組み)
- [Dependency Injection](Reference-DependencyInjection) — `IModuleRegistrar` と DI コンテナ連携の詳細
- [はじめに (Getting Started)](Getting-Started) — 初回セットアップでの登録例
- サンプル:
  - `samples/WinformsMVP.Samples/NavigatorDemo/` — 各種登録パターン
  - `samples/MultiProjectDemo.Shell/Program.cs` — モジュール化された登録の実例
