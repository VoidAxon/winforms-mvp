using System;
using System.Collections.Generic;
using Kibinago.Common;
using Kibinago.Presenters;
using Kibinago.Views;
using Kibinago.Services;

namespace Kibinago.Testing
{
    /// <summary>What a modal responder receives (see <see cref="FakeWindowNavigator.RespondTo{TPresenter, TResult}"/>).</summary>
    public sealed class ModalRequest<TPresenter>
    {
        internal ModalRequest(TPresenter presenter, object parameters, bool hasParameters, IWindowView owner)
        {
            Presenter = presenter;
            Parameters = parameters;
            HasParameters = hasParameters;
            Owner = owner;
        }

        /// <summary>The presenter passed (not attached or initialized).</summary>
        public TPresenter Presenter { get; }

        /// <summary>The parameters passed, or null for a parameterless overload.</summary>
        public object Parameters { get; }

        /// <summary>Whether a parameterized overload was called.</summary>
        public bool HasParameters { get; }

        /// <summary>The owner passed, or null.</summary>
        public IWindowView Owner { get; }
    }

    /// <summary>One recorded navigator request.</summary>
    public sealed class NavigationCall
    {
        internal NavigationCall(object presenter, bool modal, object parameters, bool hasParameters,
            Type resultType, IWindowView owner, object key, bool activated)
        {
            Presenter = presenter;
            PresenterType = presenter.GetType();
            Modal = modal;
            Parameters = parameters;
            HasParameters = hasParameters;
            ResultType = resultType;
            Owner = owner;
            Key = key;
            Activated = activated;
        }

        /// <summary>The presenter passed.</summary>
        public object Presenter { get; }

        /// <summary>The presenter's runtime type.</summary>
        public Type PresenterType { get; }

        /// <summary>True for <c>ShowWindowAsModal</c>, false for <c>ShowWindow</c>.</summary>
        public bool Modal { get; }

        /// <summary>The parameters passed, or null for a parameterless overload.</summary>
        public object Parameters { get; }

        /// <summary>Whether a parameterized overload was called.</summary>
        public bool HasParameters { get; }

        /// <summary>The result type the caller expects, or null for an overload without a result.</summary>
        public Type ResultType { get; }

        /// <summary>The owner passed, or null.</summary>
        public IWindowView Owner { get; }

        /// <summary>The key from the caller's key selector (non-modal), or null.</summary>
        public object Key { get; }

        /// <summary>True when the request activated an open window with the same key instead of opening one.</summary>
        public bool Activated { get; }

        /// <inheritdoc />
        public override string ToString() => (Modal ? "Modal " : "Window ") + PresenterType.Name;
    }

    /// <summary>
    /// <see cref="IWindowNavigator"/> fake that follows <c>WindowNavigator</c>'s rules without Forms.
    /// Presenters passed in are not attached or initialized; to test the child screen itself, host
    /// a separate instance with <see cref="PresenterTestHost"/>.
    /// </summary>
    /// <remarks>
    /// <para><b>Modal:</b> the answer comes from a responder registered with <see cref="RespondTo{TPresenter, TResult}"/>
    /// (the last one whose presenter type matches wins); an unscripted modal throws
    /// <see cref="UnscriptedInteractionException"/> unless <see cref="CancelUnscripted"/> was called. The
    /// presenter is disposed afterwards, as when a real modal window closes.</para>
    /// <para><b>Non-modal:</b> each call opens a <see cref="FakeWindow"/> (see <see cref="Windows"/>) that
    /// the test closes with a result. Keys are scoped by presenter type; the same key while open
    /// activates that window and disposes the new presenter.</para>
    /// <para>A presenter that implements only <c>IInitializable&lt;TParam&gt;</c> shown through a
    /// parameterless overload throws <see cref="InvalidOperationException"/>, as in production.</para>
    /// <code>
    /// fakes.Navigator.RespondTo&lt;CustomerSearchPresenter, Customer&gt;(r => InteractionResult&lt;Customer&gt;.Ok(alice));
    /// fakes.Navigator.Windows.Single().CloseWithResult(42);
    /// </code>
    /// </remarks>
    public class FakeWindowNavigator : IWindowNavigator
    {
        private readonly object _gate = new object();
        private readonly List<NavigationCall> _calls = new List<NavigationCall>();
        private readonly List<FakeWindow> _windows = new List<FakeWindow>();
        private readonly Dictionary<object, FakeWindow> _open = new Dictionary<object, FakeWindow>();
        private readonly List<Responder> _responders = new List<Responder>();
        private bool _cancelUnscripted;

