# Key Vault Acmebot

[![Build Status](https://dev.azure.com/shibayan/azure-acmebot/_apis/build/status/Build%20keyvault-acmebot?branchName=master)](https://dev.azure.com/shibayan/azure-acmebot/_build/latest?definitionId=38&branchName=master)
[![Release](https://img.shields.io/github/release/shibayan/keyvault-acmebot.svg)](https://github.com/shibayan/keyvault-acmebot/releases/latest)
[![License](https://img.shields.io/github/license/shibayan/keyvault-acmebot.svg)](https://github.com/shibayan/keyvault-acmebot/blob/master/LICENSE)

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
  - Azure Key Vault DNS name
- LetsEncrypt:Webhook
  - Webhook destination URL (optional, Slack recommend)

### 3. Add a access policy

Add the created Azure Function to the Key Vault `Certificate management` access policy.

![image](https://user-images.githubusercontent.com/1356444/46597665-19f7e780-cb1c-11e8-9cb3-82e706d5dfd6.png)


### 4. Assign role to Azure DNS

Assign `DNS Zone Contributor` role to Azure DNS.

## Usage

### Adding new certificate

Run `AddCertificate_HttpStart` function with parameters.

```sh
curl https://YOUR-FUNCTIONS.azurewebsites.net/api/AddCertificate_HttpStart?code=YOUR-FUNCTION-SECRET -X POST \
    -H 'Content-Type:application/json' \
    -d '{"Domains":["example.com","www.example.com"]}'
```

- Domains
  - DNS names to issue certificates.
  
## Thanks

- [ACMESharp Core](https://github.com/PKISharp/ACMESharpCore) by @ebekker
- [Durable Functions](https://github.com/Azure/azure-functions-durable-extension) by @cgillum and contributors
- [DnsClient.NET](https://github.com/MichaCo/DnsClient.NET) by @MichaCo

## License

This project is licensed under the [Apache License 2.0](https://github.com/shibayan/keyvault-acmebot/blob/master/LICENSE)
