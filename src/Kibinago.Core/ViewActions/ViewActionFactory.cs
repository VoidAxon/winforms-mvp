using System;
using System.Linq;

namespace Kibinago.ViewActions
{
    public readonly struct ViewActionFactory
    {
        private readonly string _qualifier;

        internal ViewActionFactory(string qualifier = null)
        {
            _qualifier = qualifier ?? string.Empty;
        }

        public ViewActionFactory WithQualifier(params string[] qualifiers)
        {
            if (qualifiers == null || qualifiers.Length == 0) return this;

            var newQualifier = string.Join(".", qualifiers.Where(q => !string.IsNullOrWhiteSpace(q)));
            if (string.IsNullOrEmpty(newQualifier)) return this;

            if (string.IsNullOrEmpty(_qualifier))
                return new ViewActionFactory(newQualifier);

            return new ViewActionFactory($"{_qualifier}.{newQualifier}");
        }

        public ViewAction Create(string name)
        {
            if (string.IsNullOrEmpty(name)) throw new ArgumentNullException(nameof(name));
            return string.IsNullOrEmpty(_qualifier)
                ? ViewAction.Create(name)
                : ViewAction.Create($"{_qualifier}.{name}");
        }

        /// <summary>Creates a qualified action key whose payload is a <typeparamref name="T"/>.</summary>
        public ViewAction<T> Create<T>(string name) => new ViewAction<T>(Create(name));

        public override string ToString() => _qualifier;
    }
}
