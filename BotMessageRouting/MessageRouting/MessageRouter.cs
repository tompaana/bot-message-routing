using Microsoft.Bot.Connector;
using Microsoft.Bot.Schema;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Underscore.Bot.MessageRouting.DataStore;
using Underscore.Bot.Models;
using Underscore.Bot.Utils;

namespace Underscore.Bot.MessageRouting
{
    /// <summary>
    /// Provides the main interface for message routing.
    /// 
    /// Note that your bot should only ever have but one instance of this class!
    /// </summary>
    public class MessageRouter
    {
        /// <summary>
        /// The routing data and all the parties the bot has seen including the instances of itself.
        /// </summary>
        public IRoutingDataManager RoutingDataManager
        {
            get;
            set;
        }

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="routingDataManager">The routing data manager.</param>
        public MessageRouter(IRoutingDataManager routingDataManager)
        {
            RoutingDataManager = routingDataManager;
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
        /// <param name="addClientNameToMessage">If true, will add the client's name to the beginning of the message.</param>
        /// <param name="addOwnerNameToMessage">If true, will add the owner's (agent) name to the beginning of the message.</param>
        /// <returns>The result of the operation:
        /// - MessageRouterResultType.NoActionTaken, if the activity was not consumed (no special action taken) OR
        /// - MessageRouterResultType.ConnectionRequested, if a request was successfully made OR
        /// - MessageRouterResultType.ConnectionAlreadyRequested, if a request for the given ConversationReference already exists OR
        /// - MessageRouterResultType.NoAgentsAvailable, if no aggregation channel exists while one is required OR
        /// - MessageRouterResultType.OK, if the activity was consumed and the message was routed successfully OR
        /// - MessageRouterResultType.FailedToForwardMessage in case a rule to route the message was in place, but the action failed (see the error message) OR
        /// - MessageRouterResultType.Error in case of an error (see the error message).
        /// </returns>    
        public virtual async Task<MessageRouterResult> HandleActivityAsync(
            IMessageActivity activity,
            bool tryToRequestConnectionIfNotConnected,
            bool rejectConnectionRequestIfNoAggregationChannel,
            bool addClientNameToMessage = true,
            bool addOwnerNameToMessage = false)
        {
            StoreConversationReferences(activity);

            MessageRouterResult messageRouterResult =
                await RouteMessageIfSenderIsConnectedAsync(activity, addClientNameToMessage, addOwnerNameToMessage);

            if (tryToRequestConnectionIfNotConnected
                && messageRouterResult.Type == MessageRouterResultType.NoActionTaken)
            {
                messageRouterResult = CreateConnectionRequest(
                    MessageRoutingUtils.CreateSenderConversationReference(activity),
                    rejectConnectionRequestIfNoAggregationChannel);
            }

            return messageRouterResult;
        }

        /// <summary>
        /// Tries to send the given message activity to the given ConversationReference using this bot on the same
        /// channel as the ConversationReference who the message is sent to.
        /// </summary>
        /// <param name="conversationReferenceToMessage">The ConversationReference to send the message to.</param>
        /// <param name="messageActivity">The message activity to send (message content).</param>
        /// <returns>A valid ResourceResponse instance, if successful. Null in case of an error.</returns>
        public async Task<ResourceResponse> SendMessageAsync(
            ConversationReference conversationReferenceToMessage, IMessageActivity messageActivity)
        {
            ConversationReference botConversationReference = null;

            if (conversationReferenceToMessage != null)
            {
                // We need the channel account of the bot in the SAME CHANNEL as the RECIPIENT.
                // The identity of the bot in the channel of the sender is most likely a different one and
                // thus unusable since it will not be recognized on the recipient's channel.
                botConversationReference = RoutingDataManager.FindBotConversationReferenceByChannelAndConversation(
                    conversationReferenceToMessage.ChannelId, conversationReferenceToMessage.Conversation);
            }

            if (botConversationReference != null
                && botConversationReference.Bot != null)
            {
                messageActivity.From = botConversationReference.Bot;
                messageActivity.Recipient = conversationReferenceToMessage.User;

                MessageRoutingUtils.ConnectorClientAndMessageBundle bundle =
                    MessageRoutingUtils.CreateConnectorClientAndMessageActivity(
                        conversationReferenceToMessage.ServiceUrl, messageActivity);

                return await SendAsync(bundle);
            }

            return null;
        }

        /// <summary>
        /// Tries to send the given message to the given ConversationReference using this bot on the same channel
        /// as the ConversationReference who the message is sent to.
        /// </summary>
        /// <param name="ConversationReferenceToMessage">The ConversationReference to send the message to.</param>
        /// <param name="messageText">The message content.</param>
        /// <returns>A valid ResourceResponse instance, if successful. Null in case of an error.</returns>
        public async Task<ResourceResponse> SendMessageAsync(ConversationReference ConversationReferenceToMessage, string messageText)
        {
            ConversationReference botConversationReference = null;

            if (ConversationReferenceToMessage != null)
            {
                botConversationReference = RoutingDataManager.FindBotConversationReferenceByChannelAndConversation(
                    ConversationReferenceToMessage.ChannelId, ConversationReferenceToMessage.Conversation);
            }

            if (botConversationReference != null)
            {
                MessageRoutingUtils.ConnectorClientAndMessageBundle bundle =
                    MessageRoutingUtils.CreateConnectorClientAndMessageActivity(
                        ConversationReferenceToMessage, messageText, botConversationReference?.Bot);

                return await SendAsync(bundle);
            }

            return null;
        }

        /// <summary>
        /// Stores the ConversationReference instances (sender and recipient) in the given activity.
        /// </summary>
        /// <param name="activity">The activity.</param>
        public void StoreConversationReferences(IActivity activity)
        {
            RoutingDataManager.AddConversationReference(MessageRoutingUtils.CreateSenderConversationReference(activity));
            RoutingDataManager.AddConversationReference(MessageRoutingUtils.CreateRecipientConversationReference(activity));
        }

        /// <summary>
        /// Tries to initiate a connection (1:1 conversation) by creating a request on behalf of
        /// the given ConversationReference. This method does nothing, if a request for the same user already exists.
        /// </summary>
        /// <param name="requestorConversationReference">The requestor ConversationReference.</param>
        /// <param name="rejectConnectionRequestIfNoAggregationChannel">If true, will reject all requests, if there is no aggregation channel.</param>
        /// <returns>The result of the operation:
        /// - MessageRouterResultType.ConnectionRequested, if a request was successfully made OR
        /// - MessageRouterResultType.ConnectionAlreadyRequested, if a request for the given ConversationReference already exists OR
        /// - MessageRouterResultType.NoAgentsAvailable, if no aggregation channel exists while one is required OR
        /// - MessageRouterResultType.Error in case of an error (see the error message).
        /// </returns>
        public MessageRouterResult CreateConnectionRequest(
            ConversationReference requestorConversationReference, bool rejectConnectionRequestIfNoAggregationChannel = false)
        {
            return RoutingDataManager.AddConnectionRequest(requestorConversationReference, rejectConnectionRequestIfNoAggregationChannel);
        }

        /// <summary>
        /// Tries to reject the pending connection request of the given ConversationReference.
        /// </summary>
        /// <param name="ConversationReferenceToReject">The ConversationReference whose request to reject.</param>
        /// <param name="rejecterConversationReference">The ConversationReference rejecting the request (optional).</param>
        /// <returns>The result of the operation:
        /// - MessageRouterResultType.ConnectionRejected, if the connection request was removed OR
        /// - MessageRouterResultType.Error in case of an error (see the error message).
        /// </returns>
        public virtual MessageRouterResult RejectConnectionRequest(ConversationReference ConversationReferenceToReject, ConversationReference rejecterConversationReference = null)
        {
            if (ConversationReferenceToReject == null)
            {
                throw new ArgumentNullException($"The ConversationReference to reject ({nameof(ConversationReferenceToReject)} cannot be null");
            }

            MessageRouterResult messageRouteResult = RoutingDataManager.RemoveConnectionRequest(ConversationReferenceToReject);
            messageRouteResult.ConversationReference2 = ConversationReferenceToReject;
            messageRouteResult.ConversationReference1 = rejecterConversationReference;

            if (messageRouteResult.Type == MessageRouterResultType.Error)
            {
                messageRouteResult.ErrorMessage =
                    $"Failed to remove the connection request of user \"{ConversationReferenceToReject.User?.Name}\": {messageRouteResult.ErrorMessage}";
            }

            return messageRouteResult;
        }

        /// <summary>
        /// Tries to establish 1:1 chat between the two given parties.
        /// 
        /// Note that the conversation owner will have a new separate ConversationReference in the created
        /// conversation, if a new direct conversation is created.
        /// </summary>
        /// <param name="conversationOwnerConversationReference">The ConversationReference who owns the conversation (e.g. customer service agent).</param>
        /// <param name="conversationClientConversationReference">The other ConversationReference in the conversation.</param>
        /// <param name="createNewDirectConversation">If true, will try to create a new direct conversation between
        /// the bot and the conversation owner (e.g. agent) where the messages from the other (client) ConversationReference are routed.
        /// Note that this will result in the conversation owner having a new separate ConversationReference in the created connection
        /// (for the new direct conversation).</param>
        /// <returns>
        /// The result of the operation:
        /// - MessageRouterResultType.Connected, if successfully connected OR
        /// - MessageRouterResultType.Error in case of an error (see the error message).
        /// 
        /// The result will also contain the connected parties and note that the client's identity
        /// will have changed, if a new direct conversation was created!
        /// </returns>
        public virtual async Task<MessageRouterResult> ConnectAsync(
            ConversationReference conversationOwnerConversationReference, ConversationReference conversationClientConversationReference, bool createNewDirectConversation)
        {
            if (conversationOwnerConversationReference == null || conversationClientConversationReference == null)
            {
                throw new ArgumentNullException(
                    $"Neither of the arguments ({nameof(conversationOwnerConversationReference)}, {nameof(conversationClientConversationReference)}) can be null");
            }

            MessageRouterResult result = new MessageRouterResult()
            {
                ConversationReference1 = conversationOwnerConversationReference,
                ConversationReference2 = conversationClientConversationReference
            };

            ConversationReference botConversationReference = RoutingDataManager.FindBotConversationReferenceByChannelAndConversation(
                conversationOwnerConversationReference.ChannelId, conversationOwnerConversationReference.Conversation);

            if (botConversationReference != null)
            {
                if (createNewDirectConversation)
                {
                    ConnectorClient connectorClient = new ConnectorClient(new Uri(conversationOwnerConversationReference.ServiceUrl));
                    ConversationResourceResponse conversationResourceResponse = null;

                    try
                    {
                        conversationResourceResponse =
                            await connectorClient.Conversations.CreateDirectConversationAsync(
                                botConversationReference.Bot, conversationOwnerConversationReference.User);
                    }
                    catch (Exception e)
                    {
                        System.Diagnostics.Debug.WriteLine($"Failed to create a direct conversation: {e.Message}");
                        // Do nothing here as we fallback (continue without creating a direct conversation)
                    }

                    if (conversationResourceResponse != null && !string.IsNullOrEmpty(conversationResourceResponse.Id))
                    {
                        // The conversation account of the conversation owner for this 1:1 chat is different -
                        // thus, we need to re-create the conversation owner instance
                        ConversationAccount directConversationAccount =
                            new ConversationAccount(id: conversationResourceResponse.Id);

                        conversationOwnerConversationReference = new ConversationReference(
                            null, 
                            conversationOwnerConversationReference.User,
                            null, 
                            directConversationAccount,
                            conversationOwnerConversationReference.ChannelId,
                            conversationOwnerConversationReference.ServiceUrl);

                        RoutingDataManager.AddConversationReference(conversationOwnerConversationReference);
                        RoutingDataManager.AddConversationReference(new ConversationReference(
                            null,null,
                            botConversationReference.Bot,
                            directConversationAccount,
                            botConversationReference.ChannelId,
                            botConversationReference.ServiceUrl
                            ), false);

                        result.ConversationResourceResponse = conversationResourceResponse;
                    }
                }

                result = RoutingDataManager.ConnectAndRemoveConnectionRequest(conversationOwnerConversationReference, conversationClientConversationReference);
            }
            else
            {
                result.Type = MessageRouterResultType.Error;
                result.ErrorMessage = "Failed to find the bot instance";
            }

            return result;
        }

        /// <summary>
        /// Ends all 1:1 conversations of the given ConversationReference.
        /// </summary>
        /// <param name="connectedConversationReference">The ConversationReference connected in a conversation.</param>
        /// <returns>Same as Disconnect(ConversationReference, ConnectionProfile)</returns>
        public List<MessageRouterResult> Disconnect(ConversationReference connectedConversationReference)
        {
            return Disconnect(connectedConversationReference, ConnectionProfile.Any);
        }

        /// <summary>
        /// Ends the 1:1 conversation where the given ConversationReference is the conversation client (e.g. a customer).
        /// </summary>
        /// <param name="conversationClientConversationReference">The client of a connection (conversation).</param>
        /// <returns>Same as Disconnect(ConversationReference, ConnectionProfile)</returns>
        public MessageRouterResult DisconnectClient(ConversationReference conversationClientConversationReference)
        {
            // There can be only one result since a client cannot be connected in multiple conversations
            return Disconnect(conversationClientConversationReference, ConnectionProfile.Client)[0];
        }

        /// <summary>
        /// Ends all 1:1 conversations of the given conversation owner ConversationReference (e.g. a customer service agent).
        /// </summary>
        /// <param name="conversationOwnerConversationReference">The owner of a connection (conversation).</param>
        /// <returns>Same as Disconnect(ConversationReference, ConnectionProfile)</returns>
        public List<MessageRouterResult> DisconnectOwner(ConversationReference conversationOwnerConversationReference)
        {
            return Disconnect(conversationOwnerConversationReference, ConnectionProfile.Owner);
        }

        /// <summary>
        /// Routes the message in the given activity, if the sender is connected in a conversation.
        /// For instance, if it is a message from ConversationReference connected in a 1:1 chat, the message will
        /// be forwarded to the counterpart in whatever channel that ConversationReference is on.
        /// </summary>
        /// <param name="activity">The activity to handle.</param>
        /// <param name="addClientNameToMessage">If true, will add the client's name to the beginning of the message.</param>
        /// <param name="addOwnerNameToMessage">If true, will add the owner's (agent) name to the beginning of the message.</param>
        /// <returns>The result of the operation:
        /// - MessageRouterResultType.NoActionTaken, if no routing rule for the sender is found OR
        /// - MessageRouterResultType.OK, if the message was routed successfully OR
        /// - MessageRouterResultType.FailedToForwardMessage in case of an error (see the error message).
        /// </returns>
        public virtual async Task<MessageRouterResult> RouteMessageIfSenderIsConnectedAsync(
            IMessageActivity activity, bool addClientNameToMessage = true, bool addOwnerNameToMessage = false)
        {
            MessageRouterResult result = new MessageRouterResult()
            {
                Type = MessageRouterResultType.NoActionTaken,
                Activity = activity
            };

            ConversationReference senderConversationReference = MessageRoutingUtils.CreateSenderConversationReference(activity);

            if (RoutingDataManager.IsConnected(senderConversationReference, ConnectionProfile.Owner))
            {
                // Sender is an owner of an ongoing conversation - forward the message
                result.ConversationReference1 = senderConversationReference;
                ConversationReference ConversationReferenceToForwardMessageTo = RoutingDataManager.GetConnectedCounterpart(senderConversationReference);

                if (ConversationReferenceToForwardMessageTo != null)
                {
                    result.ConversationReference2 = ConversationReferenceToForwardMessageTo;
                    string message = addOwnerNameToMessage
                        ? $"{senderConversationReference.User.Name}: {activity.Text}" : activity.Text;
                    ResourceResponse resourceResponse =
                        await SendMessageAsync(ConversationReferenceToForwardMessageTo, activity.Text);

                    if (resourceResponse != null)
                    {
                        result.Type = MessageRouterResultType.OK;
                    }
                    else
                    {
                        result.Type = MessageRouterResultType.FailedToForwardMessage;
                        result.ErrorMessage = $"Failed to forward the message to user {ConversationReferenceToForwardMessageTo}";
                    }
                }
                else
                {
                    result.Type = MessageRouterResultType.FailedToForwardMessage;
                    result.ErrorMessage = "Failed to find the ConversationReference to forward the message to";
                }
            }
            else if (RoutingDataManager.IsConnected(senderConversationReference, ConnectionProfile.Client))
            {
                // Sender is a participant of an ongoing conversation - forward the message
                result.ConversationReference2 = senderConversationReference;
                ConversationReference ConversationReferenceToForwardMessageTo = RoutingDataManager.GetConnectedCounterpart(senderConversationReference);

                if (ConversationReferenceToForwardMessageTo != null)
                {
                    result.ConversationReference1 = ConversationReferenceToForwardMessageTo;
                    string message = addClientNameToMessage
                        ? $"{senderConversationReference.User.Name}: {activity.Text}" : activity.Text;
                    await SendMessageAsync(ConversationReferenceToForwardMessageTo, message);
                    result.Type = MessageRouterResultType.OK;
                }
                else
                {
                    result.Type = MessageRouterResultType.FailedToForwardMessage;
                    result.ErrorMessage = "Failed to find the ConversationReference to forward the message to";
                }
            }

            return result;
        }

        /// <summary>
        /// Sends a message activity to the conversation using the given bundle.
        /// </summary>
        /// <param name="bundle">The bundle containing the connector client and the message activity to send.</param>
        /// <returns>A valid ResourceResponse instance, if successful. Null in case of an error.</returns>
        protected virtual async Task<ResourceResponse> SendAsync(
            MessageRoutingUtils.ConnectorClientAndMessageBundle bundle)
        {
            ResourceResponse resourceResponse = null;

            try
            {
                resourceResponse =
                    await bundle.connectorClient.Conversations.SendToConversationAsync(
                        (Activity)bundle.messageActivity);
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

        /// <summary>
        /// Ends the conversation(s) of the given ConversationReference.
        /// </summary>
        /// <param name="connectedConversationReference">The ConversationReference connected in a conversation.</param>
        /// <param name="connectionProfile">The connection profile of the given ConversationReference.</param>
        /// <returns>A list of operation results:
        /// - MessageRouterResultType.NoActionTaken, if no connection to disconnect was found OR,
        /// - MessageRouterResultType.Disconnected for each disconnection, when successful.
        /// </returns>
        protected virtual List<MessageRouterResult> Disconnect(ConversationReference connectedConversationReference, ConnectionProfile connectionProfile)
        {
            List<MessageRouterResult> messageRouterResults = new List<MessageRouterResult>();

            ConversationReference ConversationReferenceInConversation = RoutingDataManager.FindConnectedConversationReferenceByChannel(
                connectedConversationReference.ChannelId, connectedConversationReference.User);

            if (ConversationReferenceInConversation != null
                && RoutingDataManager.IsConnected(ConversationReferenceInConversation, connectionProfile))
            {
                messageRouterResults.AddRange(
                    RoutingDataManager.Disconnect(ConversationReferenceInConversation, connectionProfile));
            }
            else
            {
                MessageRouterResult messageRouterResult = new MessageRouterResult()
                {
                    Type = MessageRouterResultType.Error,
                    ErrorMessage = "No connection to disconnect found"
                };

                if (connectionProfile == ConnectionProfile.Client)
                {
                    messageRouterResult.ConversationReference2 = connectedConversationReference;
                }
                else if (connectionProfile == ConnectionProfile.Owner)
                {
                    messageRouterResult.ConversationReference1 = connectedConversationReference;
                }

                messageRouterResults.Add(messageRouterResult);
            }

            return messageRouterResults;
        }
    }
}
