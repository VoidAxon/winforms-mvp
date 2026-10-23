# RequestClose goes through CanClose — design

Date: 2026-10-23 · Status: draft for owner review · Source: real-screen validation V-28, V-34 · Reviewed with codex (one round)

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
3. No flash when a window is cancelled before it is shown (measured on Windows, V-34):
   - Managed (`WindowNavigator`): `Show` / `ShowDialog` is not called at all — modal and modeless.
   - Adopted (`Connect`, the caller shows the Form): in the Form's `Load`, set `Opacity = 0` and move it outside
     every monitor (`SystemInformation.VirtualScreen`, not a fixed −9999: monitors left of / above the primary have
     negative coordinates), then close — modal `BeginInvoke(Close)`, modeless `Close()`. Measured: no flash in
     either mode. (Closing in `Load` / `Shown` without hiding flashes for modal; opacity alone set in `Load` still
     flashes a little.)

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
- Before shown: a request held before binding or made during initialization marks "cancel opening". Managed: the
  navigator checks after initialization and converges without `Show` / `ShowDialog` (modal and modeless —
  modeless no longer shown-then-closed). Adopted: the controller hooks `Load` (V-34 technique 9). A request after
  `Shown` is a normal gated close.

## PresenterTestHost

- Push → same logical attempt: seed, run the `Normal` gate, gate push replaces, allow → record once, veto →
  discard. `CloseRequested` / `CloseCount` / `CloseResult` / `CloseStatus` describe **accepted** closes.
- Requests during initialization are recorded as accepted cancellation of the opening (no gate), as in production.
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
