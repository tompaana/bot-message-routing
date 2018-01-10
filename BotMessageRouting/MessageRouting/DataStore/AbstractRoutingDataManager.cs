using Microsoft.Bot.Connector;
using System;
using System.Collections.Generic;
using System.Linq;
using Underscore.Bot.Models;
using Underscore.Bot.Utils;

namespace Underscore.Bot.MessageRouting.DataStore
{
    /// <summary>
    /// This class reduces the amount of code needed for data store specific implementations by
    /// containing the business logic that works in most cases, but abstracting the simplest,
    /// data store specific read and write operations (methods starting with "Execute").
    /// </summary>
    [Serializable]
    public abstract class AbstractRoutingDataManager : IRoutingDataManager
    {
        /// <summary>
        /// A global time provider.
        /// Used for providing the current time for various of events.
        /// For instance, the time when a connection request is made may be useful for customer
        /// agent front-ends to see who has waited the longest and/or to collect response times.
        /// </summary>
        public virtual GlobalTimeProvider GlobalTimeProvider
        {
            get;
            protected set;
        }

#if DEBUG
        protected IList<MessageRouterResult> LastMessageRouterResults
        {
            get;
            set;
        }
#endif

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="globalTimeProvider">The global time provider for providing the current
        /// time for various events such as when a connection is requested.</param>
        public AbstractRoutingDataManager(GlobalTimeProvider globalTimeProvider = null)
        {
            GlobalTimeProvider = globalTimeProvider ?? new GlobalTimeProvider();
#if DEBUG
            LastMessageRouterResults = new List<MessageRouterResult>();
#endif
        }

        public abstract IList<Party> GetUserParties();
        public abstract IList<Party> GetBotParties();

        public virtual bool AddParty(Party partyToAdd, bool isUser = true)
        {
            if (partyToAdd == null
                || (isUser ?
                    GetUserParties().Contains(partyToAdd)
                    : GetBotParties().Contains(partyToAdd)))
            {
                return false;
            }

            if (!isUser && partyToAdd.ChannelAccount == null)
            {
                throw new NullReferenceException($"Channel account of a bot party ({nameof(partyToAdd.ChannelAccount)}) cannot be null");
            }

            ExecuteAddParty(partyToAdd, isUser);
            return true;
        }

        public virtual IList<MessageRouterResult> RemoveParty(Party partyToRemove)
        {
            List<MessageRouterResult> messageRouterResults = new List<MessageRouterResult>();
            bool wasRemoved = false;

            // Check user and bot parties
            for (int i = 0; i < 2; ++i)
            {
                bool isUser = (i == 0);
                IList<Party> partyList = isUser ? GetUserParties() : GetBotParties();
                IList<Party> partiesToRemove = FindPartiesWithMatchingChannelAccount(partyToRemove, partyList);

                if (partiesToRemove != null)
                {
                    foreach (Party party in partiesToRemove)
                    {
                        wasRemoved = ExecuteRemoveParty(party, isUser);

                        if (wasRemoved)
                        {
                            messageRouterResults.Add(new MessageRouterResult()
                            {
                                Type = MessageRouterResultType.OK
                            });
                        }
                    }
                }
            }

            // Check pending requests
            IList<Party> pendingRequestsToRemove = FindPartiesWithMatchingChannelAccount(partyToRemove, GetPendingRequests());

            foreach (Party pendingRequestToRemove in pendingRequestsToRemove)
            {
                MessageRouterResult removePendingRequestResult = RemovePendingRequest(pendingRequestToRemove);

                if (removePendingRequestResult.Type == MessageRouterResultType.ConnectionRejected)
                {
                    // Pending request was removed
                    wasRemoved = true;

                    messageRouterResults.Add(removePendingRequestResult);
                }
            }

            if (wasRemoved)
            {
                // Check if the party exists in ConnectedParties
                List<Party> keys = new List<Party>();

                foreach (var partyPair in GetConnectedParties())
                {
                    if (partyPair.Key.HasMatchingChannelInformation(partyToRemove)
                        || partyPair.Value.HasMatchingChannelInformation(partyToRemove))
                    {
                        keys.Add(partyPair.Key);
                    }
                }

                foreach (Party key in keys)
                {
                    messageRouterResults.AddRange(Disconnect(key, ConnectionProfile.Owner));
                }
            }

            if (messageRouterResults.Count == 0)
            {
                messageRouterResults.Add(new MessageRouterResult()
                {
                    Type = MessageRouterResultType.NoActionTaken
                });
            }

            return messageRouterResults;
        }

