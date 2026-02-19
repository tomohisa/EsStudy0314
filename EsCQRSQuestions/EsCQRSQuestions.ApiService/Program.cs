using System.Net;
using Azure.Data.Tables;
using Azure.Storage.Queues;
using EsCQRSQuestions.ApiService;
using EsCQRSQuestions.Domain;
using EsCQRSQuestions.Domain.Aggregates.ActiveUsers.Queries;
using EsCQRSQuestions.Domain.Aggregates.QuestionGroups.Commands;
using EsCQRSQuestions.Domain.Aggregates.QuestionGroups.Queries;
using EsCQRSQuestions.Domain.Aggregates.Questions.Commands;
using EsCQRSQuestions.Domain.Aggregates.Questions.Events;
using EsCQRSQuestions.Domain.Aggregates.Questions.Payloads;
using EsCQRSQuestions.Domain.Aggregates.Questions.Queries;
using EsCQRSQuestions.Domain.Aggregates.WeatherForecasts.Commands;
using EsCQRSQuestions.Domain.Generated;
using EsCQRSQuestions.Domain.Projections.Questions;
using EsCQRSQuestions.Domain.Services;
using EsCQRSQuestions.Domain.Workflows;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Orleans.Configuration;
using Orleans.Runtime;
using Orleans.Storage;
using ResultBoxes;
using Scalar.AspNetCore;
using Sekiban.Dcb;
using Sekiban.Pure.Command.Executor;
using Sekiban.Dcb.Actors;
using Sekiban.Dcb.Orleans.Streams;
using Sekiban.Dcb.CosmosDb;
using Sekiban.Dcb.Orleans;
using Sekiban.Dcb.Orleans.Grains;
using Sekiban.Dcb.Postgres;
using Sekiban.Dcb.Storage;

var builder = WebApplication.CreateBuilder(args);

// Add service defaults & Aspire client integrations.
builder.AddServiceDefaults();

// Add services to the container.
builder.Services.AddProblemDetails();

// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

