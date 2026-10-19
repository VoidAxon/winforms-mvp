namespace WinformsMVP.Common
{
    /// <summary>
    /// The default <see cref="IObjectContentComparer"/>:
    /// <see cref="ObjectComparer.ContentEquals(object, object)"/> (application types compared field
    /// by field; their own Equals is ignored).
    /// </summary>
    public sealed class BuiltInObjectContentComparer : IObjectContentComparer
    {
        /// <summary>The shared instance (stateless).</summary>
        public static readonly BuiltInObjectContentComparer Instance = new BuiltInObjectContentComparer();

        private BuiltInObjectContentComparer() { }

        /// <inheritdoc />
        public bool ContentEquals(object left, object right) => ObjectComparer.ContentEquals(left, right);
    }
}
