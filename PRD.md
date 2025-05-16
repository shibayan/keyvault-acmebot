# Product Requirements Document: Migration to .NET Isolated Worker Model

## Overview

This document outlines the requirements for migrating the KeyVault.Acmebot application from the in-process .NET worker model to the isolated worker model, targeting .NET 8.0.

## Background

KeyVault.Acmebot is an Azure Functions-based application that automates the renewal and management of SSL/TLS certificates through Let's Encrypt. It uses Durable Functions for orchestration and activities, making the migration complex due to significant API differences between in-process and isolated worker models.

## Current Issues Identified

Based on build errors encountered during migration attempts, the following issues need to be addressed:

### 1. Durable Functions API Incompatibilities

1. **Orchestration Context**:
   - Error: `'TaskOrchestrationContext' does not contain a definition for 'CreateActivityProxy'`
   - In isolated model, the way to create activity proxies is different
   - Affected files:
     - `GetCertificates.cs:31`
     - `SharedOrchestrator.cs:20`
     - `GetDnsZones.cs:31`
     - `RenewCertificate.cs:30`
     - `RevokeCertificate.cs:31`
     - `RenewCertificates.cs:19`

2. **Durable Client API**:
   - Error: `DurableClient attribute is applied to a 'DurableTaskClient' but must be used with either an IDurableClient, IDurableEntityClient, or an IDurableOrchestrationClient`
   - The Durable Client attribute in isolated model expects different client types
   - Affected in all HTTP functions that use Durable Client

3. **Orchestration Operations**:
   - Error: `'TaskOrchestrationContext' does not contain a definition for 'CallSubOrchestratorWithRetryAsync'`
   - Affected file: `RenewCertificates.cs:48`

4. **Client Methods**:
   - Error: `Argument 4: cannot convert from 'System.TimeSpan' to 'System.Threading.CancellationToken'`
   - Method signature changes in isolated model for `CreateCheckStatusResponseAsync`
   - Affected files:
     - `GetCertificates.cs:53`
     - `GetDnsZones.cs:53`
     - `RevokeCertificate.cs:58`

5. **Output Handling**:
   - Error: `'OrchestrationMetadata' does not contain a definition for 'Output'`
   - Orchestration status handling differences
   - Affected file: `GetInstanceState.cs:58`

### 2. Timer Function Issues

1. **Timer Trigger Namespace**:
   - Error: `The type or namespace name 'Timer' does not exist in the namespace 'Microsoft.Azure.Functions.Worker.Extensions'`
   - Timer extension not properly referenced
   - Affected files:
     - `PurgeInstanceHistory.cs:5`
     - `RenewCertificates.cs:8`

2. **PurgeInstancesOptions**:
   - Error: `The type or namespace name 'PurgeInstancesOptions' could not be found`
   - Different purge API in isolated model
   - Affected file: `PurgeInstanceHistory.cs:26`

3. **TaskOptions**:
   - Error: `'TaskOptions' does not contain a constructor that takes 2 arguments`
   - Error: `'TaskOptions' does not contain a definition for 'Handle'`
   - RetryOptions replaced with TaskOptions but with different signature
   - Affected file: `RenewCertificates.cs:68-70`

### 3. Dependency Injection and Configuration Issues

1. **Application Insights**:
   - Error: `'WorkerOptions' does not contain a definition for 'AddApplicationInsights'`
   - Different way to configure Application Insights in isolated model
   - Affected file: `Program.cs:23`

2. **Options Validation**:
   - Error: `'OptionsBuilder<AcmebotOptions>' does not contain a definition for 'ValidateDataAnnotations'`
   - Different options validation approach needed
   - Affected file: `Program.cs:31`

3. **Lifecycle Notification**:
   - Error: `The type or namespace name 'ILifeCycleNotificationHelper' could not be found`
   - Different webhook notification system in isolated model
   - Affected file: `Program.cs:81`

4. **Startup Reference**:
   - Error: `The type or namespace name 'Startup' could not be found`
   - `Startup.cs` was removed as part of migration
   - Affected file: `Constants.cs:10`

### 4. Code Structure Issues

1. **Variable Name Conflicts**:
   - Error: `A local or parameter named 'response' cannot be declared in this scope`
   - Variable name reuse within the same scope
   - Affected file: `AddCertificate.cs:46,53`

2. **Logger Reference**:
   - Error: `The name '_logger' does not exist in the current context`
   - Logger referencing issues
   - Affected file: `RenewCertificates.cs:65`

## Dependency Issues

1. **ACMESharpCore Dependency Conflict**:
   - Warning: `Detected package version outside of dependency constraint: ACMESharpCore requires Microsoft.Extensions.Logging (>= 6.0.0 && < 7.0.0) but version Microsoft.Extensions.Logging 8.0.0 was resolved`
   - ACMESharpCore requires .NET 6 libraries while we're migrating to .NET 8

## Required Changes

### Function Signature Changes

1. **HTTP-Triggered Functions**:
   ```csharp
   // Old
   [FunctionName("Name")]
   public async Task<IActionResult> Run(
       [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "route")] HttpRequest req,
       [DurableClient] IDurableClient starter,
       ILogger log)
   {
       // ...
   }

   // New
   [Function("Name")]
   public async Task<HttpResponseData> Run(
       [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "route")] HttpRequestData req,
       [DurableClient] DurableTaskClient starter)
   {
       // ...
   }
   ```

