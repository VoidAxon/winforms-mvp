# WinformsMVP.Testing package Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** A fifth package, `WinformsMVP.Testing`, that holds `PresenterTestHost` / `TestServices` and ready-made, optional fakes for every presenter-facing framework service, so applications write none.

**Architecture:** `src/WinformsMVP.Testing` (netstandard2.0, references Core only, namespace `WinformsMVP.Testing`). Core grants it `InternalsVisibleTo`. Fakes mirror production semantics (navigator keys/close order, result conversion shared from Core), throw on unscripted interactions, and expose a defined extension surface (virtual service methods, function/queue scripting, immutable call records).

**Tech Stack:** C# (LangVersion latest), netstandard2.0; tests xUnit 2.9.3 in `tests/WinformsMVP.Samples.Tests` (net48, CI) and a local net10.0 harness for the netstandard2.0 package.

**Spec:** `docs/superpowers/specs/2026-10-22-testing-package-design.md`

## Global Constraints

- New package id `WinformsMVP.Testing`, `netstandard2.0`, only dependency `WinformsMVP.Core`; no xUnit / NUnit / MSTest / mocking-library reference.
- Namespace `WinformsMVP.Testing` for every type (moved `PresenterTestHost` / `TestServices` keep it).
- No backward compatibility (owner, 2026-10-22): delete moved files from Core, no forwards.
- Unscripted confirmation / dialog / modal → `UnscriptedInteractionException` (message names kind and text/presenter), recorded first; each fake has an explicit permissive switch.
- Fakes: service methods `virtual`; call records immutable (`sealed class` with get-only properties); `FakeServices` sealed.
- Navigator mirrors `src/WinformsMVP/Services/Implementations/WindowNavigator.cs`; result conversion is the shared `CloseResults` (moved to Core, internal).
- File service: Windows path rules independent of the host OS; never `System.IO.Path` / current directory.
- Version only via tag (no `<Version>` in csproj). Commit trailer `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`. After each framework commit: `codex review --base master` (prefix `NODENV_VERSION=24.21.0` if codex is not on PATH).
- Local test command for the package (net10 harness): `dotnet test <scratchpad>/testingpkg` (Task 1 creates it); full suite runs in CI (net48).

## Review Focus

1. A `ShowWindow` whose `onClosed` reopens the same key must open a new window (key released before the callback).
2. `ShowAsModal<int>()` scripted with a `string` result must yield `Error`, never `Ok(0)`.
3. A presenter implementing only `IInitializable<TParam>` shown through a parameterless overload must throw (modal and modeless), not "open".
4. `InMemoryFileService` on Linux must resolve `C:\a\B.txt` and `c:/A/b.txt` to the same file.
5. Replacing a service in `fakes.Provider` before `Start` must be what the presenter uses (the default fake records nothing).

---

### Task 1: Package skeleton; move the host; share result conversion

