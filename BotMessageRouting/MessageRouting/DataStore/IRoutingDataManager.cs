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
        IList<Party> GetUserParties();

        /// <returns>The bot parties as a readonly list.</returns>
        IList<Party> GetBotParties();

        /// <summary>
        /// Adds the given party to the data.
        /// </summary>
        /// <param name="partyToAdd">The new party to add.</param>
        /// <param name="isUser">If true, will try to add the party to the list of users.
        /// If false, will try to add it to the list of bot identities. True by default.</param>
        /// <returns>True, if the given party was added. False otherwise (was null or already stored).</returns>
        bool AddParty(Party partyToAdd, bool isUser = true);

        /// <summary>
        /// Removes the party from all possible containers.
        /// Note that this method removes the party's all instances (user from all conversations
        /// in addition to pending requests).
        /// </summary>
        /// <param name="partyToRemove">The party to remove.</param>
        /// <returns>A list of operation result(s):
        /// - MessageRouterResultType.NoActionTaken, if the was not found in any collection OR
        /// - MessageRouterResultType.OK, if the party was removed from the collection AND
        /// - MessageRouterResultType.ConnectionRejected, if the party had a pending request AND
        /// - Disconnect() results, if the party was connected in a conversation.
        /// </returns>
        IList<MessageRouterResult> RemoveParty(Party partyToRemove);

        /// <returns>The aggregation parties as a readonly list.</returns>
        IList<Party> GetAggregationParties();

        /// <summary>
        /// Adds the given aggregation party.
        /// </summary>
        /// <param name="aggregationPartyToAdd">The party to be added as an aggregation party (channel).</param>
        /// <returns>True, if added. False otherwise (e.g. matching request already exists).</returns>
        bool AddAggregationParty(Party aggregationPartyToAdd);

        /// <summary>
        /// Removes the given aggregation party.
        /// </summary>
        /// <param name="aggregationPartyToRemove">The aggregation party to remove.</param>
        /// <returns>True, if removed successfully. False otherwise.</returns>
        bool RemoveAggregationParty(Party aggregationPartyToRemove);

        /// <returns>The (parties with) pending requests as a readonly list.</returns>
        IList<Party> GetPendingRequests();

        /// <summary>
        /// Adds the pending request for the given party.
        /// If the requestor party does not exist in the user party container, it will be added there.
        /// </summary>
        /// <param name="requestorParty">The party whose pending request to add.</param>
        /// <param name="rejectConnectionRequestIfNoAggregationChannel">If true, will reject all requests, if there is no aggregation channel.</param>
        /// <returns>The result of the operation:
        /// - MessageRouterResultType.ConnectionRequested, if a request was successfully made OR
        /// - MessageRouterResultType.ConnectionAlreadyRequested, if a request for the given party already exists OR
        /// - MessageRouterResultType.NoAgentsAvailable, if no aggregation while one is required OR
        /// - MessageRouterResultType.Error in case of an error (see the error message).
        /// </returns>
        MessageRouterResult AddPendingRequest(
            Party requestorParty, bool rejectConnectionRequestIfNoAggregationChannel = false);

        /// <summary>
        /// Removes the pending request of the given party.
        /// </summary>
        /// <param name="requestorParty">The party whose request to remove.</param>
        /// <returns>The result of the operation:
        /// - MessageRouterResultType.ConnectionRejected, if the pending request was removed OR
        /// - MessageRouterResultType.Error in case of an error (see the error message).
        /// </returns>
        MessageRouterResult RemovePendingRequest(Party requestorParty);

        /// <summary>
        /// Checks if the given party is connected in a 1:1 conversation as defined by
        /// the connection profile (e.g. as a customer, as an agent or either one).
        /// </summary>
        /// <param name="party">The party to check.</param>
        /// <param name="connectionProfile">Defines whether to look for clients, owners or both.</param>
        /// <returns>True, if the party is connected as defined by the given connection profile.
        /// False otherwise.</returns>
        bool IsConnected(Party party, ConnectionProfile connectionProfile);

        /// <returns>The connected parties, if any, where the key of the returned dictionary is the
        /// conversation owner and the value is the conversation client.</returns>
        Dictionary<Party, Party> GetConnectedParties();

        /// <summary>
        /// Resolves the given party's counterpart in a 1:1 conversation.
        /// </summary>
        /// <param name="partyWhoseCounterpartToFind">The party whose counterpart to resolve.</param>
        /// <returns>The counterpart or null, if not found.</returns>
        Party GetConnectedCounterpart(Party partyWhoseCounterpartToFind);

        /// <summary>
        /// Creates a new connection between the given parties. The method also clears the pending
        /// request of the client party, if one exists.
        /// </summary>
        /// <param name="conversationOwnerParty">The conversation owner party.</param>
        /// <param name="conversationClientParty">The conversation client (customer) party
        /// (i.e. one who requested the connection).</param>
        /// <returns>The result of the operation:
        /// - MessageRouterResultType.Connected, if successfully connected OR
        /// - MessageRouterResultType.Error in case of an error (see the error message).
        /// </returns>
        MessageRouterResult ConnectAndClearPendingRequest(Party conversationOwnerParty, Party conversationClientParty);

        /// <summary>
        /// Removes connection(s) of the given party i.e. ends the 1:1 conversations.
        /// </summary>
        /// <param name="party">The party whose connections to remove.</param>
        /// <param name="connectionProfile">The connection profile of the party (owner/client/either).</param>
        /// <returns>A list of operation results:
        /// - MessageRouterResultType.NoActionTaken, if no connection to disconnect was found OR,
        /// - MessageRouterResultType.Disconnected for each disconnection, when successful.
        /// </returns>
        IList<MessageRouterResult> Disconnect(Party party, ConnectionProfile connectionProfile);

        /// <summary>
        /// Deletes all existing routing data permanently.
        /// </summary>
        void DeleteAll();
        #endregion

        #region Utility methods
        /// <summary>
        /// Checks if the given party is associated with aggregation. In human toung this means
        /// that the given party is, for instance, a customer service agent who deals with the
        /// requests coming from customers.
        /// </summary>
        /// <param name="party">The party to check.</param>
        /// <returns>True, if is associated. False otherwise.</returns>
        bool IsAssociatedWithAggregation(Party party);

        /// <summary>
        /// Tries to resolve the name of the bot in the same conversation with the given party.
        /// </summary>
        /// <param name="party">The party from whose perspective to resolve the name.</param>
        /// <returns>The name of the bot or null, if unable to resolve.</returns>
        string ResolveBotNameInConversation(Party party);

        /// <summary>
        /// Tries to find the existing user party (stored earlier) matching the given one.
        /// </summary>
        /// <param name="partyToFind">The party to find.</param>
        /// <returns>The existing party matching the given one.</returns>
        Party FindExistingUserParty(Party partyToFind);

        /// <summary>
        /// Tries to find a stored party instance matching the given channel account ID and
        /// conversation ID.
        /// </summary>
        /// <param name="channelAccountId">The channel account ID (user ID).</param>
        /// <param name="conversationId">The conversation ID.</param>
        /// <returns>The party instance matching the given IDs or null if not found.</returns>
        Party FindPartyByChannelAccountIdAndConversationId(string channelAccountId, string conversationId);

        /// <summary>
        /// Tries to find a stored bot party instance matching the given channel ID and
        /// conversation account.
        /// </summary>
        /// <param name="channelId">The channel ID.</param>
        /// <param name="conversationAccount">The conversation account.</param>
        /// <returns>The bot party instance matching the given details or null if not found.</returns>
        Party FindBotPartyByChannelAndConversation(string channelId, ConversationAccount conversationAccount);

        /// <summary>
        /// Tries to find a party connected in a conversation.
        /// </summary>
        /// <param name="channelId">The channel ID.</param>
        /// <param name="channelAccount">The channel account.</param>
        /// <returns>The party matching the given details or null if not found.</returns>
        Party FindConnectedPartyByChannel(string channelId, ChannelAccount channelAccount);

        /// <summary>
        /// Finds the parties from the given list that match the channel account (and ID) of the given party.
        /// </summary>
        /// <param name="partyToFind">The party containing the channel details to match.</param>
        /// <param name="partyCandidates">The list of party candidates.</param>
        /// <returns>A newly created list of matching parties or null if none found.</returns>
        IList<Party> FindPartiesWithMatchingChannelAccount(Party partyToFind, IList<Party> partyCandidates);
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
