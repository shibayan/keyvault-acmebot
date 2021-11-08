# Key Vault Acmebot

![Build](https://github.com/shibayan/keyvault-acmebot/workflows/Build/badge.svg)
[![Release](https://img.shields.io/github/release/shibayan/keyvault-acmebot.svg)](https://github.com/shibayan/keyvault-acmebot/releases/latest)
[![License](https://img.shields.io/github/license/shibayan/keyvault-acmebot.svg)](https://github.com/shibayan/keyvault-acmebot/blob/master/LICENSE)
[![Terraform Registry](https://img.shields.io/badge/terraform-registry-5c4ee5.svg)](https://registry.terraform.io/modules/shibayan/keyvault-acmebot/azurerm/latest)

This application automates the issuance and renewal of ACME SSL/TLS certificates. The certificates are stored inside Azure Key Vault. Many Azure services such as Azure App Service, Application Gateway, CDN, etc. are able to import certificates directly from Key Vault.

We have started to address the following requirements:

- Use the Azure Key Vault to store SSL/TLS certificates securely
- Centralize management of a large number of certificates using a single Key Vault
- Easy to deploy and configure solution
- Highly reliable implementation
- Ease of Monitoring (Application Insights, Webhook)

Key Vault allows for secure and centralized management of ACME certificates.

## Table Of Contents

- [Feature Support](#feature-support)
- [Requirements](#requirements)
- [Getting Started](#getting-started)
- [Usage](#usage)
- [Frequently Asked Questions](#frequently-asked-questions)
- [Thanks](#thanks)
- [Sponsors](#sponsors)
- [License](#license)

## Feature Support

- All Azure App Services (Web Apps / Functions / Containers, regardless of OS)
- Azure CDN and Front Door
- Azure Application Gateway v2
- Issuing certificates for Wildcard and Zone Apex
- Issuing certificates with SANs (subject alternative names) (one certificate for multiple domains)
- Automated certificate renewal
- ACME v2 compliants Certification Authorities
  - [Let's Encrypt](https://letsencrypt.org/)
  - [Buypass Go SSL](https://www.buypass.com/ssl/resources/acme-free-ssl)
  - [ZeroSSL](https://zerossl.com/features/acme/) (Requires EAB Credentials)

## Requirements

You will need the following:

- Azure Subscription (required to deploy this solution)
- Azure Key Vault (existing one or new Key Vault can be created at deployment time)
- DNS provider (required to host your public DNS zone)
- Email address (required to register with ACME)

## Getting Started

### 1. Deploy Acmebot

| Azure (Public) | Azure China | Azure Government |
| :---: | :---: | :---: |
| <a href="https://portal.azure.com/#create/Microsoft.Template/uri/https%3A%2F%2Fraw.githubusercontent.com%2Fshibayan%2Fkeyvault-acmebot%2Fmaster%2Fazuredeploy.json" target="_blank"><img src="https://aka.ms/deploytoazurebutton" /></a> | <a href="https://portal.azure.cn/#create/Microsoft.Template/uri/https%3A%2F%2Fraw.githubusercontent.com%2Fshibayan%2Fkeyvault-acmebot%2Fmaster%2Fazuredeploy.json" target="_blank"><img src="https://aka.ms/deploytoazurebutton" /></a> | <a href="https://portal.azure.us/#create/Microsoft.Template/uri/https%3A%2F%2Fraw.githubusercontent.com%2Fshibayan%2Fkeyvault-acmebot%2Fmaster%2Fazuredeploy.json" target="_blank"><img src="https://aka.ms/deploytoazurebutton" /></a> |

### 2. Add application settings

Update the following configuration settings of the Function App:

- `Acmebot:VaultBaseUrl`
  - DNS name of the Azure Key Vault (if you are using an existing Key Vault)
- `Acmebot:Webhook`
  - Webhook destination URL (optional, Slack and Microsoft Teams are recommended)
  - Message will be sent when the process succeeds or fails

There are also additional settings that will be automatically created by Key Vault Acmebot:

- `Acmebot:Endpoint`
  - The ACME endpoint used to issue certificates
- `Acmebot:Contacts`
  - The email address (required) used in ACME account registration

### 3. Add settings for your choice DNS provider

For instructions on how to configure each DNS provider, please refer to the following page.

https://github.com/shibayan/keyvault-acmebot/wiki/DNS-Provider-Configuration

#### Supported DNS providers

- Amazon Route 53
- Azure DNS
- Cloudflare
- DNS Made Easy
- Gandi LiveDNS
- GoDaddy
- Google Cloud DNS
- GratisDNS
- TransIP DNS

### 4. Enable App Service Authentication

You must enable Authentication on the Function App that is deployed as part of this application.

In the Azure Portal, open the Function blade then select the `Authentication` menu and enable App Service authentication. Click on the `Add identity provider` button to display the screen for adding a new identity provider. If you select `Microsoft` as your Identity provider, the required settings will be automatically filled in for you. The default settings are fine.

![Add an Identity provider](https://user-images.githubusercontent.com/1356444/117532648-79e00300-b023-11eb-8cf1-92a11ffb115a.png)

Make sure that the App Service Authentication setting is set to `Require authentication`. The permissions can basically be left at the default settings.

![App Service Authentication settings](https://user-images.githubusercontent.com/1356444/117532660-8c5a3c80-b023-11eb-8573-df2e418d5c2f.png)

If you are using Sovereign Cloud, you may not be able to select Express. Enable authentication from the advanced settings with reference to the following document.

https://docs.microsoft.com/en-us/azure/app-service/configure-authentication-provider-aad#-configure-with-advanced-settings

Finally, you can save your previous settings to enable App Service authentication.

### 5. Add to Key Vault access policies (if you use an existing Key Vault)

Open the access policy of the Key Vault and add the `Certificate management` access policy for the deployed application.

![image](https://user-images.githubusercontent.com/1356444/46597665-19f7e780-cb1c-11e8-9cb3-82e706d5dfd6.png)

## Usage

### Manage certificates

Access `https://YOUR-FUNCTIONS.azurewebsites.net/dashboard` with a browser, you will see a list of certificates managed by Acmebot. From this dashboard, you can easily issue new certificates and renew and revoke existing ones.

![dashboard](https://user-images.githubusercontent.com/1356444/140616620-5e961f22-fdb6-4414-9556-5d4a003cd223.png)

The dashboard feature is currently in preview. If you have any bugs or feature suggestions, please let us know in issue [#341](https://github.com/shibayan/keyvault-acmebot/issues/341).

### Automatic renew an existing certificate

All existing ACME certificates are automatically renewed 30 days before their expiration. The default check timing is 00:00 UTC. If you need to change the time zone, use `WEBSITE_TIME_ZONE` to set the time zone.

### How to use the issued certificate in Azure services

See also https://github.com/shibayan/keyvault-acmebot/wiki/How-to-use-in-Azure-services

## Frequently Asked Questions

See also https://github.com/shibayan/keyvault-acmebot/wiki/Frequently-Asked-Questions

## Thanks

- [ACMESharp Core](https://github.com/PKISharp/ACMESharpCore) by @ebekker
- [Durable Functions](https://github.com/Azure/azure-functions-durable-extension) by @cgillum and contributors
- [DnsClient.NET](https://github.com/MichaCo/DnsClient.NET) by @MichaCo

## Sponsors

[![ZEN Architects](docs/images/zenarchitects.png)](https://zenarchitects.co.jp)

## License

This project is licensed under the [Apache License 2.0](https://github.com/shibayan/keyvault-acmebot/blob/master/LICENSE)