        /// <summary>Every request so far, in order, including activations (a snapshot).</summary>
        public IReadOnlyList<NavigationCall> Calls
        {
            get { lock (_gate) return _calls.ToArray(); }
        }

        /// <summary>Every non-modal window opened so far, closed ones included (a snapshot).</summary>
        public IReadOnlyList<FakeWindow> Windows
        {
            get { lock (_gate) return _windows.ToArray(); }
        }

        /// <summary>
        /// Answers modal requests for presenters of type <typeparamref name="TPresenter"/> (or derived).
        /// A result of a type other than the caller's expected type is converted as production does:
        /// a value of the wrong type becomes <see cref="InteractionStatus.Error"/>.
        /// </summary>
        public void RespondTo<TPresenter, TResult>(Func<ModalRequest<TPresenter>, InteractionResult<TResult>> responder)
        {
            if (responder == null) throw new ArgumentNullException(nameof(responder));
            AddResponder(typeof(TPresenter), (p, param, has, owner) =>
            {
                var r = responder(new ModalRequest<TPresenter>((TPresenter)p, param, has, owner));
                return r == null ? null : new Answer(r.Status, r.IsOk ? (object)r.Value : null, r.ErrorMessage, r.Exception);
            });
        }

        /// <summary>
        /// Answers modal requests for presenters of type <typeparamref name="TPresenter"/> from the parameters
        /// they were shown with, typed: <c>RespondTo&lt;PickerPresenter, PickerParameters, int&gt;(p =&gt; ...)</c>.
        /// The presenter must take <typeparamref name="TParam"/> (checked at compile time); a request without
        /// such parameters throws <see cref="InvalidOperationException"/>.
        /// </summary>
        public void RespondTo<TPresenter, TParam, TResult>(Func<TParam, InteractionResult<TResult>> responder)
            where TPresenter : IInitializable<TParam>
        {
            if (responder == null) throw new ArgumentNullException(nameof(responder));
            RespondTo<TPresenter, TResult>(r =>
            {
                if (r.HasParameters && r.Parameters == null) return responder(default(TParam));   // WithParam(null)
                if (!r.HasParameters || !(r.Parameters is TParam parameters))
                    throw new InvalidOperationException(
                        $"{typeof(TPresenter).Name} was shown without {typeof(TParam).Name} parameters, but the responder expects them " +
                        "(use RespondTo<TPresenter, TResult> for a parameterless request).");
                return responder(parameters);
            });
        }

        /// <summary>Answers modal requests without a result value for presenters of type <typeparamref name="TPresenter"/>.</summary>
        public void RespondTo<TPresenter>(Func<ModalRequest<TPresenter>, InteractionResult> responder)
        {
            if (responder == null) throw new ArgumentNullException(nameof(responder));
            AddResponder(typeof(TPresenter), (p, param, has, owner) =>
            {
                var r = responder(new ModalRequest<TPresenter>((TPresenter)p, param, has, owner));
                return r == null ? null : new Answer(r.Status, null, r.ErrorMessage, r.Exception);
            });
        }

        /// <summary>Answers every unscripted modal request with Cancel instead of throwing.</summary>
        public void CancelUnscripted()
        {
            lock (_gate) _cancelUnscripted = true;
        }

        /// <summary>The parameters of the most recent modal request.</summary>
        /// <exception cref="InvalidOperationException">No modal request was made.</exception>
        public T LastModalParameters<T>()
        {
            lock (_gate)
            {
                for (int i = _calls.Count - 1; i >= 0; i--)
                    if (_calls[i].Modal) return (T)_calls[i].Parameters;
            }
            throw new InvalidOperationException("No modal window was requested.");
        }

        #region IWindowNavigator

        /// <inheritdoc />
        public virtual IWindowView ShowWindow<TPresenter, TResult>(TPresenter presenter, IWindowView owner = null,
            Func<TPresenter, object> keySelector = null, Action<InteractionResult<TResult>> onClosed = null)
            where TPresenter : IPresenter
            => ShowModelessCore(presenter, null, false, true, owner, keySelector, onClosed);

