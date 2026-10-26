using System;
using System.Collections.Generic;
using System.Windows.Forms;
using Kibinago.Common;
using Kibinago.Views;
using Kibinago.Presenters;
using Kibinago.Services;

namespace Kibinago.Services
{
    /// <summary>
    /// Default <see cref="IWindowNavigator"/>: creates the presenter's Form through the
    /// <see cref="IViewMappingRegister"/>, attaches and initializes the presenter, wires the close gate
    /// and shows the window modally or non-modally.
    /// </summary>
    public class WindowNavigator : IWindowNavigator
    {
        private readonly IViewMappingRegister _viewMappingRegister;
        private readonly Dictionary<object, Form> _openForms = new Dictionary<object, Form>();
        private readonly object _lock = new object(); // For thread synchronization

        /// <summary>Creates a navigator that resolves Forms from <paramref name="viewMappingRegister"/>.</summary>
        /// <param name="viewMappingRegister">Maps each view interface to its Form.</param>
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
        /// does not implement <see cref="IWindowView"/>, or the presenter is not a window presenter.
        /// The presenter is disposed before the throw.</exception>
        public InteractionResult<TResult> ShowWindowAsModal<TPresenter, TResult>(TPresenter presenter, IWindowView owner = null) where TPresenter : IWindowPresenter, IInitializable
            => ShowModalCore<TResult>(presenter, owner, () => presenter.Initialize);

        /// <inheritdoc />
        public InteractionResult ShowWindowAsModal<TPresenter>(TPresenter presenter, IWindowView owner = null)
        where TPresenter : IWindowPresenter, IInitializable
        {
            // Internally call generic version, using object as placeholder result type
            return ShowWindowAsModal<TPresenter, object>(presenter, owner);
        }

        /// <inheritdoc cref="ShowWindowAsModal{TPresenter, TResult}(TPresenter, IWindowView)"/>
        public InteractionResult<TResult> ShowWindowAsModal<TPresenter, TParam, TResult>(TPresenter presenter, TParam parameters, IWindowView owner = null)
            where TPresenter : IWindowPresenter, IInitializable<TParam>
            => ShowModalCore<TResult>(presenter, owner, () => () => presenter.Initialize(parameters));

        /// <inheritdoc />
        public InteractionResult ShowWindowAsModal<TPresenter, TParam>(TPresenter presenter, TParam parameters, IWindowView owner = null)
            where TPresenter : IWindowPresenter, IInitializable<TParam>
        {
            // Internally call generic version, using object as placeholder result type
            return ShowWindowAsModal<TPresenter, TParam, object>(presenter, parameters, owner);
        }

        #endregion

        #region Non-Modal

        /// <summary>
        /// Shows the presenter's window non-modally. With a <paramref name="key"/>, a key
        /// that already maps to an open window of the same presenter type (runtime type) activates
        /// that window instead: the new presenter is disposed and its <paramref name="onClosed"/> is
        /// never called. Keys of different presenter types never collide.
        /// </summary>
        public IWindowView ShowWindow<TPresenter, TResult>(
            TPresenter presenter,
            IWindowView owner = null,
            object key = null,
            Action<InteractionResult<TResult>> onClosed = null
            )
            where TPresenter : IWindowPresenter, IInitializable
            => ShowModelessCore(presenter, owner, key, onClosed, () => presenter.Initialize);

        /// <inheritdoc />
        public IWindowView ShowWindow<TPresenter>(TPresenter presenter,
        IWindowView owner = null,
        object key = null) where TPresenter : IWindowPresenter, IInitializable
        {
            return ShowWindow<TPresenter, object>(
                presenter,
                owner,
                key,
                onClosed: null);
        }

