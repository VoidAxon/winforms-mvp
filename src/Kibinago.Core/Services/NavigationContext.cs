using System;
using Kibinago.Common;
using Kibinago.Views;
using Kibinago.Presenters;

namespace Kibinago.Services
{
    /// <summary>
    /// Built by <c>Navigator.For(presenter)</c> for a presenter that initializes without parameters;
    /// captures the presenter type so only the result type is written at the call site.
    /// </summary>
    /// <remarks>
    /// C# does not infer part of a method's type arguments: the direct form
    /// <c>Navigator.ShowWindowAsModal&lt;TPresenter, TResult&gt;(...)</c> needs every one. The fluent
    /// steps each infer their own, so only <c>TResult</c> is explicit:
    /// <code>
    /// var result = Navigator.For(presenter).ShowAsModal&lt;bool&gt;();
    /// </code>
    /// Every method delegates to the matching <see cref="IWindowNavigator"/> method. A presenter that
    /// takes parameters gets a <see cref="ParamRequiredNavigation{TPresenter}"/> from <c>For</c> instead,
    /// so showing it without <c>WithParam</c> fails to compile.
    /// </remarks>
    /// <typeparam name="TPresenter">The presenter type.</typeparam>
    public sealed class NavigationContext<TPresenter> where TPresenter : IWindowPresenter, IInitializable
    {
        private readonly IWindowNavigator _nav;
        private readonly TPresenter _presenter;

        internal NavigationContext(IWindowNavigator nav, TPresenter presenter)
        {
            _nav = nav;
            _presenter = presenter;
        }

        /// <summary>Shows the window modally and returns when it closes.</summary>
        /// <param name="owner">The owner: a window's view, or the view of a panel in a window (that window owns the new one); <c>null</c> for none.</param>
        /// <returns>How the window was closed.</returns>
        public InteractionResult ShowAsModal(IView owner = null)
            => _nav.ShowWindowAsModal<TPresenter>(_presenter, owner);

        /// <summary>Shows the window modally and returns its result when it closes.</summary>
        /// <typeparam name="TResult">The type of the result the presenter passes to <c>RequestClose</c>.</typeparam>
        /// <param name="owner">The owner: a window's view, or the view of a panel in a window (that window owns the new one); <c>null</c> for none.</param>
        /// <returns>The result the presenter passed to <c>RequestClose</c>, or a cancel result.</returns>
        public InteractionResult<TResult> ShowAsModal<TResult>(IView owner = null)
            => _nav.ShowWindowAsModal<TPresenter, TResult>(_presenter, owner);

        /// <summary>Shows the window non-modally and returns at once.</summary>
        /// <param name="owner">The owner: a window's view, or the view of a panel in a window (that window owns the new one); <c>null</c> for none.</param>
        /// <param name="key">Optional key: when a window of the same presenter type with the same key is
        /// already open, that window is activated and the presenter is disposed.</param>
        /// <returns>The window's view; the already open window for a matching key; <c>null</c> when the
        /// presenter cancelled the opening.</returns>
        public IWindowView ShowWindow(IView owner = null, object key = null)
            => _nav.ShowWindow<TPresenter>(_presenter, owner, key);

        /// <summary>Shows the window non-modally and returns at once; <paramref name="onClosed"/> receives the result.</summary>
        /// <typeparam name="TResult">The type of the result the presenter passes to <c>RequestClose</c>.</typeparam>
        /// <param name="owner">The owner: a window's view, or the view of a panel in a window (that window owns the new one); <c>null</c> for none.</param>
        /// <param name="key">Optional key: when a window of the same presenter type with the same key is
        /// already open, that window is activated and the presenter is disposed.</param>
        /// <param name="onClosed">Called with the result after the window closes; not called when an
        /// existing window was activated instead.</param>
        /// <returns>The window's view; the already open window for a matching key; <c>null</c> when the
        /// presenter cancelled the opening.</returns>
        public IWindowView ShowWindow<TResult>(
            IView owner = null,
            object key = null,
            Action<InteractionResult<TResult>> onClosed = null)
            => _nav.ShowWindow<TPresenter, TResult>(_presenter, owner, key, onClosed);

        /// <summary>Prepares the window without showing it; the caller places and shows the returned view (see <see cref="IWindowNavigator.PrepareWindow{TPresenter}(TPresenter, object)"/>).</summary>
        /// <param name="key">Optional key: an open window of the same presenter type with this key is returned instead (not activated).</param>
        /// <returns>The prepared view; the open window's view for a matching key; <c>null</c> when the presenter cancelled the opening.</returns>
        public IWindowView PrepareWindow(object key = null)
            => _nav.PrepareWindow<TPresenter>(_presenter, key);

        /// <summary>Prepares the window without showing it; <paramref name="onClosed"/> receives the result once it has ended.</summary>
        /// <typeparam name="TResult">The type of the result the presenter passes to <c>RequestClose</c>.</typeparam>
        /// <param name="key">Optional key: an open window of the same presenter type with this key is returned instead (not activated).</param>
        /// <param name="onClosed">Called with the result after the window ended.</param>
        /// <returns>The prepared view; the open window's view for a matching key; <c>null</c> when the presenter cancelled the opening.</returns>
        public IWindowView PrepareWindow<TResult>(object key = null, Action<InteractionResult<TResult>> onClosed = null)
            => _nav.PrepareWindow<TPresenter, TResult>(_presenter, key, onClosed);
    }

