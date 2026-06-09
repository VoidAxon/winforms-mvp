# トースト通知

非ブロッキングで一時的に表示される通知 (トースト) の完全リファレンスです。Presenter からは
`IMessageService.ShowToast(...)` で画面の隅に**積み上がる**トーストを出します。View からは
特定の画面座標に**単一の**トーストを出す `AnchoredToast` も使えます。

> **OpenForms に現れない:** トーストは `Form` ではなく、レイヤード Win32 ポップアップをラップした
> `NativeWindow` です。ネイティブ MessageBox と同じく `Application.OpenForms` には現れないため、
> その集合を列挙するホストコードがトーストの表示/自動クローズで乱されることはありません。

---

## 1. Presenter から: 隅に積み上がるトースト

Presenter は `Messages` (= `IMessageService`) 経由でトーストを出します。座標は扱いません。

```csharp
// 既定の外観 (右下、350x80、Segoe UI 10pt、3 秒)
Messages.ShowToast("保存しました", ToastType.Success);

// 表示時間だけ指定 (旧シグネチャ。後方互換)
Messages.ShowToast("接続が切れました", ToastType.Warning, duration: 5000);

// 外観を個別指定 (位置・サイズ・フォント・表示時間)
Messages.ShowToast("アップロード完了", ToastType.Info, new ToastOptions
{
    Position = ToastPosition.TopRight,
    Duration = 6000,
});
```

`ToastType` は `Info` / `Success` / `Warning` / `Error`。背景色とアイコンを決めます。

### 積み上げの挙動

- 新しいトーストが画面の隅 (既定は右下) に出て、既存のものは中央方向へ押し上げられます。
- いずれかが消える (時間切れ or クリック) と、残りが詰めて再配置されます。
- **隅ごとに独立**して積み上がります。右下と左上は互いに干渉しません。
- 各トーストの**実際の高さ**を積算して並べるので、サイズが違っても重なりません。
- 同時表示数は `ToastDefaults.MaxVisibleToasts` (既定 5) が上限。超えると最古のものから即座に閉じます。
- 本文がトースト枠に収まらない場合は末尾を省略記号 (`…`) で切り詰めます。

---

## 2. ToastOptions — 1 回ごとの上書き

各プロパティは null 許容で、**未設定なら `ToastDefaults` の値にフォールバック**します。

| プロパティ | 型 | 内容 |
|-----------|-----|------|
| `Position` | `ToastPosition?` | 表示する画面の隅 |
| `Size` | `Size?` | トーストのサイズ (px) |
| `Font` | `Font` | 本文フォント (参照型なので null = 既定) |
| `Duration` | `int?` | フェード開始までのミリ秒 |

> `Font` は**呼び出し側の所有**で、トースト側では破棄しません。`ToastDefaults.Font` や
> ここで渡したフォントは複数のトーストで安全に使い回せます。

```csharp
using (var bigFont = new Font("Yu Gothic UI", 14f, FontStyle.Bold))
{
    Messages.ShowToast("大きめの通知", ToastType.Info, new ToastOptions
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

## 3. ToastDefaults — アプリ全体の既定値

起動時に一度設定する静的クラスです (`DialogDefaults` と同じパターン)。外観系
(`Position` / `Size` / `Font` / `Duration`) は `ToastOptions` で 1 回ごとに上書きできますが、
積み上げポリシー系 (`Margin` / `Gap` / `MaxVisibleToasts` / `Opacity`) はスタック全体に効くため
ここでのみ設定します。

| プロパティ | 既定値 | 内容 |
|-----------|--------|------|
| `Position` | `BottomRight` | 表示する画面の隅 |
| `Size` | `350 x 80` | トーストのサイズ (px) |
| `Font` | `Segoe UI 10pt` | 本文フォント |
| `Duration` | `3000` | フェード開始までのミリ秒 |
| `Margin` | `20` | 画面端からの余白 (px) |
| `Gap` | `10` | トースト間の縦の間隔 (px) |
| `MaxVisibleToasts` | `5` | 同時表示数の上限 |
| `Opacity` | `0.95` | 表示中の不透明度 (0..1) |

```csharp
// 例: アプリ起動時に既定を変更
ToastDefaults.Position = ToastPosition.TopRight;
ToastDefaults.Duration = 4000;
ToastDefaults.MaxVisibleToasts = 3;
```

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

> **Presenter からは呼ばないでください。** 画面座標を知っているのは View だけです。Presenter が
> トーストを出したい場合は隅に積み上がる `IMessageService.ShowToast(...)` を使います。

---

## 5. 関連: 座標指定の MessageBox (`AnchoredMessageBox`)

ネイティブ MessageBox を特定座標に出す View 層ユーティリティです (`AnchoredToast` の MessageBox 版)。
こちらはモーダルで `DialogResult` を返します。詳細は
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
}
```

差し替え位置 (外観パラメータと同じく、単一 + アプリ全体):

```csharp
ToastDefaults.Renderer = new MyRenderer();          // アプリ全体
Messages.ShowToast("...", ToastType.Info, new ToastOptions { Renderer = new MyRenderer() }); // 1 回だけ
```

未指定なら `DefaultToastRenderer` (内蔵: 背景色 + アイコン + 本文 + 閉じる × + 省略記号) が使われます。

- **配色やアイコンだけ変えたい** → `DefaultToastRenderer` を継承し
  `GetBackgroundColor(ToastType)` / `GetIcon(ToastType)` を override。
- **完全に自前で描きたい** (進捗バー、画像、角丸など) → `ToastRenderer` を継承し
  `Render` をゼロから実装。

```csharp
// 例: 暗いカード + 左アクセントバー
public sealed class CardToastRenderer : ToastRenderer
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

## まとめ: どれを使うか

| やりたいこと | 使うもの | 呼ぶ場所 |
|------------|---------|---------|
| 隅に出る非ブロッキング通知 | `Messages.ShowToast(...)` | Presenter |
| 既定の外観をアプリ全体で変える | `ToastDefaults` | 起動時 |
| 1 回だけ外観を変える | `ToastOptions` | 呼び出し時 |
| 座標を指定した単一トースト | `AnchoredToast.Show(...)` | View |
| 座標を指定した MessageBox | `AnchoredMessageBox` | View |
| 見た目を自前で描く | `ToastRenderer` / `DefaultToastRenderer` | 起動時 or 呼び出し時 |

関連ページ: [Platform Services](Reference-Platform-Services) / [エラー処理戦略](HowTo-Handle-Errors)
