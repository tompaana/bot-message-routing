using Microsoft.Bot.Connector;
using Microsoft.Bot.Schema;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Underscore.Bot.MessageRouting.DataStore;
using Underscore.Bot.MessageRouting.Models;
using Underscore.Bot.MessageRouting.Results;
using Underscore.Bot.MessageRouting.Utils;

namespace Underscore.Bot.MessageRouting
{
    /// <summary>
    /// Provides the main interface for message routing.
    /// </summary>
    public class MessageRouter
    {
        /// <summary>
        /// The routing data and all the parties the bot has seen including the instances of itself.
        /// </summary>
        public RoutingDataManager RoutingDataManager
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
        public MessageRouter(IRoutingDataStore routingDataStore, GlobalTimeProvider globalTimeProvider = null)
        {
            RoutingDataManager = new RoutingDataManager(routingDataStore, globalTimeProvider);
        }

        /// <summary>
        /// Constructs a conversation reference instance using the sender (Activity.From) of the given activity.
        /// </summary>
        /// <param name="activity"></param>
        /// <returns>A newly created conversation reference instance.</returns>
        public static ConversationReference CreateSenderConversationReference(IActivity activity)
        {
            return new ConversationReference(
                null,
                activity.From,
                null,
                activity.Conversation,
                activity.ChannelId,
                activity.ServiceUrl);
        }

        /// <summary>
        /// Constructs a conversation reference instance using the recipient of the given activity.
        /// </summary>
        /// <param name="activity"></param>
        /// <returns>A newly created conversation reference instance.</returns>
        public static ConversationReference CreateRecipientConversationReference(IActivity activity)
        {
            return new ConversationReference(
                null,
                null,
                activity.Recipient,
                activity.Conversation,
                activity.ChannelId,
                activity.ServiceUrl);
        }

        /// <summary>
        /// Replies to the given activity with the given message.
        /// </summary>
        /// <param name="activity">The activity to reply to.</param>
        /// <param name="message">The message.</param>
        public static async Task ReplyToActivityAsync(Activity activity, string message)
        {
            if (activity != null && !string.IsNullOrEmpty(message))
            {
                Activity replyActivity = activity.CreateReply(message);
                ConnectorClient connector = new ConnectorClient(new Uri(activity.ServiceUrl));
                await connector.Conversations.ReplyToActivityAsync(replyActivity);
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"Either the activity is null or the message is empty - Activity: {activity}; message: {message}");
            }
        }

        /// <summary>
        /// Handles the new activity:
        ///   1. Adds both the sender and the receiver of the given activity into the routing data
        ///      storage (if they do not already exist there). 
        ///   2. The message in the given activity is routed to the appropriate receiver (user),
        ///      if the its sender is connected in a conversation with the receiver.
        ///   3. If connection requests are set to happen automatically
        ///      (tryToRequestConnectionIfNotConnected is true) and the sender is not yet
        ///      connected in a conversation, a request is made.
        /// </summary>
        /// <param name="activity">The activity to handle.</param>
        /// <param name="tryToRequestConnectionIfNotConnected">If true, will try to initiate
        /// the connection (1:1 conversation) automatically, if the sender is not connected already.</param>
        /// <param name="rejectConnectionRequestIfNoAggregationChannel">If true and the automatical
        /// connection request is made, will reject all requests, if there is no aggregation channel.</param>
        /// <param name="addSenderNameToMessage">If true, will add the name of the sender to the beginning of the message.</param>
        /// <param name="addOwnerNameToMessage">If true, will add the owner's (agent) name to the beginning of the message.</param>
        /// <returns>The result of the operation.</returns>    
        public virtual async Task<AbstractMessageRouterResult> HandleActivityAsync(
            IMessageActivity activity,
            bool tryToRequestConnectionIfNotConnected,
            bool rejectConnectionRequestIfNoAggregationChannel,
            bool addSenderNameToMessage = true)
        {
            StoreConversationReferences(activity);

            MessageRoutingResult messageRoutingResult =
                await RouteMessageIfSenderIsConnectedAsync(activity, addSenderNameToMessage);

            if (tryToRequestConnectionIfNotConnected
                && messageRoutingResult.Type == MessageRoutingResultType.NoActionTaken)
            {
                return CreateConnectionRequest(
                    CreateSenderConversationReference(activity),
                    rejectConnectionRequestIfNoAggregationChannel);
            }

            return messageRoutingResult;
        }

