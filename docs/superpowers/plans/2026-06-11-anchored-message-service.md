# Anchored Message Service Implementation Plan (Phase 3 of 3)

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Cursor-anchored feedback (toast + message box) callable from a Presenter as `View.ShowToast(...)` — no coordinates, no ViewAction coupling, fully unit-testable by swapping `IAnchoredMessageService` in the `ServiceLocator`.

**Architecture:** A minimal 2-method service interface (`IAnchoredMessageService`: one full-parameter toast method + one full-parameter message-box method returning `ConfirmResult`); all convenience overloads (`ShowInfo`, `ConfirmYesNo`, ...) are extension methods on the interface. The real implementation reads `Cursor.Position` at call time and delegates to the existing `AnchoredToast` / `AnchoredMessageBox` static utilities; it is registered as a built-in in `ServiceLocator` and `AddWinformsMVP`. The presenter-facing entry point is a set of `IViewBase` extension methods that resolve the service from `ServiceLocator.Current` — testable because the resolved service is replaceable. Includes the multi-monitor fix for `ToastNotification.ShowAnchored` (ported from the abandoned `feature/show-toast-for` exploration, commit `9d83b8b`).

**Design changes vs. the foundation spec's follow-on note:** the service is named `IAnchoredMessageService` (mirrors `IMessageService`), and the entry point is `IViewBase` extension methods — NOT a `PresenterBase` convenience accessor.

**Contract (goes in XML docs):** the anchor is the cursor position **at call time** — call synchronously inside the event/action handler so it equals the click point. After an `await` the cursor may have moved; use `Messages.ShowToast` (corner toast) for deferred feedback.

**Tech Stack:** .NET Framework (net40;net48), C#, xUnit 2.9.3. All new core code net40-safe. Tests that mutate `ServiceLocator.Current` use `[Collection("ServiceLocator")]` + `Reset()` (existing pattern).

---

### Task 1: Enums + `IAnchoredMessageService` + convenience extensions (mock-tested)

**Files:**
- Create: `src/WinformsMVP/Common/Interactions/MessageButtons.cs`
- Create: `src/WinformsMVP/Common/Interactions/MessageIcon.cs`
- Create: `src/WinformsMVP/Services/IAnchoredMessageService.cs`
- Create: `src/WinformsMVP/Services/AnchoredMessageServiceExtensions.cs`
- Test: `tests/WinformsMVP.Samples.Tests/Services/AnchoredMessageServiceExtensionsTests.cs`

- [ ] **Step 1: Write the failing test** (a recording fake proves the extensions forward the right full-parameter calls and map results)

```csharp
using System.Collections.Generic;
using WinformsMVP.Common;
using WinformsMVP.Common.Interactions;
using WinformsMVP.Services;
using Xunit;

namespace WinformsMVP.Samples.Tests.Services
{
    public class AnchoredMessageServiceExtensionsTests
    {
        private sealed class RecordingService : IAnchoredMessageService
        {
            public readonly List<object[]> Toasts = new List<object[]>();
            public readonly List<object[]> Messages = new List<object[]>();
            public ConfirmResult NextResult = ConfirmResult.Yes;

            public void ShowToast(string text, ToastType type, ToastOptions options)
                => Toasts.Add(new object[] { text, type, options });

            public ConfirmResult ShowMessage(string text, string caption, MessageButtons buttons, MessageIcon icon)
            {
                Messages.Add(new object[] { text, caption, buttons, icon });
                return NextResult;
            }
        }

        [Fact]
        public void ShowToast_TwoArg_ForwardsWithNullOptions()
        {
            var s = new RecordingService();
            s.ShowToast("hi", ToastType.Success);
            Assert.Single(s.Toasts);
            Assert.Equal("hi", s.Toasts[0][0]);
            Assert.Equal(ToastType.Success, s.Toasts[0][1]);
            Assert.Null(s.Toasts[0][2]);
        }

        [Theory]
        [InlineData("ShowInfo", MessageIcon.Information)]
        [InlineData("ShowWarning", MessageIcon.Warning)]
        [InlineData("ShowError", MessageIcon.Error)]
        public void ShowXxx_MapsToOkButtonAndIcon(string method, MessageIcon expectedIcon)
        {
            var s = new RecordingService();
            if (method == "ShowInfo") s.ShowInfo("t", "c");
            else if (method == "ShowWarning") s.ShowWarning("t", "c");
            else s.ShowError("t", "c");

            Assert.Single(s.Messages);
            Assert.Equal("t", s.Messages[0][0]);
            Assert.Equal("c", s.Messages[0][1]);
            Assert.Equal(MessageButtons.Ok, s.Messages[0][2]);
            Assert.Equal(expectedIcon, s.Messages[0][3]);
        }

        [Fact]
        public void ConfirmYesNo_UsesYesNoQuestion_AndMapsYesToTrue()
        {
            var s = new RecordingService { NextResult = ConfirmResult.Yes };
            Assert.True(s.ConfirmYesNo("sure?", "cap"));
            Assert.Equal(MessageButtons.YesNo, s.Messages[0][2]);
            Assert.Equal(MessageIcon.Question, s.Messages[0][3]);

            s.NextResult = ConfirmResult.No;
            Assert.False(s.ConfirmYesNo("sure?", "cap"));
        }

        [Fact]
        public void ConfirmOkCancel_UsesOkCancelQuestion_AndMapsYesToTrue()
        {
            var s = new RecordingService { NextResult = ConfirmResult.Yes };
            Assert.True(s.ConfirmOkCancel("go?", "cap"));
            Assert.Equal(MessageButtons.OkCancel, s.Messages[0][2]);
            Assert.Equal(MessageIcon.Question, s.Messages[0][3]);

            s.NextResult = ConfirmResult.Cancel;
            Assert.False(s.ConfirmOkCancel("go?", "cap"));
        }

        [Fact]
        public void ConfirmYesNoCancel_UsesYesNoCancelQuestion_AndReturnsRawResult()
        {
            var s = new RecordingService { NextResult = ConfirmResult.Cancel };
            Assert.Equal(ConfirmResult.Cancel, s.ConfirmYesNoCancel("pick", "cap"));
            Assert.Equal(MessageButtons.YesNoCancel, s.Messages[0][2]);
            Assert.Equal(MessageIcon.Question, s.Messages[0][3]);
        }
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/WinformsMVP.Samples.Tests/WinformsMVP.Samples.Tests.csproj --filter "FullyQualifiedName~AnchoredMessageServiceExtensionsTests"`
Expected: FAIL — types do not exist (compile error).

