using Projects;
using Microsoft.Extensions.Hosting;
using System;
using System.Threading.Tasks;
using Aspire.Hosting.Dashboard;
using Aspire.Hosting;
using Microsoft.Extensions.DependencyInjection;

var builder = DistributedApplication.CreateBuilder(args);

var storage = builder.AddAzureStorage("azurestorage")
    .RunAsEmulator();
    // .RunAsEmulator(r => r.WithImage("azure-storage/azurite", "3.33.0")); // no need this line for new template
var clusteringTable = storage.AddTables("OrleansSekibanClustering");
var grainStorage = storage.AddBlobs("OrleansSekibanGrainState");
var queue = storage.AddQueues("OrleansSekibanQueue");



var postgres = builder
    .AddPostgres("orleansSekibanPostgres")
    // .WithDataVolume() // Uncomment to use a data volume
    .WithPgAdmin()
    .AddDatabase("SekibanPostgres");

var orleans = builder.AddOrleans("default")
    .WithClustering(clusteringTable)
    // .WithClustering()
    .WithGrainStorage("Default", grainStorage)
    .WithGrainStorage("OrleansSekibanQueue", grainStorage)
    .WithStreaming(queue);

var apiService = builder.AddProject<EsCQRSQuestions_ApiService>("apiservice")
    // .WithEndpoint("https", annotation => annotation.IsProxied = false)
    .WithReference(postgres)
    .WithReference(orleans)
    // .WithReplicas(2); // Uncomment to run with 2 replicas
    ; // Use our custom extension method


// User web frontend
builder.AddProject<Projects.EsCQRSQuestions_Web>("webfrontend")
    .WithExternalHttpEndpoints()
    .WithReference(apiService)
    .WaitFor(apiService);

// Admin web frontend
builder.AddProject<Projects.EsCQRSQuestions_AdminWeb>("adminwebfrontend")
    .WithExternalHttpEndpoints()
    .WithReference(apiService)
    .WaitFor(apiService);

builder.Build().Run();
