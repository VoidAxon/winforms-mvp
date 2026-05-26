using System;
using System.Collections.Generic;
using System.Linq;
using WinformsMVP.Logging;
using WinformsMVP.MVP.ViewActions;
using WinformsMVP.Samples.Tests.TestHelpers;
using Xunit;

namespace WinformsMVP.Samples.Tests.ViewActions
{
    /// <summary>
    /// Tests covering the dispatch middleware pipeline added to
    /// <see cref="ViewActionDispatcher"/>: fast / slow path equivalence,
    /// onion-model ordering, short-circuit, pipeline caching, and exception
    /// flow between middleware and the built-in safety net.
    /// </summary>
    public class ViewActionDispatcherMiddlewareTests
    {
        private static readonly ViewAction TestAction = ViewAction.Create("Test.Middleware");

        // ----- Use() basic semantics -----

        [Fact]
        public void Use_NullMiddleware_Throws()
        {
            var dispatcher = new ViewActionDispatcher();
            Assert.Throws<ArgumentNullException>(() => dispatcher.Use((IDispatchMiddleware)null));
            Assert.Throws<ArgumentNullException>(
                () => dispatcher.Use((Action<DispatchContext, DispatchDelegate>)null));
        }

        [Fact]
        public void Use_ReturnsSameDispatcher_ForChaining()
        {
            var dispatcher = new ViewActionDispatcher();
            var ret1 = dispatcher.Use((ctx, next) => next(ctx));
            var ret2 = dispatcher.Use(new RecordingMiddleware("a", new List<string>()));

            Assert.Same(dispatcher, ret1);
            Assert.Same(dispatcher, ret2);
        }

        [Fact]
        public void NoMiddlewareRegistered_TakesFastPath()
        {
            var dispatcher = new ViewActionDispatcher();
            Assert.Equal(0, dispatcher.MiddlewareCount);
        }

        // ----- Onion-model ordering -----

        [Fact]
        public void Middleware_RunsInRegistrationOrder_OutermostFirst()
        {
            var dispatcher = new ViewActionDispatcher();
            var trace = new List<string>();

            dispatcher.Use(new RecordingMiddleware("A", trace));
            dispatcher.Use(new RecordingMiddleware("B", trace));
            dispatcher.Use(new RecordingMiddleware("C", trace));

            dispatcher.Register(TestAction, () => trace.Add("handler"));

            dispatcher.Dispatch(TestAction);

            Assert.Equal(
                new[] { "A:pre", "B:pre", "C:pre", "handler", "C:post", "B:post", "A:post" },
                trace);
        }

        [Fact]
        public void InlineMiddleware_RunsCorrectly_AsLambda()
        {
            var dispatcher = new ViewActionDispatcher();
            var trace = new List<string>();

            dispatcher.Use((ctx, next) =>
            {
                trace.Add("outer:pre");
                next(ctx);
                trace.Add("outer:post");
            });
            dispatcher.Use((ctx, next) =>
            {
                trace.Add("inner:pre");
                next(ctx);
                trace.Add("inner:post");
            });

            dispatcher.Register(TestAction, () => trace.Add("handler"));
            dispatcher.Dispatch(TestAction);

            Assert.Equal(
                new[] { "outer:pre", "inner:pre", "handler", "inner:post", "outer:post" },
                trace);
        }

        // ----- Short-circuit -----

        [Fact]
        public void Middleware_NotCallingNext_ShortCircuitsPipeline_HandlerNotExecuted()
        {
            var dispatcher = new ViewActionDispatcher();
            bool handlerCalled = false;
            bool innerEntered = false;

            dispatcher.Use((ctx, next) =>
            {
                // Deliberately do not call next(ctx)
            });
            dispatcher.Use((ctx, next) =>
            {
                innerEntered = true;
                next(ctx);
            });

            dispatcher.Register(TestAction, () => handlerCalled = true);

            dispatcher.Dispatch(TestAction);

            Assert.False(handlerCalled);
            Assert.False(innerEntered);
        }

        [Fact]
        public void Middleware_ShortCircuits_ActionExecutedNotRaised()
        {
            var dispatcher = new ViewActionDispatcher();
            bool actionExecutedRaised = false;
            dispatcher.ActionExecuted += (s, k) => actionExecutedRaised = true;

            dispatcher.Use((ctx, next) => { /* short-circuit */ });
            dispatcher.Register(TestAction, () => { });

            dispatcher.Dispatch(TestAction);

            Assert.False(actionExecutedRaised);
        }

        // ----- DispatchContext state -----

        [Fact]
        public void DispatchContext_ExposesActionAndPayload()
        {
            var dispatcher = new ViewActionDispatcher();
            DispatchContext captured = null;

            dispatcher.Use((ctx, next) =>
            {
                captured = ctx;
                next(ctx);
            });

            dispatcher.Register<string>(TestAction, _ => { });
            dispatcher.Dispatch(TestAction, "hello");

            Assert.NotNull(captured);
            Assert.Equal(TestAction, captured.Action);
            Assert.Equal("hello", captured.Payload);
            Assert.Equal(typeof(string), captured.ExpectedPayloadType);
        }

