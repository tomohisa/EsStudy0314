# Unique URL Implementation Design - Task 016_unique2

## Overview
This design document outlines the implementation plan for creating unique questionnaire URLs in the admin interface. The goal is to display a clickable link in the admin interface that points to a specific questionnaire using its unique code in the format `https://localhost:7201/questionair/{UniqueCode}`.

## Current Status
Based on investigation of the codebase:

1. The admin interface already displays a unique code for each question group
2. Configuration for `ClientBaseUrl` needs to be added to the appsettings.Development.json file
3. The link functionality needs to be implemented in the GroupQuestionsList component

## Implementation Steps

### 1. Update appsettings.Development.json
Add the `ClientBaseUrl` setting to the configuration file:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "ClientBaseUrl": "https://localhost:7201"
}
```

### 2. Create Configuration Access
Create a service or inject the configuration directly to access the `ClientBaseUrl` setting:

- Create a simple configuration class to hold the client URL settings
- Register this service in Program.cs
- Use dependency injection to access this configuration in components

```csharp
// New file: ClientUrlOptions.cs
public class ClientUrlOptions
{
    public string BaseUrl { get; set; } = string.Empty;
}

// In Program.cs
builder.Services.Configure<ClientUrlOptions>(builder.Configuration.GetSection("ClientBaseUrl"));
// Or simple approach:
builder.Services.AddSingleton(services => new ClientUrlOptions { 
    BaseUrl = builder.Configuration["ClientBaseUrl"] ?? "https://localhost:7201" 
});
```

### 3. Modify GroupQuestionsList Component

Update the GroupQuestionsList.razor component to:
1. Inject the configuration/service to get the ClientBaseUrl
2. Add a clickable link that combines the base URL with the unique code
3. Style the link appropriately and add copy functionality if desired

```html
<div class="mb-4">
    <div class="d-flex justify-content-between align-items-center">
        <div>
            <h2>Questions in Group: @GroupName</h2>
            <p class="text-muted">
                Unique Code: @UniqueCode
                @if(!string.IsNullOrEmpty(UniqueCode)) {
                    <a href="@QuestionnaireUrl" target="_blank" class="ms-2 btn btn-sm btn-outline-primary">
                        <i class="bi bi-link"></i> Open Link
                    </a>
                    <button class="btn btn-sm btn-outline-secondary ms-1" @onclick="CopyLinkToClipboard">
                        <i class="bi bi-clipboard"></i> Copy
                    </button>
                }
            </p>
        </div>
        <!-- Existing buttons -->
    </div>
</div>

@code {
    [Inject]
    private ClientUrlOptions ClientUrls { get; set; } = default!;
    
    [Inject]
    private IJSRuntime JsRuntime { get; set; } = default!;
    
    private string QuestionnaireUrl => $"{ClientUrls.BaseUrl}/questionair/{UniqueCode}";
    
    private async Task CopyLinkToClipboard()
    {
        await JsRuntime.InvokeVoidAsync("navigator.clipboard.writeText", QuestionnaireUrl);
    }
    
    // Existing code...
}
```

### 4. Testing Plan
1. Verify that the `ClientBaseUrl` setting is correctly read from appsettings.Development.json
2. Ensure the link is correctly displayed in the admin UI
3. Test that clicking the link opens the correct URL in a new tab
4. Test the copy functionality works as expected
5. Verify the link format is correct: `https://localhost:7201/questionair/{UniqueCode}`

## Notes
- This implementation doesn't require any changes to the Web project as mentioned in the requirements
- The URL handling on the Web project side is assumed to be already implemented or will be handled in a separate task
- For production, the `ClientBaseUrl` setting should be configured appropriately in the deployment environment

## Future Enhancements
- Add visual feedback when the link is copied
- Implement URL validation to ensure the client URL is properly formatted
- Add localization support for UI text elements