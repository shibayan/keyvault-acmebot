# Key Vault Acmebot - Copilot Overview

> **Note**: This file is generated and maintained by GitHub Copilot for AI code assistance. It does not replace the official wiki or user documentation.

## Project Summary

Key Vault Acmebot is an Azure Functions-based solution that automates ACME SSL/TLS certificate issuance and management using Azure Key Vault as the central certificate store.

## Technical Stack

- **Runtime**: .NET 6.0
- **Platform**: Azure Functions v4
- **Primary Dependencies**:
  - Azure.Security.KeyVault.Certificates (4.7.0)
  - Azure.Security.KeyVault.Keys (4.7.0)
  - Microsoft.Azure.WebJobs.Extensions.DurableTask (3.1.0)
  - ACMESharp (Custom fork)
  - DnsClient (1.8.0)

## Architecture

- **Type**: Serverless Azure Functions application
- **Pattern**: Durable Functions for long-running certificate workflows
- **Storage**: Azure Key Vault for certificate storage
- **Authentication**: Azure Identity with managed identity support

## Key Features

### ACME Certificate Management
- Automated certificate issuance and renewal
- Support for multiple ACME CAs (Let's Encrypt, Buypass, ZeroSSL, Google Trust Services, SSL.com, Entrust)
- Certificate types: Zone Apex, Wildcard, SANs (multiple domains)

### DNS Providers
Based on package references:
- Azure DNS (Azure.ResourceManager.Dns, Azure.ResourceManager.PrivateDns)
- AWS Route 53 (AWSSDK.Route53)
- Google Cloud DNS (Google.Apis.Dns.v1)

### Azure Service Integration
- App Services (Web Apps/Functions/Containers)
- Container Apps
- Front Door (Standard/Premium)
- Application Gateway v2
- API Management
- SignalR Service (Premium)
- Virtual Machines

## Project Structure

### Main Application
- **KeyVault.Acmebot.csproj**: Main Azure Functions project
- **ACMESharpCore**: Custom ACME protocol implementation (submodule/reference)

### Web Interface
- **wwwroot/**: Static web files for management dashboard

### Configuration
- **host.json**: Azure Functions host configuration
- **local.settings.json**: Local development settings

## Development Considerations

### Dependencies Management
- Uses Azure SDK v4+ packages
- Custom ACMESharp implementation via project reference
- HTTP client extensions for external API calls
- Durable Functions for workflow orchestration

### Deployment
- ARM template deployment supported
- Multi-cloud Azure support (Public, China, Government)
- Terraform registry module available

### Monitoring & Observability
- Application Insights integration
- Webhook support for notifications
- Dedicated dashboard for certificate management

## Code Patterns to Expect

### Azure Functions Patterns
- HTTP triggered functions for API endpoints
- Timer triggered functions for renewal schedules
- Durable orchestrator functions for certificate workflows
- Activity functions for individual certificate operations

### Azure SDK Usage
- Key Vault certificate and key operations
- DNS zone management for domain validation
- Azure Resource Manager operations

### ACME Protocol Implementation
- Certificate signing requests
- Domain validation (DNS-01 challenge)
- Account management and registration
- Certificate renewal automation

## Security Considerations

- Managed Identity authentication
- Key Vault access policies
- ACME account key management
- DNS provider credential handling
- EAB (External Account Binding) support for enterprise CAs

## Common File Types

- `.cs`: C# Azure Functions and business logic
- `.json`: Configuration files (host.json, local.settings.json)
- `.html/.js/.css`: Dashboard web interface files
- `.md`: Documentation files
- `.yml/.yaml`: GitHub Actions workflows
- `.json`: ARM deployment templates
```
