using WinformsMVP.MVP.Presenters;

namespace WinformsMVP.Services
{
    /// <summary>
    /// Fluent API entry points for <see cref="IWindowNavigator"/> that let callers omit the
    /// <c>TPresenter</c> generic argument at the call site while preserving full compile-time
    /// type safety.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The existing instance methods on <see cref="IWindowNavigator"/> require three explicit
    /// type arguments because C# does not support partial generic type inference:
    /// </para>
    /// <code>
    /// // 3-arg API — every type argument must be spelled out
    /// var result = Navigator.ShowWindowAsModal&lt;ComposeEmailPresenter, ComposeEmailParameters, bool&gt;(
    ///     presenter, parameters);
    /// </code>
    /// <para>
    /// The Fluent API splits the call into chained steps where each step infers its own type
    /// parameter from its argument, leaving only the business result type for the caller to
    /// specify explicitly:
    /// </para>
    /// <code>
    /// // Fluent API — TPresenter and TParam inferred; only TResult is explicit
    /// var result = Navigator.For(presenter)
    ///                       .WithParam(parameters)
    ///                       .ShowAsModal&lt;bool&gt;();
    /// </code>
    /// <para>
    /// Both styles delegate to the same underlying instance methods, so existing call sites,
    /// mock navigators, and tests continue to work without modification. Use whichever
    /// style reads better for your call.
    /// </para>
    /// </remarks>
    public static class WindowNavigatorFluentExtensions
    {
        /// <summary>
        /// Starts a Fluent navigation chain by capturing the presenter's static type. The
        /// returned context lets you call <c>.WithParam(...)</c>, <c>.ShowAsModal&lt;...&gt;()</c>,
        /// or <c>.ShowWindow(...)</c> without re-stating the presenter type.
        /// </summary>
        public static NavigationContext<TPresenter> For<TPresenter>(
            this IWindowNavigator nav,
            TPresenter presenter)
            where TPresenter : IPresenter
            => new NavigationContext<TPresenter>(nav, presenter);

        /// <summary>
        /// Attaches initialization parameters to the navigation chain. The stricter constraint
        /// <c>where TPresenter : IInitializable&lt;TParam&gt;</c> applied here is what gives
        /// the Fluent API its compile-time safety — passing parameters of a type the presenter
        /// cannot consume fails to compile.
        /// </summary>
        public static NavigationContext<TPresenter, TParam> WithParam<TPresenter, TParam>(
            this NavigationContext<TPresenter> ctx,
            TParam parameters)
            where TPresenter : IPresenter, IInitializable<TParam>
            => new NavigationContext<TPresenter, TParam>(ctx, parameters);
    }
}
