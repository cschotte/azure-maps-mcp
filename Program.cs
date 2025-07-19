using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = FunctionsApplication.CreateBuilder(args);

builder.ConfigureFunctionsWebApplication();
builder.EnableMcpToolMetadata();
builder.ConfigureMcpTool("Azure Maps Tool");

builder.Services
    .AddHttpClient("AzureMaps")
    .AddStandardResilienceHandler();

builder.Build().Run();
