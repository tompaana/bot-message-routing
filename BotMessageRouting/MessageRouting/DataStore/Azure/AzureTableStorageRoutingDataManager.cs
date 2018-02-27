using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Table;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Underscore.Bot.Models;
using Underscore.Bot.Models.Azure;
using Underscore.Bot.Utils;

namespace Underscore.Bot.MessageRouting.DataStore.Azure
{
    /// <summary>
    /// Routing data manager that stores the data in Azure Table Storage.
    /// 
    /// See IRoutingDataManager and AbstractRoutingDataManager for general documentation of
    /// properties and methods.
    /// </summary>
    [Serializable]
    public class AzureTableStorageRoutingDataManager : AbstractRoutingDataManager
    {
        protected const string TableNameParties = "Parties";
        protected const string TableNameConnections = "Connections";
        protected const string PartitionKey = "PartitionKey";

        protected CloudTable _partiesTable;
        protected CloudTable _connectionsTable;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="connectionString">The connection string associated with an Azure Table Storage.</param>
        /// <param name="globalTimeProvider">The global time provider for providing the current
        /// time for various events such as when a connection is requested.</param>
        public AzureTableStorageRoutingDataManager(string connectionString, GlobalTimeProvider globalTimeProvider = null)
            : base(globalTimeProvider)
        {
            if (string.IsNullOrEmpty(connectionString))
            {
                throw new ArgumentNullException("The connection string cannot be null or empty");
            }

            _partiesTable = AzureStorageHelper.GetTable(connectionString, TableNameParties);
            _connectionsTable = AzureStorageHelper.GetTable(connectionString, TableNameConnections);

            MakeSureTablesExistAsync();
        }

        public override IList<Party> GetUserParties()
        {
            return ToPartyList(GetPartyEntitiesAsync(PartyEntityType.User).Result);
        }

        public override IList<Party> GetBotParties()
        {
            return ToPartyList(GetPartyEntitiesAsync(PartyEntityType.Bot).Result);
        }

        public override IList<Party> GetAggregationParties()
        {
            return ToPartyList(GetPartyEntitiesAsync(PartyEntityType.Aggregation).Result);
        }

        public override IList<Party> GetPendingRequests()
        {
            return ToPartyList(GetPartyEntitiesAsync(PartyEntityType.PendingRequest).Result);
        }

        public override Dictionary<Party, Party> GetConnectedParties()
        {
            return ToConnectedPartiesDictionary(GetConnectionEntitiesAsync().Result);
        }

        public override async void DeleteAll()
        {
            base.DeleteAll();

            try
            {
                var partyEntities = await GetPartyEntitiesAsync();

                foreach (var partyEntity in partyEntities)
                {
                    await AzureStorageHelper.DeleteEntryAsync<PartyEntity>(
                        _partiesTable, partyEntity.PartitionKey, partyEntity.RowKey);
                }
            }
            catch (StorageException e)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to delete entries: {e.Message}");
                return;
            }

            var connectionEntities = await GetConnectionEntitiesAsync();

            foreach (var connectionEntity in connectionEntities)
            {
                await AzureStorageHelper.DeleteEntryAsync<ConnectionEntity>(
                    _connectionsTable, connectionEntity.PartitionKey, connectionEntity.RowKey);
            }
        }

        protected override bool ExecuteAddParty(Party partyToAdd, bool isUser)
        {
            return AzureStorageHelper.InsertAsync<PartyEntity>(
                _partiesTable,
                new PartyEntity(partyToAdd, isUser ? PartyEntityType.User : PartyEntityType.Bot)).Result;
        }

        protected override bool ExecuteRemoveParty(Party partyToRemove, bool isUser)
        {
            return AzureStorageHelper.DeleteEntryAsync<PartyEntity>(
                _partiesTable,
                PartyEntity.CreatePartitionKey(partyToRemove, isUser ? PartyEntityType.User : PartyEntityType.Bot),
                PartyEntity.CreateRowKey(partyToRemove)).Result;
        }

        protected override bool ExecuteAddAggregationParty(Party aggregationPartyToAdd)
        {
            return AzureStorageHelper.InsertAsync<PartyEntity>(
                _partiesTable, new PartyEntity(aggregationPartyToAdd, PartyEntityType.Aggregation)).Result;
        }

        protected override bool ExecuteRemoveAggregationParty(Party aggregationPartyToRemove)
        {
            return AzureStorageHelper.DeleteEntryAsync<PartyEntity>(
                _partiesTable,
                PartyEntity.CreatePartitionKey(aggregationPartyToRemove, PartyEntityType.Aggregation),
                PartyEntity.CreateRowKey(aggregationPartyToRemove)).Result;
        }

        protected override bool ExecuteAddPendingRequest(Party requestorParty)
        {
            return AzureStorageHelper.InsertAsync<PartyEntity>(
                _partiesTable, new PartyEntity(requestorParty, PartyEntityType.PendingRequest)).Result;
        }

        protected override bool ExecuteRemovePendingRequest(Party requestorParty)
        {
            return AzureStorageHelper.DeleteEntryAsync<PartyEntity>(
                _partiesTable,
                PartyEntity.CreatePartitionKey(requestorParty, PartyEntityType.PendingRequest),
                PartyEntity.CreateRowKey(requestorParty)).Result;
        }