- [ ] **Step 3: Write the enums**

`src/WinformsMVP/Common/Interactions/MessageButtons.cs`:

```csharp
namespace WinformsMVP.Common.Interactions
{
    /// <summary>
    /// Framework-level button set for message dialogs, so Presenter-facing APIs never expose
    /// the WinForms <c>MessageBoxButtons</c> type. Mapped to the platform type inside the
    /// service implementation only.
    /// </summary>
    public enum MessageButtons
    {
        Ok,
        OkCancel,
        YesNo,
        YesNoCancel
    }
}
```

`src/WinformsMVP/Common/Interactions/MessageIcon.cs`:

```csharp
namespace WinformsMVP.Common.Interactions
{
    /// <summary>
    /// Framework-level icon kind for message dialogs, so Presenter-facing APIs never expose
    /// the WinForms <c>MessageBoxIcon</c> type. Mapped to the platform type inside the
    /// service implementation only.
    /// </summary>
    public enum MessageIcon
    {
        None,
        Information,
        Warning,
        Error,
        Question
    }
}
```

- [ ] **Step 4: Write the interface** — exactly two full-parameter methods

`src/WinformsMVP/Services/IAnchoredMessageService.cs`:

```csharp
using WinformsMVP.Common;
using WinformsMVP.Common.Interactions;

namespace WinformsMVP.Services
{
    /// <summary>
    /// Shows feedback anchored at the cursor position: a non-blocking toast or a blocking
    /// message box, both appearing where the user just clicked. The anchor is the cursor
    /// position <b>at call time</b> — call synchronously inside the event/action handler so it
    /// equals the click point. After an <c>await</c> the cursor may have moved; use
    /// <see cref="IMessageService.ShowToast(string, ToastType, int)"/> (corner toast) for
    /// deferred feedback.
    /// </summary>
    /// <remarks>
    /// The interface is deliberately minimal — one full-parameter method per feedback kind —
    /// so implementations (including test mocks) implement exactly two methods. All
    /// convenience overloads (<c>ShowInfo</c>, <c>ConfirmYesNo</c>, ...) are extension methods
    /// in <see cref="AnchoredMessageServiceExtensions"/>. Presenters do not take this service
    /// as a dependency; they call the <c>IViewBase</c> extension methods (e.g.
    /// <c>View.ShowToast(...)</c>), which resolve it from <see cref="ServiceLocator.Current"/>.
    /// </remarks>
    public interface IAnchoredMessageService
    {
        /// <summary>Shows a non-blocking toast anchored at the current cursor position.</summary>
        void ShowToast(string text, ToastType type, ToastOptions options);

        /// <summary>
        /// Shows a blocking message box anchored at the current cursor position and returns the
        /// user's choice. <c>OK</c> maps to <see cref="ConfirmResult.Yes"/>.
        /// </summary>
        ConfirmResult ShowMessage(string text, string caption, MessageButtons buttons, MessageIcon icon);
    }
}
```

- [ ] **Step 5: Write the convenience extensions**

`src/WinformsMVP/Services/AnchoredMessageServiceExtensions.cs`:

