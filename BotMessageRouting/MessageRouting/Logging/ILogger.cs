using System;
using System.Runtime.CompilerServices;

namespace BotMessageRouting.MessageRouting.Logging
{
    /// <summary>
    /// Logging for use with MessageRouting
    /// </summary>
    public interface ILogger
    {
        void SetLogLevel(LogLevel logLevel);

        /// <summary>
        /// Used to log entry to a function. The methodname is resolved by using [CallerMemberName] and does not need to be provided
        /// </summary>
        /// <param name="methodName"></param>
        void Enter([CallerMemberName] string methodName = "");

        /// <summary>
        /// Log messages in verbose level or above
        /// </summary>
        /// <param name="message">The message to log</param>
        /// <param name="methodName">Resolved by the [CallerMemberName] attribute. No value required</param>
        void LogVerbose(string message, [CallerMemberName] string methodName = "");

        /// <summary>
        /// Log messages in Information level or above
        /// </summary>
        /// <param name="message">The message to log</param>
        /// <param name="methodName">Resolved by the [CallerMemberName] attribute. No value required</param>
        void LogInformation(string message, [CallerMemberName] string methodName = "");

        /// <summary>
        /// Log messages in Warning level or above
        /// </summary>
        /// <param name="message">The message to log</param>
        /// <param name="methodName">Resolved by the [CallerMemberName] attribute. No value required</param>
        void LogWarning(string message, [CallerMemberName] string methodName = "");

        /// <summary>
        /// Log messages in Error level or above
        /// </summary>
        /// <param name="message">The message to log</param>
        /// <param name="methodName">Resolved by the [CallerMemberName] attribute. No value required</param>
        void LogError(string message, [CallerMemberName] string methodName = "");

        /// <summary>
        /// Used for logging exceptions (see class ExceptionHandler for sample usage)
        /// </summary>
        /// <param name="ex">The exception to be logged</param>
        /// <param name="message">Additional text to be logged, prior to logging the exception message</param>
        /// <param name="methodName">Resolved by the [CallerMemberName] attribute. No value required</param>
        void LogException(Exception ex, string message = "", [CallerMemberName] string methodName = "");
    }
}
