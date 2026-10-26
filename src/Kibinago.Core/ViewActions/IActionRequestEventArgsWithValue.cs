namespace Kibinago.ViewActions
{
    /// <summary>
    /// Non-generic access to the payload of an <see cref="ActionRequestEventArgs{T}"/>, so a dispatch can
    /// forward it without knowing <c>T</c>.
    /// </summary>
    public interface IActionRequestEventArgsWithValue
    {
        /// <summary>Returns the payload, boxed.</summary>
        object GetValue();
    }
}
