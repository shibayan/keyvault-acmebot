<h1 align="center">
  Key Vault Acmebot
</h1>
<p align="center">
  Automated ACME SSL/TLS certificates issuer for Azure Key Vault (App Service / Container Apps / App Gateway / Front Door / CDN / others)
</p>
<p align="center">
  <a href="https://github.com/shibayan/keyvault-acmebot/actions/workflows/build.yml" rel="nofollow"><img src="https://github.com/shibayan/keyvault-acmebot/workflows/Build/badge.svg" alt="Build" style="max-width: 100%;"></a>
  <a href="https://github.com/shibayan/keyvault-acmebot/releases/latest" rel="nofollow"><img src="https://badgen.net/github/release/shibayan/keyvault-acmebot" alt="Release" style="max-width: 100%;"></a>
  <a href="https://github.com/shibayan/keyvault-acmebot/stargazers" rel="nofollow"><img src="https://badgen.net/github/stars/shibayan/keyvault-acmebot" alt="Stargazers" style="max-width: 100%;"></a>
  <a href="https://github.com/shibayan/keyvault-acmebot/network/members" rel="nofollow"><img src="https://badgen.net/github/forks/shibayan/keyvault-acmebot" alt="Forks" style="max-width: 100%;"></a>
  <a href="https://github.com/shibayan/keyvault-acmebot/blob/master/LICENSE"><img src="https://badgen.net/github/license/shibayan/keyvault-acmebot" alt="License" style="max-width: 100%;"></a>
  <a href="https://registry.terraform.io/modules/shibayan/keyvault-acmebot/azurerm/latest" rel="nofollow"><img src="https://badgen.net/badge/terraform/registry/5c4ee5" alt="Terraform" style="max-width: 100%;"></a>
  <br>
  <a href="https://github.com/shibayan/keyvault-acmebot/commits/master" rel="nofollow"><img src="https://badgen.net/github/last-commit/shibayan/keyvault-acmebot" alt="Last commit" style="max-width: 100%;"></a>
  <a href="https://github.com/shibayan/keyvault-acmebot/wiki" rel="nofollow"><img src="https://badgen.net/badge/documentation/available/ff7733" alt="Documentation" style="max-width: 100%;"></a>
  <a href="https://github.com/shibayan/keyvault-acmebot/discussions" rel="nofollow"><img src="https://badgen.net/badge/discussions/welcome/ff7733" alt="Discussions" style="max-width: 100%;"></a>
</p>

## Motivation

We have begun to address the following requirements:

- Securely store SSL/TLS certificates with Azure Key Vault
- Centralize management of large numbers of certificates with a single Key Vault
- Easy to deploy and configure solution
- Highly reliable implementation
- Easy to monitor (Application Insights, Webhook)

Key Vault Acmebot provides secure and centralized management of ACME certificates.

## Feature Support

