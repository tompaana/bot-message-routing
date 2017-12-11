using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Table;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Threading.Tasks;
using System.Web;

namespace Underscore.Bot.Utils
{
    public class StorageHelper
    {
        public static CloudStorageAccount CreateStorageAccountFromConnectionString()
        {
            CloudStorageAccount storageAccount;
            const string Message = "Conta do Azure Storage inválida. Por favor, verifique a string de conexão.";

            try
            {
                storageAccount = CloudStorageAccount.Parse(ConfigurationManager.AppSettings["StorageConnectionString"]);
            }
            catch (FormatException)
            {

                throw;
            }
            catch (ArgumentException)
            {

                throw;
            }

            return storageAccount;
        }

        public static CloudTable CreateStorageAccountFromConnectionString(string tableName)
        {
            CloudStorageAccount storageAccount;
            const string Message = "Conta do Azure Storage inválida. Por favor, verifique a string de conexão.";

            try
            {
                storageAccount = CloudStorageAccount.Parse(ConfigurationManager.AppSettings["StorageConnectionString"]);
                return GetTable(storageAccount, tableName);
            }
            catch (FormatException)
            {

                throw;
            }
            catch (ArgumentException)
            {

                throw;
            }

            return null;
        }

        public static CloudTable GetTable (CloudStorageAccount storageAccount, string tableName)
        {
            CloudTableClient tableClient = storageAccount.CreateCloudTableClient();
            return tableClient.GetTableReference(tableName);
        }

        public static T Get<T>(CloudTable table, string PartitionKey, string RowKey) where T : TableEntity
        {

            // Create a retrieve operation that takes a customer entity.
            TableOperation retrieveOperation = TableOperation.Retrieve<T>(PartitionKey, RowKey);

            // Execute the retrieve operation.
            TableResult retrievedResult = table.Execute(retrieveOperation);

            T result = null;
            // Print the phone number of the result.
            if (retrievedResult.Result != null)
            {
                result = retrievedResult.Result as T;
            }
            return result;
        }

        public static void Insert<T>(CloudTable table, T data) where T : TableEntity
        {
            try
            {
                // Create the TableOperation that inserts the customer entity.
                TableOperation insertOperation = TableOperation.Insert(data);

                // Execute the insert operation.
                table.Execute(insertOperation);
            }
            catch (Exception ex)
            {

            }
        }

        public static void InsertBatch<T>(CloudTable table, List<T> data) where T : TableEntity
        {
            // Create the batch operation.
            TableBatchOperation batchOperation = new TableBatchOperation();

            // Add both customer entities to the batch insert operation.
            foreach (TableEntity d in data)
            {
                batchOperation.Insert(d);
            }

            // Execute the batch operation.
            table.ExecuteBatch(batchOperation);
        }

        public static void Replace<T>(CloudTable table, string PartitionKey, string RowKey,
               T ReplacementData, Boolean InsertOrReplace) where T : TableEntity
        {
            // Create a retrieve operation that takes a customer entity.
            TableOperation retrieveOperation = TableOperation.Retrieve<T>(PartitionKey, RowKey);

            // Execute the operation.
            TableResult retrievedResult = table.Execute(retrieveOperation);

            // Assign the result to a CustomerEntity object.
            T updateEntity = retrievedResult.Result as T;

            if (updateEntity != null)
            {
                ReplacementData.PartitionKey = updateEntity.PartitionKey;
                ReplacementData.RowKey = updateEntity.RowKey;

                // Create the InsertOrReplace TableOperation
                TableOperation updateOperation;
                if (InsertOrReplace)
                {
                    updateOperation = TableOperation.InsertOrReplace(ReplacementData);
                }
                else
                {
                    updateOperation = TableOperation.Replace(ReplacementData);
                }

                // Execute the operation.
                table.Execute(updateOperation);
            }
        }

        /// <summary>
        /// Deletes the entry.
        /// </summary>
        /// <typeparam name="T">DTO that inherits from TableEntity</typeparam>
        /// <param name="PartitionKey">The partition key.</param>
        /// <param name="RowKey">The row key.</param>
        /// <param name="ReplacementData">The replacement data.</param>
        public static bool DeleteEntry<T>(CloudTable table, string pkey, string rkey) where T : TableEntity
        {

            // Create a retrieve operation that expects a customer entity.
            TableOperation retrieveOperation = TableOperation.Retrieve<T>(pkey, rkey);

            // Execute the operation.
            TableResult retrievedResult = table.Execute(retrieveOperation);

            // Assign the result to a CustomerEntity.
            T deleteEntity = retrievedResult.Result as T;

            // Create the Delete TableOperation.
            if (deleteEntity != null)
            {
                TableOperation deleteOperation = TableOperation.Delete(deleteEntity);

                // Execute the operation.
                table.Execute(deleteOperation);
                return true;
            }

            return false;

        }

        /// <summary>
        /// Deletes the table.
        /// </summary>
        public static void DeleteTable(CloudTable table)
        {
            // Delete the table it if exists.
            table.DeleteIfExists();
        }
    }
}