# ウィンドウクローズモデル

このページでは、フレームワークの **二方向ウィンドウクローズモデル** の設計を解説します。具体的なコードサンプルは [HowTo: ウィンドウクローズを扱う](HowTo-Handle-Window-Closing) を、`WindowNavigator` の API リファレンスは [WindowNavigator](Reference-WindowNavigator) を参照してください。

> **ダーティチェック・保存確認・キャンセル動作を正しく実装するには、このモデルを理解することが不可欠です。**

---

## なぜ専用モデルが必要か

WinForms の `Form.FormClosing` は、ユーザーの X ボタン・システムシャットダウン・タスクマネージャによる強制終了・オーナーウィンドウの伝播・コードからの `Close()` 呼び出しなど、あらゆるクローズトリガーを 1 つのイベントに集約します。このイベントは `FormClosingEventArgs` という WinForms 名前空間の型を持つため、Presenter が直接ハンドルすると [MVP の 3 つの鉄則](Concept-MVP-Pattern#3-つの鉄則-three-iron-rules) のうち 2 つに違反してしまいます。

このフレームワークは、クローズの **起点を 2 つ** (フレームワーク / Presenter) に分けつつ、閉じてよいかの判断は **1 つのゲート** (`CanClose` / `CanCloseAsync`) に集め、すべての WinForms の詳細を 1 つの内部コンポーネントに封じ込めることでこの問題を解決します。

---

## 概念モデル: 1 つのゲート、2 つの起点

| 方向 | 起点 | 仕組み | 典型的なトリガー |
|------|------|--------|----------------|
| **Pull** (ゲート) | フレームワーク | `protected virtual bool CanClose(CloseReason reason)` (非同期は `Task<bool> CanCloseAsync(CloseReason)`) オーバーライド | X ボタン / Alt+F4 / オーナーのクローズ |
| **Push** | Presenter | 基底の `RequestClose(result, status)` / `RequestClose(status)` メソッド | ユーザーが 保存 / 閉じる / OK をクリック |

ウィンドウが開いた後は、**どちらの起点のクローズも同じゲートを通ります**。X / Alt+F4 / オーナーのクローズも、Presenter の `RequestClose` (`CloseReason.Normal` として) も、`CanClose` / `CanCloseAsync` が `false` を返せば閉じません。例外はシステムシャットダウンとタスクマネージャだけで、これらはゲートを通さずに許可されます。`RequestClose` は「閉じる」ことを保証するのではなく、**そのクローズの候補となる結果** を渡してクローズを開始します。

ウィンドウが表示され始める **前** (`OnInitialize` の中など) の `RequestClose` はクローズではなく **表示のキャンセル** です。ゲートは通らず、ウィンドウは表示されません ([表示前の RequestClose — 表示のキャンセル](#表示前の-requestclose--表示のキャンセル))。

両方のパスは WinForms の `FormClosed` に収束します。`ShowWindowAsModal` の呼び出し元は、どちらの方向でクローズが起きたかに関わらず、常に同じ `InteractionResult<TResult>` を受け取ります。WinForms の `FormCloseReason` からフレームワークの `CloseReason` へのマッピングは `CloseReasonMap` の中で 1 回だけ行われ、Presenter には絶対に漏れません。

### Push 方向 — Presenter が能動的にクローズする

```
User clicks Save
    ↓
OnSave() in Presenter
    ├─ saves, then finalizes the dirty state (AcceptChanges)
    └─ RequestClose(result, InteractionStatus.Ok)
          ↓
    WindowLifecycleController (ICloseSink.Close)
          ├─ stores result as the candidate of this close attempt
          └─ form.Close()
                ↓
          FormClosing fires
                └─ calls CanClose / CanCloseAsync (CloseReason.Normal)
                      ├─ state is clean → true (no prompt)
                      │     ↓
                      │  FormClosed → Converge → onClosed callback → InteractionResult(Ok, result)
                      └─ false → window stays open, candidate discarded
```

「閉じる」ボタンも同じ経路です: `RequestClose(InteractionStatus.Cancel)` を呼ぶだけで、未保存の編集があれば X と同じ「保存しますか？」が出ます。

### Pull 方向 — 外部トリガー

```
User clicks X (or Alt+F4, system shutdown …)
    ↓
FormClosing fires
    ↓
WindowLifecycleController.OnFormClosing
    ├─ SystemShutdown / TaskManager → allowed without asking
    ├─ otherwise → calls ICloseParticipant.CanCloseGate(reason, ...)
    │       ↓
    │   Presenter.CanCloseAsync(reason)   [your override; default → CanClose(reason)]
    │       ├─ false → e.Cancel = true  (window stays open)
    │       └─ true  → e.Cancel = false
    └─ (async path: task still pending → e.Cancel = true; completes with true → re-close without asking again)
          ↓
    FormClosed → Converge → InteractionResult(Cancel) to caller
```

---

## 単一正本の不変条件

> **ダーティ状態のプロンプトは `CanClose` / `CanCloseAsync` (Pull 方向) にのみ置く。**

保存ボタン・明示的な破棄ボタンのハンドラは、`RequestClose` を呼ぶ **前に** ダーティ状態を確定させます (保存後も破棄時も `ChangeTracker.AcceptChanges`。破棄の場合は「破棄した編集の追跡を終える」意味で、保存したという意味ではありません)。`RequestClose` もゲートを通るので、この確定こそが 保存 / 破棄 でプロンプトを出さない理由です。確定せずに `RequestClose` すると、ゲートがダーティ状態を見て X と同じプロンプトを出します — 「閉じる」ボタンではそれが望む動作です。

| ボタン | ハンドラ | ゲートが見る状態 |
|--------|----------|------------------|
| 保存して閉じる | 保存 → `AcceptChanges()` → `RequestClose(id, Ok)` | クリーン → プロンプトなし |
| 破棄して閉じる (明示) | `AcceptChanges()` → `RequestClose(Cancel)` | クリーン → プロンプトなし |
| 閉じる (X と同じ) | `RequestClose(Cancel)` だけ | ダーティなら「保存しますか？」 |

ゲートが拒否しても、すでに行った処理 (破棄ハンドラの `AcceptChanges` など) は巻き戻りません。また、保存中フラグ (`_isSaving`) を見て拒否するゲートは、ハンドラ自身の `RequestClose` もブロックします。ハンドラは `RequestClose` の **前に** そのフラグを下ろしてください。

設計上の利点:

- ダーティチェックのロジックが 1 箇所に集中し、X と「閉じる」ボタンで同じ確認が出る
- Form 側にクローズ用のコード (`btnClose_Click => Close()`) が不要で、「閉じる」も Presenter からテストできる
- ゲートと各ハンドラを分離してテストできる
- 呼び出し元は `InteractionResult<TResult>` だけを見ればよく、どちらの方向でクローズが発生したかを気にする必要がない

---

## Pull ゲート — `CanClose`

Presenter で `CanClose(CloseReason reason)` をオーバーライドすることでクローズを拒否できます。`false` を返すとブロック、`true` を返すと許可します:

```csharp
protected override bool CanClose(CloseReason reason)
{
    if (!_changeTracker.IsChanged) return true;

    bool discard = Messages.ConfirmYesNo("Discard unsaved changes?", "Confirm");
    return discard;
}
```

### 非同期 Pull ゲート — `CanCloseAsync`

クローズ判断に `await` が必要な場合 (閉じる前の保存、サーバー確認、非同期の確認処理) は `CanCloseAsync` をオーバーライドします。`true` で許可、`false` でブロックです。既定の実装は同期版 `CanClose(reason)` の結果を返すので、同期版で足りる場合は従来どおり `CanClose` だけをオーバーライドしてください。

> **BREAKING:** 以前のコールバック形式 `CanClose(CloseReason reason, Action<bool> proceed)` は削除されました。`proceed(x)` を呼んでいたコードは `CanCloseAsync` で `return x;` (必要なら `await` の後) に書き換えます。

```csharp
protected override async Task<bool> CanCloseAsync(CloseReason reason)
{
    if (!_changeTracker.IsChanged) return true;

    var answer = Messages.ConfirmYesNoCancel("Save changes as a draft?", "Unsaved Changes");
    if (answer == ConfirmResult.Cancel) return false;
    if (answer == ConfirmResult.No) return true;

    // Yes: await the save. The window stays open meanwhile; a failed save blocks the close.
    return await SaveDraftAsync();
}
```

フレームワークの扱い:

- タスクが同期的に完了していれば (既定実装や、`await` に到達しなかった場合)、答えはそのまま `e.Cancel` に適用されます。
- 未完了の間、ウィンドウは開いたままです。完了して `true` なら、フレームワークがゲートを抑制した再クローズを行います。`false` なら何もしません (ウィンドウは開いたまま)。
- 判定待ちの間に来た再クローズ (X の連打など) は、もう一度尋ねずに保留 (キャンセル) されます。ただし `SystemShutdown` / `TaskManager` によるクローズは保留せずに通します。また、owner ウィンドウ・MDI 親・`Application.Exit` から来たクローズが非同期に許可された場合、フレームワークはそのクローズ (owner / MDI 親のクローズ、`Application.Exit`) を再発行するので、親側の終了も続行されます。
- タスクが fault またはキャンセルされた場合はクローズをブロックしてエラーをログ出力します (`SystemShutdown` / `TaskManager` によるクローズは許可)。ユーザーに伝えるべき失敗は、オーバーライドの中で捕まえて `Messages` で表示し `false` を返してください。

待機中にユーザーが編集を続けても、その変更をクリーン扱いにしないようにします。保存する内容を先に固定し (`var saved = ...`)、完了後に `AcceptChanges(saved)` で **保存した内容** をスナップショットにしてください。保存中の編集は (元の値に戻した場合も含め) 変更として残り、その場合は `false` を返してウィンドウを開いたままにします (実例: [`samples/Kibinago.Samples/EmailDemo/ComposeEmailPresenter.cs`](https://github.com/VoidAxon/winforms-mvp/blob/master/samples/Kibinago.Samples/EmailDemo/ComposeEmailPresenter.cs)、[HowTo シナリオ 6](HowTo-Handle-Window-Closing#シナリオ-6-非同期クローズ判断))。

---

## Push 方向 — `RequestClose`

基底の `RequestClose` メソッドを直接呼びます — 実装すべきインターフェイスも拡張メソッドも不要です。`TResult` は引数から推論されます:

```csharp
public class EditUserPresenter : WindowPresenterBase<IEditUserView>
{
    private void OnSave()
    {
        _repository.Save(_user);
        _changeTracker.AcceptChanges();   // finalize the dirty state: snapshot = saved state
        var result = new UserResult { Name = _user.Name };
        RequestClose(result, InteractionStatus.Ok);
    }

    private void OnCancel()
    {
        _changeTracker.AcceptChanges();   // stop tracking the discarded edits (not "saved") so CanClose won't ask again
        RequestClose(InteractionStatus.Cancel);   // no-result overload; C# prefers the non-generic candidate
    }
}
```

`WindowPresenterBaseCore<TView>` の 2 つのオーバーロード:

```csharp
protected void RequestClose(InteractionStatus status = InteractionStatus.Ok);                        // close, no business result
protected void RequestClose<TResult>(TResult result, InteractionStatus status = InteractionStatus.Ok); // close with a typed result
```

注意: `RequestClose(InteractionStatus.Cancel)` は結果なしオーバーロードに解決されます。結果の型がそのまま `InteractionStatus` であるウィンドウは `RequestClose<InteractionStatus>(x)` で明示的に型指定できますが、実際にそのケースが必要になることはほぼありません。

結果の変換ルール (Managed / Adopted 共通):

- `Ok` で渡した結果の型が呼び出し元の `TResult` と合わない場合、または `TResult` が null 非許容の値型なのに結果を渡さなかった場合は、`Ok(default)` ではなく **`InteractionStatus.Error`** (原因を説明する `ErrorMessage` 付き) になります。
- エラーで閉じるときは `RequestClose("保存に失敗しました", InteractionStatus.Error)` や `RequestClose(exception, InteractionStatus.Error)` のように渡すと、そのメッセージ/例外が `InteractionResult.ErrorMessage` / `Exception` に入ります。
- 開いているウィンドウへの `RequestClose` は `CloseReason.Normal` でゲートを通ります。渡した結果は **候補** で、ゲートが許可すれば呼び出し元に届き、拒否すればウィンドウは開いたまま結果は捨てられます (後のクローズに持ち越されることはありません)。`InteractionStatus.Error` のクローズもゲートを通ります (`Error` は呼び出し元が受け取る内容であって、強制クローズではありません)。
- ゲート (`CanClose` / `CanCloseAsync`) が判定している間に呼ばれた `RequestClose` は、2 回目のクローズではなく**判定中のクローズの結果**を置き換えます (最後の呼び出しが勝ちます)。ゲートが許可すればその結果で閉じ、ブロックすれば捨てられます。X → 「保存しますか？」→ はい で保存した結果を呼び出し元へ返すときに使います ([HowTo: ウィンドウクローズを扱う](HowTo-Handle-Window-Closing) のシナリオ 6)。
- 非同期ゲートの判定待ちの間は、X も `RequestClose` も新しいゲートを開始しません。`RequestClose` は判定待ちのクローズの結果を更新するだけです。
- フレームワークが close sink をバインドする前、またはウィンドウが表示され始める前 (例: `OnInitialize` の中) の `RequestClose` は表示のキャンセルになります (次節)。

---

## 表示前の RequestClose — 表示のキャンセル

> 開けない理由をユーザーに伝える書き方 (メッセージ → `RequestClose`、または理由を結果で返す) は [HowTo: ウィンドウクローズを扱う § 開けない理由をユーザーに伝える](HowTo-Handle-Window-Closing#開けない理由をユーザーに伝える) を参照。

`OnInitialize`、`OnInitializeAsync` の同期部分 (最初の `await` まで)、または close sink のバインド前に呼ばれた `RequestClose` は、ウィンドウを閉じるのではなく **開くのを取りやめます**。ユーザーはまだ何も編集できないので、ゲートは通りません。複数回呼んだ場合は最後の結果が使われます。

```csharp
protected override void OnInitialize(int orderId)
{
    var order = _repository.Find(orderId);
    if (order == null)
    {
        Messages.ShowWarning("注文が見つかりません。");
        RequestClose(InteractionStatus.Cancel);   // the window is never shown
        return;
    }
    View.Order = order;
}
```

| ホスティング | 動作 |
|--------------|------|
| Managed モーダル (`ShowAsModal`) | `ShowDialog` は呼ばれず、結果がそのまま返る |
| Managed モードレス (`ShowWindow`) | `Show` は呼ばれず、`null` を返す。順序: キーを解放 → `onClosed` を 1 回 → Presenter と Form を破棄 → 戻る (`onClosed` から同じキーで開き直せる) |
| Adopted (`Connect`) | 呼び出し元の `form.Show()` / `ShowDialog()` / `Application.Run(form)` はそのままでよい。フレームワークが Form の `Load` で不可視化 (不透明度 0、`SystemInformation.VirtualScreen` の外へ移動) してから閉じるので、何も表示されない (Windows 上でモーダル・モードレスともにちらつかないことを確認済み) |

Adopted の制限:

- MDI 子ウィンドウは不透明度が効かず位置が親相対のため、不可視化せずに `Load` で閉じます (一瞬見える場合があります)。
- `Connect` の前にすでに表示されている Form では、表示のキャンセルではなく通常の (ゲートを通る) クローズになります。
- `Application.Run(form)` のメインフォームで表示をキャンセルすると、アプリケーションが終了します。
- 他の `FormClosing` ハンドラがこのクローズを拒否した場合、フレームワークは不透明度と位置を戻し、Form は通常どおり表示されます (見えないまま生きているウィンドウは残りません)。

---

## `CloseReason` 列挙型

このフレームワークは `System.Windows.Forms.CloseReason` の代わりに独自の `CloseReason` を使い、WinForms 型が View インターフェイスや Presenter に一切漏れないようにしています。

```csharp
public enum CloseReason
{
    Normal,          // X / Alt+F4 — inspect dirty state here
    SystemShutdown,  // Windows shutting down — never block
    TaskManager,     // Force-kill — never block
    ParentClosing,   // Owner window closing — usually allow
    Unknown,
}
```

`FormCloseReason` からこの列挙型へのマッピングは `CloseReasonMap` (内部) で 1 回だけ行われます。フレームワーク内で WinForms のクローズ理由を知っている場所はここだけです。

---

## ホスティング — Managed と Adopted

**単一オーナーの規則**: 1 つの Form に対して 2 つのモードのいずれか一方だけを使います。混在させないでください。

### Managed ホスティング (MVP ネイティブウィンドウのデフォルト)

`WindowNavigator` が Form を作成し、クローズコントローラを接続して表示します。すべての新しいウィンドウに対する正規のパスはこちらです:

```csharp
// Modal — returns InteractionResult<TResult>
var result = Navigator.For(presenter).WithParam(parameters).ShowAsModal<UserResult>();

// Non-modal — returns immediately
Navigator.For(presenter).ShowWindow();
```

### Adopted ホスティング (シェルウィンドウ・レガシー移行)

自分で作成して `Show` / `Application.Run` する Form に対しては、`presenter.Connect(form)` を呼びます。これにより、フォームを表示することなく、アタッチ・初期化・クローズ配線をべき等に行います:

```csharp
// No-result adoption
presenter.Connect(form);

// Typed callback on close
presenter.Connect<IMyView, bool>(form, result =>
{
    if (result.IsOk) DoSomethingWith(result.Value);
});

// Parameterized presenter adoption
presenter.Connect(form, parameters);
```

`Connect` 後は `Show` / `Application.Run` の呼び出しは呼び出し元が行います。クローズ配線は Presenter が担当します。

> `Connect` は、同じ Form をアタッチ済みで未初期化の Presenter なら初期化まで行います。アタッチ済みの View と異なる Form を渡すと例外、接続済みの Presenter への 2 回目の `Connect` も例外です (生きている Presenter は破棄されません)。アタッチや初期化の途中で失敗した場合は Presenter が破棄されます。

---

## 内部ブリッジ — `WindowLifecycleController`

ウィンドウごとに 1 つの `WindowLifecycleController` インスタンスが作成されます。このインスタンスは:

- `ICloseSink` (Push シンク) を実装し、`RequestClose` の結果をそのクローズ試行の候補として記録して `form.Close()` を呼びます。
- `FormClosing` をブリッジし、X でも `RequestClose` でも `ICloseParticipant.CanCloseGate` を呼んで `e.Cancel` の決定を適用します。クローズ試行ごとに候補の結果を持ち、拒否されたら捨てます。
- 表示前の状態を管理し、表示のキャンセルを処理します (Managed: `OpeningCancelled` + `ConvergeWithoutShow`、Adopted: `Load` で不可視化して閉じる)。
- `FormClosed` に収束し、`onClosed` コールバックを呼び出して、Presenter を (Managed モーダル / Adopted の場合は Form も) Dispose します。Presenter の `Dispose` が例外を投げても Form は破棄されます。
- 閉じずに破棄された Form (表示しないまま呼び出し側が `Dispose` した、ドッキングライブラリや MDI 親がまとめて破棄した) は `Disposed` で同じように収束します: `onClosed` を 1 回 (保留中の結果、なければ Cancel)、Presenter の破棄、Navigator のキーの解放。破棄は拒否できないので `CanClose` は呼ばれません。先に `FormClosed` で収束していれば何もしません。

フレームワーク内で `Form` を直接参照する唯一のコンポーネントです。

---

## 呼び出し元から見た動作

呼び出し元 (親 Presenter) は `InteractionResult<TResult>` だけを見ればよく、どちらの方向でクローズが発生したかを知る必要はありません:

```csharp
private void OnEditUser()
{
    var result = Navigator.For(new EditUserPresenter())
                          .WithParam(new EditUserParameters { UserId = _selectedId })
                          .ShowAsModal<UserResult>();

    if (result.IsOk)
        ReloadUser(result.Value.UserId);
    // result.IsCancelled: user pressed X and discarded, or clicked Cancel — no action needed
}
```

---

## まとめ

| 関心事 | 保証 |
|--------|------|
| Presenter は WinForms 型を見ない | `CloseReason` はフレームワークの列挙型。`FormClosingEventArgs` は絶対に漏れない |
| ダーティチェックが 1 箇所に集中する | プロンプトは `CanClose` にのみ存在する |
| X と `RequestClose` が同じ判断を受ける | 開いたウィンドウのクローズはすべてゲートを通る (シャットダウン / タスクマネージャを除く) |
| 保存 / 破棄 でプロンプトが出ない | `RequestClose` の前に `AcceptChanges` で確定する (ゲートはクリーンな状態を見る) |
| 拒否されたクローズの結果が漏れない | 候補の結果はクローズ試行ごと。拒否で破棄され、後のクローズに持ち越されない |
| 表示前に取りやめたウィンドウは見えない | 表示前の `RequestClose` は表示のキャンセル (ゲートなし、表示しない) |
| Form はクローズコードをまったく書かない | `IWindowView` にクローズ関連のメンバーはない。「閉じる」ボタンも ViewAction |
| ゲートと各ハンドラをテストできる | `PresenterTestHost` で、ゲートは `host.CanClose(reason)` (非同期は `await host.CanCloseAsync(reason)`)、`RequestClose` の結果は `host.CloseRequested` / `CloseStatus` / `CloseResult` (受理されたクローズ) で検証 |

---

## 次のステップ

| 目的 | ページ |
|------|--------|
| 全シナリオの具体的なコードサンプル | [HowTo: ウィンドウクローズを扱う](HowTo-Handle-Window-Closing) |
| `WindowNavigator` の完全な API | [WindowNavigator](Reference-WindowNavigator) |
| ダーティ状態の追跡 | [ChangeTracker](Reference-ChangeTracker) |
| テストパターン | [HowTo: Presenter をテストする](HowTo-Test-A-Presenter) |
