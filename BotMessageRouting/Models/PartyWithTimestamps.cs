using System;
using Microsoft.Bot.Connector;

namespace Underscore.Bot.Models
{
    /// <summary>
    /// Like Party, but with timestamps to mark times for when requests were made etc.
    /// </summary>
    [Serializable]
    public class PartyWithTimestamps : Party
    {
        /// <summary>
        /// Represents the time when a request was made.
        /// DateTime.MinValue will indicate that no request is pending.
        /// </summary>
        public DateTime RequestMadeTime
        {
            get;
            set;
        }

        /// <summary>
        /// Represents the time when an engagement (1:1 conversation) was started.
        /// DateTime.MinValue will indicate that this party is not engaged in a conversation.
        /// </summary>
        public DateTime EngagementStartedTime
        {
            get;
            set;
        }

        /// <summary>
        /// Constructor.
        /// </summary>
        public PartyWithTimestamps(string serviceUrl, string channelId,
            ChannelAccount channelAccount, ConversationAccount conversationAccount)
            : base(serviceUrl, channelId, channelAccount, conversationAccount)
        {
            ResetRequestMadeTime();
            ResetEngagementStartedTime();
        }

        public void ResetRequestMadeTime()
        {
            RequestMadeTime = DateTime.MinValue;
        }

        public void ResetEngagementStartedTime()
        {
            EngagementStartedTime = DateTime.MinValue;
        }
    }
}
