using BotMessageRouting.MessageRouting.Handlers;
using BotMessageRouting.MessageRouting.Logging;
using Microsoft.Bot.Schema;
using Microsoft.WindowsAzure.Storage.Table;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using Underscore.Bot.MessageRouting.Models;
using Underscore.Bot.MessageRouting.Models.Azure;

namespace Underscore.Bot.MessageRouting.DataStore.Azure
{
    /// <summary>
    /// Routing data store that stores the data in Azure Table Storage.
    /// See the IRoutingDataStore interface for the general documentation of properties and methods.
    /// </summary>
    [Serializable]
    public class AzureTableStorageRoutingDataStore : IRoutingDataStore
    {
        protected const string DefaultPartitionKey          = "BotMessageRouting";
        protected const string TableNameBotInstances        = "BotInstances";
        protected const string TableNameUsers               = "Users";
        protected const string TableNameAggregationChannels = "AggregationChannels";
        protected const string TableNameConnectionRequests  = "ConnectionRequests";
        protected const string TableNameConnections         = "Connections";

        protected CloudTable _botInstancesTable;
        protected CloudTable _usersTable;
        protected CloudTable _aggregationChannelsTable;
        protected CloudTable _connectionRequestsTable;
        protected CloudTable _connectionsTable;

        private ILogger          _logger;
        private ExceptionHandler _exceptionHandler;


        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="connectionString">The connection string associated with an Azure Table Storage.</param>
        /// <param name="logger">Logger to use. Defaults to DebugLogger in the SDK</param>
        public AzureTableStorageRoutingDataStore(string connectionString, ILogger logger = null)
        {
            _logger = logger ?? DebugLogger.Default;
            _exceptionHandler = new ExceptionHandler(_logger);
            _logger.Enter();

            if (string.IsNullOrEmpty(connectionString))
            {
                throw new ArgumentNullException("The connection string cannot be null or empty");
            }

            _botInstancesTable        = AzureStorageHelper.GetTable(connectionString, TableNameBotInstances);
            _usersTable               = AzureStorageHelper.GetTable(connectionString, TableNameUsers);
            _aggregationChannelsTable = AzureStorageHelper.GetTable(connectionString, TableNameAggregationChannels);
            _connectionRequestsTable  = AzureStorageHelper.GetTable(connectionString, TableNameConnectionRequests);
            _connectionsTable         = AzureStorageHelper.GetTable(connectionString, TableNameConnections);

            MakeSureTablesExistAsync();
        }


        #region Get region
        public IList<ConversationReference> GetUsers()
        {
            _logger.Enter();

            var entities = GetAllEntitiesFromTable(_usersTable).Result;
            return GetAllConversationReferencesFromEntities(entities);
        }


        public IList<ConversationReference> GetBotInstances()
        {
            _logger.Enter();

            var entities = GetAllEntitiesFromTable(_botInstancesTable).Result;
            return GetAllConversationReferencesFromEntities(entities);
        }


        public IList<ConversationReference> GetAggregationChannels()
        {
            _logger.Enter();

            var entities = GetAllEntitiesFromTable(_aggregationChannelsTable).Result;
            return GetAllConversationReferencesFromEntities(entities);
        }


        public IList<ConnectionRequest> GetConnectionRequests()
        {
            _logger.Enter();

            var entities = GetAllEntitiesFromTable(_connectionRequestsTable).Result;

            var connectionRequests = new List<ConnectionRequest>();
            foreach (RoutingDataEntity entity in entities)
            {
                var connectionRequest = 
                    JsonConvert.DeserializeObject<ConnectionRequest>(entity.Body);
                connectionRequests.Add(connectionRequest);
            }
            return connectionRequests;
        }


        public IList<Connection> GetConnections()
        {
            _logger.Enter();

            var entities = GetAllEntitiesFromTable(_connectionsTable).Result;

            var connections = new List<Connection>();
            foreach (RoutingDataEntity entity in entities)
            {
                var connection = 
                    JsonConvert.DeserializeObject<Connection>(entity.Body);
                connections.Add(connection);
            }
            return connections;
        }
        #endregion

        #region Add region
        public bool AddConversationReference(ConversationReference conversationReference)
        {
            _logger.Enter();

            CloudTable table;
            if (conversationReference.Bot != null)
                table = _botInstancesTable;
            else
                table = _usersTable;

            string rowKey = conversationReference.Conversation.Id;
            string body = JsonConvert.SerializeObject(conversationReference);

            return InsertEntityToTable(rowKey, body, table);
        }