        /// <summary>
        /// Tries to send the given message activity to the given conversation reference using this bot on the same
        /// channel as the conversation reference who the message is sent to.
        /// </summary>
        /// <param name="conversationReferenceToMessage">The conversation reference to send the message to.</param>
        /// <param name="messageActivity">The message activity to send (message content).</param>
        /// <returns>A valid ResourceResponse instance, if successful. Null in case of an error.</returns>
        public virtual async Task<ResourceResponse> SendMessageAsync(
            ConversationReference conversationReferenceToMessage, IMessageActivity messageActivity)
        {
            ConversationReference botConversationReference = null;

            if (conversationReferenceToMessage != null)
            {
                // We need the channel account of the bot in the SAME CHANNEL as the RECIPIENT.
                // The identity of the bot in the channel of the sender is most likely a different one and
                // thus unusable since it will not be recognized on the recipient's channel.
                botConversationReference = RoutingDataManager.FindConversationReference(
                    conversationReferenceToMessage.ChannelId, conversationReferenceToMessage.Conversation.Id, null, true);
            }

            if (botConversationReference != null
                && botConversationReference.Bot != null)
            {
                messageActivity.From = botConversationReference.Bot;
                messageActivity.Recipient = conversationReferenceToMessage.User;

                ConnectorClientMessageBundle bundle = new ConnectorClientMessageBundle(
                        conversationReferenceToMessage.ServiceUrl, messageActivity);

                ResourceResponse resourceResponse = null;

                try
                {
                    resourceResponse =
                        await bundle.ConnectorClient.Conversations.SendToConversationAsync(
                            (Activity)bundle.MessageActivity);
                }
                catch (UnauthorizedAccessException e)
                {
                    System.Diagnostics.Debug.WriteLine($"Failed to send message: {e.Message}");
                }
                catch (Exception e)
                {
                    System.Diagnostics.Debug.WriteLine($"Failed to send message: {e.Message}");
                }

                return resourceResponse;
            }

            return null;
        }

        /// <summary>
        /// For convenience.
        /// </summary>
        /// <param name="conversationReferenceToMessage">The conversation reference instance to send the message to.</param>
        /// <param name="messageText">The message text content.</param>
        /// <returns>A valid ResourceResponse instance, if successful. Null in case of an error.</returns>
        public virtual async Task<ResourceResponse> SendMessageAsync(
            ConversationReference conversationReferenceToMessage, string messageText)
        {
            ConversationReference botConversationReference = null;

            if (conversationReferenceToMessage != null)
            {
                botConversationReference = RoutingDataManager.FindConversationReference(
                    conversationReferenceToMessage.ChannelId, conversationReferenceToMessage.Conversation.Id, null, true);
            }

            IMessageActivity messageActivity =
                ConnectorClientMessageBundle.CreateMessageActivity(
                    conversationReferenceToMessage, botConversationReference?.Bot, messageText);

            return await SendMessageAsync(conversationReferenceToMessage, messageActivity);
        }

        /// <summary>
        /// Stores the conversation reference instances (sender and recipient) in the given activity.
        /// </summary>
        /// <param name="activity">The activity.</param>
        public void StoreConversationReferences(IActivity activity)
        {
            RoutingDataManager.AddConversationReference(CreateSenderConversationReference(activity));
            RoutingDataManager.AddConversationReference(CreateRecipientConversationReference(activity));
        }

        /// <summary>
        /// Tries to initiate a connection (1:1 conversation) by creating a request on behalf of
        /// the given requestor. This method does nothing, if a request for the same user already exists.
        /// </summary>
        /// <param name="requestor">The requestor conversation reference.</param>
        /// <param name="rejectConnectionRequestIfNoAggregationChannel">
        /// If true, will reject all requests, if there is no aggregation channel.</param>
        /// <returns>The result of the operation:
        /// - ConnectionRequestResultType.OK,
        /// - ConnectionRequestResultType.AlreadyRequested,
        /// - ConnectionRequestResultType.NotSetup or
        /// - ConnectionRequestResultType.Error (see the error message for more details).
        /// </returns>
        public virtual ConnectionRequestResult CreateConnectionRequest(
            ConversationReference requestor, bool rejectConnectionRequestIfNoAggregationChannel = false)
        {
            if (requestor == null)
            {
                throw new ArgumentNullException("Requestor missing");
            }

            ConnectionRequestResult createConnectionRequestResult = null;
            RoutingDataManager.AddConversationReference(requestor);
            ConnectionRequest connectionRequest = new ConnectionRequest(requestor);

            if (RoutingDataManager.IsAssociatedWithAggregation(requestor))
            {
                createConnectionRequestResult = new ConnectionRequestResult()
                {
                    Type = ConnectionRequestResultType.Error,
                    ErrorMessage = $"The given ConversationReference ({RoutingDataManager.GetChannelAccount(requestor, out bool isBot)?.Name}) is associated with aggregation and hence invalid to request a connection"
                };
            }
            else
            {
                createConnectionRequestResult = RoutingDataManager.AddConnectionRequest(
                    connectionRequest, rejectConnectionRequestIfNoAggregationChannel);
            }

            return createConnectionRequestResult;
        }

