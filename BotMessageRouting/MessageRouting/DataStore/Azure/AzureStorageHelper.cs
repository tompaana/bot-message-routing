using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Table;
using System;

namespace Underscore.Bot.MessageRouting.DataStore.Azure
{
    /// <summary>
    /// Contains Azure table storage utility methods.
    /// </summary>
    public class AzureStorageHelper
    {
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
            CloudStorageAccount cloudStorageAccount = null;

            try
            {
                cloudStorageAccount = CloudStorageAccount.Parse(connectionString);
            }
            catch
            {
                throw;
            }

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
        public static bool Insert<T>(CloudTable cloudTable, T entryToInsert) where T : TableEntity
        {
            TableOperation insertOperation = TableOperation.Insert(entryToInsert);
            TableResult insertResult = null;

            try
            {
                insertResult = cloudTable.Execute(insertOperation);
            }
            catch (Exception e)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to insert the given entity into the table: {e.Message}");
                return false;
            }

            return (insertResult?.Result != null);
        }

        /// <summary>
        /// Deletes an entry from the given table by the given partition and row key.
        /// </summary>
        /// <typeparam name="T">TableEntity derivative.</typeparam>
        /// <param name="partitionKey">The partition key.</param>
        /// <param name="rowKey">The row key.</param>
        /// <returns>True, if an entry (entity) was deleted. False otherwise.</returns>
        public static bool DeleteEntry<T>(CloudTable cloudTable, string partitionKey, string rowKey) where T : TableEntity
        {
            TableOperation retrieveOperation = TableOperation.Retrieve<T>(partitionKey, rowKey);
            TableResult retrieveResult = cloudTable.Execute(retrieveOperation);

            if (retrieveResult.Result is T entityToDelete)
            {
                TableOperation deleteOperation = TableOperation.Delete(entityToDelete);
                cloudTable.Execute(deleteOperation);
                return true;
            }

            return false;
        }
    }
}
