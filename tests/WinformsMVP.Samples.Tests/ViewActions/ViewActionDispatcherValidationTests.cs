using System;
using WinformsMVP.MVP.ViewActions;
using Xunit;

namespace WinformsMVP.Samples.Tests.ViewActions
{
    /// <summary>
    /// Tests for <see cref="ViewActionDispatcher.ValidationMode"/> (the Debug-build "strict"
    /// safety net). Strict mode turns the two silent dispatch-time misconfigurations —
    /// dispatching an unregistered key, or a payload whose type doesn't match the handler —
    /// into thrown exceptions, while leaving handler/CanExecute exception handling and disabled
    /// actions untouched.
    /// </summary>
    public class ViewActionDispatcherValidationTests
    {
        private static readonly ViewAction Action = ViewAction.Create("Test.Action");
        private static readonly ViewAction Unregistered = ViewAction.Create("Test.Unregistered");

        [Fact]
        public void DefaultMode_IsLenient()
        {
            Assert.Equal(DispatchValidationMode.Lenient, new ViewActionDispatcher().ValidationMode);
        }

        [Fact]
        public void Lenient_DispatchUnregistered_DoesNotThrow()
        {
            var dispatcher = new ViewActionDispatcher();

            // No handler registered; lenient mode logs and ignores.
            dispatcher.Dispatch(Unregistered);
        }

        [Fact]
        public void Strict_DispatchUnregistered_Throws()
        {
            var dispatcher = new ViewActionDispatcher { ValidationMode = DispatchValidationMode.Strict };

            var ex = Assert.Throws<InvalidOperationException>(() => dispatcher.Dispatch(Unregistered));
            Assert.Contains("Test.Unregistered", ex.Message);
        }

        [Fact]
        public void Strict_DispatchRegistered_RunsHandlerAndDoesNotThrow()
        {
            var ran = false;
            var dispatcher = new ViewActionDispatcher { ValidationMode = DispatchValidationMode.Strict };
            dispatcher.Register(Action, () => ran = true);

            dispatcher.Dispatch(Action);

            Assert.True(ran);
        }

        [Fact]
        public void Strict_PayloadTypeMismatch_Throws()
        {
            var dispatcher = new ViewActionDispatcher { ValidationMode = DispatchValidationMode.Strict };
            dispatcher.Register<string>(Action, _ => { });

            var ex = Assert.Throws<InvalidOperationException>(() => dispatcher.Dispatch(Action, 42));
            Assert.Contains("Test.Action", ex.Message);
        }

        [Fact]
        public void Lenient_PayloadTypeMismatch_DoesNotThrow()
        {
            var ran = false;
            var dispatcher = new ViewActionDispatcher();
            dispatcher.Register<string>(Action, _ => ran = true);

            dispatcher.Dispatch(Action, 42);

            Assert.False(ran); // handler skipped, but no exception
        }

        [Fact]
        public void Strict_CanExecuteFalse_DoesNotThrow_AndDoesNotRun()
        {
            var ran = false;
            var dispatcher = new ViewActionDispatcher { ValidationMode = DispatchValidationMode.Strict };
            dispatcher.Register(Action, () => ran = true, canExecute: () => false);

            // A disabled action is legitimate, not a misconfiguration — strict must stay silent.
            dispatcher.Dispatch(Action);

            Assert.False(ran);
        }

        [Fact]
        public void Strict_HandlerThrows_IsStillCaughtAndDoesNotEscape()
        {
            var entered = false;
            var dispatcher = new ViewActionDispatcher { ValidationMode = DispatchValidationMode.Strict };
            dispatcher.Register(Action, () =>
            {
                entered = true;
                throw new InvalidOperationException("boom");
            });

            // Strict mode targets misconfiguration, NOT handler bugs: the handler exception is
            // still caught and logged (centralized error handling), so it must not escape.
            dispatcher.Dispatch(Action);

            Assert.True(entered);
        }
    }
}
