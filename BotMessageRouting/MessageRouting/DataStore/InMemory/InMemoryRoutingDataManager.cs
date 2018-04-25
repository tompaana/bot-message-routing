using System;
using System.Collections.Generic;
using Underscore.Bot.Models;
using Underscore.Bot.Utils;

namespace Underscore.Bot.MessageRouting.DataStore.Local
{
    /// <summary>
    /// Routing data manager that stores the data locally.
    /// 
    /// NOTE: USE THIS CLASS ONLY FOR TESTING!
    /// Storing the data like this in production would not work since the bot can and likely will
    /// have multiple instances.
    /// 
    /// See IRoutingDataManager and AbstractRoutingDataManager for general documentation of
    /// properties and methods.
    /// </summary>
    [Serializable]
    public class LocalRoutingDataManager : AbstractRoutingDataManager
    {
        /// <summary>
        /// Parties that are users (not this bot).
        /// </summary>
        protected IList<ConversationReference> UserParties
        {
            get;
            set;
        }

        /// <summary>
        /// If the bot is addressed from different channels, its identity in terms of ID and name
        /// can vary. Those different identities are stored in this list.
        /// </summary>
        protected IList<ConversationReference> BotParties
        {
            get;
            set;
        }

        /// <summary>
        /// Represents the channels (and the specific conversations e.g. specific channel in Slack),
        /// where the chat requests are directed. For instance, a channel could be where the
        /// customer service agents accept customer chat requests. 
        /// </summary>
        protected IList<ConversationReference> AggregationParties
        {
            get;
            set;
        }

        /// <summary>
        /// The list of parties waiting for their (conversation) requests to be accepted.
        /// </summary>
        protected List<ConversationReference> PendingRequests
        {
            get;
            set;
        }

        /// <summary>
        /// Contains 1:1 associations between parties i.e. parties connected in a conversation.
        /// Furthermore, the key ConversationReference is considered to be the conversation owner e.g. in
        /// a customer service situation the customer service agent.
        /// </summary>
        protected Dictionary<ConversationReference, ConversationReference> ConnectedParties
        {
            get;
            set;
        }

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="globalTimeProvider">The global time provider for providing the current
        /// time for various events such as when a connection is requested.</param>
        public LocalRoutingDataManager(GlobalTimeProvider globalTimeProvider = null)
            : base(globalTimeProvider)
        {
            AggregationParties = new List<ConversationReference>();
            UserParties = new List<ConversationReference>();
            BotParties = new List<ConversationReference>();
            PendingRequests = new List<ConversationReference>();
            ConnectedParties = new Dictionary<ConversationReference, ConversationReference>();
        }

        public override IList<ConversationReference> GetUserParties()
        {
            List<ConversationReference> userPartiesAsList = UserParties as List<ConversationReference>;
            return userPartiesAsList?.AsReadOnly();
        }

        public override IList<ConversationReference> GetBotParties()
        {
            List<ConversationReference> botPartiesAsList = BotParties as List<ConversationReference>;
            return botPartiesAsList?.AsReadOnly();
        }

        public override IList<ConversationReference> GetAggregationParties()
        {
            List<ConversationReference> aggregationPartiesAsList = AggregationParties as List<ConversationReference>;
            return aggregationPartiesAsList?.AsReadOnly();
        }

        public override IList<ConversationReference> GetPendingRequests()
        {
            List<ConversationReference> pendingRequestsAsList = PendingRequests as List<ConversationReference>;
            return pendingRequestsAsList?.AsReadOnly();
        }

        public override Dictionary<ConversationReference, ConversationReference> GetConnectedParties()
        {
            return ConnectedParties;
        }

        public override void DeleteAll()
        {
            base.DeleteAll();

            AggregationParties.Clear();
            UserParties.Clear();
            BotParties.Clear();
            PendingRequests.Clear();
            ConnectedParties.Clear();
        }

        protected override bool ExecuteAddConversationReference(ConversationReference ConversationReferenceToAdd, bool isUser)
        {
            if (isUser)
            {
                UserParties.Add(ConversationReferenceToAdd);
            }
            else
            {
                BotParties.Add(ConversationReferenceToAdd);
            }

            return true;
        }

        protected override bool ExecuteRemoveConversationReference(ConversationReference ConversationReferenceToRemove, bool isUser)
        {
            if (isUser)
            {
                return UserParties.Remove(ConversationReferenceToRemove);
            }

            return BotParties.Remove(ConversationReferenceToRemove);
        }

        protected override bool ExecuteAddAggregationConversationReference(ConversationReference aggregationConversationReferenceToAdd)
        {
            AggregationParties.Add(aggregationConversationReferenceToAdd);
            return true;
        }

        protected override bool ExecuteRemoveAggregationConversationReference(ConversationReference aggregationConversationReferenceToRemove)
        {
            return AggregationParties.Remove(aggregationConversationReferenceToRemove);
        }

        protected override bool ExecuteAddPendingRequest(ConversationReference requestorConversationReference)
        {
            PendingRequests.Add(requestorConversationReference);
            return true;
        }

        protected override bool ExecuteRemovePendingRequest(ConversationReference requestorConversationReference)
        {
            return PendingRequests.Remove(requestorConversationReference);
        }

        protected override bool ExecuteAddConnection(ConversationReference conversationOwnerConversationReference, ConversationReference conversationClientConversationReference)
        {
            ConnectedParties.Add(conversationOwnerConversationReference, conversationClientConversationReference);
            return true;
        }

        protected override bool ExecuteRemoveConnection(ConversationReference conversationOwnerConversationReference)
        {
            return ConnectedParties.Remove(conversationOwnerConversationReference);
        }
    }
}
