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
        /// <see cref="IRequestClose{TResult}"/> (or <see cref="InteractionStatus.Cancel"/> if the
        /// user simply closed it).
        /// </summary>
        /// <exception cref="System.Collections.Generic.KeyNotFoundException">No View implementation
        /// is registered for the presenter's view interface.</exception>
        /// <exception cref="InvalidOperationException">The registered View is not a <see cref="Form"/>,
        /// does not implement <see cref="IWindowView"/>, or the presenter does not implement the
        /// matching <see cref="IViewAttacher{TView}"/>. The presenter is disposed before the throw.</exception>
        public InteractionResult<TResult> ShowWindowAsModal<TPresenter, TResult>(TPresenter presenter, IWin32Window owner = null) where TPresenter : IPresenter
        {
            var form = CreateFormForPresenter(presenter, callInitialize: true);

            InteractionResult<TResult> result = InteractionResult<TResult>.Cancel();

            // Attach modal close handlers
            AttachModalCloseHandlers<TResult>(presenter, form, r => result = r);

            // WinForms modal blocking
            if (owner != null)
                form.ShowDialog(owner);
            else
                form.ShowDialog();

            // Immediately release Presenter resources after modal window closes
            (presenter as IDisposable)?.Dispose();

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
            var form = CreateFormForPresenter(presenter, callInitialize: false);

            // Initialize presenter with parameters
            presenter.Initialize(parameters);

            InteractionResult<TResult> result = InteractionResult<TResult>.Cancel();

            // Attach modal close handlers
            AttachModalCloseHandlers<TResult>(presenter, form, r => result = r);

            // WinForms modal blocking
            if (owner != null)
                form.ShowDialog(owner);
            else
                form.ShowDialog();

            // Immediately release Presenter resources after modal window closes
            (presenter as IDisposable)?.Dispose();

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
            // If onClosed is null, use a safe no-op to ensure ShowWindowInternal doesn't need null checking
            Action<InteractionResult<TResult>> finalOnClosed = onClosed ?? (r => { });

            return ShowWindowInternal(
            presenter,
            owner,
            keySelector,
            finalOnClosed
            );
        }

        public IWindowView ShowWindow<TPresenter>(TPresenter presenter,
        IWin32Window owner = null,
        Func<TPresenter, object> keySelector = null) where TPresenter : IPresenter
        {
            // Internally call the complete generic version of ShowWindow<TPresenter, TResult>,
            // with TResult set to object and onClosed set to null.
            return ShowWindow<TPresenter, object>(
                presenter,
                owner,
                keySelector,
                onClosed: null // onClosed is null here because this overload doesn't provide a callback parameter
            );
        }


        public IWindowView ShowWindow<TPresenter, TParam, TResult>(
            TPresenter presenter,
            TParam parameters,
            IWin32Window owner = null,
            Func<TPresenter, object> keySelector = null,
            Action<InteractionResult<TResult>> onClosed = null)
            where TPresenter : IPresenter, IInitializable<TParam>
        {
            object instanceKey = null;
            Form existingForm = null;

            // 1. Calculate key and check singleton/activation (thread-safe)
            if (keySelector != null)
            {
                instanceKey = keySelector(presenter);
                lock (_lock)
                {
                    if (instanceKey != null && _openForms.TryGetValue(instanceKey, out existingForm))
                    {
                        if (existingForm != null && !existingForm.IsDisposed)
                        {
                            existingForm.Activate();
                            (presenter as IDisposable)?.Dispose(); // Release new Presenter instance
                            return (IWindowView)existingForm;
                        }
                        _openForms.Remove(instanceKey); // Clean up invalid old reference
                    }
                }
            }

            // 2. Create new Form (no automatic initialization)
            var newForm = CreateFormForPresenter(presenter, callInitialize: false);

            // 3. Initialize Presenter with parameters
            presenter.Initialize(parameters);

            // 4. Handle close logic (non-modal)
            Action<InteractionResult<TResult>> safeOnClosed = onClosed ?? (r => { });
            AttachNonModalCloseHandlers<TResult>(instanceKey, presenter, newForm, safeOnClosed);

            // 5. Show window
            if (owner != null)
                newForm.Show(owner);
            else
                newForm.Show();

            // 6. Return IWindowView handle
            return (IWindowView)newForm;
        }

        public IWindowView ShowWindow<TPresenter, TParam>(
            TPresenter presenter,
            TParam parameters,
            IWin32Window owner = null,
            Func<TPresenter, object> keySelector = null)
            where TPresenter : IPresenter, IInitializable<TParam>
        {
            // Internally call the complete generic version of ShowWindow<TPresenter, TParam, TResult>,
            // with TResult set to object and onClosed set to null.
            return ShowWindow<TPresenter, TParam, object>(
                presenter,
                parameters,
                owner,
                keySelector,
                onClosed: null
            );
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
            object instanceKey = null;
            Form existingForm = null;

            // 1. Calculate key and check singleton/activation (thread-safe)
            if (keySelector != null)
            {
                instanceKey = keySelector(presenter);
                lock (_lock)
                {
                    if (instanceKey != null && _openForms.TryGetValue(instanceKey, out existingForm))
                    {
                        if (existingForm != null && !existingForm.IsDisposed)
                        {
                            existingForm.Activate();
                            (presenter as IDisposable)?.Dispose(); // Release new Presenter instance
                            return (IWindowView)existingForm;
                        }
                        _openForms.Remove(instanceKey); // Clean up invalid old reference
                    }
                }
            }

            // 2. Create new Form
            var newForm = CreateFormForPresenter(presenter, callInitialize: true);

            // 3. Handle close logic (non-modal)
            AttachNonModalCloseHandlers<TResult>(instanceKey, presenter, newForm, onClosed);

            // 4. Show window
            if (owner != null)
                newForm.Show(owner);
            else
                newForm.Show();

            // 5. Return IWindowView handle
            return (IWindowView)newForm;
        }

        #endregion

        #region Close Handlers

        private void AttachModalCloseHandlers<TResult>(
            IPresenter presenter,
            Form form,
            Action<InteractionResult<TResult>> setResultCallback)
        {
            var closeCoordinator = WireCloseGate(form);
            TResult finalResult = default(TResult);
            InteractionStatus finalStatus = InteractionStatus.Cancel; // Default: user-cancelled (e.g. clicked X)

            FormClosedEventHandler formClosedHandler = null;

            // TODO (Task 7): Wire ICloseParticipant.BindCloseSink here once WindowCloseController
            // exists. Push direction (Presenter-initiated close) is not yet wired through the
            // navigator; presenters that need it should use the Connect extension (Task 6).

            // After the Form actually closes (FormClosed).
            formClosedHandler = (s, e) =>
            {
                // A. Immediately unsubscribe to prevent leaks.
                form.FormClosed -= formClosedHandler;

                // B. Wrap final result.
                InteractionResult<TResult> result;
                switch (finalStatus)
                {
                    case InteractionStatus.Ok:
                        result = InteractionResult<TResult>.Ok(finalResult);
                        break;
                    case InteractionStatus.Error:
                        result = InteractionResult<TResult>.Error("Operation failed");
                        break;
                    case InteractionStatus.Cancel:
                    default:
                        result = InteractionResult<TResult>.Cancel();
                        break;
                }

                // C. Return result via callback.
                setResultCallback.Invoke(result);

                // D. Release Form resources.
                form.Dispose();
            };
            form.FormClosed += formClosedHandler;
        }

        private void AttachNonModalCloseHandlers<TResult>(
        object instanceKey,
        IPresenter presenter,
        Form form,
        Action<InteractionResult<TResult>> onClosed)
        {
            WireCloseGate(form);

            FormClosedEventHandler formClosedHandler = null;

            // 1. Register in _openForms dictionary (thread-safe).
            if (instanceKey != null)
            {
                lock (_lock)
                {
                    if (!_openForms.ContainsKey(instanceKey))
                    {
                        _openForms.Add(instanceKey, form);
                    }
                }
            }

            // TODO (Task 7): Wire ICloseParticipant.BindCloseSink here once WindowCloseController
            // exists. Push direction (Presenter-initiated close) is not yet wired through the
            // navigator; presenters that need it should use the Connect extension (Task 6).

            // 2. Handle FormClosed: unregister framework references, release resources, fire callback.
            formClosedHandler = (s, e) =>
            {
                // A. Clean up framework state (thread-safe).
                if (instanceKey != null)
                {
                    lock (_lock)
                    {
                        _openForms.Remove(instanceKey);
                    }
                }

                // B. Default to Cancel until Task 7 wires the push-direction sink.
                onClosed.Invoke(InteractionResult<TResult>.Cancel());

                // C. Release Presenter resources.
                (presenter as IDisposable)?.Dispose();

                // D. Clean up subscriptions and Form resources.
                form.FormClosed -= formClosedHandler;
                form.Dispose();
            };
            form.FormClosed += formClosedHandler;
        }

        #endregion

        #region Utility

        /// <summary>
        /// Creates and binds the Form for <paramref name="presenter"/>. View-mapping and
        /// configuration errors are surfaced by throwing (see <see cref="CreateAndBindForm"/>);
        /// on any failure the presenter is disposed — it can never be shown — and the original
        /// exception is rethrown so the misconfiguration is not silently swallowed.
        /// </summary>
        private Form CreateFormForPresenter(IPresenter presenter, bool callInitialize)
        {
            try
            {
                return CreateAndBindForm(presenter, presenter.ViewInterfaceType, callInitialize);
            }
            catch
            {
                (presenter as IDisposable)?.Dispose();
                throw;
            }
        }

        private Form CreateAndBindForm(IPresenter presenter, Type viewInterfaceType, bool callInitialize = true)
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

            // The FormClosing → IWindowView.OnClosing bridge is wired by WireCloseGate when the
            // close handlers are attached (it needs the per-window WindowCloseCoordinator), so it
            // is intentionally NOT set up here.

            // 2. Inject View into Presenter via the non-generic internal contract.
            //    All presenters derive from PresenterBase, which implements IViewAttachable;
            //    this is a direct virtual call (no reflection).
            ((IViewAttachable)presenter).AttachView((IViewBase)newForm);

            // 3. Initialize business logic
            if (callInitialize && presenter is IInitializable initializable)
            {
                initializable.Initialize();
            }

            return newForm;
        }

        /// <summary>
        /// Wires the WinForms <c>FormClosing</c> → framework <see cref="IWindowView.OnClosing"/>
        /// bridge for <paramref name="form"/> and returns the per-window
        /// <see cref="WindowCloseCoordinator"/> that gates it. The WinForms-to-framework
        /// <c>CloseReason</c> mapping lives in <see cref="WindowClosingBridge"/>. A close the
        /// Presenter initiated (see <see cref="WindowCloseCoordinator.BeginPresenterClose"/>)
        /// bypasses the gate.
        /// </summary>
        private static WindowCloseCoordinator WireCloseGate(Form form)
        {
            var coordinator = new WindowCloseCoordinator((IWindowView)form);
            form.FormClosing += (s, e) =>
            {
                if (coordinator.ShouldCancel(WindowClosingBridge.MapCloseReason(e.CloseReason)))
                    e.Cancel = true;
            };
            return coordinator;
        }

        #endregion
    }
}
