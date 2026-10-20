using System;
using System.Threading.Tasks;
using WinformsMVP.MVP.ViewActions;
using Xunit;

namespace WinformsMVP.Samples.Tests.ViewActions
{
    /// <summary>
    /// ViewAction&lt;T&gt; carries its payload type, so the Form side (Binder.Add) and the presenter
    /// side (Dispatcher.Register) are checked against the same T by the compiler.
    /// </summary>
    public class TypedViewActionTests
    {
        private static readonly ViewAction<int> Zoom = ViewAction.Create<int>("Typed.Zoom");

        [Fact]
        public void Create_KeepsNameAndUntypedKey()
        {
            Assert.Equal("Typed.Zoom", Zoom.Name);
            Assert.Equal(ViewAction.Create("Typed.Zoom"), Zoom.Key);
        }

        [Fact]
        public void Factory_CreateTyped_AppliesQualifier()
        {
            var key = ViewAction.Factory.WithQualifier("Orders").Create<decimal>("Discount");

            Assert.Equal("Orders.Discount", key.Name);
        }

        [Fact]
        public void Equality_IsByName()
        {
            Assert.Equal(ViewAction.Create<int>("A"), ViewAction.Create<int>("A"));
            Assert.NotEqual(ViewAction.Create<int>("A"), ViewAction.Create<int>("B"));
            Assert.True(ViewAction.Create<int>("A") == ViewAction.Create<int>("A"));
        }

        [Fact]
        public void RegisterAndDispatch_Typed_DeliversThePayload()
        {
            var dispatcher = new ViewActionDispatcher();
            int received = 0;
            dispatcher.Register(Zoom, z => received = z);

            dispatcher.Dispatch(Zoom, 150);

            Assert.Equal(150, received);
        }

        [Fact]
        public async Task RegisterAsyncAndDispatchAsync_Typed_DeliversThePayload()
        {
            var dispatcher = new ViewActionDispatcher();
            int received = 0;
            dispatcher.Register(Zoom, async (int z) => { await Task.Yield(); received = z; });

            await dispatcher.DispatchAsync(Zoom, 75);

            Assert.Equal(75, received);
        }

        [Fact]
        public async Task RegisterAsyncWithToken_Typed_DeliversThePayload()
        {
            var dispatcher = new ViewActionDispatcher();
            int received = 0;
            dispatcher.Register(Zoom, async (int z, System.Threading.CancellationToken ct) => { await Task.Yield(); received = z; });

            await dispatcher.DispatchAsync(Zoom, 20);

            Assert.Equal(20, received);
        }

        [Fact]
        public void CanDispatch_Typed_FollowsCanExecute()
        {
            var dispatcher = new ViewActionDispatcher();
            bool allowed = false;
            dispatcher.Register(Zoom, z => { }, canExecute: () => allowed);

            Assert.False(dispatcher.CanDispatch(Zoom));
            allowed = true;
            Assert.True(dispatcher.CanDispatch(Zoom));
        }

        [Fact]
        public void TypedAndUntypedKeys_ReachTheSameHandler()
        {
            // Middleware, logs and explicit-pattern code see the untyped key.
            var dispatcher = new ViewActionDispatcher();
            int received = 0;
            dispatcher.Register(Zoom, z => received = z);

            dispatcher.Dispatch(Zoom.Key, 42);

            Assert.Equal(42, received);
        }

        [Fact]
        public void Register_DefaultTypedKey_IsRejected()
        {
            var dispatcher = new ViewActionDispatcher();

            Assert.Throws<ArgumentException>(() => dispatcher.Register(default(ViewAction<int>), z => { }));
        }
    }
}
