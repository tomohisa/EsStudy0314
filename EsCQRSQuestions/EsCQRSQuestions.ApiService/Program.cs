using Azure.Data.Tables;
using Azure.Identity;
using Microsoft.AspNetCore.Mvc;
using Azure.Storage.Queues;
using EsCQRSQuestions.ApiService;
using EsCQRSQuestions.Domain;
using EsCQRSQuestions.Domain.Aggregates.ActiveUsers.Queries;
using EsCQRSQuestions.Domain.Aggregates.Questions.Commands;
using EsCQRSQuestions.Domain.Aggregates.Questions.Queries;
using EsCQRSQuestions.Domain.Aggregates.Questions.Payloads;
using EsCQRSQuestions.Domain.Aggregates.QuestionGroups.Commands;
using EsCQRSQuestions.Domain.Aggregates.QuestionGroups.Queries;
using EsCQRSQuestions.Domain.Aggregates.Questions.Events;
using EsCQRSQuestions.Domain.Aggregates.WeatherForecasts.Commands;
using EsCQRSQuestions.Domain.Generated;
using EsCQRSQuestions.Domain.Workflows;
using Orleans.Configuration;
using Orleans.Storage;
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

builder.AddKeyedAzureTableClient("OrleansSekibanClustering");

builder.Services.AddSingleton<JsonGrainStorageSerializer>();