        protected override bool ExecuteAddConnection(Party conversationOwnerParty, Party conversationClientParty)
        {
            return AzureStorageHelper.InsertAsync<ConnectionEntity>(_connectionsTable, new ConnectionEntity()
            {
                PartitionKey = conversationClientParty.ConversationAccount.Id,
                RowKey = conversationOwnerParty.ConversationAccount.Id,
                Client = JsonConvert.SerializeObject(new PartyEntity(conversationClientParty, PartyEntityType.Client)),
                Owner = JsonConvert.SerializeObject(new PartyEntity(conversationOwnerParty, PartyEntityType.Owner))
            }).Result;
        }

        protected override bool ExecuteRemoveConnection(Party conversationOwnerParty)
        {
            Dictionary<Party, Party> connectedParties = GetConnectedParties();

            if (connectedParties != null && connectedParties.Remove(conversationOwnerParty))
            {
                Party conversationClientParty = GetConnectedCounterpart(conversationOwnerParty);

                return AzureStorageHelper.DeleteEntryAsync<ConnectionEntity>(
                    _connectionsTable,
                    conversationClientParty.ConversationAccount.Id,
                    conversationOwnerParty.ConversationAccount.Id).Result;
            }

            return false;
        }

        /// <summary>
        /// Makes sure the required tables exist.
        /// </summary>
        protected virtual async void MakeSureTablesExistAsync()
        {
            try
            {
                await _partiesTable.CreateIfNotExistsAsync();
                System.Diagnostics.Debug.WriteLine("Parties table created or did already exist");
            }
            catch (StorageException e)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to create the parties table (perhaps it already exists): {e.Message}");
            }

            try
            {
                await _connectionsTable.CreateIfNotExistsAsync();
                System.Diagnostics.Debug.WriteLine("Connections table created or did already exist");
            }
            catch (StorageException e)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to create the connections table (perhaps it already exists): {e.Message}");
            }
        }

        protected virtual void OnPartiesTableCreateIfNotExistsFinished(IAsyncResult result)
        {
            if (result == null)
            {
                System.Diagnostics.Debug.WriteLine((result.IsCompleted)
                    ? "Create table operation for parties table completed"
                    : "Create table operation for parties table did not complete");
            }
        }

        protected virtual void OnConnectionsTableCreateIfNotExistsFinished(IAsyncResult result)
        {
            if (result == null)
            {
                System.Diagnostics.Debug.WriteLine((result.IsCompleted)
                    ? "Create table operation for connections table completed"
                    : "Create table operation for connections table did not complete");
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="tableQuery"></param>
        /// <returns></returns>
        protected virtual async Task<IEnumerable<PartyEntity>> GetPartyEntitiesAsync(TableQuery<PartyEntity> tableQuery = null)
        {
            tableQuery = tableQuery ?? new TableQuery<PartyEntity>();
            return await AzureStorageHelper.ExecuteTableQueryAsync<PartyEntity>(_partiesTable, tableQuery);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="partyEntityType"></param>
        /// <returns></returns>
        protected virtual async Task<IEnumerable<PartyEntity>> GetPartyEntitiesAsync(PartyEntityType partyEntityType)
        {
            TableQuery<PartyEntity> tableQuery = new TableQuery<PartyEntity>().Where(
                TableQuery.GenerateFilterCondition(PartitionKey, QueryComparisons.Equal, $"*|{partyEntityType}"));
            return await GetPartyEntitiesAsync(tableQuery);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="tableQuery"></param>
        /// <returns></returns>
        protected virtual async Task<IEnumerable<ConnectionEntity>> GetConnectionEntitiesAsync(TableQuery<ConnectionEntity> tableQuery = null)
        {
            Dictionary<Party, Party> connectedParties = new Dictionary<Party, Party>();
            tableQuery = tableQuery ?? new TableQuery<ConnectionEntity>();
            return await AzureStorageHelper.ExecuteTableQueryAsync(_connectionsTable, tableQuery);
        }

        /// <summary>
        /// Converts the given entities into a party list.
        /// </summary>
        /// <param name="partyEntities">The entities to convert.</param>
        /// <returns>A newly created list of parties based on the given entities.</returns>
        protected virtual List<Party> ToPartyList(IEnumerable<PartyEntity> partyEntities)
        {
            List<Party> partyList = new List<Party>();

            foreach (var partyEntity in partyEntities)
            {
                partyList.Add(partyEntity.ToParty());
            }

            return partyList.ToList();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="connectionEntities"></param>
        /// <returns></returns>
        protected virtual Dictionary<Party, Party> ToConnectedPartiesDictionary(IEnumerable<ConnectionEntity> connectionEntities)
        {
            Dictionary<Party, Party> connectedParties = new Dictionary<Party, Party>();

            foreach (var connectionEntity in connectionEntities)
            {
                connectedParties.Add(
                    JsonConvert.DeserializeObject<PartyEntity>(connectionEntity.Owner).ToParty(),
                    JsonConvert.DeserializeObject<PartyEntity>(connectionEntity.Client).ToParty());
            }

            return connectedParties;
        }
    }
}