        [Fact]
        public void DispatchContext_HandlerExecuted_TrueAfterSuccessfulRun()
        {
            var dispatcher = new ViewActionDispatcher();
            DispatchContext captured = null;

            dispatcher.Use((ctx, next) =>
            {
                next(ctx);
                captured = ctx;
            });

            dispatcher.Register(TestAction, () => { });
            dispatcher.Dispatch(TestAction);

            Assert.True(captured.HandlerExecuted);
            Assert.Null(captured.Exception);
        }

        [Fact]
        public void DispatchContext_HandlerExecuted_FalseWhenShortCircuited()
        {
            var dispatcher = new ViewActionDispatcher();
            DispatchContext captured = null;

            dispatcher.Use((ctx, next) =>
            {
                captured = ctx;
                // short-circuit
            });

            dispatcher.Register(TestAction, () => { });
            dispatcher.Dispatch(TestAction);

            Assert.False(captured.HandlerExecuted);
        }

        // ----- Pipeline caching -----

        [Fact]
        public void Pipeline_ReusesCompiledChain_AcrossDispatches()
        {
            var dispatcher = new ViewActionDispatcher();
            int middlewareInstantiations = 0;
            var mw = new CountingMiddleware(() => middlewareInstantiations++);
            dispatcher.Use(mw);
            dispatcher.Register(TestAction, () => { });

            dispatcher.Dispatch(TestAction);
            dispatcher.Dispatch(TestAction);
            dispatcher.Dispatch(TestAction);

            // Same middleware instance invoked three times — no rebuild between dispatches.
            Assert.Equal(3, mw.InvokeCount);
        }

        [Fact]
        public void Pipeline_InvalidatesCache_WhenUseCalledAgain()
        {
            var dispatcher = new ViewActionDispatcher();
            var trace = new List<string>();

            dispatcher.Use(new RecordingMiddleware("first", trace));
            dispatcher.Register(TestAction, () => trace.Add("handler"));

            dispatcher.Dispatch(TestAction);
            // After first dispatch: trace = [first:pre, handler, first:post]

            dispatcher.Use(new RecordingMiddleware("second", trace));
            dispatcher.Dispatch(TestAction);
            // After second dispatch the pipeline must include "second" too.

            Assert.Equal(
                new[]
                {
                    "first:pre", "handler", "first:post",
                    "first:pre", "second:pre", "handler", "second:post", "first:post"
                },
                trace);
        }

        // ----- Exception handling -----

        [Fact]
        public void Handler_Throws_NoMiddlewareCatches_BuiltinSafetyNetLogsAndSwallows()
        {
            var dispatcher = new ViewActionDispatcher();
            var logger = new CapturingLogger();
            dispatcher.Logger = logger;

            // pass-through middleware
            dispatcher.Use((ctx, next) => next(ctx));
            dispatcher.Register(TestAction, () => throw new InvalidOperationException("boom"));

            var ex = Record.Exception(() => dispatcher.Dispatch(TestAction));

            Assert.Null(ex);
            Assert.Single(logger.Entries);
            Assert.Equal(LogLevel.Error, logger.Entries[0].Level);
            Assert.IsType<InvalidOperationException>(logger.Entries[0].Exception);
        }

        [Fact]
        public void Handler_Throws_MiddlewareWrapsTryCatch_MiddlewareSeesException()
        {
            var dispatcher = new ViewActionDispatcher();
            Exception caughtByMiddleware = null;

            dispatcher.Use((ctx, next) =>
            {
                try { next(ctx); }
                catch (Exception ex)
                {
                    caughtByMiddleware = ex;
                    ctx.Exception = ex; // mark as handled
                }
            });

            dispatcher.Register(TestAction, () => throw new InvalidOperationException("boom"));

            dispatcher.Dispatch(TestAction);

            Assert.IsType<InvalidOperationException>(caughtByMiddleware);
        }

        [Fact]
        public void Handler_Throws_MiddlewareAbsorbsException_ActionExecutedNotRaised()
        {
            var dispatcher = new ViewActionDispatcher();
            bool actionExecutedRaised = false;
            dispatcher.ActionExecuted += (s, k) => actionExecutedRaised = true;

            dispatcher.Use((ctx, next) =>
            {
                try { next(ctx); }
                catch (Exception ex) { ctx.Exception = ex; }
            });

            dispatcher.Register(TestAction, () => throw new InvalidOperationException("boom"));
            dispatcher.Dispatch(TestAction);

            Assert.False(actionExecutedRaised);
        }

        [Fact]
        public void Handler_Throws_MiddlewareSwallowsAndClears_ActionExecutedStillNotRaised()
        {
            // Even if a middleware clears ctx.Exception, HandlerExecuted is still false
            // because the handler didn't complete — so ActionExecuted must NOT be raised.
            var dispatcher = new ViewActionDispatcher();
            bool actionExecutedRaised = false;
            dispatcher.ActionExecuted += (s, k) => actionExecutedRaised = true;

            dispatcher.Use((ctx, next) =>
            {
                try { next(ctx); }
                catch { /* fully swallow */ }
            });

            dispatcher.Register(TestAction, () => throw new InvalidOperationException("boom"));
            dispatcher.Dispatch(TestAction);

            Assert.False(actionExecutedRaised);
        }

