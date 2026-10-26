using System;
using Kibinago.Common;
using Kibinago.Views;
using Kibinago.Services;
using Kibinago.Testing;
using Xunit;

namespace Kibinago.Samples.Tests.Testing
{
    public class FakeWindowNavigatorTests
    {
        [Fact]
        public void Modal_Unscripted_Throws_AndIsRecorded()
        {
            var navigator = new FakeWindowNavigator();
            var presenter = new FixturePresenter();

            var ex = Assert.Throws<UnscriptedInteractionException>(
                () => navigator.ShowWindowAsModal<FixturePresenter, int>(presenter));

            Assert.Contains(nameof(FixturePresenter), ex.Message);
            Assert.Single(navigator.Calls);
            Assert.True(presenter.Disposed);
        }

        [Fact]
        public void Modal_Typed_ReturnsTheValue_ThroughTheFluentApi()
        {
            var navigator = new FakeWindowNavigator();
            navigator.RespondTo<ParamFixturePresenter, int>(r => InteractionResult<int>.Ok((int)r.Parameters * 2));
            IWindowNavigator nav = navigator;

            var result = nav.For(new ParamFixturePresenter()).WithParam(21).ShowAsModal<int>();

            Assert.True(result.IsOk);
            Assert.Equal(42, result.Value);
            Assert.Equal(21, navigator.LastModalParameters<int>());
        }

        [Fact]
        public void Modal_TypedParameters_ReachTheResponder()
        {
            var navigator = new FakeWindowNavigator();
            navigator.RespondTo<ParamFixturePresenter, int, int>(n => InteractionResult<int>.Ok(n * 2));   // n is the int passed
            IWindowNavigator nav = navigator;

            Assert.Equal(42, nav.For(new ParamFixturePresenter()).WithParam(21).ShowAsModal<int>().Value);
        }

        [Fact]
        public void Modal_RequestCarriesPresenterAndOwner()
        {
            var navigator = new FakeWindowNavigator();
            var owner = new FixtureView();
            ModalRequest<FixturePresenter> seen = null;
            navigator.RespondTo<FixturePresenter>(r => { seen = r; return InteractionResult.Ok(); });
            var presenter = new FixturePresenter();

            Assert.True(navigator.ShowWindowAsModal(presenter, owner).IsOk);

            Assert.Same(presenter, seen.Presenter);
            Assert.Same(owner, seen.Owner);
            Assert.False(seen.HasParameters);
        }

        [Fact]
        public void Modal_WrongResultType_IsError_NotDefault()
        {
            var navigator = new FakeWindowNavigator();
            navigator.RespondTo<FixturePresenter, string>(_ => InteractionResult<string>.Ok("x"));

            var result = navigator.ShowWindowAsModal<FixturePresenter, int>(new FixturePresenter());

            Assert.True(result.IsError);
        }

        [Fact]
        public void Modal_ErrorResponse_KeepsMessageAndException()
        {
            var navigator = new FakeWindowNavigator();
            var failure = new InvalidOperationException("db down");
            navigator.RespondTo<FixturePresenter, string>(_ => InteractionResult<string>.Error("failed", failure));

            var result = navigator.ShowWindowAsModal<FixturePresenter, int>(new FixturePresenter());

            Assert.Equal("failed", result.ErrorMessage);
            Assert.Same(failure, result.Exception);
        }

        [Fact]
        public void Modal_DisposesThePresenter_LastResponderWins()
        {
            var navigator = new FakeWindowNavigator();
            navigator.RespondTo<FixturePresenter, int>(_ => InteractionResult<int>.Ok(1));
            navigator.RespondTo<FixturePresenter, int>(_ => InteractionResult<int>.Ok(2));
            var presenter = new FixturePresenter();

            Assert.Equal(2, navigator.ShowWindowAsModal<FixturePresenter, int>(presenter).Value);
            Assert.True(presenter.Disposed);
        }

        [Fact]
        public void CancelUnscripted_CancelsModalsOfAnyType()
        {
            var navigator = new FakeWindowNavigator();
            navigator.CancelUnscripted();
            Assert.True(navigator.ShowWindowAsModal<FixturePresenter, int>(new FixturePresenter()).IsCancelled);
        }

        [Fact]
        public void Modeless_SameKey_ActivatesTheOpenWindow_AndDisposesTheNewPresenter()
        {
            var navigator = new FakeWindowNavigator();
            var first = new FixturePresenter();
            var second = new FixturePresenter();

            var view1 = navigator.ShowWindow(first, key: 7);
            var view2 = navigator.ShowWindow(second, key: 7);

            Assert.Same(view1, view2);
            var window = Assert.Single(navigator.Windows);
            Assert.Same(first, window.Presenter);
            Assert.Equal(1, window.Activations);
            Assert.True(second.Disposed);
            Assert.False(first.Disposed);
            Assert.True(navigator.Calls[1].Activated);
        }

        [Fact]
        public void Modeless_KeysAreScopedByPresenterType()
        {
            var navigator = new FakeWindowNavigator();
            navigator.ShowWindow(new FixturePresenter(), key: 7);
            navigator.ShowWindow(new OtherFixturePresenter(), key: 7);
            Assert.Equal(2, navigator.Windows.Count);
        }

        [Fact]
        public void Modeless_NullKeyOrNoSelector_NoDeduplication()
        {
            var navigator = new FakeWindowNavigator();
            navigator.ShowWindow(new FixturePresenter(), key: null);
            navigator.ShowWindow(new FixturePresenter(), key: null);
            navigator.ShowWindow(new FixturePresenter());
            Assert.Equal(3, navigator.Windows.Count);
        }

