using System;
using System.Collections.Generic;
using System.Windows.Forms;
using WinformsMVP.Common;
using WinformsMVP.MVP.Views;
using WinformsMVP.MVP.Presenters;
using WinformsMVP.Services;

namespace WinformsMVP.Services.Implementations
{
    public class WindowNavigator : IWindowNavigator
    {
        private readonly IViewMappingRegister _viewMappingRegister;
        private readonly Dictionary<object, Form> _openForms = new Dictionary<object, Form>();
        private readonly object _lock = new object(); // For thread synchronization

        public WindowNavigator(IViewMappingRegister viewMappingRegister)
        {
            _viewMappingRegister = viewMappingRegister;
        }

        #region Modal

        /// <summary>
        /// Shows the presenter's window modally and returns the result the presenter pushed via
        /// <c>RequestClose</c> (or <see cref="InteractionStatus.Cancel"/> if the user simply
        /// closed it).
        /// </summary>
        /// <exception cref="System.Collections.Generic.KeyNotFoundException">No View implementation
        /// is registered for the presenter's view interface.</exception>
        /// <exception cref="InvalidOperationException">The registered View is not a <see cref="Form"/>,
        /// does not implement <see cref="IWindowView"/>, or the presenter does not implement the
        /// matching <see cref="IViewAttacher{TView}"/>. The presenter is disposed before the throw.</exception>
        public InteractionResult<TResult> ShowWindowAsModal<TPresenter, TResult>(TPresenter presenter, IWin32Window owner = null) where TPresenter : IPresenter
        {
            var form = CreateFormForPresenter(presenter);

            InteractionResult<TResult> result = InteractionResult<TResult>.Cancel();
            var controller = WireController<TResult>(presenter, form, r => result = r, disposeForm: true);

            RunInitialize(presenter, form, () =>
            {
                if (presenter is IInitializable initializable) initializable.Initialize();
            });
            controller.WireFormEvents();

            if (controller.CloseRequestedBeforeShow)
            {
                controller.ConvergeWithoutShow();
                return result;
            }

            if (owner != null)
                form.ShowDialog(owner);
            else
                form.ShowDialog();

            // Presenter and form are disposed by the controller on FormClosed.
            return result;
        }

        public InteractionResult ShowWindowAsModal<TPresenter>(TPresenter presenter, IWin32Window owner = null)
        where TPresenter : IPresenter
        {
            // Internally call generic version, using object as placeholder result type
            return ShowWindowAsModal<TPresenter, object>(presenter, owner);
        }

        /// <inheritdoc cref="ShowWindowAsModal{TPresenter, TResult}(TPresenter, IWin32Window)"/>
        public InteractionResult<TResult> ShowWindowAsModal<TPresenter, TParam, TResult>(TPresenter presenter, TParam parameters, IWin32Window owner = null)
            where TPresenter : IPresenter, IInitializable<TParam>
        {
            var form = CreateFormForPresenter(presenter);

            InteractionResult<TResult> result = InteractionResult<TResult>.Cancel();
            var controller = WireController<TResult>(presenter, form, r => result = r, disposeForm: true);

            RunInitialize(presenter, form, () => presenter.Initialize(parameters));
            controller.WireFormEvents();

            if (controller.CloseRequestedBeforeShow)
            {
                controller.ConvergeWithoutShow();
                return result;
            }

            if (owner != null)
                form.ShowDialog(owner);
            else
                form.ShowDialog();

            return result;
        }

        public InteractionResult ShowWindowAsModal<TPresenter, TParam>(TPresenter presenter, TParam parameters, IWin32Window owner = null)
            where TPresenter : IPresenter, IInitializable<TParam>
        {
            // Internally call generic version, using object as placeholder result type
            return ShowWindowAsModal<TPresenter, TParam, object>(presenter, parameters, owner);
        }

        #endregion

        #region Non-Modal

        public IWindowView ShowWindow<TPresenter, TResult>(
            TPresenter presenter,
            IWin32Window owner = null,
            Func<TPresenter, object> keySelector = null,
            Action<InteractionResult<TResult>> onClosed = null
            )
            where TPresenter : IPresenter
        {
            Action<InteractionResult<TResult>> finalOnClosed = onClosed ?? (r => { });
            return ShowWindowInternal(presenter, owner, keySelector, finalOnClosed);
        }

