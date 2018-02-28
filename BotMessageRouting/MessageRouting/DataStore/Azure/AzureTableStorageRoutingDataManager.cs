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
        protected const string TableNameBotParties = "BotParties";
        protected const string TableNameUserParties = "UserParties";
        protected const string TableNameAggregationParties = "AggregationParties";
        protected const string TableNamePendingRequests = "PendingRequests";
        protected const string TableNameConnections = "Connections";
        protected const string PartitionKey = "PartitionKey";

        protected CloudTable _botPartiesTable;
        protected CloudTable _userPartiesTable;
        protected CloudTable _aggregationPartiesTable;
        protected CloudTable _pendingRequestsTable;
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

            _botPartiesTable = AzureStorageHelper.GetTable(connectionString, TableNameBotParties);
            _userPartiesTable = AzureStorageHelper.GetTable(connectionString, TableNameUserParties);
            _aggregationPartiesTable = AzureStorageHelper.GetTable(connectionString, TableNameAggregationParties);
            _pendingRequestsTable = AzureStorageHelper.GetTable(connectionString, TableNamePendingRequests);
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

            CloudTable[] cloudTables =
{
                _botPartiesTable,
                _userPartiesTable,
                _aggregationPartiesTable,
                _connectionsTable
            };

            foreach (CloudTable cloudTable in cloudTables)
            {
                try
                {
                    var partyEntities = await GetPartyEntitiesAsync(cloudTable);

                    foreach (var partyEntity in partyEntities)
                    {
                        await AzureStorageHelper.DeleteEntryAsync<PartyEntity>(
                            cloudTable, partyEntity.PartitionKey, partyEntity.RowKey);
                    }
                }
                catch (StorageException e)
                {
                    System.Diagnostics.Debug.WriteLine($"Failed to delete entries from table '{cloudTable.Name}': {e.Message}");
                    return;
                }
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
                isUser ? _userPartiesTable : _botPartiesTable,
                new PartyEntity(partyToAdd, isUser ? PartyEntityType.User : PartyEntityType.Bot)).Result;
        }

        protected override bool ExecuteRemoveParty(Party partyToRemove, bool isUser)
        {
            return AzureStorageHelper.DeleteEntryAsync<PartyEntity>(
                isUser ? _userPartiesTable : _botPartiesTable,
                PartyEntity.CreatePartitionKey(partyToRemove, isUser ? PartyEntityType.User : PartyEntityType.Bot),
                PartyEntity.CreateRowKey(partyToRemove)).Result;
        }

        protected override bool ExecuteAddAggregationParty(Party aggregationPartyToAdd)
        {
            return AzureStorageHelper.InsertAsync<PartyEntity>(
                _aggregationPartiesTable, new PartyEntity(aggregationPartyToAdd, PartyEntityType.Aggregation)).Result;
        }

        protected override bool ExecuteRemoveAggregationParty(Party aggregationPartyToRemove)
        {
            return AzureStorageHelper.DeleteEntryAsync<PartyEntity>(
                _aggregationPartiesTable,
                PartyEntity.CreatePartitionKey(aggregationPartyToRemove, PartyEntityType.Aggregation),
                PartyEntity.CreateRowKey(aggregationPartyToRemove)).Result;
        }

        protected override bool ExecuteAddPendingRequest(Party requestorParty)
        {
            return AzureStorageHelper.InsertAsync<PartyEntity>(
                _pendingRequestsTable, new PartyEntity(requestorParty, PartyEntityType.PendingRequest)).Result;
        }

        protected override bool ExecuteRemovePendingRequest(Party requestorParty)
        {
            return AzureStorageHelper.DeleteEntryAsync<PartyEntity>(
                _pendingRequestsTable,
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
            CloudTable[] cloudTables =
            {
                _botPartiesTable,
                _userPartiesTable,
                _aggregationPartiesTable,
                _pendingRequestsTable,
                _connectionsTable
            };

            foreach (CloudTable cloudTable in cloudTables)
            {
                try
                {
                    await cloudTable.CreateIfNotExistsAsync();
                    System.Diagnostics.Debug.WriteLine($"Table '{cloudTable.Name}' created or did already exist");
                }
                catch (StorageException e)
                {
                    System.Diagnostics.Debug.WriteLine($"Failed to create table '{cloudTable.Name}' (perhaps it already exists): {e.Message}");
                }
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
        /// Resolves the cloud table associated with the given party entity type.
        /// </summary>
        /// <param name="partyEntityType">The party entity type.</param>
        /// <returns>The cloud table associated with the party entity type.</returns>
        protected virtual CloudTable CloudTableByPartyEntityType(PartyEntityType partyEntityType)
        {
            switch (partyEntityType)
            {
                case PartyEntityType.Bot:
                    return _botPartiesTable;
                case PartyEntityType.User:
                    return _userPartiesTable;
                case PartyEntityType.Aggregation:
                    return _aggregationPartiesTable;
                case PartyEntityType.PendingRequest:
                    return _pendingRequestsTable;
                default:
                    throw new ArgumentException($"No cloud table associated with party entity type {partyEntityType}");
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="cloudTable"></param>
        /// <param name="tableQuery"></param>
        /// <returns></returns>
        protected virtual async Task<IEnumerable<PartyEntity>> GetPartyEntitiesAsync(
            CloudTable cloudTable, TableQuery<PartyEntity> tableQuery = null)
        {
            tableQuery = tableQuery ?? new TableQuery<PartyEntity>();
            return await AzureStorageHelper.ExecuteTableQueryAsync<PartyEntity>(cloudTable, tableQuery);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="partyEntityType"></param>
        /// <returns></returns>
        protected virtual async Task<IEnumerable<PartyEntity>> GetPartyEntitiesAsync(PartyEntityType partyEntityType)
        {
            TableQuery<PartyEntity> tableQuery = new TableQuery<PartyEntity>();
            return await GetPartyEntitiesAsync(CloudTableByPartyEntityType(partyEntityType), tableQuery);
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
