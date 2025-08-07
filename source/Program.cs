// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Azure.Maps.Mcp.Services;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

var builder = FunctionsApplication.CreateBuilder(args);

// Configure application settings
builder.ConfigureFunctionsWebApplication();

// Configure logging
builder.Services.AddLogging(logging =>
{
    logging.ClearProviders();
    logging.AddConsole();
    
    if (builder.Environment.IsDevelopment())
    {
        logging.SetMinimumLevel(LogLevel.Debug);
    }
    else
    {
        logging.SetMinimumLevel(LogLevel.Information);
    }
});

// Configure MCP (Model Context Protocol) functionality
builder.EnableMcpToolMetadata(); // Enables tool registration metadata
builder.ConfigureMcpTool("Azure Maps Tool"); // Register the tool name exposed to LLMs

// Configure dependency injection
ConfigureServices(builder.Services, builder.Configuration);

// Build and run application
var app = builder.Build();

// Add startup logging
var logger = app.Services.GetRequiredService<ILogger<Program>>();
logger.LogInformation("Azure Maps MCP application starting...");

app.Run();

/// <summary>
/// Configure application services
/// </summary>
/// <param name="services">Service collection</param>
/// <param name="configuration">Configuration instance</param>
static void ConfigureServices(IServiceCollection services, IConfiguration configuration)
{
    // Validate required configuration
    ValidateConfiguration(configuration);
    
    // Register Azure Maps service
    services.AddSingleton<IAzureMapsService, AzureMapsService>();
}

/// <summary>
/// Validate that required configuration values are present
/// </summary>
/// <param name="configuration">Configuration instance</param>
static void ValidateConfiguration(IConfiguration configuration)
{
    var requiredSettings = new[]
    {
        "AZURE_MAPS_SUBSCRIPTION_KEY"
    };

    var missingSettings = requiredSettings
        .Where(setting => string.IsNullOrEmpty(configuration[setting]))
        .ToList();

    if (missingSettings.Count > 0)
    {
        var missingSettingsMessage = string.Join(", ", missingSettings);
        throw new InvalidOperationException(
            $"Missing required configuration settings: {missingSettingsMessage}. " +
            "Please ensure these values are set in your local.settings.json or environment variables.");
    }
}