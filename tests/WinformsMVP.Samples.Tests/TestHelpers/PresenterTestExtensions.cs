using WinformsMVP.Core.Presenters;
using WinformsMVP.Core.Views;
using WinformsMVP.MVP.ViewActions;

namespace WinformsMVP.Samples.Tests.TestHelpers
{
    /// <summary>
    /// Extension methods to simplify presenter testing.
    /// </summary>
    public static class PresenterTestExtensions
    {
        /// <summary>
        /// Dispatch an action in the presenter (for testing).
        /// Accesses the protected _dispatcher field via reflection.
        /// </summary>
        public static void Dispatch<TView>(this PresenterBase<TView> presenter, ViewAction action)
            where TView : class, IViewBase
        {
            // Use reflection to access the protected _dispatcher field
            var dispatcherField = typeof(PresenterBase<TView>)
                .GetField("_dispatcher", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            if (dispatcherField != null)
            {
                var dispatcher = dispatcherField.GetValue(presenter) as ViewActionDispatcher;
                dispatcher?.Dispatch(action);
            }
        }

        /// <summary>
        /// Dispatch an action with a parameter (for testing).
        /// </summary>
        public static void Dispatch<TView, TParam>(this PresenterBase<TView> presenter, ViewAction action, TParam parameter)
            where TView : class, IViewBase
        {
            var dispatcherField = typeof(PresenterBase<TView>)
                .GetField("_dispatcher", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            if (dispatcherField != null)
            {
                var dispatcher = dispatcherField.GetValue(presenter) as ViewActionDispatcher;
                dispatcher?.Dispatch(action, parameter);
            }
        }
    }
}
