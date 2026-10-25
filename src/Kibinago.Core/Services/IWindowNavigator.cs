using System;
using Kibinago.Common;
using Kibinago.Views;
using Kibinago.Presenters;

namespace Kibinago.Services
{
    /// <summary>
    /// Opens presenter windows: creates the Form registered for the presenter's view interface,
    /// attaches and initializes the presenter, and shows the window modally or non-modally. Prefer
    /// the fluent form, <c>Navigator.For(presenter)[.WithParam(p)].ShowAsModal&lt;TResult&gt;()</c>
    /// (<see cref="WindowNavigatorFluentExtensions"/>), which infers the presenter and parameter types.
    /// </summary>
    /// <remarks>
    /// A presenter that cancels the opening (<c>RequestClose</c> during initialization) is never
    /// shown. A presenter that only implements <see cref="IInitializable{TParam}"/> passed to a
    /// parameterless overload throws <see cref="InvalidOperationException"/>.
    /// </remarks>
    public interface IWindowNavigator
    {
        // ==================================================
        // Non-modal window methods (without parameters)
        // ==================================================

        /// <summary>Shows the presenter's window non-modally and returns at once; <paramref name="onClosed"/> receives the result.</summary>
        /// <typeparam name="TPresenter">The presenter type.</typeparam>
        /// <typeparam name="TResult">The type of the result the presenter passes to <c>RequestClose</c>.</typeparam>
        /// <param name="presenter">A new presenter, not yet attached; it is disposed when the window closes.</param>
        /// <param name="owner">The owner window, or <c>null</c> for none.</param>
        /// <param name="keySelector">Optional key: when a window of the same presenter type with the same key
        /// is already open, that window is activated and <paramref name="presenter"/> is disposed.</param>
        /// <param name="onClosed">Called with the result after the window closes; not called when an existing
        /// window was activated instead.</param>
        /// <returns>The window's view; the already open window when <paramref name="keySelector"/> matched one;
        /// <c>null</c> when the presenter cancelled the opening.</returns>
        IWindowView ShowWindow<TPresenter, TResult>(
            TPresenter presenter,
            IWindowView owner = null,
            Func<TPresenter, object> keySelector = null,
            Action<InteractionResult<TResult>> onClosed = null)
            where TPresenter : IPresenter;

        /// <summary>Shows the presenter's window non-modally and returns at once.</summary>
        /// <typeparam name="TPresenter">The presenter type.</typeparam>
        /// <param name="presenter">A new presenter, not yet attached; it is disposed when the window closes.</param>
        /// <param name="owner">The owner window, or <c>null</c> for none.</param>
        /// <param name="keySelector">Optional key: when a window of the same presenter type with the same key
        /// is already open, that window is activated and <paramref name="presenter"/> is disposed.</param>
        /// <returns>The window's view; the already open window when <paramref name="keySelector"/> matched one;
        /// <c>null</c> when the presenter cancelled the opening.</returns>
        IWindowView ShowWindow<TPresenter>(
            TPresenter presenter,
            IWindowView owner = null,
            Func<TPresenter, object> keySelector = null)
            where TPresenter : IPresenter;

        // ==================================================
        // Modal window methods (without parameters)
        // ==================================================

        /// <summary>Shows the presenter's window modally and returns when it closes.</summary>
        /// <typeparam name="TPresenter">The presenter type.</typeparam>
        /// <param name="presenter">A new presenter, not yet attached; it is disposed when the window closes.</param>
        /// <param name="owner">The owner window, or <c>null</c> for none.</param>
        /// <returns>How the window was closed (the status the presenter passed to <c>RequestClose</c>, or <see cref="InteractionStatus.Cancel"/>).</returns>
        InteractionResult ShowWindowAsModal<TPresenter>(
            TPresenter presenter,
            IWindowView owner = null)
            where TPresenter : IPresenter;

        /// <summary>Shows the presenter's window modally and returns its result when it closes.</summary>
        /// <typeparam name="TPresenter">The presenter type.</typeparam>
        /// <typeparam name="TResult">The type of the result the presenter passes to <c>RequestClose</c>.</typeparam>
        /// <param name="presenter">A new presenter, not yet attached; it is disposed when the window closes.</param>
        /// <param name="owner">The owner window, or <c>null</c> for none.</param>
        /// <returns>The result the presenter passed to <c>RequestClose</c>; <see cref="InteractionStatus.Cancel"/>
        /// when the window was closed without one.</returns>
        InteractionResult<TResult> ShowWindowAsModal<TPresenter, TResult>(
            TPresenter presenter,
            IWindowView owner = null)
            where TPresenter : IPresenter;

