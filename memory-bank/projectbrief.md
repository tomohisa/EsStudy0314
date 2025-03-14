# Project Brief: EsCQRSQuestions

## Project Overview
EsCQRSQuestions is a real-time survey application built using event sourcing principles. The application allows administrators to create surveys with questions and multiple-choice answers, and participants to respond to these questions in real-time. The application uses Sekiban and C# for the backend, implementing CQRS (Command Query Responsibility Segregation) and event sourcing patterns, with Blazor for the frontend.

## Core Requirements

### Administrator Features
- Create and edit questions with multiple-choice options
- Start and stop displaying questions to participants
- View real-time statistics and responses from participants
- Access a dedicated admin page (/planning) that is not visible in the navigation bar

### Participant Features
- Enter their name (optional)
- View active questions when they are displayed by the administrator
- Select from multiple-choice options and provide optional comments
- Submit responses that are immediately visible to the administrator
- See other participants' responses in real-time

### Technical Requirements
- Implement event sourcing using Sekiban framework
- Each question is treated as a single aggregate
- Use SignalR for real-time communication between server and clients
- Develop a Blazor frontend with two main pages:
  - /planning (admin page, hidden from navigation)
  - /questionair (participant page)

## Project Goals
1. Create a functional real-time survey application
2. Demonstrate effective use of event sourcing and CQRS patterns
3. Implement real-time updates using SignalR
4. Provide a clean, intuitive user interface for both administrators and participants
5. Ensure the application is responsive and performs well under load

## Constraints
- The application must use Sekiban for event sourcing
- The frontend must be built with Blazor
- The application must follow the sample structure provided in the MessageEachOther example
