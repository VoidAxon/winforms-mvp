using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel.DataAnnotations;
using System.Threading;
using System.Windows.Forms;
using WinformsMVP.Common;
using WinformsMVP.Common.EventAggregator;
using WinformsMVP.Common.Validation.Core;
using WinformsMVP.MVP.ViewActions;
using Xunit;

namespace WinformsMVP.Samples.Tests.Common
{
    /// <summary>
    /// Regression tests for review findings in the ViewAction layer and Common utilities.
    /// </summary>
    public class CommonHardeningTests
    {
        private static readonly ViewAction Save = ViewAction.Create("Hardening.Save");
        private static readonly ViewAction Delete = ViewAction.Create("Hardening.Delete");

        #region ViewActionDispatcher

        [Fact]
        public void Register_DefaultKey_Throws()
        {
            var dispatcher = new ViewActionDispatcher();
            Assert.Throws<ArgumentException>(() => dispatcher.Register(default(ViewAction), () => { }));
        }

        [Fact]
        public void Register_NullHandler_Throws()
        {
            var dispatcher = new ViewActionDispatcher();
            Assert.Throws<ArgumentNullException>(() => dispatcher.Register(Save, (Action)null));
        }

        [Fact]
        public void Dispatch_DefaultKey_IsIgnored()
        {
            var dispatcher = new ViewActionDispatcher();
            dispatcher.Dispatch(default(ViewAction));   // must not throw
            Assert.False(dispatcher.CanDispatch(default(ViewAction)));
        }

        [Fact]
        public void HandlerThrows_FastPath_RaisesCanExecuteChanged()
        {
            var dispatcher = new ViewActionDispatcher();
            dispatcher.Register(Save, () => throw new InvalidOperationException("boom"));
            int refreshes = 0;
            dispatcher.CanExecuteChanged += (s, e) => refreshes++;

            dispatcher.Dispatch(Save);

            Assert.Equal(1, refreshes);
        }

        [Fact]
        public void HandlerThrows_OuterMiddlewareSeesExceptionWhileUnwinding()
        {
            var dispatcher = new ViewActionDispatcher();
            dispatcher.Register(Save, () => throw new InvalidOperationException("boom"));
            Exception seenInFinally = null;
            bool executedInFinally = true;
            dispatcher.Use((ctx, next) =>
            {
                try { next(ctx); }
                finally
                {
                    seenInFinally = ctx.Exception;
                    executedInFinally = ctx.HandlerExecuted;
                }
            });
            int refreshes = 0;
            dispatcher.CanExecuteChanged += (s, e) => refreshes++;

            dispatcher.Dispatch(Save);

            Assert.IsType<InvalidOperationException>(seenInFinally);
            Assert.False(executedInFinally);
            Assert.Equal(1, refreshes);
        }

        #endregion

        #region ViewActionBinder

        [Fact]
        public void Binder_SameControlForTwoActions_Throws()
        {
            var binder = new ViewActionBinder();
            using (var button = new Button { Name = "btnGo" })
            {
                binder.Add(Save, button);

                var ex = Assert.Throws<InvalidOperationException>(() => binder.Add(Delete, button));
                Assert.Contains("btnGo", ex.Message);
                Assert.Contains("Hardening.Save", ex.Message);
            }
        }

        [Fact]
        public void Binder_DefaultKey_Throws()
        {
            var binder = new ViewActionBinder();
            using (var button = new Button())
                Assert.Throws<ArgumentException>(() => binder.Add(default(ViewAction), button));
        }

        #endregion

        #region EventAggregator

        /// <summary>Queues posted callbacks so the test decides when they run.</summary>
        private sealed class QueueingContext : SynchronizationContext
        {
            public readonly Queue<(SendOrPostCallback, object)> Posted = new Queue<(SendOrPostCallback, object)>();
            public override void Post(SendOrPostCallback d, object state) => Posted.Enqueue((d, state));
            public void RunAll() { while (Posted.Count > 0) { var (d, s) = Posted.Dequeue(); d(s); } }
        }

        private sealed class Receiver
        {
            public int Received;
            public void On(string message) => Received++;
        }

        private static T WithContext<T>(SynchronizationContext context, Func<T> action)
        {
            var previous = SynchronizationContext.Current;
            SynchronizationContext.SetSynchronizationContext(context);
            try { return action(); }
            finally { SynchronizationContext.SetSynchronizationContext(previous); }
        }

        [Fact]
        public void EventAggregator_DisposedWhileMessageQueued_IsNotDelivered()
        {
            var ui = new QueueingContext();
            var aggregator = new EventAggregator();
            var receiver = new Receiver();
            // Each subscription delivers on the context current when it subscribes (the UI thread).
            var token = WithContext(ui, () => aggregator.Subscribe<string>(receiver.On));

            WithContext<object>(null, () => { aggregator.Publish("from worker"); return null; });
            Assert.Single(ui.Posted);

            token.Dispose();     // e.g. the subscriber's window closed
            ui.RunAll();

            Assert.Equal(0, receiver.Received);
        }

