# Acme-Cert-Bot Verification Tests

This directory contains tests to verify that the Acme-Cert-Bot application is functioning correctly after migration to the .NET isolated worker model.

## Test Scripts

### PowerShell Integration Test

`integration-test.ps1` is a PowerShell script that tests the core functionality of the application by making requests to various API endpoints.

#### Usage

```powershell
.\integration-test.ps1 -BaseUrl "https://your-function-app.azurewebsites.net" -ApiKey "your-function-key" [-VerboseOutput]
```

Parameters:
- `BaseUrl`: The base URL of your deployed Azure Function app
- `ApiKey`: Your function key for authentication
- `VerboseOutput`: (Optional) Switch to enable verbose logging of requests and responses

### Bash Deployment Verification

`deployment-verification.sh` is a Bash script for verifying a deployment is working correctly.

#### Usage

```bash
BASE_URL=https://your-function-app.azurewebsites.net API_KEY=your-function-key ./deployment-verification.sh
```

Environment variables:
- `BASE_URL`: The base URL of your deployed Azure Function app
- `API_KEY`: Your function key for authentication
- `VERBOSE`: Set to "true" for verbose output (optional)

## Local Testing

To test the application locally:

1. Start the Azure Functions Core Tools:

```bash
cd /path/to/KeyVault.Acmebot
func start
```

2. Run the verification script against the local endpoint:

```bash
# PowerShell
.\tests\integration-test.ps1 -BaseUrl "http://localhost:7071" -ApiKey "your-local-key"

# Or Bash
BASE_URL=http://localhost:7071 API_KEY=your-local-key ./tests/deployment-verification.sh
```

## Test Coverage

These tests verify the following functionality:

1. Certificate operations
   - List certificates
   - Add certificates
   - Renew certificates
   - Revoke certificates

2. DNS operations
   - List DNS zones

3. Infrastructure functionality
   - Static content delivery
   - Instance state management

## Note on Test Expectations

These tests are designed to verify API endpoint accessibility rather than full end-to-end certificate issuance, which would require actual DNS validation. In test environments without proper DNS configuration, some operations are expected to fail with appropriate error responses.

## Adding More Tests

To add more tests:

1. For PowerShell, add a new test section to `integration-test.ps1`
2. For Bash, add a new test function to `deployment-verification.sh` and call it from the main test runner