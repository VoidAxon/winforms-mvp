using System;

namespace Kibinago.ViewActions
{
    /// <summary>
    /// An action key that carries the type of its payload. The Form side
    /// (<c>Binder.Add(key, control, c =&gt; value)</c>) and the presenter side
    /// (<c>Dispatcher.Register(key, (T value) =&gt; ...)</c>) are then checked against the same
    /// <typeparamref name="T"/> by the compiler, instead of a mismatch surfacing at dispatch time.
    /// </summary>
    /// <remarks>
    /// Create with <see cref="ViewAction.Create{T}(string)"/> or
    /// <see cref="ViewActionFactory.Create{T}(string)"/>. There is deliberately no implicit
    /// conversion to <see cref="ViewAction"/>: an untyped overload must not silently accept a typed
    /// key with a payload of another type. Use <see cref="Key"/> where an untyped key is needed
    /// (the action observer, logs, the explicit pattern).
    /// </remarks>
    /// <typeparam name="T">The payload type.</typeparam>
    public readonly struct ViewAction<T> : IEquatable<ViewAction<T>>
    {
        internal ViewAction(ViewAction key)
        {
            Key = key;
        }

        /// <summary>The untyped key (same name); what the action observer and the explicit pattern see.</summary>
        public ViewAction Key { get; }

        /// <summary>The action name.</summary>
        public string Name => Key.Name;

        /// <summary>Keys are equal when their untyped <see cref="Key"/>s are equal.</summary>
        /// <param name="other">The key to compare with.</param>
        public bool Equals(ViewAction<T> other) => Key.Equals(other.Key);
        /// <inheritdoc />
        public override bool Equals(object obj) => obj is ViewAction<T> other && Equals(other);
        /// <inheritdoc />
        public override int GetHashCode() => Key.GetHashCode();
        /// <summary>Returns <see cref="Name"/>.</summary>
        public override string ToString() => Key.ToString();

        /// <summary>Equal when the untyped keys are equal.</summary>
        public static bool operator ==(ViewAction<T> left, ViewAction<T> right) => left.Equals(right);
        /// <summary>Not equal when the untyped keys differ.</summary>
        public static bool operator !=(ViewAction<T> left, ViewAction<T> right) => !left.Equals(right);
    }
}