        [Fact]
        public void Close_WithResult_DeliversTheTypedResult_ThenDisposes()
        {
            var navigator = new FakeWindowNavigator();
            var presenter = new ParamFixturePresenter();
            InteractionResult<string> received = null;
            bool disposedAtCallback = true;
            navigator.ShowWindow<ParamFixturePresenter, int, string>(presenter, 5,
                onClosed: r => { received = r; disposedAtCallback = presenter.Disposed; });
            var window = Assert.Single(navigator.Windows);
            Assert.Equal(5, window.Parameters);
            Assert.True(window.HasParameters);

            window.CloseWithResult("done");

            Assert.True(received.IsOk);
            Assert.Equal("done", received.Value);
            Assert.False(disposedAtCallback);
            Assert.True(presenter.Disposed);
            Assert.False(window.IsOpen);
        }

        [Fact]
        public void Close_WrongType_IsError_UserClose_IsCancel()
        {
            var navigator = new FakeWindowNavigator();
            InteractionResult<int> a = null, b = null;
            navigator.ShowWindow<FixturePresenter, int>(new FixturePresenter(), onClosed: r => a = r);
            navigator.ShowWindow<FixturePresenter, int>(new FixturePresenter(), onClosed: r => b = r);

            navigator.Windows[0].CloseWithResult("text");
            navigator.Windows[1].Close();

            Assert.True(a.IsError);
            Assert.True(b.IsCancelled);
        }

        [Fact]
        public void Close_FromOnClosed_ReopeningTheSameKey_OpensANewWindow()
        {
            var navigator = new FakeWindowNavigator();
            IWindowView reopened = null;
            var view = navigator.ShowWindow<FixturePresenter, bool>(new FixturePresenter(), key: "k",
                onClosed: _ => reopened = navigator.ShowWindow(new FixturePresenter(), key: "k"));

            navigator.Windows[0].Close();

            Assert.NotNull(reopened);
            Assert.NotSame(view, reopened);
            Assert.Equal(2, navigator.Windows.Count);
            Assert.True(navigator.Windows[1].IsOpen);
        }

        [Fact]
        public void Close_Twice_DoesNothing()
        {
            var navigator = new FakeWindowNavigator();
            int calls = 0;
            navigator.ShowWindow<FixturePresenter, int>(new FixturePresenter(), onClosed: _ => calls++);

            navigator.Windows[0].Close();
            navigator.Windows[0].CloseWithResult(1);

            Assert.Equal(1, calls);
        }

        [Fact]
        public void CloseWithError_CarriesMessageAndException()
        {
            var navigator = new FakeWindowNavigator();
            var failure = new InvalidOperationException("boom");
            InteractionResult<int> received = null;
            navigator.ShowWindow<FixturePresenter, int>(new FixturePresenter(), onClosed: r => received = r);

            navigator.Windows[0].CloseWithError("Save failed", failure);

            Assert.True(received.IsError);
            Assert.Equal("Save failed", received.ErrorMessage);
            Assert.Same(failure, received.Exception);
        }

        [Fact]
        public void Calls_RecordEveryRequest()
        {
            var navigator = new FakeWindowNavigator();
            var owner = new FixtureView();
            navigator.CancelUnscripted();

            navigator.ShowWindow(new ParamFixturePresenter(), 3, owner, 1);
            navigator.ShowWindowAsModal<FixturePresenter, string>(new FixturePresenter());

            var modeless = navigator.Calls[0];
            Assert.False(modeless.Modal);
            Assert.Equal(typeof(ParamFixturePresenter), modeless.PresenterType);
            Assert.Equal(3, modeless.Parameters);
            Assert.True(modeless.HasParameters);
            Assert.Same(owner, modeless.Owner);
            Assert.Equal(1, modeless.Key);
            Assert.Null(modeless.ResultType);

            var modal = navigator.Calls[1];
            Assert.True(modal.Modal);
            Assert.False(modal.HasParameters);
            Assert.Equal(typeof(string), modal.ResultType);
        }

        // ---- PrepareWindow ----

        [Fact]
        public void Prepare_IsRecordedAsPrepared_AndClosesLikeAWindow()
        {
            var navigator = new FakeWindowNavigator();
            InteractionResult<int> closed = null;

            var view = navigator.For(new FixturePresenter()).PrepareWindow<int>(key: 7, onClosed: r => closed = r);

            var call = Assert.Single(navigator.Calls);
            Assert.True(call.Prepared);
            Assert.False(call.Modal);
            Assert.Equal(7, call.Key);
            var window = Assert.Single(navigator.Windows);
            Assert.Same(window.View, view);

            window.CloseWithResult(3);
            Assert.Equal(3, closed.Value);
        }

        [Fact]
        public void Prepare_SameKeyWhileOpen_ReturnsTheSameView_WithoutAnActivation()
        {
            var navigator = new FakeWindowNavigator();
            var second = new FixturePresenter();

            var first = navigator.For(new FixturePresenter()).PrepareWindow(key: 7);
            var again = navigator.For(second).PrepareWindow(key: 7);

            Assert.Same(first, again);
            Assert.Single(navigator.Windows);
            Assert.Equal(0, navigator.Windows[0].Activations);
            Assert.False(navigator.Calls[1].Activated);
            Assert.True(second.Disposed);
        }

        [Fact]
        public void Prepare_WithParameters_RecordsThem()
        {
            var navigator = new FakeWindowNavigator();

            navigator.For(new ParamFixturePresenter()).WithParam(5).PrepareWindow();

            Assert.Equal(5, Assert.Single(navigator.Windows).Parameters);
            Assert.True(navigator.Calls[0].Prepared);
        }
    }
}