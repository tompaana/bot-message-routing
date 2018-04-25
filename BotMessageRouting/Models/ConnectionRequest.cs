using Microsoft.Bot.Schema;
using System;

namespace Underscore.Bot.Models
{
    [Serializable]
    public class ConnectionRequest
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
    }
}
