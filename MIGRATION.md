# Migration to .NET Isolated Model

This document outlines the steps needed to complete the migration from in-process to .NET isolated model for Azure Functions.

## Changes Made So Far

1. Updated project file:
   - Changed target framework to `net8.0`
   - Added `<OutputType>Exe</OutputType>`
   - Replaced WebJobs packages with Functions.Worker packages

2. Created Program.cs:
   - Added host builder configuration
   - Migrated Startup.cs DI container setup
   - Configured ApplicationInsights

3. Updated host.json:
   - Added extension bundle configuration

4. Created authentication helper:
   - Added HttpRequestDataExtensions.cs for authentication

5. Updated GetCertificates.cs:
   - Migrated function to use isolated model
   - Updated DurableTask API usage
   - Implemented authentication using extensions

6. Updated SharedActivity.cs:
   - Replaced `[FunctionName]` with `[Function]`
   - Updated namespace imports

7. Added local.settings.json:
   - Set `FUNCTIONS_WORKER_RUNTIME` to `dotnet-isolated`

## Remaining Tasks

1. Update all function classes:
   - Replace `[FunctionName]` with `[Function]`
   - Update namespace imports
   - Replace in-process binding types with isolated binding types

2. Update HTTP functions:
   - Change `HttpRequest` to `HttpRequestData`
   - Change `IActionResult` to `HttpResponseData`
   - Update authentication checks using `HttpRequestDataExtensions`
   - Update response creation

3. Update Durable Functions:
   - Replace `IDurableOrchestrationContext` with `TaskOrchestrationContext`
   - Replace `IDurableClient` with `DurableTaskClient`
   - Update durable function client methods

4. Testing:
   - Test functions locally to ensure they work properly
   - Deploy to a staging slot in Azure
   - Run integration tests

## Reference Documentation

- [Migrate from .NET in-process to isolated worker model](https://learn.microsoft.com/en-us/azure/azure-functions/migrate-dotnet-to-isolated-model)
- [.NET isolated process guide](https://learn.microsoft.com/en-us/azure/azure-functions/dotnet-isolated-process-guide)
- [Durable Functions for .NET isolated](https://learn.microsoft.com/en-us/azure/azure-functions/durable/durable-functions-dotnet-isolated-overview)