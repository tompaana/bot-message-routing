namespace BotMessageRouting.MessageRouting.Logging
{
    /// <summary>
    /// Used by the ILogger to set loglevel. 
    /// </summary>
    public enum LogLevel:int
    {
        Unknown     = 0,
        Verbose     = 1,
        Information = 2,
        Warning     = 3, 
        Error       = 4, 
        Exception   = 5
    }
}
