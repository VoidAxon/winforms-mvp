# ウィンドウクローズモデル

このページでは、フレームワークの **二方向ウィンドウクローズモデル** の設計を解説します。具体的なコードサンプルは [HowTo: ウィンドウクローズを扱う](HowTo-Handle-Window-Closing) を、`WindowNavigator` の API リファレンスは [WindowNavigator](Reference-WindowNavigator) を参照してください。

> **ダーティチェック・保存確認・キャンセル動作を正しく実装するには、このモデルを理解することが不可欠です。**

---

## なぜ専用モデルが必要か

WinForms の `Form.FormClosing` は、ユーザーの X ボタン・システムシャットダウン・タスクマネージャによる強制終了・オーナーウィンドウの伝播・コードからの `Close()` 呼び出しなど、あらゆるクローズトリガーを 1 つのイベントに集約します。このイベントは `FormClosingEventArgs` という WinForms 名前空間の型を持つため、Presenter が直接ハンドルすると [MVP の 3 つの鉄則](Concept-MVP-Pattern#3-つの鉄則-three-iron-rules) のうち 2 つに違反してしまいます。

このフレームワークは、クローズ責務を **2 つの独立した方向** に分割し、すべての WinForms の詳細を 1 つの内部コンポーネントに封じ込めることでこの問題を解決します。

---

## 概念モデル: Push と Pull

| 方向 | 起点 | 仕組み | 典型的なトリガー |
|------|------|--------|----------------|
| **Pull** | フレームワーク | `protected virtual bool CanClose(CloseReason reason)` (非同期は `Task<bool> CanCloseAsync(CloseReason)`) オーバーライド | X ボタン / Alt+F4 / シャットダウン |
| **Push** | Presenter | 基底の `RequestClose(result, status)` / `RequestClose(status)` メソッド | ユーザーが Save / Cancel / OK をクリック |

両方のパスは WinForms の `FormClosed` に収束します。`ShowWindowAsModal` の呼び出し元は、どちらの方向でクローズが起きたかに関わらず、常に同じ `InteractionResult<TResult>` を受け取ります。WinForms の `FormCloseReason` からフレームワークの `CloseReason` へのマッピングは `CloseReasonMap` の中で 1 回だけ行われ、Presenter には絶対に漏れません。

### Push 方向 — Presenter が能動的にクローズする

```
User clicks Save
    ↓
OnSave() in Presenter
    ├─ commits dirty flag (AcceptChanges) — for model correctness, not to skip the gate
    └─ RequestClose(result, InteractionStatus.Ok)
          ↓
    WindowLifecycleController (ICloseSink.Close)
          ├─ sets _suppressGate = true  ← skips the Pull gate once
          └─ form.Close()
                ↓
          FormClosing fires
                └─ _suppressGate is true → gate skipped, no dirty prompt
                      ↓
          FormClosed → Converge → onClosed callback → InteractionResult(Ok, result)
```

### Pull 方向 — 外部トリガー

```
User clicks X (or Alt+F4, system shutdown …)
    ↓
FormClosing fires
    ↓
WindowLifecycleController.OnFormClosing
    ├─ _suppressGate is false → calls ICloseParticipant.CanCloseGate(reason, ...)
    │       ↓
    │   Presenter.CanCloseAsync(reason)   [your override; default → CanClose(reason)]
    │       ├─ false → e.Cancel = true  (window stays open)
    │       └─ true  → e.Cancel = false
    └─ (async path: task still pending → e.Cancel = true; completes with true → re-close with _suppressGate=true)
          ↓
    FormClosed → Converge → InteractionResult(Cancel) to caller
```

---

## 単一正本の不変条件

> **ダーティ状態のプロンプトは `CanClose` / `CanCloseAsync` (Pull 方向) にのみ置く。**

Push ハンドラは `RequestClose` を呼ぶ **前に** ダーティフラグを確定させます (`AcceptChanges` / `RejectChanges`)。これにより、フレームワークが続けて実行する `CanClose` の呼び出しはクリーンな状態を見るため、再プロンプトは起きません。二重プロンプトの防止は構造的な保証であり、`AcceptChanges` と `RequestClose` の呼び出し順序には依存しません。

設計上の利点:

- ダーティチェックのロジックが 1 箇所に集中する
- Push 方向のクローズが誤って二重ダーティチェックダイアログを出すことがない
- 両方向を独立かつ分離してテストできる
- 呼び出し元は `InteractionResult<TResult>` だけを見ればよく、どちらの方向でクローズが発生したかを気にする必要がない

---

## Pull ゲート — `CanClose`

Presenter で `CanClose(CloseReason reason)` をオーバーライドすることでクローズを拒否できます。`false` を返すとブロック、`true` を返すと許可します:

```csharp
protected override bool CanClose(CloseReason reason)
{
    // Never block system-level shutdowns — a modal dialog here can freeze application exit.
    if (reason == CloseReason.SystemShutdown || reason == CloseReason.TaskManager)
        return true;

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
    // Never block system-level shutdowns.
    if (reason != CloseReason.Normal) return true;
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

待機中にユーザーが編集を続けると、保存したスナップショットより新しい変更をクリーン扱いにしてしまう恐れがあります。保存中は View の入力をロックし、完了後に「スナップショット以降に変更がないこと」を確かめてから `AcceptChanges` してください (実例: `samples/.../EmailDemo/ComposeEmailPresenter.cs`、[HowTo シナリオ 6](HowTo-Handle-Window-Closing#シナリオ-6-非同期クローズ判断))。

---

## Push 方向 — `RequestClose`

基底の `RequestClose` メソッドを直接呼びます — 実装すべきインターフェイスも拡張メソッドも不要です。`TResult` は引数から推論されます:

```csharp
public class EditUserPresenter : WindowPresenterBase<IEditUserView>
{
    private void OnSave()
    {
        var result = new UserResult { Name = View.UserName };
        _changeTracker.AcceptChanges();   // commit model state
        RequestClose(result, InteractionStatus.Ok);
    }

    private void OnCancel()
    {
        _changeTracker.RejectChanges();
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
- フレームワークが close sink をバインドする前 (例: `OnInitialize` の中) に呼ばれた `RequestClose` は保持され、バインド時に配送されます。

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

- `ICloseSink` (Push シンク) を実装し、保留中の結果とステータスを記録し、抑制フラグをセットして `form.Close()` を呼びます。
- `FormClosing` (Pull ゲート) をブリッジし、`ICloseParticipant.CanCloseGate` を呼んで `e.Cancel` の決定を適用します。
- `CloseRequestedBeforeShow` + `ConvergeWithoutShow` を通じて、表示前クローズのエッジケースを処理します。
- `FormClosed` に収束し、`onClosed` コールバックを呼び出して、Presenter を (Managed モーダル / Adopted の場合は Form も) Dispose します。Presenter の `Dispose` が例外を投げても Form は破棄されます。

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
| Push クローズがダーティプロンプトを再発火させない | `_suppressGate` フラグは構造的であり、呼び出し順序の規約には依存しない |
| Form はクローズコードをまったく書かない | `IWindowView` にクローズ関連のメンバーはない |
| 両方向を独立してテストできる | `PresenterTestHost` で、Pull は `host.CanClose(reason)` (非同期は `await host.CanCloseAsync(reason)`)、Push は `host.CloseStatus` / `CloseResult` で検証 |

---

## 次のステップ

| 目的 | ページ |
|------|--------|
| 全シナリオの具体的なコードサンプル | [HowTo: ウィンドウクローズを扱う](HowTo-Handle-Window-Closing) |
| `WindowNavigator` の完全な API | [WindowNavigator](Reference-WindowNavigator) |
| ダーティ状態の追跡 | [ChangeTracker](Reference-ChangeTracker) |
| テストパターン | [HowTo: Presenter をテストする](HowTo-Test-A-Presenter) |
