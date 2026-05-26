namespace WinformsMVP.Logging
{
    /// <summary>
    /// Log severity levels. Values match Microsoft.Extensions.Logging.LogLevel for
    /// straightforward mapping by the optional adapter package.
    /// </summary>
    public enum LogLevel
    {
        Trace = 0,
        Debug = 1,
        Information = 2,
        Warning = 3,
        Error = 4,
        Critical = 5,
        None = 6,
    }
}