        /// <inheritdoc />
        public virtual IWindowView ShowWindow<TPresenter>(TPresenter presenter, IWindowView owner = null,
            Func<TPresenter, object> keySelector = null)
            where TPresenter : IPresenter
            => ShowModelessCore<TPresenter, object>(presenter, null, false, false, owner, keySelector, null);

        /// <inheritdoc />
        public virtual InteractionResult ShowWindowAsModal<TPresenter>(TPresenter presenter, IWindowView owner = null)
            where TPresenter : IPresenter
            => ShowModalCore<object>(presenter, null, false, false, owner);

        /// <inheritdoc />
        public virtual InteractionResult<TResult> ShowWindowAsModal<TPresenter, TResult>(TPresenter presenter, IWindowView owner = null)
            where TPresenter : IPresenter
            => ShowModalCore<TResult>(presenter, null, false, true, owner);

        /// <inheritdoc />
        public virtual IWindowView ShowWindow<TPresenter, TParam, TResult>(TPresenter presenter, TParam parameters,
            IWindowView owner = null, Func<TPresenter, object> keySelector = null, Action<InteractionResult<TResult>> onClosed = null)
            where TPresenter : IPresenter, IInitializable<TParam>
            => ShowModelessCore(presenter, parameters, true, true, owner, keySelector, onClosed);

        /// <inheritdoc />
        public virtual IWindowView ShowWindow<TPresenter, TParam>(TPresenter presenter, TParam parameters,
            IWindowView owner = null, Func<TPresenter, object> keySelector = null)
            where TPresenter : IPresenter, IInitializable<TParam>
            => ShowModelessCore<TPresenter, object>(presenter, parameters, true, false, owner, keySelector, null);

        /// <inheritdoc />
        public virtual InteractionResult ShowWindowAsModal<TPresenter, TParam>(TPresenter presenter, TParam parameters,
            IWindowView owner = null)
            where TPresenter : IPresenter, IInitializable<TParam>
            => ShowModalCore<object>(presenter, parameters, true, false, owner);

        /// <inheritdoc />
        public virtual InteractionResult<TResult> ShowWindowAsModal<TPresenter, TParam, TResult>(TPresenter presenter,
            TParam parameters, IWindowView owner = null)
            where TPresenter : IPresenter, IInitializable<TParam>
            => ShowModalCore<TResult>(presenter, parameters, true, true, owner);

        #endregion

        /// <summary>
        /// Every modal overload ends here. <paramref name="hasResult"/> is false for the overloads
        /// without a result type (they pass <c>object</c>).
        /// </summary>
        protected virtual InteractionResult<TResult> ShowModalCore<TResult>(IPresenter presenter, object parameters,
            bool hasParameters, bool hasResult, IWindowView owner)
        {
            if (presenter == null) throw new ArgumentNullException(nameof(presenter));
            try
            {
                Validate(presenter, hasParameters);
                Func<IPresenter, object, bool, IWindowView, Answer> responder;
                bool cancelUnscripted;
                lock (_gate)
                {
                    _calls.Add(new NavigationCall(presenter, true, parameters, hasParameters,
                        hasResult ? typeof(TResult) : null, owner, null, false));
                    responder = FindResponder(presenter.GetType());
                    cancelUnscripted = _cancelUnscripted;
                }

                if (responder == null)
                {
                    if (cancelUnscripted) return InteractionResult<TResult>.Cancel();
                    throw new UnscriptedInteractionException(
                        $"Unscripted modal window for {presenter.GetType().Name}. Script it with " +
                        $"RespondTo<{presenter.GetType().Name}, TResult>(...), or call CancelUnscripted().");
                }

                var answer = responder(presenter, parameters, hasParameters, owner)
                    ?? throw new InvalidOperationException(
                        $"The responder for {presenter.GetType().Name} returned null; return an InteractionResult.");
                return Convert<TResult>(answer);
            }
            finally
            {
                presenter.Dispose();   // production disposes the presenter when the modal window closes
            }
        }

