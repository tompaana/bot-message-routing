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
        protected IList<Party> UserParties
        {
            get;
            set;
        }

        /// <summary>
        /// If the bot is addressed from different channels, its identity in terms of ID and name
        /// can vary. Those different identities are stored in this list.
        /// </summary>
        protected IList<Party> BotParties
        {
            get;
            set;
        }

        /// <summary>
        /// Represents the channels (and the specific conversations e.g. specific channel in Slack),
        /// where the chat requests are directed. For instance, a channel could be where the
        /// customer service agents accept customer chat requests. 
        /// </summary>
        protected IList<Party> AggregationParties
        {
            get;
            set;
        }

        /// <summary>
        /// The list of parties waiting for their (conversation) requests to be accepted.
        /// </summary>
        protected List<Party> PendingRequests
        {
            get;
            set;
        }

        /// <summary>
        /// Contains 1:1 associations between parties i.e. parties connected in a conversation.
        /// Furthermore, the key party is considered to be the conversation owner e.g. in
        /// a customer service situation the customer service agent.
        /// </summary>
        protected Dictionary<Party, Party> ConnectedParties
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
            AggregationParties = new List<Party>();
            UserParties = new List<Party>();
            BotParties = new List<Party>();
            PendingRequests = new List<Party>();
            ConnectedParties = new Dictionary<Party, Party>();
        }

        public override IList<Party> GetUserParties()
        {
            List<Party> userPartiesAsList = UserParties as List<Party>;
            return userPartiesAsList?.AsReadOnly();
        }

        public override IList<Party> GetBotParties()
        {
            List<Party> botPartiesAsList = BotParties as List<Party>;
            return botPartiesAsList?.AsReadOnly();
        }

        public override IList<Party> GetAggregationParties()
        {
            List<Party> aggregationPartiesAsList = AggregationParties as List<Party>;
            return aggregationPartiesAsList?.AsReadOnly();
        }

        public override IList<Party> GetPendingRequests()
        {
            List<Party> pendingRequestsAsList = PendingRequests as List<Party>;
            return pendingRequestsAsList?.AsReadOnly();
        }

        public override Dictionary<Party, Party> GetConnectedParties()
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

        protected override bool ExecuteAddParty(Party partyToAdd, bool isUser)
        {
            if (isUser)
            {
                UserParties.Add(partyToAdd);
            }
            else
            {
                BotParties.Add(partyToAdd);
            }

            return true;
        }

        protected override bool ExecuteRemoveParty(Party partyToRemove, bool isUser)
        {
            if (isUser)
            {
                return UserParties.Remove(partyToRemove);
            }

            return BotParties.Remove(partyToRemove);
        }

        protected override bool ExecuteAddAggregationParty(Party aggregationPartyToAdd)
        {
            AggregationParties.Add(aggregationPartyToAdd);
            return true;
        }

        protected override bool ExecuteRemoveAggregationParty(Party aggregationPartyToRemove)
        {
            return AggregationParties.Remove(aggregationPartyToRemove);
        }

        protected override bool ExecuteAddPendingRequest(Party requestorParty)
        {
            PendingRequests.Add(requestorParty);
            return true;
        }

        protected override bool ExecuteRemovePendingRequest(Party requestorParty)
        {
            return PendingRequests.Remove(requestorParty);
        }

        protected override bool ExecuteAddConnection(Party conversationOwnerParty, Party conversationClientParty)
        {
            ConnectedParties.Add(conversationOwnerParty, conversationClientParty);
            return true;
        }

        protected override bool ExecuteRemoveConnection(Party conversationOwnerParty)
        {
            return ConnectedParties.Remove(conversationOwnerParty);
        }
    }
}