- Issue certificates for Zone Apex, Wildcard and SANs (multiple domains)
- Dedicated dashboard for easy certificate management
- Automated certificate renewal
- Support for ACME v2 compliant Certification Authorities
  - [Let's Encrypt](https://letsencrypt.org/)
  - [Buypass Go SSL](https://www.buypass.com/ssl/resources/acme-free-ssl)
  - [ZeroSSL](https://zerossl.com/features/acme/) (Requires EAB Credentials)
  - [Google Trust Services](https://pki.goog/) (Requires EAB Credentials)
  - [SSL.com](https://www.ssl.com/how-to/order-free-90-day-ssl-tls-certificates-with-acme/) (Requires EAB Credentials)
  - [Entrust](https://www.entrust.com/) (Requires EAB Credentials)
- Certificates can be used with many Azure services
  - Azure App Services (Web Apps / Functions / Containers)
  - Azure Container Apps (Include custom DNS suffix)
  - Front Door (Standard / Premium)
  - Application Gateway v2
  - API Management
  - SignalR Service (Premium)
  - Virtual Machine

## Running with Docker Compose

You can build and run KeyVault.Acmebot using Docker Compose. This is useful for local development and testing, or for deploying in containerized environments.

**Prerequisites:**

*   Docker Desktop installed and running.
*   Azure CLI installed and configured (if you plan to use Azure CLI for authentication locally).

**Steps:**

1.  **Clone the repository:**
    ```bash
    git clone https://github.com/shibayan/keyvault-acmebot.git
    cd keyvault-acmebot
    ```
    If you have forked the repository, clone your fork instead.

2.  **Configure Environment Variables:**
    Before running the application, you need to set up the necessary environment variables. The `docker-compose.yml` file contains placeholders for these variables. You can either:
    *   Modify the `docker-compose.yml` file directly (not recommended for sensitive data).
    *   Create a `.env` file in the root of the project. Docker Compose automatically loads variables from a `.env` file.

    **Example `.env` file:**
    ```env
    # Azure Storage Connection String (required for some function triggers, e.g., timers)
    # For local development, you can use Azurite (Azure Storage Emulator)
    AzureWebJobsStorage=DefaultEndpointsProtocol=http;AccountName=devstoreaccount1;AccountKey=Eby8vdM02xNOcqFlqUwJPLlmEtlCDXJ1OUzFT50uSRZ6IFsuFq2UVErCz4I6tq/K1SZFPTOtr/KBHBeksoGMGw==;BlobEndpoint=http://127.0.0.1:10000/devstoreaccount1;QueueEndpoint=http://127.0.0.1:10001/devstoreaccount1;

    # Key Vault URI
    KeyVault:VaultUri=https://your-key-vault-name.vault.azure.net/

    # Azure Subscription and Tenant ID
    Acmebot:SubscriptionId=your-azure-subscription-id
    Acmebot:TenantId=your-azure-tenant-id

    # Authentication Method
    # Choose ONE of the following methods:

    # 1. Managed Identity (recommended for Azure hosted environments)
    # If using system-assigned managed identity:
    # Azul:ManagedIdentity=system
    # If using user-assigned managed identity (provide the Client ID):
    # Azul:ManagedIdentity=your-user-assigned-identity-client-id

    # 2. Service Principal
    # Azul:ServicePrincipal:ClientId=your-service-principal-client-id
    # Azul:ServicePrincipal:ClientSecret=your-service-principal-client-secret
    # Azul:ServicePrincipal:TenantId=your-azure-tenant-id # Often same as Acmebot:TenantId

    # 3. Azure CLI (for local development ONLY - ensure you are logged in via `az login`)
    # Azul:AuthenticationMode=AzureCLI

    # General Acmebot settings
    Acmebot:Contacts=mailto:youremail@example.com # Required by Let's Encrypt
    # Acmebot:AcmeEndpoint=https://acme-v02.api.letsencrypt.org/directory # Default is Let's Encrypt production
    # Acmebot:Webhook=your-webhook-url # Optional: For notifications

    # DNS Provider specific settings (example for Azure DNS)
    # Ensure the identity being used has "DNS Zone Contributor" role on the DNS zone(s)
    # Acmebot:AzureDns:SubscriptionId=your-dns-zone-subscription-id
    # Acmebot:AzureDns:ResourceGroup=your-dns-zone-resource-group
    # Acmebot:AzureDns:TenantId=your-dns-zone-tenant-id # Often same as Acmebot:TenantId
    ```
    **Important:**
    *   Replace placeholder values with your actual configuration.
    *   For `AzureWebJobsStorage` in local development, you can use the Azurite emulator. The example above shows a common Azurite connection string.
    *   Ensure the identity used (Managed Identity or Service Principal) has the necessary permissions (e.g., `Get` and `List` for secrets and certificates) on your Azure Key Vault.
    *   Configure the specific environment variables for your chosen DNS provider(s). Refer to the official KeyVault-Acmebot documentation for details on each provider.

3.  **Build and Run the Container:**
    Open a terminal in the root of the project and run:
    ```bash
    docker-compose up --build
    ```
    This command will:
    *   Build the Docker image for KeyVault.Acmebot based on the `Dockerfile`.
    *   Start the container.
    *   The Functions host will start, and you should see output indicating the available HTTP endpoints if any are HTTP triggered.

4.  **Accessing the Functions:**
    If your functions are HTTP triggered, they will typically be available at `http://localhost:8080/api/FunctionName`. For example, the dashboard might be at `http://localhost:8080/api/Dashboard`.

5.  **Stopping the Container:**
    Press `Ctrl+C` in the terminal where `docker-compose up` is running. To remove the container (and network if created solely for this), you can run:
    ```bash
    docker-compose down
    ```

**Local Development with Azure CLI Authentication:**

If you set `Azul:AuthenticationMode=AzureCLI` in your environment variables, Docker Compose will attempt to use your local Azure CLI login. For this to work from within the container, the `docker-compose.yml` file has a commented-out `volumes` section:
```yaml
    # volumes:
      # - ~/.azure:/root/.azure:ro
```
Uncomment this section to mount your local Azure CLI configuration directory into the container as read-only. **This method is for local development convenience and should not be used for production deployments.**

## Deployment

| Azure (Public) | Azure China | Azure Government |
| :---: | :---: | :---: |
| <a href="https://portal.azure.com/#create/Microsoft.Template/uri/https%3A%2F%2Fraw.githubusercontent.com%2Fshibayan%2Fkeyvault-acmebot%2Fmaster%2Fazuredeploy.json" target="_blank"><img src="https://aka.ms/deploytoazurebutton" /></a> | <a href="https://portal.azure.cn/#create/Microsoft.Template/uri/https%3A%2F%2Fraw.githubusercontent.com%2Fshibayan%2Fkeyvault-acmebot%2Fmaster%2Fazuredeploy.json" target="_blank"><img src="https://aka.ms/deploytoazurebutton" /></a> | <a href="https://portal.azure.us/#create/Microsoft.Template/uri/https%3A%2F%2Fraw.githubusercontent.com%2Fshibayan%2Fkeyvault-acmebot%2Fmaster%2Fazuredeploy.json" target="_blank"><img src="https://aka.ms/deploytoazurebutton" /></a> |

Learn more at https://github.com/shibayan/keyvault-acmebot/wiki/Getting-Started

## Sponsors

[![ZEN Architects](docs/images/zenarchitects.png)](https://zenarchitects.co.jp)

Thank you for your support of our development. Interested in special support? [Become a Sponsor](https://github.com/sponsors/shibayan)

## Thanks

- [ACMESharp Core](https://github.com/PKISharp/ACMESharpCore) by @ebekker
- [Durable Functions](https://github.com/Azure/azure-functions-durable-extension) by @cgillum and contributors
- [DnsClient.NET](https://github.com/MichaCo/DnsClient.NET) by @MichaCo

## License

This project is licensed under the [Apache License 2.0](https://github.com/shibayan/keyvault-acmebot/blob/master/LICENSE)
