data "azurerm_client_config" "current" {

}

data "azurerm_resource_group" "certbot" {
  name = "rg-wus2-cert-bot-01"
}

resource "random_string" "random" {
  length  = 4
  lower   = true
  upper   = false
  special = false
}

resource "random_uuid" "user_impersonation" {}

resource "random_uuid" "app_role_issue" {}

resource "random_uuid" "app_role_revoke" {}

resource "time_rotating" "default" {
  rotation_days = 180
}

resource "azurerm_key_vault" "default" {
  name                = "kv-acmebot-${random_string.random.result}"
  resource_group_name = data.azurerm_resource_group.certbot.name
  location            = data.azurerm_resource_group.certbot.location

  sku_name = "standard"

  enable_rbac_authorization = true
  tenant_id                 = data.azurerm_client_config.current.tenant_id
}

module "keyvault_acmebot" {
  source  = "shibayan/keyvault-acmebot/azurerm"
  version = "~> 3.0"

  app_base_name        = var.app_base_name
  resource_group_name  = data.azurerm_resource_group.certbot.name
  location             = data.azurerm_resource_group.certbot.location
  mail_address         = "anil.aluru@networkneil.dev"
  vault_uri            = azurerm_key_vault.default.vault_uri
  allowed_ip_addresses = var.home_ips

  additional_app_settings = {
    MICROSOFT_PROVIDER_AUTHENTICATION_SECRET = "@Microsoft.KeyVault(SecretUri=https://kv-acmebot-25xy.vault.azure.net/secrets/func-nn-certbot/f80152df47424e70a5459f95d1913691)"
  }

  auth_settings = {
    enabled = true
    active_directory = {
      client_id                  = "4628b810-2735-44ff-a833-19b296a7958d"
      client_secret_setting_name = "MICROSOFT_PROVIDER_AUTHENTICATION_SECRET"
      client_secret              = ""
      tenant_auth_endpoint       = "https://login.microsoftonline.com/${data.azurerm_client_config.current.tenant_id}/v2.0/"
    }
  }

  cloudflare = {
    api_token = var.cloudflare.api_token
  }

  azure_dns = {
    subscription_id = data.azurerm_client_config.current.subscription_id
  }
}

resource "azurerm_role_assignment" "func_kv_1" {
  scope                = azurerm_key_vault.default.id
  role_definition_name = "Key Vault Certificates Officer"
  principal_id         = module.keyvault_acmebot.principal_id
}

resource "azurerm_role_assignment" "func_kv_2" {
  scope                = azurerm_key_vault.default.id
  role_definition_name = "Key Vault Secrets User"
  principal_id         = module.keyvault_acmebot.principal_id
}