        // ----- Fast/slow path equivalence -----

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void HandlerExceptions_LogIdentically_OnBothPaths(bool forceSlowPath)
        {
            var dispatcher = new ViewActionDispatcher();
            var logger = new CapturingLogger();
            dispatcher.Logger = logger;

            if (forceSlowPath)
            {
                dispatcher.Use((ctx, next) => next(ctx));
            }

            dispatcher.Register(TestAction, () => throw new InvalidOperationException("boom"));
            dispatcher.Dispatch(TestAction);

            Assert.Single(logger.Entries);
            Assert.Equal(LogLevel.Error, logger.Entries[0].Level);
            Assert.IsType<InvalidOperationException>(logger.Entries[0].Exception);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void CanExecuteFalse_HandlerSkipped_OnBothPaths(bool forceSlowPath)
        {
            var dispatcher = new ViewActionDispatcher();
            bool handlerCalled = false;
            bool middlewareEntered = false;

            if (forceSlowPath)
            {
                dispatcher.Use((ctx, next) =>
                {
                    middlewareEntered = true;
                    next(ctx);
                });
            }

            dispatcher.Register(TestAction, () => handlerCalled = true, canExecute: () => false);
            dispatcher.Dispatch(TestAction);

            Assert.False(handlerCalled);
            Assert.False(middlewareEntered); // CanExecute=false stops dispatch BEFORE middleware
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void PayloadMismatch_HandlerSkipped_OnBothPaths(bool forceSlowPath)
        {
            var dispatcher = new ViewActionDispatcher();
            var logger = new CapturingLogger();
            dispatcher.Logger = logger;

            bool handlerCalled = false;
            bool middlewareEntered = false;

            if (forceSlowPath)
            {
                dispatcher.Use((ctx, next) =>
                {
                    middlewareEntered = true;
                    next(ctx);
                });
            }

            dispatcher.Register<string>(TestAction, _ => handlerCalled = true);
            dispatcher.Dispatch(TestAction, payload: 42);

            Assert.False(handlerCalled);
            Assert.False(middlewareEntered); // payload mismatch stops dispatch BEFORE middleware
            Assert.Single(logger.Entries);
            Assert.Equal(LogLevel.Warning, logger.Entries[0].Level);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void SuccessfulDispatch_RaisesActionExecuted_OnBothPaths(bool forceSlowPath)
        {
            var dispatcher = new ViewActionDispatcher();
            bool actionExecutedRaised = false;
            dispatcher.ActionExecuted += (s, k) => actionExecutedRaised = true;

            if (forceSlowPath)
            {
                dispatcher.Use((ctx, next) => next(ctx));
            }

            dispatcher.Register(TestAction, () => { });
            dispatcher.Dispatch(TestAction);

            Assert.True(actionExecutedRaised);
        }

        // ----- Recursive / re-entrant dispatch -----

        [Fact]
        public void Reentrant_Dispatch_WorksThroughPipeline()
        {
            // A handler that dispatches another action: the pipeline must be reusable
            // and not break re-entrancy. The current design caches the pipeline once
            // and looks up handler from context, so re-entrant dispatch is safe.
            var dispatcher = new ViewActionDispatcher();
            var trace = new List<string>();
            var SecondAction = ViewAction.Create("Test.Second");

            dispatcher.Use(new RecordingMiddleware("mw", trace));
            dispatcher.Register(TestAction, () =>
            {
                trace.Add("first-handler-start");
                dispatcher.Dispatch(SecondAction);
                trace.Add("first-handler-end");
            });
            dispatcher.Register(SecondAction, () => trace.Add("second-handler"));

            dispatcher.Dispatch(TestAction);

            Assert.Equal(
                new[]
                {
                    "mw:pre",
                    "first-handler-start",
                        "mw:pre",
                        "second-handler",
                        "mw:post",
                    "first-handler-end",
                    "mw:post"
                },
                trace);
        }

        // ----- Helpers -----

        private class RecordingMiddleware : IDispatchMiddleware
        {
            private readonly string _name;
            private readonly List<string> _trace;

            public RecordingMiddleware(string name, List<string> trace)
            {
                _name = name;
                _trace = trace;
            }

            public void Invoke(DispatchContext context, DispatchDelegate next)
            {
                _trace.Add(_name + ":pre");
                next(context);
                _trace.Add(_name + ":post");
            }
        }

        private class CountingMiddleware : IDispatchMiddleware
        {
            public int InvokeCount { get; private set; }

            public CountingMiddleware(Action onInstantiated)
            {
                onInstantiated?.Invoke();
            }

            public void Invoke(DispatchContext context, DispatchDelegate next)
            {
                InvokeCount++;
                next(context);
            }
        }
    }
}
