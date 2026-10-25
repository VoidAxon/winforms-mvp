# 用語集

このページは、本フレームワーク内で使われる用語の定義集です。
頭文字順に並んでいます。詳細な解説は各リンク先を参照してください。

---

## A — C

### ActionBinder

`ViewActionBinder` クラスのインスタンス。Form 内部で UI コントロール (Button、MenuItem 等) を `ViewAction` に紐付ける役割を持つ。View インターフェイスの `ActionBinder { get; }` プロパティで公開される。

詳細: [ViewAction システム § ActionBinder プロパティパターン](Reference-ViewAction-System#actionbinder-プロパティパターン)

### ActionKey

`ViewAction` の別名。アクションの不変識別子。画面の Actions クラスでは `ViewActionFactory` から生成する (単発なら `ViewAction.Create("Module.Save")`)。

### AcceptChanges

`ChangeTracker<T>.AcceptChanges()`。追跡中のオブジェクトのスナップショットを撮り直し、`IsChanged` を `false` にするメソッド (保存後に呼ぶ)。`IChangeTracking` インターフェイスのメンバー。引数版 `AcceptChanges(T value)` は `value` を新しいスナップショットにする (追跡中のオブジェクトには触れない)。

### CanExecute

`Dispatcher.Register(action, handler, canExecute: predicate)` の第 3 引数。アクションが実行可能かを判定する `Func<bool>`。`false` を返すとフレームワークが自動的にバインド済みコントロールの `Enabled` を `false` にする。

詳細: [ViewAction システム § CanExecute と自動 Enabled 制御](Reference-ViewAction-System#canexecute-と自動-enabled-制御)

### ChangeTracker

`ChangeTracker<T>` クラス。データがスナップショット (読み込み時 / 最後の保存時) から変わったかを **検出するだけ** のクラス。データは保持せず、元に戻す機能もない。`IChangeTracking` を実装。Presenter では `TrackChanges(value)` で作る。

詳細: [ChangeTracker](Reference-ChangeTracker)

### CloseReason

本フレームワーク独自の `enum`。`Normal` / `SystemShutdown` / `TaskManager` / `ParentClosing` / `Unknown` の値を持つ。WinForms の `System.Windows.Forms.CloseReason` に **対応する独自型** で、WinForms 型は保持・公開しない。`WindowNavigator` が WinForms 値をこの enum へ **変換 (マップ)** する。

詳細: [ウィンドウクローズモデル § CloseReason 列挙](Concept-Window-Closing-Model#closereason-列挙)

### CanClose

`WindowPresenterBase` の `protected virtual bool CanClose(CloseReason reason)` override。クローズゲート。開いたウィンドウのクローズ (X / Alt+F4 / オーナーのクローズ、および `RequestClose`) はすべてここを通る (システムシャットダウン / タスクマネージャを除く)。`false` を返すとクローズをブロックする。非同期バリアント: `protected virtual Task<bool> CanCloseAsync(CloseReason reason)` (既定は `CanClose(reason)` を返す。旧コールバック形式 `CanClose(reason, Action<bool> proceed)` は削除済み)。

詳細: [Window Closing Model](Concept-Window-Closing-Model)

### ControlLifecycleController

Control 系の内部コンポーネント。1 つの UserControl ホスティングにつき 1 インスタンスで、唯一の `view is Control` 境界を担い、コントロールの `Disposed` を購読して Presenter をテアダウンする (`Cleanup` 経由でサービスイベント購読を解除しリークを防ぐ)。`presenter.Connect(view)` が生成・配線する。Window 側の `WindowLifecycleController` と対称。

詳細: [Presenter 基底クラス](Reference-Presenter-Base-Classes)

### ControlPresenterBase

UserControl 系の View を扱う Presenter 基底クラス。`ControlPresenterBase<TView>` (パラメータなし) と `ControlPresenterBase<TView, TParam>` (パラメータあり) の 2 種類がある。`WindowPresenterBase` と同じ二段構築：コンストラクタは Presenter 自身の依存だけを取り、View は `presenter.Connect(view[, param])` で渡す。`Connect` が構築後に attach + 初期化 + テアダウン配線を行うため、`OnInitialize` でコンストラクタ注入の依存を安全に使える (コンストラクタ順序の罠が無い)。基底は `System.Windows.Forms` に依存しない。

詳細: [Presenter 基底クラス](Reference-Presenter-Base-Classes)

---

## D — F

### DefaultServiceProvider

`IServiceRegistry` + `IServiceProvider` の組み込み実装。`ServiceLocator.Configure(...)` で自動的に使われる。外部 DI コンテナを使わないアプリのサービス登録先。

### DialogProvider

`IDialogProvider`。`OpenFileDialog` / `SaveFileDialog` / `FolderBrowserDialog` / `ColorDialog` のシステムダイアログを抽象化したサービス。Presenter からは `Dialogs` プロパティでアクセスする。フォント・印刷系ダイアログは `Kibinago` パッケージの `IWinFormsDialogProvider : IDialogProvider` にある。

詳細: [Platform Services § IDialogProvider](Reference-Platform-Services#idialogprovider--システムダイアログ)

### Dispatcher

`ViewActionDispatcher`。Presenter 側で ViewAction をハンドラに振り分けるコンポーネント。Presenter は `Dispatcher.Register(...)` でハンドラを登録し、Dispatcher が CanExecute 判定とハンドラ呼び出しを担う。

詳細: [ViewAction システム](Reference-ViewAction-System)

### EventAggregator

`EventAggregator` クラス。Presenter 間の疎結合な pub/sub。購読は返されたトークンを Dispose するまで有効。UI スレッド自動マーシャリング付き。

詳細: [EventAggregator](Reference-EventAggregator)

### FileService

`IFileService`。`File.ReadAllText` 等の薄いラッパー。テスト時に in-memory モックに差し替えるためのサービス。Presenter からは `Files` プロパティでアクセスする。

詳細: [Platform Services § IFileService](Reference-Platform-Services#ifileservice--ファイル-io)

### Fluent API

`WindowNavigator` を使うときの型引数を最小限にする API。`Navigator.For(presenter).WithParam(param).ShowAsModal<TResult>()` のように使う。

詳細: [WindowNavigator § Fluent API](Reference-WindowNavigator#fluent-api-tpresenter-型推論)

---

## I — N

### IChangeTracking

.NET 標準のインターフェイス (`IsChanged` / `AcceptChanges()`)。`ChangeTracker<T>` が実装する。元に戻す `IRevertibleChangeTracking` は実装しない (復元は tracker の役割ではない)。

### IMessageService

メッセージダイアログのサービス抽象。`ShowInfo` / `ShowWarning` / `ShowError` / `ConfirmYesNo` 等のメソッドを持つ。Presenter からは `Messages` プロパティでアクセスする。

詳細: [Platform Services § IMessageService](Reference-Platform-Services#imessageservice--メッセージダイアログ)

### Implicit パターン / Explicit パターン

ViewAction の使い方の 2 種類。Implicit ではフレームワークが自動 Bind と CanExecute 更新を行う (推奨)。Explicit では View が `ActionRequest` イベントを発火し、Presenter が明示的に購読する (デバッグ・透明性重視のケース)。

詳細: [ViewAction システム § Implicit vs Explicit](Reference-ViewAction-System#implicit-パターン-vs-explicit-パターン)

### IModuleRegistrar

複数プロジェクト構成での View と Service の登録を担うインターフェイス。`Kibinago.DependencyInjection` パッケージで提供。

詳細: [Dependency Injection § IModuleRegistrar](Reference-DependencyInjection#imoduleregistrar-モジュール単位の登録)

### IPresenterFactory

DI 管理の依存を持つ子 Presenter を生成するためのファクトリ。親 Presenter がコンストラクタで受け取り、`_presenters.Create<ChildPresenter>()` で子を取得する。M.E.DI 実装 (`ServiceProviderPresenterFactory`) は Presenter ごとに `IServiceScope` を作り、Presenter の破棄と一緒にスコープも破棄する。

詳細: [Dependency Injection § IPresenterFactory](Reference-DependencyInjection#ipresenterfactory-子-presenter-の生成)

### RequestClose

`WindowPresenterBaseCore<TView>` が提供する Push 方向のクローズメソッド。インターフェイスの実装不要。開いたウィンドウでは X と同じく `CanClose` / `CanCloseAsync` (`CloseReason.Normal`) を通り、渡した結果はゲートが許可したときに呼び出し元へ届く候補になる。ウィンドウ表示前 (`OnInitialize` など) に呼ぶと、ゲートを通らずに表示をキャンセルする。2 つのオーバーロードがある:

- `protected void RequestClose(InteractionStatus status = InteractionStatus.Ok)` — 業務結果なしで閉じる
- `protected void RequestClose<TResult>(TResult result, InteractionStatus status = InteractionStatus.Ok)` — 型付き業務結果を返して閉じる (`TResult` は引数から推論される)

詳細: [Window Closing Model](Concept-Window-Closing-Model)

### InteractionResult

`InteractionResult<T>`。成功・キャンセル・失敗の 3 状態を持つ結果型。ファイルダイアログ・ウィンドウ表示の戻り値型として使う。

詳細: [Platform Services § InteractionResult](Reference-Platform-Services#interactionresultt--失敗しうる操作のラッパー)

### InteractionStatus

`InteractionStatus` enum。`Ok` / `Cancel` / `Error` の値を持つ。`RequestClose(result, status)` や `ICloseSink.Close(result, status)` の引数として使う。

### IViewBase / IActionableView / IWindowView

View インターフェイスの 3 層の継承構造。

- **`IViewBase`** — すべての View 共通の **ルート** (空のマーカー)。義務を課さない。アクション生成コントロールを持たない View (静的なスプラッシュ等) はこれを直接実装してよい。
- **`IActionableView : IViewBase`** — ViewAction システムに参加する View が実装する中間層。`ActionBinder` (`IViewActionBinder`) を 1 つ公開する。
- **`IWindowView : IActionableView`** — Form (ウィンドウ) 用のマーカー。独自メンバーも WinForms 型も持たない (`Handle` / `IsDisposed` / `Activate` も含まない)。クローズ制御は Presenter の `CanClose(CloseReason)` override / `RequestClose(...)` で行い、ライフサイクル・アクティブ化は具象 Form 側でフレームワークが扱う。モーダルダイアログの `owner` として渡せる (Navigator が実行時に実ウィンドウへ解決する)。
- **`IValidatableView : IViewBase`** — 検証エラーを表示する View が追加で実装する。`ValidationErrors` (`IValidationErrorDisplay`) を 1 つ公開し、Presenter の `Validation` に自動接続される。Form では通常 `ErrorProviderDisplay` を返す ([フォーム入力を検証する](HowTo-Validate-Form-Input))。

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

テスト用のモックオブジェクト。View インターフェイスのモック (Mock View) はテスト側で書き、フレームワークサービスは `Kibinago.Testing` パッケージのフェイク (`TestServices.CreateFakes()` → `FakeMessageService`、`FakeDialogProvider`、`FakeWindowNavigator` 等) を `PresenterTestHost.Start(fakes.Provider, presenter, view)` で渡して一緒に使う。

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

### ServiceLocator

`ServiceLocator.Current` (静的プロパティ)。アプリ全体で使われる `IServiceProvider` 実体を保持する。`Program.Main` で 1 回だけ `ServiceLocator.Configure(...)` か `ServiceLocator.Current =` で設定する。M.E.DI の場合は `provider.UseKibinago()` で設定される。

### Presenter

MVP の中央コンポーネント。ユースケースロジックを持ち、View インターフェイスを通じて画面を操作する。WinForms 型に依存しない。

詳細: [Presenter 基底クラス](Reference-Presenter-Base-Classes)

### Pull (方向)

ウィンドウクローズモデルで、フレームワーク (`FormClosing`: X / Alt+F4 / オーナーのクローズ) が起点となる方向。Presenter の `CanClose(CloseReason reason)` override でゲートする。Push 方向のクローズも同じゲートを通る。

詳細: [Window Closing Model](Concept-Window-Closing-Model)

### Push (方向)

ウィンドウクローズモデルで、Presenter (`OnSave` / `OnCancel` / 「閉じる」等) が起点となる方向。基底 `RequestClose(result, status)` / `RequestClose(status)` メソッドで通知する。Pull 方向と同じ `CanClose` ゲートを通る (`CloseReason.Normal`)。

### Service Locator

DI パターンの 1 つ。`ServiceLocator.Current` (`IServiceProvider`) 経由でサービスにアクセスする方式。Presenter は便利プロパティ (`Messages`、`Dialogs` 等) 経由で透過的に使う。コンストラクタが不要で、最も簡単な構成。

詳細: [Dependency Injection § Service Locator](Reference-DependencyInjection#pattern-1-service-locator-単純な-presenter-向け)

### StandardActions

`Kibinago.ViewActions.StandardActions` 静的クラス。アプリ横断で使われる標準アクション (`Save`、`Cancel`、`Delete`、`Refresh`、`Ok`、`Reset` 等) を出荷時に提供する。キーは `"Standard."` + メンバー名 (`"Standard.Save"`)。モジュール固有のアクションは自前の静的クラスで `ViewAction.Factory.WithQualifier("Module")` の Factory から宣言する。

### Supervising Controller

MVP のバリアント。Presenter は **ユースケースロジックのみ** を持ち、View のデータバインドや表示の詳細は View が担う。本フレームワークの採用パターン。

### Tell, Don't Ask

オブジェクト指向の原則。「状態を聞いて分岐する」ではなく「指示を出して任せる」。本フレームワークでは Presenter が View に対して Tell する形が基本。

詳細: [MVP 設計ルール § Rule 7](Design-Rules#rule-7-presenter-メソッドの戻り値を持たせない)

### ValidationState

Presenter の `Validation` プロパティの型。フィールド名 (Model のプロパティ名) ごとの現在の検証エラーを持つ。`Validate(model)` で DataAnnotations を再評価、`SetError` / `ClearError` で Presenter のルールを追加、送信時は `Check(model)` で全エラーを表示、`Reset()` で初期状態に戻す。`FormLevel` はフィールドに属さないエラーのキー。[フォーム入力を検証する](HowTo-Validate-Form-Input) を参照。

### ViewAction

不変な構造体。アクションの識別子として使われる。`ViewAction.Create("Module.Save")` で生成する。

詳細: [ViewAction システム](Reference-ViewAction-System)

### ViewActionBinder

Form 内部で UI コントロールを `ViewAction` に紐付けるコンポーネント。詳細は [ActionBinder](#actionbinder) の項目を参照。

### ViewActionDispatcher

Presenter 側でアクションをハンドラに振り分けるコンポーネント。詳細は [Dispatcher](#dispatcher) の項目を参照。

### ViewActionFactory

`ViewAction.Factory.WithQualifier("Prefix")` で生成する Factory オブジェクト。画面の Actions クラスの推奨形: プレフィックスを 1 回だけ書き、`Factory.Create(nameof(Save))` でキーを作る。

詳細: [ViewAction システム § ActionKey の定義方針](Reference-ViewAction-System#actionkey-の定義方針)

---

## W

### WindowLifecycleController

Per-window internal component that owns the entire WinForms close bridge for one Form. Implements `ICloseSink` (Push), runs the `CanClose` gate in `FormClosing` for every close of an open window (✕ and `RequestClose` alike), handles a `RequestClose` before the window is shown as cancelling the opening, and converges results on `FormClosed`. The only framework component that references `Form` directly. One instance per window; created by `WindowNavigator` (Managed) or `presenter.Connect(...)` (Adopted).

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
