using System;
using WinformsMVP.Common;
using WinformsMVP.MVP.Views;
using WinformsMVP.MVP.Presenters;

namespace WinformsMVP.Services
{
    public interface IWindowNavigator
    {
        // ==================================================
        // Non-modal window methods (without parameters)
        // ==================================================

        IWindowView ShowWindow<TPresenter, TResult>(
            TPresenter presenter,
            IWindowView owner = null,
            Func<TPresenter, object> keySelector = null,
            Action<InteractionResult<TResult>> onClosed = null)
            where TPresenter : IPresenter;

        IWindowView ShowWindow<TPresenter>(
            TPresenter presenter,
            IWindowView owner = null,
            Func<TPresenter, object> keySelector = null)
            where TPresenter : IPresenter;

        // ==================================================
        // Modal window methods (without parameters)
        // ==================================================

        InteractionResult ShowWindowAsModal<TPresenter>(
            TPresenter presenter,
            IWindowView owner = null)
            where TPresenter : IPresenter;

        InteractionResult<TResult> ShowWindowAsModal<TPresenter, TResult>(
            TPresenter presenter,
            IWindowView owner = null)
            where TPresenter : IPresenter;

        // ==================================================
        // Non-modal window methods (with parameters)
        // ==================================================

        IWindowView ShowWindow<TPresenter, TParam, TResult>(
            TPresenter presenter,
            TParam parameters,
            IWindowView owner = null,
            Func<TPresenter, object> keySelector = null,
            Action<InteractionResult<TResult>> onClosed = null)
            where TPresenter : IPresenter, IInitializable<TParam>;

        IWindowView ShowWindow<TPresenter, TParam>(
            TPresenter presenter,
            TParam parameters,
            IWindowView owner = null,
            Func<TPresenter, object> keySelector = null)
            where TPresenter : IPresenter, IInitializable<TParam>;

        // ==================================================
        // Modal window methods (with parameters)
        // ==================================================

        InteractionResult ShowWindowAsModal<TPresenter, TParam>(
            TPresenter presenter,
            TParam parameters,
            IWindowView owner = null)
            where TPresenter : IPresenter, IInitializable<TParam>;

        InteractionResult<TResult> ShowWindowAsModal<TPresenter, TParam, TResult>(
            TPresenter presenter,
            TParam parameters,
            IWindowView owner = null)
            where TPresenter : IPresenter, IInitializable<TParam>;
    }
}