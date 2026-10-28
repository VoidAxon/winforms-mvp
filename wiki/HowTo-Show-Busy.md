# 処理中を示す (`ShowBusy`)

時間のかかる処理の間、画面が「処理中で操作できない」ことを示します。WinForms では View に `Cursor = Cursors.WaitCursor` / `finally { Cursor = Cursors.Default; }` を書くのが定番ですが、Presenter は WinForms に触れないので、`ShowBusy()` を使います。

## 目次

- [使い方](#使い方)
- [何が起きるか](#何が起きるか)
- [オーバーレイ (`mvp.Busy.UseOverlay`)](#オーバーレイ-mvpbusyuseoverlay)
- [見た目を変える (`BusyRenderer`)](#見た目を変える-busyrenderer)
- [丸ごと差し替える](#丸ごと差し替える)
- [テスト](#テスト)
- [注意点](#注意点)

## 使い方

```csharp
// 同期処理 (UI スレッドでデータベースを呼ぶ)
private void OnSearch()
{
    using (ShowBusy())
    {
        View.Rows = _repository.Search(View.Condition);
    }
}

// 非同期処理: await の間も Busy のまま
Dispatcher.Register(Actions.Load, async ct =>
{
    using (ShowBusy())
    {
        View.Rows = await _repository.LoadAsync(ct);
    }
});
```

- `ShowBusy()` は Presenter の protected メソッドです。戻り値を `Dispose` すると終わります (`using`)。
- 入れ子にでき、どの順で終わっても構いません。2 回目の `Dispose` は何もしません。
- Presenter が破棄されると (ウィンドウを閉じた、コントロールが破棄された)、終わっていない Busy もそこで終わります。
- View がアタッチされてから (`OnViewAttached` 以降)、UI スレッドで呼びます。戻り値の `Dispose` はどのスレッドからでも構いません。

## 何が起きるか

既定 (設定なし) では:

| | 内容 |
|---|---|
| 待ちカーソル | View のウィンドウ全体。同期処理で UI スレッドが止まっていても、すぐに出る |
| 入力のブロック | View のウィンドウ全体が、マウスのボタン・ホイール・キー・Alt+ニーモニック・スクロールバーを受け付けない。コントロールの見た目は変わらない (灰色にならない) |
| 同期処理中の連打 | 処理中にたまったクリックやキーは、終わったあとに実行されず捨てられる |
| 使えるもの | タイトルバー (移動・×)、Alt+F4 (閉じるときは `CanClose` を通る)、途中で出したダイアログ (`Messages`)、ほかのウィンドウ |

- 「ウィンドウ」は View を含むフォームです。子パネル (UserControl + 自分の Presenter) から `ShowBusy()` を呼んでも、そのフォーム全体が止まります。
- MDI 子フォームでは、その子フォームだけが止まります。MDI 親のメニューやほかの子フォームは使えます (DevExpress / Telerik のオーバーレイと同じ)。
- 開いていたドロップダウン (コンボボックス、メニュー) は Busy の開始時に閉じます。
- 確定前の日本語入力 (IME の変換中の文字) は Busy の開始時に取り消します (候補ウィンドウから確定されないように)。

## オーバーレイ (`mvp.Busy.UseOverlay`)

起動時に 1 行加えると、Busy の View に重ねて表示も出ます。

```csharp
MvpApplication.Configure(framework: mvp =>
{
    mvp.Busy.UseOverlay();     // 既定の見た目: View を白く薄め、中央の白い角丸四角にリング
    mvp.Busy.Delay = 150;      // オーバーレイは 150ms 後から (短い処理でちらつかない)。既定 150
});
```

- 既定の見た目 (`HudBusyRenderer`) は文字を出しません。リングは Windows 11 の ProgressRing と同じ動きです。
- 背景の色と濃さは `Backdrop` で変えられます (色のアルファが濃さ。既定は白、115/255)。完全な透明 (`Color.Transparent`) にすると背景を出さず、中央の四角だけになります (View 全体を覆うウィンドウも作りません)。

```csharp
mvp.Busy.UseOverlay(new HudBusyRenderer { Backdrop = Color.FromArgb(60, 0, 0, 0) });   // 少し暗くする
mvp.Busy.UseOverlay(new HudBusyRenderer { Backdrop = Color.Transparent });             // 背景なし
```

- 背景があってもなくても、入力のブロックは同じです (ブロックはメッセージフィルターが行う)。違いはマウスを乗せたときの見た目だけで、背景があると下のボタンが光らず、ないと光ります (押しても何も起きません)。
- オーバーレイは **View のコントロールではなく、別のウィンドウ** (フォームが所有者の、アクティブにならない層付きウィンドウ) です。View の `Controls`・レイアウト・Z 順は変わらず、`Application.OpenForms` にも出ません。
- View とその親の移動・サイズ変更・表示切り替えに追従し、親で隠れた部分 (スクロールで外れた部分、MDI の表示域の外) は覆いません。
- 子パネルの Busy では、オーバーレイはそのパネルの上だけです (どこを読み込み中かが分かる。操作はフォーム全体で止まる)。
- フォームが最小化・非表示のとき、モーダルダイアログでフォームが無効になっているとき (ダイアログがオーバーレイの下に隠れないように)、MDI 子フォームがアクティブでないときは隠れます。
- 同期処理で UI スレッドが止まっている間は、遅延のタイマーもアニメーションも動かないため、オーバーレイは出ません (待ちカーソルだけ)。オーバーレイを見せたい処理は非同期にします。

## 見た目を変える (`BusyRenderer`)

`BusyRenderer` を継承して `mvp.Busy.UseOverlay(new MyRenderer())` に渡します。オーバーレイは 2 枚の透明な面です。

| メンバー | 役割 | 描くタイミング |
|---|---|---|
| `HasBackdrop` / `PaintBackdrop` | View 全体を覆う背景 (薄める、暗くする、何もしない) | View の大きさや位置が変わったとき |
| `GetIndicatorBounds` | 表示部分の位置と大きさ (View の座標)。`Rectangle.Empty` で出さない | 大きさや文字が変わったとき |
| `PaintIndicator` | 表示部分 (スピナー、ロゴ、文字) | 約 25 回/秒 |

```csharp
public sealed class StatusRenderer : BusyRenderer
{
    // 上端に文字付きの帯 (背景なし)
    public override Rectangle GetIndicatorBounds(BusyLayoutContext c) =>
        new Rectangle(0, 0, c.ViewSize.Width, (int)(28 * c.Scale));

    public override void PaintIndicator(BusyPaintContext c)
    {
        using (var band = new SolidBrush(Color.FromArgb(230, 255, 248, 220)))
            c.Graphics.FillRectangle(band, c.Bounds);
        var angle = (float)(c.Elapsed.TotalMilliseconds * 0.4 % 360);
        using (var pen = new Pen(c.Accent, 2 * c.Scale))
            c.Graphics.DrawArc(pen, 8 * c.Scale, 6 * c.Scale, 16 * c.Scale, 16 * c.Scale, angle, 270);
        c.DrawText(c.Message ?? "処理中…", new RectangleF(32 * c.Scale, 5 * c.Scale, c.Bounds.Width - 40 * c.Scale, 20 * c.Scale),
            SystemColors.WindowText, maxLines: 1, alignment: StringAlignment.Near);
    }
}
```

- 透明度 (アルファ) は 1 ピクセルごとに効きます。半透明の色は下の View を透かします。
- 文字は `context.DrawText` で描きます (GDI の `TextRenderer` は透明度を壊します)。長い文字は行数で切って「…」。
- アニメーションは `Elapsed` (表示してからの時間) から計算します。タイマーが遅れても速度が変わりません。
- `ShowBusy("集計中…")` の文字は `Message` で受け取れます。入れ子では、終わっていない中で最後の文字が使われます。
- 描画中の例外はログに記録され、そのオーバーレイは消えます (Busy 自体 — カーソルと入力のブロック — は続きます)。

## 丸ごと差し替える

ステータスバーのスピナーなど、まったく別の出し方にするなら `IBusyIndicator` (Core) を実装して `mvp.Busy.Use(...)` に渡します。`Begin(view, message)` が返す `IDisposable` で終わります。

## テスト

```csharp
var fakes = TestServices.CreateFakes();
using (PresenterTestHost.Start(fakes.Provider, presenter, view))
{
    var run = view.Binder.DispatchAsync(Actions.Load);
    Assert.True(fakes.Busy.IsBusyFor(view));          // 処理中
    Assert.Equal("集計中…", fakes.Busy.Calls[0].Message);
    gate.SetResult(true);
    await run;
    Assert.False(fakes.Busy.IsBusy);                   // 終わった
}
```

`TestServices.Create()` (最小構成) では `ShowBusy` は何もしません (フェイクの登録は不要)。

## 注意点

- `await` のあと View に触れる前に `Lifetime` を確認します (ウィンドウが閉じられていることがある)。`ShowBusy` の `using` はそのまま抜けて構いません。
- 中止ボタンなど、Busy 中も押せるべきコントロールはまだ指定できません (ウィンドウ全体が止まります)。
- `IMessageDialogBackend` を Form で自作すると、そのダイアログは `Application.OpenForms` に出ます (既定の `MessageBox` と `AnchoredMessageBox`、トースト、Busy オーバーレイは出ません)。

## 関連ページ

- [非同期処理を扱う](HowTo-Handle-Async-Operations)
- [フレームワークをカスタマイズする](HowTo-Customize-Framework)
- [プラットフォームサービス](Reference-Platform-Services)
