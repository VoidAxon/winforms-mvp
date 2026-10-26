using Kibinago.Presenters;

namespace Kibinago.Services
{
    /// <summary>
    /// Fluent entry point for a presenter that initializes without parameters:
    /// <c>Navigator.For(presenter).ShowAsModal&lt;TResult&gt;()</c>. The presenter type is inferred,
    /// so only the result type is written. A presenter that takes parameters goes through
    /// <see cref="WindowNavigatorParamFluentExtensions"/> instead.
    /// </summary>
    /// <remarks>
    /// <c>For</c> exists twice, once per class, with different constraints; C# drops the overload whose
    /// constraints the presenter does not meet, so the right one is picked and a control presenter
    /// matches neither (compile error).
    /// </remarks>
    public static class WindowNavigatorFluentExtensions
    {
        /// <summary>Starts a fluent navigation for a presenter that needs no parameters.</summary>
        /// <typeparam name="TPresenter">The presenter type, inferred.</typeparam>
        /// <param name="nav">The navigator.</param>
        /// <param name="presenter">A new presenter, not yet attached; it is disposed when the window closes.</param>
        /// <returns>A context that shows the window: <c>.ShowAsModal(...)</c> / <c>.ShowWindow(...)</c>.</returns>
        public static NavigationContext<TPresenter> For<TPresenter>(
            this IWindowNavigator nav,
            TPresenter presenter)
            where TPresenter : IWindowPresenter, IInitializable
            => new NavigationContext<TPresenter>(nav, presenter);
    }

    /// <summary>
    /// Fluent entry point for a presenter that takes parameters:
    /// <c>Navigator.For(presenter).WithParam(parameters).ShowAsModal&lt;TResult&gt;()</c>. The presenter
    /// and parameter types are inferred, and <c>WithParam</c> is required before anything is shown.
    /// </summary>
    public static class WindowNavigatorParamFluentExtensions
    {
        /// <summary>Starts a fluent navigation for a presenter that takes parameters; continue with <c>.WithParam(...)</c>.</summary>
        /// <typeparam name="TPresenter">The presenter type, inferred.</typeparam>
        /// <param name="nav">The navigator.</param>
        /// <param name="presenter">A new presenter, not yet attached; it is disposed when the window closes.</param>
        /// <returns>A context whose only step is <c>.WithParam(...)</c>.</returns>
        public static ParamRequiredNavigation<TPresenter> For<TPresenter>(
            this IWindowNavigator nav,
            TPresenter presenter)
            where TPresenter : IWindowPresenter, IInitializableWithParam
            => new ParamRequiredNavigation<TPresenter>(nav, presenter);

        /// <summary>
        /// Supplies the presenter's initialization parameters. The constraint
        /// <c>TPresenter : IInitializable&lt;TParam&gt;</c> makes parameters of a type the presenter does not
        /// take fail to compile.
        /// </summary>
        /// <typeparam name="TPresenter">The presenter type, inferred.</typeparam>
        /// <typeparam name="TParam">The parameter type, inferred.</typeparam>
        /// <param name="ctx">The context from <c>For</c>.</param>
        /// <param name="parameters">Passed to the presenter's <c>Initialize(TParam)</c>.</param>
        /// <returns>A context that shows the window.</returns>
        public static NavigationContext<TPresenter, TParam> WithParam<TPresenter, TParam>(
            this ParamRequiredNavigation<TPresenter> ctx,
            TParam parameters)
            where TPresenter : IWindowPresenter, IInitializable<TParam>
            => new NavigationContext<TPresenter, TParam>(ctx.Nav, ctx.Presenter, parameters);
    }
}