        [Fact]
        public void EventAggregator_CreatedWithoutContext_UsesSubscribersContext()
        {
            // Created in Program.Main before WinForms installs its context.
            var aggregator = WithContext<EventAggregator>(null, () => new EventAggregator());
            var ui = new QueueingContext();
            var receiver = new Receiver();
            WithContext(ui, () => aggregator.Subscribe<string>(receiver.On));

            WithContext<object>(null, () => { aggregator.Publish("from worker"); return null; });

            Assert.Equal(0, receiver.Received);   // marshalled, not run on the worker
            ui.RunAll();
            Assert.Equal(1, receiver.Received);
        }

        [Fact]
        public void EventAggregator_ExtensionMethodHandler_IsInvoked()
        {
            var aggregator = WithContext<EventAggregator>(null, () => new EventAggregator());
            var receiver = new ExtensionReceiver();
            Action<string> handler = receiver.Bump;   // closed over the first argument

            using (WithContext<object>(null, () => aggregator.Subscribe(handler)) as IDisposable)
                WithContext<object>(null, () => { aggregator.Publish("x"); return null; });

            Assert.Equal(1, receiver.Received);
        }

        #endregion

        #region ChangeTracker

        private sealed class Doc
        {
            public string Title { get; set; }
        }

        [Fact]
        public void ChangeTracker_InPlaceEdit_IsSeenByIsChanged()
        {
            var doc = new Doc { Title = "a" };
            var tracker = new ChangeTracker<Doc>(doc);
            Assert.False(tracker.IsChanged);

            doc.Title = "b";

            Assert.True(tracker.IsChanged);
        }

        #endregion

        #region CompositeDisposable

        [Fact]
        public void CompositeDisposable_OneItemThrows_RestAreStillDisposed()
        {
            var bag = new CompositeDisposable();
            bool first = false, last = false;
            bag.Add(Disposable.Create(() => first = true));
            bag.Add(Disposable.Create(() => throw new InvalidOperationException("bad")));
            bag.Add(Disposable.Create(() => last = true));

            Assert.Throws<InvalidOperationException>(() => bag.Dispose());

            Assert.True(first);
            Assert.True(last);
        }

        #endregion

        #region ObjectCloner / ObjectComparer collections

        private sealed class Tag
        {
            public string Name;
        }

        private sealed class Model
        {
            public HashSet<string> Keywords = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            public Queue<int> Pending = new Queue<int>();
            public Stack<string> History = new Stack<string>();
            public ReadOnlyCollection<int> Fixed = new ReadOnlyCollection<int>(new[] { 1, 2, 3 });
            public HashSet<Tag> Tags = new HashSet<Tag>();
            public Type Kind = typeof(string);
        }

        [Fact]
        public void Cloner_HashSet_KeepsComparerAndMembership()
        {
            var model = new Model();
            model.Keywords.Add("Alpha");

            var copy = ObjectCloner.DeepCopy(model);

            Assert.NotSame(model.Keywords, copy.Keywords);
            Assert.Contains("ALPHA", copy.Keywords);   // still case-insensitive
        }

        [Fact]
        public void Cloner_HashSetOfReferenceObjects_LookupsWorkOnTheClone()
        {
            var model = new Model();
            model.Tags.Add(new Tag { Name = "x" });

            var copy = ObjectCloner.DeepCopy(model);

            foreach (var tag in copy.Tags)
                Assert.Contains(tag, copy.Tags);   // hash slots were rebuilt, not field-copied
        }

        [Fact]
        public void Cloner_StackQueueReadOnlyCollectionAndType()
        {
            var model = new Model();
            model.Pending.Enqueue(1);
            model.Pending.Enqueue(2);
            model.History.Push("first");
            model.History.Push("second");

            var copy = ObjectCloner.DeepCopy(model);

            Assert.Equal(new[] { 1, 2 }, copy.Pending.ToArray());
            Assert.Equal("second", copy.History.Peek());          // top preserved
            Assert.Equal(new[] { 1, 2, 3 }, copy.Fixed);
            Assert.Same(typeof(string), copy.Kind);
        }

        [Fact]
        public void Comparer_SetOfReferenceObjects_EqualsItsUntouchedDeepCopy()
        {
            var original = new Model();
            original.Tags.Add(new Tag { Name = "x" });
            original.Tags.Add(new Tag { Name = "y" });
            var copy = ObjectCloner.DeepCopy(original);

            Assert.True(ObjectComparer.DeepEquals(original, copy));

            foreach (var tag in copy.Tags) { tag.Name = "changed"; break; }
            Assert.False(ObjectComparer.DeepEquals(original, copy));
        }

