# RequestClose goes through CanClose — design

Date: 2026-10-23 · Status: approved by the owner (direction A for Connect) · Source: real-screen validation V-28, V-34 · Reviewed with codex (two rounds)

## Why

A 「閉じる」 button must behave like ✕: ask 保存しますか when there are unsaved edits. Today a presenter cannot
start such a close. `RequestClose` (Push) skips the `CanClose` / `CanCloseAsync` gate by design, so applications
write `btnClose_Click => Close()` in the Form (against "Forms write zero closing code", untestable from the
presenter), duplicate the prompt in the handler, or call their own `CanCloseAsync` and then `RequestClose`
(which breaks when the gate itself calls `RequestClose(result, Ok)`).

Mainstream frameworks run the guard for presenter-initiated closes too: Prism (`RequestClose` → `CanCloseDialog`),
WPF (`Window.Close` raises `Closing`), Caliburn.Micro (`TryCloseAsync` → `CanCloseAsync`).

## Decisions (owner)

1. **After the window is shown, every `RequestClose` goes through the gate** with `CloseReason.Normal`. No opt-in
   API, no `force` / `skipGate` parameter, no new `CloseReason`.
2. **Before the window is shown** (`RequestClose` from `OnInitialize` / the synchronous part of
   `OnInitializeAsync`, or before the sink is bound) it is not a close but **cancelling the opening**: no gate
   (the user cannot have edited anything), the window is **not shown** and the caller receives the result.
