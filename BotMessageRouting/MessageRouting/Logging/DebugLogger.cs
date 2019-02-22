﻿using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Underscore.Bot.MessageRouting.Logging
{
    public class DebugLogger : ILogger
    {
        public void Log(string message, [CallerMemberName] string methodName = "")
        {
            if (!string.IsNullOrWhiteSpace(methodName))
            {
                message = $"{methodName}: {message}";
            }

            Debug.WriteLine($"{DateTime.Now}> {message}");
        }
    }
}