builder.AddKeyedAzureBlobClient("OrleansSekibanGrainState");
builder.AddKeyedAzureQueueClient("OrleansSekibanQueue");
builder.AddKeyedAzureTableClient("OrleansPubSubGrainState");
builder.UseOrleans(
    config =>
    {
        if ((builder.Configuration["ORLEANS_CLUSTERING_TYPE"] ?? "").ToLower() == "cosmos")
        {
            var connectionString = builder.Configuration.GetConnectionString("OrleansCosmos") ?? throw new InvalidOperationException();
            config.UseCosmosClustering(options =>
            {
                options.ConfigureCosmosClient(connectionString);
                // this can be enabled if you use Provisioning 
                // options.IsResourceCreationEnabled = true;
            });
        }

        if ((builder.Configuration["ORLEANS_GRAIN_DEFAULT_TYPE"] ?? "").ToLower() == "cosmos")
        {
            config.AddCosmosGrainStorageAsDefault(options =>
            {
                var connectionString = builder.Configuration.GetConnectionString("OrleansCosmos") ?? throw new InvalidOperationException();
                options.ConfigureCosmosClient(connectionString);
                options.IsResourceCreationEnabled = true;
            });
        }

        // Check for VNet IP Address from environment variable APP Service specific setting
        if (!string.IsNullOrWhiteSpace(builder.Configuration["WEBSITE_PRIVATE_IP"]) &&
            !string.IsNullOrWhiteSpace(builder.Configuration["WEBSITE_PRIVATE_PORTS"]))
        {
            // Get IP and ports from environment variables
            var ip = System.Net.IPAddress.Parse(builder.Configuration["WEBSITE_PRIVATE_IP"]!);
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
                        cp.TableName = "EventHubCheckpointsEventStreamsProvider";          // any table name you like
                        cp.PersistInterval = TimeSpan.FromSeconds(10); // write frequency
                        cp.ConfigureTableServiceClient(
                            builder.Configuration.GetConnectionString("OrleansSekibanTable"));
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
                        cp.TableName = "EventHubCheckpointsOrleansSekibanQueue";          // any table name you like
                        cp.PersistInterval = TimeSpan.FromSeconds(10); // write frequency
                        cp.ConfigureTableServiceClient(
                            builder.Configuration.GetConnectionString("OrleansSekibanTable"));
                    }));

                    // …your cache, queue‑mapper, pulling‑agent settings remain unchanged …
                });
        }
        else
        {
            config.AddAzureQueueStreams("EventStreamProvider", (SiloAzureQueueStreamConfigurator configurator) =>
            {
                configurator.ConfigureAzureQueue(options =>
                {
                    options.Configure<IServiceProvider>((queueOptions, sp) =>
                    {
                        queueOptions.QueueServiceClient = sp.GetKeyedService<QueueServiceClient>("OrleansSekibanQueue");
                        queueOptions.QueueNames = [
                            "orleans-service-gkelxzoes6qow-eventstreamprovider-0",
                            "orleans-service-gkelxzoes6qow-eventstreamprovider-1",
                            "orleans-service-gkelxzoes6qow-eventstreamprovider-2"];
                        queueOptions.MessageVisibilityTimeout  = TimeSpan.FromMinutes(2);
                    });
                });
                configurator.Configure<HashRingStreamQueueMapperOptions>(ob =>
                    ob.Configure(o => o.TotalQueueCount = 3));   // 8 → 3 へ

                // --- Pulling Agent の頻度・バッチ ---
                configurator.ConfigurePullingAgent(ob =>
                    ob.Configure(opt =>
                    {
                        opt.GetQueueMsgsTimerPeriod = TimeSpan.FromMilliseconds(1000);
                        opt.BatchContainerBatchSize = 256;
                        opt.StreamInactivityPeriod  = TimeSpan.FromMinutes(10);
                    }));
                // --- キャッシュ ---
                configurator.ConfigureCacheSize(8192);
            });
            config.AddAzureQueueStreams("OrleansSekibanQueue", (SiloAzureQueueStreamConfigurator configurator) =>
            {
                configurator.ConfigureAzureQueue(options =>
                {
                    options.Configure<IServiceProvider>((queueOptions, sp) =>
                    {
                        queueOptions.QueueServiceClient = sp.GetKeyedService<QueueServiceClient>("OrleansSekibanQueue");
                        queueOptions.QueueNames = [
                            "orleans-service-gkelxzoes6qow-orleanssekibanqueue-0",
                            "orleans-service-gkelxzoes6qow-orleanssekibanqueue-1",
                            "orleans-service-gkelxzoes6qow-orleanssekibanqueue-2"];
                        queueOptions.MessageVisibilityTimeout  = TimeSpan.FromMinutes(2);
                    });
                });
                configurator.Configure<HashRingStreamQueueMapperOptions>(ob =>
                    ob.Configure(o => o.TotalQueueCount = 3));   // 8 → 3 へ

                // --- Pulling Agent の頻度・バッチ ---
                configurator.ConfigurePullingAgent(ob =>
                    ob.Configure(opt =>
                    {
                        opt.GetQueueMsgsTimerPeriod = TimeSpan.FromMilliseconds(1000);
                        opt.BatchContainerBatchSize = 256;
                        opt.StreamInactivityPeriod  = TimeSpan.FromMinutes(10);
                    }));
                // --- キャッシュ ---
                configurator.ConfigureCacheSize(8192);
            });
        }
        
        if ((builder.Configuration["ORLEANS_GRAIN_DEFAULT_TYPE"] ?? "").ToLower() == "cosmos")
        {
            config.AddCosmosGrainStorage("PubSubStore",options =>
            {
                var connectionString = builder.Configuration.GetConnectionString("OrleansCosmos") ?? throw new InvalidOperationException();
                options.ConfigureCosmosClient(connectionString);
                options.IsResourceCreationEnabled = true;
            });
            config.AddCosmosGrainStorage("EventStreamProvider",options =>
            {
                var connectionString = builder.Configuration.GetConnectionString("OrleansCosmos") ?? throw new InvalidOperationException();
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
                    // opt.GrainStorageSerializer = sp.GetRequiredService<CustomJsonSerializer>();
                    opt.GrainStorageSerializer = sp.GetRequiredService<NewtonsoftJsonSerializer>();
                });
                // options.GrainStorageSerializer は既定でこの Newtonsoft シリアライザーになる
                options.Configure<IGrainStorageSerializer>(
                    (op, serializer) => op.GrainStorageSerializer = serializer);
            });
        
            // Add grain storage for the stream provider
            config.AddAzureTableGrainStorage("EventStreamProvider", options =>
            {
                options.Configure<IServiceProvider>((opt, sp) =>
                {
                    opt.TableServiceClient = sp.GetKeyedService<TableServiceClient>("OrleansPubSubGrainState");
                    // opt.GrainStorageSerializer = sp.GetRequiredService<IGrainStorageSerializer>();
                    // opt.BlobServiceClient = sp.GetKeyedService<Azure.Storage.Blobs.BlobServiceClient>("OrleansSekibanGrainState");
                    opt.GrainStorageSerializer = sp.GetRequiredService<NewtonsoftJsonSerializer>();
                    // opt.BlobServiceClient = sp.GetKeyedService<Azure.Storage.Blobs.BlobServiceClient>("OrleansSekibanGrainState");
                });
                // options.GrainStorageSerializer は既定でこの Newtonsoft シリアライザーになる
                options.Configure<IGrainStorageSerializer>(
                    (op, serializer) => op.GrainStorageSerializer = serializer);
            });
            // Orleans will automatically discover grains in the same assembly
            config.ConfigureServices(services =>
                services.AddTransient<IGrainStorageSerializer, CustomJsonSerializer>());

        }

        
    });