        /// <inheritdoc cref="ShowWindow{TPresenter, TResult}(TPresenter, IWindowView, object, Action{InteractionResult{TResult}})"/>
        public IWindowView ShowWindow<TPresenter, TParam, TResult>(
            TPresenter presenter,
            TParam parameters,
            IWindowView owner = null,
            object key = null,
            Action<InteractionResult<TResult>> onClosed = null)
            where TPresenter : IWindowPresenter, IInitializable<TParam>
            => ShowModelessCore(presenter, owner, key, onClosed, () => () => presenter.Initialize(parameters));

        /// <inheritdoc />
        public IWindowView ShowWindow<TPresenter, TParam>(
            TPresenter presenter,
            TParam parameters,
            IWindowView owner = null,
            object key = null)
            where TPresenter : IWindowPresenter, IInitializable<TParam>
        {
            return ShowWindow<TPresenter, TParam, object>(
                presenter,
                parameters,
                owner,
                key,
                onClosed: null);
        }
        #endregion

        #region Core

        // Every public Show overload funnels into one of these two, so the create → sink → initialize
        // → wire → show order (and its failure cleanup) exists exactly once per mode. The
        // initializer is produced lazily by a factory so presenter-shape validation runs inside the
        // dispose-on-failure guard of PrepareForm.

        private InteractionResult<TResult> ShowModalCore<TResult>(
            IPresenter presenter, IWindowView owner, Func<Action> initializerFactory)
        {
            InteractionResult<TResult> result = InteractionResult<TResult>.Cancel();
            var form = PrepareForm<TResult>(presenter, owner, initializerFactory,
                r => result = r, disposeForm: true, instanceKey: null, out var controller, out var resolvedOwner);

            // RequestClose during initialization cancelled the opening: never shown.
            if (controller.OpeningCancelled)
            {
                controller.ConvergeWithoutShow();
                return result;
            }

            controller.BeginShowing();
            if (resolvedOwner != null) form.ShowDialog(resolvedOwner); else form.ShowDialog();

            // Presenter and form are disposed by the controller on FormClosed.
            return result;
        }

        private IWindowView ShowModelessCore<TPresenter, TResult>(
            TPresenter presenter, IWindowView owner, object key,
            Action<InteractionResult<TResult>> onClosed, Func<Action> initializerFactory)
            where TPresenter : IPresenter
        {
            object instanceKey;
            var existing = TryActivateExisting(presenter, key, out instanceKey);
            if (existing != null) return existing;

            Form newForm = null;
            Action<InteractionResult<TResult>> safeOnClosed = onClosed ?? (r => { });
            newForm = PrepareForm<TResult>(presenter, owner, initializerFactory,
                r =>
                {
                    UnregisterOpenForm(instanceKey, newForm);
                    safeOnClosed(r);
                },
                disposeForm: false, instanceKey,
                out var controller, out var resolvedOwner);

            // RequestClose during initialization cancelled the opening: never shown. The key is released
            // and onClosed runs (it may reopen the same key) before presenter and Form are disposed.
            if (controller.OpeningCancelled)
            {
                controller.ConvergeWithoutShow();
                return null;
            }

            controller.BeginShowing();
            if (resolvedOwner != null) newForm.Show(resolvedOwner); else newForm.Show();

            return (IWindowView)newForm;
        }

