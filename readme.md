# Travelport Azure Table Storage Application

This repository contains a .NET application designed to interact with Azure Table Storage. It includes a class library that handles the CRUD operations for entities within Azure Table Storage and a console application to test these functionalities.

## Projects

- `TravelportAzureClassLibrary`: Contains the access and logic for interacting with Azure Table Storage.
- `ConsoleAppAzureTables`: A console application that utilizes the class library to perform operations against Azure Table Storage.

## Features

- Create and manage Azure Table Storage entities.
- Query entities with filters.
- Import data from JSON files to Azure Table Storage.
- Update and delete entities based on PartitionKey and RowKey.

## Getting Started

To run these projects, you will need to:

1. Have .NET 6.0 SDK installed.
2. Have access to an Azure subscription and Azure Storage Account.
3. Update the `appsettings.json` with your Azure Table Storage connection string.
    AzureTableStorage: Your Azure Table Storage connection string.
    JsonFilePath: Path to your JSON file containing the sample data to import.
    LoadSampleData: A boolean flag to determine if the sample data should be loaded on startup.

### Running the Console Application

Navigate to the directory containing the `ConsoleAppAzureTables.csproj` and execute:

```bash
dotnet run

Or you can open the TravelportAzureClassLibrary.sln and work from VS