builder.Services.AddSingleton(
    EsCQRSQuestionsDomainDomainTypes.Generate(EsCQRSQuestionsDomainEventsJsonContext.Default.Options));

SekibanSerializationTypesChecker.CheckDomainSerializability(EsCQRSQuestionsDomainDomainTypes.Generate(EsCQRSQuestionsDomainEventsJsonContext.Default.Options));

builder.Services.AddTransient<ICommandMetadataProvider, CommandMetadataProvider>();
builder.Services.AddTransient<IExecutingUserProvider, HttpExecutingUserProvider>();
builder.Services.AddHttpContextAccessor();
// builder.Services.AddTransient<IGrainStorageSerializer, CustomJsonSerializer>();
// builder.Services.AddTransient<CustomJsonSerializer>();
builder.Services.AddTransient<IGrainStorageSerializer, NewtonsoftJsonSerializer>();
builder.Services.AddTransient<NewtonsoftJsonSerializer>();
builder.Services.AddTransient<SekibanOrleansExecutor>();


// Register hub notification service
builder.Services.AddTransient<IHubNotificationService, HubNotificationService>();

// Register the background service that will use the hub notification service
builder.Services.AddHostedService<OrleansStreamBackgroundService>();

// Register the InitialQuestionsCreator service instead of the hosted service
builder.Services.AddTransient<InitialQuestionsCreator>();
// Comment out or remove the hosted service registration
// builder.Services.AddHostedService<InitialQuestionsService>();

// QuestionGroupServiceはDIに登録せず、使用時に生成する

// Add SignalR
if (!string.IsNullOrEmpty(builder.Configuration["Azure:SignalR:ConnectionString"]))
{
    builder.Services.AddSignalR().AddAzureSignalR();
    Console.WriteLine("Local SignalR configured (no connection string found)");
}
else
{
    // 従来のSignalRを使用する設定（開発環境向け）
    builder.Services.AddSignalR();
    Console.WriteLine("Local SignalR configured (no connection string found)");
}
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

// コード検証エンドポイントを追加
apiRoute.MapGet("/questions/validate/{uniqueCode}", async (
    string uniqueCode, 
    [FromServices] SekibanOrleansExecutor executor) =>
{
    // グループIDが存在するかどうかを確認するためのクエリを実行
    var groupExists = await executor.QueryAsync(new QuestionGroupExistsQuery(uniqueCode));
    
    if (groupExists.IsSuccess && groupExists.GetValue())
    {
        return Results.Ok();
    }
    
    return Results.NotFound();
})
.WithOpenApi()
.WithName("ValidateUniqueCode");

// Question API endpoints
// Queries

