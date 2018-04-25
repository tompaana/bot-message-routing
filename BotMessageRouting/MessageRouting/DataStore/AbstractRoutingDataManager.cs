using Microsoft.Bot.Schema;
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

        public abstract IList<ConversationReference> GetUserParties();
        public abstract IList<ConversationReference> GetBotParties();

        public virtual bool AddConversationReference(ConversationReference ConversationReferenceToAdd, bool isUser = true)
        {
            if (ConversationReferenceToAdd == null
                || (isUser ?
                    GetUserParties().Contains(ConversationReferenceToAdd)
                    : GetBotParties().Contains(ConversationReferenceToAdd)))
            {
                return false;
            }

            if (!isUser && ConversationReferenceToAdd.ChannelAccount == null)
            {
                throw new NullReferenceException($"Channel account of a bot ConversationReference ({nameof(ConversationReferenceToAdd.ChannelAccount)}) cannot be null");
            }

            return ExecuteAddConversationReference(ConversationReferenceToAdd, isUser);
        }

        public virtual IList<MessageRouterResult> RemoveConversationReference(ConversationReference ConversationReferenceToRemove)
        {
            List<MessageRouterResult> messageRouterResults = new List<MessageRouterResult>();
            bool wasRemoved = false;

            // Check user and bot parties
            for (int i = 0; i < 2; ++i)
            {
                bool isUser = (i == 0);
                IList<ConversationReference> ConversationReferenceList = isUser ? GetUserParties() : GetBotParties();
                IList<ConversationReference> partiesToRemove = FindPartiesWithMatchingChannelAccount(ConversationReferenceToRemove, ConversationReferenceList);

                if (partiesToRemove != null)
                {
                    foreach (ConversationReference ConversationReference in partiesToRemove)
                    {
                        wasRemoved = ExecuteRemoveConversationReference(ConversationReference, isUser);

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
            IList<ConversationReference> pendingRequestsToRemove = FindPartiesWithMatchingChannelAccount(ConversationReferenceToRemove, GetPendingRequests());

            foreach (ConversationReference pendingRequestToRemove in pendingRequestsToRemove)
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
                // Check if the ConversationReference exists in ConnectedParties
                List<ConversationReference> keys = new List<ConversationReference>();

                foreach (var ConversationReferencePair in GetConnectedParties())
                {
                    if (ConversationReferencePair.Key.HasMatchingChannelInformation(ConversationReferenceToRemove)
                        || ConversationReferencePair.Value.HasMatchingChannelInformation(ConversationReferenceToRemove))
                    {
                        keys.Add(ConversationReferencePair.Key);
                    }
                }

                foreach (ConversationReference key in keys)
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

        public abstract IList<ConversationReference> GetAggregationParties();

        public virtual bool AddAggregationConversationReference(ConversationReference aggregationConversationReferenceToAdd)
        {
            if (aggregationConversationReferenceToAdd != null)
            {
                if (aggregationConversationReferenceToAdd.ChannelAccount != null)
                {
                    throw new ArgumentException("Aggregation ConversationReference cannot contain a channel account");
                }

                IList<ConversationReference> aggregationParties = GetAggregationParties();

                if (!aggregationParties.Contains(aggregationConversationReferenceToAdd))
                {
                    return ExecuteAddAggregationConversationReference(aggregationConversationReferenceToAdd);
                }
            }

            return false;
        }

        public virtual bool RemoveAggregationConversationReference(ConversationReference aggregationConversationReferenceToRemove)
        {
            return ExecuteRemoveAggregationConversationReference(aggregationConversationReferenceToRemove);
        }

        public abstract IList<ConversationReference> GetPendingRequests();

        public virtual MessageRouterResult AddPendingRequest(
            ConversationReference requestorConversationReference, bool rejectConnectionRequestIfNoAggregationChannel = false)
        {
            AddConversationReference(requestorConversationReference, true); // Make sure the requestor ConversationReference is in the list of user parties

            MessageRouterResult result = new MessageRouterResult()
            {
                ConversationClientConversationReference = requestorConversationReference
            };

            if (requestorConversationReference != null)
            {
                if (IsAssociatedWithAggregation(requestorConversationReference))
                {
                    result.Type = MessageRouterResultType.Error;
                    result.ErrorMessage = $"The given ConversationReference ({requestorConversationReference.ChannelAccount?.Name}) is associated with aggregation and hence invalid to request a connection";
                }
                else if (GetPendingRequests().Contains(requestorConversationReference))
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
                        requestorConversationReference.ConnectionRequestTime = GetCurrentGlobalTime();

                        if (ExecuteAddPendingRequest(requestorConversationReference))
                        {
                            result.Type = MessageRouterResultType.ConnectionRequested;
                        }
                        else
                        {
                            result.Type = MessageRouterResultType.Error;
                            result.ErrorMessage = "Failed to add the pending request - this is likely an error caused by the storage implementation";
                        }
                    }
                }
            }
            else
            {
                result.Type = MessageRouterResultType.Error;
                result.ErrorMessage = "The given ConversationReference instance is null";
            }

            return result;
        }

        public virtual MessageRouterResult RemovePendingRequest(ConversationReference requestorConversationReference)
        {
            MessageRouterResult result = new MessageRouterResult()
            {
                ConversationClientConversationReference = requestorConversationReference
            };

            if (GetPendingRequests().Contains(requestorConversationReference))
            {
                if (ExecuteRemovePendingRequest(requestorConversationReference))
                {
                    result.Type = MessageRouterResultType.ConnectionRejected;
                }
                else
                {
                    result.Type = MessageRouterResultType.Error;
                    result.ErrorMessage = "Failed to remove the pending request of the given ConversationReference";
                }
            }
            else
            {
                result.Type = MessageRouterResultType.Error;
                result.ErrorMessage = "Could not find a pending request for the given ConversationReference";
            }

            return result;
        }

        public virtual bool IsConnected(ConversationReference ConversationReference, ConnectionProfile connectionProfile)
        {
            bool isConnected = false;

            if (ConversationReference != null)
            {
                switch (connectionProfile)
                {
                    case ConnectionProfile.Client:
                        isConnected = GetConnectedParties().Values.Contains(ConversationReference);
                        break;
                    case ConnectionProfile.Owner:
                        isConnected = GetConnectedParties().Keys.Contains(ConversationReference);
                        break;
                    case ConnectionProfile.Any:
                        isConnected = (GetConnectedParties().Values.Contains(ConversationReference) || GetConnectedParties().Keys.Contains(ConversationReference));
                        break;
                    default:
                        break;
                }
            }

            return isConnected;
        }

        public abstract Dictionary<ConversationReference, ConversationReference> GetConnectedParties();

        public virtual ConversationReference GetConnectedCounterpart(ConversationReference ConversationReferenceWhoseCounterpartToFind)
        {
            ConversationReference counterConversationReference = null;
            Dictionary<ConversationReference, ConversationReference> connectedParties = GetConnectedParties();

            if (IsConnected(ConversationReferenceWhoseCounterpartToFind, ConnectionProfile.Client))
            {
                for (int i = 0; i < connectedParties.Count; ++i)
                {
                    if (connectedParties.Values.ElementAt(i).Equals(ConversationReferenceWhoseCounterpartToFind))
                    {
                        counterConversationReference = connectedParties.Keys.ElementAt(i);
                        break;
                    }
                }
            }
            else if (IsConnected(ConversationReferenceWhoseCounterpartToFind, ConnectionProfile.Owner))
            {
                connectedParties.TryGetValue(ConversationReferenceWhoseCounterpartToFind, out counterConversationReference);
            }

            return counterConversationReference;
        }

        public virtual MessageRouterResult ConnectAndClearPendingRequest(
            ConversationReference conversationOwnerConversationReference, ConversationReference conversationClientConversationReference)
        {
            MessageRouterResult result = new MessageRouterResult()
            {
                ConversationOwnerConversationReference = conversationOwnerConversationReference,
                ConversationClientConversationReference = conversationClientConversationReference
            };

            if (conversationOwnerConversationReference != null && conversationClientConversationReference != null)
            {
                DateTime connectionStartedTime = GetCurrentGlobalTime();
                conversationClientConversationReference.ResetConnectionRequestTime();
                conversationClientConversationReference.ConnectionEstablishedTime = connectionStartedTime;

                bool wasConnectionAdded =
                    ExecuteAddConnection(conversationOwnerConversationReference, conversationClientConversationReference);

                if (wasConnectionAdded)
                {
                    ExecuteRemovePendingRequest(conversationClientConversationReference);
                    result.Type = MessageRouterResultType.Connected;
                }
                else
                {
                    result.Type = MessageRouterResultType.Error;
                    result.ErrorMessage =
                        $"Failed to add connection between {conversationOwnerConversationReference} and {conversationClientConversationReference}";
                }
            }
            else
            {
                result.Type = MessageRouterResultType.Error;
                result.ErrorMessage = "Either the owner or the client is missing";
            }

            return result;
        }

        public virtual IList<MessageRouterResult> Disconnect(ConversationReference ConversationReference, ConnectionProfile connectionProfile)
        {
            IList<MessageRouterResult> messageRouterResults = new List<MessageRouterResult>();

            if (ConversationReference != null)
            {
                List<ConversationReference> keysToRemove = new List<ConversationReference>();

                foreach (var ConversationReferencePair in GetConnectedParties())
                {
                    bool removeThisPair = false;

                    switch (connectionProfile)
                    {
                        case ConnectionProfile.Client:
                            removeThisPair = ConversationReferencePair.Value.Equals(ConversationReference);
                            break;
                        case ConnectionProfile.Owner:
                            removeThisPair = ConversationReferencePair.Key.Equals(ConversationReference);
                            break;
                        case ConnectionProfile.Any:
                            removeThisPair = (ConversationReferencePair.Value.Equals(ConversationReference) || ConversationReferencePair.Key.Equals(ConversationReference));
                            break;
                        default:
                            break;
                    }

                    if (removeThisPair)
                    {
                        keysToRemove.Add(ConversationReferencePair.Key);

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

        public virtual bool IsAssociatedWithAggregation(ConversationReference ConversationReference)
        {
            IList<ConversationReference> aggregationParties = GetAggregationParties();

            return (ConversationReference != null && aggregationParties != null && aggregationParties.Count() > 0
                    && aggregationParties.Where(aggregationConversationReference =>
                        aggregationConversationReference.ConversationAccount.Id == ConversationReference.ConversationAccount.Id
                        && aggregationConversationReference.ServiceUrl == ConversationReference.ServiceUrl
                        && aggregationConversationReference.ChannelId == ConversationReference.ChannelId).Count() > 0);
        }

        public virtual string ResolveBotNameInConversation(ConversationReference ConversationReference)
        {
            string botName = null;

            if (ConversationReference != null)
            {
                ConversationReference botConversationReference = FindBotConversationReferenceByChannelAndConversation(ConversationReference.ChannelId, ConversationReference.ConversationAccount);

                if (botConversationReference != null && botConversationReference.ChannelAccount != null)
                {
                    botName = botConversationReference.ChannelAccount.Name;
                }
            }

            return botName;
        }

        public virtual ConversationReference FindExistingUserConversationReference(ConversationReference ConversationReferenceToFind)
        {
            ConversationReference foundConversationReference = null;

            try
            {
                foundConversationReference = GetUserParties().First(ConversationReference => ConversationReferenceToFind.Equals(ConversationReference));
            }
            catch (ArgumentNullException)
            {
            }
            catch (InvalidOperationException)
            {
            }

            return foundConversationReference;
        }

        public virtual ConversationReference FindConversationReferenceByChannelAccountIdAndConversationId(
            string channelAccountId, string conversationId)
        {
            ConversationReference userConversationReference = null;

            try
            {
                userConversationReference = GetUserParties().Single(ConversationReference =>
                        (ConversationReference.ChannelAccount.Id.Equals(channelAccountId)
                         && ConversationReference.ConversationAccount.Id.Equals(conversationId)));
            }
            catch (InvalidOperationException)
            {
            }

            return userConversationReference;
        }

        public virtual ConversationReference FindBotConversationReferenceByChannelAndConversation(
            string channelId, ConversationAccount conversationAccount)
        {
            ConversationReference botConversationReference = null;

            try
            {
                botConversationReference = GetBotParties().Single(ConversationReference =>
                        (ConversationReference.ChannelId.Equals(channelId)
                         && ConversationReference.ConversationAccount.Id.Equals(conversationAccount.Id)));
            }
            catch (InvalidOperationException)
            {
            }

            return botConversationReference;
        }

        public virtual ConversationReference FindConnectedConversationReferenceByChannel(string channelId, ChannelAccount channelAccount)
        {
            ConversationReference foundConversationReference = null;

            try
            {
                foundConversationReference = GetConnectedParties().Keys.Single(ConversationReference =>
                        (ConversationReference.ChannelId.Equals(channelId)
                         && ConversationReference.ChannelAccount != null
                         && ConversationReference.ChannelAccount.Id.Equals(channelAccount.Id)));
            }
            catch (InvalidOperationException)
            {
            }

            if (foundConversationReference == null)
            {
                try
                {
                    // Not found in keys, try the values
                    foundConversationReference = GetConnectedParties().Values.First(ConversationReference =>
                            (ConversationReference.ChannelId.Equals(channelId)
                             && ConversationReference.ChannelAccount != null
                             && ConversationReference.ChannelAccount.Id.Equals(channelAccount.Id)));
                }
                catch (InvalidOperationException)
                {
                }
            }

            return foundConversationReference;
        }

        public virtual IList<ConversationReference> FindPartiesWithMatchingChannelAccount(ConversationReference ConversationReferenceToFind, IList<ConversationReference> ConversationReferenceCandidates)
        {
            IList<ConversationReference> matchingParties = null;
            IEnumerable<ConversationReference> foundParties = null;

            try
            {
                foundParties = ConversationReferenceCandidates.Where(ConversationReference => ConversationReference.HasMatchingChannelInformation(ConversationReferenceToFind));
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

            foreach (KeyValuePair<ConversationReference, ConversationReference> keyValuePair in GetConnectedParties())
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
        /// Adds the given ConversationReference to the collection. No sanity checks.
        /// </summary>
        /// <param name="ConversationReferenceToAdd">The new ConversationReference to add.</param>
        /// <param name="isUser">If true, the ConversationReference is considered a user.
        /// If false, the ConversationReference is considered to be a bot.</param>
        /// <returns>True, if successful. False otherwise.</returns>
        protected abstract bool ExecuteAddConversationReference(ConversationReference ConversationReferenceToAdd, bool isUser);

        /// <summary>
        /// Removes the given ConversationReference from the collection. No sanity checks.
        /// </summary>
        /// <param name="ConversationReferenceToRemove">The ConversationReference to remove.</param>
        /// <param name="isUser">If true, the ConversationReference is considered a user.
        /// If false, the ConversationReference is considered to be a bot.</param>
        /// <returns>True, if successful. False otherwise.</returns>
        protected abstract bool ExecuteRemoveConversationReference(ConversationReference ConversationReferenceToRemove, bool isUser);

        /// <summary>
        /// Adds the given aggregation ConversationReference to the collection. No sanity checks.
        /// </summary>
        /// <param name="aggregationConversationReferenceToAdd">The ConversationReference to be added as an aggregation ConversationReference (channel).</param>
        /// <returns>True, if successful. False otherwise.</returns>
        protected abstract bool ExecuteAddAggregationConversationReference(ConversationReference aggregationConversationReferenceToAdd);

        /// <summary>
        /// Removes the given aggregation ConversationReference from the collection. No sanity checks.
        /// </summary>
        /// <param name="aggregationConversationReferenceToRemove">The aggregation ConversationReference to remove.</param>
        /// <returns>True, if successful. False otherwise.</returns>
        protected abstract bool ExecuteRemoveAggregationConversationReference(ConversationReference aggregationConversationReferenceToRemove);

        /// <summary>
        /// Adds the pending request for the given ConversationReference. No sanity checks.
        /// </summary>
        /// <param name="requestorConversationReference">The ConversationReference whose pending request to add.</param>
        /// <returns>True, if successful. False otherwise.</returns>
        protected abstract bool ExecuteAddPendingRequest(ConversationReference requestorConversationReference);

        /// <summary>
        /// Removes the pending request of the given ConversationReference. No sanity checks.
        /// </summary>
        /// <param name="requestorConversationReference">The ConversationReference whose request to remove.</param>
        /// <returns>True, if successful. False otherwise.</returns>
        protected abstract bool ExecuteRemovePendingRequest(ConversationReference requestorConversationReference);

        /// <summary>
        /// Adds a connection between the given parties. No sanity checks.
        /// </summary>
        /// <param name="conversationOwnerConversationReference">The conversation owner ConversationReference.</param>
        /// <param name="conversationClientConversationReference">The conversation client (customer) ConversationReference
        /// (i.e. one who requested the connection).</param>
        /// <returns>True, if successful. False otherwise.</returns>
        protected abstract bool ExecuteAddConnection(ConversationReference conversationOwnerConversationReference, ConversationReference conversationClientConversationReference);

        /// <summary>
        /// Removes the connection of the given conversation owner ConversationReference.
        /// </summary>
        /// <param name="conversationOwnerConversationReference">The conversation owner ConversationReference.</param>
        /// <returns>True, if successful. False otherwise.</returns>
        protected abstract bool ExecuteRemoveConnection(ConversationReference conversationOwnerConversationReference);

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
        protected virtual IList<MessageRouterResult> RemoveConnections(IList<ConversationReference> conversationOwnerParties)
        {
            IList<MessageRouterResult> messageRouterResults = new List<MessageRouterResult>();

            foreach (ConversationReference conversationOwnerConversationReference in conversationOwnerParties)
            {
                Dictionary<ConversationReference, ConversationReference> connectedParties = GetConnectedParties();
                connectedParties.TryGetValue(conversationOwnerConversationReference, out ConversationReference conversationClientConversationReference);

                if (ExecuteRemoveConnection(conversationOwnerConversationReference))
                {
                    conversationOwnerConversationReference.ResetConnectionEstablishedTime();
                    conversationClientConversationReference.ResetConnectionEstablishedTime();

                    messageRouterResults.Add(new MessageRouterResult()
                    {
                        Type = MessageRouterResultType.Disconnected,
                        ConversationOwnerConversationReference = conversationOwnerConversationReference,
                        ConversationClientConversationReference = conversationClientConversationReference
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
