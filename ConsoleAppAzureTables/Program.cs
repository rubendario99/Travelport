using System;
using System.Threading.Tasks;
using TravelportAzureClassLibrary;
using System.Net.NetworkInformation;
using Azure;
using Azure.Data.Tables;
using Microsoft.Extensions.Configuration.Json;
using Microsoft.Extensions.Configuration;

class Program
{
    /// <summary>
    /// Main entry point of the program.
    /// </summary>
    /// <param name="args">Command line arguments.</param>
    static async Task Main(string[] args)
    {
        //Check if we have internet connection before proceeding
        if (!IsInternetAvailable())
        {
            Console.WriteLine("No Internet connection available. Please check your connection and try again.");
            return;
        }

        // Configure the configuration builder
        var configuration = new ConfigurationBuilder()
        .SetBasePath(Directory.GetCurrentDirectory())
        .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
        .Build();

        // Retrieve the connection string and the path to the JSON file
        string connectionString = configuration.GetConnectionString("AzureTableStorage");
        if (string.IsNullOrEmpty(connectionString))
        {
            throw new InvalidOperationException("Connection string is not configured.");
        }

        string jsonFilePathSetting = configuration["JsonFilePath"];
        if (string.IsNullOrEmpty(jsonFilePathSetting))
        {
            throw new InvalidOperationException("JsonFilePath setting is not configured.");
        }

        var jsonFilePath = Path.Combine(Directory.GetCurrentDirectory(), jsonFilePathSetting);
        var tableName = "TestSubscribers";

        // Initialize the Azure storage service
        var tableStorageService = new AzureTableStorageService(connectionString, tableName);

        // Create the table if it doesn't exist
        bool tableCreated = await tableStorageService.CreateTableIfNotExistsAsync(tableName);
        if (!tableCreated)
        {
            //If table cannot be created, stop
            return;
        }

        // Retrieve the setting to determine if sample data should be loaded
        if (!bool.TryParse(configuration["LoadSampleData"], out bool loadSampleData))
        {
            loadSampleData = false; // O el valor predeterminado que prefieras
        }

        // Import data from the JSON file if LoadSampleData is true
        if (loadSampleData)
        {
            await tableStorageService.ImportFromJsonAsync(jsonFilePath, tableName);
        }

        try
        {
            //UI
            bool exit = false;
            while (!exit)
            {
                Console.Clear();
                Console.WriteLine("Choose an option:");
                Console.WriteLine("1. Search records");
                Console.WriteLine("2. Update record");
                Console.WriteLine("3. Delete all records");
                Console.WriteLine("4. Delete all records by RowKey and/or PartitionKey or specific record");
                Console.WriteLine("5. Exit");
                Console.WriteLine();
                string option = GetInput("");

                switch (option)
                {
                    case "1":
                        await SearchRecords(tableStorageService);
                        break;
                    case "2":
                        await UpdateRecord(tableStorageService);
                        break;
                    case "3":
                        await DeleteAllRecords(tableStorageService);
                        break;
                    case "4":
                        await DeleteRecordsByRowKeyOrPartitionKey(tableStorageService);
                        break;
                    case "5":
                        exit = true;
                        break;
                    default:
                        Console.WriteLine("Invalid option.");
                        break;
                }

                if (!exit)
                {
                    Console.WriteLine("\nPress any key to continue...");
                    Console.ReadKey();
                }
            }
        }
        catch (Exception)
        {
            throw;
        }

        /// <summary>
        /// Handles the searching of records in Azure Table Storage based on user input.
        /// </summary>
        /// <param name="tableStorageService">The instance of AzureTableStorageService to perform operations.</param>
        static async Task SearchRecords(AzureTableStorageService? tableStorageService)
        {
            Console.WriteLine("Enter a RowKey to filter (leave blank for no filter):");
            string rowKey = Console.ReadLine();
            Console.WriteLine("Enter the beginning of the name to filter (leave blank for no filter):");
            string startName = Console.ReadLine();
            Console.WriteLine("Enter a minimum balance (leave blank for no filter):");
            string balance = Console.ReadLine();

            string filter = "PartitionKey eq 'Subscribers'";

            if (!string.IsNullOrWhiteSpace(startName))
            {
                string endName = startName + "z"; // Adjust end name for range query
                filter += $" and Name ge '{startName}' and Name lt '{endName}'";
            }

            if (!string.IsNullOrWhiteSpace(balance) && decimal.TryParse(balance, out decimal minBalance))
            {
                filter += $" and Balance ge {minBalance}";
            }

            if (!string.IsNullOrWhiteSpace(rowKey))
            {
                filter += $" and RowKey eq '{rowKey}'";
            }

            if (tableStorageService == null)
            {
                Console.WriteLine("Error: tableStorageService is not initialized.");
                return;
            }

            var results = await tableStorageService.QueryEntitiesWithFiltersAsync(filter);

            Console.WriteLine("Results found:");
            Console.WriteLine();
            foreach (var entity in results)
            {
                Console.WriteLine($"Name: {entity.Name}");
                Console.WriteLine($"Balance: {entity.Balance}");
                Console.WriteLine($"Active: {entity.IsActive}");
                Console.WriteLine($"Age: {entity.Age}");
                Console.WriteLine($"Gender: {entity.Gender}");
                Console.WriteLine($"Company: {entity.Company}");
                Console.WriteLine($"Phone: {entity.Phone}");
                Console.WriteLine($"Address: {entity.Address}");
                Console.WriteLine($"Email: {entity.Email}");
                Console.WriteLine(new string('-', 40)); // Line for separation
            }
        }

        /// <summary>
        /// Handles updating a specific record in Azure Table Storage.
        /// </summary>
        /// <param name="tableStorageService">The instance of AzureTableStorageService to perform operations.</param>
        static async Task UpdateRecord(AzureTableStorageService tableStorageService)
        {
            string partitionKey = GetInput("Enter the PartitionKey of the record to update:");
            string rowKey = GetInput("Enter the RowKey of the record to update:");

            // Check if the record exists
            string filter = $"PartitionKey eq '{partitionKey}' and RowKey eq '{rowKey}'";
            var results = await tableStorageService.QueryEntitiesWithFiltersAsync(filter);
            if (!results.Any())
            {
                Console.WriteLine("Record not found.");
                Console.WriteLine();
                return;
            }

            var recordToUpdate = results.First();

            // Display the current data
            Console.WriteLine();
            Console.WriteLine("Your about to modify:");
            Console.WriteLine($"Name: {recordToUpdate.Name}, Age: {recordToUpdate.Age}, Balance: {recordToUpdate.Balance}, Active: {recordToUpdate.IsActive}");
            Console.WriteLine(new string('-', 40)); // Line for separation
            Console.WriteLine();

            Console.WriteLine("Enter the new name (leave blank if you do not want to change it):");
            string newName = Console.ReadLine();

            string newAge = GetValidNumberInput("Enter the new age (leave blank if you do not want to change it):", true);
            string newBalance = GetValidNumberInput("Enter the new balance (leave blank if you do not want to change it):", true);
            string newActive = GetActiveInput("Is it active? (yes/no, leave blank if you do not want to change it):");

            await tableStorageService.UpdateEntityAsync(partitionKey, rowKey, entity =>
            {
                if (!string.IsNullOrEmpty(newName)) entity.Name = newName;
                if (!string.IsNullOrEmpty(newAge) && int.TryParse(newAge, out int age)) entity.Age = age;
                if (!string.IsNullOrEmpty(newBalance) && double.TryParse(newBalance, out double balance)) entity.Balance = balance;
                if (!string.IsNullOrEmpty(newActive)) entity.IsActive = newActive.ToLower() == "yes";
            });

            Console.WriteLine("Record updated successfully.");
        }

        /// <summary>
        /// Handles deletion of records in Azure Table Storage based on RowKey and/or PartitionKey.
        /// </summary>
        /// <param name="tableStorageService">The instance of AzureTableStorageService to perform operations.</param>
        static async Task DeleteRecordsByRowKeyOrPartitionKey(AzureTableStorageService tableStorageService)
        {
            string partitionKey = GetInput("Enter the PartitionKey of the records to delete (leave blank if not used):", true);
            string rowKey = GetInput("Enter the RowKey of the records to delete (leave blank if not used):", true);

            //Verify if both field are empty
            if (string.IsNullOrWhiteSpace(partitionKey) && string.IsNullOrWhiteSpace(rowKey))
            {
                Console.WriteLine("No PartitionKey or RowKey provided. Operation canceled.");
                return;
            }

            string filter = "";
            if (!string.IsNullOrWhiteSpace(partitionKey))
            {
                filter += $"PartitionKey eq '{partitionKey}'";
            }
            if (!string.IsNullOrWhiteSpace(rowKey))
            {
                if (!string.IsNullOrWhiteSpace(filter)) filter += " and ";
                filter += $"RowKey eq '{rowKey}'";
            }

            var results = await tableStorageService.QueryEntitiesWithFiltersAsync(filter);
            if (!results.Any())
            {
                Console.WriteLine("No records found with the specified keys.");
                return;
            }

            string confirmation = GetInput("Are you sure you want to delete the selected records? (yes/no)");
            if (confirmation.ToLower() == "yes")
            {
                foreach (var entity in results)
                {
                    await tableStorageService.DeleteEntityAsync(entity.PartitionKey, entity.RowKey);
                }
                Console.WriteLine("Records deleted.");
            }
            else
            {
                Console.WriteLine("Operation canceled.");
            }
        }

        /// <summary>
        /// Handles deletion of all records in Azure Table Storage.
        /// </summary>
        /// <param name="tableStorageService">The instance of AzureTableStorageService to perform operations.</param>
        static async Task DeleteAllRecords(AzureTableStorageService tableStorageService)
        {
            string confirmation = GetInput("Are you sure you want to delete ALL records? (yes/no)");
            if (confirmation.ToLower() == "yes")
            {
                await tableStorageService.DeleteAllEntitiesAsync();
                Console.WriteLine("All records have been deleted.");
            }
            else
            {
                Console.WriteLine("Operation canceled.");
                Console.WriteLine();
            }
        }

        /// <summary>
        /// Prompts the user for a numeric input and validates it.
        /// </summary>
        /// <param name="prompt">The prompt message displayed to the user.</param>
        /// <param name="isInt">Indicates if the expected input is an integer.</param>
        /// <returns>The valid numeric input as a string.</returns>
        static string GetValidNumberInput(string prompt, bool isInt)
        {
            while (true)
            {
                Console.WriteLine(prompt);
                string input = Console.ReadLine();

                //Allows field to be null
                if (string.IsNullOrEmpty(input))
                {
                    return input;
                }

                // Check if is valid number
                bool isValidNumber = isInt ? int.TryParse(input, out _) : double.TryParse(input, out _);

                if (isValidNumber)
                {
                    return input;
                }
                else
                {
                    Console.WriteLine("Invalid input. Please enter a valid number.");
                }
            }
        }

        /// <summary>
        /// Prompts the user for input and optionally allows empty input.
        /// </summary>
        /// <param name="message">The prompt message displayed to the user.</param>
        /// <param name="allowEmpty">Indicates whether empty input is allowed.</param>
        /// <returns>The user's input.</returns>
        static string GetInput(string message, bool allowEmpty = false)
        {
            string input;
            do
            {
                Console.WriteLine(message);
                input = Console.ReadLine();
                if (!allowEmpty && string.IsNullOrWhiteSpace(input))
                {
                    Console.WriteLine("You did not enter any value. Please try again.");
                }
            } while (!allowEmpty && string.IsNullOrWhiteSpace(input));

            return input;
        }

        /// <summary>
        /// Prompts the user for a 'yes', 'no', or blank response.
        /// </summary>
        /// <param name="prompt">The prompt message displayed to the user.</param>
        /// <returns>The user's response as 'yes', 'no', or blank.</returns>
        static string GetActiveInput(string prompt)
        {
            while (true)
            {
                Console.WriteLine(prompt);
                string input = Console.ReadLine().Trim().ToLower();

                if (string.IsNullOrEmpty(input) || input == "yes" || input == "no")
                {
                    return input;
                }
                else
                {
                    Console.WriteLine("Invalid input. Please enter 'yes', 'no', or leave blank.");
                }
            }
        }

        /// <summary>
        /// Checks for internet connectivity.
        /// </summary>
        /// <returns>True if internet is available; otherwise, false.</returns>
        static bool IsInternetAvailable()
        {
            try
            {
                using (var client = new System.Net.WebClient())
                using (client.OpenRead("http://clients3.google.com/generate_204"))
                {
                    return true;
                }
            }
            catch
            {
                return false;
            }
        }
    }
}
