using Azure;
using Azure.Data.Tables;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

//Would have been better to separate DAL from BLL, and use DI 

namespace TravelportAzureClassLibrary
{
    public class AzureTableStorageService
    {
        public class SubscriberEntity : ITableEntity
        {
            public string? PartitionKey { get; set; }
            public string? RowKey { get; set; }
            public bool IsActive { get; set; }
            [JsonConverter(typeof(JsonBalanceConverter))]
            public double Balance { get; set; }
            public int Age { get; set; }
            public string? Name { get; set; }
            public string? Gender { get; set; }
            public string? Company { get; set; }
            public string? Phone { get; set; }
            public string? Address { get; set; }
            public string? About { get; set; }
            public string? Email { get; set; }
            public DateTimeOffset? Timestamp { get; set; }
            public ETag ETag { get; set; }
        }

        private readonly TableServiceClient? _serviceClient;
        private TableClient? _tableClient;

        /// <summary>
        /// Initializes a new instance of the AzureTableStorageService class with a specified connection string and table name.
        /// </summary>
        /// <param name="connectionString">The connection string to the Azure Table Storage account.</param>
        /// <param name="tableName">The name of the table to be used or created.</param>
        public AzureTableStorageService(string connectionString, string tableName)
        {
            _serviceClient = new TableServiceClient(connectionString);
            _tableClient = _serviceClient.GetTableClient(tableName);
        }

        /// <summary>
        /// Initializes a new instance of the AzureTableStorageService class with Azure account name, account key and table name.
        /// </summary>
        /// <param name="accountName">Azure Storage account name.</param>
        /// <param name="accountKey">Azure Storage account key.</param>
        /// <param name="tableName">The name of the table to be used or created.</param>
        public AzureTableStorageService(string accountName, string accountKey, string tableName)
        {
            string connectionString = $"DefaultEndpointsProtocol=https;AccountName={accountName};AccountKey={accountKey};EndpointSuffix=core.windows.net";
            _serviceClient = new TableServiceClient(connectionString);
            _tableClient = _serviceClient.GetTableClient(tableName);
        }

        /// <summary>
        /// Creates an Azure Table if it does not already exist.
        /// </summary>
        /// <param name="tableName">The name of the table to create.</param>
        /// <returns>
        /// True if the table was created or already exists, false if the table is being deleted or other
        /// </returns>
        /// <exception cref="RequestFailedException">Thrown when an Azure request fails.</exception>
        public async Task<bool> CreateTableIfNotExistsAsync(string tableName)
        {
            var tableClient = _serviceClient.GetTableClient(tableName);
            try
            {
                await tableClient.CreateIfNotExistsAsync();
                return true;
            }
            catch (RequestFailedException ex) when (ex.Status == 409)
            {
                Console.WriteLine("The table is currently being deleted. Please try again later.");
                return false;
            }
            catch
            {
                throw;
            }
        }

        /// <summary>
        /// Initializes the TableClient for the specified table.
        /// </summary>
        /// <param name="tableName">Name of the table to initialize the client for.</param>
        public void InitializeTableClient(string tableName)
        {
            _tableClient = _serviceClient.GetTableClient(tableName);
        }

