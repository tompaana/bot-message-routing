using System;
using System.Collections.Generic;
using Microsoft.Bot.Connector;
using Microsoft.WindowsAzure.Storage;
using Microsoft.Azure;
using Underscore.Bot.MessageRouting.DataStore;
using Underscore.Bot.MessageRouting;
using Underscore.Bot.Models;
using Underscore.Bot.Utils;
using Microsoft.WindowsAzure.Storage.Table;
using System.Configuration;
using System.Linq;
using Newtonsoft.Json;

namespace Underscore.Bot.MessageRouting.DataStore
{
    public class AzureTableStorageRoutingDataManager : IRoutingDataManager
    {
        private CloudTable table;
        private CloudTable tableConversation;
        private List<MessageRouterResult> LastMessageRouterResults;

        /// <summary>
        /// Constructor.
        /// </summary>
        public AzureTableStorageRoutingDataManager()
        {
            table = StorageHelper.CreateStorageAccountFromConnectionString("Party");
            tableConversation = StorageHelper.CreateStorageAccountFromConnectionString("PartyConversation");
#if DEBUG
            LastMessageRouterResults = new List<MessageRouterResult>();
#endif
        }

        public bool AddAggregationParty(Party party)
        {
            if (party != null)
            {
                if (party.ChannelAccount != null)
                {
                    throw new ArgumentException("Parte agregada não pode conter conta de canal.");
                }

                if (!GetAggregationParties().Contains(party))
                {
                    try
                    {
                        StorageHelper.Insert<PartyEntity>(table, new PartyEntity(party, PartyType.Aggregation));
                    }
                    catch (Exception ex)
                    {
                        throw;
                    }
                    return true;
                }
            }

            return false;
        }

        public MessageRouterResult ConnectAndClearPendingRequest(Party conversationOwnerParty, Party conversationClientParty)
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
                    StorageHelper.Insert<Conversation>(tableConversation, new Conversation()
                    {
                        PartitionKey = conversationClientParty.ConversationAccount.Id,
                        RowKey = conversationOwnerParty.ConversationAccount.Id,
                        Client = JsonConvert.SerializeObject(new PartyEntity(conversationClientParty, PartyType.Client)),
                        Owner = JsonConvert.SerializeObject(new PartyEntity(conversationOwnerParty, PartyType.Owner))
                    });
                    RemovePendingRequest(conversationClientParty);

                    DateTime connectionStartedTime = DateTime.UtcNow;

                    if (conversationClientParty is PartyWithTimestamps)
                    {
                        (conversationClientParty as PartyWithTimestamps).ResetConnectionRequestTime();
                        (conversationClientParty as PartyWithTimestamps).ConnectionEstablishedTime = connectionStartedTime;
                    }

                    if (conversationOwnerParty is PartyWithTimestamps)
                    {
                        (conversationOwnerParty as PartyWithTimestamps).ConnectionEstablishedTime = connectionStartedTime;
                    }

