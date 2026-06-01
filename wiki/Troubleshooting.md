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

### NullReferenceException: `PlatformServices.Default` が `null`

**原因**: `Program.Main` で `PlatformServices.Default` を設定していない。

**対処**:

```csharp
PlatformServices.Default = new DefaultPlatformServices(
    viewMappingRegister: register,
    loggerFactory: new DebugLoggerFactory());
```

最低限の構成でも `new DefaultPlatformServices()` を呼んでください。詳しくは [Getting Started](Getting-Started) を参照。

### `Application.Run()` が予期しないフォームで起動する

**原因**: `Application.Run(new SomeForm())` で起動した Form と、`Navigator.ShowWindow()` で表示した Form が混在している。

**対処**: ルートの Presenter を Navigator で起動し、`Application.Run()` は引数なしで呼ぶ。

```csharp
PlatformServices.Default.WindowNavigator.ShowWindow(new MainPresenter());
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
   - `IXxxView` (`IWindowView` / `IViewBase` 由来) を実装している
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

1. Form が `IWindowView` / `IViewBase` 由来のインターフェイスを実装しているか
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
   public ViewActionBinder ActionBinder => _binder;   // ← 戻り値が _binder か
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
View.InputChanged += (s, e) => Dispatcher.RaiseCanExecuteChanged();
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

   `Dispatcher.CanExecute(action)` で確認できる。

3. パラメータ型が不一致

   `Register<string>` に対して `Dispatch(action, 42)` (int) を渡すと呼ばれない。

### CheckBox の状態が変わらない

`CheckBox` / `RadioButton` は `Click` ではなく `CheckedChanged` イベントに紐付きます。Presenter のハンドラが `CheckedChanged` の延長で `Checked` を再設定すると、無限ループの可能性があります。状態は View 側で完結させてください。

---

## ウィンドウクローズ

### × ボタンを押しても確認ダイアログが出ない

**原因**: `View.Closing` イベントを Presenter で購読していない。

**対処**:

```csharp
protected override void OnViewAttached()
{
    View.Closing += OnViewClosing;
}

private void OnViewClosing(object sender, WindowClosingEventArgs args)
{
    if (_changeTracker.IsChanged && !Messages.ConfirmYesNo("Discard?", "Confirm"))
        args.Cancel = true;
}
```

### Form 側に `Closing` イベントを書いても呼ばれない

**原因**: `IWindowView.Closing` は明示的インターフェイス実装にする必要がある (`Form` の非推奨 `Closing` と名前衝突するため)。

**対処**:

```csharp
private EventHandler<WindowClosingEventArgs> _closing;
event EventHandler<WindowClosingEventArgs> IWindowView.Closing
{
    add => _closing += value;
    remove => _closing -= value;
}
void IWindowView.OnClosing(WindowClosingEventArgs args) => _closing?.Invoke(this, args);
```

詳しくは [HowTo: ウィンドウクローズを扱う § Form 側のお決まりコード](HowTo-Handle-Window-Closing#form-側のお決まりコード) 参照。

### Save 後に「変更を破棄しますか?」と二重に聞かれる

**原因**: `OnSave` で `AcceptChanges()` を呼び忘れている。

**対処**:

```csharp
private void OnSave()
{
    SaveData();
    _changeTracker.AcceptChanges();   // ← RaiseClose の前に確定
    RaiseClose(result, InteractionStatus.Ok);
}
```

### システムシャットダウン時にダーティ確認ダイアログが出てフリーズ

**原因**: `CloseReason` をチェックしていない。

**対処**:

```csharp
private void OnViewClosing(object sender, WindowClosingEventArgs args)
{
    if (args.Reason == CloseReason.SystemShutdown ||
        args.Reason == CloseReason.TaskManager)
        return;       // システム終了は素通り

    // 通常のクローズだけ確認
    if (_changeTracker.IsChanged && !Messages.ConfirmYesNo(...))
        args.Cancel = true;
}
```

---

## ChangeTracker

### `IsChanged` が常に `true` になる

**原因**: 型 `T` が値等価性 (`Equals` / `IEquatable<T>`) を実装しておらず、`Clone()` も実装していない。

**対処**: 以下のいずれか。

1. `T` に `ICloneable` を実装し、`Equals` を override する
2. グローバルフックでリフレクション深いコピー・比較を使う (デフォルト)

詳しくは [ChangeTracker § 解決順序](Reference-ChangeTracker#スナップショット-複製-の解決順序) 参照。

### `RejectChanges()` を呼んでも元に戻らない

**原因**: `Clone()` の実装が浅いコピー (参照型プロパティが共有されている)。

**対処**: ネストされたオブジェクトとコレクションも `Clone()` する。

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

2. 購読者の参照が GC で消えている (弱参照)

   Presenter インスタンスへの強い参照がどこかに残っているか確認する。

3. メッセージ型が異なる

   `Publish(new MyMessageV2())` に対し `Subscribe<MyMessageV1>(handler)` は反応しない。

### バックグラウンドスレッドから `Publish` するとクロススレッド例外

**原因**: `EventAggregator` が UI スレッド以外で構築された。

**対処**: `EventAggregator` の `new` を **UI スレッド上** (`Program.Main` 等) で行う。

```csharp
// Program.Main 内で
var eventAggregator = new EventAggregator();   // ← UI スレッド
```

---

## DI / サービス

### コンストラクタ注入の Presenter を Navigator で開けない

**原因**: `WindowNavigator` は `new TPresenter()` でなく `IPresenterFactory.Create<TPresenter>()` でインスタンスを取得する仕組み。コンストラクタに DI 依存があると、Navigator は解決できない。

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
PlatformServices.Default = new DefaultPlatformServices(
    viewMappingRegister: register,
    loggerFactory: new DebugLoggerFactory());   // VS Debug ウィンドウに出力
```

