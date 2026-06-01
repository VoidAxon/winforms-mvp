using System;
using System.Collections.Generic;
using System.Windows.Forms;
using WinformsMVP.Common;
using WinformsMVP.Core.Views;
using WinformsMVP.MVP.Presenters;
using WinformsMVP.Services;

namespace WinformsMVP.Samples.Tests.Mocks
{
    /// <summary>
    /// Mock implementation of IWindowNavigator for testing.
    /// Records window displays and makes them verifiable in tests.
    /// </summary>
    public class MockWindowNavigator : IWindowNavigator
    {
        // For recording
        public List<object> ShowModalCalls { get; private set; } = new List<object>();
        public int ShowWindowCalls { get; private set; }
        public object LastPresenter { get; private set; }
        public object LastParameters { get; private set; }

        // Configurable return values
        public bool ShowModalBoolResult { get; set; } = true;
        public InteractionResult ShowModalInteractionResult { get; set; } = InteractionResult.Ok();

        public void Clear()
        {
            ShowWindowCalls = 0;
            ShowModalCalls.Clear();
            LastPresenter = null;
            LastParameters = null;
        }

        // Non-modal window methods
        public IWindowView ShowWindow<TPresenter, TResult>(
            TPresenter presenter,
            IWin32Window owner = null,
            Func<TPresenter, object> keySelector = null,
            Action<InteractionResult<TResult>> onClosed = null)
            where TPresenter : IPresenter
        {
            ShowWindowCalls++;
            LastPresenter = presenter;
            return null;  // Return null for test mock
        }

        public IWindowView ShowWindow<TPresenter>(
            TPresenter presenter,
            IWin32Window owner = null,
            Func<TPresenter, object> keySelector = null)
            where TPresenter : IPresenter
        {
            ShowWindowCalls++;
            LastPresenter = presenter;
            return null;
        }

        public IWindowView ShowWindow<TPresenter, TParam, TResult>(
            TPresenter presenter,
            TParam parameters,
            IWin32Window owner = null,
            Func<TPresenter, object> keySelector = null,
            Action<InteractionResult<TResult>> onClosed = null)
            where TPresenter : IPresenter, IInitializable<TParam>
        {
            ShowWindowCalls++;
            LastPresenter = presenter;
            LastParameters = parameters;
            return null;
        }

        public IWindowView ShowWindow<TPresenter, TParam>(
            TPresenter presenter,
            TParam parameters,
            IWin32Window owner = null,
            Func<TPresenter, object> keySelector = null)
            where TPresenter : IPresenter, IInitializable<TParam>
        {
            ShowWindowCalls++;
            LastPresenter = presenter;
            LastParameters = parameters;
            return null;
        }

        // Modal window methods
        public InteractionResult ShowWindowAsModal<TPresenter>(
            TPresenter presenter,
            IWin32Window owner = null)
            where TPresenter : IPresenter
        {
            ShowModalCalls.Add(presenter);
            LastPresenter = presenter;
            return ShowModalInteractionResult;
        }

        public InteractionResult<TResult> ShowWindowAsModal<TPresenter, TResult>(
            TPresenter presenter,
            IWin32Window owner = null)
            where TPresenter : IPresenter
        {
            ShowModalCalls.Add(presenter);
            LastPresenter = presenter;
            return ShowModalBoolResult
                ? InteractionResult<TResult>.Ok(default(TResult))
                : InteractionResult<TResult>.Cancel();
        }

        public InteractionResult ShowWindowAsModal<TPresenter, TParam>(
            TPresenter presenter,
            TParam parameters,
            IWin32Window owner = null)
            where TPresenter : IPresenter, IInitializable<TParam>
        {
            ShowModalCalls.Add(presenter);
            LastPresenter = presenter;
            LastParameters = parameters;
            return ShowModalInteractionResult;
        }

        public InteractionResult<TResult> ShowWindowAsModal<TPresenter, TParam, TResult>(
            TPresenter presenter,
            TParam parameters,
            IWin32Window owner = null)
            where TPresenter : IPresenter, IInitializable<TParam>
        {
            ShowModalCalls.Add(presenter);
            LastPresenter = presenter;
            LastParameters = parameters;
            return ShowModalBoolResult
                ? InteractionResult<TResult>.Ok(default(TResult))
                : InteractionResult<TResult>.Cancel();
        }
    }
}