if (!string.IsNullOrWhiteSpace(builder.Configuration.GetConnectionString("OrleansSekibanClustering")))
{
    builder.AddKeyedAzureTableServiceClient("OrleansSekibanClustering");
}
if (!string.IsNullOrWhiteSpace(builder.Configuration.GetConnectionString("OrleansSekibanGrainState")))
{
    builder.AddKeyedAzureBlobServiceClient("OrleansSekibanGrainState");
}
if (!string.IsNullOrWhiteSpace(builder.Configuration.GetConnectionString("OrleansSekibanQueue")))
{
    builder.AddKeyedAzureQueueServiceClient("OrleansSekibanQueue");
}
if (!string.IsNullOrWhiteSpace(builder.Configuration.GetConnectionString("OrleansPubSubGrainState")))
{
    builder.AddKeyedAzureTableServiceClient("OrleansPubSubGrainState");
}
builder.UseOrleans(config =>
{
    if ((builder.Configuration["ORLEANS_CLUSTERING_TYPE"] ?? "").ToLower() == "cosmos")
    {
        var connectionString = builder.Configuration.GetConnectionString("OrleansCosmos") ??
                               throw new InvalidOperationException();
        config.UseCosmosClustering(options =>
        {
            options.ConfigureCosmosClient(connectionString);
            options.IsResourceCreationEnabled = true;
        });
    }

    if ((builder.Configuration["ORLEANS_GRAIN_DEFAULT_TYPE"] ?? "").ToLower() == "cosmos")
        config.AddCosmosGrainStorageAsDefault(options =>
        {
            var connectionString = builder.Configuration.GetConnectionString("OrleansCosmos") ??
                                   throw new InvalidOperationException();
            options.ConfigureCosmosClient(connectionString);
            options.IsResourceCreationEnabled = true;
        });
    else
        config.AddMemoryGrainStorage("OrleansStorage");

    // Fallback clustering for ACA/self-host scenarios where clustering provider is not configured.
    // This prevents startup failure ("Unable to resolve service for type Orleans.IMembershipTable").
    if ((builder.Configuration["ORLEANS_CLUSTERING_TYPE"] ?? "").ToLower() != "cosmos")
    {
        config.UseLocalhostClustering();
    }

    // Check for VNet IP Address from environment variable APP Service specific setting
    if (!string.IsNullOrWhiteSpace(builder.Configuration["WEBSITE_PRIVATE_IP"]) &&
        !string.IsNullOrWhiteSpace(builder.Configuration["WEBSITE_PRIVATE_PORTS"]))
    {
        // Get IP and ports from environment variables
        var ip = IPAddress.Parse(builder.Configuration["WEBSITE_PRIVATE_IP"]!);
        var ports = builder.Configuration["WEBSITE_PRIVATE_PORTS"]!.Split(',');
        if (ports.Length < 2) throw new Exception("Insufficient number of private ports");
        int siloPort = int.Parse(ports[0]), gatewayPort = int.Parse(ports[1]);
        Console.WriteLine($"Using WEBSITE_PRIVATE_IP: {ip}, siloPort: {siloPort}, gatewayPort: {gatewayPort}");
        config.ConfigureEndpoints(ip, siloPort, gatewayPort, true);
    }

    // config.UseDashboard(options => { });

    if ((builder.Configuration["ORLEANS_QUEUE_TYPE"] ?? "").ToLower() == "eventhub")
    {
        config.AddEventHubStreams(
            "EventStreamProvider",
            configurator =>
            {
                // Existing Event Hub connection settings
                configurator.ConfigureEventHub(ob => ob.Configure(options =>
                {
                    options.ConfigureEventHubConnection(
                        builder.Configuration.GetConnectionString("OrleansEventHub"),
                        builder.Configuration["ORLEANS_QUEUE_EVENTHUB_NAME"],
                        "$Default");
                }));

                // 🔑 NEW –‑ tell Orleans where to persist checkpoints
                configurator.UseAzureTableCheckpointer(ob => ob.Configure(cp =>
                {
                    cp.TableName = "EventHubCheckpointsEventStreamsProvider"; // any table name you like
                    cp.PersistInterval = TimeSpan.FromSeconds(10); // write frequency
                    var tableConnectionString = builder.Configuration.GetConnectionString("OrleansSekibanTable")
                            ?? throw new InvalidOperationException();
                        cp.TableServiceClient = new TableServiceClient(tableConnectionString);
                }));

                // …your cache, queue‑mapper, pulling‑agent settings remain unchanged …
            });
        config.AddEventHubStreams(
            "OrleansSekibanQueue",
            configurator =>
            {
                // Existing Event Hub connection settings
                configurator.ConfigureEventHub(ob => ob.Configure(options =>
                {
                    options.ConfigureEventHubConnection(
                        builder.Configuration.GetConnectionString("OrleansEventHub"),
                        builder.Configuration["ORLEANS_QUEUE_EVENTHUB_NAME"],
                        "$Default");
                }));

                // 🔑 NEW –‑ tell Orleans where to persist checkpoints
                configurator.UseAzureTableCheckpointer(ob => ob.Configure(cp =>
                {
                    cp.TableName = "EventHubCheckpointsOrleansSekibanQueue"; // any table name you like
                    cp.PersistInterval = TimeSpan.FromSeconds(10); // write frequency
                    var tableConnectionString = builder.Configuration.GetConnectionString("OrleansSekibanTable")
                            ?? throw new InvalidOperationException();
                        cp.TableServiceClient = new TableServiceClient(tableConnectionString);
                }));

                // …your cache, queue‑mapper, pulling‑agent settings remain unchanged …
            });
    }
    else
    {
        config.AddAzureQueueStreams("EventStreamProvider", configurator =>
        {
            configurator.ConfigureAzureQueue(options =>
            {
                options.Configure<IServiceProvider>((queueOptions, sp) =>
                {
                    queueOptions.QueueServiceClient = sp.GetKeyedService<QueueServiceClient>("OrleansSekibanQueue");
                    queueOptions.QueueNames =
                    [
                        "orleans-service-gkelxzoes6qow-eventstreamprovider-0",
                        "orleans-service-gkelxzoes6qow-eventstreamprovider-1",
                        "orleans-service-gkelxzoes6qow-eventstreamprovider-2"
                    ];
                    queueOptions.MessageVisibilityTimeout = TimeSpan.FromMinutes(2);
                });
            });
            configurator.Configure<HashRingStreamQueueMapperOptions>(ob =>
                ob.Configure(o => o.TotalQueueCount = 3)); // 8 → 3 へ

            // --- Pulling Agent の頻度・バッチ ---
            configurator.ConfigurePullingAgent(ob =>
                ob.Configure(opt =>
                {
                    opt.GetQueueMsgsTimerPeriod = TimeSpan.FromMilliseconds(1000);
                    opt.BatchContainerBatchSize = 256;
                    opt.StreamInactivityPeriod = TimeSpan.FromMinutes(10);
                }));
            // --- キャッシュ ---
            configurator.ConfigureCacheSize(8192);
        });
        config.AddAzureQueueStreams("OrleansSekibanQueue", configurator =>
        {
            configurator.ConfigureAzureQueue(options =>
            {
                options.Configure<IServiceProvider>((queueOptions, sp) =>
                {
                    queueOptions.QueueServiceClient = sp.GetKeyedService<QueueServiceClient>("OrleansSekibanQueue");
                    queueOptions.QueueNames =
                    [
                        "orleans-service-gkelxzoes6qow-orleanssekibanqueue-0",
                        "orleans-service-gkelxzoes6qow-orleanssekibanqueue-1",
                        "orleans-service-gkelxzoes6qow-orleanssekibanqueue-2"
                    ];
                    queueOptions.MessageVisibilityTimeout = TimeSpan.FromMinutes(2);
                });
            });
            configurator.Configure<HashRingStreamQueueMapperOptions>(ob =>
                ob.Configure(o => o.TotalQueueCount = 3)); // 8 → 3 へ

            // --- Pulling Agent の頻度・バッチ ---
            configurator.ConfigurePullingAgent(ob =>
                ob.Configure(opt =>
                {
                    opt.GetQueueMsgsTimerPeriod = TimeSpan.FromMilliseconds(1000);
                    opt.BatchContainerBatchSize = 256;
                    opt.StreamInactivityPeriod = TimeSpan.FromMinutes(10);
                }));
            // --- キャッシュ ---
            configurator.ConfigureCacheSize(8192);
        });
    }

    if ((builder.Configuration["ORLEANS_GRAIN_DEFAULT_TYPE"] ?? "").ToLower() == "cosmos")
    {
        config.AddCosmosGrainStorage("OrleansStorage", options =>
        {
            var connectionString = builder.Configuration.GetConnectionString("OrleansCosmos") ??
                                   throw new InvalidOperationException();
            options.ConfigureCosmosClient(connectionString);
            options.IsResourceCreationEnabled = true;
        });
        config.AddCosmosGrainStorage("PubSubStore", options =>
        {
            var connectionString = builder.Configuration.GetConnectionString("OrleansCosmos") ??
                                   throw new InvalidOperationException();
            options.ConfigureCosmosClient(connectionString);
            options.IsResourceCreationEnabled = true;
        });
        config.AddCosmosGrainStorage("EventStreamProvider", options =>
        {
            var connectionString = builder.Configuration.GetConnectionString("OrleansCosmos") ??
                                   throw new InvalidOperationException();
            options.ConfigureCosmosClient(connectionString);
            options.IsResourceCreationEnabled = true;
        });
    }
    else
    {
        config.AddAzureTableGrainStorage("PubSubStore", options =>
        {
            options.Configure<IServiceProvider>((opt, sp) =>
            {
                opt.TableServiceClient = sp.GetKeyedService<TableServiceClient>("OrleansPubSubGrainState");
                opt.GrainStorageSerializer = sp.GetRequiredService<NewtonsoftJsonDcbOrleansSerializer>();
            });
            options.Configure<IGrainStorageSerializer>((op, serializer) => op.GrainStorageSerializer = serializer);
        });

        // Add grain storage for the stream provider
        config.AddAzureTableGrainStorage("EventStreamProvider", options =>
        {
            options.Configure<IServiceProvider>((opt, sp) =>
            {
                opt.TableServiceClient = sp.GetKeyedService<TableServiceClient>("OrleansPubSubGrainState");
                opt.GrainStorageSerializer = sp.GetRequiredService<NewtonsoftJsonDcbOrleansSerializer>();
            });
            options.Configure<IGrainStorageSerializer>((op, serializer) => op.GrainStorageSerializer = serializer);
        });

        // Orleans will automatically discover grains in the same assembly
        // Orleans will automatically discover grains in the same assembly
        config.ConfigureServices(services =>
            services.AddTransient<IGrainStorageSerializer, NewtonsoftJsonDcbOrleansSerializer>());
    }
    // Orleans will automatically discover grains in the same assembly
    config.ConfigureServices(services =>
        services.AddTransient<IGrainStorageSerializer, NewtonsoftJsonDcbOrleansSerializer>());
});