        public abstract IList<Party> GetAggregationParties();

        public virtual bool AddAggregationParty(Party aggregationPartyToAdd)
        {
            if (aggregationPartyToAdd != null)
            {
                if (aggregationPartyToAdd.ChannelAccount != null)
                {
                    throw new ArgumentException("Aggregation party cannot contain a channel account");
                }

                if (!GetAggregationParties().Contains(aggregationPartyToAdd))
                {
                    ExecuteAddAggregationParty(aggregationPartyToAdd);
                    return true;
                }
            }

            return false;
        }

        public virtual bool RemoveAggregationParty(Party aggregationPartyToRemove)
        {
            return ExecuteRemoveAggregationParty(aggregationPartyToRemove);
        }

        public abstract IList<Party> GetPendingRequests();

        public virtual MessageRouterResult AddPendingRequest(
            Party requestorParty, bool rejectConnectionRequestIfNoAggregationChannel = false)
        {
            AddParty(requestorParty, true); // Make sure the requestor party is in the list of user parties

            MessageRouterResult result = new MessageRouterResult()
            {
                ConversationClientParty = requestorParty
            };

            if (requestorParty != null)
            {
                if (IsAssociatedWithAggregation(requestorParty))
                {
                    result.Type = MessageRouterResultType.Error;
                    result.ErrorMessage = $"The given party ({requestorParty.ChannelAccount?.Name}) is associated with aggregation and hence invalid to request a connection";
                }
                else if (GetPendingRequests().Contains(requestorParty))
                {
                    result.Type = MessageRouterResultType.ConnectionAlreadyRequested;
                }
                else
                {
                    if (!GetAggregationParties().Any() && rejectConnectionRequestIfNoAggregationChannel)
                    {
                        result.Type = MessageRouterResultType.NoAgentsAvailable;
                    }
                    else
                    {
                        requestorParty.ConnectionRequestTime = GetCurrentGlobalTime();

                        ExecuteAddPendingRequest(requestorParty);
                        result.Type = MessageRouterResultType.ConnectionRequested;
                    }
                }
            }
            else
            {
                result.Type = MessageRouterResultType.Error;
                result.ErrorMessage = "The given party instance is null";
            }

            return result;
        }

        public virtual MessageRouterResult RemovePendingRequest(Party requestorParty)
        {
            MessageRouterResult result = new MessageRouterResult()
            {
                ConversationClientParty = requestorParty
            };

            if (GetPendingRequests().Contains(requestorParty))
            {
                requestorParty.ResetConnectionRequestTime();

                if (ExecuteRemovePendingRequest(requestorParty))
                {
                    result.Type = MessageRouterResultType.ConnectionRejected;
                }
                else
                {
                    result.Type = MessageRouterResultType.Error;
                    result.ErrorMessage = "Failed to remove the pending request of the given party";
                }
            }
            else
            {
                result.Type = MessageRouterResultType.Error;
                result.ErrorMessage = "Could not find a pending request for the given party";
            }

            return result;
        }

        public virtual bool IsConnected(Party party, ConnectionProfile connectionProfile)
        {
            bool isConnected = false;

            if (party != null)
            {
                switch (connectionProfile)
                {
                    case ConnectionProfile.Client:
                        isConnected = GetConnectedParties().Values.Contains(party);
                        break;
                    case ConnectionProfile.Owner:
                        isConnected = GetConnectedParties().Keys.Contains(party);
                        break;
                    case ConnectionProfile.Any:
                        isConnected = (GetConnectedParties().Values.Contains(party) || GetConnectedParties().Keys.Contains(party));
                        break;
                    default:
                        break;
                }
            }

            return isConnected;
        }

        public abstract Dictionary<Party, Party> GetConnectedParties();

