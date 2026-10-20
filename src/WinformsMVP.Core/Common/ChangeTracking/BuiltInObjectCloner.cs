namespace WinformsMVP.Common
{
    /// <summary>The default <see cref="IObjectCloner"/>: reflection deep copy (<see cref="ObjectCloner.DeepCopy(object)"/>).</summary>
    public sealed class BuiltInObjectCloner : IObjectCloner
    {
        /// <summary>The shared instance (stateless).</summary>
        public static readonly BuiltInObjectCloner Instance = new BuiltInObjectCloner();

        private BuiltInObjectCloner() { }

        /// <inheritdoc />
        public object DeepClone(object value) => ObjectCloner.DeepCopy(value);
    }
}
