using Microsoft.Bot.Schema;
using System;
using System.Collections.Generic;
using System.Linq;
using Underscore.Bot.Models;
using Underscore.Bot.Utils;

namespace Underscore.Bot.MessageRouting.DataStore
{
    /// <summary>
    /// The routing data manager.
    /// </summary>
    [Serializable]
    public class RoutingDataManager
    {
        public IRoutingDataStore RoutingDataStore
        {
            get;
            protected set;
        }

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

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="routingDataStore">The routing data store implementation.</param>
        /// <param name="globalTimeProvider">The global time provider for providing the current
        /// time for various events such as when a connection is requested.</param>
        public RoutingDataManager(IRoutingDataStore routingDataStore, GlobalTimeProvider globalTimeProvider = null)
        {
            RoutingDataStore = routingDataStore ?? throw new ArgumentNullException("Routing data store missing");
            GlobalTimeProvider = globalTimeProvider ?? new GlobalTimeProvider();
        }

        /// <returns>The users as a readonly list.</returns>
        public IList<ConversationReference> GetUsers()
        {
            return RoutingDataStore.GetUsers();
        }

        /// <returns>The bot instances as a readonly list.</returns>
        public IList<ConversationReference> GetBotInstances()
        {
            return RoutingDataStore.GetBotInstances();
        }

        public IList<ConnectionRequest> GetConnectionRequests()
        {
            return RoutingDataStore.GetConnectionRequests();
        }

        public IList<ConversationReference> GetAggregationChannels();

        public IList<Connection> GetConnections();



        #region Virtual Methods

        public virtual bool AddConversationReference(ConversationReference conversationReferenceToAdd)
        {
            if (conversationReferenceToAdd == null
                || (MessageRoutingUtils.IsBot(conversationReferenceToAdd) ?
                    GetBotParties().Contains(conversationReferenceToAdd)
                    : GetUserParties().Contains(conversationReferenceToAdd)))
            {
                return false;
            }

            if (conversationReferenceToAdd.Bot == null
                && conversationReferenceToAdd.User == null)
            {
                throw new ArgumentNullException("Both channel accounts in the conversation reference cannot be null");
            }

            return ExecuteAddConversationReference(conversationReferenceToAdd);
        }


        public virtual IList<MessageRouterResult> RemoveConversationReference(ConversationReference conversationReferenceToRemove)
        {
            List<MessageRouterResult> messageRouterResults = new List<MessageRouterResult>();
            bool wasRemoved = false;

            // Check users and bots
            IList<ConversationReference> conversationReferenceList =
                (conversationReferenceToRemove.Bot == null)
                ? GetUserParties() : GetBotParties();

            IList<ConversationReference> conversationReferencesToRemove =
                MessageRoutingUtils.ResolveConversationReferencesWithMatchingChannelAccount(
                    conversationReferenceToRemove, conversationReferenceList);

            if (conversationReferencesToRemove != null)
            {
                foreach (ConversationReference conversationReference in conversationReferencesToRemove)
                {
                    wasRemoved = ExecuteRemoveConversationReference(conversationReference);

                    if (wasRemoved)
                    {
                        messageRouterResults.Add(new MessageRouterResult()
                        {
                            Type = MessageRouterResultType.OK
                        });
                    }
                }
            }

            // Check connection requests
            wasRemoved = true;

            while (wasRemoved)
            {
                wasRemoved = false;

                foreach (ConnectionRequest connectionRequest in GetConnectionRequests())
                {
                    if (MessageRoutingUtils.HasMatchingChannelAccountInformation(
                            conversationReferenceToRemove, connectionRequest.Requestor))
                    {
                        MessageRouterResult removeConnectionRequestResult = RemoveConnectionRequest(connectionRequest);

                        if (removeConnectionRequestResult.Type == MessageRouterResultType.ConnectionRejected)
                        {
                            wasRemoved = true;
                            messageRouterResults.Add(removeConnectionRequestResult);
                            break;
                        }
                    }
                }
            }

            // Check if the ConversationReference exists in connections
            wasRemoved = true;

            while (wasRemoved)
            {
                wasRemoved = false;

                foreach (Connection connection in GetConnections())
                {
                    if (MessageRoutingUtils.HasMatchingChannelAccountInformation(conversationReferenceToRemove, connection.ConversationReference1)
                        || MessageRoutingUtils.HasMatchingChannelAccountInformation(conversationReferenceToRemove, connection.ConversationReference2))
                    {
                        wasRemoved = true;
                        messageRouterResults.Add(Disconnect(connection)); // TODO: Check that the disconnect was successful
                        break;
                    }
                }
            }

            return messageRouterResults;
        }

        public virtual bool AddaggregationChannel(ConversationReference aggregationChannelToAdd)
        {
            if (aggregationChannelToAdd != null)
            {
                if (MessageRoutingUtils.GetChannelAccount(aggregationChannelToAdd) != null)
                {
                    throw new ArgumentException("The ConversationReference instance for an aggregation channel cannot contain a ChannelAccount instance");
                }

                IList<ConversationReference> aggregationParties = GetAggregationChannels();

                if (!aggregationParties.Contains(aggregationChannelToAdd))
                {
                    return ExecuteAddAggregationChannel(aggregationChannelToAdd);
                }
            }

            return false;
        }

        public virtual bool RemoveaggregationChannel(ConversationReference aggregationChannelToRemove)
        {
            return ExecuteRemoveAggregationChannel(aggregationChannelToRemove);
        }

