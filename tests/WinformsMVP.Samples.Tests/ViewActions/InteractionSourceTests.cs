using System.ComponentModel;
using System.Windows.Forms;
using WinformsMVP.MVP.ViewActions;
using Xunit;

namespace WinformsMVP.Samples.Tests.ViewActions
{
    /// <summary>
    /// Verifies that the binder exposes the triggering component as an ambient fact for the
    /// duration of the synchronous dispatch, and restores it afterwards.
    /// </summary>
    public class InteractionSourceTests
    {
        private static readonly ViewAction TestAction =
            ViewAction.Factory.WithQualifier("InteractionSourceTests").Create("Do");

        [Fact]
        public void Dispatch_ExposesTriggeringControl_AndRestoresAfterwards()
        {
            using (var button = new Button())
            {
                var dispatcher = new ViewActionDispatcher();
                Component observed = null;
                dispatcher.Register(TestAction, () => observed = InteractionSource.Current);

                var binder = new ViewActionBinder();
                binder.Add(TestAction, button);
                binder.Bind(dispatcher);

                button.PerformClick();   // raises Click synchronously -> binder handler -> dispatch

                Assert.Same(button, observed);          // visible during the handler
                Assert.Null(InteractionSource.Current); // restored once the dispatch returned
            }
        }
    }
}
