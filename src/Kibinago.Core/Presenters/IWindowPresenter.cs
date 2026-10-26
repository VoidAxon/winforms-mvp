namespace Kibinago.Presenters
{
    /// <summary>
    /// A presenter that drives a window (<see cref="WindowPresenterBase{TView}"/> /
    /// <see cref="WindowPresenterBase{TView, TParam}"/>). <see cref="Services.IWindowNavigator"/> accepts
    /// only these, so passing a control presenter fails to compile. Has no members; never implement it
    /// directly.
    /// </summary>
    public interface IWindowPresenter : IPresenter
    {
    }
}