```csharp
using WinformsMVP.Common;
using WinformsMVP.Common.Interactions;

namespace WinformsMVP.Services
{
    /// <summary>
    /// Convenience overloads for <see cref="IAnchoredMessageService"/>. The interface itself
    /// carries only the two full-parameter methods; everything here forwards to them.
    /// </summary>
    public static class AnchoredMessageServiceExtensions
    {
        /// <summary>Shows an anchored toast with default options.</summary>
        public static void ShowToast(this IAnchoredMessageService service, string text, ToastType type)
            => service.ShowToast(text, type, null);

        /// <summary>Shows an anchored information message.</summary>
        public static void ShowInfo(this IAnchoredMessageService service, string text, string caption = "")
            => service.ShowMessage(text, caption, MessageButtons.Ok, MessageIcon.Information);

        /// <summary>Shows an anchored warning message.</summary>
        public static void ShowWarning(this IAnchoredMessageService service, string text, string caption = "")
            => service.ShowMessage(text, caption, MessageButtons.Ok, MessageIcon.Warning);

        /// <summary>Shows an anchored error message.</summary>
        public static void ShowError(this IAnchoredMessageService service, string text, string caption = "")
            => service.ShowMessage(text, caption, MessageButtons.Ok, MessageIcon.Error);

        /// <summary>Shows an anchored Yes/No confirmation. True when the user chose Yes.</summary>
        public static bool ConfirmYesNo(this IAnchoredMessageService service, string text, string caption = "")
            => service.ShowMessage(text, caption, MessageButtons.YesNo, MessageIcon.Question) == ConfirmResult.Yes;

        /// <summary>Shows an anchored OK/Cancel confirmation. True when the user chose OK.</summary>
        public static bool ConfirmOkCancel(this IAnchoredMessageService service, string text, string caption = "")
            => service.ShowMessage(text, caption, MessageButtons.OkCancel, MessageIcon.Question) == ConfirmResult.Yes;

        /// <summary>Shows an anchored Yes/No/Cancel confirmation and returns the raw result.</summary>
        public static ConfirmResult ConfirmYesNoCancel(this IAnchoredMessageService service, string text, string caption = "")
            => service.ShowMessage(text, caption, MessageButtons.YesNoCancel, MessageIcon.Question);
    }
}
```

- [ ] **Step 6: Run test to verify it passes**

Run: `dotnet test tests/WinformsMVP.Samples.Tests/WinformsMVP.Samples.Tests.csproj --filter "FullyQualifiedName~AnchoredMessageServiceExtensionsTests"`
Expected: PASS (7 test cases). Also `dotnet build src/WinformsMVP/WinformsMVP.csproj` → 0 errors (net40+net48).

- [ ] **Step 7: Commit**

```bash
git add src/WinformsMVP/Common/Interactions/MessageButtons.cs src/WinformsMVP/Common/Interactions/MessageIcon.cs src/WinformsMVP/Services/IAnchoredMessageService.cs src/WinformsMVP/Services/AnchoredMessageServiceExtensions.cs tests/WinformsMVP.Samples.Tests/Services/AnchoredMessageServiceExtensionsTests.cs
git commit -m "feat(anchored): add IAnchoredMessageService (2-method core) with convenience extensions"
```

---

### Task 2: Real implementation + built-in registration + multi-monitor fix

**Files:**
- Create: `src/WinformsMVP/Services/Implementations/AnchoredMessageService.cs`
- Modify: `src/WinformsMVP/Services/ServiceLocator.cs` (`RegisterBuiltIns`)
- Modify: `src/WinformsMVP.DependencyInjection/ServiceCollectionExtensions.cs` (`AddWinformsMVP`)
- Modify: `src/WinformsMVP/Services/Implementations/ToastNotification.cs` (multi-monitor fix)
- Modify: `tests/WinformsMVP.Samples.Tests/Services/ServiceLocatorBuiltInsTests.cs` (assert new built-in)
- Modify: `tests/WinformsMVP.Samples.Tests/DependencyInjection/ServiceCollectionExtensionsTests.cs` (assert M.E.DI resolves it)

- [ ] **Step 1: Write the failing tests** — append one assertion to `ServiceLocatorBuiltInsTests.Default_ResolvesBuiltInServices`:

```csharp
Assert.NotNull(sp.Resolve<IAnchoredMessageService>());
```

and one to the M.E.DI built-ins test in `ServiceCollectionExtensionsTests` (the `AddWinformsMvpBuiltInsTests` class):

```csharp
Assert.NotNull(provider.GetService<IAnchoredMessageService>());
```

Run: `dotnet test ... --filter "FullyQualifiedName~BuiltIns"` → FAIL (service not registered).

- [ ] **Step 2: Write the real implementation**

`src/WinformsMVP/Services/Implementations/AnchoredMessageService.cs`:

```csharp
using System.Windows.Forms;
using WinformsMVP.Common;
using WinformsMVP.Common.Interactions;

namespace WinformsMVP.Services.Implementations
{
    /// <summary>
    /// Default <see cref="IAnchoredMessageService"/>: reads <see cref="Cursor.Position"/> at
    /// call time and delegates to <see cref="AnchoredToast"/> / <see cref="AnchoredMessageBox"/>.
    /// All WinForms types (<c>MessageBoxButtons</c>, <c>MessageBoxIcon</c>, <c>DialogResult</c>,
    /// screen coordinates) stay inside this class — the interface speaks only framework types.
    /// </summary>
    public class AnchoredMessageService : IAnchoredMessageService
    {
        public void ShowToast(string text, ToastType type, ToastOptions options)
        {
            AnchoredToast.Show(text, type, Cursor.Position, options);
        }

        public ConfirmResult ShowMessage(string text, string caption, MessageButtons buttons, MessageIcon icon)
        {
            var result = AnchoredMessageBox.Show(text, caption, Map(buttons), Map(icon), Cursor.Position);
            return Map(result);
        }

        private static MessageBoxButtons Map(MessageButtons buttons)
        {
            switch (buttons)
            {
                case MessageButtons.OkCancel: return MessageBoxButtons.OKCancel;
                case MessageButtons.YesNo: return MessageBoxButtons.YesNo;
                case MessageButtons.YesNoCancel: return MessageBoxButtons.YesNoCancel;
                default: return MessageBoxButtons.OK;
            }
        }

        private static MessageBoxIcon Map(MessageIcon icon)
        {
            switch (icon)
            {
                case MessageIcon.Information: return MessageBoxIcon.Information;
                case MessageIcon.Warning: return MessageBoxIcon.Warning;
                case MessageIcon.Error: return MessageBoxIcon.Error;
                case MessageIcon.Question: return MessageBoxIcon.Question;
                default: return MessageBoxIcon.None;
            }
        }

        // OK and Yes are both affirmative; anything else is a cancel/dismiss.
        private static ConfirmResult Map(DialogResult result)
        {
            switch (result)
            {
                case DialogResult.OK:
                case DialogResult.Yes:
                    return ConfirmResult.Yes;
                case DialogResult.No:
                    return ConfirmResult.No;
                default:
                    return ConfirmResult.Cancel;
            }
        }
    }
}
```

- [ ] **Step 3: Register as built-in**

In `ServiceLocator.RegisterBuiltIns` add:

```csharp
provider.RegisterInstance<IAnchoredMessageService>(new AnchoredMessageService());
```

In `AddWinformsMVP` (the `TryAdd` block) add:

```csharp
services.TryAddSingleton<IAnchoredMessageService, AnchoredMessageService>();
```

- [ ] **Step 4: Port the multi-monitor fix**

In `src/WinformsMVP/Services/Implementations/ToastNotification.cs`, method `ShowAnchored(Point anchor)`, replace:

```csharp
var area = Screen.PrimaryScreen.WorkingArea;
```

with:

```csharp
// Clamp to the screen the anchor sits on (nearest screen if it is off-screen), so a
// toast anchored on a non-primary monitor stays there instead of being pulled back to
// the primary one. Mirrors AnchoredMessageBox.ClampToScreen.
var area = Screen.FromPoint(anchor).WorkingArea;
```

(Only in `ShowAnchored` — the corner-stacked `Show()` intentionally keeps `PrimaryScreen`.)

- [ ] **Step 5: Run tests**

Run: `dotnet test tests/WinformsMVP.Samples.Tests/WinformsMVP.Samples.Tests.csproj` → all pass (the two amended built-ins tests now green).

- [ ] **Step 6: Commit**

```bash
git add -A
git reset .claude/settings.local.json
git commit -m "feat(anchored): default AnchoredMessageService built-in (locator + M.E.DI); fix ShowAnchored multi-monitor clamp"
```

---

### Task 3: `IViewBase` entry-point extensions + test mock

**Files:**
- Create: `src/WinformsMVP/MVP/Views/AnchoredMessageViewExtensions.cs`
- Create: `tests/WinformsMVP.Samples.Tests/Mocks/MockAnchoredMessageService.cs`
- Test: `tests/WinformsMVP.Samples.Tests/Views/AnchoredMessageViewExtensionsTests.cs`

- [ ] **Step 1: Write the mock** (the reusable test double; also documents the test pattern)

`tests/WinformsMVP.Samples.Tests/Mocks/MockAnchoredMessageService.cs`:

```csharp
using System.Collections.Generic;
using WinformsMVP.Common;
using WinformsMVP.Common.Interactions;
using WinformsMVP.Services;

namespace WinformsMVP.Samples.Tests.Mocks
{
    /// <summary>
    /// Recording <see cref="IAnchoredMessageService"/> for tests. Because the
    /// <c>View.ShowToast(...)</c> view extensions resolve the service from the GLOBAL
    /// <see cref="ServiceLocator.Current"/>, tests that assert anchored feedback must install
    /// this mock globally and clean up:
    /// <code>
    /// [Collection("ServiceLocator")] // serialize: ServiceLocator.Current is process-global
    /// public class MyTests : System.IDisposable
    /// {
    ///     private readonly MockAnchoredMessageService _anchored = new MockAnchoredMessageService();
    ///     public MyTests() => ServiceLocator.Configure(reg =>
    ///         reg.RegisterInstance&lt;IAnchoredMessageService&gt;(_anchored));
    ///     public void Dispose() => ServiceLocator.Reset();
    /// }
    /// </code>
    /// A test that forgets this resolves the REAL service and pops a real window.
    /// </summary>
    public class MockAnchoredMessageService : IAnchoredMessageService
    {
        public sealed class ToastCall
        {
            public string Text;
            public ToastType Type;
            public ToastOptions Options;
        }

        public sealed class MessageCall
        {
            public string Text;
            public string Caption;
            public MessageButtons Buttons;
            public MessageIcon Icon;
        }

        public List<ToastCall> Toasts { get; } = new List<ToastCall>();
        public List<MessageCall> Messages { get; } = new List<MessageCall>();

        /// <summary>The result the next ShowMessage call returns. Default Yes.</summary>
        public ConfirmResult NextResult { get; set; } = ConfirmResult.Yes;

        public void ShowToast(string text, ToastType type, ToastOptions options)
            => Toasts.Add(new ToastCall { Text = text, Type = type, Options = options });

        public ConfirmResult ShowMessage(string text, string caption, MessageButtons buttons, MessageIcon icon)
        {
            Messages.Add(new MessageCall { Text = text, Caption = caption, Buttons = buttons, Icon = icon });
            return NextResult;
        }

        public void Clear() { Toasts.Clear(); Messages.Clear(); }
    }
}
```