        public IWindowView ShowWindow<TPresenter>(TPresenter presenter,
        IWin32Window owner = null,
        Func<TPresenter, object> keySelector = null) where TPresenter : IPresenter
        {
            return ShowWindow<TPresenter, object>(
                presenter,
                owner,
                keySelector,
                onClosed: null);
        }

        public IWindowView ShowWindow<TPresenter, TParam, TResult>(
            TPresenter presenter,
            TParam parameters,
            IWin32Window owner = null,
            Func<TPresenter, object> keySelector = null,
            Action<InteractionResult<TResult>> onClosed = null)
            where TPresenter : IPresenter, IInitializable<TParam>
        {
            object instanceKey;
            var existing = TryActivateExisting(presenter, keySelector, out instanceKey);
            if (existing != null) return existing;

            var newForm = CreateFormForPresenter(presenter);

            Action<InteractionResult<TResult>> safeOnClosed = onClosed ?? (r => { });
            var controller = WireController<TResult>(
                presenter, newForm, WrapWithKeyRemoval(instanceKey, safeOnClosed), disposeForm: false);

            RunInitialize(presenter, newForm, () => presenter.Initialize(parameters));
            RegisterOpenForm(instanceKey, newForm);
            controller.WireFormEvents();

            if (owner != null)
                newForm.Show(owner);
            else
                newForm.Show();

            return (IWindowView)newForm;
        }

        public IWindowView ShowWindow<TPresenter, TParam>(
            TPresenter presenter,
            TParam parameters,
            IWin32Window owner = null,
            Func<TPresenter, object> keySelector = null)
            where TPresenter : IPresenter, IInitializable<TParam>
        {
            return ShowWindow<TPresenter, TParam, object>(
                presenter,
                parameters,
                owner,
                keySelector,
                onClosed: null);
        }
        #endregion

        #region Core

        private IWindowView ShowWindowInternal<TPresenter, TResult>(
            TPresenter presenter,
            IWin32Window owner,
            Func<TPresenter, object> keySelector,
            Action<InteractionResult<TResult>> onClosed)
            where TPresenter : IPresenter
        {
            object instanceKey;
            var existing = TryActivateExisting(presenter, keySelector, out instanceKey);
            if (existing != null) return existing;

            var newForm = CreateFormForPresenter(presenter);

            var controller = WireController<TResult>(
                presenter, newForm, WrapWithKeyRemoval(instanceKey, onClosed), disposeForm: false);

            RunInitialize(presenter, newForm, () =>
            {
                if (presenter is IInitializable initializable) initializable.Initialize();
            });
            RegisterOpenForm(instanceKey, newForm);
            controller.WireFormEvents();

            if (owner != null)
                newForm.Show(owner);
            else
                newForm.Show();

            return (IWindowView)newForm;
        }

        /// <summary>
        /// Singleton-per-key activation. If <paramref name="keySelector"/> yields a key already
        /// mapped to a live form, that form is activated, the freshly created presenter is disposed,
        /// and the existing view is returned (non-null). Otherwise returns null and outputs the key.
        /// </summary>
        private IWindowView TryActivateExisting<TPresenter>(
            TPresenter presenter, Func<TPresenter, object> keySelector, out object instanceKey)
            where TPresenter : IPresenter
        {
            instanceKey = null;
            if (keySelector == null) return null;

            instanceKey = keySelector(presenter);
            if (instanceKey == null) return null;

            lock (_lock)
            {
                if (_openForms.TryGetValue(instanceKey, out var existingForm))
                {
                    if (existingForm != null && !existingForm.IsDisposed)
                    {
                        existingForm.Activate();
                        (presenter as IDisposable)?.Dispose();
                        return (IWindowView)existingForm;
                    }
                    _openForms.Remove(instanceKey);
                }
            }
            return null;
        }

        #endregion

        #region Close wiring

