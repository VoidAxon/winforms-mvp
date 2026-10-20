# 変更履歴

このプロジェクトの注目すべき変更はすべてこのファイルに記録します。

形式は [Keep a Changelog](https://keepachangelog.com/ja/1.1.0/) に従い、バージョニングは [Semantic Versioning](https://semver.org/lang/ja/) を採用します。

---

## [Unreleased]

### Added (追加)

- **非同期アクションハンドラ** — `ViewActionDispatcher.Register` に `Func<Task>` / `Func<CancellationToken, Task>` / `Func<T, Task>` / `Func<T, CancellationToken, Task>` のオーバーロードを追加。`async` ラムダ・`async Task` メソッドはこれらに束縛される (従来は `async void` の `Action` オーバーロードに束縛されていた)。実行中のアクションは `CanDispatch == false` (バインド済みコントロールは無効化) で、重複トリガーは無視。開始時・終了時に全 `CanExecute` を再評価し、完了時に `ActionExecuted` を発火。`await` 後の失敗も含めて例外はログ出力してアクションを再有効化。トークンは Presenter の破棄 (ウィンドウを閉じる) でキャンセルされ、それによるキャンセルはエラー扱いしない。想定内のエラーはハンドラ内で `try`/`catch` して `Messages` で伝える。
- **`ViewActionDispatcher.DispatchAsync(key[, payload])`** — 非同期ハンドラの完了で完了するタスクを返す (ハンドラの失敗では fault しない)。主にテスト用。
- **`DispatchContext.Completion` / `DispatchContext.CancellationToken`** — ミドルウェアが包むのはハンドラの同期部分のみ。`Completion` (ハンドラのタスク) を観察すれば非同期処理全体を計測・監査できる。非同期ハンドラの開始後にミドルウェアが例外を投げても、ハンドラは完了まで追跡される (その間アクションは無効、`ActionExecuted` は発火しない)。
- **`protected CancellationToken Lifetime`** (全 Presenter) — Presenter の `Dispose` の最初 (`Cleanup` より前) にキャンセルされる。非同期アクションハンドラと `OnInitializeAsync` に自動で渡される。
- **`protected virtual Task OnInitializeAsync(CancellationToken)`** — `OnInitialize` の直後に開始され、ウィンドウはその完了を待たずに表示される。失敗はログ出力、破棄によるキャンセルは静か。
- **`protected virtual Task<bool> CanCloseAsync(CloseReason)`** — 非同期の Pull ゲート (既定は同期 `CanClose(reason)` を返す)。判定待ちの間ウィンドウは開いたままで、再クローズは保留 (システムシャットダウン / タスクマネージャーは除く)。タスクが fault / キャンセルされた場合はクローズをブロックしてログ出力 (システムレベルのクローズは許可)。
- **テスト支援** — `PresenterTestHost.Initialized` (`OnInitializeAsync` の完了を待つ)、`PresenterTestHost.CanCloseAsync(reason)` (`CanCloseAsync` の判定を待つ)、サンプルテストの `CapturingActionBinder.DispatchAsync`。
- **検証エラーの表示 (`ValidationState` / `IValidatableView` / `ErrorProviderDisplay`)** — 全 Presenter に `protected ValidationState Validation` を追加。Model の DataAnnotations (`Validation.Validate(model)`: プロパティ・クラスレベル・`IValidatableObject`) と Presenter のルール (`SetError` / `ClearError`) のエラーをフィールド名 (Model のプロパティ名) ごとに保持し、送信時は `Validation.Check(model)` で全エラーを表示して妥当性を返す。`Reset()` で訪問済みフィールドを忘れる。`ValidationState.FormLevel` はフィールドに属さないエラーのキー。View が `IValidatableView` (`IValidationErrorDisplay ValidationErrors`) を実装していれば自動で接続され、変更のたびに `canExecute` を再評価。WinForms 側の `ControlErrorDisplay` はフィールド名をコントロールにマップし、ユーザーがコントロールを離れた後または送信後にエラーを表示 (フォーム全体・未マップのエラーは送信後に `FormLevel` のコントロールへ)。`ErrorProviderDisplay` は `ErrorProvider` による実装で、独自の見せ方は `ControlErrorDisplay` を継承して `SetControlError` を実装する。
- **`dotnet new` テンプレート (`WinformsMVP.Templates` パッケージ)** — 4 つ目の発行パッケージ。`dotnet new install WinformsMVP.Templates` (またはローカルの `.nupkg` パス) で導入。`winformsmvp` (アプリ: `Program.Main` でサービス・View を登録し、メインウィンドウ `IMainView` / `MainActions` / `MainPresenter` / `MainForm` を `Connect` で接続。`--framework net10.0-windows|net472`、`--mvp-version` (既定 `*-*` = プレリリースを含む最新)、`--no-restore`)、`mvp-window` (View インターフェイス + Actions + Presenter + Form/Designer。Ok は非同期アクション、Cancel は `Cancel` で閉じる。`--param-type`、`--validation` で Model + `ErrorProviderDisplay`、`--namespace` (既定はプロジェクトの `RootNamespace`))、`mvp-control` (UserControl 版。`new XxxPresenter().Connect(control)` でホスト)、`winformsmvp-tests` (xUnit + `CapturingActionBinder` + `PresenterTestHost` のサンプルテスト、`--app-project`)。生成される Form / UserControl の View プロパティには WFO1000 対策の `[DesignerSerializationVisibility(Hidden)]` 付き。CI は `tests/templates/verify-templates.sh` で全テンプレートを両フレームワーク向けに生成・`-warnaserror` ビルド・テスト実行し、リリースは 4 パッケージを publish。Wiki に [テンプレートを使う](wiki/HowTo-Use-Templates.md) を追加。
- サンプル: `ValidationDemo` を `RegistrationModel` (DataAnnotations) + `Validation.Validate` / `Check` / `Reset` / `SetError` + `ErrorProviderDisplay` に書き換え (手書きの検証・エラーラベル・エラー管理を削除)。送信ボタンはエラー中も有効のまま (未訪問フィールドのエラーを表示するため)。
- サンプル: `AsyncDemo` / `EmailDemo` / `ViewActionStateChangedExample` を非同期アクション・`OnInitializeAsync`・`CanCloseAsync` に移行 (`async void` と手動のビジーフラグ管理を削除)。`ComposeEmailPresenter` は下書き保存中に入力をロックし、保存後に変更があればクローズしない。

- **統一設定エントリ `MvpApplication.Configure(framework:, services:)`** — フレームワークのカスタマイズを起動時の 1 か所にまとめる。`framework:` の `WinformsMvpBuilder` (`mvp.Messages` / `Toasts` / `Dialogs` / `ChangeTracking` / `Events` / `Logging` / `Actions`) で差し替えと設定を行い、`services:` で業務サービスを登録する。M.E.DI では `services.AddWinformsMVP(viewRegistry, mvp => ...)` に同じ builder を渡す。優先順位は「直接登録 > `mvp.` の選択 > 組み込み既定値」、オプションオブジェクトを直接登録した場合は丸ごと置き換え (マージしない)。`UseXxx(instance)` にはファクトリ版 `UseXxx(sp => ...)` がある。詳細は wiki の HowTo-Customize-Framework。
- **`IToastService` と Presenter の `Toasts` プロパティ** — `Toasts.Show(text, type[, duration])`。View 側の `ToastOptions` 指定・アンカー表示は `IWinFormsToastService` (`Show(text, type, options)` / `ShowAnchored`)。既定実装 `WinFormsToastService` (サービスごとにトーストのスタックを持つ)。
- **`IMessageDialogBackend`** — メッセージボックスの見た目を決める唯一の場所 (メソッド 1 つ)。`MessageService` と `AnchoredMessageBox` の両方がこれを通す。既定は `NativeMessageDialogBackend`。
- **変更追跡の差し替え** — `IObjectCloner` / `IObjectContentComparer` (既定 `BuiltInObjectCloner` / `BuiltInObjectContentComparer`)、型ごとの `ChangeTrackingPolicy<T>`、`ChangeTracker(T, ChangeTrackingPolicy<T>)` コンストラクタ、Presenter の `TrackChanges(value[, policy | comparer])` (起動時の設定を使う)。CompareNETObjects などの専用ライブラリは数行のアダプターで接続できる。
- **画面ごとの比較除外 (`ChangeTracker.Ignore`)** — `.Ignore(x => x.UpdatedAt)` (ルート型のメンバー)、`.Ignore<OrderLine>(l => l.CachedTotal)` (その型が出てくるすべての場所、コレクション要素を含む)、`.IgnoreMember("Name")` (全型のその名前)、`.IgnoreType<Audit>()`。除外設定は `ChangeTrackingIgnores` として公開され、`IObjectContentComparer.ContentEquals(left, right, ignores)` に渡される (CompareNETObjects の `MembersToIgnore` / `TypesToIgnore` などへ対応付け可能)。パス式・メソッド呼び出し・計算プロパティはエラー。手書き比較関数の tracker では `InvalidOperationException`。
- **`EventAggregator` の既定登録** — 両方の登録経路で、プロバイダごとに 1 つの `IEventAggregator` をシングルトン登録。`EventAggregator` は `IDisposable` (プロバイダと一緒に破棄)。
- **`DefaultServiceProvider` は `IDisposable`** — ファクトリで自分が作ったシングルトンを破棄する (`RegisterInstance` で渡されたインスタンスは破棄しない)。`ServiceLocator` は自分で作ったプロバイダ (遅延生成の既定・`Configure` / `MvpApplication` で作ったもの) を置き換え時・`Reset` 時に破棄する。
- サンプル: `CustomizationDemo` (ランチャーの "Framework Customization") — 1 つの `Configure` でダイアログ・トースト・変更追跡ポリシーを差し替え、Presenter は通常のコードのまま。

### Changed (変更)

- **メッセージボックスとトーストを別サービスに分割** (BREAKING) — `IMessageService` はモーダルダイアログ 6 メソッドのみ (`ShowToast` を削除)。Presenter は `Toasts.Show(...)`、View 側でオプション付きなら `IWinFormsToastService`。`MessageService` / `DialogProvider` はオプションとバックエンドをコンストラクタで受け取る。
- **`ChangeTracker` のコンストラクタ** (BREAKING) — `(T)` / `(T, Func<T,T,bool>)` / `(T, IEqualityComparer<T>)` / `(T, ChangeTrackingPolicy<T>)`。省略可能だった `comparer = null` 引数は別オーバーロードになった。リテラルの `null` を渡すとオーバーロードが曖昧でコンパイルエラーになる (`new ChangeTracker<T>(value)` に書き換える)。型付きの `null` は `ArgumentNullException`。`new ChangeTracker<T>` は起動時の設定を使わない (組み込みエンジンと引数のみ)。
- **`ChangeTracker` は変更の検出だけを行う** (BREAKING) — 構築時に渡したオブジェクトを**参照で**保持し (コピーしない)、比較用のスナップショットだけを深いコピーで持つ。2 つの使い方: (1) 同じオブジェクトを編集し続ける — `IsChanged` / `AcceptChanges()`、(2) 画面から毎回新しいモデルを作る — `IsChangedWith(x)` / `AcceptChanges(x)`。`AcceptChanges(x)` は x のスナップショットを撮るだけで、追跡中のオブジェクトには触れない (非同期保存では送った内容を受け入れ、保存中の編集は未保存のまま残る)。クラスは `sealed`、実装するインターフェイスは `IChangeTracking` のみ。スレッドセーフではない (UI スレッドで使う)。
- **`ICloneable` を自動では使わない** (BREAKING) — ルートでも入れ子でも、`ObjectCloner` / `ChangeTracker` は `Clone()` を呼ばない。深いコピーと確認済みの `Clone()` は `ChangeTrackingPolicy<T>` で明示する。
- **`EventAggregator` の配送コンテキスト** — 各購読は `Subscribe` を呼んだ時点の `SynchronizationContext` に配送する (生成時のコンテキストは使わない)。
- **`FolderBrowserDialogOptions.Description` の既定値を `null` に** — 未指定なら `DialogProvider` の設定 (`mvp.Dialogs.FolderBrowserDescription`) を使う (従来は "Select Folder" 固定で全体設定が効かなかった)。
- **`winformsmvp` テンプレート** — `Program.cs` は `MvpApplication.Configure(framework:, services:)` を使う。テンプレートの sourceName を `MyMvpApp` に変更 (旧 `MvpApp` は `MvpApplication` の一部まで置換していた)。

- **コールバック形式の `CanClose(CloseReason, Action<bool> proceed)` を削除し `CanCloseAsync` に置き換え** (BREAKING) — オーバーライドしていた場合は `protected override async Task<bool> CanCloseAsync(CloseReason reason)` に書き換え、`proceed(x)` を `return x;` にする。同期の `CanClose(CloseReason)` は従来どおり。

- **`net40` サポートを終了し、`net472` / `net10.0-windows` をターゲットに** (BREAKING) — `WinformsMVP` / `WinformsMVP.DependencyInjection` は `net472;net10.0-windows`。最低 .NET Framework は 4.7.2、モダン .NET は .NET 10 (Windows)。net40 用 CallerInfo polyfill・`System.ValueTuple` 参照・`WinformsMVP.Net40SmokeTest` を削除。SDK 生成の AssemblyInfo に切り替え (`-p:Version` がアセンブリバージョンに反映され、`net10.0-windows` では `SupportedOSPlatform` も出力)。`EnableWindowsTargeting` により `*-windows` TFM を非 Windows ホストでもビルド可能。リリースワークフローは .NET 10 SDK を使用。
- **プラットフォーム非依存の `WinformsMVP.Core` (`netstandard2.0`) を分離** (BREAKING) — Presenter 基底クラス、ViewAction ディスパッチャ、EventAggregator、ChangeTracker、検証、ロギング、サービスインターフェイスを新パッケージ `WinformsMVP.Core` に移動。WinForms を参照しないため、Core に対してコンパイルされる Presenter コードは WinForms 型に触れられない。`WinformsMVP` は WinForms 実装 (`WindowNavigator`、`ViewActionBinder`、ダイアログ、トースト、`MessageService`、View マッピング) を保持し Core に依存する。**名前空間は変更なし**。
  - MVP アナライザーは `WinformsMVP.Core` に同梱され、`WinformsMVP` の利用者にも推移的に流れる。リリースは `WinformsMVP.Core` / `WinformsMVP` / `WinformsMVP.DependencyInjection` の 3 パッケージを publish。
  - **`IDialogProvider` からフォント/印刷ダイアログを分離** (BREAKING) — `IDialogProvider` はファイル/フォルダ/色ダイアログのみ。`ShowFontDialog` / `ShowPrintDialog` / `ShowPageSetupDialog` / `ShowPrintPreviewDialog`（戻り値が GDI+ 型）は新しい `IWinFormsDialogProvider : IDialogProvider` (`WinformsMVP`) へ。`ServiceLocator` の既定プロバイダは同じ `DialogProvider` インスタンスを両インターフェイスで登録し、`AddWinformsMVP` も `IWinFormsDialogProvider` を `DialogProvider` で既定登録する。
  - `ServiceLocator` の既定プロバイダは、実行時に `WinformsMVP` アセンブリの内部フック経由で WinForms の組み込みサービスを取り込む。`WinformsMVP.Core` のみ配置した場合は `FileService` + `NullLoggerFactory` のみ。
- **`ServiceProviderPresenterFactory` は Presenter ごとに `IServiceScope` を作成** — スコープは Presenter と一緒に破棄されるため、scoped サービスは Presenter/ウィンドウ単位になる。ルートコンテナが一時 Presenter (と View・transient 依存) を終了まで保持し続けるリークを解消。
- **`AddWinformsMVP` は別の `IViewMappingRegister` が登録済みなら例外** — その登録のマッピングが Navigator から見えなくなるため。
- **`DebugLogger` は `Trace.WriteLine` で出力** — `Debug.WriteLine` は Release ビルドのパッケージからコンパイル時に除去されていた。
- **`MessageFormatter` が `{@Name}` / `{$Name}` を受理** し、同じ名前の繰り返しは同じ引数を再利用 (従来は全引数が落ちていた)。

- **`ChangeTracker` の既定比較を内容比較に** (BREAKING) — `comparer` を渡さない場合、`T` の `IEquatable<T>` / `IComparable<T>` / `Equals` は使わず 設定された内容比較 (既定は新しい `ObjectComparer.ContentEquals`) で比較する。「Id が同じなら等しい」エンティティで編集が検出されず保存確認が出ない問題を解消 (ネストしたエンティティ・セット内の要素も同様)。`ContentEquals` は値型と `System.*` / `Microsoft.*` 型の `Equals` のみ信頼する。`DeepEquals` の挙動は不変。`T` の等価性を使いたい場合は `comparer` を明示する。
- **Presenter のライフサイクルと内部状態を非公開に、テスト用 `PresenterTestHost` を追加** (BREAKING) — Presenter の公開面はコンストラクタのみ (MVP ルール 16/17)。
  - `PresenterBase._dispatcher` / `_initialized` は `private`。派生 Presenter は `Dispatcher` プロパティを使う (ロガー / グローバルミドルウェアの構成も保証される)。
  - `AttachView` / `Initialize` は 4 つの Presenter 基底すべてで `IViewAttacher<TView>` / `IInitializable` / `IInitializable<in TParam>` の明示的インターフェイス実装になり、`presenter.AttachView(view)` / `presenter.Initialize()` はコンパイルできない。本番は `WindowNavigator` (Managed) か `presenter.Connect(view[, param])` (Adopted — Form/コントロールと一緒に Presenter も破棄) でホストする。`IInitializable<in TParam>` は反変。
  - 新 API `WinformsMVP.Testing.PresenterTestHost` (`WinformsMVP.Core`) — `PresenterTestHost.Start(presenter, mockView[, parameters])` は任意のモック View をアタッチ (Form 不要) して実際の初期化 (`RegisterViewActions`・binder 自動バインド・`OnInitialize`) を実行し、`RequestClose` を記録 (`CloseRequested` / `CloseCount` / `CloseResult` / `CloseStatus`)、`CanClose(reason = CloseReason.Normal)` でクローズゲートを駆動 (`bool?`。`CanCloseAsync` が未回答なら `null`、コントロール Presenter では例外)、`Dispose()` で Presenter を破棄。初期化が例外を投げた場合は Presenter を破棄して再スロー。
  - テストの推奨パターンを `((ICloseParticipant)presenter).CanCloseGate(...)` + 自作 `RecordingSink` の `BindCloseSink` から `PresenterTestHost` に置き換え (`ICloseParticipant` は internal のまま、フレームワーク内部テスト専用)。アクションの Dispatch は従来どおりモック View の `CapturingActionBinder` 経由 (`view.Binder.Dispatch(...)`)。
- **`EventAggregator` の購読を強参照に** (BREAKING) — 購読は返された `IDisposable` トークンを Dispose するまでハンドラを保持する (弱参照・GC による自動解除・コンパイル済みデリゲートを廃止)。従来はローカル変数や `this` をキャプチャするラムダのハンドラが次の GC で黙って失効していたが、メソッドグループ・キャプチャするラムダ・キャプチャする `filter` のいずれでも動作する。推奨は `_events.Subscribe<T>(handler).DisposeWith(Disposables);`。Dispose しないトークンはハンドラ (とキャプチャした Presenter) を EventAggregator の生存期間中保持し続けるため注意。UI スレッドへのマーシャリング、キュー中に Dispose された購読への非配送、例外の分離は不変 (配送コンテキストと `ClearSubscriptions` の変更は下記の統一設定の項目を参照)。
  - 新アナライザ **`MVP017`** — `IEventAggregator.Subscribe(...)` の戻り値を単独の式文で捨てると警告。明示的な `_ = ...`、変数/フィールドへの格納、`.DisposeWith(...)` などの連結、`using` は受理。

- **`ViewActionBinder` のルーティングを `Bind` のオーバーロードで決定** (BREAKING) — 以前は `ActionTriggered` に購読者がいると `Bind(dispatcher)` が Dispatcher への派発をスキップしていた (ログやステータス表示の購読だけで全ボタンが無反応になる)。現在 `Bind(dispatcher)` は常に派発し、その後 `ActionTriggered` を通知として発火する (通知側の例外が派発を妨げない)。`Bind()` はイベントのみ。Explicit パターンは「`ActionBinder` が `null` を返し、View 自身が `Bind()` を呼ぶ」形のみ。**移行**: binder を返しつつ `ActionTriggered` を `ActionRequest` に転送していた View は二重実行になるため、転送をやめて Implicit にするか、`ActionBinder => null` + `_binder.Bind()` に変更する。

### Removed (削除)

- **`ChangeTracker` の `CurrentValue` / `UpdateCurrentValue` / `RejectChanges` (`IRevertibleChangeTracking`) / `GetOriginalValue` / `IsChangedChanged`** (BREAKING) — データは呼び出し側が持つ。変更通知は無く、データが変わる箇所で `IsChanged` を読む。画面に留まったまま元に戻す場合はリポジトリから読み直し、新しい tracker を作る。
- **`ChangeTrackerDefaults` / `ToastDefaults` / `DialogDefaults`** (BREAKING) — 実行中に書き換えられる静的設定を廃止。起動時の `mvp.ChangeTracking` / `mvp.Toasts` / `mvp.Messages` + `mvp.Dialogs` に移行。
- **`IEventAggregator.ClearSubscriptions()`** (BREAKING) — テストはインスタンスを個別に作る。アプリ終了時は `MvpApplication.Run(mainForm)` (または `MvpApplication.Shutdown()`) がプロバイダごと `EventAggregator` を破棄する。
- **`MessageServiceToastExtensions`** — `IWinFormsToastService.Show(text, type, options)` を使う。
- **`DialogDefaults.FolderBrowserDialogTitle`** — どこからも使われていなかった (フォルダダイアログにタイトルはない)。
- **`WinformsMVP.Net40SmokeTest`** プロジェクト。
- 未使用の `PresenterBase<TView, TParameters>`。

### Fixed (修正)

- **Navigator** — `IInitializable<TParam>` のみを実装する Presenter をパラメータなしのオーバーロードで表示すると `InvalidOperationException` (従来は未初期化のままボタンが効かないウィンドウが開いた)。owner と Presenter の形はウィンドウ生成前に検証し、失敗時は Presenter と Form を破棄。キー付きウィンドウは `Initialize` 前に登録 (再入表示で二重に開かない)、閉じたウィンドウは自分のキーだけを削除。
- **クローズ結果** (Navigator と `Connect` で共通) — `Ok` で `TResult` と型が合わない結果、または `TResult` が非 null 許容値型なのに結果なしの場合は `Ok(default)` ではなく `InteractionStatus.Error` (メッセージ付き)。`RequestClose(message, InteractionStatus.Error)` / `RequestClose(exception, InteractionStatus.Error)` のメッセージ/例外が `InteractionResult.ErrorMessage` / `Exception` に入る。
- **`RequestClose`** — close sink がバインドされる前の呼び出しは保持され、バインド時に配送される。
- **非同期クローズゲート** — 判定待ちの間の再クローズは再質問せず保留。ただしシステムシャットダウン / タスクマネージャーによるクローズは通す。判定はどのスレッドで完了してもよい (UI スレッドへマーシャリング)。非同期で許可された場合、owner / MDI 親 / `Application.Exit` によるクローズを再発行する。Presenter の `Dispose` が例外を投げても Form は破棄される。
- **Presenter 基底** — View なしの `Initialize` は `InvalidOperationException`、2 つ目の View のアタッチは例外。`Dispose` は `Cleanup` が例外を投げても `Disposables` を解放し、再入安全。
- **`Connect` (Adopted)** — アタッチ済み・未初期化の Presenter を初期化する。アタッチ済みと異なる View は拒否。2 回目の `Connect` は例外を投げるが、生きている Presenter は破棄しない。アタッチ/初期化の失敗時は Presenter を破棄。コントロール用 `Connect` も失敗時に Presenter を破棄。
- **ViewAction** — `default(ViewAction)` は `Register` / `Binder.Add` で拒否 (`Dispatch` では無視)、null ハンドラも拒否。ハンドラの例外はハンドラを抜ける時点で `DispatchContext.Exception` に設定され、外側のミドルウェアが `finally` で参照できる。例外を投げたハンドラは `CanExecuteChanged` を発火してコントロール状態を更新。同じコントロールを 2 つのアクションにバインドすると `Add` 時に例外 (従来は最後の 1 つが黙って残った)。
- **EventAggregator** — UI スレッドへポスト済みのメッセージは、その間に破棄された購読には配送しない。各購読は `Subscribe` を呼んだ時点の `SynchronizationContext` に配送する (従来は生成時のコンテキストを優先し、`Program.Main` などコンテキストなしで生成するとワーカースレッドでハンドラが走った)。拡張メソッドのデリゲートを正しく呼び出す。
- **ChangeTracker** — `IsChanged` をキャッシュしない (追跡中のオブジェクトのインプレース編集も検出)。
- **ObjectCloner / ObjectComparer** — `HashSet` / `SortedSet` / `Queue` / `Stack` / `LinkedList` / `ReadOnlyCollection` を要素単位で扱う (比較子とスタック順を維持)。`Type` / `MemberInfo` / `Uri` / `Version` はコピーせず共有。コレクションの循環参照はコピー側に解決 (コンストラクタ専用のコレクションを通る循環は `NotSupportedException`)。参照同一性の要素を持つセットは構造的に比較するため、未変更のディープコピーと等しくなり ChangeTracker が最初から dirty にならない。
- **ModelValidator** — `IsValid` / `ValidateSequential` もクラスレベル属性、`IValidatableObject`、派生型のルールを含め、`ValidateAll` と結果が一致。
- **CompositeDisposable** — 1 つが例外を投げても全要素を破棄し、その後に再スロー。
- **Debouncer / Throttler** — `onError` 未指定のコールバックの例外をログ出力。
- **View 解決** — サービスプロバイダ経由の View フォールバックで破棄済み Form を拒否し、View は transient にすべき旨を説明。
- **トースト** — ワーカースレッドから呼んでも必ず UI スレッドで作成する (UI スレッドがない場合は従来の「呼び出しスレッドで作成」をやめ、ログを出して破棄)。`duration` を省略した呼び出しが設定の既定値を使う (従来は引数の既定値 3000 が常に渡され `ToastDefaults.Duration` が効かなかった)。呼び出しごとの `ToastOptions` (フォント含む) は `Show` から戻る前に複製する。
- **ChangeTracker** — 比較・複製の戦略を構築時に固定する (従来はドキュメントに反して毎回グローバル設定を読み直していた)。`ICloneable` を使わない (浅い `Clone()`、例えば `Array.Clone` で要素の編集を見逃していた)。
- **`ShowPrintPreviewDialog`** — 表示できた時点で `Ok(true)` を返す (ダイアログは `DialogResult.OK` を返さないため従来は常に Cancel)。

### Migration guide — 統一設定エントリ

| 旧 | 新 |
|---|---|
| `ChangeTrackerDefaults.Cloner = ...` / `.Comparer = ...` | `mvp.ChangeTracking.UseCloner(...)` / `.UseComparer(...)` (`IObjectCloner` / `IObjectContentComparer` のアダプター)。Presenter では `TrackChanges(model)` |
| Model の `ICloneable` が自動で使われる | 使われない。必要なら `mvp.ChangeTracking.For(new ChangeTrackingPolicy<T>(clone, equals))` |
| `new ChangeTracker<T>(value, null)` | `new ChangeTracker<T>(value)` |
| `tracker.CurrentValue.X = ...` | 呼び出し側の (構築時に渡した) オブジェクトを編集する |
| `tracker.UpdateCurrentValue(ReadModel())` | `tracker.IsChangedWith(ReadModel())` (使い方 2) |
| `tracker.AcceptChanges(x)` (現在値と基準の両方を置換) | `AcceptChanges(x)` は x のスナップショットのみ。追跡中のオブジェクトは変わらない |
| `RejectChanges()` (閉じる前に dirty を解除) | `AcceptChanges()` / `AcceptChanges(ReadModel())` → `RequestClose` |
| `RejectChanges()` (画面に留まって元に戻す) | リポジトリから読み直して表示し、新しい tracker を作る |
| `IsChangedChanged` | データ変更の処理で `View.IsDirty = tracker.IsChanged` と `RaiseCanExecuteChanged()` |
| `GetOriginalValue()` | 必要なら呼び出し側で元の値を保持する |
| `ToastDefaults.Xxx = ...` | `mvp.Toasts.Xxx = ...` (`ToastDefaults.Renderer` → `mvp.Toasts.UseRenderer(...)`) |
| `DialogDefaults.DefaultMessageCaption` | `mvp.Messages.DefaultCaption` |
| `DialogDefaults.Xxx` (ファイル/印刷ダイアログ・エラーメッセージ) | `mvp.Dialogs.Xxx` |
| Presenter の `Messages.ShowToast(...)` | `Toasts.Show(...)` |
| `messages.ShowToast(text, type, ToastOptions)` (拡張メソッド) | `IWinFormsToastService.Show(text, type, options)` |
| `IMessageService` の独自実装で見た目を変える | `IMessageDialogBackend` を実装して `mvp.Messages.UseDialogs(...)` |
| `new EventAggregator()` を作って注入 | 既定で登録済み。コンストラクタで `IEventAggregator` を受け取る |
| `IEventAggregator.ClearSubscriptions()` | 削除 (テストは新しいインスタンスを作る) |
| `ServiceLocator.Configure(reg => ...)` | そのまま使える。フレームワークもカスタマイズするなら `MvpApplication.Configure(framework: ..., services: reg => ...)` |
| `new MessageService()` / `new DialogProvider()` | `new MessageService(new NativeMessageDialogBackend(), new MessageDialogOptions())` / `new DialogProvider(new DialogProviderOptions())`。通常はコンテナから解決する |

### Tests (テスト)

- 統一設定: `WinformsMvpBuilderTests` / `RegistrationParityTests` (2 つの登録経路で同じ実装が解決されること、優先順位)、`WinFormsToastServiceTests` (UI スレッドでの作成、フォント複製、オプション検証)、`MessageServiceTests`、`DialogProviderOptionsTests`、`TrackChangesTests`、`ChangeTrackingPolicyTests` (配列ルート・浅い `ICloneable`)、`DefaultServiceProvider` / `ServiceLocator` の破棄、`EventAggregator` の破棄と購読時コンテキスト、MVP017 の独自実装。
- `LifecycleHardeningTests` (Navigator / クローズ結果 / 非同期 `CanClose` / `Connect` / Presenter 基底)、`CommonHardeningTests` (ViewAction / EventAggregator / ChangeTracker / ObjectCloner・ObjectComparer / ModelValidator / CompositeDisposable)、`ServicesHardeningTests` (DI スコープ / View 登録 / ロギング / トースト / 印刷プレビュー) を追加。`ServiceLocatorBuiltInsTests` に `IWinFormsDialogProvider` の既定登録を追加。テストプロジェクトは `net48` のため実行には Windows (または mono) が必要。

## [1.0.0-preview.4] - 2026-06-10

### Added (追加)

- **トーストの組み込みスタイル** — `ToastStyle.Soft`（角丸・淡色、**新しい既定**）/ `ToastStyle.Card`（角丸・白カード）/ `ToastStyle.Solid`（従来の方角・実色。旧 `Default` を改名）。`ToastOptions.Style` / `ToastDefaults.Style` で選択でき、カスタム `Renderer` は引き続きスタイルより優先。`public` な `SoftToastRenderer` / `CardToastRenderer`（配色・アイコン用の `protected virtual` フック付き）と `ToastRenderer.CornerRadius` を追加。角丸スタイルは `UpdateLayeredWindow` の per-pixel alpha で滑らかに合成し、方角の `Solid` は不透明描画で ClearType を維持するハイブリッド描画。
- **トーストの高さオートサイズ** — `ToastOptions.AutoHeight` / `ToastDefaults.AutoHeight`（既定 false）。有効時は幅固定で高さを内容に合わせ、`MinHeight`(40)..`MaxHeight`(220) でクランプ。`ToastRenderer.MeasureHeight` フックを追加（カスタムレンダラーも override 可）。
- **トーストの閉じる × を設定可能化** — `ToastOptions.ShowCloseButton` / `ToastDefaults.ShowCloseButton`（表示のみ。クリックでの解除は従来どおり）。
- **`ISelectionStore<T>` / `SelectionStore<T>` / `Cascade`** (`WinformsMVP.Common`) — 主従/N 段の連鎖選択を簡潔に書くためのプリミティブ。N 個の同型選択 Service を 1 つのジェネリックストアに、各レベルの「上位購読 → 自分をクリア → 再読込」を `Cascade.Bind` 1 行に畳む（多親は `Cascade.Combine`）。下位クリアは通知連鎖で自動化され、書き忘れによる stale 選択が構造的に起きない。`Cascade.Bind`/`Combine` は `initialSync`（既定 true）で束縛時に一度同期。`samples/WinformsMVP.Samples/CascadeDemo/` に 3 段の例。
- **`ISelectable` / `SelectableItem<T>` / `SelectableKeyComparer<T>`** (`WinformsMVP.Common`) — 選択の同一性まわりの補助。`SelectionStore<T>` の `where T : class` 制約（「未選択 = null」を表現するため）で直接は載らない値型（`int`/`enum`/`Guid`/`DateTime`/`bool`）を、Value を identity とする `SelectableItem<T>` で包む（リスト/コンボ項目としても利用可。`SelectableItem.Of`/`From`/`FromEnum` で生成）。エンティティ側は `ISelectable` を実装して Key（Id 等）を 1 か所宣言すれば、`SelectionStore<T>` は comparer 未指定でも `SelectableKeyComparer<T>` を**自動採用**し Key で判等する（再読込で別インスタンスになった「同じ行」も一致）。`ISelectable` 非実装型（`string` 等）は従来どおり `EqualityComparer<T>.Default` にフォールバック。判等の局所化のため、エンティティ自身の `Equals` は変更不要。

## [1.0.0-preview.3] - 2026-06-09

### Added (追加)

- **トースト通知を全面再実装** — `ToastNotification` を Form ベースから、layered Win32 ポップアップをラップする `NativeWindow` に変更。ネイティブの MessageBox 同様 `Application.OpenForms` に現れないため、ホストコードがそのコレクションを列挙していてもトーストの表示/自動クローズに乱されない。
- **トーストの設定 API** — `ToastOptions`（呼び出し単位）と `ToastDefaults`（アプリ全体）で position / size / font / duration / gap / 最大表示数 / opacity を制御。`IMessageService.ShowToast` に `ToastOptions` を取るオーバーロードを追加。
- **`ToastPosition`**（四隅）— トーストは隅ごとに重ならずスタックし（新しいものが端側）、最大表示数を超えると古いものを退避、長文は省略表示。
- **オーナードロー拡張点** — `ToastRenderer` / `DefaultToastRenderer` / `ToastRenderContext`（`ToolStrip.Renderer` スタイル）。呼び出し単位・アプリ全体のどちらでも差し替え可能。
- **View レイヤーの点アンカー ユーティリティ**（Presenter 向け API からは意図的に分離）— `AnchoredToast`（ツールチップ風のフリップ + 画面内クランプ付きの単発トースト）。

### Changed (変更)

- **`ControlPresenterBase<TView>` / `<TView, TParam>` を二段構築に再設計** (BREAKING) — コンストラクタは Presenter 自身の依存のみを取り、View は新しい `presenter.Connect(view[, param])` (`ControlPresenterConnectExtensions`) で渡す。`WindowPresenterBase` と対称の `AttachView` + `Initialize` パターン。これによりコンストラクタ注入した依存をコンストラクタ本体で代入する前に `OnInitialize` が走って `NullReferenceException` になる「コンストラクタ順序の罠」を根絶。
  - コンストラクタ署名が変更。`new XxxPresenter(view, deps)` → `new XxxPresenter(deps)` + `presenter.Connect(view)` への移行が必要。
  - `OnControlLoad` / `OnControlHandleCreated` フックを削除。ハンドル依存の処理は具象コントロール側へ。
- **`ControlLifecycleController`** (内部) を追加 — Control 系の唯一の `view is Control` 境界。`Disposed` を購読して Presenter をテアダウンする。Window 側の `WindowLifecycleController` と対称で、Presenter 基底から `System.Windows.Forms` 依存を排除。
- **`WindowCloseController` を `WindowLifecycleController` にリネーム** — `ControlLifecycleController` との命名対称性のため。内部クラスのため公開 API 影響なし。

### Removed (削除)

- **`IMessageService` の位置指定オーバーロードを削除** (BREAKING) — `ShowInfoAt` / `ShowWarningAt` / `ShowErrorAt` / `ConfirmYesNoAt` / `ConfirmOkCancelAt` / `ConfirmYesNoCancelAt`。位置指定は View の関心事なので、View コードから `AnchoredMessageBox` / `AnchoredToast` を呼ぶ。
- **`PositionableMessageBox` を `AnchoredMessageBox` にリネーム** (BREAKING) — あわせてダイアログを画面内に完全クランプ。

## [1.0.0-preview.2] - 2026-06-07

### Added (追加)

- **Window closing redesign — `CanClose` Pull gate** — `WindowPresenterBaseCore<TView>` に `protected virtual bool CanClose(CloseReason reason)` override と非同期バリアント `protected virtual void CanClose(CloseReason reason, Action<bool> proceed)` を追加。Presenter のクローズポリシーが一箇所に集約される。
- **Push close は基底 `RequestClose` メソッドに一本化** — `WindowPresenterBaseCore<TView>` に `protected void RequestClose(InteractionStatus status = InteractionStatus.Ok)` (結果なし) と `protected void RequestClose<TResult>(TResult result, InteractionStatus status = InteractionStatus.Ok)` (型付き結果、`TResult` は引数推論) を追加。インターフェイス実装も拡張メソッドも不要になった。`IRequestClose<TResult>` マーカーインターフェイスおよび `RequestCloseExtensions` は削除。
- **`WindowCloseController`** — 1 Form につき 1 インスタンスの内部コントローラ。Push sink (`ICloseSink`)、Pull bridge (`FormClosing`)、結果収束 (`FormClosed`) を統合。`CloseRequestedBeforeShow` / `ConvergeWithoutShow` でクローズ前表示エッジケースに対応。
- **Adopted hosting** — `presenter.Connect(form)` / `Connect<TView, TResult>(form, onClosed)` / `Connect<TView, TParam>(form, param)` 拡張メソッド (`WindowPresenterConnectExtensions`)。シェルウィンドウ・レガシー Form 移行時にフレームワークのクローズ機構を接続できる。シングルオーナーポリシーを遵守。
- ドキュメント体系を Diátaxis フレームワークに沿って再構築。GitHub Wiki に日本語ドキュメントを公開
- `WinformsMVP.DependencyInjection` パッケージ — `Microsoft.Extensions.DependencyInjection` 連携用 (`net48`)
- `WinformsMVP.Logging` 名前空間 — M.E.L. 互換 API の自社ロギング抽象 (`net40` 対応)
- コミュニティファイル: `CONTRIBUTING.md` / `CODE_OF_CONDUCT.md` / `SECURITY.md` および `.github/` テンプレート

### Changed (変更)

- **`IWindowView` からクローズメンバーを削除** (BREAKING) — `IWindowView.Closing` イベント / `IWindowView.OnClosing` メソッドを削除。クローズ制御は Presenter の `CanClose` override に一本化。Forms にクローズ用ボイラープレートは不要になった。
- **ナビゲーションの `owner` 引数を `IWindowView` に変更** (BREAKING) — `IWindowNavigator` / `NavigationContext.ShowAsModal` 等の `owner` 引数を WinForms の `IWin32Window` から框架抽象の `IWindowView` に変更。presenter 向けナビゲーション API から WinForms 型が消えた。`WindowNavigator` が実行時に実ウィンドウへ解決し、ウィンドウ非対応のビューが渡された場合は `ArgumentException`(fail-fast)を投げる。
- `ChangeTracker<T>` を `where T : class` に緩和し、`ICloneable` 実装を任意化
- 比較・複製の戦略をフック化 (`ChangeTrackerDefaults.Cloner` / `Comparer`)
- 既定の比較器を per-closed-T でキャッシュ

### Removed (削除)

- **`WindowClosingEventArgs`** (BREAKING) — `IWindowView.Closing` イベントと共に削除。
- **`CloseRequestedEventArgs<TResult>`** (BREAKING) — `IRequestClose<TResult>.CloseRequested` イベントと共に削除。
- **`IRequestClose<TResult>.CloseRequested` イベント** (BREAKING) — 削除。代替: 基底 `RequestClose(result, status)` メソッド。
- **`IRequestClose<TResult>` マーカーインターフェイス** (BREAKING) — 削除。Presenter クラスへの実装宣言は不要になった。
- **`RequestCloseExtensions`** (BREAKING) — 削除。`RequestClose` は基底クラスのメソッドとして直接提供される。
- **`WindowClosingBridge`** (BREAKING) — 削除。`WindowCloseController` がブリッジ機能を統合。
- **`WindowCloseCoordinator`** (BREAKING) — 削除。`WindowCloseController` が _suppressGate フラグで同等の保証を提供。
- **`IWindowView` から `IsDisposed` / `Activate` と `IWin32Window` 基底を削除** (BREAKING) — これらはインターフェイス経由で消費されていなかった(`WindowNavigator` は具象 `Form` の同名メンバーを使う)。`IWindowView` は `: IActionableView` の純粋なマーカーとなり、WinForms 型を一切持たなくなった。
- `WinformsMVP.Logging.MicrosoftExtensions` アダプタパッケージ
  — メインパッケージから M.E.L. 依存を完全排除。M.E.L. 連携が必要な場合はアプリ側で ~30 行のアダプタを書く方式に変更
- `IRequestClose.CanClose()` メソッド (旧バージョンの遺物) — `CanClose(CloseReason)` override に置き換え
- 旧 root-level ドキュメント (`QUICKSTART.md` / `MVP-DESIGN-RULES.md` 等) と `docs/*.md` の主題ドキュメント類
  — 内容を GitHub Wiki へ移行

### Fixed (修正)

- `Equals` 未実装モデルが `ChangeTracker` で常に `IsChanged == true` になる問題

---

### Migration guide — window closing redesign

**Forms**: Remove the `IWindowView.Closing` boilerplate (the `_closing` field and explicit-interface add/remove/invoke). Forms now need no closing code at all.

**Presenters**:
- Replace `View.Closing += OnViewClosing` with `protected override bool CanClose(CloseReason reason)`.
- Replace `CloseRequested?.Invoke(this, new CloseRequestedEventArgs<TResult>(result, status))` and the `public event` declaration with `RequestClose(result, status)`. Remove `, IRequestClose<TResult>` from the class declaration — it is deleted. No extension method required.
- The `RaiseClose` helper pattern is no longer needed.

**Tests**:
- Pull: replace `view.RaiseClosing(new WindowClosingEventArgs(...))` with `((ICloseParticipant)presenter).CanCloseGate(reason, ok => ...)`.
- Push: replace `presenter.CloseRequested += (s,e) => captured = e` with a bound `ICloseSink` recording fake (`((ICloseParticipant)presenter).BindCloseSink(sink)`).

---

## 形式について

このファイルは [Keep a Changelog v1.1.0](https://keepachangelog.com/ja/1.1.0/) のガイドラインに従います。

セクションの意味:

- **Added** — 新機能
- **Changed** — 既存機能の変更
- **Deprecated** — 近々削除される機能
- **Removed** — 削除された機能
- **Fixed** — バグ修正
- **Security** — 脆弱性関連

リリース後は `[Unreleased]` セクションの内容を新しいバージョンセクションに移動し、`[Unreleased]` を空にしてください。
