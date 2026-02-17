using EsCQRSQuestions.AdminWeb;
using EsCQRSQuestions.AdminWeb.Components;
using EsCQRSQuestions.AdminWeb.Services;
using EsCQRSQuestions.AdminWeb.Models;
using System.Globalization;

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
    BaseUrl = ResolveClientBaseUrl(builder.Configuration)
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

static string ResolveClientBaseUrl(IConfiguration configuration)
{
    // Explicit override is still supported for non-AppHost scenarios.
    var explicitBaseUrl = configuration["ClientBaseUrl"];
    if (!string.IsNullOrWhiteSpace(explicitBaseUrl))
    {
        return NormalizeForBrowser(explicitBaseUrl);
    }

    // Preferred key pattern when AppHost injects service discovery entries.
    var preferredKeys = new[]
    {
        "services:webfrontend:http:0",
        "services:webfrontend:https:0",
        "services:webfrontend:http",
        "services:webfrontend:https"
    };

    foreach (var key in preferredKeys)
    {
        var value = configuration[key];
        if (!string.IsNullOrWhiteSpace(value))
        {
            return NormalizeForBrowser(value);
        }
    }

    // Fallback: scan all webfrontend service keys and pick the first usable value.
    foreach (var entry in configuration.AsEnumerable())
    {
        if (string.IsNullOrWhiteSpace(entry.Key) || string.IsNullOrWhiteSpace(entry.Value))
        {
            continue;
        }

        if (!entry.Key.StartsWith("services:webfrontend:", true, CultureInfo.InvariantCulture))
        {
            continue;
        }

        return NormalizeForBrowser(entry.Value);
    }

    throw new InvalidOperationException(
        "ClientBaseUrl is not configured and AppHost did not provide services:webfrontend endpoint data.");
}

static string NormalizeForBrowser(string rawValue)
{
    var value = rawValue.Trim();

    if (value.StartsWith("https+http://", StringComparison.OrdinalIgnoreCase))
    {
        return $"http://{value["https+http://".Length..].TrimEnd('/')}";
    }

    if (value.StartsWith("http+https://", StringComparison.OrdinalIgnoreCase))
    {
        return $"https://{value["http+https://".Length..].TrimEnd('/')}";
    }

    if (Uri.TryCreate(value, UriKind.Absolute, out var uri) &&
        (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
    {
        return value.TrimEnd('/');
    }

    // If AppHost injects host:port without scheme, default to http for local dev.
    if (Uri.TryCreate($"http://{value}", UriKind.Absolute, out var hostPortUri))
    {
        return hostPortUri.ToString().TrimEnd('/');
    }

    throw new InvalidOperationException($"Cannot parse webfrontend endpoint value: '{rawValue}'.");
}