                    result.Type = MessageRouterResultType.Connected;
                }
                catch (ArgumentException e)
                {
                    result.Type = MessageRouterResultType.Error;
                    result.ErrorMessage = e.Message;
                    System.Diagnostics.Debug.WriteLine($"Falha ao conectar as partes {conversationOwnerParty} e {conversationClientParty}: {e.Message}");
                }
            }
            else
            {
                result.Type = MessageRouterResultType.Error;
                result.ErrorMessage = "Cliente ou proprietário está faltando.";
            }

            return result;
        }

        public bool AddParty(Party newParty, bool isUser = true)
        {

            if (newParty == null || (isUser ? GetUserParties().Contains(newParty) : GetBotParties().Contains(newParty)))
            {
                return false;
            }

            if (isUser)
            {
                //INSERINDO USER
                StorageHelper.Insert<PartyEntity>(table, new PartyEntity(newParty, PartyType.User));
            }
            else
            {
                if (newParty.ChannelAccount == null)
                {
                    throw new NullReferenceException($"Conta do canal do bot ({nameof(newParty.ChannelAccount)}) não pode ser nula");
                }
                //INSERINDO BOT
                StorageHelper.Insert<PartyEntity>(table, new PartyEntity(newParty, PartyType.Bot));
            }

            return true;
        }

        public bool AddParty(string serviceUrl, string channelId, ChannelAccount channelAccount, ConversationAccount conversationAccount, bool isUser = true)
        {
            Party p = new Party(serviceUrl, channelId, channelAccount, conversationAccount);
            return AddParty(p, isUser);
        }

        public MessageRouterResult AddPendingRequest(Party party)
        {
            MessageRouterResult result = new MessageRouterResult()
            {
                ConversationClientParty = party
            };

            if (party != null)
            {
                if (GetPendingRequests().Contains(party))
                {
                    result.Type = MessageRouterResultType.ConnectionAlreadyRequested;
                }
                else
                {

                    if (!GetAggregationParties().Any()
                        && Convert.ToBoolean(ConfigurationManager.AppSettings[MessageRouterManager.RejectPendingRequestIfNoAggregationChannelAppSetting]))
                    {
                        result.Type = MessageRouterResultType.NoAgentsAvailable;
                    }
                    else
                    {
                        if (party is PartyWithTimestamps)
                        {
                            (party as PartyWithTimestamps).ConnectionRequestTime = DateTime.UtcNow;
                        }

                        StorageHelper.Insert<PartyEntity>(table, new PartyEntity(party, PartyType.PendingRequest));
                        result.Type = MessageRouterResultType.ConnectionRequested;
                    }
                }
            }
            else
            {
                result.Type = MessageRouterResultType.Error;
                result.ErrorMessage = "Parte nula";
            }

            return result;
        }

        public void DeleteAll()
        {
            LastMessageRouterResults.Clear();
            var result = table.ExecuteQuery(new TableQuery<PartyEntity>());
            foreach (var item in result)
            {
                StorageHelper.DeleteEntry<PartyEntity>(table, item.PartitionKey, item.RowKey);
            }

            var otherResult = tableConversation.ExecuteQuery(new TableQuery<Conversation>());
            foreach (var item in result)
            {
                StorageHelper.DeleteEntry<Conversation>(table, item.PartitionKey, item.RowKey);
            }
        }

        public string ConnectionsToString()
        {
            //DEBUG
            string parties = string.Empty;

            foreach (KeyValuePair<Party, Party> keyValuePair in GetConnectedParties())
            {
                parties += $"{keyValuePair.Key} -> {keyValuePair.Value}\n\r";
            }

            return parties;
        }

        public Party FindBotPartyByChannelAndConversation(string channelId, ConversationAccount conversationAccount)
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

        public Party FindConnectedPartyByChannel(string channelId, ChannelAccount channelAccount)
        {
            //Party foundParty = null;

            //try
            //{
            //    var result = GetWhereWith2("ChannelId", channelId, "ChannelAccountID", channelAccount.Id);
            //    foundParty = new Party(result.ServiceUrl, result.ChannelId, result.ChannelAccount, result.ConversationAccount);
            //}
            //catch (InvalidOperationException)
            //{
            //}

            //return foundParty;

            Party foundParty = null;

            try
            {
                foundParty = GetConnectedParties().Keys.Single(party =>
                        (party.ChannelId.Equals(channelId)
                         && party.ChannelAccount != null
                         && party.ChannelAccount.Id.Equals(channelAccount.Id)));

                if (foundParty == null)
                {
                    // Not found in keys, try the values
                    foundParty = GetConnectedParties().Values.First(party =>
                            (party.ChannelId.Equals(channelId)
                             && party.ChannelAccount != null
                             && party.ChannelAccount.Id.Equals(channelAccount.Id)));
                }
            }
            catch (InvalidOperationException)
            {
            }

            return foundParty;


        }

        public Party FindExistingUserParty(Party partyToFind)
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

        public IList<Party> FindPartiesWithMatchingChannelAccount(Party partyToFind, IList<Party> parties)
        {
            IList<Party> matchingParties = null;
            IEnumerable<Party> foundParties = null;

            try
            {
                foundParties = GetUserParties().Where(party => party.HasMatchingChannelInformation(partyToFind));
            }
            catch (ArgumentNullException)
            {
            }
            catch (InvalidOperationException)
            {
            }

            if (foundParties != null)
            {
                matchingParties = foundParties.ToArray();
            }

            return matchingParties;
        }

        public Party FindPartyByChannelAccountIdAndConversationId(string channelAccountId, string conversationId)
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

        public Party GetConnectedCounterpart(Party partyWhoseCounterpartToFind)
        {
            Party counterparty = null;

            var ConnectedParties = GetConnectedParties();

            if (IsConnected(partyWhoseCounterpartToFind, ConnectionProfile.Client))
            {
                for (int i = 0; i < ConnectedParties.Count; ++i)
                {
                    if (ConnectedParties.Values.ElementAt(i).Equals(partyWhoseCounterpartToFind))
                    {
                        counterparty = ConnectedParties.Keys.ElementAt(i);
                        break;
                    }
                }
            }
            else if (IsConnected(partyWhoseCounterpartToFind, ConnectionProfile.Owner))
            {
                ConnectedParties.TryGetValue(partyWhoseCounterpartToFind, out counterparty);
            }

            return counterparty;
        }


        public bool IsAssociatedWithAggregation(Party party)
        {
            var AggregationParties = GetAggregationParties();
            return (party != null && AggregationParties != null && AggregationParties.Count() > 0
                    && AggregationParties.Where(aggregationParty =>
                        aggregationParty.ConversationAccount.Id == party.ConversationAccount.Id
                        && aggregationParty.ServiceUrl == party.ServiceUrl
                        && aggregationParty.ChannelId == party.ChannelId).Count() > 0);
        }

        public bool IsConnected(Party party, ConnectionProfile connectionProfile)
        {
            bool isConnected = false;

            var ConnectedParties = GetConnectedParties();

            if (party != null)
            {
                switch (connectionProfile)
                {
                    case ConnectionProfile.Client:
                        isConnected = ConnectedParties.Values.Contains(party);
                        break;
                    case ConnectionProfile.Owner:
                        isConnected = ConnectedParties.Keys.Contains(party);
                        break;
                    case ConnectionProfile.Any:
                        isConnected = (ConnectedParties.Values.Contains(party) || ConnectedParties.Keys.Contains(party));
                        break;
                    default:
                        break;
                }
            }

            return isConnected;
        }

        public bool RemoveAggregationParty(Party party)
        {

            var aggToDelete = GetList("PartitionKey", $"{party.ChannelId}|{PartyType.Aggregation.ToString()}").FirstOrDefault();
            return StorageHelper.DeleteEntry<PartyEntity>(table, aggToDelete.PartitionKey, aggToDelete.RowKey);

            //return StorageHelper.DeleteEntry<PartyEntity>(table, $"{party.ChannelAccount.Id}|{PartyType.Aggregation.ToString()}", party.ConversationAccount.Id);
        }

        public IList<MessageRouterResult> Disconnect(Party party, ConnectionProfile connectionProfile)
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

        protected virtual IList<MessageRouterResult> RemoveConnections(IList<Party> conversationOwnerParties)
        {
            IList<MessageRouterResult> messageRouterResults = new List<MessageRouterResult>();

            var ConnectedParties = GetConnectedParties();

            foreach (Party conversationOwnerParty in conversationOwnerParties)
            {
                ConnectedParties.TryGetValue(conversationOwnerParty, out Party conversationClientParty);

                if (ConnectedParties.Remove(conversationOwnerParty))
                {
                    //Deletando a conversa
                    var deleteResult = StorageHelper.DeleteEntry<Conversation>(tableConversation, conversationClientParty.ConversationAccount.Id, conversationOwnerParty.ConversationAccount.Id);

                    //Deletando a agregação
                    var removeAggregationResult = RemoveAggregationParty(conversationOwnerParty);

                    if (conversationOwnerParty is PartyWithTimestamps)
                    {
                        (conversationOwnerParty as PartyWithTimestamps).ResetConnectionEstablishedTime();
                    }

                    if (conversationClientParty is PartyWithTimestamps)
                    {
                        (conversationClientParty as PartyWithTimestamps).ResetConnectionEstablishedTime();
                    }

                    messageRouterResults.Add(new MessageRouterResult()
                    {
                        Type = MessageRouterResultType.Disconnected,
                        ConversationOwnerParty = conversationOwnerParty,
                        ConversationClientParty = conversationClientParty
                    });
                }
            }

            return messageRouterResults;
        }

        public IList<MessageRouterResult> RemoveParty(Party partyToRemove)
        {
            List<MessageRouterResult> messageRouterResults = new List<MessageRouterResult>();
            bool wasRemoved = false;

            var PendingRequests = GetPendingRequests();

            // Check user and bot parties
            IList<Party>[] partyLists = new IList<Party>[]
            {
                GetUserParties(),
                GetBotParties()
            };

            foreach (IList<Party> partyList in partyLists)
            {
                IList<Party> partiesToRemove = FindPartiesWithMatchingChannelAccount(partyToRemove, partyList);

                if (partiesToRemove != null)
                {
                    foreach (Party party in partiesToRemove)
                    {
                        //Observar qual o tipo da parte.
                        if (StorageHelper.DeleteEntry<PartyEntity>(table, $"{party.ChannelAccount.Id}|{PartyType.Aggregation.ToString()}", party.ConversationAccount.Id))
                        {
                            wasRemoved = true;
                        }
                    }
                }
            }

            // Check pending requests
            IList<Party> pendingRequestsToRemove = FindPartiesWithMatchingChannelAccount(partyToRemove, PendingRequests);

            foreach (Party pendingRequestToRemove in pendingRequestsToRemove)
            {
                if (StorageHelper.DeleteEntry<PartyEntity>(table, $"{pendingRequestToRemove.ChannelAccount.Id}|{PartyType.PendingRequest.ToString()}", pendingRequestToRemove.ConversationAccount.Id))
                {
                    wasRemoved = true;

                    messageRouterResults.Add(new MessageRouterResult()
                    {
                        Type = MessageRouterResultType.ConnectionRejected,
                        ConversationClientParty = pendingRequestToRemove
                    });
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

            return messageRouterResults;
        }

        public bool RemovePendingRequest(Party party)
        {
            return StorageHelper.DeleteEntry<PartyEntity>(table, $"{party.ChannelAccount.Id}|{PartyType.PendingRequest.ToString()}", party.ConversationAccount.Id);
        }

        public string ResolveBotNameInConversation(Party party)
        {
            string botName = null;

            if (party != null)
            {
                Party botParty = FindBotPartyByChannelAndConversation(party.ChannelAccount.Id, party.ConversationAccount);

                if (botParty != null && botParty.ChannelAccount != null)
                {
                    botName = botParty.ChannelAccount.Name;
                }
            }

            return botName;
        }

#if DEBUG
        public string GetLastMessageRouterResults()
        {
            string lastResultsAsString = string.Empty;

            foreach (MessageRouterResult result in LastMessageRouterResults)
            {
                lastResultsAsString += $"{result.ToString()}\n";
            }

            return lastResultsAsString;
        }

        public void AddMessageRouterResult(MessageRouterResult result)
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
#endif

        public IEnumerable<PartyEntity> GetList(string column, string value)
        {
            PartyEntity p = null;
            var table = StorageHelper.CreateStorageAccountFromConnectionString("Party");
            TableQuery<PartyEntity> query = new TableQuery<PartyEntity>().Where(TableQuery.GenerateFilterCondition(column, QueryComparisons.Equal, value));
            return table.ExecuteQuery(query);
        }

        public IEnumerable<PartyEntity> GetListWith2(string column, string value, string column2, string value2)
        {
            var table = StorageHelper.CreateStorageAccountFromConnectionString("Party");
            TableQuery<PartyEntity> query = new TableQuery<PartyEntity>().Where(
                    TableQuery.CombineFilters(
                        TableQuery.GenerateFilterCondition(column, QueryComparisons.Equal, value),
                        TableOperators.And,
                        TableQuery.GenerateFilterCondition(column2, QueryComparisons.Equal, value2)));
            return table.ExecuteQuery(query);
        }

        public List<Party> ToPartyList(IEnumerable<PartyEntity> list)
        {
            List<Party> plist = new List<Party>();
            foreach (var p in list)
            {
                plist.Add(p.ToParty());
            }
            return plist.ToList();
        }

        public IList<Party> GetUserParties()
        {
            List<PartyEntity> listEntity = table.ExecuteQuery(new TableQuery<PartyEntity>()).Where(x => x.PartyType == PartyType.User.ToString()).ToList();
            return ToPartyList(listEntity);
            //return listEntity.ConvertAll(x => new Party(serviceUrl = x.ServiceUrl, channelId = x.ChannelId, channelAccount = x.ChannelAccount, conversationAccount = x.ConversationAccount));
        }

        public IList<Party> GetBotParties()
        {
            List<PartyEntity> listEntity = table.ExecuteQuery(new TableQuery<PartyEntity>()).Where(x => x.PartyType == PartyType.Bot.ToString()).ToList();
            return ToPartyList(listEntity);
            //return listEntity.ConvertAll(x => new Party(serviceUrl = x.ServiceUrl, channelId = x.ChannelId, channelAccount = x.ChannelAccount, conversationAccount = x.ConversationAccount));
        }

        public IList<Party> GetPendingRequests()
        {
            List<PartyEntity> listEntity = table.ExecuteQuery(new TableQuery<PartyEntity>()).Where(x => x.PartyType == PartyType.PendingRequest.ToString()).ToList();
            return ToPartyList(listEntity);
            //return listEntity.ConvertAll(x => new Party(serviceUrl = x.ServiceUrl, channelId = x.ChannelId, channelAccount = x.ChannelAccount, conversationAccount = x.ConversationAccount));
        }

        public IList<Party> GetAggregationParties()
        {
            List<PartyEntity> listEntity = table.ExecuteQuery(new TableQuery<PartyEntity>()).Where(x => x.PartyType == PartyType.Aggregation.ToString()).ToList();
            return ToPartyList(listEntity);
            //return listEntity.ConvertAll(x => new Party(serviceUrl = x.ServiceUrl, channelId = x.ChannelId, channelAccount = x.ChannelAccount, conversationAccount = x.ConversationAccount));
        }

        public Dictionary<Party, Party> GetConnectedParties()
        {
            Dictionary<Party, Party> parties = new Dictionary<Party, Party>();
            List<Conversation> resultList = tableConversation.ExecuteQuery(new TableQuery<Conversation>()).ToList();

            foreach (var item in resultList)
            {
                parties.Add(JsonConvert.DeserializeObject<PartyEntity>(item.Owner).ToParty(), JsonConvert.DeserializeObject<PartyEntity>(item.Client).ToParty());
            }

            return parties;
        }
    }
}