        private sealed class TagNameComparer : IEqualityComparer<Tag>
        {
            public bool Equals(Tag x, Tag y) => string.Equals(x?.Name, y?.Name, StringComparison.OrdinalIgnoreCase);
            public int GetHashCode(Tag t) => StringComparer.OrdinalIgnoreCase.GetHashCode(t.Name ?? "");
        }

        [Fact]
        public void Comparer_SetWithExplicitComparer_UsesThatComparer()
        {
            var a = new HashSet<Tag>(new TagNameComparer()) { new Tag { Name = "x" } };
            var b = new HashSet<Tag>(new TagNameComparer()) { new Tag { Name = "X" } };

            Assert.True(ObjectComparer.DeepEquals(a, b));
        }

        private sealed class IntStack : Stack<int> { }

        [Fact]
        public void Cloner_StackSubclass_KeepsOrder()
        {
            var stack = new IntStack();
            stack.Push(1);
            stack.Push(2);

            var copy = ObjectCloner.DeepCopy(stack);

            Assert.Equal(2, copy.Peek());
            Assert.Equal(stack.ToArray(), copy.ToArray());
        }

        [Fact]
        public void ChangeTracker_ModelWithReferenceSet_StartsClean()
        {
            var model = new Model();
            model.Tags.Add(new Tag { Name = "x" });

            var tracker = new ChangeTracker<Model>(model);

            Assert.False(tracker.IsChanged);
        }

        [Fact]
        public void Cloner_QueueContainingItself_CopiesTheCycle()
        {
            var queue = new Queue<object>();
            queue.Enqueue(queue);

            var copy = ObjectCloner.DeepCopy(queue);

            Assert.NotSame(queue, copy);
            Assert.Same(copy, copy.Peek());
        }

        [Fact]
        public void Cloner_CycleViaObjectThroughConstructorOnlyCollection_IsCopied()
        {
            var holder = new Holder();
            holder.Items = new ReadOnlyCollection<object>(new List<object> { holder });

            var copy = ObjectCloner.DeepCopy(holder);

            Assert.Same(copy, copy.Items[0]);   // the object was registered before its fields
        }

        [Fact]
        public void Cloner_ConstructorOnlyCollectionContainingItself_ThrowsNotSupported()
        {
            var inner = new List<object>();
            var readOnly = new ReadOnlyCollection<object>(inner);
            inner.Add(readOnly);

            Assert.Throws<NotSupportedException>(() => ObjectCloner.DeepCopy(readOnly));
        }

        private sealed class Holder
        {
            public ReadOnlyCollection<object> Items;
        }

        [Fact]
        public void Comparer_HashSetAddThenRemove_IsUnchanged()
        {
            var original = new Model();
            original.Keywords.Add("a");
            var edited = ObjectCloner.DeepCopy(original);

            edited.Keywords.Add("b");
            edited.Keywords.Remove("b");

            Assert.True(ObjectComparer.DeepEquals(original, edited));
        }

        [Fact]
        public void Comparer_QueueEnqueueDequeue_ComparesByContent()
        {
            var original = new Model();
            original.Pending.Enqueue(1);
            var edited = ObjectCloner.DeepCopy(original);

            edited.Pending.Enqueue(2);
            edited.Pending.Dequeue();      // now [2], original is [1]
            Assert.False(ObjectComparer.DeepEquals(original, edited));

            edited.Pending.Clear();
            edited.Pending.Enqueue(1);     // same content, different internal head/version
            Assert.True(ObjectComparer.DeepEquals(original, edited));
        }

        #endregion

        #region ModelValidator

        private class Account : IValidatableObject
        {
            [Required]
            public string Name { get; set; }

            public int Min { get; set; }
            public int Max { get; set; }

            public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
            {
                if (Min > Max) yield return new ValidationResult("Min must not exceed Max.");
            }
        }

        private sealed class PremiumAccount : Account
        {
            [Required]
            public string Tier { get; set; }
        }

        [Fact]
        public void IsValid_AgreesWithValidateAll_ForIValidatableObject()
        {
            var model = new Account { Name = "a", Min = 5, Max = 1 };
            var validator = ModelValidator.For<Account>();

            Assert.NotEmpty(validator.ValidateAll(model));
            Assert.False(validator.IsValid(model));
        }

        [Fact]
        public void IsValid_SeesAttributesOfDerivedType()
        {
            Account model = new PremiumAccount { Name = "a", Tier = null };
            var validator = ModelValidator.For<Account>();

            Assert.NotEmpty(validator.ValidateAll(model));
            Assert.False(validator.IsValid(model));
        }

        #endregion
    }

    internal sealed class ExtensionReceiver
    {
        public int Received;
    }

    internal static class ExtensionReceiverExtensions
    {
        public static void Bump(this ExtensionReceiver receiver, string message) => receiver.Received++;
    }
}