// 新しいマルチプロジェクターを使用するエンドポイント
apiRoute.MapGet("/questions/multi", async ([FromServices]SekibanOrleansExecutor executor, [FromQuery] string textContains = "") =>
    {
        var list = await executor.QueryAsync(new EsCQRSQuestions.Domain.Projections.Questions.QuestionsQuery(textContains)).UnwrapBox();
        return list.Items;
    })
    .WithOpenApi()
    .WithName("GetQuestionsMulti");
    
// クライアント側との互換性のための既存エンドポイント維持
apiRoute.MapGet("/questions", async ([FromServices]SekibanOrleansExecutor executor) =>
    {
        var list = await executor.QueryAsync(new QuestionListQuery()).UnwrapBox();
        return list.Items;
    })
    .WithOpenApi()
    .WithName("GetQuestions");
    
apiRoute.MapGet("/questions/bygroup/{groupId}", async (Guid groupId, [FromServices]SekibanOrleansExecutor executor, [FromQuery] string textContains = "") =>
    {
        var list = await executor.QueryAsync(new EsCQRSQuestions.Domain.Projections.Questions.QuestionsQuery(textContains, groupId)).UnwrapBox();
        return list.Items;
    })
    .WithOpenApi()
    .WithName("GetQuestionsByGroup");

apiRoute.MapGet("/questions/active/{uniqueCode}", async (
        [FromServices] SekibanOrleansExecutor executor,
        string uniqueCode) =>
    {
        // UniqueCodeが指定されていない場合は空の結果を返す
        if (string.IsNullOrWhiteSpace(uniqueCode))
        {
            return new ActiveQuestionQuery.ActiveQuestionRecord(
                Guid.Empty,
                string.Empty,
                new List<QuestionOption>(),
                new List<ActiveQuestionQuery.ResponseRecord>(),
                Guid.Empty);
        }
        
        // QuestionGroupServiceをその場で生成
        var groupService = new EsCQRSQuestions.Domain.Services.QuestionGroupService(executor);
        
        // UniqueCodeからグループIDを取得
        var groupId = await groupService.GetGroupIdByUniqueCodeAsync(uniqueCode);
       if (groupId is null) 
        {
            return new ActiveQuestionQuery.ActiveQuestionRecord(
                Guid.Empty,
                string.Empty,
                new List<QuestionOption>(),
                new List<ActiveQuestionQuery.ResponseRecord>(),
                Guid.Empty);
        }
        
        // アクティブな質問を取得
        var activeQuestion = await executor.QueryAsync(new ActiveQuestionQuery(groupId.Value)).UnwrapBox();
        
        // UniqueCodeが指定され、かつグループIDが見つかった場合のみフィルタリング
        if (!string.IsNullOrWhiteSpace(uniqueCode) && groupId.HasValue && activeQuestion.QuestionId != Guid.Empty)
        {
            // 質問が指定されたグループに属していない場合は空の結果を返す
            if (activeQuestion.QuestionGroupId != groupId.Value)
            {
                return new ActiveQuestionQuery.ActiveQuestionRecord(
                    Guid.Empty,
                    string.Empty,
                    new List<QuestionOption>(),
                    new List<ActiveQuestionQuery.ResponseRecord>(),
                    Guid.Empty);
            }
        }
        
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
            [FromServices] SekibanOrleansExecutor executor) => 
        {
            // Workflowを作成して呼び出すシンプルな実装
            var workflow = new QuestionGroupWorkflow(executor);
            return await workflow.CreateQuestionAndAddToGroupEndAsync(command).UnwrapBox();
        })
    .WithOpenApi()
    .WithName("CreateQuestion");

