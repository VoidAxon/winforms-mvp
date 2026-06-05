using System;
using System.Collections.Generic;
using WinformsMVP.MVP.Views;
using WinformsMVP.MVP.Presenters;
using WinformsMVP.MVP.ViewActions;
using WinformsMVP.Samples.Tests.Mocks;
using WinformsMVP.Samples.Tests.TestHelpers;
using Xunit;

namespace WinformsMVP.Samples.Tests.ViewActions
{
    /// <summary>
    /// Integration tests for <see cref="WinformsMVP.MVP.Presenters.PresenterBase{TView}"/>
    /// + <see cref="ViewActionDispatcher"/> middleware. Specifically verifies the contract
    /// that global middleware (from <c>IPlatformServices.ConfigureDispatcher</c>) is
    /// applied <b>before</b> any local middleware registered inside
    /// <c>RegisterViewActions</c>, so global ends up outermost in the onion.
    /// </summary>
    public class PresenterMiddlewareIntegrationTests
    {
        private static readonly ViewAction SaveAction = ViewAction.Create("Test.Save");

        [Fact]
        public void GlobalMiddleware_AppliedBeforeLocalMiddleware_ExecutionOrderCorrect()
        {
            var trace = new List<string>();
            var platform = new MockPlatformServices
            {
                ConfigureDispatcher = d => d.Use(new TraceMW("global", trace))
            };

            var presenter = new TestPresenter(trace)
                .WithPlatformServices(platform);

            presenter.AttachView(new TestView());
            presenter.Initialize();

            presenter.Dispatch(SaveAction);

            // Global wraps local; local wraps handler.
            Assert.Equal(
                new[] { "global:pre", "local:pre", "handler", "local:post", "global:post" },
                trace);
        }

        [Fact]
        public void NoGlobalMiddleware_OnlyLocalRuns()
        {
            var trace = new List<string>();
            var platform = new MockPlatformServices();  // ConfigureDispatcher is null

            var presenter = new TestPresenter(trace)
                .WithPlatformServices(platform);

            presenter.AttachView(new TestView());
            presenter.Initialize();

            presenter.Dispatch(SaveAction);

            Assert.Equal(
                new[] { "local:pre", "handler", "local:post" },
                trace);
        }

        [Fact]
        public void NoMiddleware_FastPathStillWorks()
        {
            var trace = new List<string>();
            var platform = new MockPlatformServices();

            // Presenter with no local middleware (only handler)
            var presenter = new FastPathPresenter(trace)
                .WithPlatformServices(platform);

            presenter.AttachView(new TestView());
            presenter.Initialize();

            presenter.Dispatch(SaveAction);

            Assert.Equal(new[] { "handler" }, trace);
        }

        [Fact]
        public void GlobalConfig_IsAppliedExactlyOnce_EvenIfDispatchAccessedMultipleTimes()
        {
            int globalConfigInvocations = 0;
            var platform = new MockPlatformServices
            {
                ConfigureDispatcher = d => { globalConfigInvocations++; d.Use((ctx, next) => next(ctx)); }
            };

            var trace = new List<string>();
            var presenter = new TestPresenter(trace)
                .WithPlatformServices(platform);

            presenter.AttachView(new TestView());
            presenter.Initialize();

            // Dispatch many times — config should still only have fired once.
            for (int i = 0; i < 10; i++)
            {
                presenter.Dispatch(SaveAction);
            }

            Assert.Equal(1, globalConfigInvocations);
        }

        // ----- Helpers -----

        private class TraceMW : IDispatchMiddleware
        {
            private readonly string _name;
            private readonly List<string> _trace;
            public TraceMW(string name, List<string> trace) { _name = name; _trace = trace; }
            public void Invoke(DispatchContext context, DispatchDelegate next)
            {
                _trace.Add(_name + ":pre");
                next(context);
                _trace.Add(_name + ":post");
            }
        }

        // Minimal IWindowView mock - just enough to drive a WindowPresenterBase.
        public class TestView : IWindowView
        {
            public bool IsDisposed { get; private set; }
            public IntPtr Handle => IntPtr.Zero;
            public void Activate() { }
            public IViewActionBinder ActionBinder => NullViewActionBinder.Instance;
        }

        public class TestPresenter : WindowPresenterBase<TestView>
        {
            private readonly List<string> _trace;
            public TestPresenter(List<string> trace) { _trace = trace; }

            protected override void OnViewAttached() { }

            protected override void RegisterViewActions()
            {
                // Local middleware - should be added AFTER global, ending up innermost.
                _dispatcher.Use(new TraceMW("local", _trace));
                _dispatcher.Register(SaveAction, () => _trace.Add("handler"));
            }
        }

        public class FastPathPresenter : WindowPresenterBase<TestView>
        {
            private readonly List<string> _trace;
            public FastPathPresenter(List<string> trace) { _trace = trace; }

            protected override void OnViewAttached() { }

            protected override void RegisterViewActions()
            {
                _dispatcher.Register(SaveAction, () => _trace.Add("handler"));
            }
        }
    }
}
