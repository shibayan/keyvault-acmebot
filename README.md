# Azure Key Vault Let's Encrypt

[![Build status](https://ci.appveyor.com/api/projects/status/sr0okt6mld0ufkcd?svg=true)](https://ci.appveyor.com/project/shibayan/azure-keyvault-letsencrypt)

## Requirements

- Azure Subscription
- Azure DNS and Key Vault resource
- Email address (for Let's Encrypt account)

## Getting Started

### 1. Deploy to Azure Functions

<a href="https://portal.azure.com/#create/Microsoft.Template/uri/https%3A%2F%2Fraw.githubusercontent.com%2Fshibayan%2Fazure-keyvault-letsencrypt%2Fmaster%2Fazuredeploy.json" target="_blank">
  <img src="https://azuredeploy.net/deploybutton.png" />
</a>

### 2. Add application settings key

- LetsEncrypt:SubscriptionId
  - Azure Subscription Id
- LetsEncrypt:Contacts
  - Email address for Let's Encrypt account
- LetsEncrypt:VaultBaseName
  - Azure Key Vault DNS name

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

## License

This project is licensed under the [Apache License 2.0](https://github.com/shibayan/azure-keyvault-letsencrypt/blob/master/LICENSE)