var domainTypes = EsCQRSQuestionsDomainDomainTypes.Generate();
builder.Services.AddSingleton(domainTypes);

builder.Services.AddSekibanDcbNativeRuntime();
builder.Services.AddTransient<Sekiban.Dcb.MultiProjections.IMultiProjectionEventStatistics, Sekiban.Dcb.MultiProjections.NoOpMultiProjectionEventStatistics>();
builder.Services.AddTransient<Sekiban.Dcb.Actors.GeneralMultiProjectionActorOptions>(_ => new Sekiban.Dcb.Actors.GeneralMultiProjectionActorOptions());
if (builder.Configuration.GetSection("Sekiban").GetValue<string>("Database")?.ToLower() == "cosmos")
{
    builder.Services.AddSekibanDcbCosmosDbWithAspire();
    builder.Services.AddSingleton<IMultiProjectionStateStore, Sekiban.Dcb.CosmosDb.CosmosMultiProjectionStateStore>();
}
else
{
    builder.Services.AddSingleton<Sekiban.Dcb.ServiceId.IServiceIdProvider, Sekiban.Dcb.ServiceId.DefaultServiceIdProvider>();
    builder.Services.AddSingleton<IEventStore, PostgresEventStore>();
    builder.Services.AddSekibanDcbPostgresWithAspire("SekibanPostgres");
    builder.Services.AddSingleton<IMultiProjectionStateStore, Sekiban.Dcb.Postgres.PostgresMultiProjectionStateStore>();
}

