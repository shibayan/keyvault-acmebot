# Key Vault Acmebot

[![Build Status](https://dev.azure.com/shibayan/azure-acmebot/_apis/build/status/Build%20keyvault-acmebot?branchName=master)](https://dev.azure.com/shibayan/azure-acmebot/_build/latest?definitionId=38&branchName=master)
[![Release](https://img.shields.io/github/release/shibayan/keyvault-acmebot.svg)](https://github.com/shibayan/keyvault-acmebot/releases/latest)
[![License](https://img.shields.io/github/license/shibayan/keyvault-acmebot.svg)](https://github.com/shibayan/keyvault-acmebot/blob/master/LICENSE)

This is an application to automate the issuance and renewal of [Let's Encrypt](https://letsencrypt.org/) certificates stored in the Azure Key Vault. We have started to address the following requirements:

- Using the Azure Key Vault to store certificates securely
- Centralized management of a large number of certificates using a single Key Vault
- Easy to deploy and configure solution
- Highly reliable implementation
- Ease of Monitoring (Application Insights, Webhook)

Key Vault allows for secure and centralized management of Let's Encrypt certificates.

## Caution

### Upgrading to Acmebot v3

https://github.com/shibayan/keyvault-acmebot/issues/80

## Table Of Contents

- [Feature Support](#feature-support)
- [Requirements](#requirements)
- [Getting Started](#getting-started)
- [Usage](#usage)
- [Thanks](#thanks)
- [License](#license)

## Feature Support

- All Azure App Services (Web Apps / Functions / Containers, regardless of OS)
- Azure CDN and Front Door
- Azure Application Gateway v2
- Issuing certificates with SANs (subject alternative names) (one certificate for multiple domains)
- Issuing certificates and wildcard certificates for Zone Apex domains

## Requirements

- Azure Subscription
- Azure DNS 
- Azure Key Vault (existing one or new Key Vault can be created at deployment time)
- Email address (required to register with Let's Encrypt)

## Getting Started

### 1. Deploy Acmebot

<a href="https://portal.azure.com/#create/Microsoft.Template/uri/https%3A%2F%2Fraw.githubusercontent.com%2Fshibayan%2Fkeyvault-acmebot%2Fmaster%2Fazuredeploy.json" target="_blank">
  <img src="https://azuredeploy.net/deploybutton.png" />
</a>

### 2. Add application settings

Update the following configuration settings of the Function App:
- LetsEncrypt:VaultBaseUrl
  - DNS name of the Azure Key Vault (if you are using an existing Key Vault)
- LetsEncrypt:Webhook
  - Webhook destination URL (optional, Slack and Microsoft Teams are recommended)

### 3. Enabling App Service Authentication

Open the Azure Portal, navigate to the `Authentication / Authorization` menu of the deployed Function App and enable App Service authentication. Select the `Login with Azure Active Directory` as the action to perform if the request is not authenticated. We recommend using Azure Active Directory as your authentication provider, but it works with other providers as well, although it's not supported.

![Enable App Service Authentication with AAD](https://user-images.githubusercontent.com/1356444/49693401-ecc7c400-fbb4-11e8-9ae1-5d376a4d8a05.png)

Select Azure Active Directory as the authentication provider, select `Express` as the management mode, and select OK.

![Create New Azure AD App](https://user-images.githubusercontent.com/1356444/49693412-6f508380-fbb5-11e8-81fb-6bbcbe47654e.png)

Finally, you can save your previous settings to enable App Service authentication.

### 4. Add access control (IAM) to Azure DNS

Open the `Access Control (IAM)` of the target DNS zone or resource group containing the DNS zone, and assign the role of `DNS Zone Contributor` to the deployed application.

![temp](https://user-images.githubusercontent.com/1356444/64354572-a9628f00-d03a-11e9-93c9-0c12992ca9bf.png)

### 5. Add to Key Vault access policies (if you use an existing Key Vault)

Open the access policy of the Key Vault and add the `Certificate management` access policy for the deployed application.

![image](https://user-images.githubusercontent.com/1356444/46597665-19f7e780-cb1c-11e8-9cb3-82e706d5dfd6.png)

## Usage

### Issuing a new certificate

Access `https://YOUR-FUNCTIONS.azurewebsites.net/add-certificate` with a browser and authenticate with Azure Active Directory and the Web UI will be displayed. Select the target domain from that screen, add the required subdomains, and run, and after a few tens of seconds, the certificate will be issued.

![Add certificate](https://user-images.githubusercontent.com/1356444/64176075-9b283d80-ce97-11e9-8ee7-02530d0c03f2.png)

If the `Access Control (IAM)` setting is not correct, nothing will be shown in the drop-down list.

### App Service (Web Apps / Functions / Containers)

You can import the Key Vault certificate to the App Service by opening the `TLS/SSL Settings` from Azure Portal and selecting the `Import Key Vault Certificate` button from the `Private Key Certificate (.pfx)`.

![image](https://user-images.githubusercontent.com/1356444/64438173-974c2380-d102-11e9-88c0-5ed34a5ce42a.png)

After importing, the App Service will automatically check for certificate updates.

### Application Gateway v2

- https://docs.microsoft.com/en-us/azure/application-gateway/key-vault-certs

### Azure CDN / Front Door

- https://docs.microsoft.com/en-us/azure/cdn/cdn-custom-ssl?tabs=option-2-enable-https-with-your-own-certificate
- https://docs.microsoft.com/en-us/azure/frontdoor/front-door-custom-domain-https#option-2-use-your-own-certificate

### API Management

- https://docs.microsoft.com/en-us/azure/api-management/configure-custom-domain

## Thanks

- [ACMESharp Core](https://github.com/PKISharp/ACMESharpCore) by @ebekker
- [Durable Functions](https://github.com/Azure/azure-functions-durable-extension) by @cgillum and contributors
- [DnsClient.NET](https://github.com/MichaCo/DnsClient.NET) by @MichaCo

## License

This project is licensed under the [Apache License 2.0](https://github.com/shibayan/keyvault-acmebot/blob/master/LICENSE)
