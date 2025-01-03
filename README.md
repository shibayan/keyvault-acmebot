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
