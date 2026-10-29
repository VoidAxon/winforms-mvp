# トラブルシューティング

このページは、フレームワーク利用時によく遭遇するエラー・症状とその対処法をまとめたものです。
よくある質問 (FAQ) は [FAQ](FAQ)、用語定義は [用語集](Glossary) を参照してください。

---

## 目次

- [起動 / 構成](#起動--構成)
- [View / Form の解決](#view--form-の解決)
- [ViewAction システム](#viewaction-システム)
- [ウィンドウクローズ](#ウィンドウクローズ)
- [ChangeTracker](#changetracker)
- [EventAggregator](#eventaggregator)
- [DI / サービス](#di--サービス)
- [ロギング](#ロギング)
- [テスト](#テスト)

---

## 起動 / 構成

### NullReferenceException: サービスが解決できない

**原因**: `ServiceLocator.Current` にサービスが登録されていない (View レジストリが空など)。

**対処**: `Program.Main` で `MvpApplication.Configure(...)` を呼び、View を登録する (テンプレートの `Program.cs` と同じ)。

```csharp
var register = new ViewMappingRegister();
register.RegisterFromAssembly(Assembly.GetExecutingAssembly());

MvpApplication.Configure(services: reg =>
{
    reg.RegisterInstance<IViewMappingRegister>(register);
    reg.RegisterInstance<Kibinago.Logging.ILoggerFactory>(new DebugLoggerFactory());
});
```

詳しくは [Getting Started](Getting-Started) を参照。

### `Application.Run()` が予期しないフォームで起動する

**原因**: `Application.Run(new SomeForm())` で起動した Form と、`Navigator.ShowWindow()` で表示した Form が混在している。

**対処**: ルートの Presenter を Navigator で起動し、`Application.Run()` は引数なしで呼ぶ。

```csharp
ServiceLocator.Current.Resolve<IWindowNavigator>().ShowWindow(new MainPresenter());
Application.Run();
```

---

## View / Form の解決

### 「View インターフェイス `IXxxView` に対応する実装型が見つかりません」

**原因**: `IViewMappingRegister` に該当 View が登録されていない、または自動スキャンの要件を満たしていない。

**対処 (順に試す)**:

1. 明示的に登録する

   ```csharp
   register.Register<IXxxView, XxxForm>();
   ```

2. 自動スキャンで登録する (要件を満たしているか確認)

   ```csharp
   register.RegisterFromAssembly(Assembly.GetExecutingAssembly());
   ```

3. `XxxForm` の自動スキャン要件:

   - `Form` を継承している
   - `IXxxView` (`IWindowView` 由来) を実装している
   - `public` パラメータなしコンストラクタを持つ
   - `abstract` でない

4. パラメータ付きコンストラクタが必要なら Factory 登録

   ```csharp
   register.Register<IXxxView>(() => new XxxForm(settings));
   ```

詳しくは [ViewMappingRegister](Reference-ViewMappingRegister) 参照。

### 「既に登録されています」例外

**原因**: 同じ View インターフェイスを 2 回登録しようとしている。

**対処**: 重複登録を削除する、または明示的に上書きする。

```csharp
register.Register<IXxxView, XxxForm>(allowOverride: true);
```

### 自動スキャンで View が 0 件しか見つからない

**確認項目**:

1. Form が `IWindowView` 由来のインターフェイスを実装しているか
2. `public` パラメータなしコンストラクタがあるか
3. `abstract` クラスになっていないか
4. スキャン対象のアセンブリが正しいか

```csharp
// 自身のアセンブリでなく別の DLL に Form があるなら
register.RegisterFromAssembly(typeof(SomeFormInOtherDll).Assembly);
```

---

## ViewAction システム

### ボタンが反応しない

**原因 (上から順にチェック)**:

1. `_binder.Add(action, button)` を呼び忘れている

   `InitializeActionBindings()` 内で必ず呼ぶ。

2. `View.ActionBinder` プロパティが `null` を返している

   ```csharp
   public IViewActionBinder ActionBinder => _binder;   // ← 戻り値が _binder か
   ```

   `null` を返すと **Explicit パターン** になり、自動 Bind が走らない (詳しくは [ViewAction システム § Implicit vs Explicit](Reference-ViewAction-System#implicit-パターン-vs-explicit-パターン))。

3. Presenter で `Dispatcher.Register(action, handler)` を呼び忘れ

   `RegisterViewActions()` で必ず登録する。

4. `CanExecute` が常に `false` を返している

   ボタンが見た目グレーアウトしていないか確認。グレーアウトしているなら `CanExecute` が原因。

### `CanExecute` が更新されない

**原因**: アクション実行以外で状態が変わった (TextBox 入力等) が、Dispatcher に通知していない。

**対処**:

```csharp
// OnViewAttached
SubscribeViewEvent(h => View.InputChanged += h, h => View.InputChanged -= h, Dispatcher.RaiseCanExecuteChanged);
```

詳しくは [ViewAction システム § 状態変化への対応](Reference-ViewAction-System#状態変化への対応) 参照。

### `Dispatch` を呼んでもハンドラが呼ばれない

**原因 (上から順にチェック)**:

1. 同じ ActionKey の文字列が違う

   ```csharp
   // ❌ Register と Dispatch で別の Key
   Dispatcher.Register(ViewAction.Create("X"), OnX);
   Dispatcher.Dispatch(ViewAction.Create("Y"));   // 違うキー
   ```

   静的クラスで定数化して **同じインスタンス** を参照する。

2. `CanExecute` が `false` で短絡されている

   `Dispatcher.CanDispatch(action)` で確認できる。

3. パラメータ型が不一致

   `Register<string>` に対して `Dispatch(action, 42)` (int) を渡すと呼ばれない。

### CheckBox の状態が変わらない

`CheckBox` / `RadioButton` は `Click` ではなく `CheckedChanged` イベントに紐付きます。Presenter のハンドラが `CheckedChanged` の延長で `Checked` を再設定すると、無限ループの可能性があります。状態は View 側で完結させてください。

---

## ウィンドウクローズ

### × ボタンを押しても確認ダイアログが出ない

**原因**: Presenter で `CanClose(CloseReason)` を override していない。

**対処**:

```csharp
protected override bool CanClose(CloseReason reason)
{
    if (_changeTracker.IsChanged && !Messages.ConfirmYesNo("Discard?", "Confirm"))
        return false;

    return true;
}
```

詳しくは [HowTo: ウィンドウクローズを扱う § Scenario 2](HowTo-Handle-Window-Closing#シナリオ-2-クローズ時のダーティチェック) 参照。

### Form 側にクローズコードが必要か?

**必要ない。** `IWindowView` にはクローズメンバーが存在しません。Forms にクローズ用のボイラープレートは不要です。クローズ制御はすべて Presenter の `CanClose` override と基底 `RequestClose(...)` メソッドで行います。

「閉じる」ボタンも `btnClose_Click => Close()` ではなく ViewAction にします。`RequestClose` は X と同じゲートを通るので、未保存の編集があれば X と同じ確認が出ます:

```csharp
Dispatcher.Register(StandardActions.Close, () => RequestClose(InteractionStatus.Cancel));
```

### `RequestClose` を呼んだのに閉じない

**原因**: 開いているウィンドウへの `RequestClose` は `CanClose` / `CanCloseAsync` を通ります (`CloseReason.Normal`)。ゲートが `false` を返すと (確認で「いいえ」、処理中フラグで拒否など) ウィンドウは開いたままで、渡した結果は捨てられます。非同期ゲートの判定待ちの間は、新しいクローズも始まりません。

**対処**:

- 保存 / 破棄のハンドラでは、`RequestClose` の **前に** `AcceptChanges` でダーティ状態を確定させる (次項)。
- `_isSaving` のようなフラグで拒否するゲートなら、ハンドラは `RequestClose` の **前に** そのフラグを下ろす。

### Save / 破棄ボタンで「変更を破棄しますか?」と聞かれる

**原因**: Push ハンドラが `RequestClose` の前にダーティ状態を確定させていない。`RequestClose` も X と同じ `CanClose` を通るので、ダーティのままだと確認が出ます。

**対処**: 保存後 (破棄の場合も) に `AcceptChanges` を呼んでから `RequestClose` する。これで `CanClose` はクリーンな状態を見て、確認なしに閉じます。

```csharp
private void OnSave()
{
    SaveData(_order);
    _changeTracker.AcceptChanges();            // finalize the dirty state first
    RequestClose(result, InteractionStatus.Ok);
}

private void OnDiscard()
{
    _changeTracker.AcceptChanges();            // stop tracking the discarded edits (not "saved")
    RequestClose(InteractionStatus.Cancel);
}
```

書き方 2 (毎回 View から新しいモデルを作る) では `AcceptChanges(saved)` / `AcceptChanges(ReadModel())` を使います。

詳細は [ウィンドウクローズモデル — 単一正本の不変条件](Concept-Window-Closing-Model#単一正本の不変条件) を参照。

### `ShowWindow` が `null` を返す / ウィンドウが一瞬も表示されない

**原因**: Presenter が初期化中 (`OnInitialize`、`OnInitializeAsync` の最初の `await` まで) に `RequestClose` を呼んだ。これは表示のキャンセルで、ウィンドウは表示されません (ゲートも通りません)。モーダルは `RequestClose` の結果を返し、非モーダルの `ShowWindow` は `onClosed` を呼んだ後に `null` を返します。`Connect` した Form は `Load` で不可視のまま閉じられます。

**対処**: 意図した動作なら、`ShowWindow` の戻り値の `null` を確認する。意図していないなら、初期化中に `RequestClose` を呼ぶ経路を見直す。詳しくは [HowTo: ウィンドウクローズを扱う § シナリオ 8](HowTo-Handle-Window-Closing#シナリオ-8-表示前に開くのを取りやめる)。

### システムシャットダウン時にダーティ確認ダイアログが出てフリーズ

**原因**: シャットダウン・タスクマネージャーによる終了は `CanClose` / `CanCloseAsync` に届きません (フレームワークが確認なしで許可します)。それでも確認が出るなら、確認が `CanClose` の外にあります — Form の `FormClosing` ハンドラや Form 側のボタン処理で `MessageBox` を出している、など。

**対処**: 未保存の確認は `CanClose` / `CanCloseAsync` だけに書き、Form には閉じる処理を書かない (「閉じる」ボタンも ViewAction で `RequestClose(InteractionStatus.Cancel)`)。理由 (`CloseReason`) のチェックは不要です。

```csharp
protected override bool CanClose(CloseReason reason)
    => !_changeTracker.IsChanged || Messages.ConfirmYesNo("変更を破棄しますか？");
```

---

## ChangeTracker

### `IsChanged` が常に `true` になる

**原因 (上から順にチェック)**:

1. 渡した `comparer` / `ChangeTrackingPolicy<T>` の比較が参照比較になっている (例 `(a, b) => a == b`)。スナップショットは別インスタンスなので常に「異なる」と判定される
2. モデルに編集と無関係に変わる値 (更新日時・監査用タイムスタンプなど) がある。既定の内容比較はすべてのフィールドを比較する
3. `mvp.ChangeTracking.UseCloner(...)` / `For<T>(policy)` で登録した複製が、元と内容的に等しいコピーを返していない

**対処**: 既定の内容比較 (`comparer` を渡さない) に戻すか、編集対象のフィールドだけを比較する comparer をポリシーで渡す。

```csharp
mvp.ChangeTracking.For(new ChangeTrackingPolicy<Order>(
    o => (Order)o.Clone(),                      // 検証済みの深いコピー
    (a, b) => a.SameEditableContent(b)));       // タイムスタンプを無視して比較
```

詳しくは [ChangeTracker § 解決順序](Reference-ChangeTracker#スナップショット-複製-の解決順序) 参照。

### 編集しても `IsChanged` が `false` のまま

**原因 (上から順にチェック)**:

1. `_order = _repository.Load(...)` のように変数を別のオブジェクトに差し替えた。tracker は作成時に渡されたオブジェクトを追跡し続ける → 差し替えたら `TrackChanges(_order)` で tracker を作り直す
2. 編集値が View にしか無い (書き方 2)。`IsChanged` は追跡中のオブジェクトしか見ない → `IsChangedWith(ReadModel())` を使う
3. 書き方 2 で、保存後に引数なしの `AcceptChanges()` を呼んだ。最初の古いオブジェクトを撮り直してしまう → `AcceptChanges(saved)` を使う
4. ネストされたオブジェクトの編集だけが見えない場合: 下記「ネストされたオブジェクトの編集が検出されない」

詳しくは [ChangeTracker § 2 つの使い方](Reference-ChangeTracker#2-つの使い方) 参照。

### ネストされたオブジェクトの編集が検出されない

**原因**: ポリシーまたは `IObjectCloner` で登録した複製 (例 手書きの `Clone()`) が浅いコピーになっている (参照型プロパティがスナップショットと共有されている)。既定のリフレクション深いコピーではこの問題は起きない。`ICloneable` は実装していても使われない。

**対処**: ネストされたオブジェクトとコレクションも `Clone()` する (または登録をやめて既定の深いコピーを使う)。

```csharp
public object Clone()
{
    return new UserModel
    {
        Name = this.Name,
        Address = this.Address?.Clone() as Address,        // ネストも Clone
        Tags = this.Tags != null ? new List<string>(this.Tags) : null,
    };
}
```

詳しくは [ChangeTracker § なぜ深いコピーが必要か](Reference-ChangeTracker#なぜ深いコピーが必要か) 参照。

---

## EventAggregator

### `Publish` してもハンドラが呼ばれない

**原因 (上から順にチェック)**:

1. 購読の `Dispose` で購読が解除されている

   ```csharp
   _subscription = _eventAggregator.Subscribe<MyMessage>(handler);
   _subscription.Dispose();   // ← 解除されている
   ```

2. Presenter (と `Disposables` バッグ) がすでに Dispose されている

   `.DisposeWith(Disposables)` で登録した購読は Presenter の Dispose 時に解除される。発行時点で購読側の Presenter がまだ生きているか確認する。(購読は強参照なので GC で消えることはない)

3. メッセージ型が異なる

   `Publish(new MyMessageV2())` に対し `Subscribe<MyMessageV1>(handler)` は反応しない。

4. 発行側と購読側で別のインスタンスを使っている

   Presenter ごとに `new EventAggregator()` すると互いに届かない。フレームワークが既定登録するシングルトンの `IEventAggregator` をコンストラクタ注入するか、`ServiceLocator.Current.ResolveRequired<IEventAggregator>()` で解決して共有する。

5. アグリゲーターを作ったサービスプロバイダが破棄されている

   `EventAggregator` は `IDisposable` で、生成したプロバイダの破棄時に Dispose される (以後 `Publish` は何も配送しない)。`ServiceLocator.Configure` / `MvpApplication.Configure` を再度呼ぶと前のプロバイダが破棄されるため、それ以前に解決したインスタンスを保持し続けていないか確認する。設定は起動時に 1 回だけ行う。

### バックグラウンドスレッドから `Publish` するとクロススレッド例外

**原因**: マーシャリング先のコンテキストが無い。各購読は `Subscribe` を呼んだ時点のスレッドの `SynchronizationContext` へマーシャリングするため、ワーカースレッド (コンテキスト無し) で購読すると、ハンドラは発行スレッドで実行される。

**対処**: 購読は UI スレッド (Presenter の `OnViewAttached` / `OnInitialize` など) で行う。アグリゲーターをどのスレッドで生成したかは関係しない。

```csharp
// UI スレッド上の Presenter で購読する — どのスレッドから Publish しても UI スレッドで実行される
_events.Subscribe<DataLoadedMessage>(OnDataLoaded).DisposeWith(Disposables);
```

---

## DI / サービス

### コンストラクタ注入の Presenter を Navigator で開けない

**原因**: Presenter はコンテナ管理外で `new` されているため、コンストラクタ引数が DI で解決されない。`IPresenterFactory.Create<TPresenter>()` を通じて Presenter を取得する必要がある。

**対処**: `IPresenterFactory` を親 Presenter のコンストラクタに注入し、そこで子 Presenter を生成する。

```csharp
public class ParentPresenter : WindowPresenterBase<IParentView>
{
    private readonly IPresenterFactory _presenters;

    public ParentPresenter(IPresenterFactory presenters)
    {
        _presenters = presenters;
    }

    private void OnEditUser()
    {
        var presenter = _presenters.Create<EditUserPresenter>();   // DI 解決
        Navigator.For(presenter).WithParam(new EditUserParameters { ... }).ShowAsModal();
    }
}
```

詳しくは [Dependency Injection § IPresenterFactory](Reference-DependencyInjection#ipresenterfactory-子-presenter-の生成) 参照。

### M.E.DI を使うと `IServiceProvider` 解決失敗

**原因**: ランタイム引数 (UserId 等) を Presenter のコンストラクタに混ぜている。

**対処**: コンストラクタは DI 管理の安定依存だけ、ランタイム引数は Parameters クラスに分離して `Navigator.WithParam` で渡す。

```csharp
// ❌ Bad
public EditUserPresenter(IUserRepository repo, int userId, bool readOnly) { ... }

// ✅ Good
public EditUserPresenter(IUserRepository repo) { ... }

public class EditUserParameters
{
    public int UserId { get; set; }
    public bool IsReadOnly { get; set; }
}
```

---

## ロギング

### `Logger.LogInformation(...)` を呼んでも何も出ない

**原因**: `LoggerFactory` がデフォルトの `NullLoggerFactory` のまま。

**対処**: `Program.Main` で `DebugLoggerFactory` か M.E.L. アダプタを設定する。

```csharp
MvpApplication.Configure(services: reg =>
{
    reg.RegisterInstance<IViewMappingRegister>(register);
    reg.RegisterInstance<Kibinago.Logging.ILoggerFactory>(new DebugLoggerFactory());   // VS Debug ウィンドウに出力
});
```

詳しくは [Logging § 3 つの構成パス](Reference-Logging#3-つの構成パス) 参照。

### Application Insights / Seq に流したい

`Microsoft.Extensions.Logging` 経由で接続できますが、メインパッケージは M.E.L. に依存していません。アプリ側に ~30 行のアダプタを書く必要があります。完成サンプルが [`samples/MultiProjectDemo.Shell/Logging/`](https://github.com/VoidAxon/winforms-mvp/tree/master/samples/MultiProjectDemo.Shell/Logging) にあります。詳しくは [Logging § M.E.L. プロバイダのつなぎ方](Reference-Logging#mel-プロバイダのつなぎ方) 参照。

---

## テスト

### テストで `Messages` などのプロパティがフェイクを使わない / 例外になる

**原因**: サービスを `PresenterTestHost.Start` に渡していない、または `Start` の **後** にプロバイダへ登録している。Presenter はフレームワークサービスを一度だけ解決します。渡したプロバイダにないサービスはエラーになり、グローバルの `ServiceLocator.Current` で補われることはありません。

**対処**: プロバイダを第 1 引数で渡し、差し替えは `Start` の **前** に登録する。

```csharp
var fakes = TestServices.CreateFakes();                              // Kibinago.Testing
fakes.Provider.RegisterInstance<IToastService>(new MyToasts());      // 差し替えるなら先に

PresenterTestHost.Start(fakes.Provider, new MyPresenter(), mockView); // 後 (アタッチ + 初期化)
```

Presenter がコンストラクタで `Messages` / `Logger` などを使っていると `Start(services, ...)` は `InvalidOperationException` になります。その処理は `OnViewAttached` / `OnInitialize` に移してください。

### `private OnSave()` をテストから呼べない

**原因**: ルール 16 に従って `private` にしている。

**対処**: `internal` 等に格上げせず、Dispatcher 経由で呼ぶ。

```csharp
mockView.Binder.Dispatch(StandardActions.Save);   // 本物の経路
```

これで `CanExecute` も評価され、production と同じ経路をテストできます。詳しくは [HowTo: Presenter をテストする § テストを駆動するときのルール](HowTo-Test-A-Presenter#テストを駆動するときのルール) 参照。

### `FakeWindowNavigator` のモーダルで `UnscriptedInteractionException` / 結果が `Error` になる

**原因**: `FakeWindowNavigator` は、答えを用意していないモーダル表示を例外にします (想定外の画面が黙って `Cancel` 扱いにならないように)。答えは `RespondTo<TPresenter, …>` の **Presenter の型** で選ばれるため、別の型 (例: インターフェイスや兄弟クラス) で登録していると一致しません。また、呼び出し側が期待する結果型と違う型の値を返すと、本番の `WindowNavigator` と同じく `InteractionStatus.Error` になります。

**対処**: 実際に表示される Presenter の型 (またはその基底型) と、呼び出し側の `TResult` に合わせて登録する。

```csharp
// Navigator.For(new CustomerSearchPresenter(repo)).WithParam(keyword).ShowAsModal<Customer>() を駆動する
fakes.Navigator.RespondTo<CustomerSearchPresenter, Customer>(r => InteractionResult<Customer>.Ok(alice));

// 結果なし版 (ShowWindowAsModal<TPresenter>) は RespondTo<TPresenter>
fakes.Navigator.RespondTo<AboutPresenter>(_ => InteractionResult.Ok());

// どのモーダルも気にしないテストなら
fakes.Navigator.CancelUnscripted();
```

例外メッセージに、問い合わせられた Presenter の型名が含まれます。

---

## ここに載っていないエラー

ここで解決しない場合は [GitHub Issues](https://github.com/VoidAxon/winforms-mvp/issues) でお知らせください。
エラーメッセージ・スタックトレース・最小再現コードを添えていただけると助かります。