        public virtual Party GetConnectedCounterpart(Party partyWhoseCounterpartToFind)
        {
            Party counterparty = null;
            Dictionary<Party, Party> connectedParties = GetConnectedParties();

            if (IsConnected(partyWhoseCounterpartToFind, ConnectionProfile.Client))
            {
                for (int i = 0; i < connectedParties.Count; ++i)
                {
                    if (connectedParties.Values.ElementAt(i).Equals(partyWhoseCounterpartToFind))
                    {
                        counterparty = connectedParties.Keys.ElementAt(i);
                        break;
                    }
                }
            }
            else if (IsConnected(partyWhoseCounterpartToFind, ConnectionProfile.Owner))
            {
                connectedParties.TryGetValue(partyWhoseCounterpartToFind, out counterparty);
            }

            return counterparty;
        }

        public virtual MessageRouterResult ConnectAndClearPendingRequest(
            Party conversationOwnerParty, Party conversationClientParty)
        {
            MessageRouterResult result = new MessageRouterResult()
            {
                ConversationOwnerParty = conversationOwnerParty,
                ConversationClientParty = conversationClientParty
            };

            if (conversationOwnerParty != null && conversationClientParty != null)
            {
                try
                {
                    ExecuteAddConnection(conversationOwnerParty, conversationClientParty);
                    ExecuteRemovePendingRequest(conversationClientParty);

                    DateTime connectionStartedTime = GetCurrentGlobalTime();

                    conversationClientParty.ResetConnectionRequestTime();
                    conversationClientParty.ConnectionEstablishedTime = connectionStartedTime;
                    conversationOwnerParty.ConnectionEstablishedTime = connectionStartedTime;

                    result.Type = MessageRouterResultType.Connected;
                }
                catch (ArgumentException e)
                {
                    result.Type = MessageRouterResultType.Error;
                    result.ErrorMessage = e.Message;
                    System.Diagnostics.Debug.WriteLine(
                        $"Failed to connect parties {conversationOwnerParty} and {conversationClientParty}: {e.Message}");
                }
            }
            else
            {
                result.Type = MessageRouterResultType.Error;
                result.ErrorMessage = "Either the owner or the client is missing";
            }

            return result;
        }

        public virtual IList<MessageRouterResult> Disconnect(Party party, ConnectionProfile connectionProfile)
        {
            IList<MessageRouterResult> messageRouterResults = new List<MessageRouterResult>();

            if (party != null)
            {
                List<Party> keysToRemove = new List<Party>();

                foreach (var partyPair in GetConnectedParties())
                {
                    bool removeThisPair = false;

                    switch (connectionProfile)
                    {
                        case ConnectionProfile.Client:
                            removeThisPair = partyPair.Value.Equals(party);
                            break;
                        case ConnectionProfile.Owner:
                            removeThisPair = partyPair.Key.Equals(party);
                            break;
                        case ConnectionProfile.Any:
                            removeThisPair = (partyPair.Value.Equals(party) || partyPair.Key.Equals(party));
                            break;
                        default:
                            break;
                    }

                    if (removeThisPair)
                    {
                        keysToRemove.Add(partyPair.Key);

                        if (connectionProfile == ConnectionProfile.Owner)
                        {
                            // Since owner is the key in the dictionary, there can be only one
                            break;
                        }
                    }
                }

                messageRouterResults = RemoveConnections(keysToRemove);
            }

            return messageRouterResults;
        }

        public virtual void DeleteAll()
        {
#if DEBUG
            LastMessageRouterResults.Clear();
#endif
        }

        public virtual bool IsAssociatedWithAggregation(Party party)
        {
            IList<Party> aggregationParties = GetAggregationParties();

            return (party != null && aggregationParties != null && aggregationParties.Count() > 0
                    && aggregationParties.Where(aggregationParty =>
                        aggregationParty.ConversationAccount.Id == party.ConversationAccount.Id
                        && aggregationParty.ServiceUrl == party.ServiceUrl
                        && aggregationParty.ChannelId == party.ChannelId).Count() > 0);
        }

        public virtual string ResolveBotNameInConversation(Party party)
        {
            string botName = null;

            if (party != null)
            {
                Party botParty = FindBotPartyByChannelAndConversation(party.ChannelId, party.ConversationAccount);

                if (botParty != null && botParty.ChannelAccount != null)
                {
                    botName = botParty.ChannelAccount.Name;
                }
            }

            return botName;
        }

