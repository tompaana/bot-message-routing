using Microsoft.Bot.Schema;
using System.Collections.Generic;
using Underscore.Bot.Models;

namespace Underscore.Bot.MessageRouting.DataStore
{
    /// <summary>
    /// Defines the type of the connection:
    /// - None: No connection
    /// - Client: E.g. a customer
    /// - Owner: E.g. a customer service agent
    /// - Any: Either a client or an owner
    /// </summary>
    public enum ConnectionProfile
    {
        None,
        Client,
        Owner,
        Any
    };

    /// <summary>
    /// Interface for routing data managers.
    /// </summary>
    public interface IRoutingDataManager
    {
        #region CRUD methods
        /// <returns>The user parties as a readonly list.</returns>
        IList<ConversationReference> GetUserParties();

        /// <returns>The bot parties as a readonly list.</returns>
        IList<ConversationReference> GetBotParties();

        /// <summary>
        /// Adds the given ConversationReference to the data.
        /// </summary>
        /// <param name="ConversationReferenceToAdd">The new ConversationReference to add.</param>
        /// <param name="isUser">If true, will try to add the ConversationReference to the list of users.
        /// If false, will try to add it to the list of bot identities. True by default.</param>
        /// <returns>True, if the given ConversationReference was added. False otherwise (was null or already stored).</returns>
        bool AddConversationReference(ConversationReference ConversationReferenceToAdd, bool isUser = true);

        /// <summary>
        /// Removes the ConversationReference from all possible containers.
        /// Note that this method removes the ConversationReference's all instances (user from all conversations
        /// in addition to pending requests).
        /// </summary>
        /// <param name="ConversationReferenceToRemove">The ConversationReference to remove.</param>
        /// <returns>A list of operation result(s):
        /// - MessageRouterResultType.NoActionTaken, if the was not found in any collection OR
        /// - MessageRouterResultType.OK, if the ConversationReference was removed from the collection AND
        /// - MessageRouterResultType.ConnectionRejected, if the ConversationReference had a pending request AND
        /// - Disconnect() results, if the ConversationReference was connected in a conversation.
        /// </returns>
        IList<MessageRouterResult> RemoveConversationReference(ConversationReference ConversationReferenceToRemove);

        /// <returns>The aggregation parties as a readonly list.</returns>
        IList<ConversationReference> GetAggregationParties();

        /// <summary>
        /// Adds the given aggregation ConversationReference.
        /// </summary>
        /// <param name="aggregationConversationReferenceToAdd">The ConversationReference to be added as an aggregation ConversationReference (channel).</param>
        /// <returns>True, if added. False otherwise (e.g. matching request already exists).</returns>
        bool AddAggregationConversationReference(ConversationReference aggregationConversationReferenceToAdd);

        /// <summary>
        /// Removes the given aggregation ConversationReference.
        /// </summary>
        /// <param name="aggregationConversationReferenceToRemove">The aggregation ConversationReference to remove.</param>
        /// <returns>True, if removed successfully. False otherwise.</returns>
        bool RemoveAggregationConversationReference(ConversationReference aggregationConversationReferenceToRemove);

        /// <returns>The (parties with) pending requests as a readonly list.</returns>
        IList<ConversationReference> GetPendingRequests();

        /// <summary>
        /// Adds the pending request for the given ConversationReference.
        /// If the requestor ConversationReference does not exist in the user ConversationReference container, it will be added there.
        /// </summary>
        /// <param name="requestorConversationReference">The ConversationReference whose pending request to add.</param>
        /// <param name="rejectConnectionRequestIfNoAggregationChannel">If true, will reject all requests, if there is no aggregation channel.</param>
        /// <returns>The result of the operation:
        /// - MessageRouterResultType.ConnectionRequested, if a request was successfully made OR
        /// - MessageRouterResultType.ConnectionAlreadyRequested, if a request for the given ConversationReference already exists OR
        /// - MessageRouterResultType.NoAgentsAvailable, if no aggregation while one is required OR
        /// - MessageRouterResultType.Error in case of an error (see the error message).
        /// </returns>
        MessageRouterResult AddPendingRequest(
            ConversationReference requestorConversationReference, bool rejectConnectionRequestIfNoAggregationChannel = false);

        /// <summary>
        /// Removes the pending request of the given ConversationReference.
        /// </summary>
        /// <param name="requestorConversationReference">The ConversationReference whose request to remove.</param>
        /// <returns>The result of the operation:
        /// - MessageRouterResultType.ConnectionRejected, if the pending request was removed OR
        /// - MessageRouterResultType.Error in case of an error (see the error message).
        /// </returns>
        MessageRouterResult RemovePendingRequest(ConversationReference requestorConversationReference);

        /// <summary>
        /// Checks if the given ConversationReference is connected in a 1:1 conversation as defined by
        /// the connection profile (e.g. as a customer, as an agent or either one).
        /// </summary>
        /// <param name="ConversationReference">The ConversationReference to check.</param>
        /// <param name="connectionProfile">Defines whether to look for clients, owners or both.</param>
        /// <returns>True, if the ConversationReference is connected as defined by the given connection profile.
        /// False otherwise.</returns>
        bool IsConnected(ConversationReference ConversationReference, ConnectionProfile connectionProfile);

        /// <returns>The connected parties, if any, where the key of the returned dictionary is the
        /// conversation owner and the value is the conversation client.</returns>
        Dictionary<ConversationReference, ConversationReference> GetConnectedParties();

