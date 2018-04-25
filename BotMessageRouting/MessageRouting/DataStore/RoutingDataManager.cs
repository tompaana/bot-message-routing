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

        /// <summary>
        /// Adds the given ConversationReference.
        /// </summary>
        /// <param name="conversationReferenceToAdd">The new ConversationReference to add.</param>
        /// <returns>True, if the given ConversationReference was added. False otherwise (was null or already stored).</returns>
        public virtual bool AddConversationReference(ConversationReference conversationReferenceToAdd)
        {
            if (conversationReferenceToAdd.Bot == null
                && conversationReferenceToAdd.User == null)
            {
                throw new ArgumentNullException("Both channel accounts in the conversation reference cannot be null");
            }

            if (conversationReferenceToAdd == null
                || (MessageRoutingUtils.IsBot(conversationReferenceToAdd) ?
                    GetBotInstances().Contains(conversationReferenceToAdd)
                    : GetUsers().Contains(conversationReferenceToAdd)))
            {
                return false;
            }

            return RoutingDataStore.AddConversationReference(conversationReferenceToAdd);
        }

        /// <summary>
        /// Removes the specified ConversationReference from all possible containers.
        /// Note that this method removes the ConversationReference's every instance (user from all conversations
        /// in addition to connection requests).
        /// </summary>
        /// <param name="conversationReferenceToRemove">The ConversationReference to remove.</param>
        /// <returns>A list of operation result(s):
        /// - MessageRouterResultType.NoActionTaken, if the was not found in any collection OR
        /// - MessageRouterResultType.OK, if the ConversationReference was removed from the collection AND
        /// - MessageRouterResultType.ConnectionRejected, if the ConversationReference had a connection request AND
        /// - Disconnect() results, if the ConversationReference was connected in a conversation.
        /// </returns>
        public virtual IList<MessageRouterResult> RemoveConversationReference(ConversationReference conversationReferenceToRemove)
        {
            List<MessageRouterResult> messageRouterResults = new List<MessageRouterResult>();
            bool wasRemoved = false;

            // Check users and bots
            IList<ConversationReference> conversationReferenceList =
                (conversationReferenceToRemove.Bot == null)
                ? GetUsers() : GetBotInstances();

            IList<ConversationReference> conversationReferencesToRemove =
                MessageRoutingUtils.ResolveConversationReferencesWithMatchingChannelAccount(
                    conversationReferenceToRemove, conversationReferenceList);

            if (conversationReferencesToRemove != null)
            {
                foreach (ConversationReference conversationReference in conversationReferencesToRemove)
                {
                    wasRemoved = RoutingDataStore.RemoveConversationReference(conversationReference);

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
                    if (MessageRoutingUtils.HasMatchingChannelAccounts(
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
                    if (MessageRoutingUtils.HasMatchingChannelAccounts(conversationReferenceToRemove, connection.ConversationReference1)
                        || MessageRoutingUtils.HasMatchingChannelAccounts(conversationReferenceToRemove, connection.ConversationReference2))
                    {
                        wasRemoved = true;
                        messageRouterResults.Add(Disconnect(connection)); // TODO: Check that the disconnect was successful
                        break;
                    }
                }
            }

            return messageRouterResults;
        }

        /// <returns>The aggregation channels as a readonly list.</returns>
        public IList<ConversationReference> GetAggregationChannels()
        {
            return RoutingDataStore.GetAggregationChannels();
        }

        /// <summary>
        /// Adds the given aggregation channel.
        /// </summary>
        /// <param name="aggregationChannelToAdd">The aggregation channel to add.</param>
        /// <returns>True, if added. False otherwise (e.g. matching request already exists).</returns>
        public virtual bool AddAggregationChannel(ConversationReference aggregationChannelToAdd)
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
                    return RoutingDataStore.AddAggregationChannel(aggregationChannelToAdd);
                }
            }

            return false;
        }

        /// <summary>
        /// Removes the given aggregation channel.
        /// </summary>
        /// <param name="aggregationChannelToRemove">The aggregation channel to remove.</param>
        /// <returns>True, if removed successfully. False otherwise.</returns>
        public virtual bool RemoveaggregationChannel(ConversationReference aggregationChannelToRemove)
        {
            return RoutingDataStore.RemoveAggregationChannel(aggregationChannelToRemove);
        }

        /// <returns>The connection requests as a readonly list.</returns>
        public IList<ConnectionRequest> GetConnectionRequests()
        {
            return RoutingDataStore.GetConnectionRequests();
        }

        /// <summary>
        /// Adds the given connection request.
        /// </summary>
        /// <param name="connectionRequestToAdd">The connection request to add.</param>
        /// <param name="rejectConnectionRequestIfNoAggregationChannel">If true, will reject all requests, if there is no aggregation channel.</param>
        /// <returns>The result of the operation:
        /// - MessageRouterResultType.ConnectionRequested, if a request was successfully made OR
        /// - MessageRouterResultType.ConnectionAlreadyRequested, if a request for the given ConversationReference already exists OR
        /// - MessageRouterResultType.NoAgentsAvailable, if no aggregation while one is required OR
        /// - MessageRouterResultType.Error in case of an error (see the error message).
        /// </returns>
        public virtual MessageRouterResult AddConnectionRequest(
            ConnectionRequest connectionRequestToAdd, bool rejectConnectionRequestIfNoAggregationChannel = false)
        {
            if (connectionRequestToAdd == null)
            {
                throw new ArgumentNullException("Connection request is null");
            }

            MessageRouterResult addConnectionRequestResult = new MessageRouterResult();

            if (GetConnectionRequests().Contains(connectionRequestToAdd))
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
                    connectionRequestToAdd.ConnectionRequestTime = GetCurrentGlobalTime();

                    if (RoutingDataStore.AddConnectionRequest(connectionRequestToAdd))
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

        /// <summary>
        /// Removes the connection request of the user with the given ConversationReference.
        /// </summary>
        /// <param name="connectionRequestToRemove">The connection request to remove.</param>
        /// <returns>The result of the operation:
        /// - MessageRouterResultType.ConnectionRejected, if the connection request was removed OR
        /// - MessageRouterResultType.Error in case of an error (see the error message).
        /// </returns>
        public virtual MessageRouterResult RemoveConnectionRequest(ConnectionRequest connectionRequestToRemove)
        {
            MessageRouterResult removeConnectionRequestResult = new MessageRouterResult();
            removeConnectionRequestResult.ConversationReferences.Add(connectionRequestToRemove.Requestor);

            if (GetConnectionRequests().Contains(connectionRequestToRemove))
            {
                if (RoutingDataStore.RemoveConnectionRequest(connectionRequestToRemove))
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

        /// <returns>The connections.</returns>
        public IList<Connection> GetConnections()
        {
            return RoutingDataStore.GetConnections();
        }

        /// <summary>
        /// Checks if the there is a connection associated with the given ConversationReference instance.
        /// </summary>
        /// <param name="ConversationReference">The ConversationReference to check.</param>
        /// <returns>True, if a connection was found. False otherwise.</returns>
        public virtual bool IsConnected(ConversationReference conversationReference)
        {
            foreach (Connection connection in GetConnections())
            {
                if (MessageRoutingUtils.HasMatchingChannelAccounts(conversationReference, connection.ConversationReference1)
                    || MessageRoutingUtils.HasMatchingChannelAccounts(conversationReference, connection.ConversationReference2))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Resolves the given ConversationReference's counterpart in a 1:1 conversation.
        /// </summary>
        /// <param name="conversationReferenceWhoseCounterpartToFind">The ConversationReference whose counterpart to resolve.</param>
        /// <returns>The counterpart or null, if not found.</returns>
        public virtual ConversationReference GetConnectedCounterpart(ConversationReference conversationReferenceWhoseCounterpartToFind)
        {
            foreach (Connection connection in GetConnections())
            {
                if (MessageRoutingUtils.HasMatchingChannelAccounts(
                        conversationReferenceWhoseCounterpartToFind, connection.ConversationReference1))
                {
                    return connection.ConversationReference2;
                }
                else if (MessageRoutingUtils.HasMatchingChannelAccounts(
                            conversationReferenceWhoseCounterpartToFind, connection.ConversationReference2))
                {
                    return connection.ConversationReference1;
                } 
            }

            return null;
        }

        /// <summary>
        /// Adds the given connection and clears the connection request associated with the given
        /// ConversationReference instance, if one exists.
        /// </summary>
        /// <param name="connectionToAdd">The connection to add.</param>
        /// <param name="requestorConversationReference"></param>
        /// <returns>The result of the operation:
        /// - MessageRouterResultType.Connected, if successfully connected OR
        /// - MessageRouterResultType.Error in case of an error (see the error message).
        /// </returns>
        public virtual MessageRouterResult ConnectAndRemoveConnectionRequest(
            Connection connectionToAdd, ConversationReference requestorConversationReference)
        {
            MessageRouterResult connectResult = new MessageRouterResult();

            DateTime connectionEstablishedTime = GetCurrentGlobalTime();
            connectionToAdd.LastInteractionTime = connectionEstablishedTime;

            bool wasConnectionAdded = RoutingDataStore.AddConnection(connectionToAdd);

            if (wasConnectionAdded)
            {
                connectResult.Type = MessageRouterResultType.Connected;
                RemoveConnectionRequest(FindConnectionRequestByConversationReference(requestorConversationReference));
            }
            else
            {
                connectResult.Type = MessageRouterResultType.Error;
                connectResult.ErrorMessage = $"Failed to add the connection {connectionToAdd}";
            }

            return connectResult;
        }

        /// <summary>
        /// Disconnects the given connection.
        /// </summary>
        /// <param name="connectionToDisconnect">The connection to disconnect.</param>
        /// <returns>The result of the operation:
        /// - MessageRouterResultType.NoActionTaken, if no connection to disconnect was found OR,
        /// - MessageRouterResultType.Disconnected for each disconnection, when successful.
        /// </returns>
        public virtual MessageRouterResult Disconnect(Connection connectionToDisconnect)
        {
            MessageRouterResult disconnectResult = null;

            foreach (Connection connection in GetConnections())
            {
                if (connectionToDisconnect.Equals(connection))
                {
                    if (RoutingDataStore.RemoveConnection(connectionToDisconnect))
                    {
                        disconnectResult = new MessageRouterResult()
                        {
                            Type = MessageRouterResultType.Disconnected
                        };

                        if (connectionToDisconnect.ConversationReference1 != null)
                        {
                            disconnectResult.ConversationReferences.Add(connectionToDisconnect.ConversationReference1);
                        }

                        if (connectionToDisconnect.ConversationReference2 != null)
                        {
                            disconnectResult.ConversationReferences.Add(connectionToDisconnect.ConversationReference2);
                        }
                    }

                    break;
                }
            }

            if (disconnectResult == null)
            {
                disconnectResult = new MessageRouterResult()
                {
                    Type = MessageRouterResultType.Error,
                    ErrorMessage = "Failed to find the connection"
                };
            }

            return disconnectResult;
        }

        /// <summary>
        /// Checks if the given ConversationReference is associated with aggregation. In human toung this means
        /// that the given ConversationReference is, for instance, a customer service agent who deals with the
        /// requests coming from customers.
        /// </summary>
        /// <param name="conversationReference">The ConversationReference to check.</param>
        /// <returns>True, if is associated. False otherwise.</returns>
        public virtual bool IsAssociatedWithAggregation(ConversationReference conversationReference)
        {
            IList<ConversationReference> aggregationParties = GetAggregationChannels();

            return (conversationReference != null && aggregationParties != null && aggregationParties.Count() > 0
                    && aggregationParties.Where(aggregationChannel =>
                        aggregationChannel.Conversation.Id == conversationReference.Conversation.Id
                        && aggregationChannel.ServiceUrl == conversationReference.ServiceUrl
                        && aggregationChannel.ChannelId == conversationReference.ChannelId).Count() > 0);
        }

        /// <summary>
        /// Tries to resolve the name of the bot in the same conversation with the given ConversationReference.
        /// </summary>
        /// <param name="conversationReference">The ConversationReference from whose perspective to resolve the name.</param>
        /// <returns>The name of the bot or null, if unable to resolve.</returns>
        public virtual string ResolveBotNameInConversation(ConversationReference conversationReference)
        {
            string botName = null;

            if (conversationReference != null)
            {
                ConversationReference botConversationReference =
                    FindBotConversationReferenceByChannelAndConversation(
                        conversationReference.ChannelId, conversationReference.Conversation);

                if (botConversationReference != null && botConversationReference.Bot != null)
                {
                    botName = botConversationReference.Bot.Name;
                }
            }

            return botName;
        }

        /// <summary>
        /// Tries to find a connection request by the given conversation reference
        /// (associated with the requestor).
        /// </summary>
        /// <param name="conversationReference">The conversation reference associated with the requestor.</param>
        /// <returns>The connection request or null, if not found.</returns>
        public ConnectionRequest FindConnectionRequestByConversationReference(ConversationReference conversationReference)
        {
            foreach (ConnectionRequest connectionRequest in GetConnectionRequests())
            {
                if (MessageRoutingUtils.HasMatchingChannelAccounts(conversationReference, connectionRequest.Requestor))
                {
                    return connectionRequest;
                }
            }

            return null;
        }

        /// <summary>
        /// Tries to find a stored ConversationReference instance matching the given channel account ID and
        /// conversation ID.
        /// </summary>
        /// <param name="channelAccountId">The channel account ID (user ID).</param>
        /// <param name="conversationId">The conversation ID.</param>
        /// <returns>The ConversationReference instance matching the given IDs or null if not found.</returns>
        public virtual ConversationReference FindConversationReferenceByChannelAccountIdAndConversationId(
            string channelAccountId, string conversationId)
        {
            ConversationReference conversationReference = null;
            
            try
            {
                conversationReference = GetUsers().Single(userConversationReference =>
                        (userConversationReference.User.Id.Equals(channelAccountId)
                         && userConversationReference.Conversation.Id.Equals(conversationId)));
            }
            catch (InvalidOperationException)
            {
            }

            if (conversationReference == null)
            {
                try
                {
                    conversationReference = GetBotInstances().Single(botConversationReference =>
                            (botConversationReference.User.Id.Equals(channelAccountId)
                             && botConversationReference.Conversation.Id.Equals(conversationId)));
                }
                catch (InvalidOperationException)
                {
                }
            }

            return conversationReference;
        }

        /// <summary>
        /// Tries to find a stored bot ConversationReference instance matching the given channel ID and
        /// conversation account.
        /// </summary>
        /// <param name="channelId">The channel ID.</param>
        /// <param name="conversationAccount">The conversation account.</param>
        /// <returns>The bot ConversationReference instance matching the given details or null if not found.</returns>
        public virtual ConversationReference FindBotConversationReferenceByChannelAndConversation(
            string channelId, ConversationAccount conversationAccount)
        {
            ConversationReference conversationReference = null;

            try
            {
                conversationReference = GetUsers().Single(userConversationReference =>
                        (userConversationReference.ChannelId.Equals(channelId)
                         && userConversationReference.Conversation.Id.Equals(conversationAccount.Id)));
            }
            catch (InvalidOperationException)
            {
            }

            if (conversationReference == null)
            {
                try
                {
                    conversationReference = GetBotInstances().Single(botConversationReference =>
                            (botConversationReference.ChannelId.Equals(channelId)
                             && botConversationReference.Conversation.Id.Equals(conversationAccount.Id)));
                }
                catch (InvalidOperationException)
                {
                }
            }

            return conversationReference;
        }

        /// <summary>
        /// Tries to find a ConversationReference connected in a conversation.
        /// </summary>
        /// <param name="channelId">The channel ID.</param>
        /// <param name="channelAccount">The channel account.</param>
        /// <returns>The ConversationReference matching the given details or null if not found.</returns>
        public virtual ConversationReference FindConnectedConversationReferenceByChannel(
            string channelId, ChannelAccount channelAccount)
        {
            ConversationReference conversationReference = null;

            foreach (Connection connection in GetConnections())
            {
                ChannelAccount channelAccount1 =
                    MessageRoutingUtils.GetChannelAccount(connection.ConversationReference1);
                ChannelAccount channelAccount2 =
                    MessageRoutingUtils.GetChannelAccount(connection.ConversationReference2);

                if (connection.ConversationReference1.ChannelId.Equals(channelId)
                    && channelAccount1 != null
                    && channelAccount1.Id.Equals(channelAccount.Id))
                {
                    conversationReference = connection.ConversationReference1;
                    break;
                }
                else if (connection.ConversationReference2.ChannelId.Equals(channelId)
                    && channelAccount2 != null
                    && channelAccount2.Id.Equals(channelAccount.Id))
                {
                    conversationReference = connection.ConversationReference2;
                    break;
                }
            }

            return conversationReference;
        }

        /// <returns>The current global time.</returns>
        public virtual DateTime GetCurrentGlobalTime()
        {
            return (GlobalTimeProvider == null) ? DateTime.UtcNow : GlobalTimeProvider.GetCurrentTime();
        }
    }
}