- [ ] **Step 2: Write the failing test**

`tests/WinformsMVP.Samples.Tests/Views/AnchoredMessageViewExtensionsTests.cs`:

```csharp
using System;
using WinformsMVP.Common;
using WinformsMVP.Common.Interactions;
using WinformsMVP.MVP.Views;
using WinformsMVP.Samples.Tests.Mocks;
using WinformsMVP.Services;
using Xunit;

namespace WinformsMVP.Samples.Tests.Views
{
    [Collection("ServiceLocator")]
    public class AnchoredMessageViewExtensionsTests : IDisposable
    {
        private sealed class StubView : IViewBase { }

        private readonly MockAnchoredMessageService _anchored = new MockAnchoredMessageService();
        private readonly StubView _view = new StubView();

        public AnchoredMessageViewExtensionsTests()
            => ServiceLocator.Configure(reg => reg.RegisterInstance<IAnchoredMessageService>(_anchored));

        public void Dispose() => ServiceLocator.Reset();

        [Fact]
        public void ShowToast_ResolvesServiceFromLocator_AndForwards()
        {
            _view.ShowToast("saved", ToastType.Success);
            Assert.Single(_anchored.Toasts);
            Assert.Equal("saved", _anchored.Toasts[0].Text);
            Assert.Equal(ToastType.Success, _anchored.Toasts[0].Type);
            Assert.Null(_anchored.Toasts[0].Options);
        }

        [Fact]
        public void ShowToast_WithOptions_ForwardsOptions()
        {
            var options = new ToastOptions();
            _view.ShowToast("hi", ToastType.Info, options);
            Assert.Same(options, _anchored.Toasts[0].Options);
        }

        [Fact]
        public void ShowInfo_ForwardsThroughConvenienceMapping()
        {
            _view.ShowInfo("note", "cap");
            Assert.Equal(MessageButtons.Ok, _anchored.Messages[0].Buttons);
            Assert.Equal(MessageIcon.Information, _anchored.Messages[0].Icon);
        }

        [Fact]
        public void ConfirmYesNo_ReturnsMappedResult()
        {
            _anchored.NextResult = ConfirmResult.Yes;
            Assert.True(_view.ConfirmYesNo("sure?"));
            _anchored.NextResult = ConfirmResult.No;
            Assert.False(_view.ConfirmYesNo("sure?"));
        }
    }
}
```

(If `IViewBase` has members that make a bare `StubView` impossible, implement them minimally; check `src/WinformsMVP/MVP/Views/IViewBase.cs` first.)