        // ==================================================
        // Non-modal window methods (with parameters)
        // ==================================================

        /// <summary>Shows the presenter's window non-modally and returns at once; <paramref name="onClosed"/> receives the result.</summary>
        /// <typeparam name="TPresenter">The presenter type.</typeparam>
        /// <typeparam name="TParam">The presenter's initialization parameter type.</typeparam>
        /// <typeparam name="TResult">The type of the result the presenter passes to <c>RequestClose</c>.</typeparam>
        /// <param name="presenter">A new presenter, not yet attached; it is disposed when the window closes.</param>
        /// <param name="parameters">Passed to the presenter's <c>Initialize(TParam)</c>.</param>
        /// <param name="owner">The owner window, or <c>null</c> for none.</param>
        /// <param name="keySelector">Optional key: when a window of the same presenter type with the same key
        /// is already open, that window is activated and <paramref name="presenter"/> is disposed.</param>
        /// <param name="onClosed">Called with the result after the window closes; not called when an existing
        /// window was activated instead.</param>
        /// <returns>The window's view; the already open window when <paramref name="keySelector"/> matched one;
        /// <c>null</c> when the presenter cancelled the opening.</returns>
        IWindowView ShowWindow<TPresenter, TParam, TResult>(
            TPresenter presenter,
            TParam parameters,
            IWindowView owner = null,
            Func<TPresenter, object> keySelector = null,
            Action<InteractionResult<TResult>> onClosed = null)
            where TPresenter : IPresenter, IInitializable<TParam>;

        /// <summary>Shows the presenter's window non-modally and returns at once.</summary>
        /// <typeparam name="TPresenter">The presenter type.</typeparam>
        /// <typeparam name="TParam">The presenter's initialization parameter type.</typeparam>
        /// <param name="presenter">A new presenter, not yet attached; it is disposed when the window closes.</param>
        /// <param name="parameters">Passed to the presenter's <c>Initialize(TParam)</c>.</param>
        /// <param name="owner">The owner window, or <c>null</c> for none.</param>
        /// <param name="keySelector">Optional key: when a window of the same presenter type with the same key
        /// is already open, that window is activated and <paramref name="presenter"/> is disposed.</param>
        /// <returns>The window's view; the already open window when <paramref name="keySelector"/> matched one;
        /// <c>null</c> when the presenter cancelled the opening.</returns>
        IWindowView ShowWindow<TPresenter, TParam>(
            TPresenter presenter,
            TParam parameters,
            IWindowView owner = null,
            Func<TPresenter, object> keySelector = null)
            where TPresenter : IPresenter, IInitializable<TParam>;

        // ==================================================
        // Modal window methods (with parameters)
        // ==================================================

        /// <summary>Shows the presenter's window modally and returns when it closes.</summary>
        /// <typeparam name="TPresenter">The presenter type.</typeparam>
        /// <typeparam name="TParam">The presenter's initialization parameter type.</typeparam>
        /// <param name="presenter">A new presenter, not yet attached; it is disposed when the window closes.</param>
        /// <param name="parameters">Passed to the presenter's <c>Initialize(TParam)</c>.</param>
        /// <param name="owner">The owner window, or <c>null</c> for none.</param>
        /// <returns>How the window was closed (the status the presenter passed to <c>RequestClose</c>, or <see cref="InteractionStatus.Cancel"/>).</returns>
        InteractionResult ShowWindowAsModal<TPresenter, TParam>(
            TPresenter presenter,
            TParam parameters,
            IWindowView owner = null)
            where TPresenter : IPresenter, IInitializable<TParam>;

        /// <summary>Shows the presenter's window modally and returns its result when it closes.</summary>
        /// <typeparam name="TPresenter">The presenter type.</typeparam>
        /// <typeparam name="TParam">The presenter's initialization parameter type.</typeparam>
        /// <typeparam name="TResult">The type of the result the presenter passes to <c>RequestClose</c>.</typeparam>
        /// <param name="presenter">A new presenter, not yet attached; it is disposed when the window closes.</param>
        /// <param name="parameters">Passed to the presenter's <c>Initialize(TParam)</c>.</param>
        /// <param name="owner">The owner window, or <c>null</c> for none.</param>
        /// <returns>The result the presenter passed to <c>RequestClose</c>; <see cref="InteractionStatus.Cancel"/>
        /// when the window was closed without one.</returns>
        InteractionResult<TResult> ShowWindowAsModal<TPresenter, TParam, TResult>(
            TPresenter presenter,
            TParam parameters,
            IWindowView owner = null)
            where TPresenter : IPresenter, IInitializable<TParam>;
    }
}