apiRoute
    .MapPost(
        "/questions/update",
        async (
            [FromBody] UpdateQuestionCommand command,
            [FromServices] SekibanOrleansExecutor executor) => 
        {
            try
            {
                var result = await executor.CommandAsync(command);
                if (result.IsSuccess)
                {
                    return Results.Ok(result.UnwrapBox());
                }
                else
                {
                    var exception = result.GetException();
                    if (exception != null)
                    {
                        return Results.BadRequest(new { error = exception.Message });
                    }
                    return Results.BadRequest(new { error = "不明なエラーが発生しました" });
                }
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        })
    .WithOpenApi()
    .WithName("UpdateQuestion");

apiRoute
    .MapPost(
        "/questions/startDisplay",
        async (
            [FromBody] StartDisplayCommand command,
            [FromServices] SekibanOrleansExecutor executor) => 
        {
            // ワークフローを使って排他制御を実装
            var workflow = new QuestionDisplayWorkflow(executor);
            return await workflow.StartDisplayQuestionExclusivelyAsync(command.QuestionId).UnwrapBox();
        })
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
            [FromServices] SekibanOrleansExecutor executor,
            [FromServices] IHubNotificationService notificationService) =>
        {
           var response = await executor.CommandAsync(command).UnwrapBox();
           await executor.QueryAsync(new QuestionDetailQuery(command.QuestionId))
               .Remap(response => response.QuestionGroupId)
               .Conveyor(groupId => executor.QueryAsync(new GetQuestionGroupByGroupIdQuery(groupId)))
               .Remap(group => group.Payload.UniqueCode)
               .Do(uniquecode =>
               {
                   notificationService.NotifyUniqueCodeGroupAsync(uniquecode, "ResponseAdded", new
                   {
                       AggregateId = command.QuestionId,
                       ResponseId = response.PartitionKeys.AggregateId,
                       ParticipantName = command.ParticipantName,
                       SelectedOptionId = command.SelectedOptionId,
                       Comment = command.Comment,
                       Timestamp = (response.Events.First().GetPayload() as ResponseAdded)?.Timestamp,
                       ClientId = command.ClientId // クライアントIDを通知に含める
                   });
               }).UnwrapBox();
           return response;
        })
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

// QuestionGroups API endpoints
// Queries
apiRoute.MapGet("/questionGroups", async ([FromServices]SekibanOrleansExecutor executor) =>
    {
        var list = await executor.QueryAsync(new GetQuestionGroupsQuery()).UnwrapBox();
        return list.Items;
    })
    .WithOpenApi()
    .WithName("GetQuestionGroups");

apiRoute.MapGet("/questionGroups/{id}", async (Guid id, [FromServices]SekibanOrleansExecutor executor) =>
    {
        var groups = await executor.QueryAsync(new GetQuestionGroupsQuery()).UnwrapBox();
        var group = groups.Items.FirstOrDefault(g => g.Id == id);
        if (group == null)
        {
            return Results.NotFound();
        }
        return Results.Ok(group);
    })
    .WithOpenApi()
    .WithName("GetQuestionGroupById");

apiRoute.MapGet("/questionGroups/{id}/questions", async (Guid id, [FromServices]SekibanOrleansExecutor executor) =>
    {
        var questions = await executor.QueryAsync(new GetQuestionsByGroupIdQuery(id)).UnwrapBox();
        return questions.Items;
    })
    .WithOpenApi()
    .WithName("GetQuestionsByGroupId");

// Commands
apiRoute
    .MapPost(
        "/questionGroups",
        async (
            [FromBody] CreateQuestionGroup command,
            [FromServices] SekibanOrleansExecutor executor) => await executor.CommandAsync(command).UnwrapBox())
    .WithOpenApi()
    .WithName("CreateQuestionGroup");
    
// 重複チェック機能を持つエンドポイント
apiRoute
    .MapPost(
        "/questionGroups/createWithUniqueCode",
        async (
            [FromBody] CreateQuestionGroup command,
            [FromServices] SekibanOrleansExecutor executor) => 
        {
            // ワークフローを使って重複チェックを実行
            var workflow = new QuestionGroupWorkflow(executor);
            var result = await workflow.CreateGroupWithUniqueCodeAsync(
                command.Name, command.UniqueCode);
                
            return result.Match(
                groupId => Results.Ok(new { GroupId = groupId }),
                error => Results.Problem(error.Message)
            );
        })
    .WithOpenApi()
    .WithName("CreateQuestionGroupWithUniqueCode");

