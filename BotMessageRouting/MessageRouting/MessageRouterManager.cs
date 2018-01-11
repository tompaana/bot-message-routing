using Microsoft.Bot.Connector;
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
    public class MessageRouterManager
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
        public MessageRouterManager(IRoutingDataManager routingDataManager)
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
        /// - MessageRouterResultType.ConnectionAlreadyRequested, if a request for the given party already exists OR
        /// - MessageRouterResultType.NoAgentsAvailable, if no aggregation channel exists while one is required OR
        /// - MessageRouterResultType.OK, if the activity was consumed and the message was routed successfully OR
        /// - MessageRouterResultType.FailedToForwardMessage in case a rule to route the message was in place, but the action failed (see the error message) OR
        /// - MessageRouterResultType.Error in case of an error (see the error message).
        /// </returns>    
        public virtual async Task<MessageRouterResult> HandleActivityAsync(
            Activity activity,
            bool tryToRequestConnectionIfNotConnected,
            bool rejectConnectionRequestIfNoAggregationChannel,
            bool addClientNameToMessage = true,
            bool addOwnerNameToMessage = false)
        {
            MakeSurePartiesAreTracked(activity);

            MessageRouterResult messageRouterResult =
                await RouteMessageIfSenderIsConnectedAsync(activity, addClientNameToMessage, addOwnerNameToMessage);

            if (tryToRequestConnectionIfNotConnected
                && messageRouterResult.Type == MessageRouterResultType.NoActionTaken)
            {
                messageRouterResult = RequestConnection(activity, rejectConnectionRequestIfNoAggregationChannel);
            }

            return messageRouterResult;
        }

        /// <summary>
        /// Tries to send the given message activity to the given party using this bot on the same
        /// channel as the party who the message is sent to.
        /// </summary>
        /// <param name="partyToMessage">The party to send the message to.</param>
        /// <param name="messageActivity">The message activity to send (message content).</param>
        /// <returns>The ResourceResponse instance or null in case of an error.</returns>
        public async Task<ResourceResponse> SendMessageToPartyByBotAsync(
            Party partyToMessage, IMessageActivity messageActivity)
        {
            Party botParty = null;

            if (partyToMessage != null)
            {
                // We need the channel account of the bot in the SAME CHANNEL as the RECIPIENT.
                // The identity of the bot in the channel of the sender is most likely a different one and
                // thus unusable since it will not be recognized on the recipient's channel.
                botParty = RoutingDataManager.FindBotPartyByChannelAndConversation(
                    partyToMessage.ChannelId, partyToMessage.ConversationAccount);
            }

            if (botParty != null)
            {
                messageActivity.From = botParty.ChannelAccount;

                MessagingUtils.ConnectorClientAndMessageBundle bundle =
                    MessagingUtils.CreateConnectorClientAndMessageActivity(
                        partyToMessage.ServiceUrl, messageActivity);

                return await bundle.connectorClient.Conversations.SendToConversationAsync(
                    (Activity)bundle.messageActivity);
            }

            return null;
        }

        /// <summary>
        /// Tries to send the given message to the given party using this bot on the same channel
        /// as the party who the message is sent to.
        /// </summary>
        /// <param name="partyToMessage">The party to send the message to.</param>
        /// <param name="messageText">The message content.</param>
        /// <returns>The ResourceResponse instance or null in case of an error.</returns>
        public async Task<ResourceResponse> SendMessageToPartyByBotAsync(Party partyToMessage, string messageText)
        {
            Party botParty = null;

            if (partyToMessage != null)
            {
                botParty = RoutingDataManager.FindBotPartyByChannelAndConversation(
                    partyToMessage.ChannelId, partyToMessage.ConversationAccount);
            }

            if (botParty != null)
            {
                MessagingUtils.ConnectorClientAndMessageBundle bundle =
                    MessagingUtils.CreateConnectorClientAndMessageActivity(
                        partyToMessage, messageText, botParty?.ChannelAccount);

                return await bundle.connectorClient.Conversations.SendToConversationAsync(
                    (Activity)bundle.messageActivity);
            }

            return null;
        }

        /// <summary>
        /// Sends the given message activity to all the aggregation channels, if any exist.
        /// </summary>
        /// <param name="messageActivity">The message activity to broadcast.</param>
        /// <returns></returns>
        public async Task BroadcastMessageToAggregationChannelsAsync(IMessageActivity messageActivity)
        {
            foreach (Party aggregationChannel in RoutingDataManager.GetAggregationParties())
            {
                await SendMessageToPartyByBotAsync(aggregationChannel, messageActivity);
            }
        }

        /// <summary>
        /// Sends the given message to all the aggregation channels, if any exist.
        /// </summary>
        /// <param name="messageText">The message to broadcast.</param>
        /// <returns></returns>
        public async Task BroadcastMessageToAggregationChannelsAsync(string messageText)
        {
            foreach (Party aggregationChannel in RoutingDataManager.GetAggregationParties())
            {
                await SendMessageToPartyByBotAsync(aggregationChannel, messageText);
            }
        }

        /// <summary>
        /// Checks the given parties and adds them to the collection, if not already there.
        /// 
        /// Note that this method expects that the recipient is the bot. The sender could also be
        /// the bot, but that case is checked before adding the sender to the container.
        /// </summary>
        /// <param name="senderParty">The sender party (from).</param>
        /// <param name="recipientParty">The recipient party.</param>
        public void MakeSurePartiesAreTracked(Party senderParty, Party recipientParty)
        {
            // Store the bot identity, if not already stored
            RoutingDataManager.AddParty(recipientParty, false);

            // Check that the party who sent the message is not the bot
            if (!RoutingDataManager.GetBotParties().Contains(senderParty))
            {
                // Store the user party, if not already stored
                RoutingDataManager.AddParty(senderParty);
            }
        }

        /// <summary>
        /// Checks the given activity for new parties and adds them to the collection,
        /// if not already there.
        /// </summary>
        /// <param name="activity">The activity.</param>
        public void MakeSurePartiesAreTracked(IActivity activity)
        {
            MakeSurePartiesAreTracked(
                MessagingUtils.CreateSenderParty(activity),
                MessagingUtils.CreateRecipientParty(activity));
        }

        /// <summary>
        /// Removes the given party from the routing data.
        /// For convenience.
        /// </summary>
        /// <param name="partyToRemove">The party to remove.</param>
        /// <returns>A list of operation result(s):
        /// - MessageRouterResultType.NoActionTaken, if the was not found in any collection OR
        /// - MessageRouterResultType.OK, if the party was removed from the collection AND
        /// - MessageRouterResultType.ConnectionRejected, if the party had a pending request AND
        /// - Disconnect() results, if the party was connected in a conversation.</returns>
        public IList<MessageRouterResult> RemoveParty(Party partyToRemove)
        {
            return RoutingDataManager.RemoveParty(partyToRemove);
        }

        /// <summary>
        /// Tries to initiate a connection (1:1 conversation) by creating a request on behalf of
        /// the given party. This method does nothing, if a request for the same user already exists.
        /// </summary>
        /// <param name="requestorParty">The requestor party.</param>
        /// <param name="rejectConnectionRequestIfNoAggregationChannel">If true, will reject all requests, if there is no aggregation channel.</param>
        /// <returns>The result of the operation:
        /// - MessageRouterResultType.ConnectionRequested, if a request was successfully made OR
        /// - MessageRouterResultType.ConnectionAlreadyRequested, if a request for the given party already exists OR
        /// - MessageRouterResultType.NoAgentsAvailable, if no aggregation channel exists while one is required OR
        /// - MessageRouterResultType.Error in case of an error (see the error message).
        /// </returns>
        public MessageRouterResult RequestConnection(
            Party requestorParty, bool rejectConnectionRequestIfNoAggregationChannel = false)
        {
            return RoutingDataManager.AddPendingRequest(requestorParty, rejectConnectionRequestIfNoAggregationChannel);
        }

        /// <summary>
        /// Tries to initiate a connection (1:1 conversation) by creating a request on behalf of
        /// the SENDER in the given activity. This method does nothing, if a request for the same
        /// user already exists.
        /// </summary>
        /// <param name="activity">The activity. The SENDER in this activity (From property) is considered the requestor.</param>
        /// <param name="rejectConnectionRequestIfNoAggregationChannel">If true, will reject all requests, if there is no aggregation channel.</param>
        /// <returns>Same as RequestConnection(Party, bool)</returns>
        public virtual MessageRouterResult RequestConnection(
            Activity activity, bool rejectConnectionRequestIfNoAggregationChannel = false)
        {
            MessageRouterResult messageRouterResult =
                RequestConnection(MessagingUtils.CreateSenderParty(activity), rejectConnectionRequestIfNoAggregationChannel);
            messageRouterResult.Activity = activity;
            return messageRouterResult;
        }

        /// <summary>
        /// Tries to reject the pending connection request of the given party.
        /// </summary>
        /// <param name="partyToReject">The party whose request to reject.</param>
        /// <param name="rejecterParty">The party rejecting the request (optional).</param>
        /// <returns>The result of the operation:
        /// - MessageRouterResultType.ConnectionRejected, if the pending request was removed OR
        /// - MessageRouterResultType.Error in case of an error (see the error message).
        /// </returns>
        public virtual MessageRouterResult RejectPendingRequest(Party partyToReject, Party rejecterParty = null)
        {
            if (partyToReject == null)
            {
                throw new ArgumentNullException($"The party to reject ({nameof(partyToReject)} cannot be null");
            }

            MessageRouterResult messageRouteResult = RoutingDataManager.RemovePendingRequest(partyToReject);
            messageRouteResult.ConversationClientParty = partyToReject;
            messageRouteResult.ConversationOwnerParty = rejecterParty;

            if (messageRouteResult.Type == MessageRouterResultType.Error)
            {
                messageRouteResult.ErrorMessage =
                    $"Failed to remove the pending request of user \"{partyToReject.ChannelAccount?.Name}\": {messageRouteResult.ErrorMessage}";
            }

            return messageRouteResult;
        }

        /// <summary>
        /// Tries to establish 1:1 chat between the two given parties.
        /// 
        /// Note that the conversation owner will have a new separate party in the created
        /// conversation, if a new direct conversation is created.
        /// </summary>
        /// <param name="conversationOwnerParty">The party who owns the conversation (e.g. customer service agent).</param>
        /// <param name="conversationClientParty">The other party in the conversation.</param>
        /// <param name="createNewDirectConversation">If true, will try to create a new direct conversation between
        /// the bot and the conversation owner (e.g. agent) where the messages from the other (client) party are routed.
        /// Note that this will result in the conversation owner having a new separate party in the created connection
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
            Party conversationOwnerParty, Party conversationClientParty, bool createNewDirectConversation)
        {
            if (conversationOwnerParty == null || conversationClientParty == null)
            {
                throw new ArgumentNullException(
                    $"Neither of the arguments ({nameof(conversationOwnerParty)}, {nameof(conversationClientParty)}) can be null");
            }

            MessageRouterResult result = new MessageRouterResult()
            {
                ConversationOwnerParty = conversationOwnerParty,
                ConversationClientParty = conversationClientParty
            };

            Party botParty = RoutingDataManager.FindBotPartyByChannelAndConversation(
                conversationOwnerParty.ChannelId, conversationOwnerParty.ConversationAccount);

            if (botParty != null)
            {
                if (createNewDirectConversation)
                {
                    ConnectorClient connectorClient = new ConnectorClient(new Uri(conversationOwnerParty.ServiceUrl));
                    ConversationResourceResponse conversationResourceResponse = null;

                    try
                    {
                        conversationResourceResponse =
                            await connectorClient.Conversations.CreateDirectConversationAsync(
                                botParty.ChannelAccount, conversationOwnerParty.ChannelAccount);
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

                        conversationOwnerParty = new Party(
                            conversationOwnerParty.ServiceUrl, conversationOwnerParty.ChannelId,
                            conversationOwnerParty.ChannelAccount, directConversationAccount);

                        RoutingDataManager.AddParty(conversationOwnerParty);
                        RoutingDataManager.AddParty(new Party(
                            botParty.ServiceUrl, botParty.ChannelId, botParty.ChannelAccount, directConversationAccount), false);

                        result.ConversationResourceResponse = conversationResourceResponse;
                    }
                }

                result = RoutingDataManager.ConnectAndClearPendingRequest(conversationOwnerParty, conversationClientParty);
            }
            else
            {
                result.Type = MessageRouterResultType.Error;
                result.ErrorMessage = "Failed to find the bot instance";
            }

            return result;
        }

        /// <summary>
        /// Ends all 1:1 conversations of the given party.
        /// </summary>
        /// <param name="connectedParty">The party connected in a conversation.</param>
        /// <returns>Same as Disconnect(Party, ConnectionProfile)</returns>
        public List<MessageRouterResult> Disconnect(Party connectedParty)
        {
            return Disconnect(connectedParty, ConnectionProfile.Any);
        }

        /// <summary>
        /// Ends the 1:1 conversation where the given party is the conversation client (e.g. a customer).
        /// </summary>
        /// <param name="conversationClientParty">The client of a connection (conversation).</param>
        /// <returns>Same as Disconnect(Party, ConnectionProfile)</returns>
        public MessageRouterResult DisconnectClient(Party conversationClientParty)
        {
            // There can be only one result since a client cannot be connected in multiple conversations
            return Disconnect(conversationClientParty, ConnectionProfile.Client)[0];
        }

        /// <summary>
        /// Ends all 1:1 conversations of the given conversation owner party (e.g. a customer service agent).
        /// </summary>
        /// <param name="conversationOwnerParty">The owner of a connection (conversation).</param>
        /// <returns>Same as Disconnect(Party, ConnectionProfile)</returns>
        public List<MessageRouterResult> DisconnectOwner(Party conversationOwnerParty)
        {
            return Disconnect(conversationOwnerParty, ConnectionProfile.Owner);
        }

        /// <summary>
        /// Routes the message in the given activity, if the sender is connected in a conversation.
        /// For instance, if it is a message from party connected in a 1:1 chat, the message will
        /// be forwarded to the counterpart in whatever channel that party is on.
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
            Activity activity, bool addClientNameToMessage = true, bool addOwnerNameToMessage = false)
        {
            MessageRouterResult result = new MessageRouterResult()
            {
                Type = MessageRouterResultType.NoActionTaken,
                Activity = activity
            };

            Party senderParty = MessagingUtils.CreateSenderParty(activity);

            if (RoutingDataManager.IsConnected(senderParty, ConnectionProfile.Owner))
            {
                // Sender is an owner of an ongoing conversation - forward the message
                result.ConversationOwnerParty = senderParty;
                Party partyToForwardMessageTo = RoutingDataManager.GetConnectedCounterpart(senderParty);

                if (partyToForwardMessageTo != null)
                {
                    result.ConversationClientParty = partyToForwardMessageTo;
                    string message = addOwnerNameToMessage
                        ? $"{senderParty.ChannelAccount.Name}: {activity.Text}" : activity.Text;
                    ResourceResponse resourceResponse =
                        await SendMessageToPartyByBotAsync(partyToForwardMessageTo, activity.Text);

                    if (resourceResponse != null)
                    {
                        result.Type = MessageRouterResultType.OK;
                    }
                    else
                    {
                        result.Type = MessageRouterResultType.FailedToForwardMessage;
                        result.ErrorMessage = $"Failed to forward the message to user {partyToForwardMessageTo}";
                    }
                }
                else
                {
                    result.Type = MessageRouterResultType.FailedToForwardMessage;
                    result.ErrorMessage = "Failed to find the party to forward the message to";
                }
            }
            else if (RoutingDataManager.IsConnected(senderParty, ConnectionProfile.Client))
            {
                // Sender is a participant of an ongoing conversation - forward the message
                result.ConversationClientParty = senderParty;
                Party partyToForwardMessageTo = RoutingDataManager.GetConnectedCounterpart(senderParty);

                if (partyToForwardMessageTo != null)
                {
                    result.ConversationOwnerParty = partyToForwardMessageTo;
                    string message = addClientNameToMessage
                        ? $"{senderParty.ChannelAccount.Name}: {activity.Text}" : activity.Text;
                    await SendMessageToPartyByBotAsync(partyToForwardMessageTo, message);
                    result.Type = MessageRouterResultType.OK;
                }
                else
                {
                    result.Type = MessageRouterResultType.FailedToForwardMessage;
                    result.ErrorMessage = "Failed to find the party to forward the message to";
                }
            }

            return result;
        }

        /// <summary>
        /// Ends the conversation(s) of the given party.
        /// </summary>
        /// <param name="connectedParty">The party connected in a conversation.</param>
        /// <param name="connectionProfile">The connection profile of the given party.</param>
        /// <returns>A list of operation results:
        /// - MessageRouterResultType.NoActionTaken, if no connection to disconnect was found OR,
        /// - MessageRouterResultType.Disconnected for each disconnection, when successful.
        /// </returns>
        protected virtual List<MessageRouterResult> Disconnect(Party connectedParty, ConnectionProfile connectionProfile)
        {
            List<MessageRouterResult> messageRouterResults = new List<MessageRouterResult>();

            Party partyInConversation = RoutingDataManager.FindConnectedPartyByChannel(
                connectedParty.ChannelId, connectedParty.ChannelAccount);

            if (partyInConversation != null
                && RoutingDataManager.IsConnected(partyInConversation, connectionProfile))
            {
                messageRouterResults.AddRange(
                    RoutingDataManager.Disconnect(partyInConversation, connectionProfile));
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
                    messageRouterResult.ConversationClientParty = connectedParty;
                }
                else if (connectionProfile == ConnectionProfile.Owner)
                {
                    messageRouterResult.ConversationOwnerParty = connectedParty;
                }

                messageRouterResults.Add(messageRouterResult);
            }

            return messageRouterResults;
        }
    }
}
