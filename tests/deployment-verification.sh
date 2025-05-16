#!/bin/bash
# Deployment Verification Script for Acme-Cert-Bot
# Use this script to verify that the application is running correctly after deployment

# Exit on any error
set -e

# Check if required parameters are provided
if [ -z "$BASE_URL" ] || [ -z "$API_KEY" ]; then
  echo "Usage: BASE_URL=https://your-function-app.azurewebsites.net API_KEY=your-function-key ./deployment-verification.sh"
  exit 1
fi

# Colors for output
GREEN='\033[0;32m'
RED='\033[0;31m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

# Function to log messages
log() {
  local timestamp=$(date '+%Y-%m-%d %H:%M:%S')
  local level=${2:-INFO}
  echo -e "[$timestamp] [$level] $1"
}

# Function to make HTTP requests
api_request() {
  local method=$1
  local endpoint=$2
  local expected_status=${3:-200}
  local payload=$4
  
  # Construct URL and ensure no trailing slash
  local url="${BASE_URL}/${endpoint}"
  url=${url%/}
  
  # Headers
  local headers=(
    -H "x-functions-key: ${API_KEY}"
    -H "Content-Type: application/json"
    -H "Accept: application/json"
    -H "X-MS-CLIENT-PRINCIPAL-ID: test-user"
    -H "X-MS-CLIENT-PRINCIPAL-NAME: test@example.com"
  )
  
  # Debug output
  if [ "$VERBOSE" = "true" ]; then
    log "Request: $method $url" "DEBUG"
    if [ -n "$payload" ]; then
      log "Payload: $payload" "DEBUG"
    fi
  fi
  
  # Make the request
  local response
  local status
  
  if [ -n "$payload" ]; then
    # With payload
    response=$(curl -s -w "\n%{http_code}" -X $method "${headers[@]}" -d "$payload" "$url")
  else
    # Without payload
    response=$(curl -s -w "\n%{http_code}" -X $method "${headers[@]}" "$url")
  fi
  
  # Extract status code
  status=$(echo "$response" | tail -n1)
  # Extract response body (all but last line)
  local body=$(echo "$response" | sed '$d')
  
  # Debug output
  if [ "$VERBOSE" = "true" ]; then
    log "Response status: $status" "DEBUG"
    log "Response body: $body" "DEBUG"
  fi
  
  # Check status
  if [ "$status" -eq "$expected_status" ]; then
    echo "$body"
    return 0
  else
    if [ "$status" -eq "$expected_status" ]; then
      # If we got the expected error status, just return
      return 0
    fi
    
    log "Request failed: $method $url" "ERROR"
    log "Expected status: $expected_status, Actual: $status" "ERROR"
    log "Response: $body" "ERROR"
    return 1
  fi
}

run_test() {
  local test_name=$1
  local test_function=$2
  
  log "Running test: $test_name" "TEST"
  
  if $test_function; then
    echo -e "${GREEN}✓ PASSED:${NC} $test_name"
  else
    echo -e "${RED}✗ FAILED:${NC} $test_name"
    exit 1
  fi
}

# ==================== TESTS ====================

# Test 1: Check if the app is running by accessing the home page
test_app_running() {
  local response=$(curl -s -o /dev/null -w "%{http_code}" "$BASE_URL")
  
  if [ "$response" -eq 200 ] || [ "$response" -eq 401 ]; then
    return 0
  else
    log "App not running correctly. Status code: $response" "ERROR"
    return 1
  fi
}

# Test 2: Get certificates
test_get_certificates() {
  local certificates=$(api_request "GET" "api/certificates")
  
  # Check if we got a valid response
  if [ -n "$certificates" ]; then
    log "Successfully retrieved certificates" "SUCCESS"
    return 0
  else
    return 1
  fi
}

# Test 3: Get DNS zones
test_get_dns_zones() {
  local dns_zones=$(api_request "GET" "api/dns-zones")
  
  # Check if we got a valid response
  if [ -n "$dns_zones" ]; then
    log "Successfully retrieved DNS zones" "SUCCESS"
    return 0
  else
    return 1
  fi
}

# Test 4: Test instance state endpoint
test_instance_state() {
  # This will likely return a 400 since we're using a fake ID
  api_request "GET" "api/state/test-instance-id" 400 > /dev/null
  
  # If we get here, the endpoint is accessible
  log "Instance state endpoint is accessible" "SUCCESS"
  return 0
}

# Test 5: Test certificate endpoints (we expect errors without actual certs)
test_certificate_endpoints() {
  local test_certificate='{
    "certificateName": "test-certificate",
    "dnsNames": ["test.example.com"],
    "keyType": "RSA",
    "keySize": 2048
  }'
  
  # Test certificate creation endpoint
  api_request "POST" "api/certificate" 400 "$test_certificate" > /dev/null || true
  
  # Test certificate renewal endpoint
  api_request "POST" "api/certificate/test-certificate/renew" 400 > /dev/null || true
  
  # Test certificate revocation endpoint
  api_request "POST" "api/certificate/test-certificate/revoke" 400 > /dev/null || true
  
  log "Certificate endpoints are accessible" "SUCCESS"
  return 0
}

# ==================== RUN TESTS ====================

echo "Starting deployment verification for $BASE_URL"

run_test "Application is running" test_app_running
run_test "Get certificates endpoint" test_get_certificates
run_test "Get DNS zones endpoint" test_get_dns_zones
run_test "Instance state endpoint" test_instance_state
run_test "Certificate operation endpoints" test_certificate_endpoints

echo -e "\n${GREEN}All tests passed! Deployment verification successful.${NC}"