        public virtual Party FindExistingUserParty(Party partyToFind)
        {
            Party foundParty = null;

            try
            {
                foundParty = GetUserParties().First(party => partyToFind.Equals(party));
            }
            catch (ArgumentNullException)
            {
            }
            catch (InvalidOperationException)
            {
            }

            return foundParty;
        }

        public virtual Party FindPartyByChannelAccountIdAndConversationId(
            string channelAccountId, string conversationId)
        {
            Party userParty = null;

            try
            {
                userParty = GetUserParties().Single(party =>
                        (party.ChannelAccount.Id.Equals(channelAccountId)
                         && party.ConversationAccount.Id.Equals(conversationId)));
            }
            catch (InvalidOperationException)
            {
            }

            return userParty;
        }

        public virtual Party FindBotPartyByChannelAndConversation(
            string channelId, ConversationAccount conversationAccount)
        {
            Party botParty = null;

            try
            {
                botParty = GetBotParties().Single(party =>
                        (party.ChannelId.Equals(channelId)
                         && party.ConversationAccount.Id.Equals(conversationAccount.Id)));
            }
            catch (InvalidOperationException)
            {
            }

            return botParty;
        }

        public virtual Party FindConnectedPartyByChannel(string channelId, ChannelAccount channelAccount)
        {
            Party foundParty = null;

            try
            {
                foundParty = GetConnectedParties().Keys.Single(party =>
                        (party.ChannelId.Equals(channelId)
                         && party.ChannelAccount != null
                         && party.ChannelAccount.Id.Equals(channelAccount.Id)));
            }
            catch (InvalidOperationException)
            {
            }

            if (foundParty == null)
            {
                try
                {
                    // Not found in keys, try the values
                    foundParty = GetConnectedParties().Values.First(party =>
                            (party.ChannelId.Equals(channelId)
                             && party.ChannelAccount != null
                             && party.ChannelAccount.Id.Equals(channelAccount.Id)));
                }
                catch (InvalidOperationException)
                {
                }
            }

            return foundParty;
        }

        public virtual IList<Party> FindPartiesWithMatchingChannelAccount(Party partyToFind, IList<Party> partyCandidates)
        {
            IList<Party> matchingParties = null;
            IEnumerable<Party> foundParties = null;

            try
            {
                foundParties = partyCandidates.Where(party => party.HasMatchingChannelInformation(partyToFind));
            }
            catch (ArgumentNullException e)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to find parties: {e.Message}");
            }
            catch (InvalidOperationException e)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to find parties: {e.Message}");
            }

            if (foundParties != null)
            {
                matchingParties = foundParties.ToArray();
            }

            return matchingParties;
        }

#if DEBUG
        public virtual string ConnectionsToString()
        {
            string parties = string.Empty;

            foreach (KeyValuePair<Party, Party> keyValuePair in GetConnectedParties())
            {
                parties += $"{keyValuePair.Key} -> {keyValuePair.Value}\n\r";
            }

            return parties;
        }

        public virtual string GetLastMessageRouterResults()
        {
            string lastResultsAsString = string.Empty;

            foreach (MessageRouterResult result in LastMessageRouterResults)
            {
                lastResultsAsString += $"{result.ToString()}\n";
            }

            return lastResultsAsString;
        }

        public virtual void AddMessageRouterResult(MessageRouterResult result)
        {
            if (result != null)
            {
                if (LastMessageRouterResults.Count > 9)
                {
                    LastMessageRouterResults.Remove(LastMessageRouterResults.ElementAt(0));
                }

                LastMessageRouterResults.Add(result);
            }
        }

        public virtual void ClearMessageRouterResults()
        {
            LastMessageRouterResults.Clear();
        }
