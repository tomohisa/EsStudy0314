using Azure.Storage.Queues;
using MessageEachOther.ApiService;
using Microsoft.AspNetCore.Mvc;
using MessageEachOther.Domain;
using MessageEachOther.Domain.Generated;
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
        // config.AddMemoryStreams("EventStreamProvider").AddMemoryGrainStorage("EventStreamProvider");
        // for SignalR.Orleans (7.2.0)
        // config.UseSignalR();
        // config.RegisterHub<NotificationHub>();
        // config.AddOrgnalRWithMemoryGrainStorage();
    });

builder.Services.AddSingleton(
    MessageEachOtherDomainDomainTypes.Generate(MessageEachOtherDomainEventsJsonContext.Default.Options));

SekibanSerializationTypesChecker.CheckDomainSerializability(MessageEachOtherDomainDomainTypes.Generate());

builder.Services.AddTransient<ICommandMetadataProvider, CommandMetadataProvider>();
builder.Services.AddTransient<IExecutingUserProvider, HttpExecutingUserProvider>();
builder.Services.AddHttpContextAccessor();

builder.Services.AddTransient<SekibanOrleansExecutor>();

// Register hub notification services
builder.Services.AddTransient<IHubNotificationService, HubNotificationService<ChatHub>>();
builder.Services.AddTransient<IHubNotificationService, HubNotificationService<NotificationHub>>();

// Register the background service that will use all hub notification services
builder.Services.AddHostedService<OrleansStreamBackgroundService>();

if (builder.Configuration.GetSection("Sekiban").GetValue<string>("Database")?.ToLower() == "cosmos")
{
    // Cosmos settings
    builder.AddSekibanCosmosDb();
} else
{
    // Postgres settings
    builder.AddSekibanPostgresDb();
}

// builder.Services.AddSignalR(); // .AddOrleans(); // for SignalR.Orleans (7.2.0)
builder.Services.AddSignalR();//.UseOrgnalR();

// Add CORS services and configure a policy that allows all origins
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});
var app = builder.Build();

var apiRoute = app
    .MapGroup("/api")
    .AddEndpointFilter<ExceptionEndpointFilter>();

// Configure the HTTP request pipeline.
app.UseExceptionHandler();

// Use CORS middleware (must be called before other middleware that sends responses)
app.UseCors();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

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

app.MapHub<ChatHub>("/chatHub");
app.MapHub<NotificationHub>("/notificationHub");

app.Run();
