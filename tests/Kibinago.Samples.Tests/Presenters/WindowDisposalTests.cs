using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows.Forms;
using Kibinago.Common;
using Kibinago.Presenters;
using Kibinago.Services;
using Kibinago.Testing;
using Kibinago.ViewActions;
using Kibinago.Views;
using Xunit;
using CloseReason = Kibinago.Common.CloseReason;

namespace Kibinago.Samples.Tests.Presenters
{
    /// <summary>
    /// A window whose Form is disposed without closing — the caller drops a Form it never showed, or a
    /// container disposes the panes it holds (a docking host, an MDI parent) — still ends like a close:
    /// onClosed once, the presenter disposed, a navigator key released.
    /// </summary>
    public class WindowDisposalTests
    {
        public interface IPaneView : IWindowView { }

        private sealed class PaneForm : Form, IPaneView
        {
            public IViewActionBinder ActionBinder { get; } = new CapturingActionBinder();
        }

        private sealed class PanePresenter : WindowPresenterBase<IPaneView>
        {
            public int CleanupCount;
            public TaskCompletionSource<bool> Gate;   // set: CanCloseAsync waits for it
            public bool CloseInInit;

            protected override void OnInitialize()
            {
                if (CloseInInit) RequestClose(7, InteractionStatus.Ok);
            }

            protected override Task<bool> CanCloseAsync(CloseReason reason)
                => Gate != null ? Gate.Task : base.CanCloseAsync(reason);

            protected override void Cleanup() => CleanupCount++;
        }

        private static (WindowNavigator navigator, List<PaneForm> forms) NewNavigator()
        {
            var forms = new List<PaneForm>();
            var registry = new ViewMappingRegister();
            registry.Register<IPaneView>(() => { var f = new PaneForm(); forms.Add(f); return f; });
            return (new WindowNavigator(registry), forms);
        }

        [Fact]
        public void ShownWindow_DisposedWithoutClosing_EndsOnce_AndReleasesItsKey()
        {
            var (navigator, forms) = NewNavigator();
            var presenter = new PanePresenter();
            var closed = new List<InteractionResult>();

            navigator.ShowWindow<PanePresenter, object>(presenter, key: 1, onClosed: r => closed.Add(r));
            forms[0].Dispose();

            Assert.Single(closed);
            Assert.True(closed[0].IsCancelled);
            Assert.Equal(1, presenter.CleanupCount);

            var again = new PanePresenter();
            navigator.ShowWindow(again, key: 1);   // the key was released: a new window
            try { Assert.Equal(2, forms.Count); }
            finally { forms[1].Dispose(); }
        }

        [Fact]
        public void ClosedWindow_DisposedAfterwards_DoesNotEndTwice()
        {
            var (navigator, forms) = NewNavigator();
            var presenter = new PanePresenter();
            int closedCount = 0;

            navigator.ShowWindow<PanePresenter, object>(presenter, onClosed: _ => closedCount++);
            forms[0].Close();
            forms[0].Dispose();

            Assert.Equal(1, closedCount);
            Assert.Equal(1, presenter.CleanupCount);
        }

        [Fact]
        public void ConnectedForm_NeverShown_DisposedByTheCaller_Ends()
        {
            var presenter = new PanePresenter();
            var closed = new List<InteractionResult>();
            var form = new PaneForm();

            presenter.Connect(form, onClosed: r => closed.Add(r));
            form.Dispose();

            Assert.Single(closed);
            Assert.True(closed[0].IsCancelled);
            Assert.Equal(1, presenter.CleanupCount);
        }

        [Fact]
        public void CancelledOpening_ThenDisposedBeforeShown_DeliversTheRequestedResult()
        {
            var presenter = new PanePresenter { CloseInInit = true };
            var closed = new List<InteractionResult<int>>();
            var form = new PaneForm();

            presenter.Connect<IPaneView, int>(form, r => closed.Add(r));   // the close waits for Load
            form.Dispose();

            var result = Assert.Single(closed);
            Assert.True(result.IsOk);
            Assert.Equal(7, result.Value);
            Assert.Equal(1, presenter.CleanupCount);
        }

        [Fact]
        public void DisposedWhileAnAsyncCloseDecisionIsPending_EndsOnce_AndTheLateAnswerIsIgnored()
        {
            var (navigator, forms) = NewNavigator();
            var presenter = new PanePresenter { Gate = new TaskCompletionSource<bool>() };
            int closedCount = 0;

            navigator.ShowWindow<PanePresenter, object>(presenter, onClosed: _ => closedCount++);
            forms[0].Close();   // the gate is pending: the window stays
            Assert.False(forms[0].IsDisposed);

            forms[0].Dispose();
            presenter.Gate.SetResult(true);   // too late: nothing left to close

            Assert.Equal(1, closedCount);
            Assert.Equal(1, presenter.CleanupCount);
        }

        [Fact]
        public void PaneHostedInAContainer_DisposedWithTheContainer_Ends()
        {
            // A docking host keeps its panes as non-top-level Forms inside itself and disposes them with it.
            var presenter = new PanePresenter();
            int closedCount = 0;
            using (var container = new Form())
            {
                var pane = new PaneForm { TopLevel = false };
                presenter.Connect(pane, onClosed: _ => closedCount++);
                container.Controls.Add(pane);
                pane.Show();
            }

            Assert.Equal(1, closedCount);
            Assert.Equal(1, presenter.CleanupCount);
        }
    }
}
