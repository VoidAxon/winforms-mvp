using System;

namespace WinformsMVP.MVP.ViewActions
{
    public struct ViewAction : IEquatable<ViewAction>
    {
        public string Name { get; }
        private ViewAction(string name)
        {
            if (string.IsNullOrEmpty(name)) throw new ArgumentNullException(nameof(name));
            Name = name;
        }

        public static ViewAction Create(string name) => new ViewAction(name);
        public static ViewActionFactory Factory { get; } = new ViewActionFactory();

        public ViewAction WithQualifier(ViewActionFactory factory) => factory.Create(Name);

        public ViewAction WithSuffix(string suffix, string sep = "_")
            => new ViewAction($"{Name}{sep}{suffix}");

        public bool Equals(ViewAction other) => string.Equals(Name, other.Name, StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is ViewAction other && Equals(other);

        public static bool operator ==(ViewAction left, ViewAction right) => left.Equals(right);
        public static bool operator !=(ViewAction left, ViewAction right) => !left.Equals(right);

        public override int GetHashCode()
            => Name?.GetHashCode() ?? 0;

        public override string ToString() => Name;
    }
}
