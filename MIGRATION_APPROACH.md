# Migration Approach to .NET Isolated Worker Model

Based on the build errors, we need to take a more structured approach to migrating this application to the .NET isolated worker model.

## Recommended Strategy

Rather than trying to migrate everything at once, a phased approach will be more successful:

### Phase 1: Create a New Project

1. Create a new branch from master
   ```bash
   git checkout -b feature/isolated-worker-migration
   ```

2. Create a new empty .NET 8.0 isolated Functions project using the official templates
   ```bash
   func init KeyVault.Acmebot.Isolated --worker-runtime dotnet-isolated --target-framework net8.0
   ```

3. Add the necessary NuGet packages
   ```bash
   cd KeyVault.Acmebot.Isolated
   dotnet add package Microsoft.Azure.Functions.Worker
   dotnet add package Microsoft.Azure.Functions.Worker.Sdk
   dotnet add package Microsoft.Azure.Functions.Worker.Extensions.Http
   dotnet add package Microsoft.Azure.Functions.Worker.Extensions.Timer
   dotnet add package Microsoft.Azure.Functions.Worker.Extensions.DurableTask
   dotnet add package Microsoft.Extensions.DependencyInjection
   dotnet add package Microsoft.Extensions.Logging
   dotnet add package Azure.Identity
   dotnet add package Azure.Security.KeyVault.Certificates
   # Add other dependent packages as needed
   ```

### Phase 2: Migrate Core Infrastructure

1. Create a proper Program.cs file using the isolated model
2. Migrate the dependency injection setup
3. Test that the project compiles and runs locally

### Phase 3: Migrate Simple Functions First

1. Start by migrating HTTP-triggered functions (not using Durable Functions)
2. Set up proper authentication
3. Test each function as it's migrated

### Phase 4: Adapt Durable Functions

The Durable Functions API has significant differences between the in-process and isolated models:

1. Replace `IDurableOrchestrationContext` with `TaskOrchestrationContext`
2. Update `CreateActivityProxy<T>` usage patterns 
3. Replace `CallSubOrchestratorWithRetryAsync` with appropriate isolated equivalents
4. Update client-side methods like `WaitForCompletionOrCreateCheckStatusResponseAsync`

### Phase 5: Integration and Testing

1. Run all functions locally
2. Deploy to a staging slot
3. Run integration tests
4. Fix any issues found during testing

## API Changes to Note

Based on the errors, pay special attention to:

1. **Durable Client API**:
   - Replace `IDurableClient` with `DurableTaskClient`
   - Update methods like `StartNewAsync` to `ScheduleNewOrchestrationInstanceAsync`

2. **Orchestration Context API**:
   - Replace `IDurableOrchestrationContext` with `TaskOrchestrationContext`
   - Update methods like `CreateActivityProxy` and `CallSubOrchestratorWithRetryAsync`

3. **Application Insights**:
   - The way to configure Application Insights is different in isolated model
   - `AddApplicationInsights()` needs to be replaced with proper isolated model setup

4. **Function Attributes**:
   - `[FunctionName]` -> `[Function]`
   - `[TimerTrigger]` needs correct namespaces and imports

## Resources

- [Official migration guide](https://learn.microsoft.com/en-us/azure/azure-functions/migrate-dotnet-to-isolated-model)
- [Durable Functions for .NET isolated](https://learn.microsoft.com/en-us/azure/azure-functions/durable/durable-functions-dotnet-isolated-overview)
- [Sample migration PR](https://github.com/Azure/azure-functions-dotnet-worker/discussions/1539)

## Next Steps

This migration requires careful planning and incremental changes. Consider starting with a simpler function to validate the approach before migrating the more complex Durable Functions logic.