# Technical Context: EsCQRSQuestions

## Technologies Used

### Backend
- **.NET 8**: The latest version of the .NET platform, providing performance improvements and new features
- **C#**: The primary programming language for backend development
- **ASP.NET Core**: Web framework for building APIs and web applications
- **Sekiban**: Event sourcing framework for .NET, providing infrastructure for CQRS and event sourcing
- **SignalR**: Real-time communication library for pushing updates to connected clients
- **Entity Framework Core**: ORM for database access (used by Sekiban for some storage options)
- **System.Text.Json**: JSON serialization/deserialization

### Frontend
- **Blazor**: Web framework for building interactive web UIs using C# instead of JavaScript
- **ASP.NET Core**: Hosting platform for the Blazor application
- **SignalR Client**: Client library for real-time communication with the server
- **Bootstrap**: CSS framework for responsive design
- **Chart.js**: JavaScript library for data visualization (used for displaying survey results)

### Storage
- **In-memory storage**: For development and testing
- **Azure Cosmos DB**: For production (supported by Sekiban)
- **PostgreSQL**: Alternative storage option (supported by Sekiban)

### Development Tools
- **Visual Studio 2022**: Primary IDE for development
- **Visual Studio Code**: Alternative lightweight editor
- **Git**: Version control system
- **GitHub**: Repository hosting and collaboration
- **Docker**: Containerization for consistent development and deployment environments

## Development Setup

### Prerequisites
- .NET 8 SDK
- Visual Studio 2022 or Visual Studio Code
- Git
- Docker (optional, for containerized development)

### Project Structure
The solution follows a standard .NET solution structure with multiple projects:

```
EsCQRSQuestions/
├── EsCQRSQuestions.sln
├── src/
│   ├── EsCQRSQuestions.ApiService/      # API and SignalR hub
│   ├── EsCQRSQuestions.AppHost/         # Application host for .NET Aspire
│   ├── EsCQRSQuestions.Domain/          # Domain models, events, and commands
│   ├── EsCQRSQuestions.ServiceDefaults/ # Service default configurations
│   └── EsCQRSQuestions.Web/             # Blazor web application
└── tests/
    ├── EsCQRSQuestions.Domain.Tests/    # Unit tests for domain logic
    └── EsCQRSQuestions.Web.Tests/       # UI tests for web application
```

### Local Development Workflow
1. Clone the repository
2. Open the solution in Visual Studio or VS Code
3. Restore NuGet packages
4. Build the solution
5. Run the application using the AppHost project
6. Access the web application at https://localhost:5001
7. Access the API at https://localhost:5000

### Configuration
The application uses the standard ASP.NET Core configuration system, with settings in:
- `appsettings.json`: Base configuration
- `appsettings.Development.json`: Development-specific overrides
- Environment variables: Runtime configuration
- User secrets: Local development secrets

## Technical Constraints

### Sekiban Framework
- The application must use Sekiban for event sourcing
- Events must follow Sekiban's conventions and patterns
- Aggregates must be designed according to Sekiban's requirements
- Storage options are limited to those supported by Sekiban

### Blazor
- The frontend must be built with Blazor
- UI components should be reusable and follow Blazor conventions
- State management should use Blazor's built-in mechanisms

### SignalR
- Real-time communication must use SignalR
- SignalR hub methods must be designed for the specific needs of the application
- Connection management must handle reconnection scenarios

### Performance
- The application should handle multiple concurrent users
- Event sourcing should be optimized to avoid performance issues with large event streams
- SignalR connections should be managed efficiently

## Dependencies

### NuGet Packages
- `Sekiban.Core`: Core Sekiban framework
- `Sekiban.Web`: Web integration for Sekiban
- `Sekiban.Testing`: Testing utilities for Sekiban
- `Microsoft.AspNetCore.SignalR`: SignalR server library
- `Microsoft.AspNetCore.SignalR.Client`: SignalR client library
- `Microsoft.AspNetCore.Components`: Blazor components
- `Microsoft.AspNetCore.Components.Web`: Blazor web components
- `Microsoft.AspNetCore.Components.WebAssembly`: Blazor WebAssembly hosting
- `Microsoft.EntityFrameworkCore`: Entity Framework Core
- `Microsoft.Extensions.DependencyInjection`: Dependency injection
- `Microsoft.Extensions.Logging`: Logging infrastructure
- `System.Text.Json`: JSON handling

### External Services
- **Azure Cosmos DB**: For event storage in production
- **Azure App Service**: For hosting the application
- **Azure SignalR Service**: For scaling SignalR in production (optional)

## Development Patterns

### Event Sourcing with Sekiban
Sekiban provides a framework for implementing event sourcing in .NET applications. Key components include:

- **Aggregates**: Domain objects that encapsulate state and behavior
- **Events**: Immutable records of state changes
- **Commands**: Requests to change state
- **Projections**: Transform events into read models
- **Event Store**: Persistent storage for events

### SignalR Integration
SignalR is used for real-time communication between the server and clients. Key patterns include:

- **Hub**: Central communication point for clients
- **Groups**: Logical grouping of clients for targeted messages
- **Connections**: Individual client connections
- **Streaming**: Continuous data flow for real-time updates

### Blazor Component Architecture
The frontend is built using Blazor components with a hierarchical structure:

- **Layouts**: Define the overall page structure
- **Pages**: Represent different routes in the application
- **Components**: Reusable UI elements
- **Services**: Shared functionality and state management

## Reference Implementation
The application is based on the MessageEachOther sample, which demonstrates:

- SignalR integration with Sekiban
- Real-time updates in a Blazor application
- Event sourcing patterns for a simple domain

Key reference files:
- `Sample/MessageEachOther/MessageEachOther.ApiService/Program.cs`: Backend setup
- `Sample/MessageEachOther/MessageEachOther.Web/Components/Pages/Weather.razor`: Frontend implementation
