using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace BotMessageRouting.MessageRouting.Logging
{
    public class DebugLogger : ILogger
    {
        private LogLevel _logLevel;

        /// <summary>
        /// The Default Logger uses System.Diagnostics.Debug.Writeline() as it's output
        /// </summary>
        public static ILogger Default = new DebugLogger(LogLevel.Verbose);

        public DebugLogger(LogLevel logLevel = LogLevel.Verbose)
        {
            SetLogLevel(logLevel);
        }


        public void Enter([CallerMemberName] string methodName = "")
        {
            if (_logLevel == LogLevel.Verbose)
                Log($"Entering {methodName}()");
        }


        public void LogError(string message, [CallerMemberName] string methodName = "")
        {
            if (_logLevel >= LogLevel.Error)
                Log($"ERROR: {methodName}(): {message}");
        }


        public void LogException(Exception ex, string message = "", [CallerMemberName] string methodName = "")
        {
            if (_logLevel >= LogLevel.Exception)
            {
                if (string.IsNullOrEmpty(message))
                    Log($"EXCEPTION: {methodName}(): {ex.Message}");
                else
                    Log($"EXCEPTION: {methodName}(): {message} (exception: {ex.Message})");
            }
        }


        public void LogInformation(string message, [CallerMemberName] string methodName = "")
        {
            if (_logLevel >= LogLevel.Information)
                Log($"Information: {methodName}(): {message}");
        }


        public void LogVerbose(string message, [CallerMemberName] string methodName = "")
        {
            if (_logLevel >= LogLevel.Verbose)
                Log($"Information: {methodName}(): {message}");
        }


        public void LogWarning(string message, [CallerMemberName] string methodName = "")
        {
            if (_logLevel >= LogLevel.Warning)
                Log($"Warning: {methodName}(): {message}");
        }


        public void SetLogLevel(LogLevel logLevel)
        {
            if (logLevel == LogLevel.Unknown)
                throw new ArgumentException("LogLevel was not set");

            _logLevel = logLevel;
        }


        private void Log(string message)
        {
            Debug.WriteLine($"{DateTime.Now}> {message}");
        }
    }
}
