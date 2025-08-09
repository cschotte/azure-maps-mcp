// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Azure.Maps.Mcp.Services;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = FunctionsApplication.CreateBuilder(args);

// Configure application settings
builder.ConfigureFunctionsWebApplication();

// Use default logging configuration provided by Functions runtime

// Configure MCP (Model Context Protocol) functionality
builder.EnableMcpToolMetadata(); // Enables tool registration metadata
builder.ConfigureMcpTool("Azure Maps Tool"); // Register the tool name exposed to LLMs

// Configure dependency injection
builder.Services.AddSingleton<IAzureMapsService, AzureMapsService>();

// Build and run application
var app = builder.Build();
app.Run();