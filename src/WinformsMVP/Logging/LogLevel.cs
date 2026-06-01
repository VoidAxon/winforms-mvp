namespace WinformsMVP.Logging
{
    /// <summary>
    /// Log severity levels. Values match Microsoft.Extensions.Logging.LogLevel so a
    /// thin application-level adapter can map between the two by cast.
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