        public bool AddAggregationChannel(ConversationReference aggregationChannel)
        {
            _logger.Enter();

            string rowKey = aggregationChannel.Conversation.Id;
            string body = JsonConvert.SerializeObject(aggregationChannel);

            return InsertEntityToTable(rowKey, body, _aggregationChannelsTable);
        }


        public bool AddConnectionRequest(ConnectionRequest connectionRequest)
        {
            _logger.Enter();

            string rowKey = connectionRequest.Requestor.Conversation.Id;
            string body = JsonConvert.SerializeObject(connectionRequest);

            return InsertEntityToTable(rowKey, body, _connectionRequestsTable);
        }


        public bool AddConnection(Connection connection)
        {
            _logger.Enter();

            string rowKey = connection.ConversationReference1.Conversation.Id +
                connection.ConversationReference2.Conversation.Id;
            string body = JsonConvert.SerializeObject(connection);

            return InsertEntityToTable(rowKey, body, _connectionsTable);
        }

        #endregion

        #region Remove region
        public bool RemoveConversationReference(ConversationReference conversationReference)
        {
            _logger.Enter();

            CloudTable table;
            if (conversationReference.Bot != null)
                table = _botInstancesTable;
            else table = _usersTable;

            string rowKey = conversationReference.Conversation.Id;
            return AzureStorageHelper.DeleteEntryAsync<RoutingDataEntity>(
                table, DefaultPartitionKey, rowKey).Result;
        }


        public bool RemoveAggregationChannel(ConversationReference aggregationChannel)
        {
            _logger.Enter();

            string rowKey = aggregationChannel.Conversation.Id;
            return AzureStorageHelper.DeleteEntryAsync<RoutingDataEntity>(
                _aggregationChannelsTable, DefaultPartitionKey, rowKey).Result;
        }


        public bool RemoveConnectionRequest(ConnectionRequest connectionRequest)
        {
            _logger.Enter();

            string rowKey = connectionRequest.Requestor.Conversation.Id;
            return AzureStorageHelper.DeleteEntryAsync<RoutingDataEntity>(
                _connectionRequestsTable, DefaultPartitionKey, rowKey).Result;
        }


        public bool RemoveConnection(Connection connection)
        {
            _logger.Enter();

            string rowKey = connection.ConversationReference1.Conversation.Id +
                connection.ConversationReference2.Conversation.Id;
            return AzureStorageHelper.DeleteEntryAsync<RoutingDataEntity>(
                _connectionsTable, DefaultPartitionKey, rowKey).Result;
        }
        #endregion

        #region Validators and helpers

        /// <summary>
        /// Makes sure the required tables exist.
        /// </summary>
        protected virtual async void MakeSureTablesExistAsync()
        {
            _logger.Enter();

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
                await _exceptionHandler.ExecuteAsync(
                    unsafeFunction: ( ) => cloudTable.CreateIfNotExistsAsync(),
                    customHandler : (e) => Debug.WriteLine($"Failed to create table '{cloudTable.Name}' (perhaps it already exists): {e.Message}")
                    );
            }
        }


        private List<ConversationReference> GetAllConversationReferencesFromEntities(IList<RoutingDataEntity> entities)
        {
            _logger.Enter();

            var conversationReferences = new List<ConversationReference>();
            foreach (RoutingDataEntity entity in entities)
            {
                var conversationReference =
                    JsonConvert.DeserializeObject<ConversationReference>(entity.Body);
                conversationReferences.Add(conversationReference);
            }
            return conversationReferences;
        }


        private async Task<IList<RoutingDataEntity>> GetAllEntitiesFromTable(CloudTable table)
        {
            _logger.Enter();

            var query = new TableQuery<RoutingDataEntity>()
                .Where(TableQuery.GenerateFilterCondition(
                    "PartitionKey", QueryComparisons.Equal, DefaultPartitionKey));
            return await table.ExecuteTableQueryAsync(query);
        }


        private static bool InsertEntityToTable(string rowKey, string body, CloudTable table)
        {            
            return AzureStorageHelper.InsertAsync<RoutingDataEntity>(table, new RoutingDataEntity()
                {
                    Body = body, PartitionKey = DefaultPartitionKey, RowKey = rowKey
                }).Result;
        }

        #endregion
    }
}