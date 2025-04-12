# Basic
Do exactly what I ask. You are assisting, do not break current feature without being asked.
Just suggest if you think about better refactoring.

# Project Structure
- The project follows a clean architecture pattern with Event Sourcing as the core persistence mechanism.
- Domain models are organized into aggregates, each with its own events, commands, and projections.
- The frontend is built with Blazor, following a component-based architecture.

# Execute AppHost to open aspire and run all projects
cd /Users/tomohisa/dev/test/EsStudy0314/EsCQRSQuestions && dotnet run --project EsCQRSQuestions.AppHost/EsCQRSQuestions.AppHost.csproj --launch-profile https

# Kill program
pkill -9 -f "EsCQRSQuestions" && pkill -9 -f "dotnet" && pkill -9 -f "dcp"

# Endpoints
- **User Frontend**: https://localhost:7201
  - Participant page: https://localhost:7201/questionair

- **Admin Frontend**: https://localhost:7141
  - Admin page: https://localhost:7141/planning

# how to use ResultBox

.clinerules/ResultBox-LLM.md

# how to use Sekiban
.clinerules/Sekiban.Instructions.md