3. No flash, and **no extra code for the user**, when a window is cancelled before it is shown (measured on
   Windows, V-34). "Before shown" ends:
   - Managed (`WindowNavigator`): immediately before the framework calls `Show` / `ShowDialog`. A request before
     that cancels the opening: `Show` / `ShowDialog` is not called at all (modal and modeless). Modeless:
     `ShowWindow` returns `null`; order: unregister the key → `onClosed` once → dispose presenter and Form →
     return (so `onClosed` may reopen the same key). The framework disposes the never-shown Form.
   - Adopted (`Connect`, the caller shows the Form — direction A, chosen by the owner): `Connect` hooks the Form's
     `Load`. A request before `Load` (including during `Connect`'s initialization) cancels the opening: in `Load`
     the framework sets `Opacity = 0`, moves the Form so its whole bounds lie outside
     `SystemInformation.VirtualScreen`, then closes it — modal `BeginInvoke(Close)`, modeless `Close()`.
     The caller keeps writing `presenter.Connect(form); form.Show();` / `Application.Run(form)`.
     Measured: no flash in either mode (closing in `Load` / `Shown` without hiding flashes for modal; opacity alone
     set in `Load` still flashes a little; a fixed (−9999, −9999) is not guaranteed off-screen because monitors
     left of / above the primary have negative coordinates).
     Limits (documented): an MDI child (opacity has no effect, location is parent-relative) is closed in `Load`
     without hiding and may flash; a Form already shown before `Connect` gets a normal gated close; for
     `Application.Run(form)` the cancellation ends the application, as expected. If another `FormClosing` handler
     cancels the hidden close, the framework restores opacity and location and the Form is shown normally — never
     an invisible live window. The cancellation closes without the gate (it is not a user close).

## Semantics after the window is shown

`RequestClose(result, status)` supplies a **candidate result** and starts a close attempt; it does not guarantee
closing.

| Situation | Behavior |
|---|---|
| Fresh `RequestClose` | Start a `Normal` attempt seeded with the result/status |
| Gate allows, did not call `RequestClose` | The caller receives the seeded result/status |
| Gate calls `RequestClose(other)` (save on close with a result) | Replaces the attempt's result; last call wins (unchanged rule) |
| Gate returns false | Window stays open; result discarded; not logged as an error |
| Gate throws / faults / is cancelled | Blocks; logged as today (cancellation not as an error) |
| Async gate pending | Window stays open; further ✕ or `RequestClose` do not start another gate — a `RequestClose` updates the pending attempt's result; a veto discards it |
| `RequestClose(ex / message, Error)` | Also gated: `Error` says what the caller receives, not "force" |
| System shutdown / Task Manager | Unchanged: allowed without the gate |
| Another handler or an owned child cancels the actual close | Caller not notified; the candidate does not leak into a later close |

Consequence to document: the invariant "finalize dirty state (`AcceptChanges`) before `RequestClose`" makes
Save / Discard behave as today; a plain `RequestClose(Cancel)` on a dirty screen now prompts like ✕. A gate that
vetoes while busy (`_isSaving`) must have the busy state cleared before the handler's `RequestClose`. A veto does
not roll back work already done (e.g. `AcceptChanges` of a discard).

## WinForms implementation

- Gate only in the WinForms close controller (`WindowLifecycleController`); `WindowPresenterBaseCore` keeps
  delivery and pre-binding retention, no second gate.
- Push after shown: store the candidate → `form.Close()` → `FormClosing` runs the gate → `FormClosed` delivers.
  Remove the fresh-push `_suppressGate`; keep suppression only for the continuation of an already approved async
  attempt (a token scoped to that attempt). `OnFormClosing` must not reset the seeded result.
- No nested gate during `_gateRunning` / `_gatePending`; completion and convergence idempotent; stale callbacks
  ignored; UI thread only.
- Owned windows: an owner push deferred by a child's async gate must keep the owner's candidate when the owner
  close is replayed; a new ✕ starts clean. Same check for MDI-parent and `Application.Exit` replays.
- Modal dialogs: buttons bound to actions use `DialogResult.None` (WinForms would otherwise end the dialog before an
  async decision). Document; templates already avoid DialogResult on bound buttons (verify).
- Explicit opening state in the controller: `Opening → Shown (active) → Closed`, plus a "cancel opening" flag set
  by any request while `Opening`. Handle creation does not change it; hiding does not reset it. Managed: leaves
  `Opening` right before `Show` / `ShowDialog`. Adopted: leaves `Opening` when `Load` has run (or immediately when
  the Form is already visible at `Connect`). Requests held before the sink is bound keep "opening" provenance only
  if the Form is still opening at bind time; otherwise they are normal gated closes.
- Lifecycle events are wired **before** retained requests are delivered (today `Connect` binds, initializes, then
  wires; reorder with failure cleanup).
- Each close attempt has its own identity: candidate result, seeded/replaced, consumed or discarded with that
  attempt (fresh push, fresh ✕, approved async retry, owner / MDI-parent / `Application.Exit` replay). A veto by
  another handler does not leak the candidate into a later close; a child-deferred owner replay keeps the owner's
  candidate. No nested gate while one is running or pending; stale async callbacks ignored. UI thread only.

## PresenterTestHost

- Push → same logical attempt: seed, run the `Normal` gate, gate push replaces, allow → record once, veto →
  discard. `CloseRequested` / `CloseCount` / `CloseResult` / `CloseStatus` describe **accepted** closes.
- The simulated opening ends when the synchronous part of `Start` returns. Requests before that are an accepted
  cancellation of the opening (no gate), coalesced into one record (last result wins), as in production. Afterwards
  every `RequestClose` is gated. An accepted close is terminal for the host (later requests are ignored).
- `host.CanClose()` / `CanCloseAsync()` unchanged (repeatable policy checks).
- Add a test-only `Task WaitForCloseDecisionAsync()` (a void `RequestClose` may finish after `DispatchAsync`).

## Tests

Fresh push allow / veto / sync throw / async allow-veto-fault-cancel; result selection (seed kept, gate
replacement wins, last wins); push then ✕, ✕ then push, repeated pushes (one gate call, one accepted result);
before-shown cancel (bound before init, `OnInitialize`, sync part of `OnInitializeAsync`) managed modal/modeless
not shown, adopted closed in `Load`; Close action dirty Yes/No/Cancel; owned-window deferred replay keeps the
owner's result; shutdown bypass unchanged. Reverse `PresenterPush_SkipsGate_ConvergesWithResult`; update sample
tests that simulated the follow-up gate by hand.

## Docs

CLAUDE.md window-closing section (one gate, two initiators; before-shown cancel), wiki Concept-Window-Closing-Model
and HowTo-Handle-Window-Closing (tables, diagrams), Reference-WindowNavigator, Troubleshooting, HowTo-Test-A-Presenter,
Reference-ChangeTracker wording, `ComposeEmailPresenter` comments, templates' guidance (Close vs Discard), CHANGELOG
(BREAKING behavior change), FEATURES.

## Not added

`RequestCloseLikeUser`, `skipGate`, `force`, a new close reason, public presenter events, closing members on
`IWindowView`, Form-side close handlers; applications calling their own `CanCloseAsync` before `RequestClose`.
