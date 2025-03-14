# Product Context: EsCQRSQuestions

## Why This Project Exists
EsCQRSQuestions was created to demonstrate the power of event sourcing and CQRS patterns in a real-time, interactive application. It serves as both a practical tool for gathering feedback in real-time settings (such as meetings, conferences, or classrooms) and as a technical showcase for implementing event sourcing with the Sekiban framework.

## Problems It Solves

### For Event Organizers and Presenters
1. **Real-time Audience Engagement**: Allows presenters to gauge audience understanding and opinions instantly
2. **Interactive Presentations**: Transforms one-way presentations into interactive discussions
3. **Immediate Feedback Collection**: Gathers responses as they happen, rather than after the fact
4. **Response Visualization**: Shows aggregated responses in real-time to help guide discussions

### For Participants
1. **Active Participation**: Provides a simple way to contribute to discussions
2. **Anonymity Option**: Allows participants to respond without identifying themselves if desired
3. **Visibility of Group Opinions**: Shows how their responses compare to the group

### For Developers
1. **Event Sourcing Implementation**: Demonstrates a practical application of event sourcing
2. **CQRS Pattern Usage**: Shows how to separate command and query responsibilities
3. **Real-time Updates with SignalR**: Provides an example of implementing real-time features
4. **Blazor Frontend Integration**: Shows how to build a modern web UI with Blazor

## How It Should Work

### Administrator Flow
1. Administrator accesses the hidden /planning page
2. Creates questions with multiple-choice options
3. Edits questions as needed before they are displayed
4. Controls when questions are displayed to participants using Start/Stop Display
5. Views responses in real-time as they are submitted
6. Can analyze the aggregated results

### Participant Flow
1. Participant accesses the /questionair page
2. Optionally enters their name
3. Sees a welcome screen when no questions are active
4. When a question is activated, sees the question and response options
5. Selects an answer and optionally adds a comment
6. Submits their response
7. Sees other participants' responses
8. Returns to the welcome screen when the question is deactivated

### System Flow
1. Administrator creates or updates a question (Command)
2. System stores this as an event in the event store
3. Administrator activates a question (Command)
4. System broadcasts this via SignalR to all connected clients
5. Participants submit responses (Commands)
6. Each response is stored as an event
7. System broadcasts new responses to all clients
8. Administrator deactivates the question (Command)
9. System broadcasts this to all clients

## User Experience Goals

### For Administrators
- **Simplicity**: Easy question creation and management
- **Control**: Clear mechanisms for controlling question display
- **Visibility**: Real-time view of participant responses
- **Insights**: Useful aggregation of response data

### For Participants
- **Clarity**: Clear presentation of questions and response options
- **Ease of Use**: Simple, intuitive interface for submitting responses
- **Feedback**: Immediate confirmation of response submission
- **Transparency**: Ability to see how their response compares to others

### Overall Experience
- **Responsiveness**: The application should feel immediate and responsive
- **Reliability**: Responses should never be lost, even in case of connection issues
- **Consistency**: The UI should be consistent across different devices
- **Accessibility**: The application should be usable by people with different abilities