        /// <summary>
        /// Imports data from a JSON file into the Azure Table Storage.
        /// </summary>
        /// <param name="jsonFilePath">The path to the JSON file containing the data to import.</param>
        /// <param name="tableName">The name of the table where the data will be imported.</param>
        /// <exception cref="JsonException">Thrown when there is an issue with JSON parsing.</exception>
        /// <exception cref="Exception">General exceptions during the import process.</exception>
        public async Task ImportFromJsonAsync(string jsonFilePath, string tableName)
        {
            InitializeTableClient(tableName);

            try
            {
                var jsonData = File.ReadAllText(jsonFilePath);
                var entities = JsonConvert.DeserializeObject<List<SubscriberEntity>>(jsonData);

                if (entities == null)
                {
                    Console.WriteLine("No entities found in the JSON file.");
                    return;
                }

                foreach (var entity in entities)
                {
                    try
                    {
                        await AddEntityAsync(entity);
                        Console.WriteLine("New record added.");
                    }
                    catch (RequestFailedException e) when (e.Status == 409) // entity already exists
                    {
                        Console.WriteLine($"The entity with PartitionKey: {entity.PartitionKey} and RowKey: {entity.RowKey} already exists.");
                    }
                }
            }
            catch (JsonException jsonEx)
            {
                // Handle JSON parsing errors
                Console.WriteLine($"There was an error parsing the JSON file: {jsonEx.Message}");
                Console.WriteLine("\nPress any key to continue...");
                Console.ReadKey();
            }
            catch (Exception ex)
            {
                // Handle other general exceptions that might occur
                Console.WriteLine($"An unexpected error occurred: {ex.Message}");
                Console.WriteLine("\nPress any key to continue...");
                Console.ReadKey();
            }
        }

        /// <summary>
        /// Adds a new entity to the Azure Table Storage.
        /// </summary>
        /// <param name="entity">The entity to add to the table.</param>
        /// <exception cref="InvalidOperationException">Thrown if the TableClient is not initialized.</exception>
        public async Task AddEntityAsync(SubscriberEntity entity)
        {
            if (_tableClient == null)
            {
                throw new InvalidOperationException("TableClient is not initialized.");
            }

            await _tableClient.AddEntityAsync(entity);
        }

        /// <summary>
        /// Queries entities from Azure Table Storage with specified filters.
        /// </summary>
        /// <param name="filter">The OData filter string.</param>
        /// <returns>A list of entities matching the filter criteria.</returns>
        public async Task<List<SubscriberEntity>> QueryEntitiesWithFiltersAsync(string filter)
        {
            AsyncPageable<SubscriberEntity> queryResults = _tableClient.QueryAsync<SubscriberEntity>(filter);
            var entities = new List<SubscriberEntity>();
            int count = 0;
            const int maxRecords = 2000;

            await foreach (var entity in queryResults)
            {
                if (count >= maxRecords)
                {
                    break;
                }

                entities.Add(entity);
                count++;
            }

            return entities;
        }

        /// <summary>
        /// Updates an existing entity in Azure Table Storage.
        /// </summary>
        /// <param name="partitionKey">The PartitionKey of the entity to update.</param>
        /// <param name="rowKey">The RowKey of the entity to update.</param>
        /// <param name="updateAction">The action containing updates to apply to the entity.</param>
        public async Task UpdateEntityAsync(string partitionKey, string rowKey, Action<SubscriberEntity> updateAction)
        {
            var response = await _tableClient.GetEntityAsync<SubscriberEntity>(partitionKey, rowKey);
            if (response != null)
            {
                var entity = response.Value;

                // Update entity
                updateAction(entity);

                // Save changes to table
                await _tableClient.UpdateEntityAsync(entity, entity.ETag, TableUpdateMode.Replace);
            }
        }

        /// <summary>
        /// Deletes an entity from Azure Table Storage.
        /// </summary>
        /// <param name="partitionKey">The PartitionKey of the entity to delete.</param>
        /// <param name="rowKey">The RowKey of the entity to delete.</param>
        public async Task DeleteEntityAsync(string partitionKey, string rowKey)
        {
            await _tableClient.DeleteEntityAsync(partitionKey, rowKey);
        }

        /// <summary>
        /// Deletes all entities in the Azure Table Storage.
        /// </summary>
        public async Task DeleteAllEntitiesAsync()
        {
            var entities = _tableClient.QueryAsync<SubscriberEntity>();
            await foreach (var entity in entities)
            {
                await _tableClient.DeleteEntityAsync(entity.PartitionKey, entity.RowKey);
            }
        }
    }
}
