# トースト通知

非ブロッキングで一時的に表示される通知 (トースト) の完全リファレンスです。Presenter からは
`Toasts.Show(...)` (= `IToastService`) で画面の隅に**積み上がる**トーストを出します。View からは
1 回ごとの外観指定ができる `IWinFormsToastService` と、特定の画面座標に**単一の**トーストを出す
`AnchoredToast` も使えます。

> **2 つのインターフェイス:** `IToastService` (`Kibinago.Core`) は Presenter 用で
> `Show(text, type[, duration])` だけを持ちます。`IWinFormsToastService : IToastService`
> (`Kibinago` パッケージ) は `ToastOptions` / 画面座標といった WinForms 型を取るオーバーロードを
> 追加した View 用です。既定の `IToastService` 登録は `IWinFormsToastService` に転送するため、
> Presenter と View のトーストは 1 つのスタックを共有します。`IMessageService` にはトーストの
> メンバーはありません。

> **OpenForms に現れない:** トーストは `Form` ではなく、レイヤード Win32 ポップアップをラップした
> `NativeWindow` です。ネイティブ MessageBox と同じく `Application.OpenForms` には現れないため、
> その集合を列挙するホストコードがトーストの表示/自動クローズで乱されることはありません。

---

## 1. Presenter から: 隅に積み上がるトースト

Presenter は `Toasts` プロパティ (= `IToastService`) 経由でトーストを出します。座標は扱いません。

```csharp
// 既定の外観 (起動時の設定。未設定なら右下、350x80、Segoe UI 10pt、3 秒)
Toasts.Show("保存しました", ToastType.Success);

// 表示時間だけ指定 (null = 設定された既定値)
Toasts.Show("接続が切れました", ToastType.Warning, duration: 5000);
```

Presenter から 1 回ごとに変えられるのは表示時間だけです。位置・サイズ・フォントなどの外観は
起動時にアプリ全体で設定します (3 章)。1 回ごとに外観を変えたいのは View 側のコードで、
`IWinFormsToastService` を使います (2 章)。

> トーストは常に UI スレッドで生成されます。ワーカースレッドから呼んでも UI スレッドへポストされます。
> UI スレッドがまだ無い (メッセージループ開始前) / もう無い (アプリ終了後) 場合、トーストは
> 破棄されて警告がログに出ます。呼び出し側が例外で失敗することはありません。

`ToastType` は `Info` / `Success` / `Warning` / `Error`。背景色とアイコンを決めます。

### 積み上げの挙動

- 新しいトーストが画面の隅 (既定は右下) に出て、既存のものは中央方向へ押し上げられます。
- いずれかが消える (時間切れ or クリック) と、残りが詰めて再配置されます。
- **隅ごとに独立**して積み上がります。右下と左上は互いに干渉しません。
- 各トーストの**実際の高さ**を積算して並べるので、サイズが違っても重なりません。
- 同時表示数は `mvp.Toasts.MaxVisibleToasts` (既定 5) が上限。超えると最古のものから即座に閉じます。
- 本文がトースト枠に収まらない場合は末尾を省略記号 (`…`) で切り詰めます。

---

## 2. ToastOptions — 1 回ごとの上書き (View: `IWinFormsToastService`)

`ToastOptions` は `IWinFormsToastService.Show(text, type, options)` に渡します。View
(Form / UserControl) のコードで、アプリのトーストサービスを取得して使います。

```csharp
private readonly IWinFormsToastService _toasts =
    ServiceLocator.Current.ResolveRequired<IWinFormsToastService>();

// 外観を個別指定 (位置・サイズ・フォント・表示時間)
_toasts.Show("アップロード完了", ToastType.Info, new ToastOptions
{
    Position = ToastPosition.TopRight,
    Duration = 6000,
});
```

各プロパティは null 許容で、**未設定なら起動時に設定した既定値 (3 章) にフォールバック**します。