apiRoute
    .MapPut(
        "/questionGroups/{id}",
        async (
            Guid id,
            [FromBody] UpdateQuestionGroupCommand command,
            [FromServices] SekibanOrleansExecutor executor) => 
        {
            if (id != command.GroupId)
            {
                return Results.BadRequest("ID in URL does not match ID in command");
            }
            return Results.Ok(await executor.CommandAsync(command).UnwrapBox());
        })
    .WithOpenApi()
    .WithName("UpdateQuestionGroup");

apiRoute
    .MapDelete(
        "/questionGroups/{id}",
        async (
            Guid id,
            [FromServices] SekibanOrleansExecutor executor) => 
        {
            var command = new DeleteQuestionGroup(id);
            return Results.Ok(await executor.CommandAsync(command).UnwrapBox());
        })
    .WithOpenApi()
    .WithName("DeleteQuestionGroup");

apiRoute
    .MapPost(
        "/questionGroups/{id}/questions",
        async (
            Guid id,
            [FromBody] AddQuestionToGroup command,
            [FromServices] SekibanOrleansExecutor executor) => 
        {
            if (id != command.QuestionGroupId)
            {
                return Results.BadRequest("Group ID in URL does not match ID in command");
            }
            return Results.Ok(await executor.CommandAsync(command).UnwrapBox());
        })
    .WithOpenApi()
    .WithName("AddQuestionToGroup");

apiRoute
    .MapPut(
        "/questionGroups/{groupId}/questions/{questionId}/order",
        async (
            Guid groupId,
            Guid questionId,
            [FromBody] int newOrder,
            [FromServices] SekibanOrleansExecutor executor) => 
        {
            var command = new ChangeQuestionOrder(groupId, questionId, newOrder);
            return Results.Ok(await executor.CommandAsync(command).UnwrapBox());
        })
    .WithOpenApi()
    .WithName("ChangeQuestionOrder");

apiRoute
    .MapDelete(
        "/questionGroups/{groupId}/questions/{questionId}",
        async (
            Guid groupId,
            Guid questionId,
            [FromServices] SekibanOrleansExecutor executor) => 
        {
            var command = new RemoveQuestionFromGroup(groupId, questionId);
            return Results.Ok(await executor.CommandAsync(command).UnwrapBox());
        })
    .WithOpenApi()
    .WithName("RemoveQuestionFromGroup");

// System administration endpoints
apiRoute
    .MapPost(
        "/system/createInitialQuestions",
        async (
            HttpRequest request,
            [FromServices] InitialQuestionsCreator creator,
            [FromServices] IConfiguration configuration,
            CancellationToken cancellationToken) => 
        {
            // Check authorization key for the initial questions creation
            var initialQuestionsKey = configuration["InitialQuestionsKey"];
            if (!string.IsNullOrEmpty(initialQuestionsKey))
            {
                var authHeader = request.Headers.Authorization.ToString();
                if (string.IsNullOrEmpty(authHeader) || !authHeader.Equals($"Key {initialQuestionsKey}"))
                {
                    return Results.Unauthorized();
                }
            }
            
            await creator.CreateInitialQuestions(cancellationToken);
            return Results.Ok(new { message = "Initial questions created successfully" });
        })
    .WithName("CreateInitialQuestions")
    .WithOpenApi();

// ワークフローを使用した新しいエンドポイント - グループと質問を一度に作成
apiRoute
    .MapPost(
        "/questionGroups/createWithQuestions",
        async (
            [FromBody] QuestionGroupWorkflow.CreateGroupWithQuestionsCommand command,
            [FromServices] SekibanOrleansExecutor executor) => 
        {
            // executorを使用してワークフローを作成
            var workflow = new QuestionGroupWorkflow(executor);
            var result = await workflow.CreateGroupWithQuestionsAsync(command);
            return result.Match(
                groupId => Results.Ok(new { GroupId = groupId }),
                error => Results.Problem(error.Message)
            );
        })
    .WithOpenApi()
    .WithName("CreateQuestionGroupWithQuestions");

// This endpoint is already implemented above with SekibanOrleansExecutor


app.Run();