using System;
using System.Linq;

namespace Kibinago.ViewActions
{
    /// <summary>
    /// Creates <see cref="ViewAction"/> keys under a dot-separated qualifier, so a screen's actions class names its
    /// scope once: <c>Factory = ViewAction.Factory.WithQualifier("OrderEdit")</c>, then
    /// <c>Save = Factory.Create(nameof(Save))</c> gives the key <c>"OrderEdit.Save"</c>. Immutable.
    /// </summary>
    public readonly struct ViewActionFactory
    {
        private readonly string _qualifier;

        internal ViewActionFactory(string qualifier = null)
        {
            _qualifier = qualifier ?? string.Empty;
        }

        /// <summary>
        /// Returns a factory whose qualifier is this one's followed by <paramref name="qualifiers"/>, joined with dots;
        /// blank qualifiers are skipped, and with none left this factory is returned unchanged.
        /// </summary>
        /// <param name="qualifiers">The qualifier segments to append.</param>
        public ViewActionFactory WithQualifier(params string[] qualifiers)
        {
            if (qualifiers == null || qualifiers.Length == 0) return this;

            var newQualifier = string.Join(".", qualifiers.Where(q => !string.IsNullOrWhiteSpace(q)));
            if (string.IsNullOrEmpty(newQualifier)) return this;

            if (string.IsNullOrEmpty(_qualifier))
                return new ViewActionFactory(newQualifier);

            return new ViewActionFactory($"{_qualifier}.{newQualifier}");
        }

        /// <summary>Creates the key <c>qualifier.name</c> (just <paramref name="name"/> when there is no qualifier).</summary>
        /// <param name="name">The action name, usually <c>nameof(Field)</c>.</param>
        /// <exception cref="ArgumentNullException"><paramref name="name"/> is null or empty.</exception>
        public ViewAction Create(string name)
        {
            if (string.IsNullOrEmpty(name)) throw new ArgumentNullException(nameof(name));
            return string.IsNullOrEmpty(_qualifier)
                ? ViewAction.Create(name)
                : ViewAction.Create($"{_qualifier}.{name}");
        }

        /// <summary>Creates a qualified action key whose payload is a <typeparamref name="T"/>.</summary>
        public ViewAction<T> Create<T>(string name) => new ViewAction<T>(Create(name));

        /// <summary>Returns the qualifier (empty when there is none).</summary>
        public override string ToString() => _qualifier;
    }
}
