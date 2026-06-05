using System;
using System.Collections.Generic;
using WinformsMVP.Common;
using WinformsMVP.Common.Events;
using WinformsMVP.MVP.Views;
using WinformsMVP.MVP.Presenters;
using WinformsMVP.MVP.ViewActions;
using WinformsMVP.Samples.Tests.Mocks;
using Xunit;

namespace WinformsMVP.Samples.Tests.Presenters
{
    /// <summary>
    /// Tests for the framework's two-direction window-closing infrastructure:
    /// <list type="bullet">
    ///   <item><description><b>Push</b>: a Presenter raising <see cref="IRequestClose{TResult}.CloseRequested"/>
    ///     to close the window and return a typed business result.</description></item>
    ///   <item><description><b>Pull</b>: subscribing to <see cref="IWindowView.Closing"/> to allow or block
    ///     external close requests, honouring <see cref="CloseReason"/>.</description></item>
    /// </list>
    /// </summary>
    public class WindowClosingTests
    {
        #region Test doubles

        public interface IFakeView : IWindowView
        {
            string Title { get; set; }
            bool HasUnsavedChanges { get; set; }
        }

        public class FakeView : IFakeView
        {
            public string Title { get; set; }
            public bool HasUnsavedChanges { get; set; }
            public bool IsDisposed { get; private set; }
            public IntPtr Handle => IntPtr.Zero;
            public IViewActionBinder ActionBinder { get; } = NullViewActionBinder.Instance;
            public void Activate() { }

            public event EventHandler<WindowClosingEventArgs> Closing;

            public void OnClosing(WindowClosingEventArgs args)
                => Closing?.Invoke(this, args);

            // Test helper: simulate the framework triggering a close.
            public WindowClosingEventArgs RaiseClosing(CloseReason reason = CloseReason.Normal)
            {
                var args = new WindowClosingEventArgs(reason);
                OnClosing(args);
                return args;
            }
        }

        /// <summary>
        /// Presenter that subscribes to View.Closing and blocks the close when there are
        /// unsaved changes (Pull direction). For Push, it implements
        /// <see cref="IRequestClose{TResult}"/> the same way real presenters do —
        /// a <c>CloseRequested</c> event plus a small private helper that raises it.
        /// </summary>
        public class FakePresenter : WindowPresenterBase<IFakeView>, IRequestClose<string>
        {
            public event EventHandler<CloseRequestedEventArgs<string>> CloseRequested;

            public List<CloseReason> ObservedClosingReasons { get; } = new List<CloseReason>();

            protected override void OnViewAttached()
            {
                View.Closing += OnViewClosing;
            }

            private void OnViewClosing(object sender, WindowClosingEventArgs args)
            {
                ObservedClosingReasons.Add(args.Reason);

                // Skip dirty-check on system shutdown / task manager — let the process exit.
                if (args.Reason != CloseReason.Normal) return;

                if (View.HasUnsavedChanges)
                    args.Cancel = true;
            }

            // Expose RaiseClose / RaiseCancel to tests as public passthroughs.
            public void TestRequestClose(string result)
                => CloseRequested?.Invoke(this, new CloseRequestedEventArgs<string>(result, InteractionStatus.Ok));
            public void TestRequestCancel()
                => CloseRequested?.Invoke(this, new CloseRequestedEventArgs<string>(null, InteractionStatus.Cancel));
        }

        #endregion

        #region CloseReason / WindowClosingEventArgs

        [Fact]
        public void WindowClosingEventArgs_DefaultsToCancelFalse()
        {
            var args = new WindowClosingEventArgs(CloseReason.Normal);

            Assert.False(args.Cancel);
            Assert.Equal(CloseReason.Normal, args.Reason);
        }

        [Fact]
        public void WindowClosingEventArgs_CancelIsMutable()
        {
            var args = new WindowClosingEventArgs(CloseReason.Normal);

            args.Cancel = true;

            Assert.True(args.Cancel);
        }

        [Fact]
        public void WindowClosingEventArgs_Cancel_OnceSetTrue_CannotBeReset()
        {
            var args = new WindowClosingEventArgs(CloseReason.Normal);

            args.Cancel = true;
            args.Cancel = false;   // write-once veto: ignored

            Assert.True(args.Cancel);
        }

        [Theory]
        [InlineData(CloseReason.Normal)]
        [InlineData(CloseReason.SystemShutdown)]
        [InlineData(CloseReason.TaskManager)]
        [InlineData(CloseReason.ParentClosing)]
        [InlineData(CloseReason.Unknown)]
        public void WindowClosingEventArgs_PreservesReason(CloseReason reason)
        {
            var args = new WindowClosingEventArgs(reason);

            Assert.Equal(reason, args.Reason);
        }

        #endregion

        #region Pull direction (View.Closing)

        [Fact]
        public void Closing_WithoutDirtyState_DoesNotCancel()
        {
            var view = new FakeView { HasUnsavedChanges = false };
            var presenter = new FakePresenter();
            presenter.AttachView(view);
            presenter.Initialize();

            var args = view.RaiseClosing();

            Assert.False(args.Cancel);
        }

        [Fact]
        public void Closing_WithDirtyState_CancelsClose()
        {
            var view = new FakeView { HasUnsavedChanges = true };
            var presenter = new FakePresenter();
            presenter.AttachView(view);
            presenter.Initialize();

            var args = view.RaiseClosing();

            Assert.True(args.Cancel);
        }

