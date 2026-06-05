using System;
using System.Windows.Forms;
using WinformsMVP.Common;
using WinformsMVP.MVP.Views;
using WinformsMVP.MVP.Presenters;

namespace WinformsMVP.Services
{
    /// <summary>
    /// Intermediate context built by <see cref="WindowNavigatorFluentExtensions.For{TPresenter}"/>
    /// that captures the presenter type so subsequent steps in the Fluent API only need to
    /// specify the result type explicitly.
    /// </summary>
    /// <remarks>
    /// <para>
    /// C# does not support partial generic type inference: with the 3-arg form
    /// <c>Navigator.ShowWindowAsModal&lt;TPresenter, TParam, TResult&gt;(...)</c> the caller must
    /// spell out every type argument. The Fluent API splits that into chained steps so each
    /// step can infer its own type parameter, leaving only <c>TResult</c> for the caller to
    /// specify explicitly:
    /// </para>
    /// <code>
    /// var result = Navigator.For(presenter)        // TPresenter inferred
    ///                       .WithParam(parameters) // TParam inferred
    ///                       .ShowAsModal&lt;bool&gt;(); // only TResult explicit
    /// </code>
    /// <para>
    /// This is purely syntactic sugar over <see cref="IWindowNavigator"/>; every method here
    /// delegates to the corresponding instance method on the navigator. The existing 3-arg
    /// API remains fully supported.
    /// </para>
    /// </remarks>
    public sealed class NavigationContext<TPresenter> where TPresenter : IPresenter
    {
        internal readonly IWindowNavigator Nav;
        internal readonly TPresenter Presenter;

        internal NavigationContext(IWindowNavigator nav, TPresenter presenter)
        {
            Nav = nav;
            Presenter = presenter;
        }

        /// <summary>
        /// Shows the presenter as a modal window with no business result.
        /// </summary>
        public InteractionResult ShowAsModal(IWin32Window owner = null)
            => Nav.ShowWindowAsModal<TPresenter>(Presenter, owner);

        /// <summary>
        /// Shows the presenter as a modal window and waits for a typed business result
        /// the presenter pushes via <c>RequestClose(result, status)</c>.
        /// </summary>
        public InteractionResult<TResult> ShowAsModal<TResult>(IWin32Window owner = null)
            => Nav.ShowWindowAsModal<TPresenter, TResult>(Presenter, owner);

        /// <summary>
        /// Shows the presenter as a non-modal window.
        /// </summary>
        /// <param name="keySelector">Optional singleton key. When non-null and a window with
        /// the same key is already open, that window is activated instead of creating a new
        /// one. The lambda receives the strongly-typed presenter, so presenter properties can
        /// be accessed without casts.</param>
        public IWindowView ShowWindow(
            IWin32Window owner = null,
            Func<TPresenter, object> keySelector = null)
            => Nav.ShowWindow<TPresenter>(Presenter, owner, keySelector);

        /// <summary>
        /// Shows the presenter as a non-modal window and invokes <paramref name="onClosed"/>
        /// with the final result after the window closes.
        /// </summary>
        public IWindowView ShowWindow<TResult>(
            IWin32Window owner = null,
            Func<TPresenter, object> keySelector = null,
            Action<InteractionResult<TResult>> onClosed = null)
            => Nav.ShowWindow<TPresenter, TResult>(Presenter, owner, keySelector, onClosed);
    }

    /// <summary>
    /// Intermediate context built by
    /// <see cref="WindowNavigatorFluentExtensions.WithParam{TPresenter, TParam}"/> that
    /// carries both the presenter and its initialization parameters.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The class-level constraint <c>where TPresenter : IInitializable&lt;TParam&gt;</c>
    /// is enforced at the call site of <c>WithParam</c>: passing a presenter that does not
    /// implement <see cref="IInitializable{TParam}"/> for the inferred <c>TParam</c> fails
    /// to compile, giving the Fluent API the same compile-time safety as the 3-arg
    /// instance methods on <see cref="IWindowNavigator"/>.
    /// </para>
    /// </remarks>
    public sealed class NavigationContext<TPresenter, TParam>
        where TPresenter : IPresenter, IInitializable<TParam>
    {
        internal readonly IWindowNavigator Nav;
        internal readonly TPresenter Presenter;
        internal readonly TParam Parameters;

        internal NavigationContext(NavigationContext<TPresenter> baseCtx, TParam parameters)
        {
            Nav = baseCtx.Nav;
            Presenter = baseCtx.Presenter;
            Parameters = parameters;
        }

        /// <summary>
        /// Shows the presenter as a modal window with the captured parameters and no
        /// business result.
        /// </summary>
        public InteractionResult ShowAsModal(IWin32Window owner = null)
            => Nav.ShowWindowAsModal<TPresenter, TParam>(Presenter, Parameters, owner);

        /// <summary>
        /// Shows the presenter as a modal window with the captured parameters and waits
        /// for a typed business result.
        /// </summary>
        public InteractionResult<TResult> ShowAsModal<TResult>(IWin32Window owner = null)
            => Nav.ShowWindowAsModal<TPresenter, TParam, TResult>(Presenter, Parameters, owner);

        /// <summary>
        /// Shows the presenter as a non-modal window with the captured parameters.
        /// </summary>
        public IWindowView ShowWindow(
            IWin32Window owner = null,
            Func<TPresenter, object> keySelector = null)
            => Nav.ShowWindow<TPresenter, TParam>(Presenter, Parameters, owner, keySelector);

        /// <summary>
        /// Shows the presenter as a non-modal window with the captured parameters and
        /// invokes <paramref name="onClosed"/> with the final result after the window closes.
        /// </summary>
        public IWindowView ShowWindow<TResult>(
            IWin32Window owner = null,
            Func<TPresenter, object> keySelector = null,
            Action<InteractionResult<TResult>> onClosed = null)
            => Nav.ShowWindow<TPresenter, TParam, TResult>(Presenter, Parameters, owner, keySelector, onClosed);
    }
}