        public virtual MessageRouterResult AddConnectionRequest(
            ConversationReference requestor, bool rejectConnectionRequestIfNoAggregationChannel = false)
        {
            if (requestor == null)
            {
                throw new ArgumentNullException("Requestor missing");
            }

            MessageRouterResult addConnectionRequestResult = new MessageRouterResult();
            addConnectionRequestResult.ConversationReferences.Add(requestor);

            AddConversationReference(requestor);
            ConnectionRequest connectionRequest = new ConnectionRequest(requestor);

            if (IsAssociatedWithAggregation(requestor))
            {
                addConnectionRequestResult.Type = MessageRouterResultType.Error;
                addConnectionRequestResult.ErrorMessage = $"The given ConversationReference ({MessageRoutingUtils.GetChannelAccount(requestor)?.Name}) is associated with aggregation and hence invalid to request a connection";
            }
            else if (GetConnectionRequests().Contains(connectionRequest))
            {
                addConnectionRequestResult.Type = MessageRouterResultType.ConnectionAlreadyRequested;
            }
            else
            {
                if (!GetAggregationChannels().Any() && rejectConnectionRequestIfNoAggregationChannel)
                {
                    addConnectionRequestResult.Type = MessageRouterResultType.NoAgentsAvailable;
                }
                else
                {
                    connectionRequest.ConnectionRequestTime = GetCurrentGlobalTime();

                    if (ExecuteAddConnectionRequest(connectionRequest))
                    {
                        addConnectionRequestResult.Type = MessageRouterResultType.ConnectionRequested;
                    }
                    else
                    {
                        addConnectionRequestResult.Type = MessageRouterResultType.Error;
                        addConnectionRequestResult.ErrorMessage = "Failed to add the connection request - this is likely an error caused by the storage implementation";
                    }
                }
            }

            return addConnectionRequestResult;
        }

        public virtual MessageRouterResult RemoveConnectionRequest(ConnectionRequest connectionRequestToRemove)
        {
            MessageRouterResult removeConnectionRequestResult = new MessageRouterResult();
            removeConnectionRequestResult.ConversationReferences.Add(connectionRequestToRemove.Requestor);

            if (GetConnectionRequests().Contains(connectionRequestToRemove))
            {
                if (ExecuteRemoveConnectionRequest(connectionRequestToRemove))
                {
                    removeConnectionRequestResult.Type = MessageRouterResultType.ConnectionRejected;
                }
                else
                {
                    removeConnectionRequestResult.Type = MessageRouterResultType.Error;
                    removeConnectionRequestResult.ErrorMessage = "Failed to remove the connection request of the given ConversationReference";
                }
            }
            else
            {
                removeConnectionRequestResult.Type = MessageRouterResultType.Error;
                removeConnectionRequestResult.ErrorMessage = "Could not find a connection request for the given ConversationReference";
            }

            return removeConnectionRequestResult;
        }

        public virtual bool IsConnected(ConversationReference conversationReference)
        {
            foreach (Connection connection in GetConnections())
            {
                if (MessageRoutingUtils.HasMatchingChannelAccountInformation(conversationReference, connection.ConversationReference1)
                    || MessageRoutingUtils.HasMatchingChannelAccountInformation(conversationReference, connection.ConversationReference2))
                {
                    return true;
                }
            }

            return false;
        }

        public virtual ConversationReference GetConnectedCounterpart(ConversationReference conversationReferenceWhoseCounterpartToFind)
        {
            foreach (Connection connection in GetConnections())
            {
                if (MessageRoutingUtils.HasMatchingChannelAccountInformation(
                        conversationReferenceWhoseCounterpartToFind, connection.ConversationReference1))
                {
                    return connection.ConversationReference2;
                }
                else if (MessageRoutingUtils.HasMatchingChannelAccountInformation(
                    conversationReferenceWhoseCounterpartToFind, connection.ConversationReference2))
                {
                    return connection.ConversationReference1;
                } 
            }

            return null;
        }

        public virtual MessageRouterResult ConnectAndRemoveConnectionRequest(
            ConversationReference conversationOwnerConversationReference, ConversationReference conversationClientConversationReference)
        {
            MessageRouterResult result = new MessageRouterResult()
            {
                ConversationReference1 = conversationOwnerConversationReference,
                ConversationReference2 = conversationClientConversationReference
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
                    ExecuteRemoveConnectionRequest(conversationClientConversationReference);
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

        public virtual MessageRouterResult Disconnect(Connection connectionToDisconnect)
        {
            MessageRouterResult disconnectResult = null;

            foreach (Connection connection in GetConnections())
            {
                if (connectionToDisconnect.Equals(connection))
                {
                    if (ExecuteRemoveConnection(connectionToDisconnect))
                    {

                    }

                    break;
                }
            }

            return disconnectResult;
        }

        public virtual void DeleteAll()
        {
#if DEBUG
            LastMessageRouterResults.Clear();
#endif
        }

        public virtual bool IsAssociatedWithAggregation(ConversationReference ConversationReference)
        {
            IList<ConversationReference> aggregationParties = GetAggregationChannels();

            return (ConversationReference != null && aggregationParties != null && aggregationParties.Count() > 0
                    && aggregationParties.Where(aggregationChannel =>
                        aggregationChannel.ConversationAccount.Id == ConversationReference.ConversationAccount.Id
                        && aggregationChannel.ServiceUrl == ConversationReference.ServiceUrl
                        && aggregationChannel.ChannelId == ConversationReference.ChannelId).Count() > 0);
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
                foundConversationReference = GetConnections().Keys.Single(ConversationReference =>
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
                    foundConversationReference = GetConnections().Values.First(ConversationReference =>
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

        #endregion


        #region Protected Virtual Methods

        protected virtual DateTime GetCurrentGlobalTime()
        {
            return (GlobalTimeProvider == null) ? DateTime.UtcNow : GlobalTimeProvider.GetCurrentTime();
        }

        #endregion
    }
}