builder.Services.AddTransient<IGrainStorageSerializer, NewtonsoftJsonDcbOrleansSerializer>();
builder.Services.AddTransient<NewtonsoftJsonDcbOrleansSerializer>();
builder.Services.AddSingleton<IStreamDestinationResolver>(_ =>
    new DefaultOrleansStreamDestinationResolver("EventStreamProvider", "AllEvents", Guid.Empty));
builder.Services.AddSingleton<IEventSubscriptionResolver>(_ =>
    new DefaultOrleansEventSubscriptionResolver("EventStreamProvider", "AllEvents", Guid.Empty));
builder.Services.AddSingleton<IEventPublisher, OrleansEventPublisher>();
builder.Services.AddTransient<ISekibanExecutor, OrleansDcbExecutor>();
builder.Services.AddScoped<IActorObjectAccessor, OrleansActorObjectAccessor>();


// Register hub notification service
builder.Services.AddTransient<IHubNotificationService, HubNotificationService>();

// Register the background service that will use the hub notification service
builder.Services.AddHostedService<OrleansStreamBackgroundService>();

// Comment out or remove the hosted service registration
// builder.Services.AddHostedService<InitialQuestionsService>();

// QuestionGroupServiceはDIに登録せず、使用時に生成する

// Add SignalR
if (!string.IsNullOrEmpty(builder.Configuration["Azure:SignalR:ConnectionString"]))
{
    builder.Services.AddSignalR().AddAzureSignalR();
    Console.WriteLine("Azure SignalR configured");
}
else
{
    // 従来のSignalRを使用する設定（開発環境向け）
    builder.Services.AddSignalR();
    Console.WriteLine("Local SignalR configured (no connection string found)");
}

// Add CORS services and configure a policy that allows specific origins with credentials
// builder.Services.AddCors(options =>
// {
//     options.AddDefaultPolicy(policy =>
//     {
//         policy.WithOrigins("https://localhost:7201", "https://localhost:5260")
//               .AllowAnyHeader()
//               .AllowAnyMethod()
//               .AllowCredentials();
//     });
// });