| プロパティ | 型 | 内容 |
|-----------|-----|------|
| `Position` | `ToastPosition?` | 表示する画面の隅 |
| `Size` | `Size?` | トーストのサイズ (px) |
| `Font` | `Font` | 本文フォント (参照型なので null = 既定) |
| `Duration` | `int?` | フェード開始までのミリ秒 |
| `Style` | `ToastStyle?` | 組み込みスタイル（7 章参照）。`Renderer` 設定時は無視 |
| `ShowCloseButton` | `bool?` | 閉じる × を表示するか（表示のみ） |
| `AutoHeight` | `bool?` | 高さを内容に合わせる（幅は `Size.Width` で固定。7 章参照） |
| `MinHeight` / `MaxHeight` | `int?` | オート高さの下限／上限 (px) |

> `ToastOptions` は `Show` が戻る前にコピーされます (`Font` は複製)。そのため、渡したフォントは
> `Show` の直後に破棄してかまいません。一方 `mvp.Toasts.Font` に設定したフォントは呼び出し側の
> 所有で、アプリ終了まで生存させる必要があります。

```csharp
using (var bigFont = new Font("Yu Gothic UI", 14f, FontStyle.Bold))
{
    _toasts.Show("大きめの通知", ToastType.Info, new ToastOptions
    {
        Size = new Size(420, 110),
        Font = bigFont,
        Position = ToastPosition.BottomLeft,
    });
}
```

### ToastPosition

```csharp
public enum ToastPosition { TopLeft, TopRight, BottomLeft, BottomRight }
```

上の隅は下方向へ、下の隅は上方向へ積み上がります。

---

## 3. `mvp.Toasts` — アプリ全体の既定値

起動時の設定 (`MvpApplication.Configure(framework: mvp => ...)` または M.E.DI の
`AddKibinago(viewRegistry, mvp => ...)`) で一度だけ設定します。値は `ToastHostOptions` として
登録され、トーストサービスの生成時に検証・コピーされます (以後の変更は効きません)。外観系
(`Position` / `Size` / `Font` / `Duration`) は `ToastOptions` で 1 回ごとに上書きできますが、
積み上げポリシー系 (`Margin` / `Gap` / `MaxVisibleToasts` / `Opacity`) はスタック全体に効くため
ここでのみ設定します。設定入口の全体は [フレームワークをカスタマイズする](HowTo-Customize-Framework) を参照してください。

| プロパティ | 既定値 | 内容 |
|-----------|--------|------|
| `Position` | `BottomRight` | 表示する画面の隅 |
| `Size` | `350 x 80` | トーストのサイズ (px) |
| `Font` | `Segoe UI 10pt` | 本文フォント |
| `Duration` | `3000` | フェード開始までのミリ秒 |
| `Style` | `Soft` | 組み込みスタイル（`Soft` / `Card` / `Solid`） |
| `ShowCloseButton` | `true` | 閉じる × を既定で表示するか |
| `AutoHeight` | `false` | 高さを内容に合わせる（幅は固定） |
| `MinHeight` | `40` | オート高さの下限 (px) |
| `MaxHeight` | `220` | オート高さの上限 (px) |
| `Margin` | `20` | 画面端からの余白 (px) |
| `Gap` | `10` | トースト間の縦の間隔 (px) |
| `MaxVisibleToasts` | `5` | 同時表示数の上限 |
| `Opacity` | `0.95` | 表示中の不透明度 (0 より大きく 1 以下) |

```csharp
// 例: アプリ起動時に既定を変更 (Program.cs)
MvpApplication.Configure(framework: mvp =>
{
    mvp.Toasts.Position = ToastPosition.TopRight;
    mvp.Toasts.Duration = 4000;
    mvp.Toasts.MaxVisibleToasts = 3;
});
```

不正な値 (`Duration <= 0`、`MaxHeight < MinHeight` など) はトーストサービスの生成時に
`ArgumentException` になります。

トーストサービス自体を差し替える場合は `mvp.Toasts.Use(IWinFormsToastService)` を使います。
Presenter の `Toasts` (`IToastService`) と View の `IWinFormsToastService` / `AnchoredToast` の
両方が差し替え後のサービスを使います。

---

## 4. View から: 座標にアンカーする単一トースト (`AnchoredToast`)

特定の画面座標 (コントロールの位置、カーソル位置など) にトーストを出したい場合に使う
**View 層専用**ユーティリティです。

