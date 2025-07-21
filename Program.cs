using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = FunctionsApplication.CreateBuilder(args);

builder.ConfigureFunctionsWebApplication();
builder.EnableMcpToolMetadata(); // Enables tool registration metadata
builder.ConfigureMcpTool("Azure Maps Tool"); // Register the tool name exposed to LLMs

builder.Services
    .AddHttpClient("AzureMaps") // Named http client for Azure Maps
    .AddStandardResilienceHandler(); // Optional: adds resilience policies

builder.Build().Run();
