# レート制限 (Debounce / Throttle)

高頻度に発生するイベント (キー入力・スクロール・ドラッグ・リサイズなど) を間引くための 2 つのユーティリティ、`Debouncer` と `Throttler` を提供します。いずれも `WinformsMVP.Common` 名前空間にあり、**特定の UI フレームワークに依存しません** (WinForms / WPF いずれでも同じコードで動作)。

> **位置づけ**: 「入力のたびに重い処理が走る」「ドラッグ中に更新が殺到する」といった状況を、UI スレッドに安全な形で抑制します。`ViewActionBinder` には組み込まず、必要な箇所で個別に組み合わせる独立した部品です (コア原語を単純に保つ方針)。

---

## 目次

- [Debounce と Throttle の違い](#debounce-と-throttle-の違い)
- [UI フレームワーク非依存の仕組み](#ui-フレームワーク非依存の仕組み)
- [基本的な使い方](#基本的な使い方)
- [RateLimit ファクトリ](#ratelimit-ファクトリ)
- [操作の一覧 (Signal / Cancel / Flush / Dispose)](#操作の一覧-signal--cancel--flush--dispose)
- [ライフサイクルと破棄](#ライフサイクルと破棄)
- [バックプレッシャー (積み上がり・並行実行の防止)](#バックプレッシャー-積み上がり並行実行の防止)
- [画面クローズ後に「幽霊コールバック」が走らない保証](#画面クローズ後に幽霊コールバックが走らない保証)
- [エラー処理](#エラー処理)
- [主要 API](#主要-api)
- [関連ページ](#関連ページ)

---

## Debounce と Throttle の違い

| | **Debounce** (`Debouncer`) | **Throttle** (`Throttler`) |
|---|---|---|
| 発火タイミング | 最後のシグナルから `delay` 経過後に **1 回** | `interval` ごとに **最大 1 回** (先頭 + 末尾) |
| 連続入力中の挙動 | 入力が止まるまで何も起きない | 一定間隔で定期的に発火し続ける |
| 典型用途 | 検索ボックスの「入力が止まったら検索」、入力値の検証 | スクロール・ドラッグ・リサイズ中の定期更新 |

`Throttler` は **先頭 (leading) + 末尾 (trailing)** セマンティクスです。最初のシグナルは即座に発火し、ウィンドウ期間中に来たシグナルはまとめて 1 回、ウィンドウ終端で発火します (最新の状態を取りこぼしません)。

---

## UI フレームワーク非依存の仕組み

内部では BCL の `System.Threading.Timer` のみを使用します (WinForms の `Timer` ではありません)。タイマーのコールバックはスレッドプール上で走るため、そのままでは UI コントロールに触れられません。

そこで **構築時に `SynchronizationContext.Current` をキャプチャ** し、コールバックをそのコンテキストへ `Post` し直します。

- WinForms → `WindowsFormsSynchronizationContext`
- WPF → `DispatcherSynchronizationContext`

つまり **UI スレッド上で `new` する (または `RateLimit.Debounce(...)` する) だけで、コールバックは自動的に UI スレッドに戻されます**。明示的にコンテキストを渡すことも、`null` を渡してインライン実行 (編組なし) にすることもできます。

> **外部依存ゼロ**。コアパッケージ (`WinformsMVP.Core`) に余計な依存を持ち込まないため、Rx (System.Reactive) などには依存していません。

---

## 基本的な使い方

```csharp
using WinformsMVP.Common;

// Debounce: 入力が 300ms 止まったら検索する
_searchDebouncer = RateLimit.Debounce(300, OnSearchSettled);
textBox.TextChanged += (s, e) => _searchDebouncer.Signal();

// Throttle: ドラッグ中は最大 150ms に 1 回だけ更新する
_sliderThrottler = RateLimit.Throttle(150, OnPreviewUpdate);
trackBar.ValueChanged += (s, e) => _sliderThrottler.Signal();
```

`OnSearchSettled` / `OnPreviewUpdate` は引数なしのメソッド (`Action`) です。UI スレッドでファクトリを呼んでいるので、これらのハンドラ内でコントロールに直接触れて構いません。

---

## RateLimit ファクトリ

コンストラクタを直接呼ぶ代わりに、`RateLimit` 静的ファクトリを使うと簡潔に書けます。`int` ミリ秒を受け取り、**メソッドグループをそのまま渡せます** (`(Action)` キャスト不要 — これが `Action` 拡張メソッドではなくファクトリにした理由です)。

```csharp
RateLimit.Debounce(300, OnSearchSettled);                    // int ミリ秒
RateLimit.Debounce(TimeSpan.FromSeconds(1), OnSearch);       // TimeSpan
RateLimit.Throttle(150, OnPreviewUpdate);
RateLimit.Throttle(interval, OnPreviewUpdate);
```

戻り値は `Debouncer` / `Throttler` なので、`Signal()` も `DisposeWith(Disposables)` もそのまま使えます。

---

## 操作の一覧 (Signal / Cancel / Flush / Dispose)

3 つの「実行系」操作は直交します。

| 操作 | 保留中の処理 | オブジェクトの寿命 | 用途 |
|------|--------------|--------------------|------|
| `Cancel()` (Debouncer のみ) | **破棄**する | 生存 (再び `Signal` 可) | 保留をなかったことにする |
| `Flush()` | **今すぐ実行**する (保留がある時) | 生存 (再び `Signal` 可) | 取りこぼしたくない最後の 1 回を確定させる |
| `Dispose()` | **破棄**する (実行しない) | 破棄 (以後すべて no-op) | 解放する |

### `Flush()` の重要な性質

- 保留がある時だけ実行します。保留がなければ **何もしません (no-op)**。「無条件に 1 回走らせる」ものではありません。
- **呼び出したスレッド上で同期実行** します (キャプチャ済みコンテキストを経由しません)。そのため UI スレッドから呼べば UI スレッドで走り、戻った時点で処理は完了しています。
- 同期だがブロッキング待機ではないため、**UI スレッドで呼んでもデッドロックしません** (「Post 済みのコールバックの完了を待つ」方式はデッドロックします — だからその方式は採用していません)。

```csharp
// 画面クローズ時に最後の 1 回を取りこぼさず、かつ解放する
protected override void OnFormClosed(FormClosedEventArgs e)
{
    _searchDebouncer.Flush();    // 保留があれば今ここで同期実行
    _searchDebouncer.Dispose();  // その後で解放
    base.OnFormClosed(e);
}
```

> **注意**: `Dispose()` は保留中の処理を実行せず **破棄** します。最後の 1 回を確実に走らせたい場合のみ、先に `Flush()` を呼んでください。

---

## ライフサイクルと破棄

レート制限器は内部にタイマーを持つため、**確定的に破棄してください** (フレームワークの `CompositeDisposable` / `DisposeWith` ドクトリンに従います)。

**Presenter — `DisposeWith(Disposables)` で自動解放 (推奨):**

```csharp
private Debouncer _search;

protected override void OnInitialize()
{
    _search = RateLimit.Debounce(300, OnSearchSettled).DisposeWith(Disposables);
    // Cleanup 後にフレームワークが自動的に Dispose する。Cleanup オーバーライド不要。
}
```

**Form (Presenter なしの View 駆動) — `OnFormClosed` で破棄:**

```csharp
protected override void OnFormClosed(FormClosedEventArgs e)
{
    _searchDebouncer.Dispose();
    _sliderThrottler.Dispose();
    base.OnFormClosed(e);
}
```

---

## バックプレッシャー (積み上がり・並行実行の防止)

action の実行時間が `delay` / `interval` を**超える**場合 (重い処理を UI スレッドで同期実行している等)、素朴な実装では発火要求がキューに積み上がり、操作を止めた後も「古い呼び出し」が何度も再生されてしまいます。無マーシャリング時には並行実行の恐れもあります。

これを防ぐため、両クラスは **drop-while-running (実行中コアレッシング)** をデフォルトで備えています (内部の [`CoalescingRunner`])。

- action が実行中 (Post 済みで未完了) の間に来た発火は **キューに積まれず**、「もう 1 回必要」という事実だけが記録されます。
- 実行中の action が完了したら、**最新の 1 回だけ**追走します。
- → 任意の瞬間で「**最大 1 つ実行中 + 最大 1 つ待機**」。積み上がりも自己との並行実行も発生しません。

```
interval=10ms / action=50ms / 高頻度 Signal の場合:
  素朴な実装  → 停止後に数十回の古い呼び出しを再生 (キューが無制限に増加)
  本実装      → action 自身の完了ペースに律速され、積み上がらない
```

> **業界での位置づけ**: この組み合わせは新規の発明ではなく、定番パターンの合成です。間引きは古典的な debounce / throttle (lodash 等)、実行中の取りこぼしは RxJS の `exhaustMap`、「中間を捨て最新を残す」は Reactive Streams の `onBackpressureLatest` / Akka Streams の `conflate` / 金融配信系の conflation に相当します。**速い action には一切影響しません** (実行中ウィンドウが一瞬のため、コアレッシング経路に入りません)。

> **トレードオフ**: 遅い action の場合、Throttler のペースは「固定間隔ごと」から「action の完了駆動」へと退化します。これは意図的な取捨選択であり (積み上げるくらいなら間隔を譲る)、`conflate` 系と同じ思想です。

---

## 画面クローズ後に「幽霊コールバック」が走らない保証

「画面を閉じたのに、保留中のコールバックが後からバックグラウンドで 1 回走って、破棄済みコントロールに触れて落ちる」——これを防ぐ仕組みが揃っています。

1. **未発火のタイマー** → `Dispose` がタイマーを停止。走りません。
2. **既に UI キューへ `Post` 済みのコールバック** → 実際に実行される瞬間に `_disposed` を **再チェック** し、破棄済みならスキップします。
3. **UI スレッドは単一スレッド** → `Dispose` を UI スレッドで呼ぶ限り、その瞬間に UI スレッドが同じコールバックを実行中ということはあり得ません (すでに完了済みか、まだ始まっておらず再チェックで弾かれるか、のどちらか)。
4. **最後の保険** → 万一コールバックが走って例外を投げても、内部でガードされ (`onError` へ転送、なければ `ILoggerFactory` 経由でログ出力して握り潰し)、**プロセスは落ちません**。

→ **UI スレッドで `Dispose` (または `DisposeWith` による自動解放) する限り、クローズ後にコールバックがコントロールへ触れることはありません。**

> ただし大前提として「必ず解放すること」。`Dispose` も `DisposeWith` もしないと、保留中のタイマーは発火し、`_disposed` が false のままなのでクローズ後に走ってしまいます。

---

## エラー処理

タイマーのコールバックはスレッドプールスレッドで走るため、未捕捉例外はプロセスを終了させます。これを防ぐため、アクションは常にガードされます。

```csharp
_search = RateLimit.Debounce(300, OnSearchSettled,
    onError: ex => Logger.LogError(ex, "search failed"));
```

- `onError` を渡せば、アクションが投げた例外はそこへ転送されます。
- `onError` が `null` の場合、例外は `ServiceLocator` の `ILoggerFactory` (カテゴリ `WinformsMVP.Common.RateLimit`) で **エラーログに出力されてから握り潰され** (プロセス保護優先)、タイマー/UI ポンプは死にません。
- `onError` 自身が投げても、それも握り潰されます。

---

## 主要 API

```csharp
namespace WinformsMVP.Common
{
    public sealed class Debouncer : IDisposable
    {
        public Debouncer(TimeSpan delay, Action action,
            Action<Exception> onError = null,
            SynchronizationContext synchronizationContext = null);

        public void Signal();   // 活動を記録し、待機期間を (再) 開始
        public void Cancel();   // 保留中の発火を破棄 (オブジェクトは生存)
        public void Flush();    // 保留があれば呼び出しスレッドで同期実行
        public void Dispose();  // タイマー停止・解放 (冪等)
    }

    public sealed class Throttler : IDisposable
    {
        public Throttler(TimeSpan interval, Action action,
            Action<Exception> onError = null,
            SynchronizationContext synchronizationContext = null);

        public void Signal();   // 先頭で即発火、または末尾発火を予約
        public void Flush();    // 保留中の末尾発火を呼び出しスレッドで同期実行
        public void Dispose();  // タイマー停止・解放 (冪等)
    }

    public static class RateLimit
    {
        public static Debouncer Debounce(int milliseconds, Action action, ...);
        public static Debouncer Debounce(TimeSpan delay, Action action, ...);
        public static Throttler Throttle(int milliseconds, Action action, ...);
        public static Throttler Throttle(TimeSpan interval, Action action, ...);
    }
}
```

> `delay` は 0〜Int32.MaxValue ミリ秒、`interval` は 0 より大きく Int32.MaxValue ミリ秒以下。範囲外は `ArgumentOutOfRangeException`、`action` が `null` なら `ArgumentNullException`。

---

## 関連ページ

- [ViewAction システム](Reference-ViewAction-System) — UI イベントをアクションへルーティングする仕組み (レート制限と組み合わせ可能)
- [Presenter の購読ライフサイクルを管理する](HowTo-Manage-Presenter-Subscriptions) — `DisposeWith(Disposables)` の使い方
- [非同期処理を扱う](HowTo-Handle-Async-Operations)

サンプル: `samples/WinformsMVP.Samples/DebounceThrottleDemo/` (ランチャーの「Architecture」セクション)
