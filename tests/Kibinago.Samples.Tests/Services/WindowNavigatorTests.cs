using System;
using System.Collections.Generic;
using System.Windows.Forms;
using Kibinago.Presenters;
using Kibinago.ViewActions;
using Kibinago.Views;
using Kibinago.Samples.Tests.Mocks;
using Kibinago.Services;
using Xunit;
using Kibinago.Testing;

namespace Kibinago.Samples.Tests.Services
{
    /// <summary>
    /// Tests for <see cref="WindowNavigator"/>'s error contract: view-mapping and
    /// configuration failures are reported by throwing (not by returning
    /// <c>InteractionResult.Error</c>), and the presenter is disposed before the throw so a
    /// misconfiguration never leaks a presenter that can never be shown.
    /// </summary>
    /// <remarks>
    /// Only the failure paths are exercised here because they throw before
    /// <c>Form.ShowDialog()/Show()</c> — so no real window is created and the tests stay headless.
    /// </remarks>
    public class WindowNavigatorTests
    {
        #region Test doubles

        public interface ITestNavView : IWindowView { }

        /// <summary>Parameterless presenter that records whether it was disposed / had a view attached.</summary>
        private sealed class TestNavPresenter : WindowPresenterBase<ITestNavView>
        {
            public bool Disposed { get; private set; }
            public bool ViewWasAttached { get; private set; }
            protected override void OnViewAttached() => ViewWasAttached = true;
            protected override void Cleanup() => Disposed = true;
        }

        /// <summary>An <see cref="IView"/> that is NOT an <see cref="ITestNavView"/>.</summary>
        private sealed class WrongView : IView { }

        /// <summary>Parameterized presenter (exercises the <c>TParam</c> overloads).</summary>
        private sealed class TestNavPresenterWithParam : WindowPresenterBase<ITestNavView, int>
        {
            public bool Disposed { get; private set; }
            protected override void OnInitialize(int parameters) { }
            protected override void Cleanup() => Disposed = true;
        }

        /// <summary>
        /// A view implementation that satisfies <see cref="ITestNavView"/> but is NOT a
        /// <see cref="System.Windows.Forms.Form"/>, used to trigger the "must inherit from Form" guard.
        /// </summary>
        private sealed class NotAFormView : ITestNavView
        {
            public IViewActionBinder ActionBinder { get; } = new CapturingActionBinder();
        }

        private static WindowNavigator NewNavigatorWithEmptyRegistry()
            => new WindowNavigator(new ViewMappingRegister());

        /// <summary>A real <see cref="Form"/> implementing <see cref="ITestNavView"/>, so the
        /// navigator gets past form creation into Initialize. Never actually shown by these tests.</summary>
        private sealed class TestNavForm : Form, ITestNavView
        {
            public IViewActionBinder ActionBinder { get; } = new CapturingActionBinder();
        }

        /// <summary>Presenter whose <c>OnInitialize</c> throws when asked to, exercising the init-failure path.</summary>
        private sealed class ThrowingInitPresenter : WindowPresenterBase<ITestNavView>
        {
            public bool Throw = true;
            public bool Disposed { get; private set; }
            protected override void OnInitialize()
            {
                if (Throw) throw new InvalidOperationException("boom in init");
            }
            protected override void Cleanup() => Disposed = true;
        }

        #endregion

        [Fact]
        public void ShowWindowAsModal_NoMappingRegistered_ThrowsAndDisposesPresenter()
        {
            var navigator = NewNavigatorWithEmptyRegistry();
            var presenter = new TestNavPresenter();

            Assert.Throws<KeyNotFoundException>(
                () => navigator.ShowWindowAsModal<TestNavPresenter, object>(presenter));

            Assert.True(presenter.Disposed, "Presenter should be disposed when its window cannot be created.");
        }

        [Fact]
        public void ShowWindowAsModalWithParam_NoMappingRegistered_ThrowsAndDisposesPresenter()
        {
            var navigator = NewNavigatorWithEmptyRegistry();
            var presenter = new TestNavPresenterWithParam();

            Assert.Throws<KeyNotFoundException>(
                () => navigator.ShowWindowAsModal<TestNavPresenterWithParam, int, object>(presenter, 42));

            Assert.True(presenter.Disposed, "Presenter should be disposed when its window cannot be created.");
        }

        [Fact]
        public void ShowWindow_NonModal_NoMappingRegistered_ThrowsAndDisposesPresenter()
        {
            var navigator = NewNavigatorWithEmptyRegistry();
            var presenter = new TestNavPresenter();

            Assert.Throws<KeyNotFoundException>(
                () => navigator.ShowWindow<TestNavPresenter>(presenter));

            Assert.True(presenter.Disposed, "Presenter should be disposed when its window cannot be created.");
        }

        [Fact]
        public void ShowWindowAsModal_ViewIsNotAForm_ThrowsInvalidOperationAndDisposesPresenter()
        {
            var registry = new ViewMappingRegister();
            // Factory yields a non-Form implementation of the view interface.
            registry.Register<ITestNavView>(() => new NotAFormView());
            var navigator = new WindowNavigator(registry);
            var presenter = new TestNavPresenter();

            var ex = Assert.Throws<InvalidOperationException>(
                () => navigator.ShowWindowAsModal<TestNavPresenter, object>(presenter));

            Assert.Contains("Form", ex.Message);
            Assert.True(presenter.Disposed, "Presenter should be disposed when the view is not a Form.");
        }

