using Microsoft.Bot.Schema;
using Newtonsoft.Json;
using System;
using Underscore.Bot.MessageRouting.DataStore;

namespace Underscore.Bot.Models
{
    [Serializable]
    public class ConnectionRequest : IEquatable<ConnectionRequest>
    {
        public ConversationReference Requestor
        {
            get;
            set;
        }

        /// <summary>
        /// Represents the time when a request was made.
        /// DateTime.MinValue will indicate that no request is pending.
        /// </summary>
        public DateTime ConnectionRequestTime
        {
            get;
            set;
        }

        public ConnectionRequest(ConversationReference requestor)
        {
            Requestor = requestor;
            ResetConnectionRequestTime(); 
        }

        public void ResetConnectionRequestTime()
        {
            ConnectionRequestTime = DateTime.MinValue;
        }

        public bool Equals(ConnectionRequest other)
        {
            return (other != null
                && RoutingDataManager.HasMatchingChannelAccounts(Requestor, other.Requestor));
        }

        public static ConnectionRequest FromJson(string connectionAsJsonString)
        {
            ConnectionRequest connectionRequest = null;

            try
            {
                connectionRequest = JsonConvert.DeserializeObject<ConnectionRequest>(connectionAsJsonString);
            }
            catch (Exception)
            {
            }

            return connectionRequest;
        }

        public string ToJson()
        {
            return JsonConvert.SerializeObject(this);
        }
    }
}