        /// <summary>
        /// Every non-modal overload ends here. <paramref name="hasResult"/> is false for the
        /// overloads without a result type.
        /// </summary>
        protected virtual IWindowView ShowModelessCore<TPresenter, TResult>(TPresenter presenter, object parameters,
            bool hasParameters, bool hasResult, IWindowView owner, Func<TPresenter, object> keySelector,
            Action<InteractionResult<TResult>> onClosed)
            where TPresenter : IPresenter
        {
            if (presenter == null) throw new ArgumentNullException(nameof(presenter));

            var key = keySelector?.Invoke(presenter);
            var instanceKey = key == null ? null : (object)(presenter.GetType(), key);
            var resultType = hasResult ? typeof(TResult) : null;

            lock (_gate)
            {
                if (instanceKey != null && _open.TryGetValue(instanceKey, out var existing))
                {
                    existing.Activations++;
                    _calls.Add(new NavigationCall(presenter, false, parameters, hasParameters, resultType, owner, key, true));
                    presenter.Dispose();
                    return existing.View;
                }
            }

            try
            {
                Validate(presenter, hasParameters);
            }
            catch
            {
                presenter.Dispose();
                throw;
            }

            var window = new FakeWindow(presenter, parameters, hasParameters, key, owner,
                (result, status) => onClosed?.Invoke(Convert<TResult>(result, status)),
                w => Release(instanceKey, w));
            lock (_gate)
            {
                _calls.Add(new NavigationCall(presenter, false, parameters, hasParameters, resultType, owner, key, false));
                _windows.Add(window);
                if (instanceKey != null) _open[instanceKey] = window;
            }
            return window.View;
        }

        private void Release(object instanceKey, FakeWindow window)
        {
            if (instanceKey == null) return;
            lock (_gate)
            {
                if (_open.TryGetValue(instanceKey, out var mapped) && ReferenceEquals(mapped, window))
                    _open.Remove(instanceKey);
            }
        }

        // Same checks as WindowNavigator.PrepareForm, before anything is shown.
        private static void Validate(IPresenter presenter, bool hasParameters)
        {
            if (!(presenter is ICloseParticipant))
                throw new InvalidOperationException(
                    presenter.GetType().Name + " cannot be shown by WindowNavigator because it does not " +
                    "derive from WindowPresenterBase / WindowPresenterBaseCore (no close participant).");
            if (hasParameters || presenter is IInitializable) return;
            foreach (var iface in presenter.GetType().GetInterfaces())
            {
                if (iface.IsGenericType && iface.GetGenericTypeDefinition() == typeof(IInitializable<>))
                    throw new InvalidOperationException(
                        $"{presenter.GetType().Name} requires initialization parameters of type " +
                        $"{iface.GetGenericArguments()[0].Name}. Show it with an overload that takes " +
                        "parameters, e.g. Navigator.For(presenter).WithParam(parameters).");
            }
        }

        private void AddResponder(Type presenterType, Func<IPresenter, object, bool, IWindowView, Answer> respond)
        {
            lock (_gate) _responders.Add(new Responder(presenterType, respond));
        }

        private Func<IPresenter, object, bool, IWindowView, Answer> FindResponder(Type presenterType)
        {
            for (int i = _responders.Count - 1; i >= 0; i--)
                if (_responders[i].PresenterType.IsAssignableFrom(presenterType)) return _responders[i].Respond;
            return null;
        }

        private static InteractionResult<TResult> Convert<TResult>(Answer answer)
        {
            if (answer.Status == InteractionStatus.Error)
                return InteractionResult<TResult>.Error(answer.ErrorMessage, answer.Exception);
            return CloseResults.Build<TResult>(answer.Value, answer.Status);
        }

        private static InteractionResult<TResult> Convert<TResult>(object result, InteractionStatus status)
        {
            if (result is FakeWindow.CloseError error)
                return InteractionResult<TResult>.Error(error.Message ?? error.Exception?.Message, error.Exception);
            return CloseResults.Build<TResult>(result, status);
        }

        private sealed class Responder
        {
            public Responder(Type presenterType, Func<IPresenter, object, bool, IWindowView, Answer> respond)
            {
                PresenterType = presenterType;
                Respond = respond;
            }

            public Type PresenterType { get; }
            public Func<IPresenter, object, bool, IWindowView, Answer> Respond { get; }
        }

        private sealed class Answer
        {
            public Answer(InteractionStatus status, object value, string errorMessage, Exception exception)
            {
                Status = status;
                Value = value;
                ErrorMessage = errorMessage;
                Exception = exception;
            }

            public InteractionStatus Status { get; }
            public object Value { get; }
            public string ErrorMessage { get; }
            public Exception Exception { get; }
        }
    }
}