var app = builder.Build();

var apiRoute = app
    .MapGroup("/api")
    .AddEndpointFilter<ExceptionEndpointFilter>();

static bool IsTransientQuestionGroupError(Exception ex)
{
    if (ex is DbUpdateException || ex is OrleansException)
    {
        return true;
    }

    if (ex.InnerException is not null)
    {
        return IsTransientQuestionGroupError(ex.InnerException);
    }

    var message = ex.Message ?? string.Empty;
    return message.Contains("duplicate key value", StringComparison.OrdinalIgnoreCase)
           || message.Contains("Stream type mismatch", StringComparison.OrdinalIgnoreCase);
}

static async Task<T> RetryTransientAsync<T>(Func<Task<T>> action, int maxAttempts = 5)
{
    for (var attempt = 1; ; attempt++)
    {
        try
        {
            return await action();
        }
        catch (Exception ex) when (attempt < maxAttempts && IsTransientQuestionGroupError(ex))
        {
            await Task.Delay(100 * attempt * attempt);
        }
    }
}

static async Task<string?> TryResolveUniqueCodeByQuestionIdAsync(ISekibanExecutor executor, Guid questionId)
{
    var questionResult = await executor.QueryAsync(new QuestionDetailQuery(questionId));
    if (!questionResult.IsSuccess)
    {
        return null;
    }

    var question = questionResult.GetValue();
    if (question.QuestionGroupId == Guid.Empty)
    {
        return null;
    }

    var groupsResult = await executor.QueryAsync(new GetQuestionGroupsQuery());
    if (!groupsResult.IsSuccess)
    {
        return null;
    }

    var group = groupsResult.GetValue().Items.FirstOrDefault(g => g.Id == question.QuestionGroupId);
    return group?.UniqueCode;
}

// Configure the HTTP request pipeline.
app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

// Use CORS middleware (must be called before other middleware that sends responses)
// app.UseCors();

// app.UseRouting();
app.MapHub<QuestionHub>("/questionHub");

string[] summaries =
    ["Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"];

apiRoute.MapGet("/weatherforecast", async ([FromServices] ISekibanExecutor executor) =>
    {
        var list = await executor.QueryAsync(new WeatherForecastQuery("")).UnwrapBox();
        return list.Items;
    })
    .WithName("GetWeatherForecast");

apiRoute
    .MapPost(
        "/inputweatherforecast",
        async (
            [FromBody] InputWeatherForecastCommand command,
            [FromServices] ISekibanExecutor executor) => await executor.ExecuteAsync(command).UnwrapBox())
    .WithName("InputWeatherForecast");

apiRoute
    .MapPost(
        "/removeweatherforecast",
        async (
            [FromBody] RemoveWeatherForecastCommand command,
            [FromServices] ISekibanExecutor executor) => await executor.ExecuteAsync(command).UnwrapBox())
    .WithName("RemoveWeatherForecast");

apiRoute
    .MapPost(
        "/updateweatherforecastlocation",
        async (
            [FromBody] UpdateWeatherForecastLocationCommand command,
            [FromServices] ISekibanExecutor executor) => await executor.ExecuteAsync(command).UnwrapBox())
    .WithName("UpdateWeatherForecastLocation");

app.MapDefaultEndpoints();

// コード検証エンドポイントを追加
apiRoute.MapGet("/questions/validate/{uniqueCode}", async (
        string uniqueCode,
        [FromServices] ISekibanExecutor executor) =>
    {
        // グループIDが存在するかどうかを確認するためのクエリを実行
        var groupExists = await executor.QueryAsync(new QuestionGroupExistsQuery(uniqueCode));

        if (groupExists.IsSuccess && groupExists.GetValue()) return Results.Ok();

        return Results.NotFound();
    })
    .WithName("ValidateUniqueCode");

// Question API endpoints
// Queries

// 新しいマルチプロジェクターを使用するエンドポイント
apiRoute.MapGet("/questions/multi",
        async ([FromServices] ISekibanExecutor executor, [FromQuery] string textContains = "") =>
        {
            var list = await executor.QueryAsync(new QuestionsQuery(textContains)).UnwrapBox();
            return list.Items;
        })
    .WithName("GetQuestionsMulti");