        /// <summary>
        /// Creates the per-window <see cref="WindowCloseController"/> and injects its close sink
        /// into the presenter (Push). Form events are wired separately, AFTER Initialize, via
        /// <see cref="WindowCloseController.WireFormEvents"/>. The controller converges the close
        /// result through <paramref name="onClosed"/>.
        /// </summary>
        private WindowCloseController WireController<TResult>(
            IPresenter presenter, Form form, Action<InteractionResult<TResult>> onClosed, bool disposeForm)
        {
            var participant = presenter as ICloseParticipant;
            if (participant == null)
                throw new InvalidOperationException(
                    presenter.GetType().Name + " cannot be shown by WindowNavigator because it does not " +
                    "derive from WindowPresenterBase / WindowPresenterBaseCore (no close participant).");
            var controller = new WindowCloseController(
                (IWindowView)form,
                participant,
                (res, status) => onClosed(BuildResult<TResult>(res, status)),
                disposeForm);
            controller.BindSink();
            return controller;
        }

        private Action<InteractionResult<TResult>> WrapWithKeyRemoval<TResult>(
            object instanceKey, Action<InteractionResult<TResult>> inner)
        {
            if (instanceKey == null) return inner;
            return r =>
            {
                lock (_lock) { _openForms.Remove(instanceKey); }
                inner(r);
            };
        }

        private static InteractionResult<TResult> BuildResult<TResult>(object result, InteractionStatus status)
        {
            switch (status)
            {
                case InteractionStatus.Ok:
                    return InteractionResult<TResult>.Ok(result is TResult typed ? typed : default(TResult));
                case InteractionStatus.Error:
                    return InteractionResult<TResult>.Error("Operation failed");
                case InteractionStatus.Cancel:
                default:
                    return InteractionResult<TResult>.Cancel();
            }
        }

        private void RegisterOpenForm(object instanceKey, Form form)
        {
            if (instanceKey == null) return;
            lock (_lock)
            {
                if (!_openForms.ContainsKey(instanceKey))
                    _openForms.Add(instanceKey, form);
            }
        }

        #endregion

        #region Utility

        /// <summary>
        /// Creates and attaches the Form for <paramref name="presenter"/> (does NOT initialize —
        /// the navigator initializes explicitly after the close sink is injected). View-mapping and
        /// configuration errors are surfaced by throwing; on any failure the presenter is disposed
        /// and the original exception is rethrown so the misconfiguration is not silently swallowed.
        /// </summary>
        private Form CreateFormForPresenter(IPresenter presenter)
        {
            try
            {
                return CreateAndBindForm(presenter, presenter.ViewInterfaceType);
            }
            catch
            {
                (presenter as IDisposable)?.Dispose();
                throw;
            }
        }

        private Form CreateAndBindForm(IPresenter presenter, Type viewInterfaceType)
        {
            // 1. Create View instance (instantiate using ViewMappingRegister)
            var newForm = _viewMappingRegister.CreateInstance(viewInterfaceType) as Form;
            if (newForm == null)
            {
                throw new InvalidOperationException(
                    $"The implementation of View interface {viewInterfaceType.Name} is not a Form. " +
                    $"Views used with WindowNavigator must inherit from System.Windows.Forms.Form.");
            }

            // Critical: Ensure View implements IWindowView interface
            if (!(newForm is IWindowView))
            {
                throw new InvalidOperationException($"View {newForm.GetType().Name} must implement IWindowView interface to support WindowNavigator's non-modal functionality.");
            }

            // The FormClosing/FormClosed bridge + Push sink are owned by WindowCloseController,
            // wired by the caller after this returns (the sink must be injected before Initialize),
            // so nothing close-related is set up here beyond attaching the view.

            // 2. Inject View into Presenter via the non-generic internal contract.
            ((IViewAttachable)presenter).AttachView((IViewBase)newForm);

            return newForm;
        }

        /// <summary>
        /// Runs the presenter's Initialize step. On failure, disposes the presenter and the
        /// orphaned form (it can never be shown) and rethrows.
        /// </summary>
        private static void RunInitialize(IPresenter presenter, Form form, Action initialize)
        {
            try
            {
                initialize();
            }
            catch
            {
                (presenter as IDisposable)?.Dispose();
                form.Dispose();
                throw;
            }
        }

        #endregion
    }
}
