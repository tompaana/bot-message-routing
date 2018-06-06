using BotMessageRouting.MessageRouting.Logging;
using Microsoft.Bot.Schema;
using Newtonsoft.Json;
using System;
using Underscore.Bot.MessageRouting.DataStore;

namespace Underscore.Bot.MessageRouting.Models
{
    [Serializable]
    public class Connection : IEquatable<Connection>
    {
        private readonly ILogger _logger;

        /// <summary>
        /// Represents the last time in which user and agent interact in the connection.
        /// </summary>
        public DateTime TimeSinceLastActivity{ get; set; }

        public ConversationReference ConversationReference1 { get; set; }

        public ConversationReference ConversationReference2 { get; set; }

        public Connection(ConversationReference conversationReference1, ConversationReference conversationReference2, ILogger logger = null)
        {
            _logger                = logger ?? DebugLogger.Default;
            _logger.Enter();

            ConversationReference1 = conversationReference1;
            ConversationReference2 = conversationReference2;
            TimeSinceLastActivity  = DateTime.Now;
        }

        /// <summary>
        /// Checks if the given connection matches this one.
        /// </summary>
        /// <param name="other">The other connection.</param>
        /// <returns>True, if the connections are match. False otherwise.</returns>
        public bool Equals(Connection other)
        {
            _logger.Enter();

            return (other != null
                && ((RoutingDataManager.Match(ConversationReference1, other.ConversationReference1)
                     && RoutingDataManager.Match(ConversationReference2, other.ConversationReference2))
                     || (RoutingDataManager.Match(ConversationReference1, other.ConversationReference2)
                         && RoutingDataManager.Match(ConversationReference2, other.ConversationReference1))));
        }

        public static Connection FromJson(string connectionAsJsonString)
        {            
            Connection connection = null;

            try
            {
                connection = JsonConvert.DeserializeObject<Connection>(connectionAsJsonString);
            }
            catch (Exception)
            {
            }

            return connection;
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
