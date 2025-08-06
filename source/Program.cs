// Copyright (c) 2025 Clemens Schotte
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using Azure.Maps.Mcp.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = FunctionsApplication.CreateBuilder(args);

builder.ConfigureFunctionsWebApplication();
builder.EnableMcpToolMetadata(); // Enables tool registration metadata
builder.ConfigureMcpTool("Azure Maps Tool"); // Register the tool name exposed to LLMs

// Add Azure Maps service
builder.Services.AddSingleton<IAzureMapsService, AzureMapsService>();

builder.Build().Run();