// クライアント側との互換性のための既存エンドポイント維持
apiRoute.MapGet("/questions", async ([FromServices] ISekibanExecutor executor) =>
    {
        var list = await executor.QueryAsync(new QuestionListQuery()).UnwrapBox();
        return list.Items;
    })
    .WithName("GetQuestions");

apiRoute.MapGet("/questions/bygroup/{groupId}",
        async (Guid groupId, [FromServices] ISekibanExecutor executor, [FromQuery] string textContains = "",
            [FromQuery] string? waitForSortableUniqueId = null) =>
        {
            var list = await executor.QueryAsync(new QuestionsQuery(textContains, groupId)
                { WaitForSortableUniqueId = waitForSortableUniqueId }).UnwrapBox();
            return list.Items;
        })
    .WithName("GetQuestionsByGroup");

apiRoute.MapGet("/questions/active/{uniqueCode}", async (
        [FromServices] ISekibanExecutor executor,
        string uniqueCode) =>
    {
        // 投影間の不整合を避けるため、QuestionsMultiProjector だけで
        // uniqueCode -> group -> active question を解決する。
        return await executor.QueryAsync(new ActiveQuestionByUniqueCodeQuery(uniqueCode)).UnwrapBox();
    })
    .WithName("GetActiveQuestion");

apiRoute.MapGet("/questions/{id}", async (Guid id, [FromServices] ISekibanExecutor executor) =>
    {
        var question = await executor.QueryAsync(new QuestionDetailQuery(id)).UnwrapBox();
        if (question == null) return Results.NotFound();
        return Results.Ok(question);
    })
    .WithName("GetQuestionById");

// Commands
apiRoute
    .MapPost(
        "/questions/create",
        async (
            [FromBody] CreateQuestionCommand command,
            [FromServices] ISekibanExecutor executor) =>
        {
            // Workflowを作成して呼び出すシンプルな実装
            var workflow = new QuestionGroupWorkflow(executor);
            // ToSimpleCommandResponseを使用してLastSortableUniqueIdを含むレスポンスに変換
            return await workflow.CreateQuestionAndAddToGroupEndAsync(command).UnwrapBox();
        })
    .WithName("CreateQuestion");

apiRoute
    .MapPost(
        "/questions/update",
        async (
                [FromBody] UpdateQuestionCommand command,
                [FromServices] ISekibanExecutor executor) =>
            await executor.ExecuteAsync(command).ToSimpleCommandResponse().UnwrapBox())
    .WithName("UpdateQuestion");

apiRoute
    .MapPost(
        "/questions/startDisplay",
        async (
            [FromBody] StartDisplayCommand command,
            [FromServices] ISekibanExecutor executor) =>
        {
            // ワークフローを使って排他制御を実装
            var workflow = new QuestionDisplayWorkflow(executor);
            return await workflow.StartDisplayQuestionExclusivelyAsync(command.QuestionId).UnwrapBox();
        })
    .WithName("StartDisplayQuestion");

apiRoute
    .MapPost(
        "/questions/stopDisplay",
        (
                [FromBody] StopDisplayCommand command,
                [FromServices] ISekibanExecutor executor) =>
            executor.ExecuteAsync(command).ToSimpleCommandResponse().UnwrapBox())
    .WithName("StopDisplayQuestion");