        /// <summary>
        /// Tries to reject the connection request of the associated with the given conversation reference.
        /// </summary>
        /// <param name="requestorToReject">The conversation reference of the party whose request to reject.</param>
        /// <param name="rejecterConversationReference">The conversation reference of the party rejecting the request (optional).</param>
        /// <returns>The result of the operation:
        /// - ConnectionRequestResultType.Rejected or
        /// - ConnectionRequestResultType.Error (see the error message for more details).
        /// </returns>
        public virtual ConnectionRequestResult RejectConnectionRequest(
            ConversationReference requestorToReject, ConversationReference rejecterConversationReference = null)
        {
            if (requestorToReject == null)
            {
                throw new ArgumentNullException("The conversation reference instance of the party whose request to reject cannot be null");
            }

            ConnectionRequestResult rejectConnectionRequestResult = null;
            ConnectionRequest connectionRequest =
                RoutingDataManager.FindConnectionRequest(requestorToReject);

            if (connectionRequest != null)
            {
                rejectConnectionRequestResult = RoutingDataManager.RemoveConnectionRequest(connectionRequest);
            }

            return rejectConnectionRequestResult;
        }

        /// <summary>
        /// Tries to establish a connection (1:1 chat) between the two given parties.
        /// 
        /// Note that the conversation owner will have a new separate conversation reference in the created
        /// conversation, if a new direct conversation is created.
        /// </summary>
        /// <param name="conversationReference1">The conversation reference who owns the conversation (e.g. customer service agent).</param>
        /// <param name="conversationReference2">The other conversation reference in the conversation.</param>
        /// <param name="createNewDirectConversation">
        /// If true, will try to create a new direct conversation between the bot and the
        /// conversation owner (e.g. agent) where the messages from the other (client) conversation
        /// reference are routed.
        /// 
        /// Note that this will result in the conversation owner having a new separate conversation
        /// reference in the created connection (for the new direct conversation).
        /// </param>
        /// <returns>
        /// The result of the operation:
        /// - ConnectionResultType.Connected,
        /// - ConnectionResultType.Error (see the error message for more details).
        /// </returns>
        public virtual async Task<ConnectionResult> ConnectAsync(
            ConversationReference conversationReference1, ConversationReference conversationReference2, bool createNewDirectConversation)
        {
            if (conversationReference1 == null || conversationReference2 == null)
            {
                throw new ArgumentNullException(
                    $"Neither of the arguments ({nameof(conversationReference1)}, {nameof(conversationReference2)}) can be null");
            }

            ConversationReference botConversationReference =
                RoutingDataManager.FindConversationReference(
                    conversationReference1.ChannelId, conversationReference1.Conversation.Id, null, true);

            if (botConversationReference == null)
            {
                return new ConnectionResult()
                {
                    Type = ConnectionResultType.Error,
                    ErrorMessage = "Failed to find the bot instance"
                };
            }

            ConversationResourceResponse conversationResourceResponse = null;

            if (createNewDirectConversation)
            {
                ConnectorClient connectorClient =
                    new ConnectorClient(new Uri(conversationReference1.ServiceUrl));

                try
                {
                    conversationResourceResponse =
                        await connectorClient.Conversations.CreateDirectConversationAsync(
                            botConversationReference.Bot, conversationReference1.User);
                }
                catch (Exception e)
                {
                    System.Diagnostics.Debug.WriteLine($"Failed to create a direct conversation: {e.Message}");
                    // Do nothing here as we fallback (continue without creating a direct conversation)
                }

                if (conversationResourceResponse != null
                    && !string.IsNullOrEmpty(conversationResourceResponse.Id))
                {
                    // The conversation account of the conversation owner for this 1:1 chat is different -
                    // thus, we need to re-create the conversation owner instance
                    ConversationAccount directConversationAccount =
                        new ConversationAccount(id: conversationResourceResponse.Id);

                    conversationReference1 = new ConversationReference(
                        null,
                        conversationReference1.User,
                        null,
                        directConversationAccount,
                        conversationReference1.ChannelId,
                        conversationReference1.ServiceUrl);

                    RoutingDataManager.AddConversationReference(conversationReference1);
                    RoutingDataManager.AddConversationReference(new ConversationReference(
                        null, null,
                        botConversationReference.Bot,
                        directConversationAccount,
                        botConversationReference.ChannelId,
                        botConversationReference.ServiceUrl
                        ));
                }
            }

            Connection connection = new Connection(conversationReference1, conversationReference2);
            ConnectionResult connectResult =
                RoutingDataManager.ConnectAndRemoveConnectionRequest(connection, conversationReference2);
            connectResult.ConversationResourceResponse = conversationResourceResponse;

            return connectResult;
        }

