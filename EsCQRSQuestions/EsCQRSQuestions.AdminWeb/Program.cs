using EsCQRSQuestions.AdminWeb;
using EsCQRSQuestions.AdminWeb.Components;
using EsCQRSQuestions.AdminWeb.Services;
using EsCQRSQuestions.AdminWeb.Models;

var builder = WebApplication.CreateBuilder(args);

// Add service defaults & Aspire client integrations.
builder.AddServiceDefaults();

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddOutputCache();

// Register ClientUrlOptions
builder.Services.AddSingleton(services => new ClientUrlOptions 
{ 
    BaseUrl = builder.Configuration["ClientBaseUrl"] ?? "https://localhost:7201" 
});

builder.Services.AddHttpClient<QuestionApiClient>(client =>
    {
        // This URL uses "https+http://" to indicate HTTPS is preferred over HTTP.
        // Learn more about service discovery scheme resolution at https://aka.ms/dotnet/sdschemes.
        client.BaseAddress = new("https+http://apiservice");
    });

builder.Services.AddHttpClient<ActiveUsersApiClient>(client =>
    {
        client.BaseAddress = new("https+http://apiservice");
    });

// Register QuestionGroupApiClient
builder.Services.AddHttpClient<QuestionGroupApiClient>(client =>
    {
        client.BaseAddress = new("https+http://apiservice");
    });

// Register IHttpMessageHandlerFactory for SignalR client
builder.Services.AddHttpClient();

// Register QuestionHubService as a scoped service
builder.Services.AddScoped<QuestionHubService>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseAntiforgery();

app.UseOutputCache();

app.MapStaticAssets();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.MapDefaultEndpoints();

app.Run();