        [Fact]
        public void IViewAttachable_AttachView_CastsToTViewAndAttaches()
        {
            // The non-generic internal contract WindowNavigator uses instead of reflection.
            var presenter = new TestNavPresenter();

            ((IViewAttachable)presenter).AttachView(new NotAFormView());

            Assert.True(presenter.ViewWasAttached,
                "AttachView(IView) should cast to TView and route through SetView/OnViewAttached.");
        }

        [Fact]
        public void IViewAttachable_AttachView_WithWrongViewType_ThrowsInvalidCast()
        {
            var presenter = new TestNavPresenter();

            Assert.Throws<InvalidCastException>(
                () => ((IViewAttachable)presenter).AttachView(new WrongView()));
        }

        [Fact]
        public void ShowWindow_NonModal_InitThrows_DoesNotLeaveStaleEntryInOpenForms()
        {
            // Regression: a non-modal show whose Initialize throws must NOT leave its keyed entry
            // in _openForms pointing at a disposed form. A subsequent show with the same key must
            // create a fresh window instead of "activating" the dead one.
            var registry = new ViewMappingRegister();
            registry.Register<ITestNavView, TestNavForm>();
            var navigator = new WindowNavigator(registry);

            object key = "doc-1";

            var failing = new ThrowingInitPresenter();
            Assert.Throws<InvalidOperationException>(
                () => navigator.ShowWindow<ThrowingInitPresenter>(failing, key: key));

            // The stale (disposed) form must have been pruned, so the next show (same presenter type,
            // same key: the same window scope) creates a new one.
            var second = new ThrowingInitPresenter { Throw = false };
            IWindowView view = null;
            try
            {
                view = navigator.ShowWindow<ThrowingInitPresenter>(second, key: key);

                Assert.NotNull(view);
                Assert.False(second.Disposed,
                    "A fresh window must be created (the second presenter must not be disposed as a duplicate).");
                Assert.False(((Form)view).IsDisposed,
                    "The newly created form must be live, not the stale disposed entry.");
            }
            finally
            {
                // Close the live non-modal window the navigator opened (the controller disposes it).
                if (view is Form openedForm && !openedForm.IsDisposed)
                    openedForm.Close();
            }
        }

        #region Cancelling the opening (RequestClose during initialization)

        private sealed class RefusingPresenter : WindowPresenterBase<ITestNavView>
        {
            public bool Disposed { get; private set; }
            public int GateCalls;
            protected override void OnInitialize() => RequestClose("not found", Kibinago.Common.InteractionStatus.Error);
            protected override bool CanClose(Kibinago.Common.CloseReason reason) { GateCalls++; return false; }
            protected override void Cleanup() => Disposed = true;
        }

        private static (WindowNavigator navigator, List<TestNavForm> forms) NavigatorWithTrackedForms()
        {
            var forms = new List<TestNavForm>();
            var register = new ViewMappingRegister();
            register.Register<ITestNavView>(() => { var f = new TestNavForm(); forms.Add(f); return f; });
            return (new WindowNavigator(register), forms);
        }

        [Fact]
        public void Modal_CancelledDuringInitialization_IsNeverShown_NoGate()
        {
            var (navigator, forms) = NavigatorWithTrackedForms();
            var presenter = new RefusingPresenter();

            var result = navigator.ShowWindowAsModal<RefusingPresenter, string>(presenter);

            Assert.True(result.IsError);
            Assert.Equal("not found", result.ErrorMessage);
            Assert.Equal(0, presenter.GateCalls);
            Assert.False(forms[0].IsHandleCreated);   // never shown
            Assert.True(forms[0].IsDisposed);
            Assert.True(presenter.Disposed);
        }

        [Fact]
        public void Modeless_CancelledDuringInitialization_ReturnsNull_CallsOnClosed_ReleasesTheKey()
        {
            var (navigator, forms) = NavigatorWithTrackedForms();
            Kibinago.Common.InteractionResult<string> closed = null;

            var view = navigator.ShowWindow<RefusingPresenter, string>(new RefusingPresenter(),
                key: 1, onClosed: r => closed = r);

            Assert.Null(view);
            Assert.True(closed.IsError);
            Assert.False(forms[0].IsHandleCreated);
            Assert.True(forms[0].IsDisposed);

            // The key is free again: a second request is not "activate the open window".
            var again = navigator.ShowWindow<RefusingPresenter, string>(new RefusingPresenter(), key: 1);
            Assert.Null(again);
            Assert.Equal(2, forms.Count);
        }

        private sealed class HandleTouchingRefusingPresenter : WindowPresenterBase<ITestNavView>
        {
            private readonly Action _viewOperation;
            public int GateCalls;
            public HandleTouchingRefusingPresenter(Action viewOperation) => _viewOperation = viewOperation;
            protected override void OnInitialize()
            {
                RequestClose(Kibinago.Common.InteractionStatus.Cancel);
                _viewOperation();   // a view operation that creates the Form's handle
            }
            protected override bool CanClose(Kibinago.Common.CloseReason reason) { GateCalls++; return true; }
        }

        [Fact]
        public void Modal_CancelledDuringInitialization_HandleCreatedMeanwhile_StillNotShown_NoGate()
        {
            var (navigator, forms) = NavigatorWithTrackedForms();
            var presenter = new HandleTouchingRefusingPresenter(() => { var _ = forms[0].Handle; });

            var result = navigator.ShowWindowAsModal<HandleTouchingRefusingPresenter, string>(presenter);

            Assert.True(result.IsCancelled);
            Assert.Equal(0, presenter.GateCalls);
            Assert.True(forms[0].IsDisposed);
        }

        #endregion
    }
}