#endif

        /// <summary>
        /// Adds the given party to the collection. No sanity checks.
        /// </summary>
        /// <param name="partyToAdd">The new party to add.</param>
        /// <param name="isUser">If true, the party is considered a user.
        /// If false, the party is considered to be a bot.</param>
        /// <returns>True, if successful. False otherwise.</returns>
        protected abstract bool ExecuteAddParty(Party partyToAdd, bool isUser);

        /// <summary>
        /// Removes the given party from the collection. No sanity checks.
        /// </summary>
        /// <param name="partyToRemove">The party to remove.</param>
        /// <param name="isUser">If true, the party is considered a user.
        /// If false, the party is considered to be a bot.</param>
        /// <returns>True, if successful. False otherwise.</returns>
        protected abstract bool ExecuteRemoveParty(Party partyToRemove, bool isUser);

        /// <summary>
        /// Adds the given aggregation party to the collection. No sanity checks.
        /// </summary>
        /// <param name="aggregationPartyToAdd">The party to be added as an aggregation party (channel).</param>
        /// <returns>True, if successful. False otherwise.</returns>
        protected abstract bool ExecuteAddAggregationParty(Party aggregationPartyToAdd);

        /// <summary>
        /// Removes the given aggregation party from the collection. No sanity checks.
        /// </summary>
        /// <param name="aggregationPartyToRemove">The aggregation party to remove.</param>
        /// <returns>True, if successful. False otherwise.</returns>
        protected abstract bool ExecuteRemoveAggregationParty(Party aggregationPartyToRemove);

        /// <summary>
        /// Adds the pending request for the given party. No sanity checks.
        /// </summary>
        /// <param name="requestorParty">The party whose pending request to add.</param>
        /// <returns>True, if successful. False otherwise.</returns>
        protected abstract bool ExecuteAddPendingRequest(Party requestorParty);

        /// <summary>
        /// Removes the pending request of the given party. No sanity checks.
        /// </summary>
        /// <param name="requestorParty">The party whose request to remove.</param>
        /// <returns>True, if successful. False otherwise.</returns>
        protected abstract bool ExecuteRemovePendingRequest(Party requestorParty);

        /// <summary>
        /// Adds a connection between the given parties. No sanity checks.
        /// </summary>
        /// <param name="conversationOwnerParty">The conversation owner party.</param>
        /// <param name="conversationClientParty">The conversation client (customer) party
        /// (i.e. one who requested the connection).</param>
        /// <returns>True, if successful. False otherwise.</returns>
        protected abstract bool ExecuteAddConnection(Party conversationOwnerParty, Party conversationClientParty);

        /// <summary>
        /// Removes the connection of the given conversation owner party.
        /// </summary>
        /// <param name="conversationOwnerParty">The conversation owner party.</param>
        /// <returns>True, if successful. False otherwise.</returns>
        protected abstract bool ExecuteRemoveConnection(Party conversationOwnerParty);

        /// <returns>The current global "now" time.</returns>
        protected virtual DateTime GetCurrentGlobalTime()
        {
            return (GlobalTimeProvider == null) ? DateTime.UtcNow : GlobalTimeProvider.GetCurrentTime();
        }

        /// <summary>
        /// Removes the connections of the given conversation owners.
        /// </summary>
        /// <param name="conversationOwnerParties">The conversation owners whose connections to remove.</param>
        /// <returns>The operation result(s).</returns>
        protected virtual IList<MessageRouterResult> RemoveConnections(IList<Party> conversationOwnerParties)
        {
            IList<MessageRouterResult> messageRouterResults = new List<MessageRouterResult>();

            foreach (Party conversationOwnerParty in conversationOwnerParties)
            {
                Dictionary<Party, Party> connectedParties = GetConnectedParties();
                connectedParties.TryGetValue(conversationOwnerParty, out Party conversationClientParty);

                if (ExecuteRemoveConnection(conversationOwnerParty))
                {
                    conversationOwnerParty.ResetConnectionEstablishedTime();
                    conversationClientParty.ResetConnectionEstablishedTime();

                    messageRouterResults.Add(new MessageRouterResult()
                    {
                        Type = MessageRouterResultType.Disconnected,
                        ConversationOwnerParty = conversationOwnerParty,
                        ConversationClientParty = conversationClientParty
                    });
                }
            }

            if (messageRouterResults.Count == 0)
            {
                messageRouterResults.Add(new MessageRouterResult()
                {
                    Type = MessageRouterResultType.NoActionTaken
                });
            }

            return messageRouterResults;
        }
    }
}
