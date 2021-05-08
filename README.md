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
- [License](#license)

## Feature Support

- All Azure App Services (Web Apps / Functions / Containers, regardless of OS)
- Azure CDN and Front Door
- Azure Application Gateway v2
- Issuing certificates with SANs (subject alternative names) (one certificate for multiple domains)
- Issuing certificates and wildcard certificates for Zone Apex domains
- Automated certificate renewal
- ACME-compliant Certification Authorities
  - [Let's Encrypt](https://letsencrypt.org/)
  - [Buypass Go SSL](https://www.buypass.com/ssl/resources/acme-free-ssl)
  - [ZeroSSL](https://zerossl.com/features/acme/) (Requires EAB Credentials)

## Requirements

You will need the following:

- Azure Subscription (required to deploy this solution)
- Azure Key Vault (existing one or new Key Vault can be created at deployment time)
- DNS provider (required to host your public DNS zone)
  - Amazon Route 53
  - Azure DNS (The resource must be unlocked)
  - Cloudflare
  - DNS Made Easy
  - GoDaddy
  - Google Cloud DNS
  - GratisDNS
  - TransIP DNS
- Email address (required to register with ACME)

## Getting Started

### 1. Deploy Acmebot

For Azure Cloud

<a href="https://portal.azure.com/#create/Microsoft.Template/uri/https%3A%2F%2Fraw.githubusercontent.com%2Fshibayan%2Fkeyvault-acmebot%2Fmaster%2Fazuredeploy.json" target="_blank">
  <img src="https://aka.ms/deploytoazurebutton" />
</a>

For Azure China

<a href="https://portal.azure.cn/#create/Microsoft.Template/uri/https%3A%2F%2Fraw.githubusercontent.com%2Fshibayan%2Fkeyvault-acmebot%2Fmaster%2Fazuredeploy.json" target="_blank">
  <img src="https://aka.ms/deploytoazurebutton" />
</a>

For Azure Government

<a href="https://portal.azure.us/#create/Microsoft.Template/uri/https%3A%2F%2Fraw.githubusercontent.com%2Fshibayan%2Fkeyvault-acmebot%2Fmaster%2Fazuredeploy.json" target="_blank">
  <img src="https://aka.ms/deploytoazurebutton" />
</a>

### 2. Add application settings

Update the following configuration settings of the Function App:

- `Acmebot:VaultBaseUrl`
  - DNS name of the Azure Key Vault (if you are using an existing Key Vault)
- `Acmebot:Webhook`
  - Webhook destination URL (optional, Slack and Microsoft Teams are recommended)

There are also additional settings that will be automatically created by Key Vault Acmebot:

- `Acmebot:Endpoint`
  - The ACME endpoint used to issue certificates
- `Acmebot:Contacts`
  - The email address (required) used in ACME account registration

### 3. Add settings for your choice DNS provider

For instructions on how to configure each DNS provider, please refer to the following page.

https://github.com/shibayan/keyvault-acmebot/wiki/DNS-Provider-Configuration

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

### Issue a new certificate

Access `https://YOUR-FUNCTIONS.azurewebsites.net/add-certificate` with a browser and authenticate with Azure Active Directory and the Web UI will be displayed. Select the target domain from that screen, add the required subdomains, and run, and after a few tens of seconds, the certificate will be issued.

![Add certificate](https://user-images.githubusercontent.com/1356444/64176075-9b283d80-ce97-11e9-8ee7-02530d0c03f2.png)

If the `Access Control (IAM)` setting is not correct, nothing will be shown in the drop-down list.

### Renew an existing certificate

All existing ACME certificates are automatically renewed 30 days before their expiration.

The default check timing is 00:00 UTC. If you need to change the time zone, use `WEBSITE_TIME_ZONE` to set the time zone.

### How to use the issued certificate in Azure services

#### App Service (Web Apps / Functions / Containers)

You can import the Key Vault certificate to the App Service by opening the `TLS/SSL Settings` from Azure Portal and selecting the `Import Key Vault Certificate` button from the `Private Key Certificate (.pfx)`.

![image](https://user-images.githubusercontent.com/1356444/64438173-974c2380-d102-11e9-88c0-5ed34a5ce42a.png)

After importing, the App Service will automatically check for certificate updates.

#### Application Gateway v2

- https://docs.microsoft.com/en-us/azure/application-gateway/key-vault-certs

#### Azure CDN / Front Door

Azure CDN / Front Door will now automatically deploy the latest version of the certificate when the Key Vault certificate is updated. Selecting `Latest` as the Key Vault certificate version will automatically update it.

- https://docs.microsoft.com/en-us/azure/cdn/cdn-custom-ssl?tabs=option-2-enable-https-with-your-own-certificate
- https://docs.microsoft.com/en-us/azure/frontdoor/front-door-custom-domain-https#option-2-use-your-own-certificate

#### API Management

- https://docs.microsoft.com/en-us/azure/api-management/configure-custom-domain

#### Other services

The issued certificate can be downloaded from Key Vault and used elsewhere, either in Azure or outside Azure.

## Frequently Asked Questions

### Remove a Certificate

To Remove a certificate from the system delete it from the Key Vault. Key Vault Acmebot will no longer renew the certificate.

### Reinstalling Or Updating Key Vault Acmebot

To Reinstall or Upgrade Key Vault Acmebot without removing your certificates, ensure that the Key Vault is not removed. Key Vault Acmebot will use the exisiting certificates and vault after upgrade or reinstall

## Thanks

- [ACMESharp Core](https://github.com/PKISharp/ACMESharpCore) by @ebekker
- [Durable Functions](https://github.com/Azure/azure-functions-durable-extension) by @cgillum and contributors
- [DnsClient.NET](https://github.com/MichaCo/DnsClient.NET) by @MichaCo

## License

This project is licensed under the [Apache License 2.0](https://github.com/shibayan/keyvault-acmebot/blob/master/LICENSE)