        [Fact]
        public void Closing_OnSystemShutdown_BypassesDirtyCheck()
        {
            var view = new FakeView { HasUnsavedChanges = true };
            var presenter = new FakePresenter();
            presenter.AttachView(view);
            presenter.Initialize();

            var args = view.RaiseClosing(CloseReason.SystemShutdown);

            Assert.False(args.Cancel);
        }

        [Fact]
        public void Closing_OnTaskManagerClose_BypassesDirtyCheck()
        {
            var view = new FakeView { HasUnsavedChanges = true };
            var presenter = new FakePresenter();
            presenter.AttachView(view);
            presenter.Initialize();

            var args = view.RaiseClosing(CloseReason.TaskManager);

            Assert.False(args.Cancel);
        }

        [Fact]
        public void Closing_PassesReasonToHandler()
        {
            var view = new FakeView();
            var presenter = new FakePresenter();
            presenter.AttachView(view);
            presenter.Initialize();

            view.RaiseClosing(CloseReason.ParentClosing);

            Assert.Single(presenter.ObservedClosingReasons);
            Assert.Equal(CloseReason.ParentClosing, presenter.ObservedClosingReasons[0]);
        }

        [Fact]
        public void Closing_MultipleSubscribers_AllReceiveEvent()
        {
            var view = new FakeView();
            int subscriber1Count = 0;
            int subscriber2Count = 0;
            view.Closing += (s, e) => subscriber1Count++;
            view.Closing += (s, e) => subscriber2Count++;

            view.RaiseClosing();

            Assert.Equal(1, subscriber1Count);
            Assert.Equal(1, subscriber2Count);
        }

        [Fact]
        public void Closing_AnySubscriberSettingCancel_BlocksClose()
        {
            var view = new FakeView();
            view.Closing += (s, e) => { /* subscriber 1: allows */ };
            view.Closing += (s, e) => { e.Cancel = true; /* subscriber 2: blocks */ };

            var args = view.RaiseClosing();

            Assert.True(args.Cancel);
        }

        [Fact]
        public void Closing_LaterSubscriberCannotUndoEarlierVeto()
        {
            var view = new FakeView();
            view.Closing += (s, e) => { e.Cancel = true; /* subscriber 1: blocks */ };
            view.Closing += (s, e) => { e.Cancel = false; /* subscriber 2: tries to allow — must be ignored */ };

            var args = view.RaiseClosing();

            Assert.True(args.Cancel);
        }

        #endregion

        #region Push direction (CloseRequested / RequestClose)

        [Fact]
        public void RequestClose_RaisesCloseRequested_WithResultAndOkStatus()
        {
            var view = new FakeView();
            var presenter = new FakePresenter();
            presenter.AttachView(view);
            presenter.Initialize();

            CloseRequestedEventArgs<string> captured = null;
            presenter.CloseRequested += (s, e) => captured = e;

            presenter.TestRequestClose("hello-world");

            Assert.NotNull(captured);
            Assert.Equal("hello-world", captured.Result);
            Assert.Equal(InteractionStatus.Ok, captured.Status);
        }

        [Fact]
        public void RequestCancel_RaisesCloseRequested_WithDefaultResultAndCancelStatus()
        {
            var view = new FakeView();
            var presenter = new FakePresenter();
            presenter.AttachView(view);
            presenter.Initialize();

            CloseRequestedEventArgs<string> captured = null;
            presenter.CloseRequested += (s, e) => captured = e;

            presenter.TestRequestCancel();

            Assert.NotNull(captured);
            Assert.Null(captured.Result);
            Assert.Equal(InteractionStatus.Cancel, captured.Status);
        }

        [Fact]
        public void RequestClose_WithoutSubscribers_DoesNotThrow()
        {
            var view = new FakeView();
            var presenter = new FakePresenter();
            presenter.AttachView(view);
            presenter.Initialize();

            // No exception expected.
            presenter.TestRequestClose("anything");
        }

        [Fact]
        public void Presenter_ImplementsIRequestClose()
        {
            var presenter = new FakePresenter();

            // IRequestClose<TResult> is what WindowNavigator detects to wire up the close path.
            Assert.IsAssignableFrom<IRequestClose<string>>(presenter);
        }

        #endregion

        #region Push then Pull (the canonical "Presenter requests close" flow)

        /// <summary>
        /// When the Presenter requests close, the framework calls <c>Form.Close()</c> which
        /// triggers <c>FormClosing</c> → <c>IWindowView.OnClosing</c> → the Presenter's
        /// own Closing handler. The handler should observe a fully-finalized clean state
        /// (in this fake, <c>HasUnsavedChanges == false</c>) and not cancel.
        /// </summary>
        [Fact]
        public void RequestClose_FollowedByClosing_AllowsClose()
        {
            var view = new FakeView { HasUnsavedChanges = false };
            var presenter = new FakePresenter();
            presenter.AttachView(view);
            presenter.Initialize();

            bool closeRequested = false;
            presenter.CloseRequested += (s, e) => closeRequested = true;

            // Push direction.
            presenter.TestRequestClose("done");
            // Then simulate framework's follow-up FormClosing → IWindowView.Closing.
            var args = view.RaiseClosing();

            Assert.True(closeRequested);
            Assert.False(args.Cancel);
        }

        #endregion

    }
}