詳しくは [Logging § 3 つの構成パス](Reference-Logging#3-つの構成パス) 参照。

### Application Insights / Seq に流したい

`Microsoft.Extensions.Logging` 経由で接続できますが、メインパッケージは M.E.L. に依存していません。アプリ側に ~30 行のアダプタを書く必要があります。完成サンプルが `samples/MultiProjectDemo.Shell/Logging/` にあります。詳しくは [Logging § M.E.L. プロバイダのつなぎ方](Reference-Logging#mel-プロバイダのつなぎ方) 参照。

---

## テスト

### `WithPlatformServices(...)` 後も `Messages` プロパティがデフォルトを返す

**原因**: `WithPlatformServices` を `Initialize()` の **後** に呼んでいる。

**対処**: `AttachView` → `WithPlatformServices` → `Initialize` の順で呼ぶ。

```csharp
var presenter = new MyPresenter()
    .WithPlatformServices(mockPlatform);   // 先

presenter.AttachView(mockView);
presenter.Initialize();   // 後
```

### `private OnSave()` をテストから呼べない

**原因**: ルール 16 に従って `private` にしている。

**対処**: `internal` 等に格上げせず、Dispatcher 経由で呼ぶ。

```csharp
presenter.Dispatcher.Dispatch(CommonActions.Save);   // 本物の経路
```

これで `CanExecute` も評価され、production と同じ経路をテストできます。詳しくは [HowTo: Presenter をテストする § テストを駆動するときのルール](HowTo-Test-A-Presenter#テストを駆動するときのルール) 参照。

### `MockWindowNavigator` で結果を設定しても反映されない

**原因**: 型引数が `SetupModalResult` と `Dispatch` で一致していない。

**対処**: パラメータあり/なしで型引数が違う。

```csharp
// パラメータなし
_platform.WindowNavigator.SetupModalResult<MyPresenter, MyResult>(...);

// パラメータあり
_platform.WindowNavigator.SetupModalResult<MyPresenter, MyParam, MyResult>(...);
```

---

## ここに載っていないエラー

ここで解決しない場合は [GitHub Issues](https://github.com/VoidAxon/winforms-mvp/issues) でお知らせください。
エラーメッセージ・スタックトレース・最小再現コードを添えていただけると助かります。
