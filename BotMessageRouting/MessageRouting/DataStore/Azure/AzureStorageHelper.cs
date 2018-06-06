using BotMessageRouting.MessageRouting.Handlers;
using BotMessageRouting.MessageRouting.Logging;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Table;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Underscore.Bot.MessageRouting.DataStore.Azure
{
    /// <summary>
    /// Contains Azure table storage utility methods.
    /// </summary>
    public static class AzureStorageHelper
    {
        private static ILogger          _logger = DebugLogger.Default;
        private static ExceptionHandler _exceptionHandler = new ExceptionHandler(_logger);



        /// <summary>
        /// Allows you to override the default built-in logger and replace it with your own ILogger implementation
        /// </summary>
        /// <param name="logger">The logger to use instead of the built-in one</param>
        public static void SetLogger(ILogger logger)
        {
            if (logger != null)
            {
                _logger           = logger;
                _exceptionHandler = new ExceptionHandler(_logger);
            }
        }


        /// <summary>
        /// Tries to resolve a table in the storage defined by the given connection string and table name.
        /// </summary>
        /// <param name="connectionString">The Azure storage connection string.</param>
        /// <param name="tableName">The name of the table to resolve and return.</param>
        /// <returns>The resolved table or null in case of an error.</returns>
        /// <exception cref="ArgumentException"></exception>
        /// <exception cref="ArgumentNullException"></exception>
        /// <exception cref="FormatException"></exception>
        public static CloudTable GetTable(string connectionString, string tableName)
        {
            _logger.Enter();
            CloudStorageAccount cloudStorageAccount = null;

            var storageAccount = _exceptionHandler.Get(
                    () => CloudStorageAccount.Parse(connectionString),
                    returnDefaultType: false
                );

            CloudTableClient cloudTableClient = cloudStorageAccount?.CreateCloudTableClient();
            return cloudTableClient?.GetTableReference(tableName);
        }


        /// <summary>
        /// Tries to insert the given entry into the given table.
        /// </summary>
        /// <typeparam name="T">TableEntity derivative.</typeparam>
        /// <param name="cloudTable">The destination table.</param>
        /// <param name="entryToInsert">The entry to insert into the table.</param>
        /// <returns>True, if the given entry was inserted successfully. False otherwise.</returns>
        public static async Task<bool> InsertAsync<T>(CloudTable cloudTable, T entryToInsert) where T : ITableEntity
        {
            _logger.Enter();
            var insertOperation = TableOperation.Insert(entryToInsert);

            var insertResult = await _exceptionHandler.GetAsync(
                    unsafeFunction: ( ) => cloudTable.ExecuteAsync(insertOperation),
                    customHandler : (e) => _logger.LogException(e, $"Failed to insert the given entity into table '{cloudTable.Name}'")
                );

            return (insertResult?.Result != null);
        }


        /// <summary>
        /// Deletes an entry from the given table by the given partition and row key.
        /// </summary>
        /// <typeparam name="T">TableEntity derivative.</typeparam>
        /// <param name="partitionKey">The partition key.</param>
        /// <param name="rowKey">The row key.</param>
        /// <returns>True, if an entry (entity) was deleted. False otherwise.</returns>
        public static async Task<bool> DeleteEntryAsync<T>(CloudTable cloudTable, string partitionKey, string rowKey) where T : ITableEntity
        {
            _logger.Enter();
            var retrieveOperation = TableOperation.Retrieve<T>(partitionKey, rowKey);
            var retrieveResult    = await _exceptionHandler.GetAsync(() => cloudTable.ExecuteAsync(retrieveOperation));

            if (retrieveResult.Result is T entityToDelete)
            {
                TableOperation deleteOperation = TableOperation.Delete(entityToDelete);
                await _exceptionHandler.ExecuteAsync(() => cloudTable.ExecuteAsync(deleteOperation));
                return true;
            }
            return false;
        }


        /// <summary>
        /// 
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="cloudTable"></param>
        /// <param name="tableQuery"></param>
        /// <param name="cancellationToken"></param>
        /// <param name="onProgress"></param>
        /// <returns></returns>
        public static async Task<IList<T>> ExecuteTableQueryAsync<T>(this CloudTable cloudTable, TableQuery<T> tableQuery, CancellationToken cancellationToken = default(CancellationToken), Action<IList<T>> onProgress = null) where T : ITableEntity, new()
        {
            _logger.Enter();
            var items = new List<T>();
            TableContinuationToken tableContinuationToken = null;

            do
            {
                bool failed = false;
                TableQuerySegment<T> tableQuerySegment = await _exceptionHandler.GetAsync(
                    unsafeFunction: () => cloudTable.ExecuteQuerySegmentedAsync<T>(tableQuery, tableContinuationToken),
                    customHandler: (e) => failed = true
                    );

                if (failed)
                    return items;

                tableContinuationToken = tableQuerySegment.ContinuationToken;
                items.AddRange(tableQuerySegment);
                onProgress?.Invoke(items);
            }
            while (tableContinuationToken != null && !cancellationToken.IsCancellationRequested);

            return items;
        }
    }
}
