# Azure DevOps Custom Report

A .NET application for generating custom reports from Azure DevOps work items.

## Features

- Retrieve all work items from an Azure DevOps project
- Retrieve work items by a specific query
- Generate custom reports based on work item data

## Getting Started

### Prerequisites

- .NET 8.0 SDK or later
- Azure DevOps account with a Personal Access Token (PAT)

### Configuration

#### Option 1: Using User Secrets (Recommended for Development)

This project uses the .NET User Secrets manager to store sensitive information securely during development.

To set up your secrets:

```bash
cd AdoReport.WorkerApp
dotnet user-secrets set "AzureDevOps:Organization" "your-organization"
dotnet user-secrets set "AzureDevOps:Project" "your-project"
dotnet user-secrets set "AzureDevOps:PersonalAccessToken" "your-pat-token"
```

To list all secrets:

```bash
dotnet user-secrets list
```

#### Option 2: Using appsettings.json (Not recommended for sensitive data)

Update the `appsettings.json` file with your Azure DevOps information:

```json
{
  "AzureDevOps": {
    "Organization": "your-organization",
    "Project": "your-project",
    "PersonalAccessToken": "your-pat-token"
  }
}
```

### Running the Application

```bash
cd AdoReport.WorkerApp
dotnet run
```

## Code Style Standards

This project uses EditorConfig, Directory.Build.props to enforce consistent code style across the codebase.

### EditorConfig

The `.editorconfig` file defines coding styles like indentation, line endings, and spacing. It's supported by most modern IDEs.

### Directory.Build.props

The `Directory.Build.props` file sets common project properties like nullable reference types, implicit usings, and code analysis settings.

### Key Conventions

- Use 4 spaces for indentation
- Use PascalCase for public members and types
- Use camelCase with underscore prefix for private fields
- Include XML documentation for public APIs
- Follow the dependency inversion principle with interfaces in the Abstractions folder

## Project Structure

- **AdoReport.WorkerApp**: Worker service application
  - **Services**: Contains service implementations
    - **Abstractions**: Contains service interfaces
  - **Models**: Contains data models

## License

This project is licensed under the MIT License - see the LICENSE file for details.

## TODO list
[TODO]
[ ] Migrate views
[ ] Track changes in Superset