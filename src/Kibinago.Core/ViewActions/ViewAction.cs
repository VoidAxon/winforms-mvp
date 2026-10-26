using System;

namespace Kibinago.ViewActions
{
    /// <summary>
    /// Immutable key that identifies a view action (e.g. <c>"OrderEdit.Save"</c>), compared by name (ordinal).
    /// Define keys once in a static actions class (<see cref="Factory"/> or <see cref="Create(string)"/>), never as raw strings at
    /// call sites. <c>default(ViewAction)</c> has no name and is rejected by <c>Register</c> / <c>Binder.Add</c>.
    /// </summary>
    public struct ViewAction : IEquatable<ViewAction>
    {
        /// <summary>The full key, including any qualifier (e.g. <c>"OrderEdit.Save"</c>); null for <c>default(ViewAction)</c>.</summary>
        public string Name { get; }
        private ViewAction(string name)
        {
            if (string.IsNullOrEmpty(name)) throw new ArgumentNullException(nameof(name));
            Name = name;
        }

        /// <summary>Creates a key with exactly this name (no qualifier is added).</summary>
        /// <param name="name">The full key, e.g. <c>"Scope.Name"</c>.</param>
        /// <exception cref="ArgumentNullException"><paramref name="name"/> is null or empty.</exception>
        public static ViewAction Create(string name) => new ViewAction(name);

        /// <summary>Creates an action key whose payload is a <typeparamref name="T"/>.</summary>
        public static ViewAction<T> Create<T>(string name) => new ViewAction<T>(new ViewAction(name));
        /// <summary>
        /// A factory without a qualifier; call <see cref="ViewActionFactory.WithQualifier(string[])"/> on it to name a screen's
        /// scope once: <c>ViewAction.Factory.WithQualifier("OrderEdit")</c>.
        /// </summary>
        public static ViewActionFactory Factory { get; } = new ViewActionFactory();

        /// <summary>Keys are equal when their names are equal (ordinal).</summary>
        /// <param name="other">The key to compare with.</param>
        public bool Equals(ViewAction other) => string.Equals(Name, other.Name, StringComparison.Ordinal);
        /// <inheritdoc />
        public override bool Equals(object obj) => obj is ViewAction other && Equals(other);

        /// <summary>Equal when the names are equal (ordinal).</summary>
        public static bool operator ==(ViewAction left, ViewAction right) => left.Equals(right);
        /// <summary>Not equal when the names differ (ordinal).</summary>
        public static bool operator !=(ViewAction left, ViewAction right) => !left.Equals(right);

        /// <inheritdoc />
        public override int GetHashCode()
            => Name?.GetHashCode() ?? 0;

        /// <summary>Returns <see cref="Name"/>.</summary>
        public override string ToString() => Name;
    }
}
