using System;
using System.Collections.Generic;
using System.Windows.Forms;
using WinformsMVP.Common;
using WinformsMVP.Common.Events;
using WinformsMVP.MVP.Views;
using WinformsMVP.MVP.Presenters;
using WinFormsCloseReason = System.Windows.Forms.CloseReason;
using MvpCloseReason = WinformsMVP.Common.CloseReason;

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

        public InteractionResult<TResult> ShowWindowAsModal<TPresenter, TResult>(TPresenter presenter, IWin32Window owner = null) where TPresenter : IPresenter
        {
            Type viewInterfaceType = presenter.ViewInterfaceType;
            var form = CreateAndBindForm(presenter, viewInterfaceType);
            if (form == null)
                return InteractionResult<TResult>.Error($"Cannot create View instance: {viewInterfaceType.Name}");

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

        public InteractionResult<TResult> ShowWindowAsModal<TPresenter, TParam, TResult>(TPresenter presenter, TParam parameters, IWin32Window owner = null)
            where TPresenter : IPresenter, IInitializable<TParam>
        {
            Type viewInterfaceType = presenter.ViewInterfaceType;
            var form = CreateAndBindForm(presenter, viewInterfaceType, callInitialize: false);
            if (form == null)
                return InteractionResult<TResult>.Error($"Cannot create View instance: {viewInterfaceType.Name}");

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
            Type viewInterfaceType = presenter.ViewInterfaceType;
            var newForm = CreateAndBindForm(presenter, viewInterfaceType, callInitialize: false);

            if (newForm == null)
            {
                (presenter as IDisposable)?.Dispose();
                Action<InteractionResult<TResult>> finalOnClosed = onClosed ?? (r => { });
                finalOnClosed.Invoke(InteractionResult<TResult>.Error($"Cannot create View instance: {viewInterfaceType.Name}"));
                return null;
            }

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
            Type viewInterfaceType = presenter.ViewInterfaceType;
            var newForm = CreateAndBindForm(presenter, viewInterfaceType);

            if (newForm == null)
            {
                (presenter as IDisposable)?.Dispose();
                onClosed.Invoke(InteractionResult<TResult>.Error($"Cannot create View instance: {viewInterfaceType.Name}"));
                return null;
            }

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
            var requestCloser = presenter as IRequestClose<TResult>;
            TResult finalResult = default(TResult);
            InteractionStatus finalStatus = InteractionStatus.Cancel; // Default: user-cancelled (e.g. clicked X)

            EventHandler<CloseRequestedEventArgs<TResult>> closeRequestedHandler = null;
            FormClosedEventHandler formClosedHandler = null;

            // 1. Presenter actively requests close (Push direction via IRequestClose.CloseRequested).
            //    Pull direction (user clicks X) is handled by the Presenter subscribing to
            //    IWindowView.Closing — WindowNavigator does NOT inspect CanClose / similar
            //    APIs on the Presenter.
            if (requestCloser != null)
            {
                closeRequestedHandler = (s, e) =>
                {
                    // Presenter has set the result; record before triggering Form.Close().
                    finalResult = e.Result;
                    finalStatus = e.Status;

                    // Trigger Form close. FormClosing → IWindowView.OnClosing fires after this;
                    // the Presenter's Closing handler is expected to allow the close (since
                    // dirty state should already have been finalized in the OnSave/OnCancel
                    // logic that produced the CloseRequested event).
                    form.Close();
                };
                requestCloser.CloseRequested += closeRequestedHandler;
            }

            // 2. After the Form actually closes (FormClosed).
            formClosedHandler = (s, e) =>
            {
                // A. Immediately unsubscribe to prevent leaks.
                form.FormClosed -= formClosedHandler;
                if (requestCloser != null)
                {
                    requestCloser.CloseRequested -= closeRequestedHandler;
                }

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
            var requestCloser = presenter as IRequestClose<TResult>;

            EventHandler<CloseRequestedEventArgs<TResult>> closeRequestedHandler = null;
            FormClosedEventHandler formClosedHandler = null;

            TResult finalResult = default(TResult);
            InteractionStatus finalStatus = InteractionStatus.Cancel;

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

            // 2. Handle Presenter actively requesting close (Push direction).
            //    Pull direction (user closing the window) is handled by the Presenter
            //    subscribing to IWindowView.Closing — WindowNavigator does NOT need to
            //    inspect any Presenter-side API to decide whether to cancel.
            if (requestCloser != null)
            {
                closeRequestedHandler = (s, e) =>
                {
                    finalResult = e.Result;
                    finalStatus = e.Status;

                    form.Close();
                };
                requestCloser.CloseRequested += closeRequestedHandler;
            }

            // 3. Handle FormClosed: unregister framework references, release resources, fire callback.
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

                onClosed.Invoke(result);

                // C. Release Presenter resources.
                (presenter as IDisposable)?.Dispose();

                // D. Clean up all event subscriptions and Form resources.
                form.FormClosed -= formClosedHandler;
                if (requestCloser != null)
                {
                    requestCloser.CloseRequested -= closeRequestedHandler;
                }
                form.Dispose();
            };
            form.FormClosed += formClosedHandler;
        }

        #endregion

        #region Utility

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
            if (!(newForm is IWindowView windowView))
            {
                throw new InvalidOperationException($"View {newForm.GetType().Name} must implement IWindowView interface to support WindowNavigator's non-modal functionality.");
            }

            // 2. Bridge WinForms FormClosing to the framework's IWindowView.OnClosing.
            //    Presenters subscribe to IWindowView.Closing to decide whether to allow
            //    the close (e.g. prompt on unsaved changes). WinForms types do not leak
            //    beyond this bridge.
            newForm.FormClosing += (s, e) =>
            {
                var args = new WindowClosingEventArgs(MapCloseReason(e.CloseReason));
                windowView.OnClosing(args);
                if (args.Cancel) e.Cancel = true;
            };

            // 3. Inject View into Presenter
            InjectViewIntoPresenter(presenter, newForm as IViewBase);

            // 4. Initialize business logic
            if (callInitialize && presenter is IInitializable initializable)
            {
                initializable.Initialize();
            }

            return newForm;
        }

        /// <summary>
        /// Maps WinForms <see cref="WinFormsCloseReason"/> to the framework abstraction
        /// <see cref="MvpCloseReason"/>. WinForms types are intentionally not exposed to
        /// presenter code; this is the single place the mapping happens.
        /// </summary>
        private static MvpCloseReason MapCloseReason(WinFormsCloseReason reason)
        {
            switch (reason)
            {
                case WinFormsCloseReason.UserClosing:
                    return MvpCloseReason.Normal;
                case WinFormsCloseReason.WindowsShutDown:
                    return MvpCloseReason.SystemShutdown;
                case WinFormsCloseReason.TaskManagerClosing:
                    return MvpCloseReason.TaskManager;
                case WinFormsCloseReason.FormOwnerClosing:
                case WinFormsCloseReason.MdiFormClosing:
                    return MvpCloseReason.ParentClosing;
                case WinFormsCloseReason.ApplicationExitCall:
                    return MvpCloseReason.Normal;
                default:
                    return MvpCloseReason.Unknown;
            }
        }

        private void InjectViewIntoPresenter(IPresenter presenter, IViewBase viewInstance)
        {
            Type viewInterfaceType = presenter.ViewInterfaceType;
            Type attacherType = typeof(IViewAttacher<>).MakeGenericType(viewInterfaceType);

            var attachMethod = attacherType.GetMethod("AttachView");

            if (attachMethod != null)
            {
                attachMethod.Invoke(presenter, new object[] { viewInstance });
            }
            else
            {
                throw new InvalidOperationException($"Presenter {presenter.GetType().Name} does not correctly implement interface {attacherType.Name}.");
            }
        }

        #endregion
    }
}
