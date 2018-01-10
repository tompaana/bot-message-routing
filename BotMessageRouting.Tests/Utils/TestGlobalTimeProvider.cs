using System;
using Underscore.Bot.Utils;

namespace BotMessageRouting.Tests.Utils
{
    /// <summary>
    /// Test class for providing a date time that we know beforehand.
    /// </summary>
    public class TestGlobalTimeProvider : GlobalTimeProvider
    {
        private DateTime _nextDateTimeToProvide;
        public DateTime NextDateTimeToProvide
        {
            get
            {
                return _nextDateTimeToProvide;
            }
            private set
            {
                _nextDateTimeToProvide = value;
            }
        }

        public TestGlobalTimeProvider()
        {
            NextDateTimeToProvide = DateTime.Now;
        }

        public override DateTime GetCurrentTime()
        {
            DateTime dateTime = NextDateTimeToProvide;
            NextDateTimeToProvide = DateTime.Now;
            return dateTime;
        }
    }
}