    /// <summary>
    /// Built by <c>Navigator.For(presenter)</c> for a presenter that takes parameters
    /// (<see cref="IInitializable{TParam}"/>). It can only be continued with <c>.WithParam(...)</c>,
    /// so opening such a presenter without its parameters fails to compile.
    /// </summary>
    /// <typeparam name="TPresenter">The presenter type.</typeparam>
    public sealed class ParamRequiredNavigation<TPresenter> where TPresenter : IWindowPresenter, IInitializableWithParam
    {
        internal readonly IWindowNavigator Nav;
        internal readonly TPresenter Presenter;

        internal ParamRequiredNavigation(IWindowNavigator nav, TPresenter presenter)
        {
            Nav = nav;
            Presenter = presenter;
        }
    }

    /// <summary>
    /// Built by <c>Navigator.For(presenter).WithParam(parameters)</c>; only the result type is written
    /// at the call site. Every method delegates to the matching <see cref="IWindowNavigator"/> method.
    /// </summary>
    /// <typeparam name="TPresenter">The presenter type.</typeparam>
    /// <typeparam name="TParam">The presenter's initialization parameter type.</typeparam>
    public sealed class NavigationContext<TPresenter, TParam>
        where TPresenter : IWindowPresenter, IInitializable<TParam>
    {
        private readonly IWindowNavigator _nav;
        private readonly TPresenter _presenter;
        private readonly TParam _parameters;

        internal NavigationContext(IWindowNavigator nav, TPresenter presenter, TParam parameters)
        {
            _nav = nav;
            _presenter = presenter;
            _parameters = parameters;
        }

        /// <summary>Shows the window modally and returns when it closes.</summary>
        /// <param name="owner">The owner: a window's view, or the view of a panel in a window (that window owns the new one); <c>null</c> for none.</param>
        /// <returns>How the window was closed.</returns>
        public InteractionResult ShowAsModal(IView owner = null)
            => _nav.ShowWindowAsModal<TPresenter, TParam>(_presenter, _parameters, owner);

        /// <summary>Shows the window modally and returns its result when it closes.</summary>
        /// <typeparam name="TResult">The type of the result the presenter passes to <c>RequestClose</c>.</typeparam>
        /// <param name="owner">The owner: a window's view, or the view of a panel in a window (that window owns the new one); <c>null</c> for none.</param>
        /// <returns>The result the presenter passed to <c>RequestClose</c>, or a cancel result.</returns>
        public InteractionResult<TResult> ShowAsModal<TResult>(IView owner = null)
            => _nav.ShowWindowAsModal<TPresenter, TParam, TResult>(_presenter, _parameters, owner);

        /// <summary>Shows the window non-modally and returns at once.</summary>
        /// <param name="owner">The owner: a window's view, or the view of a panel in a window (that window owns the new one); <c>null</c> for none.</param>
        /// <param name="key">Optional key: when a window of the same presenter type with the same key is
        /// already open, that window is activated and the presenter is disposed.</param>
        /// <returns>The window's view; the already open window for a matching key; <c>null</c> when the
        /// presenter cancelled the opening.</returns>
        public IWindowView ShowWindow(IView owner = null, object key = null)
            => _nav.ShowWindow<TPresenter, TParam>(_presenter, _parameters, owner, key);

        /// <summary>Shows the window non-modally and returns at once; <paramref name="onClosed"/> receives the result.</summary>
        /// <typeparam name="TResult">The type of the result the presenter passes to <c>RequestClose</c>.</typeparam>
        /// <param name="owner">The owner: a window's view, or the view of a panel in a window (that window owns the new one); <c>null</c> for none.</param>
        /// <param name="key">Optional key: when a window of the same presenter type with the same key is
        /// already open, that window is activated and the presenter is disposed.</param>
        /// <param name="onClosed">Called with the result after the window closes; not called when an
        /// existing window was activated instead.</param>
        /// <returns>The window's view; the already open window for a matching key; <c>null</c> when the
        /// presenter cancelled the opening.</returns>
        public IWindowView ShowWindow<TResult>(
            IView owner = null,
            object key = null,
            Action<InteractionResult<TResult>> onClosed = null)
            => _nav.ShowWindow<TPresenter, TParam, TResult>(_presenter, _parameters, owner, key, onClosed);

        /// <summary>Prepares the window without showing it; the caller places and shows the returned view (see <see cref="IWindowNavigator.PrepareWindow{TPresenter, TParam}(TPresenter, TParam, object)"/>).</summary>
        /// <param name="key">Optional key: an open window of the same presenter type with this key is returned instead (not activated).</param>
        /// <returns>The prepared view; the open window's view for a matching key; <c>null</c> when the presenter cancelled the opening.</returns>
        public IWindowView PrepareWindow(object key = null)
            => _nav.PrepareWindow<TPresenter, TParam>(_presenter, _parameters, key);

        /// <summary>Prepares the window without showing it; <paramref name="onClosed"/> receives the result once it has ended.</summary>
        /// <typeparam name="TResult">The type of the result the presenter passes to <c>RequestClose</c>.</typeparam>
        /// <param name="key">Optional key: an open window of the same presenter type with this key is returned instead (not activated).</param>
        /// <param name="onClosed">Called with the result after the window ended.</param>
        /// <returns>The prepared view; the open window's view for a matching key; <c>null</c> when the presenter cancelled the opening.</returns>
        public IWindowView PrepareWindow<TResult>(object key = null, Action<InteractionResult<TResult>> onClosed = null)
            => _nav.PrepareWindow<TPresenter, TParam, TResult>(_presenter, _parameters, key, onClosed);
    }
}
