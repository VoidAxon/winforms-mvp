# HowTo: 画面内のページ移動 (パンくず)

「顧客一覧 → 顧客詳細 → 受注編集」のように、**1 つのウィンドウの中で階層をたどり**、上部のパンくず (`顧客一覧 › 山田 太郎 › 受注 101`) で任意の階層へ戻る画面のための仕組みです。

使う部品は次のとおりです。

| 型 | 役割 |
|----|------|
| `PageStack` | 1 つの画面のページの積み重ね。親 Presenter が持つ (`new PageStack(this, () => View.Pages)`) |
| `IPageHost` / `PageHost` | ページを表示する場所。`PageHost` (Kibinago のパネル) をフォームに置き、View インターフェイスで `IPageHost` として公開する。ページのコントロールはビューの登録 (ウィンドウと同じ `IViewMappingRegister`) から作られる |
| `IPageCloseGuard` | 「このページを捨ててよいか」に答えるページ側のインターフェイス (1 メソッド) |

使わない画面には何も増えません (Presenter 基底クラスのメンバーは 1 つも追加されていません)。

サンプル: `samples/Kibinago.Samples/PageNavigationDemo/` (サンプルアプリの「画面の組み立て」→「ページ移動 (パンくず)」)。

---

## 目次

- [いつ使うか](#いつ使うか)
- [親 View のインターフェイス](#親-view-のインターフェイス)
- [ページを積む / 戻る](#ページを積む--戻る)
- [ページ側: IPageCloseGuard](#ページ側-ipagecloseguard)
- [確認の順序と「全部か無しか」](#確認の順序と全部か無しか)
- [IsBusy — 確認中の Push は何もしない](#isbusy--確認中の-push-は何もしない)
- [結果を親へ返す: パラメーターのコールバック](#結果を親へ返す-パラメーターのコールバック)
- [「また表示された」を知りたいとき](#また表示されたを知りたいとき)
- [ウィンドウを閉じるとき](#ウィンドウを閉じるとき)
- [ページを持つパネルごと破棄するとき: CanCloseAllAsync](#ページを持つパネルごと破棄するとき-cancloseallasync)
- [非同期の確認と ShowBusy](#非同期の確認と-showbusy)
- [ページが外から破棄されたとき](#ページが外から破棄されたとき)
- [テスト](#テスト)
- [やらないこと (Prism との比較)](#やらないこと-prism-との比較)
- [自分のレイアウトで表示する](#自分のレイアウトで表示する)
- [パンくずのコントロールを替える](#パンくずのコントロールを替える)
- [関連ページ](#関連ページ)

---

## いつ使うか

- 一覧から詳細へ、詳細から編集へ **ドリルダウン** し、パンくずで戻る画面
- 戻ったときに、捨てるページ (未保存の編集) に **確認したい** 画面

別ウィンドウで開くなら [WindowNavigator](Reference-WindowNavigator)、同時に並べて見せるなら [マスター/詳細](HowTo-Implement-Master-Detail) や `AttachChild` のパネルです。

ページは `AttachChild` の子と同じく、親 Presenter が `new` した **コントロール Presenter** です。下のページは生きたまま隠れ (一覧の選択・スクロール位置はそのまま)、戻るときに捨てたページは破棄されます。

## 親 View のインターフェイス

親 View が公開するのは、ページを表示する場所 (`IPageHost`) だけです。ページのコントロールは `PageHost` が作り、隠し、外します。Presenter はコントロールに触れません。

```csharp
public interface IPageNavigationDemoView : IControlView   // ウィンドウなら IWindowView
{
    IPageHost Pages { get; }                             // フォーム (UserControl) に置いた PageHost
}
```

ページのコントロールは、起動時にウィンドウと同じ方法で登録します (アセンブリのスキャンは Form だけを対象にするので、ページは明示的に登録します)。

```csharp
register.Register<ICustomerListView, CustomerListControl>();
register.Register<ICustomerDetailView, CustomerDetailControl>();
register.Register<IOrderEditView, OrderEditControl>();
```

`PageHost` は、ページを積む Presenter のサービス (M.E.DI ではそのスコープ) から `IViewMappingRegister` を引き、`WindowNavigator` と同じ方法でコントロールを作ります。コンテナーにだけ登録したビュー (コンストラクターに依存を持つもの) も作れます。

パンくずは View の仕事です。`PageHost` は各ページのタイトルを `PageTitles` に持ち、増減のたびに `PagesChanged` を出すので、View はそれを見てパンくずを描き直します。クリックは型付き `ViewAction<int>` (ペイロード = 階層、0 が最初のページ) にします。テストから `await view.Binder.DispatchAsync(...)` で同じ経路を叩けます。

```csharp
public static readonly ViewAction<int> Breadcrumb = Factory.Create<int>(nameof(Breadcrumb));

// View (UserControl)
_pageHost.PagesChanged += (s, e) => RebuildCrumbs();      // _pageHost.PageTitles から LinkLabel を作り直す
_binder.Add(PageNavigationDemoActions.Breadcrumb, crumb, c => level);   // 1 つのパンくずごとに
```

## ページを積む / 戻る

```csharp
public sealed class PageNavigationDemoPresenter : ControlPresenterBase<IPageNavigationDemoView>, IPageCloseGuard
{
    private readonly DemoCustomerStore _store;
    private readonly PageStack _pages;

    public PageNavigationDemoPresenter(DemoCustomerStore store)
    {
        _store = store;
        _pages = new PageStack(this, () => View.Pages);   // コンストラクターで作ってよい (View は最初の Push で読む)
    }

    protected override void RegisterViewActions()
        => Dispatcher.Register(PageNavigationDemoActions.Breadcrumb, (int level) => _pages.PopToCountAsync(level + 1));

    protected override void OnInitialize()      // Push は View があるとき (OnInitialize 以降) から
        => _pages.Push(new CustomerListPresenter(_store), new CustomerListParams { Open = OpenCustomer }, "顧客一覧");

    private void OpenCustomer(int customerId)
        => _pages.Push(new CustomerDetailPresenter(_store),
                       new CustomerDetailParams { CustomerId = customerId, OpenOrder = OpenOrder },
                       _store.Customer(customerId).Name);    // 3 つ目の引数 = パンくずのタイトル
}
```

| メンバー | 内容 |
|----------|------|
| `Push(page, title)` / `Push(page, parameters, title)` | ホストがページのビューを作り、ページを attach + 初期化して一番上に積む。ページは親のサービス (自分のサービスを持っていればそれ) を使い、親と一緒に破棄される。`true` = 一番上に載った。`false` = 確認中で何もしなかった、またはページが初期化の途中で終わった |
| `PopToCountAsync(n)` | 先頭 n ページを残して上を捨てる (パンくずのジャンプ)。捨てるページ全部が同意したときだけ。`true` = 外した (または外すものがなかった) |
| `PopAsync()` | 一番上を捨てる。空なら `true` |
| `CanCloseAllAsync()` | 全ページに確認するだけ (何も外さない)。[後述](#ページを持つパネルごと破棄するとき-cancloseallasync) |
| `Count` / `IsBusy` | ページ数 / 確認・削除の最中 |

外すときは、上から順に **Presenter を破棄 → ホストからビューを外す** (`RemovePage`、コントロールは破棄される) です (`AttachChild` のハンドルと同じ順)。どちらかが例外を投げても残りは続け、最後にまとめて投げます (1 つならそのまま、複数なら `AggregateException`)。

`Push` の失敗は次のとおりです。

- **ビューを作る前の拒否** (引数 null、親に View がない、ページが別の場所で使用中 …): 例外。何も作られず、Presenter も破棄されません。
- **ホストがビューを作れない** (登録がない、コントロールのコンストラクターが例外 …): 例外。ホストは元のまま。
- **作ったビューをページが受け取れない** (型が違う、ページのサービスが合わない): ホストからビューを外して例外。Presenter は破棄されません。
- **受け付けた後の失敗** (ページの `OnInitialize` が例外 …): ページを破棄し、ビューを 1 回だけ外してから例外を投げます。

## ページ側: IPageCloseGuard

捨てられたくない (未保存の編集がある) ページは `IPageCloseGuard` を **明示的に** 実装します (Presenter の公開面に出さない: [Rule 16](Design-Rules#rule-16-presenter-メソッドの可視性))。

```csharp
public sealed class OrderEditPresenter : ControlPresenterBase<IOrderEditView, OrderEditParams>, IPageCloseGuard
{
    Task<bool> IPageCloseGuard.CanCloseAsync()
        => Task.FromResult(!_tracker.IsChangedWith(Read()) || Messages.ConfirmYesNo("受注の変更を破棄しますか?", "受注編集"));
}
```

- 同期で答えるなら `Task.FromResult(...)`。保存を待つなら `async` で。
- **1 つの答えがすべての「捨てる」に使われます**: パンくずのジャンプ、ウィンドウを閉じる、親パネルの破棄。未保存確認のコードは 1 か所です。
- 実装しないページは常に捨ててよいページです (そのページが持つ入れ子のスタックは確認されます)。
- ウィンドウ Presenter はこれを実装せず、従来どおり `CanClose` / `CanCloseAsync(CloseReason)` をオーバーライドします。
- 拒否のあとでまた聞かれることがあります。確認の中で保存したものは、別のページが拒否しても取り消されません。
- 失敗した Task・キャンセル・`null` の Task・同期の例外は **拒否** として扱い、ログに残します。

## 確認の順序と「全部か無しか」

ジャンプ・ウィンドウクローズ・`CanCloseAllAsync` は同じ規則で確認します。

1. スタックは **上から** 確認します。
2. 各ページでは、**そのページが持つスタック (入れ子) を先に**、そのページ自身の確認を後にします。
3. 1 つずつ待ってから次に聞きます。**最初の拒否で止め、何も外しません。**

`[一覧, 詳細(中に [A, B]), 編集]` を全部捨てるなら、確認は `編集 → B → A → 詳細 → 一覧` の順です。

確認の最中に同じ確認が入れ子で呼ばれても (自分のスタックへ転送するパネルの確認など)、各ページは 1 回しか聞かれません。

## IsBusy — 確認中の Push は何もしない

確認や削除の最中、スタックは busy です。

- `Push` / `PopToCountAsync` / `PopAsync` / `CanCloseAllAsync` → すぐ `false` (待ち行列には入れません。確認ダイアログが開いている間の 2 回目のクリックは無視されます)。`Push` は何も作らず、渡した Presenter にも触れません。
- 例外は、確認の途中で **その確認が聞いているページ自身** が呼ぶ `CanCloseAllAsync` (自分のスタックへ転送するパネルの確認) です。これは今の確認に加わって答えます ([後述](#ページを持つパネルごと破棄するとき-cancloseallasync))。

呼ぶ側が `IsBusy` を先に調べる必要はありません。

`IsBusy` が止めるのは **スタックの操作だけ** です。ページの編集やほかのアクションは止めません。ジャンプの間ずっと入力を止めたい画面は、親で囲みます。

```csharp
using (ShowBusy()) await _pages.PopToCountAsync(level + 1);
```

## 結果を親へ返す: パラメーターのコールバック

ページから親への結果は、**ページのパラメーターに入れたコールバック** で返します ([Rule 10](Design-Rules#rule-10-view-にアクセスするのは-presenter-だけ) の例外)。

```csharp
public sealed class OrderEditParams
{
    public int OrderId;
    public Action<DemoOrder> Saved;   // 保存した受注
}

// 親
_pages.Push(new OrderEditPresenter(_store),
            new OrderEditParams { OrderId = orderId, Saved = OnOrderSaved }, "受注 " + orderId);
```

コールバックが運ぶのはアプリのデータだけで、子は親 Presenter も `PageStack` も知りません。**複数のページが見る状態** (一覧の集計など) はコールバックではなく Model / Service に置き、その変更イベントで各ページが更新します (サンプルでは `DemoCustomerStore.OrderSaved` で一覧と詳細が更新される)。

## 「また表示された」を知りたいとき

フレームワークは「上のページが閉じてまた表示された」を通知しません。必要なページは、自分の View の意味のあるイベントで受け取ります。

```csharp
// View: VisibleChanged を意味のあるイベントに
public event EventHandler Reshown;
// Presenter
SubscribeViewEvent(h => View.Reshown += h, h => View.Reshown -= h, Reload);
```

## ウィンドウを閉じるとき

ウィンドウの ✕ / Alt+F4 / `RequestClose` は、**先にツリー内の全ページに確認し**、全員が同意したときだけウィンドウ自身の `CanClose` / `CanCloseAsync` を呼びます (ページの拒否で閉じない)。対象は、ウィンドウが持つスタックと、`AttachChild` の子孫が持つスタックすべてです。

- システムのシャットダウン / タスクマネージャーはこれまでどおり誰にも聞きません。
- ジャンプの確認中に ✕ が来たら、聞かずに閉じません (2 つ目のダイアログは出ない)。
- 閉じることが決まってから実際に閉じるまで、スタックは押さえたままです (その間に積んだり戻ったりはできない)。別の `FormClosing` ハンドラーがキャンセルしたら解放されます。

`Connect` で WinForms ホストに載せた **コントロール Presenter** がスタックを持つ場合、上にウィンドウ Presenter がないのでウィンドウクローズには参加しません。ホスト側で `CanCloseAllAsync` を呼んでから外してください。

## ページを持つパネルごと破棄するとき: CanCloseAllAsync

タブを閉じる、動的パネルを外すなど、**祖先が `AttachChild` のハンドルでパネルを破棄する** ときは、破棄は止められない (同期・拒否なし) ので、先に聞きます。パネルは自分の確認をスタックへ転送します。

```csharp
sealed class CustomerWorkspacePanelPresenter : ControlPresenterBase<IWorkspacePanelView>, IPageCloseGuard
{
    Task<bool> IPageCloseGuard.CanCloseAsync() => _pages.CanCloseAllAsync();
}

// 祖先: 子 Presenter (の確認) をハンドルの隣に持っておき、破棄の前に
if (child is IPageCloseGuard guard && !await guard.CanCloseAsync()) return;
handle.Dispose();
```

サンプルアプリのシェルは、カテゴリを切り替える前にこの方法で各デモに聞いています (`Shell/ShellPresenter.cs`)。ウィンドウクローズやジャンプの中から転送された `CanCloseAllAsync` は、その確認の一部として動きます (各ページは 1 回だけ聞かれる)。

## 非同期の確認と ShowBusy

確認の中で保存を待つ (非モーダルな待ち) ときは、その待ちを `ShowBusy()` で囲みます。

```csharp
async Task<bool> IPageCloseGuard.CanCloseAsync()
{
    if (!IsDirty) return true;
    var answer = Messages.ConfirmYesNoCancel("変更を保存しますか?");
    if (answer == ConfirmResult.Cancel) return false;
    if (answer == ConfirmResult.No) return true;
    using (ShowBusy()) return await SaveAsync();
}
```

`ShowBusy()` はその確認が答えた時点で終わります。ウィンドウクローズでは各ページの確認とウィンドウ自身の `CanCloseAsync` が順に走り、その間フレームワーク全体の入力ロックはありません。**2 つの確認の間に別のページで行われた編集は聞き直しません。**

## ページが外から破棄されたとき

ページのコントロールが別の場所で破棄された (または Presenter が直接破棄された) ときは、スタックはそのページと **その上のページすべて** を、聞かずに外します。上のページは上から順に破棄 → ホストから外し、最後に破棄されたページ自身のビューも (その破棄が終わってから) 外します。パンくずはホストの `PagesChanged` で揃います。確認の最中なら、そのページは「もう捨てられた」ものとして扱い、残りの確認を続けます。

親 Presenter の破棄 (ウィンドウを閉じた) では、ページは一緒に破棄され、ビューはホストから外しません (Presenter の破棄は View を壊さない。コントロールはフォームと一緒に破棄される)。その後のスタックは使えません (`Push` は `ObjectDisposedException`、ほかは `false`)。

## テスト

普通の Presenter と同じく `PresenterTestHost` とモックの View で、本物の入口から叩きます。ホストは `Kibinago.Testing` の `FakePageHost` で、ビューの種類ごとにモックを登録します。

```csharp
// MockHostView: IPageHost Pages => Host; Host = new FakePageHost()
//     .Register<ICustomerListView>(() => new MockListView())
//     .Register<ICustomerDetailView>(() => new MockDetailView())
//     .Register<IOrderEditView>(() => new MockOrderView());
var view = new MockHostView();
using (PresenterTestHost.Start(fakes.Provider, new PageNavigationDemoPresenter(store), view))
{
    view.List.Open(1);
    view.Detail.Open(101);
    view.Order.Edit(99);
    fakes.Messages.EnqueueYesNo(false);                                  // 「破棄しますか?」→ いいえ

    await view.Binder.DispatchAsync(PageNavigationDemoActions.Breadcrumb, 0);

    Assert.Equal(3, view.Host.Pages.Count);                              // 何も外れていない
    Assert.Empty(view.Host.Removed);
}
```

`FakePageHost` は `Pages` (下から順)・`PageTitles`・`Removed` (外した順) を記録し、`Page<T>(level)` でその階層のモックを取り出せます。

ウィンドウクローズは `host.CanClose()` / `await host.CanCloseAsync()` (問い合わせ: 何も閉じないので答えたらスタックを解放) と、Presenter の `RequestClose` (受け付けた閉じる: `host.Dispose()` までスタックを押さえる) で確かめられます。サンプルのテスト: `tests/Kibinago.Samples.Tests/Samples/PageNavigationDemoPresenterTests.cs`。

## やらないこと (Prism との比較)

Prism WPF のリージョンナビゲーション (`RequestNavigate`、`INavigationAware`、`IConfirmNavigationRequest`、`KeepAlive`、ジャーナル) にあたるものですが、範囲を絞っています。

| Prism | Kibinago | 理由 |
|-------|----------|------|
| 進む / ジャーナル | なし | 「進む」はブラウザーを模した API (WPF `Frame`、Prism のジャーナル、Web ルーター) にしかありません。アプリのドリルダウン (ReactiveUI、MAUI `NavigationPage`、iOS / Android) にはなく、パンくずの意味は「右を捨てて戻る」です |
| `KeepAlive` | 選ばない | 下のページは常に生きたまま、捨てたページは常に破棄。選ぶものがありません |
| 戻ったときの通知 (`OnNavigatedTo`) | なし | 結果はコールバック、「また表示された」は View のイベント (上記) |
| リージョン名・名前での View 解決・パラメーターでの再利用 | なし | Presenter は親が `new` し、コントロールは `PageHost` がビューの登録 (ウィンドウと同じ `IViewMappingRegister`、型での明示的な登録) から作る |
| 確認 (`IConfirmNavigationRequest`) | `IPageCloseGuard` (1 メソッド) | ウィンドウクローズと親パネルの破棄にも同じ答えを使う |

## 自分のレイアウトで表示する

`PageHost` は「一番上のページだけを全面に表示する」パネルです。タブに並べるなど別の見せ方をしたいときは、`IPageHost` (`AddPage` / `RemovePage` の 2 メソッド) を自分で実装して View インターフェイスで公開します。`AddPage` は新しいビューを作って返し、作れないときは何も変えずに例外を投げます。

## パンくずのコントロールを替える

パンくずは View の実装です。`LinkLabel` の並びを AntdUI の `Breadcrumb` などに替えても、変わるのは UserControl だけです (クリックを `Breadcrumb` アクションの `level` にする)。Presenter とテストはそのままです。

## 関連ページ

- [マスター/詳細パターン](HowTo-Implement-Master-Detail)
- [Presenter 基底クラス](Reference-Presenter-Base-Classes) (`AttachChild`)
- [ウィンドウを閉じる処理](HowTo-Handle-Window-Closing)
- [ChangeTracker](Reference-ChangeTracker)
- [MVP 設計ルール](Design-Rules) (Rule 10 のコールバックの例外)
