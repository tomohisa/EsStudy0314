using Microsoft.AspNetCore.Mvc;
using Azure.Storage.Queues;
using EsCQRSQuestions.ApiService;
using EsCQRSQuestions.Domain;
using EsCQRSQuestions.Domain.Aggregates.ActiveUsers.Queries;
using EsCQRSQuestions.Domain.Aggregates.Questions.Commands;
using EsCQRSQuestions.Domain.Aggregates.Questions.Queries;
using EsCQRSQuestions.Domain.Aggregates.WeatherForecasts.Commands;
using EsCQRSQuestions.Domain.Generated;
using ResultBoxes;
using Scalar.AspNetCore;
using Sekiban.Pure.AspNetCore;
using Sekiban.Pure.Command.Handlers;
using Sekiban.Pure.CosmosDb;
using Sekiban.Pure.Orleans.Parts;
using Sekiban.Pure.Postgres;

var builder = WebApplication.CreateBuilder(args);

// Add service defaults & Aspire client integrations.
builder.AddServiceDefaults();

// Add services to the container.
builder.Services.AddProblemDetails();

// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

builder.AddKeyedAzureTableClient("orleans-sekiban-clustering");
builder.AddKeyedAzureBlobClient("orleans-sekiban-grain-state");
builder.AddKeyedAzureQueueClient("orleans-sekiban-queue");
builder.UseOrleans(
    config =>
    {
        // config.UseDashboard(options => { });
        config.AddAzureQueueStreams("EventStreamProvider", (SiloAzureQueueStreamConfigurator configurator) =>
        {
            configurator.ConfigureAzureQueue(options =>
            {
                options.Configure<IServiceProvider>((queueOptions, sp) =>
                {
                    queueOptions.QueueServiceClient = sp.GetKeyedService<QueueServiceClient>("orleans-sekiban-queue");
                });
            });
        });
        
        // Add grain storage for the stream provider
        config.AddAzureBlobGrainStorage("EventStreamProvider", options =>
        {
            options.Configure<IServiceProvider>((opt, sp) =>
            {
                opt.BlobServiceClient = sp.GetKeyedService<Azure.Storage.Blobs.BlobServiceClient>("orleans-sekiban-grain-state");
            });
        });
    });

builder.Services.AddSingleton(
    EsCQRSQuestionsDomainDomainTypes.Generate(EsCQRSQuestionsDomainEventsJsonContext.Default.Options));

SekibanSerializationTypesChecker.CheckDomainSerializability(EsCQRSQuestionsDomainDomainTypes.Generate());

builder.Services.AddTransient<ICommandMetadataProvider, CommandMetadataProvider>();
builder.Services.AddTransient<IExecutingUserProvider, HttpExecutingUserProvider>();
builder.Services.AddHttpContextAccessor();

builder.Services.AddTransient<SekibanOrleansExecutor>();

// Register hub notification service
builder.Services.AddTransient<IHubNotificationService, HubNotificationService>();

// Register the background service that will use the hub notification service
builder.Services.AddHostedService<OrleansStreamBackgroundService>();

// Register the service that will create initial questions
builder.Services.AddHostedService<InitialQuestionsService>();

// Add SignalR
builder.Services.AddSignalR();

if (builder.Configuration.GetSection("Sekiban").GetValue<string>("Database")?.ToLower() == "cosmos")
{
    // Cosmos settings
    builder.AddSekibanCosmosDb();
} else
{
    // Postgres settings
    builder.AddSekibanPostgresDb();
}
// Add CORS services and configure a policy that allows specific origins with credentials
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins("https://localhost:7201", "https://localhost:5260")
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

var app = builder.Build();

var apiRoute = app
    .MapGroup("/api")
    .AddEndpointFilter<ExceptionEndpointFilter>();

// Configure the HTTP request pipeline.
app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

// Use CORS middleware (must be called before other middleware that sends responses)
app.UseCors();

// Map SignalR hub with CORS
app.MapHub<QuestionHub>("/questionHub").RequireCors(policy => policy
    .WithOrigins("https://localhost:7201", "https://localhost:5260")
    .AllowAnyHeader()
    .AllowAnyMethod()
    .AllowCredentials());

string[] summaries = ["Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"];

apiRoute.MapGet("/weatherforecast", async ([FromServices]SekibanOrleansExecutor executor) =>
    {
        var list = await executor.QueryAsync(new WeatherForecastQuery("")).UnwrapBox();
        return list.Items;
}).WithOpenApi()
.WithName("GetWeatherForecast");

