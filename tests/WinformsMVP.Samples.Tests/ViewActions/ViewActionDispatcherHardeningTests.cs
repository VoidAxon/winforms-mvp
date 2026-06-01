using System;
using System.Linq;
using WinformsMVP.Logging;
using WinformsMVP.MVP.ViewActions;
using WinformsMVP.Samples.Tests.TestHelpers;
using Xunit;

namespace WinformsMVP.Samples.Tests.ViewActions
{
    /// <summary>
    /// Tests covering exception protection, payload type checks, and logger integration
    /// added to <see cref="ViewActionDispatcher"/>.
    /// </summary>
    public class ViewActionDispatcherHardeningTests
    {
        private static readonly ViewAction TestAction = ViewAction.Create("Test.Action");

        // ----- Logger default & assignment -----

        [Fact]
        public void Logger_DefaultsToNonNull_NullLoggerInstance()
        {
            var dispatcher = new ViewActionDispatcher();

            Assert.NotNull(dispatcher.Logger);
        }

        [Fact]
        public void Logger_AssigningNull_FallsBackToNullLogger()
        {
            var dispatcher = new ViewActionDispatcher();
            var capture = new CapturingLogger();

            dispatcher.Logger = capture;
            Assert.Same(capture, dispatcher.Logger);

            dispatcher.Logger = null;
            Assert.NotNull(dispatcher.Logger);
            Assert.NotSame(capture, dispatcher.Logger);
        }

        // ----- Handler exceptions are caught and logged -----

        [Fact]
        public void Dispatch_HandlerThrows_LogsErrorAndSuppressesActionExecuted()
        {
            var dispatcher = new ViewActionDispatcher();
            var logger = new CapturingLogger();
            dispatcher.Logger = logger;

            dispatcher.Register(TestAction, () => throw new InvalidOperationException("boom"));

            bool actionExecutedRaised = false;
            dispatcher.ActionExecuted += (s, k) => actionExecutedRaised = true;

            var ex = Record.Exception(() => dispatcher.Dispatch(TestAction));

            Assert.Null(ex);
            Assert.False(actionExecutedRaised);
            Assert.Single(logger.Entries);
            Assert.Equal(LogLevel.Error, logger.Entries[0].Level);
            Assert.IsType<InvalidOperationException>(logger.Entries[0].Exception);
        }

        // ----- CanExecute exceptions are caught -----

        [Fact]
        public void Dispatch_CanExecuteThrows_LogsErrorTreatsAsDisabledAndDoesNotInvokeHandler()
        {
            var dispatcher = new ViewActionDispatcher();
            var logger = new CapturingLogger();
            dispatcher.Logger = logger;

            bool handlerCalled = false;
            dispatcher.Register(
                TestAction,
                () => handlerCalled = true,
                canExecute: () => throw new InvalidOperationException("predicate failure"));

            var ex = Record.Exception(() => dispatcher.Dispatch(TestAction));

            Assert.Null(ex);
            Assert.False(handlerCalled);
            Assert.Single(logger.Entries);
            Assert.Equal(LogLevel.Error, logger.Entries[0].Level);
        }

        [Fact]
        public void CanDispatch_CanExecuteThrows_ReturnsFalseAndLogs()
        {
            var dispatcher = new ViewActionDispatcher();
            var logger = new CapturingLogger();
            dispatcher.Logger = logger;

            dispatcher.Register(
                TestAction,
                () => { },
                canExecute: () => throw new InvalidOperationException("predicate failure"));

            bool result = dispatcher.CanDispatch(TestAction);

            Assert.False(result);
            Assert.Single(logger.Entries);
            Assert.Equal(LogLevel.Error, logger.Entries[0].Level);
        }

        // ----- Payload type mismatch -----

        [Fact]
        public void Dispatch_WithWrongPayloadType_LogsWarningAndDoesNotInvokeHandler()
        {
            var dispatcher = new ViewActionDispatcher();
            var logger = new CapturingLogger();
            dispatcher.Logger = logger;

            bool handlerCalled = false;
            dispatcher.Register<string>(TestAction, _ => handlerCalled = true);

            dispatcher.Dispatch(TestAction, payload: 42);

            Assert.False(handlerCalled);
            Assert.Single(logger.Entries);
            Assert.Equal(LogLevel.Warning, logger.Entries[0].Level);
            Assert.Contains("payload type mismatch", logger.Entries[0].Message, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void Dispatch_WithMatchingPayloadType_InvokesHandler()
        {
            var dispatcher = new ViewActionDispatcher();
            var logger = new CapturingLogger();
            dispatcher.Logger = logger;

            string captured = null;
            dispatcher.Register<string>(TestAction, s => captured = s);

            dispatcher.Dispatch(TestAction, "hello");

            Assert.Equal("hello", captured);
            Assert.Empty(logger.Entries);
        }

        [Fact]
        public void Dispatch_ValueTypeHandlerWithNullPayload_LogsWarningAndSkips()
        {
            var dispatcher = new ViewActionDispatcher();
            var logger = new CapturingLogger();
            dispatcher.Logger = logger;

            bool handlerCalled = false;
            dispatcher.Register<int>(TestAction, _ => handlerCalled = true);

            dispatcher.Dispatch(TestAction, payload: null);

            Assert.False(handlerCalled);
            Assert.Single(logger.Entries);
            Assert.Equal(LogLevel.Warning, logger.Entries[0].Level);
        }

        [Fact]
        public void Dispatch_ReferenceTypeHandlerWithNullPayload_InvokesHandler()
        {
            var dispatcher = new ViewActionDispatcher();
            string captured = "not-null";
            dispatcher.Register<string>(TestAction, s => captured = s);

            dispatcher.Dispatch(TestAction, payload: null);

            Assert.Null(captured);
        }

        [Fact]
        public void Dispatch_NullableValueTypeHandlerWithNullPayload_InvokesHandler()
        {
            var dispatcher = new ViewActionDispatcher();
            int? captured = 99;
            dispatcher.Register<int?>(TestAction, v => captured = v);

            dispatcher.Dispatch(TestAction, payload: null);

            Assert.Null(captured);
        }

        [Fact]
        public void Dispatch_ParameterlessHandlerWithExtraPayload_StillInvokesHandler()
        {
            var dispatcher = new ViewActionDispatcher();
            var logger = new CapturingLogger();
            dispatcher.Logger = logger;

            bool handlerCalled = false;
            dispatcher.Register(TestAction, () => handlerCalled = true);

            dispatcher.Dispatch(TestAction, payload: "ignored");

            Assert.True(handlerCalled);
            Assert.Empty(logger.Entries);
        }

        // ----- Unregistered keys -----

        [Fact]
        public void Dispatch_UnregisteredAction_LogsDebugAndDoesNotThrow()
        {
            var dispatcher = new ViewActionDispatcher();
            var logger = new CapturingLogger();
            dispatcher.Logger = logger;

            var ex = Record.Exception(() => dispatcher.Dispatch(TestAction));

            Assert.Null(ex);
            Assert.Single(logger.Entries);
            Assert.Equal(LogLevel.Debug, logger.Entries[0].Level);
        }

        // ----- ActionExecuted suppression on failure -----

        [Fact]
        public void Dispatch_PayloadMismatch_DoesNotRaiseActionExecuted()
        {
            var dispatcher = new ViewActionDispatcher();
            dispatcher.Register<string>(TestAction, _ => { });

            bool raised = false;
            dispatcher.ActionExecuted += (s, k) => raised = true;

            dispatcher.Dispatch(TestAction, payload: 42);

            Assert.False(raised);
        }
    }
}
