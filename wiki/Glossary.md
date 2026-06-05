# 用語集

このページは、本フレームワーク内で使われる用語の定義集です。
頭文字順に並んでいます。詳細な解説は各リンク先を参照してください。

---

## A — C

### ActionBinder

`ViewActionBinder` クラスのインスタンス。Form 内部で UI コントロール (Button、MenuItem 等) を `ViewAction` に紐付ける役割を持つ。View インターフェイスの `ActionBinder { get; }` プロパティで公開される。

詳細: [ViewAction システム § ActionBinder プロパティパターン](Reference-ViewAction-System#actionbinder-プロパティパターン)

### ActionKey

`ViewAction` の別名。アクションの不変識別子。`ViewAction.Create("Module.Save")` で生成する。

### AcceptChanges

`ChangeTracker<T>.AcceptChanges()`。現在値を新しいベースラインとして確定するメソッド。`IChangeTracking` インターフェイスのメンバー。

### CanExecute

`Dispatcher.Register(action, handler, canExecute: predicate)` の第 3 引数。アクションが実行可能かを判定する `Func<bool>`。`false` を返すとフレームワークが自動的にバインド済みコントロールの `Enabled` を `false` にする。

詳細: [ViewAction システム § CanExecute と自動 Enabled 制御](Reference-ViewAction-System#canexecute-と自動-enabled-制御)

### ChangeTracker

`ChangeTracker<T>` クラス。編集 / キャンセル用の変更追跡を提供する。`IChangeTracking` と `IRevertibleChangeTracking` を実装。

詳細: [ChangeTracker](Reference-ChangeTracker)

### CloseReason

本フレームワーク独自の `enum`。`Normal` / `SystemShutdown` / `TaskManager` / `ParentClosing` / `Unknown` の値を持つ。WinForms の `System.Windows.Forms.CloseReason` に **対応する独自型** で、WinForms 型は保持・公開しない。`WindowNavigator` が WinForms 値をこの enum へ **変換 (マップ)** する。

詳細: [ウィンドウクローズモデル § CloseReason 列挙](Concept-Window-Closing-Model#closereason-列挙)

### CanClose

`WindowPresenterBase` の `protected virtual bool CanClose(CloseReason reason)` override。Pull 方向のクローズゲート。`false` を返すとクローズをブロックする。非同期バリアント: `protected virtual void CanClose(CloseReason reason, Action<bool> proceed)`。

詳細: [Window Closing Model](Concept-Window-Closing-Model)

### ControlPresenterBase

UserControl 系の View を扱う Presenter 基底クラス。`ControlPresenterBase<TView>` (パラメータなし) と `ControlPresenterBase<TView, TParam>` (パラメータあり) の 2 種類がある。

詳細: [Presenter 基底クラス](Reference-Presenter-Base-Classes)

---

## D — F

### DefaultPlatformServices

`IPlatformServices` の標準実装。`PlatformServices.Default` に通常はこのインスタンスをセットする。

### DialogProvider

`IDialogProvider`。`OpenFileDialog` / `SaveFileDialog` / `FolderBrowserDialog` 等のシステムダイアログを抽象化したサービス。Presenter からは `Dialogs` プロパティでアクセスする。

詳細: [Platform Services § IDialogProvider](Reference-Platform-Services#idialogprovider--システムダイアログ)

### Dispatcher

`ViewActionDispatcher`。Presenter 側で ViewAction をハンドラに振り分けるコンポーネント。Presenter は `Dispatcher.Register(...)` でハンドラを登録し、Dispatcher が CanExecute 判定とハンドラ呼び出しを担う。

詳細: [ViewAction システム](Reference-ViewAction-System)

### EventAggregator

`EventAggregator` クラス。Presenter 間の疎結合な pub/sub。弱参照 + UI スレッド自動マーシャリング付き。

詳細: [EventAggregator](Reference-EventAggregator)

### FileService

`IFileService`。`File.ReadAllText` 等の薄いラッパー。テスト時に in-memory モックに差し替えるためのサービス。Presenter からは `Files` プロパティでアクセスする。

詳細: [Platform Services § IFileService](Reference-Platform-Services#ifileservice--ファイル-io)

### Fluent API

`WindowNavigator` を使うときの型引数を最小限にする API。`Navigator.For(presenter).WithParam(param).ShowAsModal<TResult>()` のように使う。

詳細: [WindowNavigator § Fluent API](Reference-WindowNavigator#fluent-api-tpresenter-型推論)

---

## I — N

### IChangeTracking / IRevertibleChangeTracking

.NET 標準のインターフェイス。`ChangeTracker<T>` がこれらを実装することで、`AcceptChanges()` / `RejectChanges()` を統一 API で扱える。

### IMessageService

メッセージダイアログのサービス抽象。`ShowInfo` / `ShowWarning` / `ShowError` / `ConfirmYesNo` 等のメソッドを持つ。Presenter からは `Messages` プロパティでアクセスする。

詳細: [Platform Services § IMessageService](Reference-Platform-Services#imessageservice--メッセージダイアログ)

### Implicit パターン / Explicit パターン

ViewAction の使い方の 2 種類。Implicit ではフレームワークが自動 Bind と CanExecute 更新を行う (推奨)。Explicit では View が `ActionRequest` イベントを発火し、Presenter が明示的に購読する (デバッグ・透明性重視のケース)。

詳細: [ViewAction システム § Implicit vs Explicit](Reference-ViewAction-System#implicit-パターン-vs-explicit-パターン)

### IModuleRegistrar

複数プロジェクト構成での View と Service の登録を担うインターフェイス。`WinformsMVP.DependencyInjection` パッケージで提供。

詳細: [Dependency Injection § IModuleRegistrar](Reference-DependencyInjection#imoduleregistrar-モジュール単位の登録)

### IPresenterFactory

DI 管理の依存を持つ子 Presenter を生成するためのファクトリ。親 Presenter がコンストラクタで受け取り、`_presenters.Create<ChildPresenter>()` で子を取得する。

詳細: [Dependency Injection § IPresenterFactory](Reference-DependencyInjection#ipresenterfactory-子-presenter-の生成)

### IRequestClose

`IRequestClose<TResult>`。業務結果を呼び出し元に返したい Presenter が実装するマーカーインターフェイス。メンバーは 0 個 — 結果型 `TResult` を宣言するだけ。`this.RequestClose(result, status)` 拡張メソッドが型安全に呼べるようになる。

詳細: [Window Closing Model](Concept-Window-Closing-Model)

### InteractionResult

`InteractionResult<T>`。成功・キャンセル・失敗の 3 状態を持つ結果型。ファイルダイアログ・ウィンドウ表示の戻り値型として使う。

詳細: [Platform Services § InteractionResult](Reference-Platform-Services#interactionresultt--失敗しうる操作のラッパー)

### InteractionStatus

`InteractionStatus` enum。`Ok` / `Cancel` / `Error` の値を持つ。`this.RequestClose(result, status)` や `ICloseSink.Close(result, status)` の引数として使う。

### IViewBase / IActionableView / IWindowView

View インターフェイスの 3 層の継承構造。

- **`IViewBase`** — すべての View 共通の **ルート** (空のマーカー)。義務を課さない。アクション生成コントロールを持たない View (静的なスプラッシュ等) はこれを直接実装してよい。
- **`IActionableView : IViewBase`** — ViewAction システムに参加する View が実装する中間層。`ActionBinder` (`IViewActionBinder`) を 1 つ公開する。
- **`IWindowView : IActionableView, IWin32Window`** — Form (ウィンドウ) 用。`Handle`, `IsDisposed`, `Activate` などウィンドウのライフサイクル識別子を追加する。クローズメンバー (`Closing` / `OnClosing` 等) は含まない — クローズ制御は Presenter の `CanClose` override で行う。

慣習として、Form 系の View は `IWindowView` を、UserControl 系の View は `IViewBase` (アクションを持つなら `IActionableView`) を起点に定義する。`IViewBase` は「UserControl 専用」ではなく、全 View の共通ルートである点に注意。

### IViewMappingRegister

View インターフェイスと Form クラスの紐付けを管理するレジストリ。`WindowNavigator` がこれを使って View を解決する。

詳細: [ViewMappingRegister](Reference-ViewMappingRegister)

### Logger / LoggerFactory

`ILogger` と `ILoggerFactory`。`Microsoft.Extensions.Logging` 互換 API を持つ自社抽象。Presenter からは `Logger` プロパティでアクセスする。

詳細: [Logging](Reference-Logging)

### Middleware

`ViewActionDispatcher` のミドルウェアパイプラインで使うコンポーネント。`IDispatchMiddleware` を実装し、`Dispatcher.Use(...)` で登録する。

詳細: [ViewAction システム § ミドルウェアパイプライン](Reference-ViewAction-System#ミドルウェアパイプライン)

### Mock

テスト用のモックオブジェクト。`MockPlatformServices` がモックサービスを束ね、各 Presenter テストで Mock View と一緒に使う。

詳細: [HowTo: Presenter をテストする](HowTo-Test-A-Presenter)

### MVP

Model-View-Presenter。本フレームワークが採用する設計パターン。本フレームワークは特に **Supervising Controller** バリアントを使う。

詳細: [MVP パターンとは](Concept-MVP-Pattern)

### Navigator

`IWindowNavigator`。ウィンドウのライフサイクル管理を担うサービス。Modal / Non-Modal / シングルトン / 結果取得を統一 API で提供する。Presenter からは `Navigator` プロパティでアクセスする。

詳細: [WindowNavigator](Reference-WindowNavigator)

---

## P — V

### Parameters クラス

`WindowPresenterBase<TView, TParam>` 等で使う、Presenter の起動時パラメータを束ねるクラス。例: `EditUserParameters { UserId, IsReadOnly }`。

ランタイム引数 (`UserId` 等) を Presenter のコンストラクタに混ぜない設計のために存在する。

詳細: [WindowNavigator § パラメータ vs DI](Reference-WindowNavigator#パラメータ-vs-di-役割分担)

### PlatformServices

`PlatformServices.Default` (静的プロパティ)。アプリ全体で使われる `IPlatformServices` 実体を保持する。`Program.Main` で 1 回だけ設定する。

### Presenter

MVP の中央コンポーネント。ユースケースロジックを持ち、View インターフェイスを通じて画面を操作する。WinForms 型に依存しない。

詳細: [Presenter 基底クラス](Reference-Presenter-Base-Classes)

### Pull (方向)

ウィンドウクローズモデルで、フレームワーク (`FormClosing`) が起点となる方向。Presenter の `CanClose(CloseReason reason)` override でゲートする。

詳細: [Window Closing Model](Concept-Window-Closing-Model)

### Push (方向)

ウィンドウクローズモデルで、Presenter (`OnSave` / `OnCancel` 等) が起点となる方向。`this.RequestClose(result, status)` 拡張メソッドで通知する。

### RejectChanges

`ChangeTracker<T>.RejectChanges()`。現在値をベースラインに戻すメソッド。`IRevertibleChangeTracking` のメンバー。

### Service Locator

DI パターンの 1 つ。`PlatformServices.Default` 経由でサービスにアクセスする方式。コンストラクタが不要で、最も簡単な構成。

詳細: [Dependency Injection § Service Locator](Reference-DependencyInjection#pattern-1-service-locator-単純な-presenter-向け)

### StandardActions

`WinformsMVP.MVP.ViewActions.StandardActions` 静的クラス。アプリ横断で使われる標準アクション (`Save`、`Cancel`、`Delete`、`Refresh`、`Ok`、`Reset` 等) を出荷時に提供する。モジュール固有のアクションは自前の静的クラスで `ViewAction.Create(...)` して宣言する。

### Supervising Controller

MVP のバリアント。Presenter は **ユースケースロジックのみ** を持ち、View のデータバインドや表示の詳細は View が担う。本フレームワークの採用パターン。

### Tell, Don't Ask

オブジェクト指向の原則。「状態を聞いて分岐する」ではなく「指示を出して任せる」。本フレームワークでは Presenter が View に対して Tell する形が基本。

詳細: [MVP 設計ルール § Rule 7](Design-Rules#rule-7-presenter-メソッドの戻り値を持たせない)

### ViewAction

不変な構造体。アクションの識別子として使われる。`ViewAction.Create("Module.Save")` で生成する。

詳細: [ViewAction システム](Reference-ViewAction-System)

### ViewActionBinder

Form 内部で UI コントロールを `ViewAction` に紐付けるコンポーネント。詳細は [ActionBinder](#actionbinder) の項目を参照。

### ViewActionDispatcher

Presenter 側でアクションをハンドラに振り分けるコンポーネント。詳細は [Dispatcher](#dispatcher) の項目を参照。

### ViewActionFactory

`ViewAction.Factory.WithQualifier("Prefix")` で生成する Factory オブジェクト。同じプレフィックスを繰り返したくないときに使う。

詳細: [ViewAction システム § ActionKey の定義方針](Reference-ViewAction-System#actionkey-の定義方針)

---

## W

### WindowCloseController

Per-window internal component that owns the entire WinForms close bridge for one Form. Implements `ICloseSink` (Push), bridges `FormClosing` → `CanClose` (Pull), and converges results on `FormClosed`. The only framework component that references `Form` directly. One instance per window; created by `WindowNavigator` (Managed) or `presenter.Connect(...)` (Adopted).

詳細: [Window Closing Model](Concept-Window-Closing-Model)

### WindowNavigator

`IWindowNavigator` の標準実装。詳細は [Navigator](#navigator) の項目を参照。

### WindowPresenterBase

Form 系の View を扱う Presenter 基底クラス。`WindowPresenterBase<TView>` と `WindowPresenterBase<TView, TParam>` の 2 種類がある。

詳細: [Presenter 基底クラス](Reference-Presenter-Base-Classes)

---

## 関連ページ

- [FAQ](FAQ) — よくある質問
- [トラブルシューティング](Troubleshooting) — エラーメッセージ別の対処
- [MVP パターンとは](Concept-MVP-Pattern) — 基礎概念
- [アーキテクチャ概観](Concept-Architecture-Overview) — フレームワーク全体像
