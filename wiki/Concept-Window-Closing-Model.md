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
| **Pull** | フレームワーク | `protected virtual bool CanClose(CloseReason reason)` オーバーライド | X ボタン / Alt+F4 / シャットダウン |
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
    WindowCloseController (ICloseSink.Close)
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
WindowCloseController.OnFormClosing
    ├─ _suppressGate is false → calls ICloseParticipant.CanCloseGate(reason, proceed)
    │       ↓
    │   Presenter.CanClose(reason, proceed)   [your override]
    │       ├─ return false → e.Cancel = true  (window stays open)
    │       └─ return true  → e.Cancel = false
    └─ (async path: proceed(true) from continuation → re-close with _suppressGate=true)
          ↓
    FormClosed → Converge → InteractionResult(Cancel) to caller
```

---

## 単一正本の不変条件

> **ダーティ状態のプロンプトは `CanClose` (Pull 方向) にのみ置く。**

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

### 非同期 Pull ゲート

クローズ判断にコールバック (非同期保存、サーバーラウンドトリップ) が必要な場合は、2 引数の形式をオーバーライドします。`proceed(true)` で許可、`proceed(false)` でブロックします — 必要に応じてコンティニュエーションの中から呼んでも構いません。`Task` ではなく `Action<bool>` を使うことで net40 との互換性を維持します:

```csharp
protected override void CanClose(CloseReason reason, Action<bool> proceed)
{
    if (reason == CloseReason.SystemShutdown || reason == CloseReason.TaskManager)
    {
        proceed(true);
        return;
    }

    // Default one-argument overload chains through here:
    // proceed(CanClose(reason));

    // Async example — check with a server before closing:
    CheckServerAsync(ok => proceed(ok));
}
```

フレームワーク内部の非同期処理: `proceed` が同期的に (つまり `CanCloseGate` が返る前に) 呼ばれた場合、答えは `e.Cancel` に直接適用されます。コンティニュエーションから (つまり `CanCloseGate` が返った後に) 呼ばれた場合、`proceed(true)` はゲートを抑制した再クローズをトリガーし、`proceed(false)` はウィンドウを開いたままにします (何もアクションは不要です)。

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

---

## 内部ブリッジ — `WindowCloseController`

ウィンドウごとに 1 つの `WindowCloseController` インスタンスが作成されます。このインスタンスは:

- `ICloseSink` (Push シンク) を実装し、保留中の結果とステータスを記録し、抑制フラグをセットして `form.Close()` を呼びます。
- `FormClosing` (Pull ゲート) をブリッジし、`ICloseParticipant.CanCloseGate` を呼んで `e.Cancel` の決定を適用します。
- `CloseRequestedBeforeShow` + `ConvergeWithoutShow` を通じて、表示前クローズのエッジケースを処理します。
- `FormClosed` に収束し、`onClosed` コールバックを呼び出して、Presenter を (Managed モーダル / Adopted の場合は Form も) Dispose します。

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
| 両方向を独立してテストできる | Pull は `ICloseParticipant.CanCloseGate` 経由、Push は記録用 `ICloseSink` を束縛 |

---

## 次のステップ

| 目的 | ページ |
|------|--------|
| 全シナリオの具体的なコードサンプル | [HowTo: ウィンドウクローズを扱う](HowTo-Handle-Window-Closing) |
| `WindowNavigator` の完全な API | [WindowNavigator](Reference-WindowNavigator) |
| ダーティ状態の追跡 | [ChangeTracker](Reference-ChangeTracker) |
| テストパターン | [HowTo: Presenter をテストする](HowTo-Test-A-Presenter) |