apiRoute
    .MapPost(
        "/inputweatherforecast",
        async (
            [FromBody] InputWeatherForecastCommand command,
            [FromServices] SekibanOrleansExecutor executor) => await executor.CommandAsync(command).UnwrapBox())
    .WithName("InputWeatherForecast")
    .WithOpenApi();

apiRoute
    .MapPost(
        "/removeweatherforecast",
        async (
            [FromBody] RemoveWeatherForecastCommand command,
            [FromServices] SekibanOrleansExecutor executor) => await executor.CommandAsync(command).UnwrapBox())
    .WithName("RemoveWeatherForecast")
    .WithOpenApi();

apiRoute
    .MapPost(
        "/updateweatherforecastlocation",
        async (
            [FromBody] UpdateWeatherForecastLocationCommand command,
            [FromServices] SekibanOrleansExecutor executor) => await executor.CommandAsync(command).UnwrapBox())
    .WithName("UpdateWeatherForecastLocation")
    .WithOpenApi();

app.MapDefaultEndpoints();

// Question API endpoints
// Queries
apiRoute.MapGet("/questions", async ([FromServices]SekibanOrleansExecutor executor) =>
    {
        var list = await executor.QueryAsync(new QuestionListQuery()).UnwrapBox();
        return list.Items;
    })
    .WithOpenApi()
    .WithName("GetQuestions");

apiRoute.MapGet("/questions/active", async ([FromServices]SekibanOrleansExecutor executor) =>
    {
        var activeQuestion = await executor.QueryAsync(new ActiveQuestionQuery()).UnwrapBox();
        return activeQuestion;
    })
    .WithOpenApi()
    .WithName("GetActiveQuestion");

apiRoute.MapGet("/questions/{id}", async (Guid id, [FromServices]SekibanOrleansExecutor executor) =>
    {
        var question = await executor.QueryAsync(new QuestionDetailQuery(id)).UnwrapBox();
        if (question == null)
        {
            return Results.NotFound();
        }
        return Results.Ok(question);
    })
    .WithOpenApi()
    .WithName("GetQuestionById");

// Commands
apiRoute
    .MapPost(
        "/questions/create",
        async (
            [FromBody] CreateQuestionCommand command,
            [FromServices] SekibanOrleansExecutor executor) => await executor.CommandAsync(command).UnwrapBox())
    .WithOpenApi()
    .WithName("CreateQuestion");

apiRoute
    .MapPost(
        "/questions/update",
        async (
            [FromBody] UpdateQuestionCommand command,
            [FromServices] SekibanOrleansExecutor executor) => await executor.CommandAsync(command).UnwrapBox())
    .WithOpenApi()
    .WithName("UpdateQuestion");

apiRoute
    .MapPost(
        "/questions/startDisplay",
        async (
            [FromBody] StartDisplayCommand command,
            [FromServices] SekibanOrleansExecutor executor) => await executor.CommandAsync(command).UnwrapBox())
    .WithOpenApi()
    .WithName("StartDisplayQuestion");

apiRoute
    .MapPost(
        "/questions/stopDisplay",
        async (
            [FromBody] StopDisplayCommand command,
            [FromServices] SekibanOrleansExecutor executor) => await executor.CommandAsync(command).UnwrapBox())
    .WithOpenApi()
    .WithName("StopDisplayQuestion");

apiRoute
    .MapPost(
        "/questions/addResponse",
        async (
            [FromBody] AddResponseCommand command,
            [FromServices] SekibanOrleansExecutor executor) => await executor.CommandAsync(command).UnwrapBox())
    .WithOpenApi()
    .WithName("AddResponse");

apiRoute
    .MapPost(
        "/questions/delete",
        async (
            [FromBody] DeleteQuestionCommand command,
            [FromServices] SekibanOrleansExecutor executor) => await executor.CommandAsync(command).UnwrapBox())
    .WithOpenApi()
    .WithName("DeleteQuestion");

// ActiveUsers API endpoints
apiRoute.MapGet("/activeusers/{id}", async (Guid id, [FromServices]SekibanOrleansExecutor executor) =>
    {
        var activeUsers = await executor.QueryAsync(new ActiveUsersQuery(id)).UnwrapBox();
        if (activeUsers == null)
        {
            return Results.NotFound();
        }
        return Results.Ok(activeUsers);
    })
    .WithOpenApi()
    .WithName("GetActiveUsers");

app.Run();
