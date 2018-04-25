using Microsoft.Bot.Schema;
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
    public class AzureTableStorageRoutingDataStore : IRoutingDataStore
    {
        protected const string TableNameBotParties = "BotParties";
        protected const string TableNameUserParties = "UserParties";
        protected const string TableNameAggregationParties = "AggregationParties";
        protected const string TableNameConnectionRequests = "ConnectionRequests";
        protected const string TableNameConnections = "Connections";
        protected const string PartitionKey = "PartitionKey";

        protected CloudTable _botPartiesTable;
        protected CloudTable _userPartiesTable;
        protected CloudTable _aggregationPartiesTable;
        protected CloudTable _connectionRequestsTable;
        protected CloudTable _connectionsTable;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="connectionString">The connection string associated with an Azure Table Storage.</param>
        /// <param name="globalTimeProvider">The global time provider for providing the current
        /// time for various events such as when a connection is requested.</param>
        public AzureTableStorageRoutingDataStore(string connectionString, GlobalTimeProvider globalTimeProvider = null)
            : base(globalTimeProvider)
        {
            if (string.IsNullOrEmpty(connectionString))
            {
                throw new ArgumentNullException("The connection string cannot be null or empty");
            }

            _botPartiesTable = AzureStorageHelper.GetTable(connectionString, TableNameBotParties);
            _userPartiesTable = AzureStorageHelper.GetTable(connectionString, TableNameUserParties);
            _aggregationPartiesTable = AzureStorageHelper.GetTable(connectionString, TableNameAggregationParties);
            _connectionRequestsTable = AzureStorageHelper.GetTable(connectionString, TableNameConnectionRequests);
            _connectionsTable = AzureStorageHelper.GetTable(connectionString, TableNameConnections);

            MakeSureTablesExistAsync();
        }

        public override IList<ConversationReference> GetUserParties()
        {
            return ToConversationReferenceList(GetConversationReferenceEntitiesAsync(ConversationReferenceEntityType.User).Result);
        }

        public override IList<ConversationReference> GetBotParties()
        {
            return ToConversationReferenceList(GetConversationReferenceEntitiesAsync(ConversationReferenceEntityType.Bot).Result);
        }

        public override IList<ConversationReference> GetAggregationChannels()
        {
            return ToConversationReferenceList(GetConversationReferenceEntitiesAsync(ConversationReferenceEntityType.Aggregation).Result);
        }

        public override IList<ConversationReference> GetConnectionRequests()
        {
            return ToConversationReferenceList(GetConversationReferenceEntitiesAsync(ConversationReferenceEntityType.ConnectionRequest).Result);
        }

        public override Dictionary<ConversationReference, ConversationReference> GetConnectedParties()
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
                    var ConversationReferenceEntities = await GetConversationReferenceEntitiesAsync(cloudTable);

                    foreach (var ConversationReferenceEntity in ConversationReferenceEntities)
                    {
                        await AzureStorageHelper.DeleteEntryAsync<ConversationReferenceEntity>(
                            cloudTable, ConversationReferenceEntity.PartitionKey, ConversationReferenceEntity.RowKey);
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

        protected override bool ExecuteAddConversationReference(ConversationReference ConversationReferenceToAdd, bool isUser)
        {
            return AzureStorageHelper.InsertAsync<ConversationReferenceEntity>(
                isUser ? _userPartiesTable : _botPartiesTable,
                new ConversationReferenceEntity(ConversationReferenceToAdd, isUser ? ConversationReferenceEntityType.User : ConversationReferenceEntityType.Bot)).Result;
        }

        protected override bool ExecuteRemoveConversationReference(ConversationReference ConversationReferenceToRemove, bool isUser)
        {
            return AzureStorageHelper.DeleteEntryAsync<ConversationReferenceEntity>(
                isUser ? _userPartiesTable : _botPartiesTable,
                ConversationReferenceEntity.CreatePartitionKey(ConversationReferenceToRemove, isUser ? ConversationReferenceEntityType.User : ConversationReferenceEntityType.Bot),
                ConversationReferenceEntity.CreateRowKey(ConversationReferenceToRemove)).Result;
        }

        protected override bool ExecuteAddAggregationConversationReference(ConversationReference aggregationConversationReferenceToAdd)
        {
            return AzureStorageHelper.InsertAsync<ConversationReferenceEntity>(
                _aggregationPartiesTable, new ConversationReferenceEntity(aggregationConversationReferenceToAdd, ConversationReferenceEntityType.Aggregation)).Result;
        }

        protected override bool ExecuteRemoveAggregationConversationReference(ConversationReference aggregationConversationReferenceToRemove)
        {
            return AzureStorageHelper.DeleteEntryAsync<ConversationReferenceEntity>(
                _aggregationPartiesTable,
                ConversationReferenceEntity.CreatePartitionKey(aggregationConversationReferenceToRemove, ConversationReferenceEntityType.Aggregation),
                ConversationReferenceEntity.CreateRowKey(aggregationConversationReferenceToRemove)).Result;
        }

        protected override bool ExecuteAddConnectionRequest(ConversationReference requestorConversationReference)
        {
            return AzureStorageHelper.InsertAsync<ConversationReferenceEntity>(
                _connectionRequestsTable, new ConversationReferenceEntity(requestorConversationReference, ConversationReferenceEntityType.ConnectionRequest)).Result;
        }

        protected override bool ExecuteRemoveConnectionRequest(ConversationReference requestorConversationReference)
        {
            return AzureStorageHelper.DeleteEntryAsync<ConversationReferenceEntity>(
                _connectionRequestsTable,
                ConversationReferenceEntity.CreatePartitionKey(requestorConversationReference, ConversationReferenceEntityType.ConnectionRequest),
                ConversationReferenceEntity.CreateRowKey(requestorConversationReference)).Result;
        }

        protected override bool ExecuteAddConnection(ConversationReference conversationOwnerConversationReference, ConversationReference conversationClientConversationReference)
        {
            string conversationOwnerAccountID, conversationClientAccountID;
            CheckWichConversationReferenceIsNull(conversationOwnerConversationReference, conversationClientConversationReference, out conversationOwnerAccountID, out conversationClientAccountID);

            return AzureStorageHelper.InsertAsync<ConnectionEntity>(_connectionsTable, new ConnectionEntity()
            {
                PartitionKey = conversationClientAccountID,
                RowKey = conversationOwnerAccountID,
                Client = JsonConvert.SerializeObject(new ConversationReferenceEntity(conversationClientConversationReference, ConversationReferenceEntityType.Client)),
                Owner = JsonConvert.SerializeObject(new ConversationReferenceEntity(conversationOwnerConversationReference, ConversationReferenceEntityType.Owner))
            }).Result;
        }

        // PARTIAL METHOD
        private static void CheckWichConversationReferenceIsNull(ConversationReference conversationOwnerConversationReference, ConversationReference conversationClientConversationReference, out string conversationOwnerAccountID, out string conversationClientAccountID)
        {
            if (conversationClientConversationReference.Bot != null)
                conversationClientAccountID = conversationClientConversationReference.Bot.Id;
            else conversationClientAccountID = conversationClientConversationReference.User.Id;

            if (conversationOwnerConversationReference.Bot != null)
                conversationOwnerAccountID = conversationOwnerConversationReference.Bot.Id;
            else conversationOwnerAccountID = conversationOwnerConversationReference.User.Id;
        }

        protected override bool ExecuteRemoveConnection(ConversationReference conversationOwnerConversationReference)
        {
            Dictionary<ConversationReference, ConversationReference> connectedParties = GetConnectedParties();

            if (connectedParties != null && connectedParties.Remove(conversationOwnerConversationReference))
            {
                ConversationReference conversationClientConversationReference = GetConnectedCounterpart(conversationOwnerConversationReference);

                string conversationOwnerAccountID, conversationClientAccountID;
                CheckWichConversationReferenceIsNull(conversationOwnerConversationReference, conversationClientConversationReference, out conversationOwnerAccountID, out conversationClientAccountID);

                return AzureStorageHelper.DeleteEntryAsync<ConnectionEntity>(
                    _connectionsTable,
                    conversationClientAccountID,
                    conversationOwnerAccountID).Result;
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
                _connectionRequestsTable,
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
        /// Resolves the cloud table associated with the given ConversationReference entity type.
        /// </summary>
        /// <param name="ConversationReferenceEntityType">The ConversationReference entity type.</param>
        /// <returns>The cloud table associated with the ConversationReference entity type.</returns>
        protected virtual CloudTable CloudTableByConversationReferenceEntityType(ConversationReferenceEntityType ConversationReferenceEntityType)
        {
            switch (ConversationReferenceEntityType)
            {
                case ConversationReferenceEntityType.Bot:
                    return _botPartiesTable;
                case ConversationReferenceEntityType.User:
                    return _userPartiesTable;
                case ConversationReferenceEntityType.Aggregation:
                    return _aggregationPartiesTable;
                case ConversationReferenceEntityType.ConnectionRequest:
                    return _connectionRequestsTable;
                default:
                    throw new ArgumentException($"No cloud table associated with ConversationReference entity type {ConversationReferenceEntityType}");
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="cloudTable"></param>
        /// <param name="tableQuery"></param>
        /// <returns></returns>
        protected virtual async Task<IEnumerable<ConversationReferenceEntity>> GetConversationReferenceEntitiesAsync(
            CloudTable cloudTable, TableQuery<ConversationReferenceEntity> tableQuery = null)
        {
            tableQuery = tableQuery ?? new TableQuery<ConversationReferenceEntity>();
            return await AzureStorageHelper.ExecuteTableQueryAsync<ConversationReferenceEntity>(cloudTable, tableQuery);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="ConversationReferenceEntityType"></param>
        /// <returns></returns>
        protected virtual async Task<IEnumerable<ConversationReferenceEntity>> GetConversationReferenceEntitiesAsync(ConversationReferenceEntityType ConversationReferenceEntityType)
        {
            TableQuery<ConversationReferenceEntity> tableQuery = new TableQuery<ConversationReferenceEntity>();
            return await GetConversationReferenceEntitiesAsync(CloudTableByConversationReferenceEntityType(ConversationReferenceEntityType), tableQuery);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="tableQuery"></param>
        /// <returns></returns>
        protected virtual async Task<IEnumerable<ConnectionEntity>> GetConnectionEntitiesAsync(TableQuery<ConnectionEntity> tableQuery = null)
        {
            Dictionary<ConversationReference, ConversationReference> connectedParties = new Dictionary<ConversationReference, ConversationReference>();
            tableQuery = tableQuery ?? new TableQuery<ConnectionEntity>();
            return await AzureStorageHelper.ExecuteTableQueryAsync(_connectionsTable, tableQuery);
        }

        /// <summary>
        /// Converts the given entities into a ConversationReference list.
        /// </summary>
        /// <param name="ConversationReferenceEntities">The entities to convert.</param>
        /// <returns>A newly created list of parties based on the given entities.</returns>
        protected virtual List<ConversationReference> ToConversationReferenceList(IEnumerable<ConversationReferenceEntity> ConversationReferenceEntities)
        {
            List<ConversationReference> ConversationReferenceList = new List<ConversationReference>();

            foreach (var ConversationReferenceEntity in ConversationReferenceEntities)
            {
                ConversationReferenceList.Add(ConversationReferenceEntity.ToConversationReference());
            }

            return ConversationReferenceList.ToList();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="connectionEntities"></param>
        /// <returns></returns>
        protected virtual Dictionary<ConversationReference, ConversationReference> ToConnectedPartiesDictionary(IEnumerable<ConnectionEntity> connectionEntities)
        {
            Dictionary<ConversationReference, ConversationReference> connectedParties = new Dictionary<ConversationReference, ConversationReference>();

            foreach (var connectionEntity in connectionEntities)
            {
                connectedParties.Add(
                    JsonConvert.DeserializeObject<ConversationReferenceEntity>(connectionEntity.Owner).ToConversationReference(),
                    JsonConvert.DeserializeObject<ConversationReferenceEntity>(connectionEntity.Client).ToConversationReference());
            }

            return connectedParties;
        }
    }
}
