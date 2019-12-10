# Key Vault Acmebot

[![Build Status](https://dev.azure.com/shibayan/azure-acmebot/_apis/build/status/Build%20keyvault-acmebot?branchName=master)](https://dev.azure.com/shibayan/azure-acmebot/_build/latest?definitionId=38&branchName=master)
[![Release](https://img.shields.io/github/release/shibayan/keyvault-acmebot.svg)](https://github.com/shibayan/keyvault-acmebot/releases/latest)
[![License](https://img.shields.io/github/license/shibayan/keyvault-acmebot.svg)](https://github.com/shibayan/keyvault-acmebot/blob/master/LICENSE)

This function provide easy automation of Let's Encrypt for Azure Key Vault. This project started to solve some problems.

- Store certificates securely with Key Vault
- Centrally manage many certificates with one Key Vault
- Simple deployment and configuration
- Robustness of implementation
- Easy monitoring (Application Insights, Webhook)

Use Key Vault for secure and centralized management of Let's Encrypt certificates.

## Table Of Contents

- [Feature Support](#feature-support)
- [Requirements](#requirements)
- [Getting Started](#getting-started)
- [Usage](#usage)
- [Thanks](#thanks)
- [License](#license)

## Feature Support

- All Azure App Service (Web Apps / Functions / Containers, any OS)
- Azure CDN / Front Door
- Azure Application Gateway v2
- Subject Alternative Names (SANs) certificates (multi-domains support)
- Zone Apex and Wildcard certificates

## Requirements

- Azure Subscription
- Azure DNS and Key Vault resource
- Email address (for Let's Encrypt account)

## Getting Started

### 1. Deploy to Azure Functions

<a href="https://portal.azure.com/#create/Microsoft.Template/uri/https%3A%2F%2Fraw.githubusercontent.com%2Fshibayan%2Fkeyvault-acmebot%2Fmaster%2Fazuredeploy.json" target="_blank">
  <img src="https://azuredeploy.net/deploybutton.png" />
</a>

### 2. Add application settings key

- LetsEncrypt:SubscriptionId
  - Azure Subscription Id
- LetsEncrypt:Contacts
  - Email address for Let's Encrypt account
- LetsEncrypt:VaultBaseUrl
  - Azure Key Vault DNS name (Only when using an existing Key Vault)
- LetsEncrypt:Webhook
  - Webhook destination URL (optional, Slack recommend)

### 3. Enable App Service Authentication (EasyAuth) with AAD

Open `Authentication / Authorization` from Azure Portal and turn on App Service Authentication. Then select `Log in with Azure Active Directory` as an action when not logging in.

![Enable App Service Authentication with AAD](https://user-images.githubusercontent.com/1356444/49693401-ecc7c400-fbb4-11e8-9ae1-5d376a4d8a05.png)

Set up Azure Active Directory provider by selecting `Express`.

![Create New Azure AD App](https://user-images.githubusercontent.com/1356444/49693412-6f508380-fbb5-11e8-81fb-6bbcbe47654e.png)

### 4. Assign role to Azure DNS

Assign `DNS Zone Contributor` role to Azure DNS zone or Resource Group.

![temp](https://user-images.githubusercontent.com/1356444/64354572-a9628f00-d03a-11e9-93c9-0c12992ca9bf.png)

### 5. Add a access policy (Only when using an existing Key Vault)

Add the created Azure Function to the Key Vault `Certificate management` access policy.

![image](https://user-images.githubusercontent.com/1356444/46597665-19f7e780-cb1c-11e8-9cb3-82e706d5dfd6.png)

## Usage

### Adding new certificate

Go to `https://YOUR-FUNCTIONS.azurewebsites.net/add-certificate`. Since the Web UI is displayed, if you select the target DNS zone and input domain and execute it, a certificate will be issued.

![Add certificate](https://user-images.githubusercontent.com/1356444/64176075-9b283d80-ce97-11e9-8ee7-02530d0c03f2.png)

If nothing is displayed in the dropdown, the IAM setting is incorrect.

### App Service (Web Apps / Functions / Containers)

Select "Import Key Vault Certificate" button to import the certificate from Key Vault into App Service.

![image](https://user-images.githubusercontent.com/1356444/64438173-974c2380-d102-11e9-88c0-5ed34a5ce42a.png)

After that, the certificate will automatically be renewed from Key Vault.

### Application Gateway v2

- https://docs.microsoft.com/en-us/azure/application-gateway/key-vault-certs

### Azure CDN / Front Door

- https://docs.microsoft.com/en-us/azure/cdn/cdn-custom-ssl?tabs=option-2-enable-https-with-your-own-certificate
- https://docs.microsoft.com/en-us/azure/frontdoor/front-door-custom-domain-https#option-2-use-your-own-certificate

## Thanks

- [ACMESharp Core](https://github.com/PKISharp/ACMESharpCore) by @ebekker
- [Durable Functions](https://github.com/Azure/azure-functions-durable-extension) by @cgillum and contributors
- [DnsClient.NET](https://github.com/MichaCo/DnsClient.NET) by @MichaCo

## License

This project is licensed under the [Apache License 2.0](https://github.com/shibayan/keyvault-acmebot/blob/master/LICENSE)
