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
        /// does not implement <see cref="IWindowView"/>, the presenter is not a window presenter,
        /// or the presenter requires parameters (use the overload that takes them). The presenter
        /// is disposed before the throw.</exception>
        public InteractionResult<TResult> ShowWindowAsModal<TPresenter, TResult>(TPresenter presenter, IWindowView owner = null) where TPresenter : IPresenter
            => ShowModalCore<TResult>(presenter, owner, () => ParameterlessInitializer(presenter));

        public InteractionResult ShowWindowAsModal<TPresenter>(TPresenter presenter, IWindowView owner = null)
        where TPresenter : IPresenter
        {
            // Internally call generic version, using object as placeholder result type
            return ShowWindowAsModal<TPresenter, object>(presenter, owner);
        }

        /// <inheritdoc cref="ShowWindowAsModal{TPresenter, TResult}(TPresenter, IWindowView)"/>
        public InteractionResult<TResult> ShowWindowAsModal<TPresenter, TParam, TResult>(TPresenter presenter, TParam parameters, IWindowView owner = null)
            where TPresenter : IPresenter, IInitializable<TParam>
            => ShowModalCore<TResult>(presenter, owner, () => () => presenter.Initialize(parameters));

        public InteractionResult ShowWindowAsModal<TPresenter, TParam>(TPresenter presenter, TParam parameters, IWindowView owner = null)
            where TPresenter : IPresenter, IInitializable<TParam>
        {
            // Internally call generic version, using object as placeholder result type
            return ShowWindowAsModal<TPresenter, TParam, object>(presenter, parameters, owner);
        }

        #endregion

        #region Non-Modal

        /// <summary>
        /// Shows the presenter's window non-modally. With a <paramref name="keySelector"/>, a key
        /// that already maps to an open window activates that window instead: the new presenter is
        /// disposed and its <paramref name="onClosed"/> is never called.
        /// </summary>
        public IWindowView ShowWindow<TPresenter, TResult>(
            TPresenter presenter,
            IWindowView owner = null,
            Func<TPresenter, object> keySelector = null,
            Action<InteractionResult<TResult>> onClosed = null
            )
            where TPresenter : IPresenter
            => ShowModelessCore(presenter, owner, keySelector, onClosed, () => ParameterlessInitializer(presenter));

        public IWindowView ShowWindow<TPresenter>(TPresenter presenter,
        IWindowView owner = null,
        Func<TPresenter, object> keySelector = null) where TPresenter : IPresenter
        {
            return ShowWindow<TPresenter, object>(
                presenter,
                owner,
                keySelector,
                onClosed: null);
        }

        /// <inheritdoc cref="ShowWindow{TPresenter, TResult}(TPresenter, IWindowView, Func{TPresenter, object}, Action{InteractionResult{TResult}})"/>
        public IWindowView ShowWindow<TPresenter, TParam, TResult>(
            TPresenter presenter,
            TParam parameters,
            IWindowView owner = null,
            Func<TPresenter, object> keySelector = null,
            Action<InteractionResult<TResult>> onClosed = null)
            where TPresenter : IPresenter, IInitializable<TParam>
            => ShowModelessCore(presenter, owner, keySelector, onClosed, () => () => presenter.Initialize(parameters));

        public IWindowView ShowWindow<TPresenter, TParam>(
            TPresenter presenter,
            TParam parameters,
            IWindowView owner = null,
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

            if (controller.CloseRequestedBeforeShow)
            {
                controller.ConvergeWithoutShow();
                return result;
            }

            if (resolvedOwner != null) form.ShowDialog(resolvedOwner); else form.ShowDialog();

            // Presenter and form are disposed by the controller on FormClosed.
            return result;
        }

        private IWindowView ShowModelessCore<TPresenter, TResult>(
            TPresenter presenter, IWindowView owner, Func<TPresenter, object> keySelector,
            Action<InteractionResult<TResult>> onClosed, Func<Action> initializerFactory)
            where TPresenter : IPresenter
        {
            object instanceKey;
            var existing = TryActivateExisting(presenter, keySelector, out instanceKey);
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
        /// The Initialize step for the parameterless Show overloads. A presenter that only accepts
        /// parameters (<see cref="IInitializable{TParam}"/>) would otherwise be shown uninitialized —
        /// no action handlers, no <c>OnInitialize</c> — so it is rejected up front.
        /// </summary>
        private static Action ParameterlessInitializer(IPresenter presenter)
        {
            if (presenter is IInitializable initializable) return initializable.Initialize;

            foreach (var iface in presenter.GetType().GetInterfaces())
            {
                if (iface.IsGenericType && iface.GetGenericTypeDefinition() == typeof(IInitializable<>))
                    throw new InvalidOperationException(
                        $"{presenter.GetType().Name} requires initialization parameters of type " +
                        $"{iface.GetGenericArguments()[0].Name}. Show it with an overload that takes " +
                        "parameters, e.g. Navigator.For(presenter).WithParam(parameters).");
            }
            return () => { };
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
