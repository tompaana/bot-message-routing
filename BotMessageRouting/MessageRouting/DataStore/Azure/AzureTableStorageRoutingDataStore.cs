using Microsoft.Bot.Schema;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Table;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Underscore.Bot.Models;
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
        protected const string TableNameBotInstances = "BotInstances";
        protected const string TableNameUsers = "Users";
        protected const string TableNameAggregationChannels = "AggregationChannels";
        protected const string TableNameConnectionRequests = "ConnectionRequests";
        protected const string TableNameConnections = "Connections";
        protected const string PartitionKey = "PartitionKey";

        protected CloudTable _botInstancesTable;
        protected CloudTable _usersTable;
        protected CloudTable _aggregationChannelsTable;
        protected CloudTable _connectionRequestsTable;
        protected CloudTable _connectionsTable;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="connectionString">The connection string associated with an Azure Table Storage.</param>
        /// <param name="globalTimeProvider">The global time provider for providing the current
        /// time for various events such as when a connection is requested.</param>
        public AzureTableStorageRoutingDataStore(string connectionString, GlobalTimeProvider globalTimeProvider = null)
            : base()
        {
            if (string.IsNullOrEmpty(connectionString))
            {
                throw new ArgumentNullException("The connection string cannot be null or empty");
            }

            _botInstancesTable = AzureStorageHelper.GetTable(connectionString, TableNameBotInstances);
            _usersTable = AzureStorageHelper.GetTable(connectionString, TableNameUsers);
            _aggregationChannelsTable = AzureStorageHelper.GetTable(connectionString, TableNameAggregationChannels);
            _connectionRequestsTable = AzureStorageHelper.GetTable(connectionString, TableNameConnectionRequests);
            _connectionsTable = AzureStorageHelper.GetTable(connectionString, TableNameConnections);

            MakeSureTablesExistAsync();
        }

        #region Get region
        public IList<ConversationReference> GetUsers()
        {
            var entities = GetAllEntitiesFromTable("botHandOff", _usersTable).Result;

            return GetAllConversationReferencesFromEntities(entities);
        }

        public IList<ConversationReference> GetBotInstances()
        {
            var entities = GetAllEntitiesFromTable("botHandOff", _botInstancesTable).Result;

            return GetAllConversationReferencesFromEntities(entities);
        }

        public IList<ConversationReference> GetAggregationChannels()
        {
            var entities = GetAllEntitiesFromTable("botHandOff", _aggregationChannelsTable).Result;

            return GetAllConversationReferencesFromEntities(entities);
        }

        public IList<ConnectionRequest> GetConnectionRequests()
        {
            IList<HandOffEntity> entities = GetAllEntitiesFromTable("botHandOff", _connectionRequestsTable).Result;

            List<ConnectionRequest> connectionRequests = new List<ConnectionRequest>();
            foreach (HandOffEntity entity in entities)
            {
                ConnectionRequest connectionRequest =
                    JsonConvert.DeserializeObject<ConnectionRequest>(entity.Body);
                connectionRequests.Add(connectionRequest);
            }
            return connectionRequests;
        }

        public IList<Connection> GetConnections()
        {
            IList<HandOffEntity> entities = GetAllEntitiesFromTable("botHandOff", _connectionsTable).Result;

            List<Connection> connections = new List<Connection>();
            foreach (HandOffEntity entity in entities)
            {
                Connection connection =
                    JsonConvert.DeserializeObject<Connection>(entity.Body);
                connections.Add(connection);
            }
            return connections;
        }
        #endregion

        #region Add region
        public bool AddConversationReference(ConversationReference conversationReferenceToAdd)
        {
            CloudTable table;
            if (conversationReferenceToAdd.Bot != null)
                table = _botInstancesTable;
            else table = _usersTable;

            string rowKey = conversationReferenceToAdd.Conversation.Id;
            string body = JsonConvert.SerializeObject(conversationReferenceToAdd);

            return InsertEntityToTable(rowKey, body, table);
        }

        public bool AddAggregationChannel(ConversationReference aggregationChannelToAdd)
        {
            string rowKey = aggregationChannelToAdd.Conversation.Id;
            string body = JsonConvert.SerializeObject(aggregationChannelToAdd);

            return InsertEntityToTable(rowKey, body, _aggregationChannelsTable);
        }

        public bool AddConnectionRequest(ConnectionRequest connectionRequestToAdd)
        {
            string rowKey = connectionRequestToAdd.Requestor.Conversation.Id;
            string body = JsonConvert.SerializeObject(connectionRequestToAdd);

            return InsertEntityToTable(rowKey, body, _connectionRequestsTable);
        }
        public bool AddConnection(Connection connectionToAdd)
        {
            string rowKey = connectionToAdd.ConversationReference1.Conversation.Id +
                connectionToAdd.ConversationReference2.Conversation.Id;
            string body = JsonConvert.SerializeObject(connectionToAdd);

            return InsertEntityToTable(rowKey, body, _connectionsTable);
        }
        #endregion

        #region Remove region
        public bool RemoveConversationReference(ConversationReference conversationReferenceToAdd)
        {
            CloudTable table;
            if (conversationReferenceToAdd.Bot != null)
                table = _botInstancesTable;
            else table = _usersTable;

            return AzureStorageHelper.DeleteEntryAsync<HandOffEntity>(
                table,
                "handOffBot",
                conversationReferenceToAdd.Conversation.Id).Result;
        }

        public bool RemoveAggregationChannel(ConversationReference toRemove)
        {
            return AzureStorageHelper.DeleteEntryAsync<HandOffEntity>(
                _connectionRequestsTable,
                "handOffBot",
                toRemove.Conversation.Id).Result;
        }

        public bool RemoveConnectionRequest(ConnectionRequest connectionRequestToRemove)
        {
            return AzureStorageHelper.DeleteEntryAsync<HandOffEntity>(
                _connectionRequestsTable,
                "handOffBot",
                connectionRequestToRemove.Requestor.Conversation.Id).Result;
        }

        public bool RemoveConnection(Connection connectionToRemove)
        {
            string rowKey = connectionToRemove.ConversationReference1.Conversation.Id +
                connectionToRemove.ConversationReference2.Conversation.Id;

            return AzureStorageHelper.DeleteEntryAsync<HandOffEntity>(
                _connectionsTable,
                "handOffBot",
                rowKey).Result;
        }
        #endregion

        #region Validators and helpers
        /// <summary>
        /// Makes sure the required tables exist.
        /// </summary>
        protected virtual async void MakeSureTablesExistAsync()
        {
            CloudTable[] cloudTables =
            {
                _botInstancesTable,
                _usersTable,
                _aggregationChannelsTable,
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

        private List<ConversationReference> GetAllConversationReferencesFromEntities(IList<HandOffEntity> entities)
        {
            List<ConversationReference> conversationReferences = new List<ConversationReference>();
            foreach (HandOffEntity entity in entities)
            {
                ConversationReference conversationReference =
                    JsonConvert.DeserializeObject<ConversationReference>(entity.Body);
                conversationReferences.Add(conversationReference);
            }
            return conversationReferences;
        }

        private async Task<IList<HandOffEntity>> GetAllEntitiesFromTable(string partitionKey, CloudTable table)
        {
            TableQuery<HandOffEntity> query = new TableQuery<HandOffEntity>()
                .Where(TableQuery.GenerateFilterCondition(
                    "PartitionKey",
                    QueryComparisons.Equal,
                    partitionKey));

            return await table.ExecuteTableQueryAsync(query);
        }

        private static bool InsertEntityToTable(string rowKey, string body, CloudTable table)
        {
            return AzureStorageHelper.InsertAsync<HandOffEntity>(
                table, new HandOffEntity()
                {
                    Body = body,
                    PartitionKey = "handOffBot",
                    RowKey = rowKey
                }).Result;
        }
        #endregion
    }

    public class HandOffEntity : TableEntity
    {
        public string Body { get; set; }
    }
}