```csharp
// Form / UserControl のコードから
AnchoredToast.Show("ここに表示", ToastType.Info, Cursor.Position);

// コントロールのすぐ下に
var anchor = someButton.PointToScreen(new Point(0, someButton.Height + 4));
AnchoredToast.Show("このボタンの説明", ToastType.Info, anchor);

// 種類ごとの便捷メソッド (ShowInfo / ShowSuccess / ShowWarning / ShowError)
AnchoredToast.ShowSuccess("保存しました", anchor);
```

- **単一**: 同時に存在できるのは 1 つだけ。もう一度呼ぶと前のものを閉じます。隅に積み上がる
  トースト群とは独立しており、互いに干渉しません。
- **tooltip 式の配置**: アンカーから右下方向へ広がり、はみ出すならアンカーの反対側へ反転。
  最後に必ず画面内へ**裁き込む**ため、画面外や負の座標を渡しても全体が見える位置に収まります。
- `ToastOptions` の `Position` (隅) は無視されます (アンカー点が位置を決めるため)。
  `Size` / `Font` / `Duration` は有効です。
- `AnchoredToast` はアプリのトーストサービスの `IWinFormsToastService.ShowAnchored(...)` へ転送する
  静的ショートカットです。`mvp.Toasts.Use(...)` で差し替えたサービスもアンカー付きトーストを受け取ります。

> **Presenter からは呼ばないでください。** 画面座標を知っているのは View だけです。Presenter が
> トーストを出したい場合は隅に積み上がる `Toasts.Show(...)` を使います。

---

## 5. 関連: 座標指定の MessageBox (`AnchoredMessageBox`)

MessageBox を特定座標に出す View 層ユーティリティです (`AnchoredToast` の MessageBox 版)。
こちらはモーダルで `DialogResult` を返します。Presenter の `Messages` と同じ
`IMessageDialogBackend` を通るため、`mvp.Messages.UseDialogs(...)` で差し替えた見た目が使われます。詳細は
[Platform Services](Reference-Platform-Services) を参照してください。座標が画面端に近くても、
ダイアログ全体が見えるよう自動で引き戻します。

---

## 6. カスタム描画 (`ToastRenderer`)

トーストの見た目は `ToastRenderer` で差し替えられます (`ToolStrip.Renderer` と同じ発想)。
描画ロジックだけを担当し、位置・サイズ・積み上げ・単一化・フェード・裁き込みは引き続き
フレームワークが管理します。

```csharp
public abstract class ToastRenderer
{
    public abstract void Render(ToastRenderContext context);
}

public sealed class ToastRenderContext
{
    public Graphics Graphics { get; }   // 描画先
    public Rectangle Bounds { get; }    // 描画領域 (原点 0,0)
    public string Message { get; }
    public ToastType Type { get; }
    public Font Font { get; }
    public int CornerRadius { get; }       // 角丸半径 (px、0 = 方角)
    public bool ShowCloseButton { get; }   // 閉じる × を描くか
}
```

差し替え位置 (外観パラメータと同じく、単一 + アプリ全体):

```csharp
mvp.Toasts.UseRenderer(new MyRenderer());           // アプリ全体 (起動時の framework: ブロック)
_toasts.Show("...", ToastType.Info, new ToastOptions { Renderer = new MyRenderer() }); // 1 回だけ (View)
```

未指定なら `DefaultToastRenderer` (内蔵: 背景色 + アイコン + 本文 + 閉じる × + 省略記号) が使われます。

- **配色やアイコンだけ変えたい** → `DefaultToastRenderer` を継承し
  `GetBackgroundColor(ToastType)` / `GetIcon(ToastType)` を override。
- **完全に自前で描きたい** (進捗バー、画像、角丸など) → `ToastRenderer` を継承し
  `Render` をゼロから実装。

```csharp
// 例: 暗いカード + 左アクセントバー
public sealed class DarkCardToastRenderer : ToastRenderer
{
    public override void Render(ToastRenderContext c)
    {
        c.Graphics.Clear(Color.FromArgb(45, 45, 48));
        using (var bar = new SolidBrush(Color.FromArgb(86, 156, 255)))
        using (var text = new SolidBrush(Color.WhiteSmoke))
        using (var f = new StringFormat { LineAlignment = StringAlignment.Center })
        {
            c.Graphics.FillRectangle(bar, 0, 0, 6, c.Bounds.Height);
            c.Graphics.DrawString(c.Message, c.Font, text,
                new RectangleF(18, 8, c.Bounds.Width - 28, c.Bounds.Height - 16), f);
        }
    }
}
```