        /// <summary>
        /// Disconnects all connections associated with the given conversation reference.
        /// </summary>
        /// <param name="connectedConversationReference">The conversation reference connected in a conversation.</param>
        /// <returns>The results:
        /// - ConnectionResultType.Disconnected,
        /// - ConnectionResultType.Error (see the error message for more details).
        /// </returns>
        public virtual IList<ConnectionResult> Disconnect(ConversationReference connectedConversationReference)
        {
            IList<ConnectionResult> disconnectResults = new List<ConnectionResult>();
            bool wasDisconnected = true;

            while (wasDisconnected)
            {
                wasDisconnected = false;
                Connection connection = RoutingDataManager.FindConnection(connectedConversationReference);

                if (connection != null)
                {
                    ConnectionResult disconnectResult = RoutingDataManager.Disconnect(connection);
                    disconnectResults.Add(disconnectResult);

                    if (disconnectResult.Type == ConnectionResultType.Disconnected)
                    {
                        wasDisconnected = true;
                    }
                }
            }

            return disconnectResults;
        }

        /// <summary>
        /// Routes the message in the given activity, if the sender is connected in a conversation.
        /// </summary>
        /// <param name="activity">The activity to handle.</param>
        /// <param name="addNameToMessage">If true, will add the name of the sender to the beginning of the message.</param>
        /// <returns>The result of the operation:
        /// - MessageRouterResultType.NoActionTaken, if no routing rule for the sender is found OR
        /// - MessageRouterResultType.OK, if the message was routed successfully OR
        /// - MessageRouterResultType.FailedToForwardMessage in case of an error (see the error message).
        /// </returns>
        public virtual async Task<MessageRoutingResult> RouteMessageIfSenderIsConnectedAsync(
            IMessageActivity activity, bool addNameToMessage = true)
        {
            ConversationReference senderConversationReference = CreateSenderConversationReference(activity);
            Connection connection = RoutingDataManager.FindConnection(senderConversationReference);

            MessageRoutingResult messageRoutingResult = new MessageRoutingResult()
            {
                Type = MessageRoutingResultType.NoActionTaken,
                Connection = connection
            };

            if (connection != null)
            {
                ConversationReference conversationReferenceToForwardMessageTo =
                    RoutingDataManager.HaveMatchingChannelAccounts(
                        senderConversationReference, connection.ConversationReference1)
                        ? connection.ConversationReference2 : connection.ConversationReference1;

                if (conversationReferenceToForwardMessageTo != null)
                {
                    string message = addNameToMessage
                        ? $"{senderConversationReference.User.Name}: {activity.Text}" : activity.Text;
                    ResourceResponse resourceResponse =
                        await SendMessageAsync(conversationReferenceToForwardMessageTo, activity.Text);

                    if (resourceResponse != null)
                    {
                        messageRoutingResult.Type = MessageRoutingResultType.OK;

                        if (!RoutingDataManager.UpdateTimeSinceLastActivity(connection))
                        {
                            System.Diagnostics.Debug.WriteLine("Failed to update the time since the last activity property of the connection");
                        }
                    }
                    else
                    {
                        messageRoutingResult.Type = MessageRoutingResultType.FailedToForwardMessage;
                        messageRoutingResult.ErrorMessage = $"Failed to forward the message to user {conversationReferenceToForwardMessageTo}";
                    }
                }
                else
                {
                    messageRoutingResult.Type = MessageRoutingResultType.Error;
                    messageRoutingResult.ErrorMessage = "Failed to find the user/bot to forward the message to";
                }
            }

            return messageRoutingResult;
        }
    }
}
