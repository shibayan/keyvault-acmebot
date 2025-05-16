# .NET Isolated Worker Migration - Summary

## Changes Made

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

4. Created helper classes for the isolated model:
   - Added HttpRequestDataExtensions.cs for authentication
   - Added StaticFileHelper.cs for serving static files

5. Updated functions to use the isolated model:
   - GetCertificates.cs
   - GetDnsZones.cs
   - SharedActivity.cs (all activity functions)
   - SharedOrchestrator.cs
   - StaticPage.cs
   - RenewCertificates.cs
   - AddCertificate.cs

6. Key changes in functions:
   - Changed `[FunctionName]` to `[Function]`
   - Changed HTTP triggers to use `HttpRequestData` and `HttpResponseData` instead of `HttpRequest` and `IActionResult`
   - Changed durable functions to use `TaskOrchestrationContext` and `DurableTaskClient`
   - Updated authentication using extension methods
   - Updated logging patterns to use dependency injection via constructor instead of function parameters

## Remaining Work

1. The following functions still need to be updated:
   - GetInstanceState.cs 
   - PurgeInstanceHistory.cs
   - RenewCertificate.cs
   - RevokeCertificate.cs

2. Testing:
   - Test locally to ensure all functions work properly
   - Deploy to a staging slot in Azure
   - Run integration tests

## Pattern for Updating Functions

1. HTTP-triggered functions:
   ```csharp
   // Old
   [FunctionName("FunctionName")]
   public async Task<IActionResult> Run(
       [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "route")] HttpRequest req,
       ILogger log)
   {
       // ...
   }

   // New
   [Function("FunctionName")]
   public async Task<HttpResponseData> Run(
       [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "route")] HttpRequestData req)
   {
       // ...
   }
   ```

2. Durable orchestrator functions:
   ```csharp
   // Old
   [FunctionName("FunctionName")]
   public async Task Run([OrchestrationTrigger] IDurableOrchestrationContext context)
   {
       // ...
   }

   // New
   [Function("FunctionName")]
   public async Task Run([OrchestrationTrigger] TaskOrchestrationContext context)
   {
       // ...
   }
   ```

3. Durable client functions:
   ```csharp
   // Old
   [FunctionName("FunctionName")]
   public async Task<IActionResult> Run(
       [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "route")] HttpRequest req,
       [DurableClient] IDurableClient starter,
       ILogger log)
   {
       var instanceId = await starter.StartNewAsync("OrchestratorName", null);
       return starter.CreateCheckStatusResponse(req, instanceId);
   }

   // New
   [Function("FunctionName")]
   public async Task<HttpResponseData> Run(
       [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "route")] HttpRequestData req,
       [DurableClient] DurableTaskClient starter)
   {
       var instanceId = await starter.ScheduleNewOrchestrationInstanceAsync("OrchestratorName");
       return await starter.CreateCheckStatusResponseAsync(req, instanceId);
   }
   ```

4. Timer-triggered functions:
   ```csharp
   // Old
   [FunctionName("FunctionName")]
   public async Task Run([TimerTrigger("0 0 0 * * *")] TimerInfo timer, ILogger log)
   {
       // ...
   }

   // New
   [Function("FunctionName")]
   public async Task Run([TimerTrigger("0 0 0 * * *")] FunctionContext context)
   {
       var logger = context.GetLogger<YourClassName>();
       // ...
   }
   ```

## Resources

For more information, see:
- [Migrate from .NET in-process to isolated worker model](https://learn.microsoft.com/en-us/azure/azure-functions/migrate-dotnet-to-isolated-model)
- [.NET isolated process guide](https://learn.microsoft.com/en-us/azure/azure-functions/dotnet-isolated-process-guide)
- [Durable Functions for .NET isolated](https://learn.microsoft.com/en-us/azure/azure-functions/durable/durable-functions-dotnet-isolated-overview)