> サンプル `ToastDemo` の「Custom renderer (owner-draw)」ボタンがこの例を動かします。

---

## 7. 組み込みスタイルと閉じるボタン

トーストには 3 つの組み込み外観があり、`ToastStyle` で選びます。`ToastRenderer` を自前で書かずに見た目を切り替えられます。**既定は `Soft`** です。

| スタイル | 形状 | 外観 |
|---------|------|------|
| `Soft` | 角丸 | 淡い色背景・色付き丸アイコン・濃い文字（**既定**） |
| `Card` | 角丸 | 白背景・左の色アクセントバー・色付き丸アイコン・濃い文字 |
| `Solid` | 方角 | 単色背景・白文字・左アイコン（従来の外観） |

```csharp
// 1 回だけ (View)
_toasts.Show("保存しました", ToastType.Success, new ToastOptions { Style = ToastStyle.Card });
// アプリ全体の既定 (起動時)
mvp.Toasts.Style = ToastStyle.Solid;
```

角丸の形状は `ToastRenderer.CornerRadius` でレンダラー自身が持ちます（`Solid` は 0 = 方角）。`Renderer` を明示設定すると `Style` より優先されます。解決順（具体的なものが優先）:
`ToastOptions.Renderer` → `ToastOptions.Style` → `mvp.Toasts.UseRenderer(...)` → `mvp.Toasts.Style`。

組み込みの `SoftToastRenderer` / `CardToastRenderer` は `public` で、`DefaultToastRenderer`（`Solid` スタイルを描画）と同様に `protected virtual` の配色/アイコン用フックを持ちます。継承すればレイアウトを書き換えずに色やアイコンだけ変えられます。

### 高さのオートサイズ

`AutoHeight` を有効にすると、幅は固定のまま高さが内容に合わせて伸縮します（単行はコンパクト、複数行は伸びる）。高さは `MinHeight`..`MaxHeight` でクランプされ、`MaxHeight` を超える分は末尾を省略記号にします。

```csharp
_toasts.Show(longText, ToastType.Info, new ToastOptions { AutoHeight = true });
// アプリ全体 (起動時)
mvp.Toasts.AutoHeight = true;
mvp.Toasts.MaxHeight = 260;
```

スタッキングは各トーストの実高さで積み重なるため、可変高さでも重なりません。カスタムレンダラーは `MeasureHeight` を override して自分のレイアウトに合った高さを返せます（既定は本文の折り返し高さを計測）。

### 閉じる (×) ボタンの表示

閉じる × は**表示のみ**で、描画の有無に関わらずトーストはどこをクリックしても閉じます。表示の ON/OFF:

```csharp
// 1 回だけ (View)
_toasts.Show("× なし", ToastType.Info, new ToastOptions { ShowCloseButton = false });
// アプリ全体 (起動時)
mvp.Toasts.ShowCloseButton = false;
```

---

## まとめ: どれを使うか

| やりたいこと | 使うもの | 呼ぶ場所 |
|------------|---------|---------|
| 隅に出る非ブロッキング通知 | `Toasts.Show(...)` | Presenter |
| 既定の外観をアプリ全体で変える | `mvp.Toasts` | 起動時 |
| 1 回だけ外観を変える | `IWinFormsToastService.Show(text, type, ToastOptions)` | View |
| 組み込みスタイルを選ぶ | `ToastOptions.Style` / `mvp.Toasts.Style` | 呼び出し時 or 起動時 |
| 座標を指定した単一トースト | `AnchoredToast.Show(...)` / `IWinFormsToastService.ShowAnchored(...)` | View |
| 座標を指定した MessageBox | `AnchoredMessageBox` | View |
| 見た目を自前で描く | `ToastRenderer` / `DefaultToastRenderer` (`mvp.Toasts.UseRenderer`) | 起動時 or 呼び出し時 |
| トーストサービスごと差し替える | `mvp.Toasts.Use(...)` | 起動時 |

関連ページ: [Platform Services](Reference-Platform-Services) / [フレームワークをカスタマイズする](HowTo-Customize-Framework) / [エラー処理戦略](HowTo-Handle-Errors)
