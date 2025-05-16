# Verifying the .NET Isolated Worker Migration

The migration to .NET isolated worker model has been completed and committed to git. This document provides a quick guide for verifying the changes and deploying the migrated application.

## Changes Summary

1. **Core Migration**
   - Updated to .NET 8.0
   - Migrated all functions to the isolated model
   - Added Program.cs for host configuration
   - Updated all dependencies

2. **Added Support Files**
   - Authentication helpers
   - Static file serving capabilities
   - Testing scripts

3. **Documentation**
   - Migration process and details
   - Testing approach
   - Deployment guidance

## Quick Verification Steps

1. **Local Testing**

   ```bash
   # Start the function app locally
   cd KeyVault.Acmebot
   func start
   
   # In another terminal, run the verification script
   cd ..
   BASE_URL=http://localhost:7071 API_KEY=<your-key> ./tests/deployment-verification.sh
   ```

2. **Deploy to Staging**

   ```bash
   # Create a staging slot if it doesn't exist
   az functionapp deployment slot create --name YourFunctionAppName --resource-group YourResourceGroup --slot staging
   
   # Deploy to staging
   func azure functionapp publish YourFunctionAppName --slot staging
   
   # Update settings for isolated model
   az functionapp config appsettings set --name YourFunctionAppName --resource-group YourResourceGroup --slot staging --settings FUNCTIONS_WORKER_RUNTIME=dotnet-isolated
   
   # Verify deployment
   BASE_URL=https://yourapp-staging.azurewebsites.net API_KEY=<your-key> ./tests/deployment-verification.sh
   ```

3. **Swap to Production**

   ```bash
   az functionapp deployment slot swap --name YourFunctionAppName --resource-group YourResourceGroup --slot staging --target-slot production
   ```

## Manual Verification

For a thorough verification, manually check:

1. Certificate listing works
2. Adding a new certificate works
3. Certificate renewal works
4. DNS zone listing works
5. Static content is properly served

## Important Note About local.settings.json

The `local.settings.json` file has been created but not committed to git (as it's typically ignored for security reasons). Make sure to update this file in your local and deployed environments with:

```json
{
  "IsEncrypted": false,
  "Values": {
    "AzureWebJobsStorage": "...",
    "FUNCTIONS_WORKER_RUNTIME": "dotnet-isolated"
  }
}
```

## Rollback Plan

If issues are encountered, you can:

1. Roll back to the previous commit:
   ```bash
   git checkout <previous-commit-id>
   ```

2. Or swap slots back in Azure:
   ```bash
   az functionapp deployment slot swap --name YourFunctionAppName --resource-group YourResourceGroup --slot production --target-slot staging
   ```