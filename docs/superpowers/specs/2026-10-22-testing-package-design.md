# WinformsMVP.Testing package — design

Date: 2026-10-22 · Status: approved (after codex review; owner decided A = throw when unscripted, B = migrate the framework's own test mocks now) · Source: real-screen validation V-19

## Why

Every application that tests its presenters hand-writes the same fakes. Today three copies exist
(the real-screen sample, this repository's own tests, the `winformsmvp-tests` template), none uses a
mocking library:

| Fake | sample | framework tests | template |
|---|---|---|---|
| `CapturingActionBinder` (dispatch like a bound control) | 28 lines | 66 | 32 |
| messages (record calls, scripted confirm answers) | 28 | 103 | — |
| toasts (record) | 13 | 25 | — |
| navigator (record windows, scripted modal results, close a non-modal window) | 101 | 134 | — |
| dialogs (scripted answers) | 26 | 81 | — |
| files (`IFileService`, 29 members) | 55 (partial) | 252 | — |

## Goals

- Ship ready-made fakes so an application writes none of the above.
- **Flexibility (owner's requirement):** the fakes are a convenience, never a requirement. A test can use
  all of them, replace any one with its own implementation, or use none (hand-written fakes, Moq, …).
- Keep test code out of production assemblies: a separate package referenced only by test projects.
- No dependency on any test framework (xUnit / NUnit / MSTest) or mocking library.

## Non-goals

- Fakes for WinForms-only, view-side services (`IWinFormsDialogProvider`, `IWinFormsToastService`,
  `IMessageDialogBackend`): presenters never use them.
- Backward compatibility: the framework has no users yet (owner's statement); types move freely.

## Package

- New project `src/WinformsMVP.Testing/WinformsMVP.Testing.csproj`, `netstandard2.0`, PackageId
  `WinformsMVP.Testing`, references `WinformsMVP.Core` only. Namespace `WinformsMVP.Testing`.
- **Moves** `PresenterTestHost` and `TestServices` from Core (`src/WinformsMVP.Core/Testing/`) to the
  new project (same namespace, so test code only changes its package reference).
- Core gets `InternalsVisibleTo("WinformsMVP.Testing")` (the host uses the internal
  `IPresenterServicesBinding` and `ICloseParticipant`).
- Release workflow packs and publishes it (5 packages). README "install" section, CLAUDE.md package list,
  FEATURES.md §14, wiki `HowTo-Test-A-Presenter` updated.
- The `winformsmvp-tests` template references `WinformsMVP.Testing` and drops its own
  `CapturingActionBinder` copy; `tests/templates/verify-templates.sh` keeps working (local feed holds
  the new package).

## API

All types in `WinformsMVP.Testing`. **Extension surface (not "everything virtual")**: each fake's service
methods are `virtual` (override one operation, keep the rest); recorded calls are immutable records;
scripting is by properties (functions) and queues. Containers (`FakeServices`), records and
`PresenterTestHost` are not designed for inheritance.

**Unscripted interaction throws** (owner's decision A): a confirmation, a dialog or a modal window the test
did not script throws `UnscriptedInteractionException` naming what was asked, so an unexpected question
never silently takes a branch. Each fake has an explicit permissive switch for tests that do not care
(e.g. `messages.AnswerUnscripted(yes: true)`, `navigator.CancelUnscripted()`, `dialogs.CancelUnscripted()`).
The call is recorded before answering or throwing.

### Entry point

```csharp
public static class TestServices
{
    public static DefaultServiceProvider Create();   // unchanged: a silent ILoggerFactory only
    public static FakeServices CreateFakes();        // Create() + the default fakes below registered
}

public sealed class FakeServices
{
    public DefaultServiceProvider Provider { get; }
    // The DEFAULT fake instances registered in Provider. Replace any service before
    // PresenterTestHost.Start (presenters resolve services once): Provider.RegisterInstance<IX>(mine);
    // the property then still returns the (unused) default fake.
    public FakeMessageService Messages { get; }
    public FakeToastService Toasts { get; }
    public FakeWindowNavigator Navigator { get; }
    public FakeDialogProvider Dialogs { get; }
    public InMemoryFileService Files { get; }
    public RecordingLoggerFactory Logs { get; }       // replaces the silent logger
}
```

`DefaultServiceProvider` does not dispose instances registered with `RegisterInstance`; the fakes hold no
unmanaged resources, so no disposal contract is needed.

### `CapturingActionBinder : IViewActionBinder`

As today (template / tests): captures the dispatcher the framework binds; `Dispatch`, `DispatchAsync`,
`CanDispatch` and the typed `ViewAction<T>` overloads go through it (same channel as a bound control, so
`canExecute` applies). Clear `InvalidOperationException` before the presenter bound it.

### `FakeMessageService : IMessageService`

- `IReadOnlyList<MessageCall> Calls`; `MessageCall { MessageKind Kind; string Text; string Caption }`;
  `MessageKind { Info, Warning, Error, YesNo, OkCancel, YesNoCancel }`.
- Answers, by precedence **queued → handler → unscripted (throw, or the permissive default)**:
  `EnqueueYesNo(params bool[])`, `EnqueueOkCancel(params bool[])`, `EnqueueYesNoCancel(params ConfirmResult[])`;
  `Func<MessageCall, bool> YesNoHandler`, `OkCancelHandler`; `Func<MessageCall, ConfirmResult> YesNoCancelHandler`.
- `ClearCalls()`; scripts are reset separately (`ResetScripts()`).

### `FakeToastService : IToastService`

`IReadOnlyList<ToastCall> Calls` (`Text`, `Type`, `Duration`), `ClearCalls()`.

### `RecordingLoggerFactory : ILoggerFactory`

Records `LogEntry { Category, Level, Exception, Message (formatted), Template, Args (copied) }`;
`IReadOnlyList<LogEntry> Entries`; all levels enabled.

### `FakeDialogProvider : IDialogProvider`

- Typed handlers per dialog: `Func<OpenFileDialogOptions, InteractionResult<string>> OpenFileHandler`,
  `OpenMultipleFilesHandler`, `SaveFileHandler`, `FolderBrowserHandler`, `ColorHandler`; typed queues
  `EnqueueSaveFile(InteractionResult<string>)` etc. Precedence queued → handler → unscripted.
- One history: `IReadOnlyList<DialogCall> Calls` with `DialogKind` and the options object as passed
  (null preserved). Handler exceptions propagate (they are test setup errors).
- No simulation of native behavior (filters, extension insertion, file checks).

### `FakeWindowNavigator : IWindowNavigator`

Implements the 8 overloads with the same constraints/defaults, routed through one modal and one modeless
core (so the fluent `Navigator.For(...)` API works unchanged). Mirrors `WindowNavigator`:

- **Parameterless overloads** throw `InvalidOperationException` for a presenter that implements only
  `IInitializable<TParam>` (same message as production), after the activation check.
- **Modal**: records a `NavigationCall`, answers from a typed responder, then **disposes the presenter**
  (production disposes it when the window closes).
  `RespondTo<TPresenter, TResult>(Func<ModalRequest<TPresenter>, InteractionResult<TResult>>)`,
  `RespondTo<TPresenter>(Func<ModalRequest<TPresenter>, InteractionResult>)` for no-result calls;
  `ModalRequest` exposes `Presenter`, `Parameters`, `HasParameters`, `Owner`. A responder registered for a
  result type that differs from the caller's yields `Error` (as production's result conversion).
  Last registration for a presenter type wins; unscripted → throw (or `CancelUnscripted()`).
- **Modeless**: key identity `(presenter.GetType(), key)`; no selector or a null key → no de-duplication;
  the selector is evaluated once. Same key while open → returns the same `IWindowView`, increments that
  window's `Activations`, **disposes the incoming presenter**, ignores its parameters / owner / callback.
- `FakeWindow { Presenter, Parameters, HasParameters, Key, Owner, View (stable stub IWindowView), IsOpen,
  Activations }` with `Close()` (user close → Cancel), `CloseWithResult<T>(T value, InteractionStatus status = Ok)`,
  `CloseWithError(string message, Exception exception = null)`. Closing: mark closed and release the key,
  then invoke the caller's `onClosed` with the result converted **by production's conversion**
  (wrong type / missing non-nullable value → Error; cancel discards the value), then dispose the presenter
  in `finally`. A second close does nothing; callback exceptions propagate.
- `IReadOnlyList<NavigationCall> Calls` (every request, incl. activations: presenter type, parameters,
  `HasParameters`, expected result type, owner, key, `Activated`), `IReadOnlyList<FakeWindow> Windows`
  (incl. closed), `T LastModalParameters<T>()`.
- Presenters passed in are **not** attached or initialized (there is no Form). To test the child's behavior,
  host a separate instance with `PresenterTestHost` — the navigator owns (and disposes) the one it received.
- Production's internal result conversion (`CloseResults`) moves to Core (internal) so both share it.

### `InMemoryFileService : IFileService`

- **Windows path rules independent of the host OS**: case-insensitive, `\` and `/` accepted, `.`/`..`
  normalized, relative paths resolved against an instance base (`C:	est` by default); path helpers
  (`Combine`, `GetFileName`, `GetDirectoryName`, `GetExtension`, `GetFullPath`, …) implemented with the same
  rules (never the process current directory or `System.IO.Path`).
- Same argument validation as `FileService` (null / empty path → `ArgumentNullException`).
- Ordinary writes, copies and moves **require an existing parent directory** (`DirectoryNotFoundException`);
  only `CreateDirectory` and the setup helpers create parents. Destination collisions / overwrite rules
  (`IOException`), missing files (`FileNotFoundException`), file-vs-directory conflicts as `System.IO`.
- Content is bytes: text APIs encode/decode through memory streams (BOM written by an encoding is kept and
  honored on read; `ReadAllLines` splits like `File.ReadAllLines`); append appends bytes; byte arrays are
  copied in and out.
- `GetFiles` / `GetDirectories`: immediate children, `*` and `?` patterns, no promised order.
- Setup and inspection: `SeedFile(path, text | bytes)`, `SeedDirectory(path)` (create parents),
  `IReadOnlyCollection<string> FilePaths`, `IReadOnlyCollection<string> DirectoryPaths`.
- Failure injection: `Func<FileOperationCall, Exception> Failure` (operation, path, destination), thrown
  before any change; e.g. simulate a file locked by Excel.
- Out of scope: ACLs, locks/sharing, timestamps, links, device paths, long-path quirks.

## Flexibility, concretely

```csharp
var fakes = TestServices.CreateFakes();                                   // all
fakes.Provider.RegisterInstance<IFileService>(new MyFileService());      // replace one (before Start)
fakes.Messages.YesNoHandler = m => m.Text.Contains("削除");               // script one
class MyNavigator : FakeWindowNavigator { public override ... }          // override an operation
var sp = TestServices.Create(); sp.RegisterInstance<IMessageService>(mock.Object);   // none
```

## Testing the testing package

- Unit tests for every fake in `tests/WinformsMVP.Samples.Tests` (net48, CI); the package is netstandard2.0,
  so the fakes' tests also run locally on net10.0. Key cases: all 8 navigator overloads and the fluent API,
  key/activation/duplicate disposal, close order and re-entrancy (reopening the same key from `onClosed`),
  typed results incl. wrong type; message/dialog precedence and unscripted throw; file-system semantics
  (parents required, collisions, BOM, append, patterns, host-independent paths); replacing each service.
- **Owner's decision B**: this repository's own tests switch their shared service mocks
  (`Mocks/MockMessageService`, `MockToastService`, `MockWindowNavigator`, `MockDialogProvider`,
  `MockFileService`, `CapturingActionBinder`, `MockServices`) to the package fakes now, scripting answers
  explicitly. App-specific mocks (views, repositories) stay.
- The `winformsmvp-tests` template references `WinformsMVP.Testing` (`PrivateAssets="all"` not needed for a
  test project) and drops its `CapturingActionBinder` copy; `verify-templates.sh` packs the new package
  into its local feed.