apiRoute
    .MapPost(
        "/questions/addResponse",
        async (
            [FromBody] AddResponseCommand command,
            [FromServices] ISekibanExecutor executor,
            [FromServices] IHubNotificationService notificationService,
            [FromServices] ILogger<Program> logger) =>
        {
            var commandResult = await executor.ExecuteAsync(command);
            commandResult.UnwrapBox();
            var notificationPayload = new
            {
                AggregateId = command.QuestionId,
                ResponseId = (commandResult.GetValue().Events.FirstOrDefault()?.Payload as ResponseAdded)?.ResponseId ?? Guid.Empty,
                command.ParticipantName,
                command.SelectedOptionId,
                command.Comment,
                (commandResult.GetValue().Events.FirstOrDefault()?.Payload as ResponseAdded)?.Timestamp,
                command.ClientId
            };

            try
            {
                var uniqueCode = await TryResolveUniqueCodeByQuestionIdAsync(executor, command.QuestionId);
                var notifyTasks = new List<Task> { notificationService.NotifyAdminsAsync("ResponseAdded", notificationPayload) };

                if (!string.IsNullOrWhiteSpace(uniqueCode))
                {
                    notifyTasks.Add(notificationService.NotifyUniqueCodeGroupAsync(uniqueCode, "ResponseAdded", notificationPayload));
                }

                await Task.WhenAll(notifyTasks);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "ResponseAdded notification failed. QuestionId: {QuestionId}", command.QuestionId);
            }

            return commandResult.ToSimpleCommandResponse().UnwrapBox();
        })
    .WithName("AddResponse");

apiRoute
    .MapPost(
        "/questions/updateResponseComment",
        async (
            [FromBody] UpdateResponseCommentCommand command,
            [FromServices] ISekibanExecutor executor,
            [FromServices] IHubNotificationService notificationService,
            [FromServices] ILogger<Program> logger) =>
        {
            var commandResult = await executor.ExecuteAsync(command);
            commandResult.UnwrapBox();
            var notificationPayload = new
            {
                AggregateId = command.QuestionId,
                command.ClientId,
                command.Comment,
                Timestamp = (commandResult.GetValue().Events.FirstOrDefault()?.Payload as ResponseCommentUpdated)?.Timestamp
            };

            try
            {
                var uniqueCode = await TryResolveUniqueCodeByQuestionIdAsync(executor, command.QuestionId);
                var notifyTasks = new List<Task>
                {
                    notificationService.NotifyAdminsAsync("ResponseCommentUpdated", notificationPayload)
                };

                if (!string.IsNullOrWhiteSpace(uniqueCode))
                {
                    notifyTasks.Add(notificationService.NotifyUniqueCodeGroupAsync(uniqueCode, "ResponseCommentUpdated", notificationPayload));
                }

                await Task.WhenAll(notifyTasks);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "ResponseCommentUpdated notification failed. QuestionId: {QuestionId}", command.QuestionId);
            }

            return commandResult.ToSimpleCommandResponse().UnwrapBox();
        })
    .WithName("UpdateResponseComment");

apiRoute
    .MapPost(
        "/questions/delete",
        async (
            [FromBody] DeleteQuestionCommand command,
            [FromServices] ISekibanExecutor executor) =>
        {
            var result = await executor.ExecuteAsync(command);
            return result.ToSimpleCommandResponse().UnwrapBox();
        })
    .WithName("DeleteQuestion");

// ActiveUsers API endpoints
apiRoute.MapGet("/activeusers/{id}",
        async (Guid id, [FromQuery] string? waitForSortableUniqueId, [FromServices] ISekibanExecutor executor) =>
        {
            var query = new ActiveUsersQuery(id)
            {
                WaitForSortableUniqueId = waitForSortableUniqueId
            };
            var activeUsers = await executor.QueryAsync(query).UnwrapBox();
            if (activeUsers == null) return Results.NotFound();
            return Results.Ok(activeUsers);
        })
    .WithName("GetActiveUsers");

// QuestionGroups API endpoints
// Queries
apiRoute.MapGet("/questionGroups",
        async ([FromQuery] string? waitForSortableUniqueId, [FromServices] ISekibanExecutor executor) =>
        {
            var query = new GetQuestionGroupsQuery
            {
                WaitForSortableUniqueId = waitForSortableUniqueId
            };
            var list = await RetryTransientAsync(async () => await executor.QueryAsync(query).UnwrapBox());
            return list.Items;
        })
    .WithName("GetQuestionGroups");

apiRoute.MapGet("/questionGroups/{id}",
        async (Guid id, [FromQuery] string? waitForSortableUniqueId, [FromServices] ISekibanExecutor executor) =>
        {
            var query = new GetQuestionGroupsQuery
            {
                WaitForSortableUniqueId = waitForSortableUniqueId
            };
            var groups = await RetryTransientAsync(async () => await executor.QueryAsync(query).UnwrapBox());
            var group = groups.Items.FirstOrDefault(g => g.Id == id);
            if (group == null) return Results.NotFound();
            return Results.Ok(group);
        })
    .WithName("GetQuestionGroupById");

