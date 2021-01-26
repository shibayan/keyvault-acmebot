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

## Announcements

### Upgrade to Acmebot v3

Key Vault Acmebot v3 has been released since December 31, 2019. Users deploying earlier than this are encouraged to upgrade to v3 by following the ugprade process described here:

https://github.com/shibayan/keyvault-acmebot/issues/80

### Automate Azure CDN / Front Door certificates deployment

As of August 2020, Azure CDN / Front Door does not automatically deploy new Key Vault certificates. I develop an utility application to automatically deploy a new version of the certificate.

https://github.com/shibayan/keyvault-certificate-rotation

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

## Requirements

You will need the following:

- Azure Subscription (required to deploy this solution)
- Azure Key Vault (existing one or new Key Vault can be created at deployment time)
- DNS provider (required to host your public DNS zone)
  - Azure DNS (The resource must be unlocked)
  - Cloudflare
  - Google Cloud DNS
  - GratisDNS
  - TransIP DNS
  - DNS Made Easy
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

- Acmebot:VaultBaseUrl
  - DNS name of the Azure Key Vault (if you are using an existing Key Vault)
- Acmebot:Webhook
  - Webhook destination URL (optional, Slack and Microsoft Teams are recommended)

There are also additional settings that will be automatically created by Key Vault Acmebot
- Acmebot:Endpoint
  - The ACME endpoint used to issue certificates
- Acmebot:Contacts
  - The email address (required) used in ACME account registration
- Acmebot:AzureDns:SubscriptionId
  - Your Azure subscription ID in order to identify which DNS resources can be used for ACME

### 3. Enable App Service Authentication

You must enable Authentication on the Function App that is deployed as part of this application.

In the Azure Portal, open the Function blade then select the `Authentication / Authorization` menu and enable App Service authentication. Select the `Login with Azure Active Directory` as the action to perform if the request is not authenticated. We recommend using Azure Active Directory as your authentication provider, but it works with other providers as well, although it's not supported.

![Enable App Service Authentication with AAD](https://user-images.githubusercontent.com/1356444/49693401-ecc7c400-fbb4-11e8-9ae1-5d376a4d8a05.png)

Select Azure Active Directory as the authentication provider, select `Express` as the management mode, and select OK.

![Create New Azure AD App](https://user-images.githubusercontent.com/1356444/49693412-6f508380-fbb5-11e8-81fb-6bbcbe47654e.png)

Finally, you can save your previous settings to enable App Service authentication.

### 4. Add access control (IAM) to Azure DNS

Open the `Access Control (IAM)` of the target DNS zone or resource group containing the DNS zone, and assign the role of `DNS Zone Contributor` to the deployed application.

![temp](https://user-images.githubusercontent.com/1356444/64354572-a9628f00-d03a-11e9-93c9-0c12992ca9bf.png)

When using a DNS provider other than Azure DNS, please refer to the following page for configuration.

https://github.com/shibayan/keyvault-acmebot/wiki/DNS-Provider-Configuration

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

#### Azure CDN

- https://docs.microsoft.com/en-us/azure/cdn/cdn-custom-ssl?tabs=option-2-enable-https-with-your-own-certificate

#### Azure Front Door

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
