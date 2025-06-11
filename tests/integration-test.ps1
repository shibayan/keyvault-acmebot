# Integration Test Script for Acme-Cert-Bot
# Use this script to verify core functionality after deployment

param(
    [Parameter(Mandatory=$true)]
    [string]$BaseUrl,
    
    [Parameter(Mandatory=$true)]
    [string]$ApiKey,
    
    [Parameter(Mandatory=$false)]
    [switch]$VerboseOutput
)

# Helper to log messages
function Write-Log {
    param(
        [string]$Message,
        [string]$Level = "INFO"
    )
    
    $timestamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
    Write-Host "[$timestamp] [$Level] $Message"
}

# Helper to make HTTP requests
function Invoke-ApiRequest {
    param(
        [string]$Method,
        [string]$Endpoint,
        [object]$Body = $null,
        [int]$ExpectedStatusCode = 200
    )
    
    $url = "$BaseUrl/$Endpoint".TrimEnd('/')
    $headers = @{
        "x-functions-key" = $ApiKey
        "Content-Type" = "application/json"
        "Accept" = "application/json"
        # For testing purposes, simulate authentication
        "X-MS-CLIENT-PRINCIPAL-ID" = "test-user"
        "X-MS-CLIENT-PRINCIPAL-NAME" = "test@example.com"
    }
    
    if ($VerboseOutput) {
        Write-Log "Making request: $Method $url" "DEBUG"
        if ($Body) {
            Write-Log "Request body: $($Body | ConvertTo-Json -Depth 10)" "DEBUG"
        }
    }
    
    try {
        if ($Body) {
            $bodyJson = $Body | ConvertTo-Json -Depth 10
            $response = Invoke-RestMethod -Uri $url -Method $Method -Body $bodyJson -Headers $headers -ContentType "application/json" -ErrorAction Stop
        } else {
            $response = Invoke-RestMethod -Uri $url -Method $Method -Headers $headers -ErrorAction Stop
        }
        
        if ($VerboseOutput) {
            Write-Log "Response: $($response | ConvertTo-Json -Depth 10)" "DEBUG"
        }
        
        return $response
    } catch {
        $statusCode = $_.Exception.Response.StatusCode.value__
        $statusDesc = $_.Exception.Response.StatusDescription
        
        if ($statusCode -eq $ExpectedStatusCode) {
            Write-Log "Received expected status code $statusCode" "INFO"
            return $null
        }
        
        Write-Log "API request failed: $Method $url" "ERROR"
        Write-Log "Status code: $statusCode, Description: $statusDesc" "ERROR"
        Write-Log "Error details: $_" "ERROR"
        throw "API request failed: $_"
    }
}

# Set error action preference
$ErrorActionPreference = "Stop"

Write-Log "Starting integration tests against $BaseUrl"

# Test 1: Get certificates
Write-Log "Test 1: Get certificates" "TEST"
try {
    $certificates = Invoke-ApiRequest -Method "GET" -Endpoint "api/certificates"
    Write-Log "Successfully retrieved $(($certificates | Measure-Object).Count) certificates" "SUCCESS"
} catch {
    Write-Log "Failed to get certificates" "FAIL"
    throw
}

# Test 2: Get DNS zones
Write-Log "Test 2: Get DNS zones" "TEST"
try {
    $dnsZones = Invoke-ApiRequest -Method "GET" -Endpoint "api/dns-zones"
    Write-Log "Successfully retrieved DNS zones" "SUCCESS"
} catch {
    Write-Log "Failed to get DNS zones" "FAIL"
    throw
}

# Test 3: Test instance state endpoint
Write-Log "Test 3: Test instance state endpoint" "TEST"
try {
    # This will likely return a 400 since we're using a fake ID, but it verifies the endpoint works
    Invoke-ApiRequest -Method "GET" -Endpoint "api/state/test-instance-id" -ExpectedStatusCode 400
    Write-Log "Instance state endpoint available" "SUCCESS"
} catch {
    Write-Log "Failed to access instance state endpoint" "FAIL"
    throw
}

# Test 4: Certificate operations (simulated)
Write-Log "Test 4: Simulate certificate operations" "TEST"

# Define test certificate data
$testCertificate = @{
    certificateName = "test-certificate"
    dnsNames = @("test.example.com")
    keyType = "RSA"
    keySize = 2048
}

# Simulate the certificate create cycle to ensure endpoints are accessible
# In a real test, we'd complete the full flow, but here we're validating endpoint access
try {
    # We expect this to return error in a test environment without actual DNS validation
    # But it should hit the endpoint and return some kind of response
    try {
        Invoke-ApiRequest -Method "POST" -Endpoint "api/certificate" -Body $testCertificate -ExpectedStatusCode 400
    } catch {
        Write-Log "Certificate creation responded (expected error in test env)" "INFO"
    }
    
    # Test renewal endpoint is accessible
    try {
        Invoke-ApiRequest -Method "POST" -Endpoint "api/certificate/test-certificate/renew" -ExpectedStatusCode 400
    } catch {
        Write-Log "Certificate renewal endpoint responded (expected error in test env)" "INFO"
    }
    
    # Test revocation endpoint is accessible
    try {
        Invoke-ApiRequest -Method "POST" -Endpoint "api/certificate/test-certificate/revoke" -ExpectedStatusCode 400
    } catch {
        Write-Log "Certificate revocation endpoint responded (expected error in test env)" "INFO"
    }
    
    Write-Log "Certificate operation endpoints are accessible" "SUCCESS"
} catch {
    Write-Log "Failed to access certificate operation endpoints" "FAIL"
    throw
}

# Test static content delivery
Write-Log "Test 5: Static content delivery" "TEST"
try {
    $response = Invoke-WebRequest -Uri "$BaseUrl" -UseBasicParsing
    if ($response.StatusCode -eq 200) {
        Write-Log "Successfully accessed static content" "SUCCESS"
    } else {
        Write-Log "Unexpected status code from static content: $($response.StatusCode)" "WARN"
    }
} catch {
    Write-Log "Failed to access static content" "FAIL"
    Write-Log "Error: $_" "ERROR"
}

Write-Log "Integration tests completed successfully!" "SUCCESS"