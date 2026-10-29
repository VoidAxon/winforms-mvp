# HowTo: フレームワークをカスタマイズする

このページでは、フレームワークの既定の動作 (メッセージボックス、トースト、ダイアログ、変更追跡、イベントアグリゲーター、ログ、ViewAction) を **アプリ起動時の 1 か所** で差し替える方法を示します。

- フレームワークの設定は `framework:` ブロック、業務サービスの登録は `services:` ブロックに分けて書きます。どちらも同じサービスコンテナに入ります。
- 何も書かなければ、すべて組み込みの既定値で動きます。
- 起動後に設定は固定されます (以前の `ChangeTrackerDefaults` / `ToastDefaults` / `DialogDefaults` のような、実行中に書き換えられる静的設定はありません)。

---

## 目次

- [基本形](#基本形)
- [差し替えられるもの一覧](#差し替えられるもの一覧)
- [優先順位](#優先順位)
- [M.E.DI を使う場合](#medi-を使う場合)
- [例: メッセージボックスを自社デザインにする](#例-メッセージボックスを自社デザインにする)
- [例: 変更追跡に専用ライブラリを使う](#例-変更追跡に専用ライブラリを使う)
- [注意点](#注意点)
- [関連ページ](#関連ページ)

---

## 基本形

```csharp
// Program.cs
MvpApplication.Configure(
    framework: mvp =>
    {
        // フレームワークのカスタマイズ: 「mvp.」と入力すると差し替え可能な項目が一覧表示されます
        mvp.Messages.UseDialogs(new CompanyStyleDialogBackend());
        mvp.Messages.DefaultCaption = "Contoso CRM";
        mvp.Toasts.Duration = 4500;
        mvp.Toasts.UseRenderer(new BrandToastRenderer());
        mvp.ChangeTracking.UseComparer(new CompareNetObjectsComparer());
    },
    services: reg =>
    {
        // 業務サービス (モジュールごとに登録)
        BillingModule.Register(reg);
        reg.RegisterInstance<IUserRepository>(new SqlUserRepository(connectionString));
    });
```

`MvpApplication.Configure` は新しいサービスコンテナを作り、既定値 → `framework:` の選択 → `services:` の登録の順に積み上げて `ServiceLocator.Current` に設定します。Presenter の `Messages` / `Toasts` / `Dialogs` / `TrackChanges` などは、この設定をそのまま使います。

`framework:` を省略すれば、従来の `ServiceLocator.Configure(reg => ...)` と同じ構成になります (`ServiceLocator.Configure` も引き続き使えます)。

> `MvpApplication.Configure` は、Presenter や View がサービスを解決する前 (通常は `Application.Run` の前) に呼んでください。それより前にフレームワークが自動で作った既定のコンテナは、呼び出し時に破棄されます。

---

## 差し替えられるもの一覧

| 変えたいもの | `mvp.` で設定 | 直接登録するインターフェイス |
|------------|--------------|-------------------------|
| メッセージボックスの見た目 (Presenter の `Messages` と `AnchoredMessageBox` の両方) | `Messages.UseDialogs(...)` | `IMessageDialogBackend` |
| メッセージボックスの既定タイトル | `Messages.DefaultCaption` | `MessageDialogOptions` |
| メッセージサービス全体 | — | `IMessageService` |
| トーストの位置・サイズ・フォント・表示時間・積み上げ・スタイル | `Toasts.Position` / `Size` / `Font` / `Duration` / `MaxVisibleToasts` / `Style` ほか | `ToastHostOptions` |
| トーストの描画 (アプリ全体) | `Toasts.UseRenderer(...)` | `ToastRenderer` |
| トースト通知の仕組み全体 (OS 通知など) | `Toasts.Use(...)` | `IWinFormsToastService` |
| ファイル/フォルダ/印刷ダイアログの既定タイトル・フィルター・エラーメッセージ | `Dialogs.OpenFileDialogTitle` ほか | `DialogProviderOptions` |
| ダイアログ全体 | — | `IDialogProvider` |
| 変更追跡の比較 / 複製 (アプリ全体) | `ChangeTracking.UseComparer(...)` / `UseCloner(...)` | `IObjectContentComparer` / `IObjectCloner` |
| 変更追跡の比較 / 複製 (型ごと) | `ChangeTracking.For(policy)` | `ChangeTrackingPolicy<T>` |
| イベントアグリゲーター | `Events.Use(...)` | `IEventAggregator` |
| ログ | `Logging.Use(...)` | `ILoggerFactory` |
| すべてのアクションの実行を記録 (操作ログ・計測) | `Actions.Observe(...)` | `IActionObserver` |
| 配線ミスを例外に (Strict 検証) | `Actions.StrictValidation()` | `ActionDispatcherOptions` |

インスタンスを受け取る `UseXxx(...)` には、ほかのサービスを使って作るためのファクトリ版もあります。

```csharp
mvp.Messages.UseDialogs(sp => new CompanyStyleDialogBackend(sp.Resolve<IThemeService>()));
```

---

## 優先順位

```
services: で直接登録したもの  >  framework: (mvp.) での選択  >  組み込みの既定値
```

- `framework:` はサービス登録の書き方を簡単にするだけのものです。実行時にフレームワークが見るのはコンテナだけです。
- `mvp.` に用意されていないものも、インターフェイスを `services:` で直接登録すれば差し替えられます。
- **オプションは丸ごと置き換え** です。`services:` で `ToastHostOptions` を直接登録すると、`mvp.Toasts.*` で設定した値はすべて無視されます (項目ごとのマージはしません)。どちらか一方で設定してください。

```csharp
MvpApplication.Configure(
    framework: mvp => mvp.Toasts.Duration = 4500,                                // 無視される
    services: reg => reg.RegisterInstance(new ToastHostOptions { Gap = 3 }));     // こちらが使われる (Duration は既定の 3000)
```

---

## M.E.DI を使う場合

`AddKibinago` に同じ `mvp` を渡せます。既定値も優先順位も `MvpApplication.Configure` と同じです。

```csharp
var services = new ServiceCollection();
services.AddKibinago(viewRegistry, mvp =>
{
    mvp.Messages.UseDialogs(new CompanyStyleDialogBackend());
    mvp.Toasts.Duration = 4500;
});
services.AddBilling();      // 業務サービスは通常どおり AddXxx で

var provider = services.BuildServiceProvider();
provider.UseKibinago();
```

`AddKibinago` はフレームワークの登録に `TryAdd` を使います。`AddKibinago` より **前** に登録したサービスはそのまま残り、**後** に登録したサービスは M.E.DI の規則 (最後の登録が勝つ) で優先されます。

---

## 例: メッセージボックスを自社デザインにする

`IMessageDialogBackend` のメソッドは 1 つだけです。これを実装すると、Presenter の `Messages.ShowInfo` / `ConfirmYesNo` なども、View 側の `AnchoredMessageBox` も、すべてこのダイアログで表示されます。

```csharp
public sealed class CompanyStyleDialogBackend : IMessageDialogBackend
{
    public MessageDialogResult Show(MessageDialogRequest request)
    {
        // request.Text / Caption (既定タイトル適用済み) / Buttons / Icon / Owner / Location
        using (var form = new CompanyMessageForm(request))
        {
            form.ShowDialog(request.Owner);
            return form.Result;   // MessageDialogResult.Ok / Yes / No / Cancel ...
        }
    }
}
```

- **Form で作るダイアログは、表示中 `Application.OpenForms` に入ります。** 既定の `NativeMessageDialogBackend` (Windows のメッセージボックス) やトースト・処理中の表示は Form ではないので入りません。`OpenForms` を順に見る処理 (すべて閉じる、メインウィンドウを探すなど) がある場合は、型でこのダイアログを除いてください (`OpenForms.Cast<Form>().Where(f => !(f is CompanyMessageForm))`)。
- `request.Location` は `AnchoredMessageBox` (View が位置を決める表示) のときだけ値があります。`Messages` からの呼び出しでは `null` で、オーナーの中央に出します。

完全なサンプルは [`samples/Kibinago.Samples/CustomizationDemo/`](https://github.com/VoidAxon/winforms-mvp/tree/master/samples/Kibinago.Samples/CustomizationDemo) (サンプルの「カスタマイズ」→ カスタム設定で別プロセスを起動) を参照してください。

---

## 例: 変更追跡に専用ライブラリを使う

フレームワーク側の小さなインターフェイスに数行のアダプターを書くだけです。Core パッケージは外部ライブラリに依存しません。

```csharp
// CompareNETObjects: each kind of ignore maps onto one of its settings
public sealed class CompareNetObjectsComparer : IObjectContentComparer
{
    public bool ContentEquals(object left, object right, ChangeTrackingIgnores ignores)
    {
        var config = new ComparisonConfig();
        foreach (var m in ignores.Members)     config.MembersToIgnore.Add(m.Owner.Name + "." + m.Name);
        foreach (var n in ignores.MemberNames) config.MembersToIgnore.Add(n);
        foreach (var t in ignores.Types)       config.TypesToIgnore.Add(t);
        return new CompareLogic(config).Compare(left, right).AreEqual;
    }
}

// Force.DeepCloner
public sealed class DeepClonerAdapter : IObjectCloner
{
    public object DeepClone(object value) => value.DeepClone();
}

mvp.ChangeTracking.UseComparer(new CompareNetObjectsComparer());
mvp.ChangeTracking.UseCloner(new DeepClonerAdapter());
```

特定の型だけ比較方法を変える場合は型ごとのポリシーを登録します (宣言型 `T` と完全一致した場合だけ使われます)。

```csharp
mvp.ChangeTracking.For(new ChangeTrackingPolicy<Order>(
    o => (Order)o.Clone(),                   // 深いコピーだと確認済みの Clone()
    (a, b) => a.SameEditableContent(b)));    // 更新日時などを無視
```

---

## 注意点

- **`TrackChanges` と `new ChangeTracker<T>` の違い**: 起動時の `mvp.ChangeTracking` の設定が使われるのは、Presenter の `TrackChanges(value)` で作ったトラッカーだけです。`new ChangeTracker<T>(value)` は組み込みのエンジン (またはコンストラクタに渡したもの) だけを使います。
- **`ICloneable` は自動では使われません**: .NET の `Clone()` は浅いコピーでもよいことになっているため、変更を見逃すおそれがあります。深いコピーだと確認できている `Clone()` を使いたい場合は、上の型ごとのポリシーで明示してください。
- **トースト**: `Toasts.Show` はどのスレッドから呼んでも UI スレッドで表示されます。UI スレッドがまだない (メッセージループ開始前) または終了後の呼び出しは、ログに警告を出して無視されます。
- **`IToastService` だけを直接登録した場合**: 置き換わるのは Presenter の `Toasts` だけで、View 側 (`IWinFormsToastService`、`AnchoredToast`) は組み込みのままです。両方を置き換えるには `mvp.Toasts.Use(...)` か `IWinFormsToastService` を登録してください。
- **破棄**: コンテナが自分で作ったサービス (トーストサービス、イベントアグリゲーターなど) はコンテナの破棄時に破棄されます。`Application.Run(mainForm)` の代わりに `MvpApplication.Run(mainForm)` を使うと、メインフォームが閉じたときに `MvpApplication.Configure` で作ったコンテナが破棄されます (自分で終了処理を書く場合は `MvpApplication.Shutdown()`)。M.E.DI のコンテナは作った側で `Dispose` してください。`UseXxx(instance)` や `RegisterInstance` で渡したインスタンスは呼び出し側の所有で、フレームワークは破棄しません。

---

## 関連ページ

- [Reference: プラットフォームサービス](Reference-Platform-Services.md)
- [Reference: DependencyInjection](Reference-DependencyInjection.md)
- [Reference: ChangeTracker](Reference-ChangeTracker.md)
- [Reference: トースト通知](Reference-Toast-Notifications.md)
- [Reference: EventAggregator](Reference-EventAggregator.md)
