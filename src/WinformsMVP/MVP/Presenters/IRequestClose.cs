namespace WinformsMVP.MVP.Presenters
{
    /// <summary>
    /// Marker implemented by window presenters that return a typed business result when they
    /// actively request close (the Push direction). It declares the result type once so the
    /// <see cref="RequestCloseExtensions.RequestClose{TResult}"/> extension is compile-time typed
    /// and lines up with <c>WindowNavigator.ShowWindowAsModal&lt;TPresenter, TResult&gt;</c>.
    /// </summary>
    /// <remarks>
    /// Must be implemented on a type deriving from <see cref="WindowPresenterBaseCore{TView}"/>
    /// (i.e. any <c>WindowPresenterBase</c>); the framework injects the close sink there. Push:
    /// <c>this.RequestClose(result)</c>. Pull: override <c>CanClose(CloseReason)</c>.
    /// </remarks>
    /// <typeparam name="TResult">The business result type.</typeparam>
    public interface IRequestClose<TResult>
    {
    }
}
