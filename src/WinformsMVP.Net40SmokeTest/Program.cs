using System;
using System.ComponentModel.DataAnnotations;
using WinformsMVP.Common;
using WinformsMVP.Common.EventAggregator;
using WinformsMVP.Common.Validation.Core;
using WinformsMVP.Core.Models;
using WinformsMVP.Logging;
using WinformsMVP.MVP.ViewActions;

namespace WinformsMVP.Net40SmokeTest
{
    /// <summary>
    /// Runtime smoke test for the net40 build. xUnit targets net48 only, so this
    /// console exe is the only automated guard against IL/BCL paths that compile
    /// but fail at load or first invocation on the net40 runtime.
    ///
    /// <para>
    /// Each smoke method exercises one risk area. A throw counts as failure; the
    /// process exit code is the failure count. Output is intentionally terse so the
    /// summary is easy to scan in CI logs.
    /// </para>
    /// </summary>
    public class Program
    {
        private static int _passed;
        private static int _failed;

        public static int Main(string[] args)
        {
            Console.WriteLine("WinformsMVP net40 smoke test");
            Console.WriteLine("Runtime: " + Environment.Version + " (" + (IntPtr.Size == 8 ? "x64" : "x86") + ")");
            Console.WriteLine();

            Run("Logging          - DebugLogger + MessageFormatter named placeholders", LoggingSmoke);
            Run("EventAggregator  - pub/sub via expression-tree-compiled delegate",     EventAggregatorSmoke);
            Run("ChangeTracker    - IsChanged / AcceptChanges / RejectChanges",         ChangeTrackerSmoke);
            Run("ViewActions      - Dispatcher.Register / Dispatch + CanExecute",       ViewActionDispatcherSmoke);
            Run("ModelValidator   - DataAnnotations + Phase 3 reflection fixes",        ModelValidatorSmoke);
            Run("BindableBase     - [CallerMemberName] polyfill activates",             BindableBaseSmoke);

            Console.WriteLine();
            Console.WriteLine("Result: " + _passed + " passed, " + _failed + " failed");
            return _failed;
        }

        private static void Run(string name, Action smoke)
        {
            try
            {
                smoke();
                _passed++;
                Console.WriteLine("[PASS] " + name);
            }
            catch (Exception ex)
            {
                _failed++;
                Console.WriteLine("[FAIL] " + name);
                Console.WriteLine("       " + ex.GetType().FullName + ": " + ex.Message);
            }
        }

        // ───── Smokes ─────────────────────────────────────────────────────

        private static void LoggingSmoke()
        {
            var factory = new DebugLoggerFactory();
            var logger = factory.CreateLogger("SmokeTest");

            // Named placeholders exercise MessageFormatter on the net40 runtime.
            // A FormatException inside string.Format would propagate here.
            logger.LogInformation("User {UserName} performed {Action} on {Timestamp}", "Alice", "Save", DateTime.UtcNow);

            // NullLoggerFactory.Instance must produce a non-null logger that accepts calls.
            var nullLogger = NullLoggerFactory.Instance.CreateLogger("X");
            nullLogger.LogWarning("should be silently dropped: {Value}", 42);
        }

        private static void EventAggregatorSmoke()
        {
            // EventAggregator builds an open-instance delegate via System.Linq.Expressions
            // and falls back to MethodInfo.Invoke if compilation fails. Both paths exist
            // on net40 but the compiled-delegate path is the riskier of the two.
            var aggregator = new EventAggregator();
            string received = null;
            using (aggregator.Subscribe<string>(msg => received = msg))
            {
                aggregator.Publish("hello");
            }

            if (received != "hello")
            {
                throw new InvalidOperationException("Subscribe/Publish returned '" + (received ?? "<null>") + "', expected 'hello'.");
            }
        }

        private static void ChangeTrackerSmoke()
        {
            // ChangeTracker clones the initial value on construction, so the default
            // reference-equality comparer would mark the tracker as IsChanged immediately.
            // Provide an explicit value-equality comparer matching the model's semantics.
            var initial = new TrackedModel { Name = "before" };
            var tracker = new ChangeTracker<TrackedModel>(initial, (a, b) => a.Name == b.Name);
            if (tracker.IsChanged)
            {
                throw new InvalidOperationException("Tracker reports IsChanged immediately after construction.");
            }

            var modified = new TrackedModel { Name = "after" };
            tracker.UpdateCurrentValue(modified);
            if (!tracker.IsChanged)
            {
                throw new InvalidOperationException("Tracker did not detect a value change.");
            }

            tracker.RejectChanges();
            if (tracker.IsChanged || tracker.CurrentValue.Name != "before")
            {
                throw new InvalidOperationException("RejectChanges did not restore the original value.");
            }
        }

        private static void ViewActionDispatcherSmoke()
        {
            var dispatcher = new ViewActionDispatcher();
            var key = ViewAction.Create("Smoke.Run");

            bool invoked = false;
            bool guard = true;
            dispatcher.Register(key, () => invoked = true, canExecute: () => guard);

            if (!dispatcher.CanDispatch(key))
            {
                throw new InvalidOperationException("CanDispatch returned false when canExecute is true.");
            }

            dispatcher.Dispatch(key);
            if (!invoked)
            {
                throw new InvalidOperationException("Handler did not run on Dispatch.");
            }

            guard = false;
            invoked = false;
            dispatcher.Dispatch(key);
            if (invoked)
            {
                throw new InvalidOperationException("Handler ran despite canExecute=false.");
            }
        }

        private static void ModelValidatorSmoke()
        {
            // Exercises every Phase 3 BCL adjustment: ValidationContext(m, null, null),
            // PropertyInfo.GetValue(m, null), GetCustomAttributes(...).Cast<>().FirstOrDefault().
            var validator = ModelValidator.For<ValidatedModel>();

            var invalid = new ValidatedModel { Name = "" };
            var errors = validator.ValidateAll(invalid);
            if (errors.Count == 0)
            {
                throw new InvalidOperationException("Validator did not flag an empty [Required] property.");
            }

            var valid = new ValidatedModel { Name = "Alice" };
            var ok = validator.ValidateAll(valid);
            if (ok.Count != 0)
            {
                throw new InvalidOperationException("Validator flagged a valid model with " + ok.Count + " errors.");
            }
        }

        private static void BindableBaseSmoke()
        {
            // The [CallerMemberName] polyfill lives in src/WinformsMVP/Polyfills/CallerInfoAttributes.cs
            // under #if NET40. If the polyfill is not active, propertyName falls back to null and
            // the PropertyChanged event fires with the wrong name.
            var subject = new ObservableSubject();
            string lastName = null;
            subject.PropertyChanged += (_, e) => lastName = e.PropertyName;

            subject.Value = "hi";

            if (lastName != "Value")
            {
                throw new InvalidOperationException(
                    "Expected PropertyChanged for 'Value' but got '" + (lastName ?? "<null>") + "'. " +
                    "CallerMemberName polyfill is probably inactive under net40.");
            }
        }
    }

    // ───── Helper types ───────────────────────────────────────────────────

    internal sealed class TrackedModel : ICloneable
    {
        public string Name { get; set; }
        public object Clone() => new TrackedModel { Name = this.Name };
    }

    internal sealed class ValidatedModel
    {
        [Required(AllowEmptyStrings = false)]
        public string Name { get; set; }
    }

    internal sealed class ObservableSubject : BindableBase
    {
        private string _value;
        public string Value
        {
            get => _value;
            set => SetProperty(ref _value, value);
        }
    }
}