**Files:**
- Create: `src/WinformsMVP.Testing/WinformsMVP.Testing.csproj` (netstandard2.0, `IsPackable=true`, PackageId/Description/README like Core's csproj), `src/WinformsMVP.Testing/README.md` (short).
- Move (git mv): `src/WinformsMVP.Core/Testing/PresenterTestHost.cs`, `TestServices.cs` → `src/WinformsMVP.Testing/`.
- Move: `src/WinformsMVP/Services/Implementations/CloseResults.cs` → `src/WinformsMVP.Core/Services/CloseResults.cs` (namespace `WinformsMVP.Services`, still `internal`).
- Modify: `src/WinformsMVP.Core/WinformsMVP.Core.csproj` (`InternalsVisibleTo Include="WinformsMVP.Testing"`), `winforms-mvp.sln` (add project), `tests/WinformsMVP.Samples.Tests/*.csproj` and any sample/test project using `PresenterTestHost` (add ProjectReference), `Directory.Build.props` if packability is opt-in there.
- Create: local harness `<scratchpad>/testingpkg/testingpkg.csproj` (net10.0, xUnit, references the new project, `Compile Include` of `tests/WinformsMVP.Samples.Tests/Testing/*.cs`).

- [ ] **Step 1:** Create the project and move files; fix `using`s in `WindowNavigator` / Connect code that referenced `CloseResults`.
- [ ] **Step 2:** Build: `dotnet build winforms-mvp.sln` → `0 Error(s)`.
- [ ] **Step 3:** `grep -rn "WinformsMVP.Core/Testing" src` → nothing; `PresenterTestHostTests` still compile.
- [ ] **Step 4:** Commit `refactor!: WinformsMVP.Testing package; PresenterTestHost and TestServices move out of Core`.

### Task 2: CapturingActionBinder

**Interfaces — Produces:** `public class CapturingActionBinder : IViewActionBinder { ViewActionDispatcher Dispatcher {get;}; void Bind(ViewActionDispatcher); void Dispatch(ViewAction, object payload = null); Task DispatchAsync(ViewAction, object payload = null); bool CanDispatch(ViewAction); void Dispatch<T>(ViewAction<T>, T); Task DispatchAsync<T>(ViewAction<T>, T); bool CanDispatch<T>(ViewAction<T>) }` — `virtual` Dispatch/DispatchAsync/CanDispatch.

- [ ] **Step 1: Tests** `tests/WinformsMVP.Samples.Tests/Testing/CapturingActionBinderTests.cs`:
  `Dispatch_BeforeBind_ThrowsWithAHint` (InvalidOperationException mentions binding); `Dispatch_RunsTheRegisteredHandler_AndHonorsCanExecute` (host a tiny presenter; disabled action → handler not run, `CanDispatch` false); `DispatchAsync_AwaitsAnAsyncHandler`; `TypedOverloads_PassThePayload`.
- [ ] **Step 2:** Run in harness → FAIL (type missing).
- [ ] **Step 3:** Implement `src/WinformsMVP.Testing/CapturingActionBinder.cs` (from `tests/.../Mocks/CapturingActionBinder.cs`, plus the before-bind message).
- [ ] **Step 4:** Harness → PASS. Commit `feat(testing): CapturingActionBinder`.

### Task 3: Messages, toasts, logs, and the unscripted exception

**Interfaces — Produces:**
- `public class UnscriptedInteractionException : InvalidOperationException`.
- `public enum MessageKind { Info, Warning, Error, YesNo, OkCancel, YesNoCancel }`; `public sealed class MessageCall { MessageKind Kind; string Text; string Caption }`.
- `public class FakeMessageService : IMessageService` — `IReadOnlyList<MessageCall> Calls`; `Func<MessageCall,bool> YesNoHandler, OkCancelHandler`; `Func<MessageCall,ConfirmResult> YesNoCancelHandler`; `EnqueueYesNo(params bool[])`, `EnqueueOkCancel(params bool[])`, `EnqueueYesNoCancel(params ConfirmResult[])`; `AnswerUnscripted(bool yes)` (YesNo/OkCancel → yes, YesNoCancel → Yes/No); `ClearCalls()`, `ResetScripts()`; all `IMessageService` methods virtual.
- `public sealed class ToastCall { string Text; ToastType Type; int? Duration }`; `public class FakeToastService : IToastService` — `Calls`, `ClearCalls()`.
- `public sealed class LogEntry { string Category; LogLevel Level; Exception Exception; string Message; string Template; IReadOnlyList<object> Args }`; `public class RecordingLoggerFactory : ILoggerFactory` — `IReadOnlyList<LogEntry> Entries`, `Clear()`; loggers enabled for all levels; `Message` via `MessageFormatter.Format`.

- [ ] **Step 1: Tests** `Testing/FakeMessageServiceTests.cs`: `Info_IsRecorded`; `ConfirmYesNo_Unscripted_ThrowsNamingTheQuestion_AndIsRecorded`; `Queue_IsUsedBeforeTheHandler_InOrder`; `Handler_SeesTheCall_IncludingCaption`; `AnswerUnscripted_AnswersYes`; `YesNoCancel_QueueAndHandler`. `Testing/FakeToastServiceTests.cs`: `Show_IsRecordedWithDuration`. `Testing/RecordingLoggerFactoryTests.cs`: `LogError_RecordsLevelCategoryFormattedMessageAndException`.
- [ ] **Step 2:** Harness → FAIL. **Step 3:** Implement (`src/WinformsMVP.Testing/FakeMessageService.cs`, `FakeToastService.cs`, `RecordingLoggerFactory.cs`, `UnscriptedInteractionException.cs`, records in their service's file). **Step 4:** Harness → PASS. Commit `feat(testing): message, toast and logger fakes`.

### Task 4: FakeDialogProvider

**Interfaces — Produces:** `public enum DialogKind { OpenFile, OpenMultipleFiles, SaveFile, FolderBrowser, Color }`; `public sealed class DialogCall { DialogKind Kind; object Options }`; `public class FakeDialogProvider : IDialogProvider` — handlers `OpenFileHandler`, `OpenMultipleFilesHandler`, `SaveFileHandler`, `FolderBrowserHandler` (`Func<TOptions, InteractionResult<…>>`), `ColorHandler` (`Func<ColorDialogOptions, InteractionResult<Color>>`); `EnqueueOpenFile/OpenMultipleFiles/SaveFile/FolderBrowser/Color(InteractionResult<…>)`; `CancelUnscripted()`; `Calls`, `ClearCalls()`; methods virtual.

- [ ] **Step 1: Tests** `Testing/FakeDialogProviderTests.cs`: `SaveFile_Unscripted_Throws`; `SaveFile_Queued_ReturnsIt_AndRecordsTheOptions`; `NullOptions_ArePreservedInTheCall`; `Handler_ExceptionPropagates`; `CancelUnscripted_ReturnsCancel`.
- [ ] **Step 2–4:** FAIL → implement `src/WinformsMVP.Testing/FakeDialogProvider.cs` → PASS. Commit `feat(testing): dialog fake`.

### Task 5: FakeWindowNavigator

**Interfaces — Consumes:** `CloseResults.Build(object, InteractionStatus)` / `Build<TResult>(object, InteractionStatus)` (Core, internal, Task 1).
**Produces:**
- `public sealed class ModalRequest<TPresenter> { TPresenter Presenter; object Parameters; bool HasParameters; IWindowView Owner }`.
- `public sealed class NavigationCall { object Presenter; Type PresenterType; bool Modal; object Parameters; bool HasParameters; Type ResultType; IWindowView Owner; object Key; bool Activated }`.
- `public class FakeWindow { object Presenter; object Parameters; bool HasParameters; object Key; IWindowView Owner; IWindowView View; bool IsOpen; int Activations; void Close(); void CloseWithResult<T>(T value, InteractionStatus status = InteractionStatus.Ok); void CloseWithError(string message, Exception exception = null) }`.
- `public class FakeWindowNavigator : IWindowNavigator` — the 8 interface methods (virtual, via `protected virtual` `ShowModalCore` / `ShowModelessCore`); `RespondTo<TPresenter, TResult>(Func<ModalRequest<TPresenter>, InteractionResult<TResult>>)`, `RespondTo<TPresenter>(Func<ModalRequest<TPresenter>, InteractionResult>)`; `CancelUnscripted()`; `Calls`, `Windows`, `LastModalParameters<T>()`.
- Algorithms (mirror production): modeless — compute key once (`(presenter.GetType(), keySelector?.Invoke(presenter))`; null key → no de-dup); open existing → `Activations++`, record `Activated`, `presenter.Dispose()`, return existing `View`; otherwise parameterless-initializer check (same message as `WindowNavigator.ParameterlessInitializer`), create `FakeWindow` holding `Action<object, InteractionStatus> deliver = (r, s) => onClosed?.Invoke(CloseResults.Build<TResult>(r, s))`. Close: if closed return; `IsOpen=false`; release key; try `deliver(result, status)` finally `Presenter.Dispose()`. Modal: initializer check, record, responder by exact presenter runtime type (last wins) else unscripted; a responder whose `TResult` differs from the call's converts through `CloseResults.Build<TCall>(value, status)`; finally dispose presenter.

- [ ] **Step 1: Tests** `Testing/FakeWindowNavigatorTests.cs` (use a tiny `WindowPresenterBase` presenter + a parameterized one): `Modal_Unscripted_Throws`; `Modal_Typed_ReturnsTheValue_ThroughFluentApi` (`Navigator.For(p).WithParam(x).ShowAsModal<int>()`); `Modal_WrongResultType_IsError` (Review Focus 2); `Modal_DisposesThePresenter`; `Modal_ParameterizedOnlyPresenter_ThroughParameterlessOverload_Throws` (Review Focus 3); `Modeless_SameKey_ActivatesAndDisposesTheNewPresenter`; `Modeless_KeysAreScopedByPresenterType`; `Modeless_NullKey_NoDedup`; `Close_WithResult_DeliversTypedResult_ThenDisposes`; `Close_FromOnClosed_ReopeningTheSameKey_OpensANewWindow` (Review Focus 1); `Close_Twice_DoesNothing`; `CloseWithError_CarriesTheMessage`; `Calls_RecordParametersAndHasParameters`.
- [ ] **Step 2–4:** FAIL → implement `src/WinformsMVP.Testing/FakeWindowNavigator.cs` (+ `FakeWindow.cs`) → PASS. Commit `feat(testing): window navigator fake mirroring WindowNavigator`.

### Task 6: InMemoryFileService

**Interfaces — Produces:** `public enum FileOperation { Read, Write, Append, Copy, Move, Delete, CreateDirectory, DeleteDirectory, Enumerate }`; `public sealed class FileOperationCall { FileOperation Operation; string Path; string Destination }`; `public class InMemoryFileService : IFileService` — ctor `(string baseDirectory = @"C:\test")`; all 29 members virtual; `SeedFile(string path, string text, Encoding encoding = null)`, `SeedFile(string path, byte[] bytes)`, `SeedDirectory(string path)`; `IReadOnlyCollection<string> FilePaths`, `DirectoryPaths`; `Func<FileOperationCall, Exception> Failure`.
- Path algorithm (`WindowsPath` internal static): replace `/` with `\`; rooted if `X:\` or `\\`; relative → combine with base; split, drop `.`, pop on `..`; key = upper-invariant full path for lookup, original casing kept for display. Helpers `Combine`, `GetFileName`, `GetFileNameWithoutExtension`, `GetDirectoryName`, `GetExtension`, `GetFullPath` on the same rules. Patterns: `*`/`?` → regex, matched against the file name, case-insensitive.

- [ ] **Step 1: Tests** `Testing/InMemoryFileServiceTests.cs`: `PathsAreCaseAndSeparatorInsensitive_OnAnyHost` (Review Focus 4); `Write_WithoutParentDirectory_ThrowsDirectoryNotFound`; `SeedFile_CreatesParents`; `ReadAllText_RoundTripsWithEncoding_AndHonorsBom`; `AppendAllText_AppendsBytes`; `ReadAllLines_SplitsLikeFile`; `Copy_ToExisting_WithoutOverwrite_ThrowsIOException`; `Move_MissingSource_ThrowsFileNotFound`; `GetFiles_ImmediateChildren_WithPattern`; `DeleteDirectory_NonEmpty_WithoutRecursive_Throws`; `NullPath_ThrowsArgumentNull`; `Failure_IsThrownBeforeAnyChange`; `PathHelpers_FollowWindowsRules` (`GetDirectoryName(@"C:\a\b.txt") == @"C:\a"`, `GetExtension("x.CSV") == ".CSV"`).
- [ ] **Step 2–4:** FAIL → implement `src/WinformsMVP.Testing/InMemoryFileService.cs` (+ `WindowsPath.cs`) → PASS. Commit `feat(testing): in-memory file service with host-independent Windows paths`.

### Task 7: TestServices.CreateFakes

**Interfaces — Produces:** `public sealed class FakeServices { DefaultServiceProvider Provider; FakeMessageService Messages; FakeToastService Toasts; FakeWindowNavigator Navigator; FakeDialogProvider Dialogs; InMemoryFileService Files; RecordingLoggerFactory Logs }`; `public static FakeServices TestServices.CreateFakes()`.

- [ ] **Step 1: Tests** `Testing/TestServicesFakesTests.cs`: `CreateFakes_RegistersEveryFake` (resolve each interface → same instance as the property); `APresenterUsesTheFakes_EndToEnd` (host a presenter that confirms, toasts, saves a file, opens a modal); `ReplacingAServiceBeforeStart_IsWhatThePresenterUses` (Review Focus 5).
- [ ] **Step 2–4:** FAIL → implement in `src/WinformsMVP.Testing/TestServices.cs` / `FakeServices.cs` → PASS. Commit `feat(testing): TestServices.CreateFakes`.

### Task 8: The framework's own tests use the package fakes (owner's decision B)

**Files:** delete `tests/WinformsMVP.Samples.Tests/Mocks/{CapturingActionBinder,MockMessageService,MockToastService,MockWindowNavigator,MockDialogProvider,MockFileService,MockServices}.cs`; update every test that used them (switch to `TestServices.CreateFakes()` and the package types; script answers explicitly where the old mocks answered permissively). Keep app-specific mocks (views, repositories).

- [ ] **Step 1:** Replace usages; `dotnet build winforms-mvp.sln` → 0 errors.
- [ ] **Step 2:** Every test that relied on a permissive default now scripts its answer (grep the old defaults: `MockMessageService` YesNo default, navigator default results) — reviewed by reading each changed test.
- [ ] **Step 3:** Push the branch; CI → all tests pass. Commit `test: framework tests use WinformsMVP.Testing fakes`.

### Task 9: Templates, release, docs

**Files:**
- `templates/content/tests/*.csproj`: add `<PackageReference Include="WinformsMVP.Testing" Version="MVP_PACKAGE_VERSION" />`; delete `templates/content/tests/CapturingActionBinder.cs`; `ExamplePresenterTests.cs` uses `TestServices.CreateFakes()` and disposes the host.
- `tests/templates/verify-templates.sh`: pack `src/WinformsMVP.Testing` into the local feed.
- `.github/workflows/release.yml`: `dotnet pack src/WinformsMVP.Testing/...` and publish (5 packages); `ci.yml` if it packs.
- Docs: `README.md` (packages, install), `CLAUDE.md` (package list, testing section), `FEATURES.md` §14, `wiki/HowTo-Test-A-Presenter.md`, `wiki/HowTo-Use-Templates.md` if it mentions the binder copy, `CHANGELOG.md` (Added + BREAKING move).

- [ ] **Step 1:** Edit; `tests/templates/verify-templates.sh` → `== All templates OK`.
- [ ] **Step 2:** `dotnet build winforms-mvp.sln -c Release` and `dotnet pack src/WinformsMVP.Testing -c Release -p:Version=0.0.0-local` → nupkg contains `lib/netstandard2.0/WinformsMVP.Testing.dll`.
- [ ] **Step 3:** Commit `docs/build: ship WinformsMVP.Testing (templates, release, docs)`.

### Task 10 (local, sample repository): use the package

`consultation-record-sample` (local only, never pushed / sent to external services): `tests/ConsultationSample.Tests` references `WinformsMVP.Testing` (ProjectReference); delete its `FakeMessageService`, `FakeToastService`, `FakeNavigator`, `FakeDialogProvider`, `FakeFileService`, `CapturingActionBinder`, `FakeServices`; tests use `TestServices.CreateFakes()`. `dotnet test tests/ConsultationSample.Tests` → all pass. Commit on `impl`, merge to `main` / `mvvm`; record in `docs/validation-log.md` (V-19 resolved).
