using Microsoft.Bot.Schema;
using System;

namespace Underscore.Bot.Models
{
    [Serializable]
    public class Connection
    {
        /// <summary>
        /// Represents the last time in which user and agent interact in the connection.
        /// TODO: We had to change the value every time there is an interaction between the two.
        /// </summary>
        public DateTime LastInteractionTime
        {
            get;
            set;
        }

        public ConversationReference Requestor
        {
            get;
            set;
        }

        public ConversationReference Approver
        {
            get;
            set;
        }

        public Connection(ConversationReference requestor, ConversationReference approver)
        {
            Requestor = requestor;
            Approver = approver;
            LastInteractionTime = DateTime.Now;
        }
    }
}
