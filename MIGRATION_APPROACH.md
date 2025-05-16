# Azure Functions Migration to .NET 8.0 Isolated Model

This document outlines the approach taken to migrate the KeyVault.Acmebot Azure Functions application from the in-process model to the isolated worker model running on .NET 8.0.

## Migration Goals

1. Update the application to .NET 8.0
2. Migrate from the in-process model to the isolated worker model
3. Maintain all existing functionality
4. Fix any compatibility issues between the two models
5. Ensure proper dependency resolution with ACMESharpCore

## Key Changes

### Project Configuration

- Updated `KeyVault.Acmebot.csproj` to target .NET 8.0 with `OutputType=Exe`
- Added NuGet package references for the isolated model:
  - Microsoft.Azure.Functions.Worker
  - Microsoft.Azure.Functions.Worker.Sdk
  - Microsoft.Azure.Functions.Worker.Extensions.DurableTask
  - Microsoft.Azure.Functions.Worker.Extensions.Http
  - Microsoft.Azure.Functions.Worker.Extensions.Timer
  - Microsoft.Azure.Functions.Worker.ApplicationInsights

### Host Configuration

- Created `Program.cs` for the isolated model host configuration
- Implemented DI registration similar to what was in `Startup.cs`
- Added `host.json` configuration for extension bundles

### Function Changes

- Updated HTTP trigger functions to use `HttpRequestData` and `HttpResponseData`
- Migrated Durable Functions to use the isolated model classes:
  - Changed `IDurableOrchestrationContext` to `TaskOrchestrationContext`
  - Changed `IDurableOrchestrationClient` to `DurableTaskClient`
  - Implemented custom `CreateActivityProxy` extension method

### Helper Classes

- Created `HttpRequestDataExtensions.cs` for authentication and role checking
- Created `StaticFileHelper.cs` for static file serving
- Created `ApplicationInsightsLoggingMiddleware.cs` for logging
- Created `DurableClientExtensions.cs` for status response creation

### Dependency Handling

- Added binding redirects in `App.config` to handle conflicts between ACMESharpCore (.NET 6.0) and the .NET 8.0 target
- Added `NoWarn` for NU1107 to suppress dependency conflict warnings

### Timer Triggers

- Simplified `PurgeInstanceHistory` as a placeholder due to API differences in the isolated model
- Maintained timer trigger functionality with isolated model syntax

## Testing

The migrated application has been tested locally to ensure:
1. HTTP functions work correctly
2. Durable functions orchestration is maintained
3. Authentication is preserved
4. Static files are served correctly

## Known Issues & Limitations

1. The `PurgeInstanceHistory` function is a placeholder that will need further implementation
2. There are some warnings related to the `TimerTriggerAttribute` conflict
3. Some code quality warnings related to serialization and property types

## Next Steps

1. Complete testing in a staging environment
2. Implement full purge instance history functionality
3. Address code quality warnings
4. Update CI/CD pipelines for production deployment