        /// <summary>
        /// Validates the owner and presenter, creates and attaches the Form, injects the close sink,
        /// initializes the presenter and wires the form events. A non-null
        /// <paramref name="instanceKey"/> is registered BEFORE Initialize, so a re-entrant show for
        /// the same key (e.g. OnInitialize pumps a modal prompt) activates this window instead of
        /// opening a twin. On any failure the presenter and the form (if one was created) are
        /// disposed, the key is released, and the exception is rethrown.
        /// </summary>
        private Form PrepareForm<TResult>(
            IPresenter presenter, IWindowView owner, Func<Action> initializerFactory,
            Action<InteractionResult<TResult>> onClosed, bool disposeForm, object instanceKey,
            out WindowLifecycleController controller, out IWin32Window resolvedOwner)
        {
            if (presenter == null) throw new ArgumentNullException(nameof(presenter));

            Form form = null;
            try
            {
                resolvedOwner = ResolveOwner(owner);
                var participant = presenter as ICloseParticipant;
                if (participant == null)
                    throw new InvalidOperationException(
                        presenter.GetType().Name + " cannot be shown by WindowNavigator because it does not " +
                        "derive from WindowPresenterBase / WindowPresenterBaseCore (no close participant).");
                var initialize = initializerFactory();

                form = CreateForm(presenter.ViewInterfaceType);
                ((IViewAttachable)presenter).AttachView((IViewBase)form);

                controller = new WindowLifecycleController(
                    (IWindowView)form,
                    participant,
                    (res, status) => onClosed(CloseResults.Build<TResult>(res, status)),
                    disposeForm);
                controller.BindSink();   // sink before Initialize, so OnInitialize may RequestClose

                RegisterOpenForm(instanceKey, form);
                initialize();
                controller.WireFormEvents();
                return form;
            }
            catch
            {
                UnregisterOpenForm(instanceKey, form);
                (presenter as IDisposable)?.Dispose();
                form?.Dispose();
                throw;
            }
        }

        /// <summary>
        /// Singleton-per-key activation. Keys are scoped by the presenter's runtime type: the
        /// registry key is (presenter type, selected key), so two screens that both use, say, a
        /// record id never activate each other's window. If that key maps to a live form, the form
        /// is activated, the freshly created presenter is disposed, and the existing view is
        /// returned (non-null). Otherwise returns null and outputs the scoped key.
        /// </summary>
        private IWindowView TryActivateExisting<TPresenter>(
            TPresenter presenter, object key, out object instanceKey)
            where TPresenter : IPresenter
        {
            instanceKey = null;
            if (key == null) return null;

                        if (key == null || presenter == null) return null;   // a null presenter is rejected by PrepareForm
            instanceKey = (presenter.GetType(), key);

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

        #region Open-form registry

        private void RegisterOpenForm(object instanceKey, Form form)
        {
            if (instanceKey == null) return;
            lock (_lock)
            {
                _openForms[instanceKey] = form;
            }
        }

        /// <summary>Removes the key only while it still maps to <paramref name="form"/>, so a
        /// window closing late never evicts a different window registered under the same key.</summary>
        private void UnregisterOpenForm(object instanceKey, Form form)
        {
            if (instanceKey == null || form == null) return;
            lock (_lock)
            {
                if (_openForms.TryGetValue(instanceKey, out var mapped) && ReferenceEquals(mapped, form))
                    _openForms.Remove(instanceKey);
            }
        }

        #endregion

        #region Utility

        /// <summary>
        /// Resolves a framework <see cref="IWindowView"/> owner to the WinForms <see cref="IWin32Window"/>
        /// that <c>Form.ShowDialog</c>/<c>Form.Show</c> require. A null owner means "no owner". A
        /// non-window view passed as an owner is a misuse and throws.
        /// </summary>
        private static IWin32Window ResolveOwner(IWindowView owner)
        {
            if (owner == null) return null;
            if (owner is IWin32Window win) return win;
            throw new ArgumentException("owner must be a Form-backed view.", nameof(owner));
        }

        private Form CreateForm(Type viewInterfaceType)
        {
            var instance = _viewMappingRegister.CreateInstance(viewInterfaceType);
            var newForm = instance as Form;
            if (newForm == null)
            {
                (instance as IDisposable)?.Dispose();
                throw new InvalidOperationException(
                    $"The implementation of View interface {viewInterfaceType.Name} is not a Form. " +
                    $"Views used with WindowNavigator must inherit from System.Windows.Forms.Form.");
            }

            if (!(newForm is IWindowView))
            {
                newForm.Dispose();
                throw new InvalidOperationException($"View {newForm.GetType().Name} must implement IWindowView interface to be shown by WindowNavigator.");
            }

            return newForm;
        }

        #endregion
    }
}