Run: `dotnet test ... --filter "FullyQualifiedName~AnchoredMessageViewExtensionsTests"` → FAIL (extensions don't exist).

- [ ] **Step 3: Write the view extensions**

`src/WinformsMVP/MVP/Views/AnchoredMessageViewExtensions.cs`:

```csharp
using WinformsMVP.Common;
using WinformsMVP.Common.Interactions;
using WinformsMVP.Services;

namespace WinformsMVP.MVP.Views
{
    /// <summary>
    /// Cursor-anchored feedback as view behavior: <c>View.ShowToast(...)</c>,
    /// <c>View.ConfirmYesNo(...)</c>. From the presenter's point of view this is "tell the view
    /// to give feedback where the user just clicked" — no coordinates, no controls.
    /// </summary>
    /// <remarks>
    /// <para>
    /// These extensions resolve <see cref="IAnchoredMessageService"/> from the GLOBAL
    /// <see cref="ServiceLocator.Current"/> (a static method cannot see a per-presenter provider
    /// injected via <c>SetServiceProvider</c>). Tests that assert anchored feedback register a
    /// mock service via <c>ServiceLocator.Configure</c> inside the <c>"ServiceLocator"</c> test
    /// collection and call <c>ServiceLocator.Reset()</c> afterwards.
    /// </para>
    /// <para>
    /// The anchor is the cursor position at call time — call synchronously inside the
    /// event/action handler. After an <c>await</c> use <c>Messages.ShowToast</c> instead.
    /// </para>
    /// <para>
    /// The <paramref name="view"/> receiver is not used by the current implementation; it exists
    /// so the call site reads as view behavior and to allow a future implementation to use the
    /// view (owner window, screen context) without changing call sites.
    /// </para>
    /// </remarks>
    public static class AnchoredMessageViewExtensions
    {
        private static IAnchoredMessageService Service()
            => ServiceLocator.Current.ResolveRequired<IAnchoredMessageService>();

        /// <summary>Shows a toast anchored at the cursor (full-parameter form).</summary>
        public static void ShowToast(this IViewBase view, string text, ToastType type, ToastOptions options)
            => Service().ShowToast(text, type, options);

        /// <summary>Shows a toast anchored at the cursor with default options.</summary>
        public static void ShowToast(this IViewBase view, string text, ToastType type)
            => Service().ShowToast(text, type, null);

        /// <summary>Shows an anchored message box (full-parameter form).</summary>
        public static ConfirmResult ShowMessage(this IViewBase view, string text, string caption, MessageButtons buttons, MessageIcon icon)
            => Service().ShowMessage(text, caption, buttons, icon);

        /// <summary>Shows an anchored information message.</summary>
        public static void ShowInfo(this IViewBase view, string text, string caption = "")
            => Service().ShowInfo(text, caption);

        /// <summary>Shows an anchored warning message.</summary>
        public static void ShowWarning(this IViewBase view, string text, string caption = "")
            => Service().ShowWarning(text, caption);

        /// <summary>Shows an anchored error message.</summary>
        public static void ShowError(this IViewBase view, string text, string caption = "")
            => Service().ShowError(text, caption);

        /// <summary>Anchored Yes/No confirmation. True when the user chose Yes.</summary>
        public static bool ConfirmYesNo(this IViewBase view, string text, string caption = "")
            => Service().ConfirmYesNo(text, caption);

        /// <summary>Anchored OK/Cancel confirmation. True when the user chose OK.</summary>
        public static bool ConfirmOkCancel(this IViewBase view, string text, string caption = "")
            => Service().ConfirmOkCancel(text, caption);
    }
}
```

- [ ] **Step 4: Run tests**

Run: `dotnet test ... --filter "FullyQualifiedName~AnchoredMessageViewExtensionsTests"` → PASS (4 tests). Then the full suite → all pass.

- [ ] **Step 5: Commit**

```bash
git add src/WinformsMVP/MVP/Views/AnchoredMessageViewExtensions.cs tests/WinformsMVP.Samples.Tests/Mocks/MockAnchoredMessageService.cs tests/WinformsMVP.Samples.Tests/Views/AnchoredMessageViewExtensionsTests.cs
git commit -m "feat(anchored): View.ShowToast/ShowMessage IViewBase extensions resolving via ServiceLocator; add MockAnchoredMessageService"
```

---

### Task 4: Sample demo + presenter-level test demonstrating the pattern

**Files:**
- Create: `samples/WinformsMVP.Samples/AnchoredMessageDemo/IAnchoredMessageDemoView.cs`
- Create: `samples/WinformsMVP.Samples/AnchoredMessageDemo/AnchoredMessageDemoPresenter.cs`
- Create: `samples/WinformsMVP.Samples/AnchoredMessageDemo/AnchoredMessageDemoForm.cs`
- Modify: `samples/WinformsMVP.Samples/SampleLauncherForm.cs` (register in launcher)
- Test: `tests/WinformsMVP.Samples.Tests/Presenters/AnchoredMessageDemoPresenterTests.cs`

- [ ] **Step 1: View interface + action keys**

`samples/WinformsMVP.Samples/AnchoredMessageDemo/IAnchoredMessageDemoView.cs`:

```csharp
using WinformsMVP.MVP.Views;
using WinformsMVP.MVP.ViewActions;

namespace WinformsMVP.Samples.AnchoredMessageDemo
{
    public static class AnchoredMessageDemoActions
    {
        private static readonly ViewActionFactory Factory =
            ViewAction.Factory.WithQualifier("AnchoredMessageDemo");

        public static readonly ViewAction Save = Factory.Create("Save");
        public static readonly ViewAction Delete = Factory.Create("Delete");
        public static readonly ViewAction GridTouch = Factory.Create("GridTouch");
    }

    /// <summary>
    /// View for the anchored-message demo. Note there is no feedback method here — anchored
    /// toast/message-box come from the framework's IViewBase extensions (View.ShowToast, ...).
    /// </summary>
    public interface IAnchoredMessageDemoView : IWindowView
    {
        void ShowHint(string message);
    }
}
```

- [ ] **Step 2: Presenter** — uses `View.ShowToast` / `View.ConfirmYesNo`, no coordinates anywhere

`samples/WinformsMVP.Samples/AnchoredMessageDemo/AnchoredMessageDemoPresenter.cs`:

```csharp
using WinformsMVP.Common;
using WinformsMVP.MVP.Presenters;
using WinformsMVP.MVP.Views;

namespace WinformsMVP.Samples.AnchoredMessageDemo
{
    /// <summary>
    /// Demonstrates cursor-anchored feedback: the presenter calls View.ShowToast /
    /// View.ConfirmYesNo (IViewBase extensions) synchronously inside action handlers, so the
    /// feedback appears at the click point. No coordinates, controls, or WinForms types here.
    /// </summary>
    public class AnchoredMessageDemoPresenter : WindowPresenterBase<IAnchoredMessageDemoView>
    {
        protected override void OnViewAttached() { }

        protected override void RegisterViewActions()
        {
            Dispatcher.Register(AnchoredMessageDemoActions.Save, OnSave);
            Dispatcher.Register(AnchoredMessageDemoActions.Delete, OnDelete);
            Dispatcher.Register(AnchoredMessageDemoActions.GridTouch, OnGridTouch);
        }

        private void OnSave()
        {
            View.ShowHint("Saved — toast anchored at the click point.");
            View.ShowToast("Saved!", ToastType.Success);
        }

        private void OnDelete()
        {
            if (View.ConfirmYesNo("Delete this item?", "Confirm"))
            {
                View.ShowHint("Deleted — confirmation was anchored at the click point.");
                View.ShowToast("Deleted", ToastType.Warning);
            }
            else
            {
                View.ShowHint("Delete cancelled.");
            }
        }

        private void OnGridTouch()
        {
            View.ShowHint("Grid clicked — toast anchored where you clicked.");
            View.ShowToast("Row touched", ToastType.Info);
        }
    }
}
```

- [ ] **Step 3: Form** — follow the existing demo style (e.g. `CheckBoxDemo/SettingsDemoForm.cs`): private controls, `ViewActionBinder` in `InitializeActionBindings()`, `ActionBinder` property. Layout: a "Save" button, a "Delete..." button, a small `DataGridView` (4 rows, click bound to `GridTouch`), and a hint `Label` for `ShowHint`. Title: `"WinForms MVP - Anchored Message Demo"`. Bind:

```csharp
_viewActionBinder = new ViewActionBinder();
_viewActionBinder.Add(AnchoredMessageDemoActions.Save, _saveButton);
_viewActionBinder.Add(AnchoredMessageDemoActions.Delete, _deleteButton);
_viewActionBinder.Add(AnchoredMessageDemoActions.GridTouch, _grid);
```

Register in `SampleLauncherForm` under the "Navigation & Windows" section:

```csharp
new DemoItem("Anchored Message Demo",
    "Cursor-anchored toast & MessageBox • View.ShowToast extension • IAnchoredMessageService",
    Color.FromArgb(0, 128, 128), LaunchAnchoredMessageDemo));
```

with the standard launch method (`new AnchoredMessageDemoForm()` + `new AnchoredMessageDemoPresenter()` + `AttachView` + `Initialize` + `Show` + hide/restore launcher — mirror `LaunchToDoDemo`). Add the `using WinformsMVP.Samples.AnchoredMessageDemo;`.

- [ ] **Step 4: Presenter test demonstrating the global-locator pattern**

`tests/WinformsMVP.Samples.Tests/Presenters/AnchoredMessageDemoPresenterTests.cs`:

```csharp
using System;
using WinformsMVP.Common;
using WinformsMVP.Common.Interactions;
using WinformsMVP.MVP.ViewActions;
using WinformsMVP.Samples.AnchoredMessageDemo;
using WinformsMVP.Samples.Tests.Mocks;
using WinformsMVP.Services;
using Xunit;

namespace WinformsMVP.Samples.Tests.Presenters
{
    /// <summary>
    /// Demonstrates the testing pattern for cursor-anchored feedback: the View.ShowToast
    /// extensions resolve IAnchoredMessageService from the global ServiceLocator, so the test
    /// installs a recording mock via ServiceLocator.Configure (serialized by the
    /// "ServiceLocator" collection) and resets it on Dispose.
    /// </summary>
    [Collection("ServiceLocator")]
    public class AnchoredMessageDemoPresenterTests : IDisposable
    {
        private sealed class StubView : IAnchoredMessageDemoView
        {
            public string LastHint;
            public IViewActionBinder ActionBinder => null; // explicit: no auto-binding in tests
            public void ShowHint(string message) => LastHint = message;
        }

        private readonly MockAnchoredMessageService _anchored = new MockAnchoredMessageService();
        private readonly StubView _view = new StubView();
        private readonly AnchoredMessageDemoPresenter _presenter = new AnchoredMessageDemoPresenter();

        public AnchoredMessageDemoPresenterTests()
        {
            ServiceLocator.Configure(reg => reg.RegisterInstance<IAnchoredMessageService>(_anchored));
            _presenter.AttachView(_view);
            _presenter.Initialize();
        }

        public void Dispose()
        {
            _presenter.Dispose();
            ServiceLocator.Reset();
        }

        [Fact]
        public void Save_ShowsSuccessToast()
        {
            _presenter.Dispatcher.Dispatch(AnchoredMessageDemoActions.Save);
            Assert.Single(_anchored.Toasts);
            Assert.Equal("Saved!", _anchored.Toasts[0].Text);
            Assert.Equal(ToastType.Success, _anchored.Toasts[0].Type);
        }

        [Fact]
        public void Delete_Confirmed_ShowsWarningToast()
        {
            _anchored.NextResult = ConfirmResult.Yes;
            _presenter.Dispatcher.Dispatch(AnchoredMessageDemoActions.Delete);
            Assert.Equal(MessageButtons.YesNo, _anchored.Messages[0].Buttons);
            Assert.Single(_anchored.Toasts);
            Assert.Equal(ToastType.Warning, _anchored.Toasts[0].Type);
        }

        [Fact]
        public void Delete_Declined_ShowsNoToast()
        {
            _anchored.NextResult = ConfirmResult.No;
            _presenter.Dispatcher.Dispatch(AnchoredMessageDemoActions.Delete);
            Assert.Empty(_anchored.Toasts);
            Assert.Equal("Delete cancelled.", _view.LastHint);
        }
    }
}
```

> Adapt to the actual test conventions: check how existing presenter tests attach views (`AttachView`/`Initialize` are public on `WindowPresenterBase` — verify against e.g. `ToDoDemoPresenterTests`) and whether `IWindowView`-derived stubs need more members. `AttachView` may live on `IViewAttacher<TView>`/`IViewAttachable` — mirror whatever `ToDoDemoPresenterTests` does.

- [ ] **Step 5: Build samples + run full suite**

`dotnet build winforms-mvp.sln -c Debug` → 0 errors. `dotnet test ...` → all pass.

- [ ] **Step 6: Commit**

```bash
git add -A
git reset .claude/settings.local.json
git commit -m "feat(samples): anchored message demo (button/grid/confirm) + presenter tests via ServiceLocator mock pattern"
```

---

### Task 5: Documentation

**Files:**
- Modify: `CLAUDE.md` — add `IAnchoredMessageService` to the services list ("Services & platform access" section): one line, e.g. `View.ShowToast(...)` / `View.ConfirmYesNo(...)` — cursor-anchored feedback via IViewBase extensions (`IAnchoredMessageService`), synchronous-call contract.
- Modify: `src/WinformsMVP/Services/IMessageService.cs` — update the positioned-dialogs remark: View code still uses `AnchoredMessageBox`/`AnchoredToast` directly; Presenters now have `View.ShowToast(...)` (the `IViewBase` extensions backed by `IAnchoredMessageService`) for cursor-anchored feedback.
- Modify: `wiki/Reference-Platform-Services.md` — add an `IAnchoredMessageService` section: the 2-method interface, convenience extensions, the View extensions, the synchronous-call/cursor contract, the test pattern (global locator + collection), and the multi-monitor behavior.
- Modify: the spec `docs/superpowers/specs/2026-06-11-service-locator-foundation-design.md` — update the "Follow-on" section to match what was built (name `IAnchoredMessageService`, IViewBase-extension entry point instead of a presenter accessor).

- [ ] **Step 1: Apply the doc updates** (describe only what exists). 
- [ ] **Step 2: Commit**

```bash
git add CLAUDE.md wiki/ src/WinformsMVP/Services/IMessageService.cs docs/superpowers/specs/2026-06-11-service-locator-foundation-design.md
git commit -m "docs(anchored): document IAnchoredMessageService, View.ShowToast extensions, and test pattern"
```

---

### Task 6: Full gate

- [ ] `dotnet build winforms-mvp.sln -c Debug` → 0 errors (only pre-existing xUnit1031 warnings).
- [ ] `dotnet test tests/WinformsMVP.Samples.Tests/WinformsMVP.Samples.Tests.csproj` → all pass.
- [ ] Manual smoke (controller will run): launch the sample app, open "Anchored Message Demo", click the button/grid/menu — feedback appears at the click point; verify multi-monitor behavior on screen B.

---

## Self-Review

- **Spec coverage:** minimal 2-method interface + extension conveniences (user decision) — Task 1; cursor-anchored real impl + built-in registration both locator and M.E.DI — Task 2; multi-monitor fix port — Task 2; IViewBase extension entry point + global-locator testability + mock — Task 3; sample + presenter-test pattern — Task 4; docs incl. spec follow-on correction — Task 5.
- **Placeholder scan:** Task 4's Form body is described by reference to an existing demo's concrete style with the exact bindings given (UI layout boilerplate intentionally delegated); all other code is complete.
- **Type consistency:** `IAnchoredMessageService.ShowToast(text,type,options)` / `ShowMessage(text,caption,MessageButtons,MessageIcon):ConfirmResult`; extensions `ShowInfo/ShowWarning/ShowError/ConfirmYesNo/ConfirmOkCancel/ConfirmYesNoCancel`; view extensions mirror them; `MockAnchoredMessageService.Toasts/Messages/NextResult` — used identically across tasks.
- **net40 safety:** enums, switch, extension methods, `Cursor.Position` — all net40-safe.
