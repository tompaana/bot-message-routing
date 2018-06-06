using BotMessageRouting.MessageRouting.Handlers;
using BotMessageRouting.MessageRouting.Logging;
using Microsoft.Bot.Schema;
using Newtonsoft.Json;
using System;
using Underscore.Bot.MessageRouting.DataStore;

namespace Underscore.Bot.MessageRouting.Models
{
    [Serializable]
    public class ConnectionRequest : IEquatable<ConnectionRequest>
    {
        private static ILogger _logger                    = DebugLogger.Default;
        private static ExceptionHandler _exceptionHandler = new ExceptionHandler(_logger);

        public ConversationReference Requestor{ get; set; }

        /// <summary>
        /// Represents the time when a request was made.
        /// DateTime.MinValue will indicate that no request is pending.
        /// </summary>
        public DateTime ConnectionRequestTime { get; set; }

        public ConnectionRequest(ConversationReference requestor, ILogger logger = null)
        {
            if (_exceptionHandler == null)
            {
                _logger           = logger ?? DebugLogger.Default;
                _exceptionHandler = new ExceptionHandler(logger ?? _logger);
            }
            _logger.Enter();
            Requestor         = requestor;
            ResetConnectionRequestTime(); 
        }

        public void ResetConnectionRequestTime()
        {
            _logger.Enter();
            ConnectionRequestTime = DateTime.MinValue;
        }

        public bool Equals(ConnectionRequest other)
        {
            _logger.Enter();
            return (other != null && RoutingDataManager.Match(Requestor, other.Requestor));
        }

        public static ConnectionRequest FromJson(string connectionAsJsonString)
        {
            _logger.Enter();

            return _exceptionHandler.Get(() => JsonConvert.DeserializeObject<ConnectionRequest>(connectionAsJsonString));
        }

        public string ToJson()
        {
            _logger.Enter();

            return JsonConvert.SerializeObject(this);
        }

        public override string ToString()
        {
            _logger.Enter();

            return ToJson();
        }
    }
}