2. **Orchestrator Functions**:
   ```csharp
   // Old
   [FunctionName("Name")]
   public async Task Run([OrchestrationTrigger] IDurableOrchestrationContext context)
   {
       // ...
   }

   // New
   [Function("Name")]
   public async Task Run([OrchestrationTrigger] TaskOrchestrationContext context)
   {
       // ...
   }
   ```

3. **Activity Functions**:
   ```csharp
   // Old
   [FunctionName("Name")]
   public async Task<T> Run([ActivityTrigger] IDurableActivityContext context)
   {
       // ...
   }

   // New
   [Function("Name")]
   public async Task<T> Run([ActivityTrigger] TaskActivityContext context)
   {
       // ...
   }
   ```

4. **Timer Functions**:
   ```csharp
   // Old
   [FunctionName("Name")]
   public async Task Run([TimerTrigger("cron")] TimerInfo timer, ILogger log)
   {
       // ...
   }

   // New
   [Function("Name")]
   public async Task Run([TimerTrigger("cron")] object timerInfo)
   {
       // ...
   }
   ```

### Infrastructure Changes

1. **Program.cs**:
   - Replace the configuration of Application Insights
   - Update options validation
   - Update lifecycle notification registration

2. **Extension Methods**:
   - Create extensions for ActivityProxy creation in TaskOrchestrationContext
   - Create extensions for HTTP response handling with DurableTaskClient

3. **Package References**:
   - Update to correct versions of Microsoft.Azure.Functions.Worker packages
   - Add binding redirects for ACMESharpCore dependency conflicts

### Durable Functions Specific Changes

1. **Activity Proxy**:
   - Create a custom implementation or extension method for CreateActivityProxy
   - Update all orchestrator functions to use the new approach

2. **RetryOptions**:
   - Replace RetryOptions with TaskOptions with proper constructor and properties
   - Update all references to match the new API

3. **Client API**:
   - Update client methods:
     - StartNewAsync → ScheduleNewOrchestrationInstanceAsync
     - WaitForCompletionOrCreateCheckStatusResponseAsync → CreateCheckStatusResponseAsync

## Testing Requirements

1. **Compilation Testing**:
   - Ensure the project builds without errors
   - Address all warnings that could cause runtime issues

2. **Functional Testing**:
   - Local testing of each function type:
     - HTTP-triggered functions
     - Timer-triggered functions
     - Durable orchestrations
   - Test authentication flow
   - Test certificate operations end-to-end

3. **Deployment Testing**:
   - Deploy to staging environment
   - Verify runtime behavior matches expected
   - Test performance characteristics

## Success Criteria

1. Application successfully builds with .NET 8.0 isolated worker model
2. All functions execute correctly with the same behavior as the original
3. No regression in functionality or performance
4. No critical warnings in the build process
5. Clean deployment to Azure Functions

## Implementation Approach

Given the complexity of the changes required, a phased implementation approach is recommended:

1. **Phase 1**: Create a new branch for the migration
2. **Phase 2**: Update project file, add Program.cs, update packages
3. **Phase 3**: Migrate simple HTTP functions first
4. **Phase 4**: Create helpers for Durable Functions interaction
5. **Phase 5**: Migrate timer functions
6. **Phase 6**: Migrate complex Durable Function orchestrators
7. **Phase 7**: Comprehensive testing and deployment

## Documentation Requirements

1. Update README.md with information about the migration
2. Document any behavioral differences between in-process and isolated models
3. Update deployment instructions
4. Document any new configuration settings required

## Dependencies and Constraints

1. **Breaking Changes**:
   - Durable Functions API has significant breaking changes in the isolated model
   - Authentication handling is different

2. **Compatibility**:
   - Must remain compatible with existing Azure resources
   - Must maintain current functionality

3. **Performance**:
   - Should maintain or improve performance characteristics

## Timeline

This migration is complex and will require significant code changes. The estimated timeline is:

1. Initial setup and framework migration: 1 day
2. Basic HTTP function migration: 1 day
3. Timer function migration: 1 day
4. Durable function migration: 2-3 days
5. Testing and bug fixing: 1-2 days

Total estimated time: 6-8 days of focused development effort

## Risks and Mitigation

1. **Risk**: Incompatible Durable Functions APIs
   **Mitigation**: Create adapter/helper classes to bridge the gap

2. **Risk**: ACMESharpCore dependency conflicts
   **Mitigation**: Use assembly binding redirects or consider forking/updating the library

3. **Risk**: Runtime behavior differences
   **Mitigation**: Comprehensive testing in staging environment

4. **Risk**: Performance regression
   **Mitigation**: Performance testing before and after migration

## Conclusion

Migrating KeyVault.Acmebot to the .NET isolated worker model will provide better process isolation, improved performance, and alignment with Microsoft's Azure Functions roadmap. However, it requires significant changes to adapt to the different programming model, particularly around Durable Functions. A careful, phased approach is necessary to ensure a successful migration.