        /// <summary>
        /// Resolves the given ConversationReference's counterpart in a 1:1 conversation.
        /// </summary>
        /// <param name="ConversationReferenceWhoseCounterpartToFind">The ConversationReference whose counterpart to resolve.</param>
        /// <returns>The counterpart or null, if not found.</returns>
        ConversationReference GetConnectedCounterpart(ConversationReference ConversationReferenceWhoseCounterpartToFind);

        /// <summary>
        /// Creates a new connection between the given parties. The method also clears the pending
        /// request of the client ConversationReference, if one exists.
        /// </summary>
        /// <param name="conversationOwnerConversationReference">The conversation owner ConversationReference.</param>
        /// <param name="conversationClientConversationReference">The conversation client (customer) ConversationReference
        /// (i.e. one who requested the connection).</param>
        /// <returns>The result of the operation:
        /// - MessageRouterResultType.Connected, if successfully connected OR
        /// - MessageRouterResultType.Error in case of an error (see the error message).
        /// </returns>
        MessageRouterResult ConnectAndClearPendingRequest(ConversationReference conversationOwnerConversationReference, ConversationReference conversationClientConversationReference);

        /// <summary>
        /// Removes connection(s) of the given ConversationReference i.e. ends the 1:1 conversations.
        /// </summary>
        /// <param name="ConversationReference">The ConversationReference whose connections to remove.</param>
        /// <param name="connectionProfile">The connection profile of the ConversationReference (owner/client/either).</param>
        /// <returns>A list of operation results:
        /// - MessageRouterResultType.NoActionTaken, if no connection to disconnect was found OR,
        /// - MessageRouterResultType.Disconnected for each disconnection, when successful.
        /// </returns>
        IList<MessageRouterResult> Disconnect(ConversationReference ConversationReference, ConnectionProfile connectionProfile);

        /// <summary>
        /// Deletes all existing routing data permanently.
        /// </summary>
        void DeleteAll();
        #endregion

        #region Utility methods
        /// <summary>
        /// Checks if the given ConversationReference is associated with aggregation. In human toung this means
        /// that the given ConversationReference is, for instance, a customer service agent who deals with the
        /// requests coming from customers.
        /// </summary>
        /// <param name="ConversationReference">The ConversationReference to check.</param>
        /// <returns>True, if is associated. False otherwise.</returns>
        bool IsAssociatedWithAggregation(ConversationReference ConversationReference);

        /// <summary>
        /// Tries to resolve the name of the bot in the same conversation with the given ConversationReference.
        /// </summary>
        /// <param name="ConversationReference">The ConversationReference from whose perspective to resolve the name.</param>
        /// <returns>The name of the bot or null, if unable to resolve.</returns>
        string ResolveBotNameInConversation(ConversationReference ConversationReference);

        /// <summary>
        /// Tries to find the existing user ConversationReference (stored earlier) matching the given one.
        /// </summary>
        /// <param name="ConversationReferenceToFind">The ConversationReference to find.</param>
        /// <returns>The existing ConversationReference matching the given one.</returns>
        ConversationReference FindExistingUserConversationReference(ConversationReference ConversationReferenceToFind);

        /// <summary>
        /// Tries to find a stored ConversationReference instance matching the given channel account ID and
        /// conversation ID.
        /// </summary>
        /// <param name="channelAccountId">The channel account ID (user ID).</param>
        /// <param name="conversationId">The conversation ID.</param>
        /// <returns>The ConversationReference instance matching the given IDs or null if not found.</returns>
        ConversationReference FindConversationReferenceByChannelAccountIdAndConversationId(string channelAccountId, string conversationId);

        /// <summary>
        /// Tries to find a stored bot ConversationReference instance matching the given channel ID and
        /// conversation account.
        /// </summary>
        /// <param name="channelId">The channel ID.</param>
        /// <param name="conversationAccount">The conversation account.</param>
        /// <returns>The bot ConversationReference instance matching the given details or null if not found.</returns>
        ConversationReference FindBotConversationReferenceByChannelAndConversation(string channelId, ConversationAccount conversationAccount);

        /// <summary>
        /// Tries to find a ConversationReference connected in a conversation.
        /// </summary>
        /// <param name="channelId">The channel ID.</param>
        /// <param name="channelAccount">The channel account.</param>
        /// <returns>The ConversationReference matching the given details or null if not found.</returns>
        ConversationReference FindConnectedConversationReferenceByChannel(string channelId, ChannelAccount channelAccount);

        /// <summary>
        /// Finds the parties from the given list that match the channel account (and ID) of the given ConversationReference.
        /// </summary>
        /// <param name="ConversationReferenceToFind">The ConversationReference containing the channel details to match.</param>
        /// <param name="ConversationReferenceCandidates">The list of ConversationReference candidates.</param>
        /// <returns>A newly created list of matching parties or null if none found.</returns>
        IList<ConversationReference> FindPartiesWithMatchingChannelAccount(ConversationReference ConversationReferenceToFind, IList<ConversationReference> ConversationReferenceCandidates);
        #endregion

        #region Methods for debugging
#if DEBUG
        /// <returns>The connections (parties in conversation) as a string.
        /// Will return an empty string, if no connections exist.</returns>
        string ConnectionsToString();

        string GetLastMessageRouterResults();

        void AddMessageRouterResult(MessageRouterResult result);

        void ClearMessageRouterResults();
#endif
        #endregion
    }
}