apiRoute.MapGet("/questionGroups/{id}/questions",
        async (Guid id, [FromQuery] string? waitForSortableUniqueId, [FromServices] ISekibanExecutor executor) =>
        {
            var query = new GetQuestionsByGroupIdQuery(id)
            {
                WaitForSortableUniqueId = waitForSortableUniqueId
            };
            var questions = await RetryTransientAsync(async () => await executor.QueryAsync(query).UnwrapBox());
            return questions.Items;
        })
    .WithName("GetQuestionsByGroupId");

// Commands
apiRoute
    .MapPost(
        "/questionGroups",
        async (
                [FromBody] CreateQuestionGroup command,
                [FromServices] ISekibanExecutor executor) =>
            await RetryTransientAsync(async () => await executor.ExecuteAsync(command).ToSimpleCommandResponse().UnwrapBox()))
    .WithName("CreateQuestionGroup");

// 重複チェック機能を持つエンドポイント
apiRoute
    .MapPost(
        "/questionGroups/createWithUniqueCode",
        async (
            [FromBody] CreateQuestionGroup command,
            [FromServices] ISekibanExecutor executor) =>
        {
            // ワークフローを使って重複チェックを実行
            var workflow = new QuestionGroupWorkflow(executor);
            return await workflow.CreateGroupWithUniqueCodeAsync(
                command.Name, command.UniqueCode);
        })
    .WithName("CreateQuestionGroupWithUniqueCode");

apiRoute
    .MapPut(
        "/questionGroups/{id}",
        async (
            Guid id,
            [FromBody] UpdateQuestionGroupCommand command,
            [FromServices] ISekibanExecutor executor) =>
        {
            if (id != command.GroupId) return Results.BadRequest("ID in URL does not match ID in command");
            var result = await RetryTransientAsync(async () => await executor.ExecuteAsync(command));
            return Results.Ok(result.ToSimpleCommandResponse().UnwrapBox());
        })
    .WithName("UpdateQuestionGroup");

apiRoute
    .MapDelete(
        "/questionGroups/{id}",
        async (
            Guid id,
            [FromServices] ISekibanExecutor executor) =>
        {
            var command = new DeleteQuestionGroup(id);
            var result = await RetryTransientAsync(async () => await executor.ExecuteAsync(command));
            return Results.Ok(result.ToSimpleCommandResponse().UnwrapBox());
        })
    .WithName("DeleteQuestionGroup");

apiRoute
    .MapPost(
        "/questionGroups/{id}/questions",
        async (
            Guid id,
            [FromBody] AddQuestionToGroup command,
            [FromServices] ISekibanExecutor executor) =>
        {
            if (id != command.QuestionGroupId)
                return Results.BadRequest("Group ID in URL does not match ID in command");
            var result = await executor.ExecuteAsync(command);
            return Results.Ok(result.ToSimpleCommandResponse().UnwrapBox());
        })
    .WithName("AddQuestionToGroup");

apiRoute
    .MapPut(
        "/questionGroups/{groupId}/questions/{questionId}/order",
        async (
            Guid groupId,
            Guid questionId,
            [FromBody] int newOrder,
            [FromServices] ISekibanExecutor executor) =>
        {
            var command = new ChangeQuestionOrder(groupId, questionId, newOrder);
            var result = await executor.ExecuteAsync(command);
            return Results.Ok(result.ToSimpleCommandResponse().UnwrapBox());
        })
    .WithName("ChangeQuestionOrder");

apiRoute
    .MapDelete(
        "/questionGroups/{groupId}/questions/{questionId}",
        async (
            Guid groupId,
            Guid questionId,
            [FromServices] ISekibanExecutor executor) =>
        {
            var command = new RemoveQuestionFromGroup(groupId, questionId);
            var result = await executor.ExecuteAsync(command);
            return Results.Ok(result.ToSimpleCommandResponse().UnwrapBox());
        })
    .WithName("RemoveQuestionFromGroup");


app.Run();
